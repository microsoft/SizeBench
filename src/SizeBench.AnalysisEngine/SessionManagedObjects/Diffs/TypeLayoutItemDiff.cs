using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Type Layout Diff: {UserDefinedType.Name}, InstanceSizeDiff = {InstanceSizeDiff}, Excl. Alignment Waste Diff = {AlignmentWasteExclusiveDiff}")]
public sealed class TypeLayoutItemDiff
{
    [Display(AutoGenerateField = false)]
    public TypeLayoutItem? BeforeTypeLayout { get; }
    [Display(AutoGenerateField = false)]
    public TypeLayoutItem? AfterTypeLayout { get; }

    public UserDefinedTypeSymbol UserDefinedType => this.AfterTypeLayout?.UserDefinedType ?? this.BeforeTypeLayout!.UserDefinedType;

    public TypeSymbolDiff UserDefinedTypeDiff { get; }

    public long InstanceSizeDiff
    {
        get
        {
            long afterSize = this.AfterTypeLayout?.UserDefinedType.InstanceSize ?? 0;
            long beforeSize = this.BeforeTypeLayout?.UserDefinedType.InstanceSize ?? 0;
            return afterSize - beforeSize;
        }
    }

    public decimal AlignmentWasteExclusive => this.AfterTypeLayout?.AlignmentWasteExclusive ?? this.BeforeTypeLayout!.AlignmentWasteExclusive;

    public decimal AlignmentWasteExclusiveDiff
    {
        get
        {
            var afterWasteExclusive = this.AfterTypeLayout?.AlignmentWasteExclusive ?? 0;
            var beforeWasteExclusive = this.BeforeTypeLayout?.AlignmentWasteExclusive ?? 0;
            return afterWasteExclusive - beforeWasteExclusive;
        }
    }

    public decimal AlignmentWasteIncludingBaseTypes => this.AfterTypeLayout?.AlignmentWasteIncludingBaseTypes ?? this.BeforeTypeLayout!.AlignmentWasteIncludingBaseTypes;

    public decimal AlignmentWasteIncludingBaseTypesDiff
    {
        get
        {
            var afterWasteIncludingBaseTypes = this.AfterTypeLayout?.AlignmentWasteIncludingBaseTypes ?? 0;
            var beforeWasteIncludingBaseTypes = this.BeforeTypeLayout?.AlignmentWasteIncludingBaseTypes ?? 0;
            return afterWasteIncludingBaseTypes - beforeWasteIncludingBaseTypes;
        }
    }

    public int UsedForVFPtrsExclusiveDiff
    {
        get
        {
            var afterVFPtrExclusive = (int)(this.AfterTypeLayout?.UsedForVFPtrsExclusive ?? 0);
            var beforeVFPtrExclusive = (int)(this.BeforeTypeLayout?.UsedForVFPtrsExclusive ?? 0);
            return afterVFPtrExclusive - beforeVFPtrExclusive;
        }
    }
    public int UsedForVFPtrsIncludingBaseTypesDiff
    {
        get
        {
            var afterVFPtrIncludingBaseTypes = (int)(this.AfterTypeLayout?.UsedForVFPtrsIncludingBaseTypes ?? 0);
            var beforeVFPtrIncludingBaseTypes = (int)(this.BeforeTypeLayout?.UsedForVFPtrsIncludingBaseTypes ?? 0);
            return afterVFPtrIncludingBaseTypes - beforeVFPtrIncludingBaseTypes;
        }
    }

    public bool IsUnchanged { get; }

    public ReadOnlyCollection<TypeLayoutItemMemberDiff> MemberDiffs { get; private set; } = new List<TypeLayoutItemMemberDiff>().AsReadOnly();

    public ReadOnlyCollection<TypeLayoutItemDiff> BaseTypeDiffs { get; private set; } = new List<TypeLayoutItemDiff>().AsReadOnly();

    internal TypeLayoutItemDiff(TypeLayoutItem? before, TypeLayoutItem? after)
    {
        if (before is null && after is null)
        {
            throw new ArgumentException("Both before and after are null - that doesn't make sense, just don't construct one of these.");
        }

        // We'll start out assuming the type is unchanged since it's easier to flip to false as soon as we discover that the type *is*
        // changed.
        var isUnchanged = true;

        this.BeforeTypeLayout = before;
        this.AfterTypeLayout = after;

        if (before is null || after is null)
        {
            isUnchanged = false;
        }

        CreateAllMemberDiffs(ref isUnchanged);
        CreateAllBaseTypeDiffs(ref isUnchanged);

        this.UserDefinedTypeDiff = new TypeSymbolDiff(this.BeforeTypeLayout?.UserDefinedType, this.AfterTypeLayout?.UserDefinedType);
        this.IsUnchanged = isUnchanged;
    }

    private static bool IsSameMember(TypeLayoutItemMember beforeMember, TypeLayoutItemMember afterMember,
                                     TypeLayoutItemMember? previousBeforeMember, TypeLayoutItemMember? previousAfterMember,
                                     TypeLayoutItemMember? nextBeforeMember, TypeLayoutItemMember? nextAfterMember)
    {
        if (beforeMember.IsAlignmentMember || afterMember.IsAlignmentMember)
        {
            // If one was alignment, and the other is not, they cannot be the same.
            if (beforeMember.IsAlignmentMember != afterMember.IsAlignmentMember)
            {
                return false;
            }

            // Now the hardest case - if they're both alignment members, we don't want to just comapre the offset.  If the offset
            // matches then that's easy and we can early-out, so let's start there.
            if (beforeMember.Offset == afterMember.Offset)
            {
                return true;
            }

            // ok, so by this point they're both alignment members, and they do not share an offset - now we'll heuristically guess
            // that they're the same alignment if they have the same member just before them or just after them.

            ThrowIfConsecutiveAlignmentMembersFound(previousBeforeMember, nextBeforeMember, previousAfterMember, nextAfterMember);

            if (previousBeforeMember != null && previousAfterMember != null && previousBeforeMember.Name == previousAfterMember.Name)
            {
                return beforeMember.Name == afterMember.Name;
            }

            if (nextBeforeMember != null && nextAfterMember != null && nextBeforeMember.Name == nextAfterMember.Name)
            {
                return beforeMember.Name == afterMember.Name;
            }

            return false;
        }

        return beforeMember.Name == afterMember.Name;
    }

    private static void ThrowIfConsecutiveAlignmentMembersFound(TypeLayoutItemMember? previousBeforeMember, TypeLayoutItemMember? nextBeforeMember,
                                                                TypeLayoutItemMember? previousAfterMember, TypeLayoutItemMember? nextAfterMember)
    {
        if (previousBeforeMember != null && previousBeforeMember.IsAlignmentMember)
        {
            throw new InvalidOperationException("Two consecutive alignment members found, should not be possible.");
        }

        if (previousAfterMember != null && previousAfterMember.IsAlignmentMember)
        {
            throw new InvalidOperationException("Two consecutive alignment members found, should not be possible.");
        }

        if (nextBeforeMember != null && nextBeforeMember.IsAlignmentMember)
        {
            throw new InvalidOperationException("Two consecutive alignment members found, should not be possible.");
        }

        if (nextAfterMember != null && nextAfterMember.IsAlignmentMember)
        {
            throw new InvalidOperationException("Two consecutive alignment members found, should not be possible.");
        }
    }

    private void CreateAllMemberDiffs(ref bool isUnchanged)
    {
        var memberDiffs = new List<TypeLayoutItemMemberDiff>();

        if (this.BeforeTypeLayout is null)
        {
            if (this.AfterTypeLayout!.MemberLayouts != null)
            {
                foreach (var member in this.AfterTypeLayout.MemberLayouts)
                {
                    memberDiffs.Add(new TypeLayoutItemMemberDiff(null, member));
                }
            }

            this.MemberDiffs = memberDiffs.AsReadOnly();
            return;
        }
        if (this.AfterTypeLayout is null)
        {
            if (this.BeforeTypeLayout!.MemberLayouts != null)
            {
                foreach (var member in this.BeforeTypeLayout.MemberLayouts)
                {
                    memberDiffs.Add(new TypeLayoutItemMemberDiff(member, null));
                }
            }

            this.MemberDiffs = memberDiffs.AsReadOnly();
            return;
        }

        var afterMembersLeft = this.AfterTypeLayout.MemberLayouts?.ToList() ?? Enumerable.Empty<TypeLayoutItemMember>().ToList();

        TypeLayoutItemMember? beforeTailSlopAlignmentMember = null;

        if (this.BeforeTypeLayout.MemberLayouts != null)
        {
            for (var i = 0; i < this.BeforeTypeLayout.MemberLayouts.Count; i++)
            {
                var beforeMember = this.BeforeTypeLayout.MemberLayouts[i];

                // Tail slop alignment is special since we know it can exist at most once in before/after, and we always
                // want to pair them up if they exist, so we'll skip tail slop and handle it later.
                if (beforeMember.IsTailSlopAlignmentMember)
                {
                    beforeTailSlopAlignmentMember = beforeMember;
                    continue;
                }

                var matchingAfterMember = afterMembersLeft.FirstOrDefault(afterMember =>
                    {
                        // It's safe to deref AfterTypeLayout.MemberLayouts here since we are iterating over
                        // the afterMembersLeft collection which is constructed originally from the
                        // AfterTypeLayout.MemberLayouts.
                        var indexOfAfterMember = this.AfterTypeLayout.MemberLayouts!.IndexOf(afterMember);

                        var prevBeforeMember = i > 0 ? this.BeforeTypeLayout.MemberLayouts[i - 1] : null;
                        var prevAfterMember = indexOfAfterMember > 0 ? this.AfterTypeLayout.MemberLayouts[indexOfAfterMember - 1] : null;

                        var nextBeforeMember = i < this.BeforeTypeLayout.MemberLayouts.Count - 1 ? this.BeforeTypeLayout.MemberLayouts[i + 1] : null;
                        var nextAfterMember = indexOfAfterMember < this.AfterTypeLayout.MemberLayouts.Count - 1 ? this.AfterTypeLayout.MemberLayouts[indexOfAfterMember + 1] : null;
                        return IsSameMember(beforeMember, afterMember, prevBeforeMember, prevAfterMember, nextBeforeMember, nextAfterMember);
                    });

                // If it matched, it can never match again
                if (matchingAfterMember != null)
                {
                    afterMembersLeft.Remove(matchingAfterMember);
                }
                else
                {
                    isUnchanged = false; // We couldn't find a matching 'after' member so there was a change
                }

                if (matchingAfterMember != null && beforeMember.IsBitField != matchingAfterMember.IsBitField)
                {
                    // It used to be a bitfield, now it's not (or vice-versa).  Easiest thing to do is show one deleted, one added
                    memberDiffs.Add(new TypeLayoutItemMemberDiff(beforeMember, null));
                    memberDiffs.Add(new TypeLayoutItemMemberDiff(null, matchingAfterMember));
                    isUnchanged = false;

                    continue;
                }

                var memberDiff = new TypeLayoutItemMemberDiff(beforeMember, matchingAfterMember);
                memberDiffs.Add(memberDiff);
                if (memberDiff.SizeDiff != 0)
                {
                    isUnchanged = false;
                }
            }
        }

        if (beforeTailSlopAlignmentMember != null)
        {
            var afterTailSlopAlignmentMemberIfPresent = afterMembersLeft.SingleOrDefault(m => m.IsTailSlopAlignmentMember);
            memberDiffs.Add(new TypeLayoutItemMemberDiff(beforeTailSlopAlignmentMember, afterTailSlopAlignmentMemberIfPresent));
            if (afterTailSlopAlignmentMemberIfPresent != null)
            {
                afterMembersLeft.Remove(afterTailSlopAlignmentMemberIfPresent);
            }
            else
            {
                isUnchanged = false; // No matching tail slop, therefore there was a change
            }
        }

        foreach (var member in afterMembersLeft)
        {
            // This member is new in the 'after' since we couldn't find it above
            memberDiffs.Add(new TypeLayoutItemMemberDiff(null, member));
            isUnchanged = false;
        }

        this.MemberDiffs = memberDiffs.AsReadOnly();
    }

    private void CreateAllBaseTypeDiffs(ref bool isUnchanged)
    {
        var baseTypeLayouts = new List<TypeLayoutItemDiff>();

        if (this.BeforeTypeLayout is null || this.BeforeTypeLayout.BaseTypeLayouts is null)
        {
            if (this.AfterTypeLayout?.BaseTypeLayouts != null)
            {
                foreach (var baseType in this.AfterTypeLayout.BaseTypeLayouts)
                {
                    baseTypeLayouts.Add(new TypeLayoutItemDiff(null, baseType));
                }

                this.BaseTypeDiffs = baseTypeLayouts.AsReadOnly();
                isUnchanged = false;
            }
            return;
        }
        if (this.AfterTypeLayout is null || this.AfterTypeLayout.BaseTypeLayouts is null)
        {
            // We know that this.BeforeTypeLayout is not null, and BeforeTypeLayout.BaseTypeLayouts is not null,
            // otherwise we would have already entered the first condition above.
            foreach (var baseType in this.BeforeTypeLayout.BaseTypeLayouts)
            {
                baseTypeLayouts.Add(new TypeLayoutItemDiff(baseType, null));
            }

            this.BaseTypeDiffs = baseTypeLayouts.AsReadOnly();
            isUnchanged = false;
            return;
        }

        var afterBaseTypesRemaining = this.AfterTypeLayout.BaseTypeLayouts.ToList();

        foreach (var baseType in this.BeforeTypeLayout.BaseTypeLayouts)
        {
            var matchingAfterItem = afterBaseTypesRemaining.FirstOrDefault(bt => bt.UserDefinedType.IsVeryLikelyTheSameAs(baseType.UserDefinedType));

            if (matchingAfterItem != null)
            {
                afterBaseTypesRemaining.Remove(matchingAfterItem);
            }
            else
            {
                isUnchanged = false; // No matching base type means we've found a change
            }

            var baseTypeDiff = new TypeLayoutItemDiff(baseType, matchingAfterItem);
            baseTypeLayouts.Add(baseTypeDiff);
            if (!baseTypeDiff.IsUnchanged)
            {
                isUnchanged = false;
            }
        }

        foreach (var baseType in afterBaseTypesRemaining)
        {
            // This base class is new in the inheritance hierarchy
            baseTypeLayouts.Add(new TypeLayoutItemDiff(null, baseType));
            isUnchanged = false;
        }

        this.BaseTypeDiffs = baseTypeLayouts.AsReadOnly();
    }
}
