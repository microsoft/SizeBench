using System.Diagnostics;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("{Name}, Size={Size}")]
public sealed class SourceFileCOFFGroupContribution : Contribution
{
    public COFFGroup COFFGroup { get; }
    public SourceFile SourceFile { get; }

    internal SourceFileCOFFGroupContribution(string name, COFFGroup coffGroup, SourceFile sourceFile)
        : base(name)
    {
        this.COFFGroup = coffGroup;
        this.SourceFile = sourceFile;
    }
}
