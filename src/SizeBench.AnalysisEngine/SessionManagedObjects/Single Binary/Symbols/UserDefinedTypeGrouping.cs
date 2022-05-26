namespace SizeBench.AnalysisEngine.Symbols;

public sealed class UserDefinedTypeGrouping
{
    public UserDefinedTypeSymbol? UserDefinedType { get; }
    public TemplatedUserDefinedTypeSymbol? TemplatedUserDefinedType { get; }

    internal UserDefinedTypeGrouping(string groupName, List<UserDefinedTypeSymbol> udts)
    {
        if (udts.Count == 1)
        {
            this.UserDefinedType = udts[0];
        }
        else
        {
            this.TemplatedUserDefinedType = new TemplatedUserDefinedTypeSymbol(groupName, udts);
        }
    }
}
