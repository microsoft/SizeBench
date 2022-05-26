namespace SizeBench.AnalysisEngine.Symbols;

// Values should exactly match CV_access_e in DIA
#pragma warning disable CA1008 // Enums should have zero value - there's no zero value in DIA, so we should not have one either.
public enum AccessModifier
#pragma warning restore CA1008 // Enums should have zero value
{
    Private = 1,
    Protected = 2,
    Public = 3
}
