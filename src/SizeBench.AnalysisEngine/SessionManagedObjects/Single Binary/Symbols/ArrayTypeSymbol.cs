namespace SizeBench.AnalysisEngine.Symbols;

public sealed class ArrayTypeSymbol : TypeSymbol
{
    internal ArrayTypeSymbol(SessionDataCache cache,
                             string name,
                             uint size,
                             uint symIndexId,
                             TypeSymbol elementType,
                             uint elementCount) : base(cache, name, size, symIndexId)
    {
        this.ElementType = elementType;
        this.ElementCount = elementCount;
    }

    public TypeSymbol ElementType { get; }

    //TODO: support multi-dimensional arrays once I figure out how they can ever get generated outside of FORTRAN
    internal uint ElementCount { get; }

    public override bool CanLoadLayout => this.ElementType.CanLoadLayout;

    internal override bool IsVeryLikelyTheSameAs(TypeSymbol otherSymbol)
    {
        // Note we explicitly do not call into the base implementation, we don't want array types to be matched
        // based on their name, because the name includes the element count (like "short[10]" vs. "short[20]").
        return otherSymbol is ArrayTypeSymbol other &&
               this.ElementType.IsVeryLikelyTheSameAs(other.ElementType);
    }
}
