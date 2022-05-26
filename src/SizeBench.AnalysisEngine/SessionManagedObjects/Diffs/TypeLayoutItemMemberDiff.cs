using System.Diagnostics;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("TypeLayout Member Diff: {Name}, SizeDiff={SizeDiff}")]
public sealed class TypeLayoutItemMemberDiff
{
    internal TypeLayoutItemMemberDiff(TypeLayoutItemMember? before, TypeLayoutItemMember? after)
    {
        this.BeforeMember = before;
        this.AfterMember = after;
        if (before?.Type != null || after?.Type != null)
        {
            this.Type = new TypeSymbolDiff(before?.Type, after?.Type);
        }
    }

    public TypeLayoutItemMember? BeforeMember { get; }
    public TypeLayoutItemMember? AfterMember { get; }

    public string Name => this.AfterMember?.Name ?? this.BeforeMember!.Name;
    public TypeSymbolDiff? Type { get; }
    public bool IsAlignmentMember => this.AfterMember?.IsAlignmentMember ?? this.BeforeMember!.IsAlignmentMember;
    public decimal SizeDiff
    {
        get
        {
            var afterSize = this.AfterMember?.Size ?? 0;
            var beforeSize = this.BeforeMember?.Size ?? 0;
            return afterSize - beforeSize;
        }
    }
}
