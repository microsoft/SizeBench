﻿namespace SizeBench.AnalysisEngine.Symbols;

// Represents an IMAGE_IMPORT_BY_NAME structure
internal sealed class ImportByNameSymbol : ImportSymbolBase
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.ImportByName;

    internal string ImportDescriptorName { get; }
    internal ushort Ordinal { get; }

    internal ImportByNameSymbol(uint rva, uint size, ushort ordinal, string importDescriptorName, string thunkName, SymbolSourcesSupported symbolSourcesSupported)
        : base(rva, size, $"`string': \"{thunkName}\"", symbolSourcesSupported)
    {
        this.Ordinal = ordinal;
        this.ImportDescriptorName = importDescriptorName;
    }
}
