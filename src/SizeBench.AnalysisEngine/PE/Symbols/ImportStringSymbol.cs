namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class ImportStringSymbol : ImportSymbolBase
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.ImportString;

    internal ImportStringSymbol(uint rva, uint length, string str, SymbolSourcesSupported symbolSourcesSupported)
        : base(rva, length, str, symbolSourcesSupported)
    { }
}
