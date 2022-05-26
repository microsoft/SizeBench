using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("CodeBlockSymbolDiff Name={Name}, SizeDiff={SizeDiff}, VirtualSizeDiff={VirtualSizeDiff}")]
public sealed class CodeBlockSymbolDiff : SymbolDiff
{
    [Display(AutoGenerateField = false)]
    public CodeBlockSymbol? BeforeCodeBlockSymbol { get; }
    [Display(AutoGenerateField = false)]
    public CodeBlockSymbol? AfterCodeBlockSymbol { get; }

    [Display(AutoGenerateField = false)]
    public FunctionCodeSymbolDiff ParentFunctionDiff { get; internal set; }

    // We can't initialize the parent function until we make the blocks (they have a circular dependency) but we will guarantee that we always
    // set the ParentFunctionDiff property before returning one of these to a caller - and we don't want a caller to consider a parent function
    // as possibly being null as it should never be.
    // If we somehow ever return a null ParentFunctionDiff then it is a bug in the AnalysisEngine and should be fixed rather than removing this
    // suppression and making the property nullable.
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    internal CodeBlockSymbolDiff(CodeBlockSymbol? beforeSymbol, CodeBlockSymbol? afterSymbol) : base(beforeSymbol, afterSymbol)
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    {
        if (beforeSymbol is null && afterSymbol is null)
        {
            throw new ArgumentException("Both before and after can't be null - if that's true, just don't create this in the first place!");
        }

        this.BeforeCodeBlockSymbol = beforeSymbol;
        this.AfterCodeBlockSymbol = afterSymbol;
    }
}
