namespace SizeBench.AnalysisEngine;

public sealed record class SessionOptions
{
    public SymbolSourcesSupported SymbolSourcesSupported { get; init; } = SymbolSourcesSupported.All;
}
