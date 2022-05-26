namespace SizeBench.AnalysisEngine;

public sealed class LibSectionContributionDiff : SectionContributionDiff
{
    public LibDiff LibDiff { get; }

    public LibSectionContribution? BeforeSectionContribution { get; }
    public LibSectionContribution? AfterSectionContribution { get; }

    internal LibSectionContributionDiff(string name,
                                        LibSectionContribution? beforeContribution,
                                        LibSectionContribution? afterContribution,
                                        BinarySectionDiff binarySectionDiff,
                                        LibDiff libDiff) : base(name, beforeContribution, afterContribution, binarySectionDiff)
    {
        if (beforeContribution is null && afterContribution is null)
        {
            throw new ArgumentException("Both before and after can't be null - if that's true, just don't create this in the first place!");
        }

        this.BeforeSectionContribution = beforeContribution;
        this.AfterSectionContribution = afterContribution;
        this.LibDiff = libDiff;
    }
}
