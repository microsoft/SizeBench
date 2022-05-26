namespace SizeBench.AnalysisEngine.Symbols;

public sealed class BasicTypeSymbol : TypeSymbol
{
    internal BasicTypeSymbol(SessionDataCache cache,
                             string name,
                             uint size,
                             uint symIndexId) : base(cache, name, size, symIndexId)
    { }

    public override bool CanLoadLayout => false;
}
