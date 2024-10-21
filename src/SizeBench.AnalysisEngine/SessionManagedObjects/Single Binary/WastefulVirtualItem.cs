using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Wasteful Virtual: Wasted Size = {WastedSize}, Type Name = {UserDefinedType.Name}")]
public sealed class WastefulVirtualItem
{
    public bool IsCOMType { get; }

    public int WastePerSlot
    {
        get
        {
            var numClassesWastedOn = this.UserDefinedType.DerivedTypeCount == 0 ? 0 : this.UserDefinedType.DerivedTypeCount + 1; // +1 to include self
            return numClassesWastedOn * this.BytesPerWord;
        }
    }

    [DisplayFormat(DataFormatString = "{0:N0}")]
    public uint WastedSize => (uint)((this.WastedOverridesPureWithExactlyOneOverride.Count + this.WastedOverridesNonPureWithNoOverrides.Count) * this.WastePerSlot);
    public UserDefinedTypeSymbol UserDefinedType { get; }

    // Wasteful Virtuals do their comparisons by looking at the entire unique signature, except for the parent type since the whole point is that these are being comapred
    // across base and derived types.  If you are recording the wasteful virtual in a database or showing it in a UI to a user, these are the recommended flags to show, as
    // they match those used by the analysis that discovers them.
    public static FunctionCodeNameFormatting NameFormattingForWastedOverrides => (FunctionCodeNameFormatting.IncludeUniqueSignature & ~FunctionCodeNameFormatting.IncludeParentType);

    private readonly List<IFunctionCodeSymbol> _wastedOverridesPureWithExactlyOneOverride = new List<IFunctionCodeSymbol>();
    public IReadOnlyCollection<IFunctionCodeSymbol> WastedOverridesPureWithExactlyOneOverride => this._wastedOverridesPureWithExactlyOneOverride;

    private readonly List<IFunctionCodeSymbol> _wastedOverridesNonPureWithNoOverrides = new List<IFunctionCodeSymbol>();
    public IReadOnlyCollection<IFunctionCodeSymbol> WastedOverridesNonPureWithNoOverrides => this._wastedOverridesNonPureWithNoOverrides;

    internal void AddWastedOverrideThatIsPureWithExactlyOneOverride(IFunctionCodeSymbol func) => this._wastedOverridesPureWithExactlyOneOverride.Add(func);
    internal void AddWastedOverrideThatIsNotPureWithNoOverrides(IFunctionCodeSymbol func) => this._wastedOverridesNonPureWithNoOverrides.Add(func);
    internal byte BytesPerWord { get; }

    internal WastefulVirtualItem(UserDefinedTypeSymbol udt, bool isCOMType, byte bytesPerWord)
    {
        this.UserDefinedType = udt;
        this.IsCOMType = isCOMType;
        this.BytesPerWord = bytesPerWord;
    }
}
