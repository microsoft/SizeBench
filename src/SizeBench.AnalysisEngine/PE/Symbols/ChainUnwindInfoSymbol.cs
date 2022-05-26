namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class ChainUnwindInfoSymbol : XDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.ChainUnwindInfo;

    internal ChainUnwindInfoSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size)
        : base(targetSymbol, targetStartRVA, rva, size)
    {
    }

    internal override string SymbolPrefix => "[chain-unwind]";
}
