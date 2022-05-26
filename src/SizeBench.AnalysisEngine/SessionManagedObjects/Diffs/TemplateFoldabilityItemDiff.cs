using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Template Foldability Diff: {TemplateName}, Wasted Size Diff = {WastedSizeDiff}")]
public sealed class TemplateFoldabilityItemDiff
{
    [Display(AutoGenerateField = false)]
    public TemplateFoldabilityItem? BeforeTemplateFoldabilityItem { get; }
    [Display(AutoGenerateField = false)]
    public TemplateFoldabilityItem? AfterTemplateFoldabilityItem { get; }

    public string TemplateName => this.BeforeTemplateFoldabilityItem?.TemplateName ?? this.AfterTemplateFoldabilityItem?.TemplateName ?? String.Empty;

    public string Name => this.TemplateName;

    [Display(AutoGenerateField = false)]
    public int SizeDiff
    {
        get
        {
            long afterSize = this.AfterTemplateFoldabilityItem?.TotalSize ?? 0;
            long beforeSize = this.BeforeTemplateFoldabilityItem?.TotalSize ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    // Template Foldability items always exist in real space, so VirtualSizeDiff == SizeDiff always
    [Display(AutoGenerateField = false)]
    public int VirtualSizeDiff => this.SizeDiff;

    public int WastedSizeDiff
    {
        get
        {
            long afterSize = this.WastedSizeRemaining;
            long beforeSize = this.BeforeTemplateFoldabilityItem?.WastedSize ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    public uint WastedSizeRemaining => this.AfterTemplateFoldabilityItem?.WastedSize ?? 0;

    public TemplateFoldabilityItemDiff(TemplateFoldabilityItem? before, TemplateFoldabilityItem? after)
    {
        if (before is null && after is null)
        {
            throw new ArgumentException("Both before and after are null - that doesn't make sense, just don't construct one of these.");
        }

        this.BeforeTemplateFoldabilityItem = before;
        this.AfterTemplateFoldabilityItem = after;
    }
}
