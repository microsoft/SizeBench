namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class IpToStateMapSymbol : XDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.IpToStateMap;

    internal IpToStateMapSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size)
        : base(targetSymbol, targetStartRVA, rva, size)
    {
    }

    internal override string SymbolPrefix => "[ip2state]";
}
