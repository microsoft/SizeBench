namespace SizeBench.AnalysisEngine.Symbols;

public sealed class SeparatedCodeBlockSymbol : CodeBlockSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.SeparatedCodeBlock;

    // TODO: this seems wrong, as a separated block could be folded and shared among multiple parent functions.  How should the API represent this?
    internal uint ParentFunctionSymIndexId { get; }

    internal SeparatedCodeBlockSymbol(SessionDataCache cache,
                                      uint rva,
                                      uint size,
                                      uint symIndexId,
                                      uint parentFunctionSymIndexId) : base(cache, rva, size, symIndexId: symIndexId)
    {
        this.ParentFunctionSymIndexId = parentFunctionSymIndexId;
    }

    protected override void OnParentFunctionSet()
    {
        this.Name = $"{BlockOfCodePrefix}{this.ParentFunction.FormattedName.UniqueSignatureWithNoPrefixes}";
        if (this.IsCOMDATFolded)
        {
            this.CanonicalName = this.DataCache.AllCanonicalNames![this.RVA].CanonicalName;
        }
        else
        {
            this.CanonicalName = this.Name;
        }
    }

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
#pragma warning disable CA1508 // Code Analysis seems to think that otherSeparatedBlock.ParentFunction.SeparatedBlocks.Count will always be 1, but I can't see how, so suppressing that warning.

        // There can be many separated blocks in one function and we have no way to tell them apart.  But,
        // if there's just one separated block in this and the one we're comparing to, we can go ahead and let
        // them be 'similar enough'.
        return ((ComplexFunctionCodeSymbol)this.ParentFunction).SeparatedBlocks.Count == 1 &&
               otherSymbol is SeparatedCodeBlockSymbol otherSeparatedBlock &&
               ((ComplexFunctionCodeSymbol)otherSeparatedBlock.ParentFunction).SeparatedBlocks.Count == 1 &&
               this.ParentFunction.IsVeryLikelyTheSameAs(otherSeparatedBlock.ParentFunction);

#pragma warning restore CA1508
    }
}
