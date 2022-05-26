namespace SizeBench.AnalysisEngine.Symbols;

public sealed class PointerTypeSymbol : TypeSymbol
{
    internal PointerTypeSymbol(SessionDataCache cache,
                               TypeSymbol pointerTargetType,
                               string name,
                               uint instanceSize,
                               uint symIndexId) : base(cache, name, instanceSize, symIndexId)
    {
        this.PointerTargetType = pointerTargetType;
    }

    // For example, if this PointerTypeSymbol represents "char *", then the PointerTargetType is "char".
    public TypeSymbol PointerTargetType { get; }

    // A pointer has a layout if the thing it points to has a layout.  For example, "char*" doesn't have
    // a layout since "char" doesn't.  But "const MyCustomType&" does have a layout since "const MyCustomType"
    // has a layout.
    public override bool CanLoadLayout => this.PointerTargetType.CanLoadLayout;

    internal override bool IsVeryLikelyTheSameAs(TypeSymbol otherSymbol)
    {
        // Note we explicitly do not call into the base implementation, we don't want pointer types to be matched
        // based on their name, because the name includes things like "*" vs. "&" for reference vs. pointer,
        // which is rarely interesting to consider "different."
        return otherSymbol is PointerTypeSymbol other &&
               this.PointerTargetType.IsVeryLikelyTheSameAs(other.PointerTargetType);
    }
}
