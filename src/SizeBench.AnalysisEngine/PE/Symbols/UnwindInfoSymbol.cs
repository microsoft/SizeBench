namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class UnwindInfoSymbol : XDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.UnwindInfo;

    internal UnwindInfoSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size)
        : base(targetSymbol, targetStartRVA, rva, size)
    {
    }

    internal override string SymbolPrefix => "[unwind]";
}
