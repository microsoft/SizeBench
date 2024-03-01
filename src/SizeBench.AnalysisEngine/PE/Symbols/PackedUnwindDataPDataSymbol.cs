namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class PackedUnwindDataPDataSymbol : PDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.PackedUnwindDataPData;

    internal PackedUnwindDataPDataSymbol(uint targetStartRVA, uint rva, uint size, SymbolSourcesSupported symbolSourcesSupported)
        : base(targetStartRVA, 0, rva, size, symbolSourcesSupported)
    {
    }

    internal override string SymbolPrefix => "[packedUnwindData-pdata]";
}
