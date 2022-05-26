using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

// DataSymbol with LocationType == LocIsStatic
[DebuggerDisplay("Static Data Symbol Name={Name}, Size={Size}")]
public sealed class StaticDataSymbol : Symbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.StaticData;

    internal override bool CanBeFolded => true;

    internal DataKind DataKind { get; }

    public Compiland? CompilandReferencedIn { get; }

    public IFunctionCodeSymbol? ParentFunction { get; }

    internal readonly TypeSymbol? Type;

    internal StaticDataSymbol(SessionDataCache cache,
                              string name,
                              uint rva,
                              uint size,
                              bool isVirtualSize,
                              uint symIndexId,
                              DataKind dataKind,
                              TypeSymbol? type,
                              Compiland? referencedIn,
                              IFunctionCodeSymbol? functionParent) : base(cache, GetName(name, functionParent), rva, size, isVirtualSize, symIndexId)
    {
        Debug.Assert(dataKind is DataKind.DataIsFileStatic or DataKind.DataIsGlobal or DataKind.DataIsStaticLocal);

        this.DataKind = dataKind;
        this.Type = type;
        this.CompilandReferencedIn = referencedIn;
        this.ParentFunction = functionParent;
    }

    private static string GetName(string name, IFunctionCodeSymbol? functionParent)
    {
        if (functionParent is null)
        {
            return name;
        }
        else
        {
            return $"{name} (in {functionParent.FullName})";
        }
    }

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        return otherSymbol is StaticDataSymbol other &&
               this.DataKind == other.DataKind &&
               TypesAreSame(other) &&
               ParentFunctionsAreSame(other) &&
               base.IsVeryLikelyTheSameAs(otherSymbol);
    }

    private bool ParentFunctionsAreSame(StaticDataSymbol other)
    {
        if ((this.ParentFunction is null) != (other.ParentFunction is null))
        {
            return false;
        }

        // If both parent functions are null, they're obviously the same
        if (this.ParentFunction is null || other.ParentFunction is null)
        {
            return true;
        }

        return this.ParentFunction.IsVeryLikelyTheSameAs(other.ParentFunction);
    }

    private bool TypesAreSame(StaticDataSymbol other)
    {
        // Data Symbols rather commonly have generic names - a good example is "_TlgEvent" used by the 
        // TraceLoggingWrite system in Windows.  These are examples but they highlight a common pattern
        // that we should use when determining 'sameness' to improve diffing.  So we'll take
        // into account the name of the function/type that contains the symbol - but this is tricky since often
        // the type name can include a lambda (compiler-generated name) or it can include some __COUNTER__ stuff
        // or things like that.  So just comparing this.Type.Name would be pointless.  We need to be a bit more
        // fancy.  As a start, we'll look at just the part of the Type.Name before the first "::" (if one exists)

        // An example from Windows.UI.Xaml.dll to leave here to help clarify for future readers is a _TlgEvent 
        // with this Type.Name:
        //       TraceForFailFast::__l7::<unnamed-type-_TlgEvent>
        // which comes from the "TraceForFailFast" function, but all the "<unnamed-type-..." is not going to be
        // helpful in diffing.  This heuristic is probably too simple, as namespaces could be involved, but it's
        // a starting point and can be improved as more examples crop up.

        if ((this.Type is null) != (other.Type is null))
        {
            return false;
        }

        // If both types are null, they're obviously the same
        if (this.Type is null || other.Type is null)
        {
            return true;
        }

        if (this.Type.IsVeryLikelyTheSameAs(other.Type))
        {
            return true;
        }

        // If the types weren't "very likely the same" then we get to the messy part - see the big comment above.
        var colonColonIndex = this.Type.Name.IndexOf("::", StringComparison.Ordinal);
        var thisTypeNameBeforeColonColon = colonColonIndex == -1 ? this.Type.Name : this.Type.Name[..colonColonIndex];
        colonColonIndex = other.Type.Name.IndexOf("::", StringComparison.Ordinal);
        var otherTypeNameBeforeColonColon = colonColonIndex == -1 ? other.Type.Name : other.Type.Name[..colonColonIndex];

        return String.Equals(thisTypeNameBeforeColonColon, otherTypeNameBeforeColonColon, StringComparison.Ordinal);
    }
}
