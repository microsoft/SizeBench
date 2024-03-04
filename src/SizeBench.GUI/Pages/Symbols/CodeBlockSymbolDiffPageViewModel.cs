using System.Globalization;
using System.Text;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages.Symbols;

internal sealed class CodeBlockSymbolDiffPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private bool _doesBeforeSymbolExist;
    public bool DoesBeforeSymbolExist
    {
        get => this._doesBeforeSymbolExist;
        private set { this._doesBeforeSymbolExist = value; RaisePropertyChanged(); }
    }

    private bool _doesAfterSymbolExist;
    public bool DoesAfterSymbolExist
    {
        get => this._doesAfterSymbolExist;
        private set { this._doesAfterSymbolExist = value; RaisePropertyChanged(); }
    }

    private string? _blocksOfDifferentTypeWarningText;
    public string? BlocksOfDifferentTypeWarningText
    {
        get => this._blocksOfDifferentTypeWarningText;
        private set { this._blocksOfDifferentTypeWarningText = value; RaisePropertyChanged(); }
    }

    private CodeBlockSymbolDiff? _symbolDiff;
    public CodeBlockSymbolDiff? SymbolDiff
    {
        get => this._symbolDiff;
        private set { this._symbolDiff = value; RaisePropertyChanged(); }
    }

    private FunctionCodeSymbolDiff? _parentFunctionSymbolDiff;
    public FunctionCodeSymbolDiff? ParentFunctionSymbolDiff
    {
        get => this._parentFunctionSymbolDiff;
        private set
        {
            this._parentFunctionSymbolDiff = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.IsBeforeParentFunctionComplex));
            RaisePropertyChanged(nameof(this.IsAfterParentFunctionComplex));
        }
    }

    public bool IsBeforeParentFunctionComplex => this.ParentFunctionSymbolDiff?.BeforeSymbol?.Blocks.Count > 1;

    public bool IsAfterParentFunctionComplex => this.ParentFunctionSymbolDiff?.AfterSymbol?.Blocks.Count > 1;

    private SymbolPlacement? _beforePlacement;
    public SymbolPlacement? BeforePlacement
    {
        get => this._beforePlacement;
        private set { this._beforePlacement = value; RaisePropertyChanged(); }
    }

    private SymbolPlacement? _afterPlacement;
    public SymbolPlacement? AfterPlacement
    {
        get => this._afterPlacement;
        private set { this._afterPlacement = value; RaisePropertyChanged(); }
    }

    public bool IsBeforeBlockCodeUsedForMultipleBlocks => this.BeforeFoldedBlocks?.Count > 1;

    private IReadOnlyList<ISymbol>? _beforeFoldedBlocks;
    public IReadOnlyList<ISymbol>? BeforeFoldedBlocks
    {
        get => this._beforeFoldedBlocks;
        set
        {
            this._beforeFoldedBlocks = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.IsBeforeBlockCodeUsedForMultipleBlocks));
        }
    }

    public bool IsAfterBlockCodeUsedForMultipleBlocks => this.AfterFoldedBlocks?.Count > 1;

    private IReadOnlyList<ISymbol>? _afterFoldedBlocks;
    public IReadOnlyList<ISymbol>? AfterFoldedBlocks
    {
        get => this._afterFoldedBlocks;
        set
        {
            this._afterFoldedBlocks = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.IsAfterBlockCodeUsedForMultipleBlocks));
        }
    }

    private string _pageTitle = "Symbol Diff";
    public string PageTitle
    {
        get => this._pageTitle;
        private set { this._pageTitle = value; RaisePropertyChanged(); }
    }

    public CodeBlockSymbolDiffPageViewModel(IUITaskScheduler uiTaskScheduler,
                                            IDiffSession diffSession) : base(diffSession)
    {
        this._uiTaskScheduler = uiTaskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        uint? beforeSymbolRVA = null;
        uint? afterSymbolRVA = null;
        if (this.QueryString.TryGetValue("BeforeRVA", out var value))
        {
            beforeSymbolRVA = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }

        if (this.QueryString.TryGetValue("AfterRVA", out value))
        {
            afterSymbolRVA = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }

        this.DoesBeforeSymbolExist = beforeSymbolRVA != null;
        this.DoesAfterSymbolExist = afterSymbolRVA != null;

        if (!this.DoesBeforeSymbolExist && !this.DoesAfterSymbolExist)
        {
            this.PageTitle = $"Symbol Diff: {this.QueryString["Name"]}";
            return;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up symbol diff",
            async (token) => this.SymbolDiff = await this.DiffSession.LoadSymbolDiffByBeforeAndAfterRVA(beforeSymbolRVA, afterSymbolRVA, token) as CodeBlockSymbolDiff);

        // It's possible no symbol diff was found.  For example, if the Symbol was removed by /OPT:REF or /Gw, then it 
        // will have an RVA of 0 which will then cause the lookup above to fail.  Or it's possible that someone
        // tries to type an RVA into the address bar and fat-fingers it.
        // So in cases where we can't find a Symbol, let's just stop and show an empty UI, not crash trying to deref
        // the SymbolDiff below.
        if (this.SymbolDiff is null)
        {
            return;
        }

        this.PageTitle = $"Symbol Diff: {this.SymbolDiff.Name}";
        this.DoesBeforeSymbolExist = this.SymbolDiff.BeforeSymbol != null;
        this.DoesAfterSymbolExist = this.SymbolDiff.AfterSymbol != null;
        this.ParentFunctionSymbolDiff = this.SymbolDiff.ParentFunctionDiff;

        if (this.SymbolDiff.BeforeSymbol != null && this.SymbolDiff.AfterSymbol != null &&
            this.SymbolDiff.BeforeSymbol.GetType() != this.SymbolDiff.AfterSymbol.GetType())
        {
            CreateBlocksOfDifferentTypeWarningText(this.SymbolDiff.BeforeSymbol, this.SymbolDiff.AfterSymbol);
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up the location of {this.SymbolDiff.Name}", async (token) =>
        {
            var placementLookups = new List<Task>(capacity: 2);

            if (this.DoesBeforeSymbolExist)
            {
                placementLookups.Add(this.DiffSession.BeforeSession.LookupSymbolPlacementInBinary(this.SymbolDiff.BeforeSymbol!, token).ContinueWith(t => this.BeforePlacement = t.Result, TaskScheduler.Default));
            }

            if (this.DoesAfterSymbolExist)
            {
                placementLookups.Add(this.DiffSession.AfterSession.LookupSymbolPlacementInBinary(this.SymbolDiff.AfterSymbol!, token).ContinueWith(t => this.AfterPlacement = t.Result, TaskScheduler.Default));
            }

            await Task.WhenAll(placementLookups);
        });

        await this._uiTaskScheduler.StartLongRunningUITask("Looking up any blocks that may have folded with this one", async (token) =>
        {
            var foldingLookups = new List<Task>(capacity: 2);

            if (this.SymbolDiff.BeforeCodeBlockSymbol != null)
            {
                foldingLookups.Add(this.DiffSession.BeforeSession.EnumerateAllSymbolsFoldedAtRVA(this.SymbolDiff.BeforeCodeBlockSymbol.RVA, token).ContinueWith(t =>
                {
                    // The block we started with will of course be found, so we only care if at least 2 things were found.
                    if (t.Result.Count > 1)
                    {
                        this.BeforeFoldedBlocks = t.Result.OrderBy(b => b.Name).ToList();
                    }
                }, TaskScheduler.Default));
            }

            if (this.SymbolDiff.AfterCodeBlockSymbol != null)
            {
                foldingLookups.Add(this.DiffSession.AfterSession.EnumerateAllSymbolsFoldedAtRVA(this.SymbolDiff.AfterCodeBlockSymbol.RVA, token).ContinueWith(t =>
                {
                    // The block we started with will of course be found, so we only care if at least 2 things were found.
                    if (t.Result.Count > 1)
                    {
                        this.AfterFoldedBlocks = t.Result.OrderBy(b => b.Name).ToList();
                    }
                }, TaskScheduler.Default));
            }

            await Task.WhenAll(foldingLookups);
        });
    }

    private void CreateBlocksOfDifferentTypeWarningText(ISymbol before, ISymbol after)
    {
        var sb = new StringBuilder();
        sb.Append("The before and after symbols here aren't exactly comparable - ");
        if (before is SimpleFunctionCodeSymbol)
        {
            sb.Append("the symbol in the 'before' binary is an entire function, ");
        }
        else if (before is PrimaryCodeBlockSymbol)
        {
            sb.Append("the symbol in the 'before' binary is only the primary block of a function that is separated into multiple blocks (such as by PGO), ");
        }
        else
        {
            // We should never be trying to display this page with two symbols of differing types unless we are comparing a primary code block and a simple function - 
            // separated code blocks should always be directly of the same type and we won't reach here.
            throw new NotImplementedException("Something has gone wrong in constructing this page.  This is a bug in SizeBench.");
        }

        sb.Append("and ");

        if (after is SimpleFunctionCodeSymbol)
        {
            sb.Append("the symbol in the 'after' binary is an entire function.");
        }
        else if (after is PrimaryCodeBlockSymbol)
        {
            sb.Append("the symbol in the 'after' binary is only the primary block of a function that is separated into multiple blocks (such as by PGO).");
        }
        else
        {
            // We should never be trying to display this page with two symbols of differing types unless we are comparing a primary code block and a simple function - 
            // separated code blocks should always be directly of the same type and we won't reach here.
            throw new NotImplementedException("Something has gone wrong in constructing this page.  This is a bug in SizeBench.");
        }

        // It's intentional that the string ends with ", then " and seems to be hanging.  It's because the XAML has the hyperlink after that.
        sb.Append("  If you meant to compare the entire function across all blocks, then ");

        this.BlocksOfDifferentTypeWarningText = sb.ToString();
    }
}
