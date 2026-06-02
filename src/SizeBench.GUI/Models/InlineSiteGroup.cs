using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.GUI.Models;
public sealed record InlineSiteGroup(
        string InlinedFunctionName,
        List<InlineSiteSymbol> InlineSites)
{
    public long TotalSize => this.InlineSites.Sum(s => s.Size);
}
