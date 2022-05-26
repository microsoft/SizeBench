using System.Globalization;

namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class CustomTypeSymbol : TypeSymbol
{
    internal CustomTypeSymbol(SessionDataCache cache,
                              string oemName,
                              uint oemSymbolId,
                              uint instanceSize,
                              uint symIndexId) : base(cache, ConjureName(oemName, oemSymbolId), instanceSize, symIndexId)
    { }

    private static string ConjureName(string oemName, uint oemSymbolId) =>
        $"[{oemName} defined type 0x{oemSymbolId.ToString("X", CultureInfo.InvariantCulture)}]";

    public override bool CanLoadLayout => false;

    // To properly check if two CustomTypes are likely the same, I think we need to look at the IDiaSymbol::get_dataBytes and/or
    // the get_types.  Both of those aren't marshalling well now, so we'll just live with the diffs being much noisier on these
    // symbols until that can be addressed.
    internal override bool IsVeryLikelyTheSameAs(TypeSymbol otherSymbol) => false;
}
