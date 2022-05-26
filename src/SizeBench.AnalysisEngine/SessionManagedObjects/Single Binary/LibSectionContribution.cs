namespace SizeBench.AnalysisEngine;

public sealed class LibSectionContribution : Contribution
{
    public BinarySection BinarySection { get; }
    public Library Lib { get; }

    internal LibSectionContribution(string name, BinarySection binarySection, Library lib)
        : base(name)
    {
        this.BinarySection = binarySection;
        this.Lib = lib;
    }
}
