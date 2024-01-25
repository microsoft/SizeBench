namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class LoadConfigTableSymbol : ISymbol
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

    public SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.LoadConfigTable;

    internal LoadConfigTableSymbol(uint rva, uint size, string name)
    {
        this.RVA = rva;
        this.Size = size;
        this.Name = name;
    }

    public bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (otherSymbol is not LoadConfigTableSymbol otherLoadConfigTableSymbol)
        {
            return false;
        }

        return this.SymbolComparisonClass == otherLoadConfigTableSymbol.SymbolComparisonClass &&
               String.Equals(this.Name, otherLoadConfigTableSymbol.Name, StringComparison.Ordinal);
    }
}
