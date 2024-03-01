namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class UnwindInfoSymbol : XDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.UnwindInfo;

    internal UnwindInfoSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size, SymbolSourcesSupported symbolSourcesSupported)
        : base(targetSymbol, targetStartRVA, rva, size, symbolSourcesSupported)
    {
    }

    internal override string SymbolPrefix => "[unwind]";
}
