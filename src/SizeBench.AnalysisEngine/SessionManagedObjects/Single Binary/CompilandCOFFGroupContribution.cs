namespace SizeBench.AnalysisEngine;

public sealed class CompilandCOFFGroupContribution : Contribution
{
    public COFFGroup COFFGroup { get; }
    public Compiland Compiland { get; }

    internal CompilandCOFFGroupContribution(string name, COFFGroup coffGroup, Compiland compiland)
        : base(name)
    {
        this.COFFGroup = coffGroup;
        this.Compiland = compiland;
    }
}
