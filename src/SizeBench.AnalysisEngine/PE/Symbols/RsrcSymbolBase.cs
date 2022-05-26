namespace SizeBench.AnalysisEngine.Symbols;

internal abstract class RsrcSymbolBase : ISymbol
{

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

    internal RsrcSymbolBase(uint rva, uint size, string name)
    {
        this.RVA = rva;
        this.Size = size;
        this.Name = name;
    }

    public virtual bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (otherSymbol is not RsrcSymbolBase otherRsrcSymbol)
        {
            return false;
        }

        return this.SymbolComparisonClass == otherRsrcSymbol.SymbolComparisonClass &&
               String.Equals(this.Name, otherRsrcSymbol.Name, StringComparison.Ordinal);
    }
}
