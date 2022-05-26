namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class HandlerMapSymbol : XDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.HandlerMap;

    internal HandlerMapSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size)
        : base(targetSymbol, targetStartRVA, rva, size)
    {
    }

    internal override string SymbolPrefix => "[handlerMap]";
}
