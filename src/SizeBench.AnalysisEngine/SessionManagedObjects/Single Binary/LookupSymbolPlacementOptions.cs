namespace SizeBench.AnalysisEngine;

public sealed record LookupSymbolPlacementOptions(
    bool IncludeBinarySectionAndCOFFGroup = true,
    bool IncludeLibAndCompiland = true,
    bool IncludeSourceFile = true);