namespace SizeBench.AnalysisEngine.Symbols;

public sealed class PrimaryCodeBlockSymbol : CodeBlockSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.PrimaryCodeBlock;

    internal PrimaryCodeBlockSymbol(SessionDataCache cache,
                                    uint rva,
                                    uint size,
                                    uint symIndexId) : base(cache, rva, size, symIndexId: symIndexId)
    {
    }

    protected override void OnParentFunctionSet()
    {
        this.Name = $"{BlockOfCodePrefix}{this.ParentFunction.FormattedName.UniqueSignatureWithNoPrefixes}";
        if (this.IsCOMDATFolded)
        {
            this.CanonicalName = $"{BlockOfCodePrefix}{this.DataCache.AllCanonicalNames![this.RVA].CanonicalName}";
        }
        else
        {
            this.CanonicalName = this.Name;
        }
    }

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        if (otherSymbol is PrimaryCodeBlockSymbol otherPrimaryBlock)
        {
            return this.ParentFunction.IsVeryLikelyTheSameAs(otherPrimaryBlock.ParentFunction);
        }

        // If this function was 'simple' in one side and complex in the other, we'll allow them to match primary block
        // of the complex side to this simple function.  That way a function that goes from PGO-dead to PGO-live and split
        // into two chunks can still be somewhat compared.
        return otherSymbol is SimpleFunctionCodeSymbol otherSimpleFunction &&
               this.ParentFunction.IsVeryLikelyTheSameAs(otherSimpleFunction);
    }
}
