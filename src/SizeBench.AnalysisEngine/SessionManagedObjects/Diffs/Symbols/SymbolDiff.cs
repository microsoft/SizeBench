using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("SymbolDiff Name={Name}, SizeDiff={SizeDiff}, VirtualSizeDiff={VirtualSizeDiff}")]
public class SymbolDiff
{
    [Display(AutoGenerateField = false)]
    public ISymbol? BeforeSymbol { get; }
    [Display(AutoGenerateField = false)]
    public ISymbol? AfterSymbol { get; }

    public string Name => this.BeforeSymbol?.Name ?? this.AfterSymbol!.Name;

    public int SizeDiff
    {
        get
        {
            long afterSize = this.AfterSymbol?.Size ?? 0;
            long beforeSize = this.BeforeSymbol?.Size ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    public int VirtualSizeDiff
    {
        get
        {
            long afterSize = this.AfterSymbol?.VirtualSize ?? 0;
            long beforeSize = this.BeforeSymbol?.VirtualSize ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    internal SymbolDiff(ISymbol? beforeSymbol, ISymbol? afterSymbol)
    {
        if (beforeSymbol is null && afterSymbol is null)
        {
            throw new ArgumentException("Both before and after can't be null - if that's true, just don't create this in the first place!");
        }

        this.BeforeSymbol = beforeSymbol;
        this.AfterSymbol = afterSymbol;
    }
}
