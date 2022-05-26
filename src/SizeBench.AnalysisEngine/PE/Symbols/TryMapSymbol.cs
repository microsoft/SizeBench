namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class TryMapSymbol : XDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.TryMap;

    internal TryMapSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size)
        : base(targetSymbol, targetStartRVA, rva, size)
    {
    }

    internal override string SymbolPrefix => "[tryMap]";
}
