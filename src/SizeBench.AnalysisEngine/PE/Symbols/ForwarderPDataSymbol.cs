namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class ForwarderPDataSymbol : PDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.ForwarderPData;

    internal ForwarderPDataSymbol(uint targetStartRVA, uint rva, uint size, SymbolSourcesSupported symbolSourcesSupported)
        : base(targetStartRVA, 0, rva, size, symbolSourcesSupported)
    {
    }

    internal override string SymbolPrefix => "[forwarder-pdata]";
}
