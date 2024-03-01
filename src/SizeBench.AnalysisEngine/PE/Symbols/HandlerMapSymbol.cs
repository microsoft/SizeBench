namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class HandlerMapSymbol : XDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.HandlerMap;

    internal HandlerMapSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size, SymbolSourcesSupported symbolSourcesSupported)
        : base(targetSymbol, targetStartRVA, rva, size, symbolSourcesSupported)
    {
    }

    internal override string SymbolPrefix => "[handlerMap]";
}
