using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Duplicate Data Diff: {Name}, Total Size Diff = {SizeDiff}, Wasted Size Diff = {WastedSizeDiff}")]
public sealed class DuplicateDataItemDiff
{
    [Display(AutoGenerateField = false)]
    public DuplicateDataItem? BeforeDuplicate { get; }
    [Display(AutoGenerateField = false)]
    public DuplicateDataItem? AfterDuplicate { get; }
    public SymbolDiff SymbolDiff { get; }

    public string Name => this.SymbolDiff.Name;

    public int SizeDiff
    {
        get
        {
            long afterSize = this.AfterDuplicate?.TotalSize ?? 0;
            long beforeSize = this.BeforeDuplicate?.TotalSize ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    // All DuplicateData takes up real space, so it's always identical Size and VirtualSize.
    public int VirtualSizeDiff => this.SizeDiff;

    public long WastedSizeDiff
    {
        get
        {
            long afterSize = this.WastedSizeRemaining;
            long beforeSize = this.BeforeDuplicate?.WastedSize ?? 0;
            return afterSize - beforeSize;
        }
    }

    public uint WastedSizeRemaining => this.AfterDuplicate?.WastedSize ?? 0;

    internal DuplicateDataItemDiff(DuplicateDataItem? before, DuplicateDataItem? after, DiffSessionDataCache dataCache)
    {
        if (before is null && after is null)
        {
            throw new ArgumentOutOfRangeException(nameof(before), "Both before and after are null - that doesn't make sense, just don't construct one of these.");
        }

        this.BeforeDuplicate = before;
        this.AfterDuplicate = after;
        this.SymbolDiff = SymbolDiffFactory.CreateSymbolDiff(before?.Symbol, after?.Symbol, dataCache);
    }
}
