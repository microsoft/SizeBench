namespace SizeBench.AnalysisEngine;

public abstract class COFFGroupContributionDiff : ContributionDiff
{
    public COFFGroupDiff COFFGroupDiff { get; }

    internal COFFGroupContributionDiff(string name, Contribution? before, Contribution? after, COFFGroupDiff cgDiff) : base(name, before, after)
    {
        this.COFFGroupDiff = cgDiff;
    }
}
