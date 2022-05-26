using System.Diagnostics;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("{Name}, Size={Size}")]
public sealed class SourceFileCompilandContribution : Contribution
{
    public Compiland Compiland { get; }
    public SourceFile SourceFile { get; }

    internal SourceFileCompilandContribution(string name, Compiland compiland, SourceFile sourceFile)
        : base(name)
    {
        this.Compiland = compiland;
        this.SourceFile = sourceFile;
    }
}
