using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal abstract class ImportSymbolBase : ISymbol
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"{GetType().Name}: {this.Name} ({this.Size} bytes)";

    public string Name { get; }

    public uint RVA { get; }
    // The 'end' RVA is the last byte occupied by this symbol - so we subtract 1 from the size because
    // (RVA + Size) would point to the byte *after* the symbol.
    public uint RVAEnd => this.RVA + this.Size - 1;
    public uint Size { get; }
    // All the PESymbol types take up real space, so their Size == VirtualSize always
    public uint VirtualSize => this.Size;

    public bool IsCOMDATFolded => false;

    public abstract SymbolComparisonClass SymbolComparisonClass { get; }

    internal ImportSymbolBase(uint rva, uint size, string name, SymbolSourcesSupported symbolSourcesSupported)
    {
        Debug.Assert(symbolSourcesSupported.HasFlag(SymbolSourcesSupported.OtherPESymbols));

        this.RVA = rva;
        this.Size = size;
        this.Name = name;
    }

    public bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (otherSymbol is not ImportSymbolBase otherImportSymbolBase)
        {
            return false;
        }

        return this.SymbolComparisonClass == otherImportSymbolBase.SymbolComparisonClass &&
               String.Equals(this.Name, otherImportSymbolBase.Name, StringComparison.Ordinal);
    }
}
