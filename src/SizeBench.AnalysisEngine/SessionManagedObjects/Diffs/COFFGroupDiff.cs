using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("COFF Group Diff: {Name}, SizeDiff={SizeDiff}")]
public sealed class COFFGroupDiff
{
    [Display(AutoGenerateField = false)]
    public COFFGroup? BeforeCOFFGroup { get; }
    [Display(AutoGenerateField = false)]
    public COFFGroup? AfterCOFFGroup { get; }

    public string Name => this.BeforeCOFFGroup?.Name ?? this.AfterCOFFGroup?.Name ?? String.Empty;

    public int SizeDiff
    {
        get
        {
            long afterSize = this.AfterCOFFGroup?.Size ?? 0;
            long beforeSize = this.BeforeCOFFGroup?.Size ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    public int VirtualSizeDiff
    {
        get
        {
            long afterVirtualSize = this.AfterCOFFGroup?.VirtualSize ?? 0;
            long beforeVirtualSize = this.BeforeCOFFGroup?.VirtualSize ?? 0;
            return (int)(afterVirtualSize - beforeVirtualSize);
        }
    }

    [Display(Name = "Size in memory (including padding) Diff")]
    public int VirtualSizeIncludingPaddingDiff
    {
        get
        {
            long afterVirtualSize = this.AfterCOFFGroup?.VirtualSizeIncludingPadding ?? 0;
            long beforeVirtualSize = this.BeforeCOFFGroup?.VirtualSizeIncludingPadding ?? 0;
            return (int)(afterVirtualSize - beforeVirtualSize);
        }
    }

    [Display(AutoGenerateField = false)]
    public BinarySectionDiff SectionDiff { get; }

    internal COFFGroupDiff(COFFGroup? beforeCG, COFFGroup? afterCG, BinarySectionDiff sectionDiff, DiffSessionDataCache cache)
    {
#if DEBUG
        if (cache.COFFGroupDiffsConstrutedEver.Any(cgd => cgd.Name == (beforeCG?.Name ?? afterCG?.Name)))
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.SectionDiff = sectionDiff;
        this.BeforeCOFFGroup = beforeCG;
        this.AfterCOFFGroup = afterCG;

        cache?.RecordCOFFGroupDiffConstructed(this);
    }
}
