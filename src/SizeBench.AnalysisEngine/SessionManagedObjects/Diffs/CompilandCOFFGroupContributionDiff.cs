namespace SizeBench.AnalysisEngine;

public sealed class CompilandCOFFGroupContributionDiff : COFFGroupContributionDiff
{
    public CompilandDiff CompilandDiff { get; }

    public CompilandCOFFGroupContribution? BeforeCOFFGroupContribution { get; }
    public CompilandCOFFGroupContribution? AfterCOFFGroupContribution { get; }

    internal CompilandCOFFGroupContributionDiff(string name,
                                                CompilandCOFFGroupContribution? beforeContribution,
                                                CompilandCOFFGroupContribution? afterContribution,
                                                COFFGroupDiff coffGroupDiff,
                                                CompilandDiff compilandDiff) : base(name, beforeContribution, afterContribution, coffGroupDiff)
    {
        if (beforeContribution is null && afterContribution is null)
        {
            throw new ArgumentException("Both before and after can't be null - if that's true, just don't create this in the first place!");
        }

        this.BeforeCOFFGroupContribution = beforeContribution;
        this.AfterCOFFGroupContribution = afterContribution;
        this.CompilandDiff = compilandDiff;
    }
}
