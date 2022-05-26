using System.ComponentModel.DataAnnotations;

namespace SizeBench.AnalysisEngine;

public abstract class ContributionDiff
{
    public string Name => this.ContributionName;
    private string ContributionName { get; set; }
    [Display(AutoGenerateField = false)]
    public Contribution? BeforeContribution { get; }
    [Display(AutoGenerateField = false)]
    public Contribution? AfterContribution { get; }

    public int SizeDiff
    {
        get
        {
            long afterSize = this.AfterContribution?.Size ?? 0;
            long beforeSize = this.BeforeContribution?.Size ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    public int VirtualSizeDiff
    {
        get
        {
            long afterSize = this.AfterContribution?.VirtualSize ?? 0;
            long beforeSize = this.BeforeContribution?.VirtualSize ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    internal ContributionDiff(string name, Contribution? before, Contribution? after)
    {
        this.ContributionName = name;
        this.BeforeContribution = before;
        this.AfterContribution = after;
    }
}
