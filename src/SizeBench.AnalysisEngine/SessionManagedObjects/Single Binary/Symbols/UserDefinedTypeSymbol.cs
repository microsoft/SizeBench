using System.Diagnostics;
using SizeBench.AnalysisEngine.DIAInterop;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("User-Defined Type Symbol Name={Name}")]
public sealed class UserDefinedTypeSymbol : TypeSymbol
{
    #region Base Types and Derived Types

    // Calculating what base types this type has can be somewhat expensive and not everyone who looks at a UDT cares whether the base type
    // info is available.  So it's optional and can be populated on-demand.

    [DebuggerDisplay("Base Type: {_baseTypeSymbol.Name}, Offset = {_offset}")]
    internal readonly struct BaseType
    {
        internal readonly UserDefinedTypeSymbol _baseTypeSymbol;
        internal readonly uint _offset;

        internal BaseType(UserDefinedTypeSymbol baseType, uint offset)
        {
            this._baseTypeSymbol = baseType;
            this._offset = offset;
        }
    }

    private bool _areBaseTypesLoaded;

    private List<BaseType>? _baseTypes;
    internal List<BaseType>? BaseTypes
    {
        get
        {
            if (!this._areBaseTypesLoaded)
            {
                throw new InvalidOperationException($"Trying to access {nameof(this.BaseTypes)} but you haven't called {nameof(LoadBaseTypes)} yet!");
            }

            return this._baseTypes;
        }
    }

    // This loads all the base types for this type, and all of their base types and so on for the entire hierarchy up to the root
    internal void LoadBaseTypes(SessionDataCache dataCache, IDIAAdapter diaAdapter, CancellationToken cancellationToken)
    {
        if (this._areBaseTypesLoaded)
        {
            return;
        }

        foreach ((var baseTypeId, var offset) in diaAdapter.FindAllBaseTypeIDsForUDT(this))
        {
            this._baseTypes ??= new List<BaseType>();

            if (dataCache.AllTypesBySymIndexId.TryGetValue(baseTypeId, out var baseTypeSymbol))
            {
                // The code hasn't been written yet to handle a base type that's not a UDT - is it possible to have any other type
                // of base type?
                if (baseTypeSymbol is not UserDefinedTypeSymbol baseTypeUDT)
                {
                    throw new InvalidOperationException($"Something has gone wrong, a UserDefinedType's base type has been found to be a {baseTypeSymbol.GetType().Name}...");
                }

                baseTypeUDT.LoadBaseTypes(dataCache, diaAdapter, cancellationToken);
                this._baseTypes.Add(new BaseType(baseTypeUDT, offset));
            }
            else
            {
                var newUDT = diaAdapter.FindTypeSymbolBySymIndexId<UserDefinedTypeSymbol>(baseTypeId, cancellationToken)
                    ?? throw new InvalidOperationException("Something went wrong loading a base type...");

                newUDT.LoadBaseTypes(dataCache, diaAdapter, cancellationToken);
                this._baseTypes.Add(new BaseType(newUDT, offset));
            }
        }

        this._areBaseTypesLoaded = true;
    }

    private bool _areDerivedTypesLoaded;
    private HashSet<uint>? _derivedTypeSymIndexIds;
    internal int DerivedTypeCount
    {
        get
        {
            if (!this._areDerivedTypesLoaded)
            {
                throw new InvalidOperationException($"Trying to access {nameof(this.DerivedTypeCount)} but you haven't yet ensured all derived clasess are loaded!");
            }

            return this._derivedTypeSymIndexIds?.Count ?? 0;
        }
    }
    internal IEnumerable<UserDefinedTypeSymbol> EnumerateDerivedTypes(IDIAAdapter diaAdapter, CancellationToken cancellationToken)
    {
        if (!this._areDerivedTypesLoaded)
        {
            throw new InvalidOperationException($"Trying to call {nameof(this.EnumerateDerivedTypes)} but you haven't yet ensured all derived clasess are loaded!");
        }

        if (this._derivedTypeSymIndexIds is null)
        {
            yield break;
        }

        foreach (var derivedSymIndexId in this._derivedTypeSymIndexIds)
        {
            yield return diaAdapter.FindTypeSymbolBySymIndexId<UserDefinedTypeSymbol>(derivedSymIndexId, cancellationToken);
        }
    }

    internal void AddDerivedType(UserDefinedTypeSymbol typeDerivedFromThisOne)
    {
        // If someone tries to call this to add a derived type that we arleady know about, we'll let that slide even
        // if AreDerivedClassesLoaded == true, it makes the calling code simpler to write.
        this._derivedTypeSymIndexIds ??= new HashSet<uint>();
        if (this._derivedTypeSymIndexIds.Add(typeDerivedFromThisOne.SymIndexId))
        {
            if (this._areDerivedTypesLoaded)
            {
                throw new InvalidOperationException("Can't add a derived type after the type has set AreDerivedTypesLoaded==true");
            }
        }
    }

    // We don't just have a property setter because we only want this to be able to go from false->true, never the other direction.
    internal void MarkDerivedTypesLoaded()
    {
        this._areDerivedTypesLoaded = true;
    }

    #endregion

    #region Functions

    // Loading functions is expensive so we defer it until somebody needs it since many callers don't care about all the functions
    // on a UDT.

    private List<IFunctionCodeSymbol>? _functions;

    // This is internal and must only be called on the DIA thread, but it's very convenient for other things already on the DIA thread to be
    // able to get synchronous access to Functions as a property instead of awaiting every time they want to load this.
    internal List<IFunctionCodeSymbol> Functions
    {
        get
        {
            EnsureFunctionsLoaded(CancellationToken.None);

            return this._functions!;
        }
    }

    public async ValueTask<IReadOnlyList<IFunctionCodeSymbol>> GetFunctionsAsync(CancellationToken token)
    {
        this._functions ??= (await this._session.EnumerateFunctionsFromUserDefinedType(this, token).ConfigureAwait(true)).ToList();

        return this._functions;
    }

    internal void EnsureFunctionsLoaded(CancellationToken cancellationToken)
    {
        if (this._functions != null)
        {
            return;
        }

        this._functions = this._diaAdapter.FindAllFunctionsWithinUDT(this.SymIndexId, cancellationToken).ToList();
        this._functions.TrimExcess();
    }

    #endregion

    #region Data Members

    private bool _areDataMembersLoaded;

    private MemberDataSymbol[]? _dataMembers;
    internal MemberDataSymbol[] DataMembers
    {
        get
        {
            EnsureDataMembersLoaded(CancellationToken.None);

            return this._dataMembers!;
        }
    }

    internal void EnsureDataMembersLoaded(CancellationToken cancellationToken)
    {
        if (this._areDataMembersLoaded)
        {
            return;
        }

        this._dataMembers = this._diaAdapter.FindAllMemberDataSymbolsWithinUDT(this, cancellationToken).ToArray();

        this._areDataMembersLoaded = true;
    }

    #endregion

    #region VTableCount

    private bool _isVTableCountLoaded;

    private byte _vtableCount;
    internal byte VTableCount
    {
        get
        {
            EnsureVTableCountLoaded();

            return this._vtableCount;
        }
    }

    internal void EnsureVTableCountLoaded()
    {
        if (this._isVTableCountLoaded)
        {
            return;
        }

        this._vtableCount = this._diaAdapter.FindCountOfVTablesWithin(this.SymIndexId);

        this._isVTableCountLoaded = true;
    }

    #endregion

    internal readonly UserDefinedTypeKind _userDefinedTypeKind;
    private readonly IDIAAdapter _diaAdapter;
    private readonly ISession _session;

    internal UserDefinedTypeSymbol(SessionDataCache dataCache,
                                   IDIAAdapter diaAdapter,
                                   ISession session,
                                   string name,
                                   uint instanceSize,
                                   uint symIndexId,
                                   UserDefinedTypeKind udtKind) : base(dataCache, name, instanceSize, symIndexId)
    {
        this._userDefinedTypeKind = udtKind;
        this._diaAdapter = diaAdapter;
        this._session = session;
    }

    public override bool CanLoadLayout => true;
}

internal static class UserDefinedTypeSymbolExtensions
{
    internal static void LoadAllBaseTypes(this List<UserDefinedTypeSymbol> udts,
                                          SessionDataCache dataCache,
                                          IDIAAdapter diaAdapter,
                                          CancellationToken cancellationToken,
                                          Action<string, uint, uint?> progressReporter)
    {
        const int loggerOutputVelocity = 100;
        uint nextLoggerOutput = loggerOutputVelocity;
        var udtsEnumerated = 0;

        for (var i = 0; i < udts.Count; i++)
        {
            udtsEnumerated++;
            if (udtsEnumerated >= nextLoggerOutput)
            {
                progressReporter($"Base type information loaded for {udtsEnumerated:N0}/{udts.Count:N0} user-defined types so far.", nextLoggerOutput, (uint)udts.Count);
                nextLoggerOutput += loggerOutputVelocity;
            }

            cancellationToken.ThrowIfCancellationRequested();

            udts[i].LoadBaseTypes(dataCache, diaAdapter, cancellationToken);
        }
    }

    internal static void LoadAllDerivedTypes(this List<UserDefinedTypeSymbol> udts,
                                             CancellationToken cancellationToken,
                                             Action<string, uint, uint?> progressReporter)
    {
        const int loggerOutputVelocity = 100;
        uint nextLoggerOutput = loggerOutputVelocity;
        var udtsEnumerated = 0;

        foreach (var udt in udts)
        {
            udtsEnumerated++;
            if (udtsEnumerated >= nextLoggerOutput)
            {
                progressReporter($"Derived type information processed for {udtsEnumerated:N0}/{udts.Count:N0} user-defined types so far.", nextLoggerOutput, (uint)udts.Count);
                nextLoggerOutput += loggerOutputVelocity;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // If this has any base types, then add this as a derived type to all the bases (and their bases, and so on)
            if (udt.BaseTypes != null)
            {
                AddDerivedTypeToBaseTypes(udt, udt.BaseTypes);
            }
        }

        foreach (var udt in udts)
        {
            udt.MarkDerivedTypesLoaded();
        }
    }

    private static void AddDerivedTypeToBaseTypes(UserDefinedTypeSymbol derivedType, List<UserDefinedTypeSymbol.BaseType> baseTypes)
    {
        foreach (var baseType in baseTypes)
        {
            baseType._baseTypeSymbol.AddDerivedType(derivedType);

            if (baseType._baseTypeSymbol.BaseTypes != null)
            {
                AddDerivedTypeToBaseTypes(derivedType, baseType._baseTypeSymbol.BaseTypes);
            }
        }
    }
}
