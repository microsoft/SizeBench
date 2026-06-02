using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class PEDirectorySymbol : ISymbol
{
    private string DebuggerDisplay => $"{GetType().Name}: {this.Name}";

    public string Name { get; }

    public uint RVA { get; }
    // The 'end' RVA is the last byte occupied by this symbol - so we subtract 1 from the size because
    // (RVA + Size) would point to the byte *after* the symbol.
    public uint RVAEnd => this.RVA + this.Size - 1;
    public uint Size { get; }
    // All the PESymbol types take up real space, so their Size == VirtualSize always
    public uint VirtualSize => this.Size;

    public bool IsCOMDATFolded => false;

    public SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.PEDirectory;

    // There are times, like when parsing a binary produced by lld-link, that we may not have any COFF Group
    // RVA ranges (SymTagCoffGroup) for the region of a PE Directory.  This is arguably a bug in lld-link's PDB
    // generation, but we can compensate for it by synthesizing a COFF group from the directory if needed.
    internal string COFFGroupFallbackName { get; }

    internal PEDirectorySymbol(uint rva, uint size, string name)
    {
        // We specifically don't check for this because directories are very valuable for other parts of SizeBench (ex: checking
        // the debug signature), and they're so cheap to parse that avoiding them is unnecessary for perf.
        //Debug.Assert(symbolSourcesSupported.HasFlag(SymbolSourcesSupported.OtherPESymbols));

        this.RVA = rva;
        this.Size = size;
        this.Name = $"[PE Directory] {name}";

        this.COFFGroupFallbackName = $".sizebench-synthesized-PE-directory-{name.Replace(" ", "", StringComparison.Ordinal)}";
    }

    public bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (otherSymbol is not PEDirectorySymbol otherPEDirectorySymbol)
        {
            return false;
        }

        return String.Equals(this.Name, otherPEDirectorySymbol.Name, StringComparison.Ordinal);
    }
}
