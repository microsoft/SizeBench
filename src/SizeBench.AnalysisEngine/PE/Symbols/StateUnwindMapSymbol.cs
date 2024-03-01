namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class StateUnwindMapSymbol : XDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.StateUnwindMap;

    internal StateUnwindMapSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size, SymbolSourcesSupported symbolSourcesSupported)
        : base(targetSymbol, targetStartRVA, rva, size, symbolSourcesSupported)
    {
    }

    internal override string SymbolPrefix => "[stateUnwindMap]";
}
