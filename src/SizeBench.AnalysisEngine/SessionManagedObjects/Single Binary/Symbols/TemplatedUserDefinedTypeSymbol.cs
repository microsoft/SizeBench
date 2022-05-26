namespace SizeBench.AnalysisEngine.Symbols;

public sealed class TemplatedUserDefinedTypeSymbol
{
    public string TemplateName { get; }
    public IReadOnlyList<UserDefinedTypeSymbol> UserDefinedTypes { get; }

    internal TemplatedUserDefinedTypeSymbol(string templateName, List<UserDefinedTypeSymbol> udts)
    {
        this.TemplateName = templateName;
        this.UserDefinedTypes = udts;
    }
}
