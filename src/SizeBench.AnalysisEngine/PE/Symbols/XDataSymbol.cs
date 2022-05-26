namespace SizeBench.AnalysisEngine.Symbols;

internal abstract class XDataSymbol : EHSymbolBase
{
    internal XDataSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size)
        : base(targetSymbol, targetStartRVA, rva, size)
    {
    }
}
