namespace SizeBench.AnalysisEngine;

public abstract class SectionContributionDiff : ContributionDiff
{
    public BinarySectionDiff BinarySectionDiff { get; }

    internal SectionContributionDiff(string name, Contribution? before, Contribution? after, BinarySectionDiff sectionDiff) : base(name, before, after)
    {
        this.BinarySectionDiff = sectionDiff;
    }
}
