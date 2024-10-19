using Dia2Lib;

namespace SizeBench.AnalysisEngine.Symbols;

// These types are for how we attempt to deal with folding in the binary - two types of things can fold: DIA symbols, and XDATA symbols.
// DIA symbols fold when /OPT:ICF is in use, and what results is multiple names and SymIndexIDs for the same RVA.
// XDATA symbols fold when the same unwinding data can be used for multiple functions, and what results is multiple TargetSymbolRVAs for the same XDATA record RVA.
internal sealed class NameCanonicalization
{
    private bool _hasEverHadAnyNonPublicSymbolNameAdded;
    private bool _hasEverHadAnyNonThunkSymbolNameAdded;
    private readonly List<(uint symIndexId, SymTagEnum symTag, string name)> _namesBySymIndexId = new();
    public IReadOnlyList<(uint symIndexId, SymTagEnum symTag, string name)> NamesBySymIndexID => this._namesBySymIndexId;
    public uint CanonicalSymIndexID { get; private set; }
    public string CanonicalName { get; private set; } = String.Empty;

    public bool IsNameEvenGoingToBeConsidered(SymTagEnum symTag)
    {
        // PublicSymbols have ugly names that don't sort well because they start with things like "public:" and "virtual" and include the function's
        // return type.  We don't want any of that, so if we are getting a PublicSymbol but we've seen any non-public symbol we don't want this name,
        // just throw it away.
        return !((symTag == SymTagEnum.SymTagPublicSymbol && this._hasEverHadAnyNonPublicSymbolNameAdded) ||
                 (symTag == SymTagEnum.SymTagThunk && this._hasEverHadAnyNonThunkSymbolNameAdded));
    }

    public bool AddName(uint symIndexId, SymTagEnum symTag, IDiaSymbol? diaSymbol = null, IDiaSession? diaSession = null, SessionDataCache? dataCache = null,
                        string? name = null, Func<IDiaSymbol, IDiaSession, SessionDataCache, string>? nameCreator = null)
    {
        // PublicSymbols have ugly names that don't sort well because they start with things like "public:" and "virtual" and include the function's
        // return type.  We don't want any of that, so if we are getting a PublicSymbol but we've seen any non-public symbol we don't want this name,
        // just throw it away.
        if (!IsNameEvenGoingToBeConsidered(symTag))
        {
            return false;
        }

        if (name is null)
        {
            ArgumentNullException.ThrowIfNull(nameCreator);
            ArgumentNullException.ThrowIfNull(diaSymbol);
            ArgumentNullException.ThrowIfNull(diaSession);
            ArgumentNullException.ThrowIfNull(dataCache);

            name = nameCreator(diaSymbol, diaSession, dataCache);
        }

        // If we are seeing the same name a second time, it's not going to help later so we'll discard it.  Sometimes the same name appears with multiple
        // SymIndexIDs which is annoying but as far as I know harmless.
        for (var i = 0; i < this._namesBySymIndexId.Count; i++)
        {
            if (this._namesBySymIndexId[i].name.Equals(name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        this._namesBySymIndexId.Add((symIndexId, symTag, name));
        this._hasEverHadAnyNonPublicSymbolNameAdded |= symTag != SymTagEnum.SymTagPublicSymbol;
        this._hasEverHadAnyNonThunkSymbolNameAdded |= symTag != SymTagEnum.SymTagThunk;
        return true;
    }

    public void Canonicalize()
    {
        uint? canonicalSymIndexId = null;
        SymTagEnum? canonicalSymTag = null;
        string? canonicalName = null;

        foreach ((var symIndex, var symTag, var name) in this._namesBySymIndexId)
        {
            // If we find a "[thunk]" we want to avoid using that as the canonical name if we can, because the real name of the function is
            // after that prefix, so we take that out of the name if it's there.
            var nameToCompare = name.AsSpan();
            if (symTag is SymTagEnum.SymTagThunk)
            {
                if (nameToCompare.StartsWith("[thunk]:", StringComparison.Ordinal))
                {
                    nameToCompare = nameToCompare["[thunk]:".Length..];
                }
                if (nameToCompare.StartsWith("[thunk]", StringComparison.Ordinal))
                {
                    nameToCompare = nameToCompare["[thunk]".Length..];
                }
                nameToCompare = nameToCompare.TrimStart();
            }

            // If the canonical sym tag is a block, but we've found a function at the same RVA, we want to use the function name instead.
            if (canonicalName is null || 
                (canonicalSymTag is SymTagEnum.SymTagBlock && symTag == SymTagEnum.SymTagFunction) ||
                canonicalName.AsSpan().CompareTo(nameToCompare, StringComparison.Ordinal) > 0)
            {
                canonicalSymIndexId = symIndex;
                canonicalSymTag = symTag;
                canonicalName = nameToCompare.ToString();
            }
        }

        if (canonicalSymIndexId != null && canonicalName != null)
        {
            this.CanonicalSymIndexID = canonicalSymIndexId.Value;
            this.CanonicalName = canonicalName;
        }
    }
}