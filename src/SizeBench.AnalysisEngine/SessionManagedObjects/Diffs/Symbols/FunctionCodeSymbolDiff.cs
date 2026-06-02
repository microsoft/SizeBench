using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("FunctionSymbolDiff Name={FullName}, SizeDiff={SizeDiff}")]
public sealed class FunctionCodeSymbolDiff
{
    [Display(AutoGenerateField = false)]
    public IFunctionCodeSymbol? BeforeSymbol { get; }
    [Display(AutoGenerateField = false)]
    public IFunctionCodeSymbol? AfterSymbol { get; }

    public string FullName => this.BeforeSymbol?.FullName ?? this.AfterSymbol!.FullName;
    public string FunctionName => this.BeforeSymbol?.FunctionName ?? this.AfterSymbol!.FunctionName;
    public FunctionCodeFormattedName FormattedName => this.BeforeSymbol?.FormattedName ?? this.AfterSymbol!.FormattedName;

    public int SizeDiff
    {
        get
        {
            long afterSize = this.AfterSymbol?.Size ?? 0;
            long beforeSize = this.BeforeSymbol?.Size ?? 0;
            return (int)(afterSize - beforeSize);
        }
    }

    public IReadOnlyList<CodeBlockSymbolDiff> CodeBlockDiffs { get; }

    internal FunctionCodeSymbolDiff(IFunctionCodeSymbol? beforeSymbol, IFunctionCodeSymbol? afterSymbol, List<CodeBlockSymbolDiff> codeBlocks)
    {
        if (beforeSymbol is null && afterSymbol is null)
        {
            throw new ArgumentException("Both before and after can't be null - if that's true, just don't create this in the first place!");
        }

        this.BeforeSymbol = beforeSymbol;
        this.AfterSymbol = afterSymbol;
        this.CodeBlockDiffs = codeBlocks;
        foreach (var block in codeBlocks)
        {
            block.ParentFunctionDiff = this;
        }
    }
}
