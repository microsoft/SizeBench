using SizeBench.AnalysisEngine.Symbols;

internal sealed class ImportStringSymbol : ImportSymbolBase
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.ImportString;

    internal ImportStringSymbol(uint rva, uint length, string str)
        : base(rva, length, str)
    { }
}
