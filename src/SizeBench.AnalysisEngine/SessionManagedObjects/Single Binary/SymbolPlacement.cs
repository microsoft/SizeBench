namespace SizeBench.AnalysisEngine;

public sealed class SymbolPlacement
{
    public BinarySection? BinarySection { get; }
    public COFFGroup? COFFGroup { get; }
    public Library? Lib { get; }
    public Compiland? Compiland { get; }
    public SourceFile? SourceFile { get; }

    internal SymbolPlacement(BinarySection? section, COFFGroup? coffGroup,
                             Library? lib, Compiland? compiland,
                             SourceFile? sourceFile)
    {
        this.BinarySection = section;
        this.COFFGroup = coffGroup;
        this.Lib = lib;
        this.Compiland = compiland;
        this.SourceFile = sourceFile;
    }
}
