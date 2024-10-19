using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Wasteful Virtual Diff: {TypeName}, Wasted Size Diff = {WastedSizeDiff}")]
public sealed class WastefulVirtualItemDiff
{
    [Display(AutoGenerateField = false)]
    public WastefulVirtualItem? BeforeWastefulVirtual { get; }
    [Display(AutoGenerateField = false)]
    public WastefulVirtualItem? AfterWastefulVirtual { get; }

    public string TypeName => this.BeforeWastefulVirtual?.UserDefinedType.Name ?? this.AfterWastefulVirtual!.UserDefinedType.Name;

    public bool IsCOMType => this.BeforeWastefulVirtual?.IsCOMType ?? this.AfterWastefulVirtual!.IsCOMType;

    public string Name => this.TypeName;

    [Display(AutoGenerateField = false)]
    public int SizeDiff => this.WastedSizeDiff;

    // Wasteful Virtuals always take up real space, so Size == VirtualSize.
    [Display(AutoGenerateField = false)]
    public int VirtualSizeDiff => this.SizeDiff;

    public int WastedSizeDiff
    {
        get
        {
            long afterSize = this.WastedSizeRemaining;
            long beforeSize = this.BeforeWastefulVirtual?.WastedSize ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    public uint WastedSizeRemaining => this.AfterWastefulVirtual?.WastedSize ?? 0;

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
#pragma warning disable CA1034 // Nested types should not be visible - this type is very simple, so un-nesting it doesn't seem worth the work
    public class TypeHierarchyChange
#pragma warning restore CA1034
    {
        [ExcludeFromCodeCoverage] // Used only by the debugger
        private string DebuggerDisplay
        {
            get
            {
                //TODO: would be nice if these were able to use the 'size to friendly size' converter, or this logic were moved to the view
                //      layer of the UI, so users could see this in KB instead of bytes, if it's big enough.
                if (this.WasteChange > 0)
                {
                    return $"{this.Type.Name} added to hierarchy, adding {this.WasteChange} bytes of waste.";
                }
                else
                {
                    return $"{this.Type.Name} removed from hierarchy, removing {Math.Abs(this.WasteChange)} bytes of waste.";
                }
            }
        }

        public TypeSymbolDiff Type { get; }
        public int WasteChange { get; }

        public TypeHierarchyChange(TypeSymbolDiff type, int wasteChange)
        {
            this.Type = type;
            this.WasteChange = wasteChange;
        }

        public override string ToString() => this.DebuggerDisplay;
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
#pragma warning disable CA1034 // Nested types should not be visible - this type is very simple, so un-nesting it doesn't seem worth the work
    public class WastedOverrideChange
#pragma warning restore CA1034
    {
        private string DebuggerDisplay
        {
            get
            {
                //TODO: would be nice if these were able to use the 'size to friendly size' converter, or this logic were moved to the view
                //      layer of the UI, so users could see this in KB instead of bytes, if it's big enough.
                if (this.WasteChange > 0)
                {
                    return $"{this.Function.FormattedName.UniqueSignature} added to wasteful virtual list, adding {this.WasteChange} bytes of waste.";
                }
                else
                {
                    return $"{this.Function.FormattedName.UniqueSignature} removed from wasteful virtual list, removing {Math.Abs(this.WasteChange)} bytes of waste.";
                }
            }
        }

        public FunctionCodeSymbolDiff Function { get; }
        public int WasteChange { get; }

        public WastedOverrideChange(FunctionCodeSymbolDiff function, int wasteChange)
        {
            this.Function = function;
            this.WasteChange = wasteChange;
        }

        public override string ToString() => this.DebuggerDisplay;
    }

    [Display(AutoGenerateField = false)]
    public ReadOnlyCollection<TypeHierarchyChange> TypeHierarchyChanges { get; }

    [Display(AutoGenerateField = false)]
    public ReadOnlyCollection<WastedOverrideChange> WastedOverrideChanges { get; }

    private static IEnumerable<IFunctionCodeSymbol> AllWastedOverrides(WastefulVirtualItem wvi) => wvi.WastedOverridesPureWithExactlyOneOverride.Concat(wvi.WastedOverridesNonPureWithNoOverrides);

    internal WastefulVirtualItemDiff(WastefulVirtualItem? before, WastefulVirtualItem? after, DiffSessionDataCache cache, IDIAAdapter beforeDIAAdapter, IDIAAdapter afterDIAAdapter)
    {
        if (before is null && after is null)
        {
            throw new ArgumentException("Both before and after are null - that doesn't make sense, just don't construct one of these.");
        }

        this.BeforeWastefulVirtual = before;
        this.AfterWastefulVirtual = after;

        var typeHierarchyChanges = new List<TypeHierarchyChange>();
        var virtualChanges = new List<WastedOverrideChange>();

        if (before is null)
        {
            // Easy, everything in the 'after' is brand new waste
            foreach (var wastedOverride in AllWastedOverrides(after!))
            {
                virtualChanges.Add(new WastedOverrideChange(SymbolDiffFactory.CreateFunctionCodeSymbolDiff(null, wastedOverride, cache), after!.WastePerSlot));
            }

            // No need to look at the types, the wasted overrides account for that space already.
        }
        else if (after is null)
        {
            // Similarly easy, everything in the 'before' is waste that's been removed
            foreach (var wastedOverride in AllWastedOverrides(before))
            {
                virtualChanges.Add(new WastedOverrideChange(SymbolDiffFactory.CreateFunctionCodeSymbolDiff(null, wastedOverride, cache), 0 - before.WastePerSlot));
            }

            // No need to look at the types, the wasted overrides account for that space already.
        }
        else
        {
            var newWastedOverridesInAfter = 0;
            var wastedOverridesGoneSinceBefore = 0;

            foreach (var wastedOverrideInBefore in AllWastedOverrides(before))
            {
                var matchingWastedOverrideInAfter = AllWastedOverrides(after).FirstOrDefault(f => f.IsVeryLikelyTheSameAs(wastedOverrideInBefore));
                if (matchingWastedOverrideInAfter is null)
                {
                    // This 'wasted override' was present in before but is no longer present in after,
                    // so it is a savings!
                    virtualChanges.Add(new WastedOverrideChange(SymbolDiffFactory.CreateFunctionCodeSymbolDiff(wastedOverrideInBefore, null, cache), 0 - before.WastePerSlot));
                    wastedOverridesGoneSinceBefore++;
                }
            }

            foreach (var wastedOverrideInAfter in AllWastedOverrides(after))
            {
                var matchingWastedOverrideInBefore = AllWastedOverrides(before).FirstOrDefault(f => f.IsVeryLikelyTheSameAs(wastedOverrideInAfter));
                if (matchingWastedOverrideInBefore is null)
                {
                    // This 'wasted override' was NOT present in before but it IS in after,
                    // so it is a new source of waste.
                    virtualChanges.Add(new WastedOverrideChange(SymbolDiffFactory.CreateFunctionCodeSymbolDiff(null, wastedOverrideInAfter, cache), after.WastePerSlot));
                    newWastedOverridesInAfter++;
                }
            }

            // Start by figuring out which derived types are added/removed
            foreach (var derivedTypeFromBefore in before.UserDefinedType.EnumerateDerivedTypes(beforeDIAAdapter, CancellationToken.None))
            {
                var matchingTypeInAfter = after.UserDefinedType.EnumerateDerivedTypes(afterDIAAdapter, CancellationToken.None).FirstOrDefault(udt => udt.IsVeryLikelyTheSameAs(derivedTypeFromBefore));
                if (matchingTypeInAfter is null)
                {
                    // This type was in the 'before' as a derived type, but is not in 'after' - so it's a savings!
                    // Each wasted override (in the 'before' list only) is gone, so the savings is one word for
                    // each of those.
                    // We already attributed savings for the wasted override changes above, so deduct those from
                    // the type savings to avoid double-counting.
                    typeHierarchyChanges.Add(new TypeHierarchyChange(new TypeSymbolDiff(derivedTypeFromBefore, null),
                                                                     0 - ((AllWastedOverrides(before).Count() - wastedOverridesGoneSinceBefore) * before.BytesPerWord)));
                }
            }

            foreach (var derivedTypeFromAfter in after.UserDefinedType.EnumerateDerivedTypes(afterDIAAdapter, CancellationToken.None))
            {
                var matchingTypeInBefore = before.UserDefinedType.EnumerateDerivedTypes(beforeDIAAdapter, CancellationToken.None).FirstOrDefault(udt => udt.IsVeryLikelyTheSameAs(derivedTypeFromAfter));
                if (matchingTypeInBefore is null)
                {
                    // This type was NOT in the 'before' as a derived type, but it IS in 'after' - so it's all brand new waste :(
                    // Each wasted override (in the 'after' list only) is gone, so the savings is one word for each of those.
                    // We already attributed waste for the wasted override changes above, so deduct those from
                    // the waste calculation to avoid double-counting.
                    typeHierarchyChanges.Add(new TypeHierarchyChange(new TypeSymbolDiff(null, derivedTypeFromAfter),
                                                                     (AllWastedOverrides(after).Count() - newWastedOverridesInAfter) * after.BytesPerWord));
                }
            }
        }

        this.TypeHierarchyChanges = new ReadOnlyCollection<TypeHierarchyChange>(typeHierarchyChanges);
        this.WastedOverrideChanges = new ReadOnlyCollection<WastedOverrideChange>(virtualChanges);

#if DEBUG
        SanityCheckNoByteLeftBehind();
#endif
    }

#if DEBUG
    // Doesn't make sense for code coverage, the goal is to never hit this function.  The exception thrown in
    // debug tests is enough, should this ever hit.
    [ExcludeFromCodeCoverage]
    private void SanityCheckNoByteLeftBehind()
    {
        var wasteChangeFromTypeHierarchy = this.TypeHierarchyChanges.Sum(hc => hc.WasteChange);
        var wasteChangeFromWastedOverrides = this.WastedOverrideChanges.Sum(wo => wo.WasteChange);

        if ((wasteChangeFromTypeHierarchy + wasteChangeFromWastedOverrides) != this.WastedSizeDiff)
        {
            throw new InvalidOperationException($"Not all bytes of wasteful virtual diff for {this.TypeName} accounted for! " +
                                                $"WastedSizeDiff={this.WastedSizeDiff}, " +
                                                $"hierarchy={wasteChangeFromTypeHierarchy}, " +
                                                $"overrides={wasteChangeFromWastedOverrides}, " +
                                                $"sum of constituent parts={wasteChangeFromTypeHierarchy + wasteChangeFromWastedOverrides}");
        }
    }
#endif
}
