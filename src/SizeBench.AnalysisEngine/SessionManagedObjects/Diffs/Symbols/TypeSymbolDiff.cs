using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("TypeSymbolDiff Name={Name}, InstanceSizeDiff={SizeDiff}")]
public class TypeSymbolDiff
{
    [Display(AutoGenerateField = false)]
    public TypeSymbol? BeforeSymbol { get; }
    [Display(AutoGenerateField = false)]
    public TypeSymbol? AfterSymbol { get; }

    public string Name => this.BeforeSymbol?.Name ?? this.AfterSymbol!.Name;

    public int InstanceSizeDiff
    {
        get
        {
            long afterSize = this.AfterSymbol?.InstanceSize ?? 0;
            long beforeSize = this.BeforeSymbol?.InstanceSize ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    internal TypeSymbolDiff(TypeSymbol? beforeSymbol, TypeSymbol? afterSymbol)
    {
        if (beforeSymbol is null && afterSymbol is null)
        {
            throw new ArgumentException("Both before and after can't be null - if that's true, just don't create this in the first place!");
        }

        this.BeforeSymbol = beforeSymbol;
        this.AfterSymbol = afterSymbol;
    }
}
