using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

internal abstract class XDataSymbol : EHSymbolBase
{
    internal XDataSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size, SymbolSourcesSupported symbolSourcesSupported)
        : base(targetSymbol, targetStartRVA, rva, size)
    {
        Debug.Assert(symbolSourcesSupported.HasFlag(SymbolSourcesSupported.XDATA));
    }
}
