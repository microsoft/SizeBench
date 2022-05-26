namespace SizeBench.AnalysisEngine;

public sealed class LibCOFFGroupContribution : Contribution
{
    public COFFGroup COFFGroup { get; }
    public Library Lib { get; }

    internal LibCOFFGroupContribution(string name, COFFGroup coffGroup, Library lib)
        : base(name)
    {
        this.COFFGroup = coffGroup;
        this.Lib = lib;
    }
}
