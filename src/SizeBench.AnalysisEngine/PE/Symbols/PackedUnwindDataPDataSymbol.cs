namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class PackedUnwindDataPDataSymbol : PDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.PackedUnwindDataPData;

    internal PackedUnwindDataPDataSymbol(uint targetStartRVA, uint rva, uint size)
        : base(targetStartRVA, 0, rva, size)
    {
    }

    internal override string SymbolPrefix => "[packedUnwindData-pdata]";
}
