using System.Diagnostics;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Type Layout for {UserDefinedType.Name}")]
public sealed class TypeLayoutItem
{
    internal TypeLayoutItem(UserDefinedTypeSymbol udt,
                            decimal alignmentWasteExclusive,
                            uint usedForVFPtrsExclusive,
                            TypeLayoutItem[]? baseTypeLayouts,
                            TypeLayoutItemMember[]? memberLayouts)
    {
        this.UserDefinedType = udt;
        this.AlignmentWasteExclusive = alignmentWasteExclusive;
        this.UsedForVFPtrsExclusive = usedForVFPtrsExclusive;
        this.BaseTypeLayouts = baseTypeLayouts;
        this.MemberLayouts = memberLayouts;
    }

    public UserDefinedTypeSymbol UserDefinedType { get; }
    public decimal AlignmentWasteExclusive { get; }
    public decimal AlignmentWasteIncludingBaseTypes => this.AlignmentWasteExclusive + (this.BaseTypeLayouts is null ? 0 : this.BaseTypeLayouts.Sum(bcl => bcl.AlignmentWasteIncludingBaseTypes));
    public uint UsedForVFPtrsExclusive { get; }
    public uint UsedForVFPtrsIncludingBaseTypes => this.UsedForVFPtrsExclusive + (this.BaseTypeLayouts is null ? 0 : (uint)this.BaseTypeLayouts.Sum(bcl => bcl.UsedForVFPtrsIncludingBaseTypes));
    public IReadOnlyList<TypeLayoutItem>? BaseTypeLayouts { get; }
    public IReadOnlyList<TypeLayoutItemMember>? MemberLayouts { get; }
}
