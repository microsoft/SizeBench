namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class CppXdataSymbol : XDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.CppXdata;

    public CppXdataSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size) :
        base(targetSymbol, targetStartRVA, rva, size)
    {
    }

    internal override string SymbolPrefix => "[cppxdata]";
}
