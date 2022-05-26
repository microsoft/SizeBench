namespace SizeBench.AnalysisEngine;

public sealed class SymbolEnumerationOptions
{
    // When enumerating symbols, do not look for data symbols or pdata or xdata, just code symbols (things that are executable)
    public bool OnlyCodeSymbols { get; set; }
}
