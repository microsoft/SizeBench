namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class EnumTypeSymbol : TypeSymbol
{
    internal EnumTypeSymbol(SessionDataCache cache,
                            string name,
                            uint instanceSize,
                            uint symIndexId) : base(cache, name, instanceSize, symIndexId)
    { }

    public override bool CanLoadLayout => false;
}
