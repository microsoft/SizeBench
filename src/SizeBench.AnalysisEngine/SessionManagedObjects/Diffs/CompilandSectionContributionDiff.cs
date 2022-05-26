namespace SizeBench.AnalysisEngine;

public sealed class CompilandSectionContributionDiff : SectionContributionDiff
{
    public CompilandDiff CompilandDiff { get; }

    public CompilandSectionContribution? BeforeSectionContribution { get; }
    public CompilandSectionContribution? AfterSectionContribution { get; }

    internal CompilandSectionContributionDiff(string name,
                                              CompilandSectionContribution? beforeContribution,
                                              CompilandSectionContribution? afterContribution,
                                              BinarySectionDiff binarySectionDiff,
                                              CompilandDiff compilandDiff) : base(name, beforeContribution, afterContribution, binarySectionDiff)
    {
        if (beforeContribution is null && afterContribution is null)
        {
            throw new ArgumentException("Both before and after can't be null - if that's true, just don't create this in the first place!");
        }

        this.BeforeSectionContribution = beforeContribution;
        this.AfterSectionContribution = afterContribution;
        this.CompilandDiff = compilandDiff;
    }
}
