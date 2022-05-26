using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("Annotation: {SourceFileName}, Line {LineNumber}, Text={Text}")]
public sealed class AnnotationSymbol
{
    public string Name { get; }
    public uint Size { get; }
    // This is the RVA that is being annotated, the RVA itself does not take up any space in the binary (it's only in the PDB) so it does not
    // exist in the binary or take up any space there on-disk or in memory.
    public uint AnnotatingRVA { get; }
    public string Text { get; }
    public SourceFile? SourceFile { get; }
    public string SourceFileName => this.SourceFile?.ShortName ?? "unknown source file";
    public uint LineNumber { get; }

    internal uint SymIndexId;

    // This is true if the annotation is either in an inlined function, or is annotating a call to an inlined function (we can't yet
    // tell the difference)
    public bool IsInlinedOrAnnotatingInlineSite { get; }

    internal AnnotationSymbol(SessionDataCache cache,
                              string text,
                              SourceFile? sourceFile,
                              uint lineNumber,
                              bool isInlinedOrAnnotatingInlineSite,
                              uint symIndexId)
    {
#if DEBUG
        if (cache.AllAnnotationsBySymIndexId.ContainsKey(symIndexId) == true)
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.Name = $"Annotation on {sourceFile?.ShortName ?? "unknown source file"}, line {lineNumber}";
        this.Text = text;
        this.SourceFile = sourceFile;
        this.LineNumber = lineNumber;
        this.IsInlinedOrAnnotatingInlineSite = isInlinedOrAnnotatingInlineSite;

        cache.AllAnnotationsBySymIndexId.Add(symIndexId, this);
    }
}
