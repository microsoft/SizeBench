namespace SizeBench.AnalysisEngine.Symbols;

// Represents an IMAGE_THUNK_DATA32 or IMAGE_THUNK_DATA64
internal sealed class ImportThunkSymbol : ImportSymbolBase
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.ImportThunk;

    internal ImportThunkSymbol(uint rva, uint size, ushort ordinal, string importDescriptorName, string? thunkName, SymbolSourcesSupported symbolSourcesSupported)
        : base(rva, size, ConjureName(ordinal, importDescriptorName, thunkName), symbolSourcesSupported)
    { }

    private static string ConjureName(ushort ordinal, string importDescriptorName, string? thunkName)
    {
        if (thunkName == null)
        {
            return $"[import thunk] {importDescriptorName} Ordinal {ordinal}";
        }
        else
        {
            return $"[import thunk] {importDescriptorName} {thunkName}, ordinal {ordinal}";
        }
    }
}
