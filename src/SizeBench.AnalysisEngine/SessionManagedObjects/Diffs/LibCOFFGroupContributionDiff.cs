namespace SizeBench.AnalysisEngine;

public sealed class LibCOFFGroupContributionDiff : COFFGroupContributionDiff
{
    public LibDiff LibDiff { get; }

    public LibCOFFGroupContribution? BeforeCOFFGroupContribution { get; }
    public LibCOFFGroupContribution? AfterCOFFGroupContribution { get; }

    internal LibCOFFGroupContributionDiff(string name,
                                        LibCOFFGroupContribution? beforeContribution,
                                        LibCOFFGroupContribution? afterContribution,
                                        COFFGroupDiff coffGroupDiff,
                                        LibDiff libDiff) : base(name, beforeContribution, afterContribution, coffGroupDiff)
    {
        if (beforeContribution is null && afterContribution is null)
        {
            throw new ArgumentException("Both before and after can't be null - if that's true, just don't create this in the first place!");
        }

        this.BeforeCOFFGroupContribution = beforeContribution;
        this.AfterCOFFGroupContribution = afterContribution;
        this.LibDiff = libDiff;
    }
}
