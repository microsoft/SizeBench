using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Binary Section Diff: {Name}, SizeDiff={SizeDiff}")]
public sealed class BinarySectionDiff
{
    [Display(AutoGenerateField = false)]
    public BinarySection? BeforeSection { get; }
    [Display(AutoGenerateField = false)]
    public BinarySection? AfterSection { get; }

    public string Name => this.BeforeSection?.Name ?? this.AfterSection?.Name ?? String.Empty;

    public int SizeDiff
    {
        get
        {
            long afterSize = this.AfterSection?.Size ?? 0;
            long beforeSize = this.BeforeSection?.Size ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    public int VirtualSizeDiff
    {
        get
        {
            long afterVirtualSize = this.AfterSection?.VirtualSize ?? 0;
            long beforeVirtualSize = this.BeforeSection?.VirtualSize ?? 0;
            return (int)(afterVirtualSize - beforeVirtualSize);
        }
    }

    [Display(Name = "Size in memory (including section padding) Diff")]
    public int VirtualSizeIncludingPaddingDiff
    {
        get
        {
            long afterVirtualSize = this.AfterSection?.VirtualSizeIncludingPadding ?? 0;
            long beforeVirtualSize = this.BeforeSection?.VirtualSizeIncludingPadding ?? 0;
            return (int)(afterVirtualSize - beforeVirtualSize);
        }
    }

    private readonly List<COFFGroupDiff> _coffGroupDiffs = new List<COFFGroupDiff>();
    public IReadOnlyList<COFFGroupDiff> COFFGroupDiffs => this._coffGroupDiffs;

    internal BinarySectionDiff(BinarySection? beforeSection, BinarySection? afterSection, DiffSessionDataCache cache)
    {
        if (beforeSection is null && afterSection is null)
        {
            throw new ArgumentException("Cannot have both before and after be null - that doesn't make any sense.");
        }

#if DEBUG
        if (cache.BinarySectionDiffsConstructedEver.Any(bsd => bsd.Name == (beforeSection?.Name ?? afterSection?.Name)))
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.BeforeSection = beforeSection;
        this.AfterSection = afterSection;

        if (beforeSection != null)
        {
            foreach (var beforeCG in beforeSection.COFFGroups)
            {
                // We have to match based on the name since we don't have anything else better to use.  But given how
                // consistent the names tend to be, this should be ok.
                var matchingAfterCG = afterSection?.COFFGroups.FirstOrDefault(cg => cg.Name == beforeCG.Name);

#if DEBUG
                if (matchingAfterCG != null && afterSection!.COFFGroups.Where(cg => cg.Name == beforeCG.Name).Count() > 1)
                {
                    throw new InvalidOperationException("This shouldn't be possible, and will throw off how diffing works.  Look into it...");
                }
#endif

                this._coffGroupDiffs.Add(new COFFGroupDiff(beforeCG, matchingAfterCG, this, cache));
            }
        }

        if (afterSection != null)
        {
            // Now catch any COFF Groups that are only in the 'after' list
            foreach (var afterCG in afterSection.COFFGroups)
            {
                if (this._coffGroupDiffs.Any(cg => cg.Name == afterCG.Name))
                {
                    continue;
                }

                // This COFF Group is only in the 'after' list
                this._coffGroupDiffs.Add(new COFFGroupDiff(null, afterCG, this, cache));
            }
        }

        cache.RecordBinarySectionDiffConstructed(this);
    }
}
