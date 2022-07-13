using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages.Symbols;

internal sealed class FunctionCodeSymbolDiffPageViewModel : BinaryDiffViewModelBase
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

    private string? _explanatoryText;
    public string? ExplanatoryText
    {
        get => this._explanatoryText;
        private set { this._explanatoryText = value; RaisePropertyChanged(); }
    }

    private FunctionCodeSymbolDiff? _functionDiff;
    public FunctionCodeSymbolDiff? FunctionDiff
    {
        get => this._functionDiff;
        private set
        {
            this._functionDiff = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.BeforeFunctionContainsMultipleCodeBlocks));
            RaisePropertyChanged(nameof(this.AfterFunctionContainsMultipleCodeBlocks));
        }
    }

    private string _beforeAttributes = String.Empty;
    public string BeforeAttributes
    {
        get => this._beforeAttributes;
        private set { this._beforeAttributes = value; RaisePropertyChanged(); }
    }

    private string _afterAttributes = String.Empty;
    public string AfterAttributes
    {
        get => this._afterAttributes;
        private set { this._afterAttributes = value; RaisePropertyChanged(); }
    }

    public bool BeforeFunctionContainsMultipleCodeBlocks => this.FunctionDiff?.BeforeSymbol?.Blocks.Count > 1;

    public bool AfterFunctionContainsMultipleCodeBlocks => this.FunctionDiff?.AfterSymbol?.Blocks.Count > 1;

    public ObservableCollection<KeyValuePair<CodeBlockSymbol, SymbolPlacement>> BeforeBlockPlacements { get; } = new ObservableCollection<KeyValuePair<CodeBlockSymbol, SymbolPlacement>>();
    public ObservableCollection<KeyValuePair<CodeBlockSymbol, SymbolPlacement>> AfterBlockPlacements { get; } = new ObservableCollection<KeyValuePair<CodeBlockSymbol, SymbolPlacement>>();

    public bool IsBeforeFunctionCodeUsedForMultipleFunctions => this.BeforeFoldedFunctions?.Count > 1;
    public bool IsAfterFunctionCodeUsedForMultipleFunctions => this.AfterFoldedFunctions?.Count > 1;

    private IReadOnlyList<IFunctionCodeSymbol>? _beforeFoldedFunctions;
    public IReadOnlyList<IFunctionCodeSymbol>? BeforeFoldedFunctions
    {
        get => this._beforeFoldedFunctions;
        set
        {
            this._beforeFoldedFunctions = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.IsBeforeFunctionCodeUsedForMultipleFunctions));
        }
    }

    private IReadOnlyList<IFunctionCodeSymbol>? _afterFoldedFunctions;
    public IReadOnlyList<IFunctionCodeSymbol>? AfterFoldedFunctions
    {
        get => this._afterFoldedFunctions;
        set
        {
            this._afterFoldedFunctions = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.IsAfterFunctionCodeUsedForMultipleFunctions));
        }
    }

    private string _pageTitle = "Function Diff";
    public string PageTitle
    {
        get => this._pageTitle;
        private set { this._pageTitle = value; RaisePropertyChanged(); }
    }

    public FunctionCodeSymbolDiffPageViewModel(IUITaskScheduler uiTaskScheduler,
                                               IDiffSession diffSession) : base(diffSession)
    {
        this._uiTaskScheduler = uiTaskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        uint? beforeSymbolRVA = null;
        uint? afterSymbolRVA = null;
        if (this.QueryString.ContainsKey("BeforeRVA"))
        {
            beforeSymbolRVA = Convert.ToUInt32(this.QueryString["BeforeRVA"], CultureInfo.InvariantCulture);
        }

        if (this.QueryString.ContainsKey("AfterRVA"))
        {
            afterSymbolRVA = Convert.ToUInt32(this.QueryString["AfterRVA"], CultureInfo.InvariantCulture);
        }

        this.DoesBeforeSymbolExist = beforeSymbolRVA != null;
        this.DoesAfterSymbolExist = afterSymbolRVA != null;

        if (!this.DoesBeforeSymbolExist && !this.DoesAfterSymbolExist)
        {
            this.PageTitle = $"Symbol Diff: {this.QueryString["Name"]}";
            return;
        }

        SymbolDiff? symbolDiff = null;

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up function diff",
            async (token) => symbolDiff = await this.DiffSession.LoadSymbolDiffByBeforeAndAfterRVA(beforeSymbolRVA, afterSymbolRVA, token));

        // It's possible no symbol diff was found.  For example, if the Symbol was removed by /OPT:REF or /Gw, then it 
        // will have an RVA of 0 which will then cause the lookup above to fail.  Or it's possible that someone
        // tries to type an RVA into the address bar and fat-fingers it.
        // So in cases where we can't find a Symbol, let's just stop and show an empty UI, not crash trying to deref
        // the SymbolDiff below.
#pragma warning disable CA1508 // Code Analysis can't see that the 'symbolDiff' variable gets set in the lambda passed to StartLongRunningUITask, so it thinks it's always null
        if (symbolDiff is null)
        {
            return;
        }
#pragma warning restore CA1508

        if (symbolDiff is CodeBlockSymbolDiff codeBlockSymbolDiff &&
            (codeBlockSymbolDiff.BeforeCodeBlockSymbol is null || codeBlockSymbolDiff.BeforeCodeBlockSymbol is PrimaryCodeBlockSymbol || codeBlockSymbolDiff.BeforeCodeBlockSymbol is SimpleFunctionCodeSymbol) &&
            (codeBlockSymbolDiff.AfterCodeBlockSymbol is null || codeBlockSymbolDiff.AfterCodeBlockSymbol is PrimaryCodeBlockSymbol || codeBlockSymbolDiff.AfterCodeBlockSymbol is SimpleFunctionCodeSymbol))
        {
            this.FunctionDiff = codeBlockSymbolDiff.ParentFunctionDiff;
        }
        else
        {
            return;
        }

        this.PageTitle = $"Function Diff: {this.FunctionDiff.FormattedName.IncludeParentType}";
        this.DoesBeforeSymbolExist = this.FunctionDiff.BeforeSymbol != null;
        this.DoesAfterSymbolExist = this.FunctionDiff.AfterSymbol != null;

        if (this.FunctionDiff.BeforeSymbol != null && this.FunctionDiff.AfterSymbol != null)
        {
            CreateExplanatoryText(this.FunctionDiff.BeforeSymbol, this.FunctionDiff.AfterSymbol);
        }

        this.BeforeAttributes = FormatFunctionAttributes(this.FunctionDiff.BeforeSymbol);
        this.AfterAttributes = FormatFunctionAttributes(this.FunctionDiff.AfterSymbol);

        await LookupBlockPlacements(this.FunctionDiff);

        await LookupFoldedFunctions(this.FunctionDiff);
    }

    private Task LookupBlockPlacements(FunctionCodeSymbolDiff functionDiff)
    {
        return this._uiTaskScheduler.StartLongRunningUITask($"Looking up the location of blocks in {functionDiff.FullName}", async (token) =>
        {
            var countOfBeforeBlocks = functionDiff.BeforeSymbol?.Blocks.Count ?? 0;
            var countOfAfterBlocks = functionDiff.AfterSymbol?.Blocks.Count ?? 0;
            var beforePlacements = new Dictionary<CodeBlockSymbol, Task<SymbolPlacement>>(capacity: countOfBeforeBlocks);
            var afterPlacements = new Dictionary<CodeBlockSymbol, Task<SymbolPlacement>>(capacity: countOfAfterBlocks);

            var placementTasks = new List<Task<SymbolPlacement>>(capacity: countOfBeforeBlocks + countOfAfterBlocks);

            if (functionDiff.BeforeSymbol != null)
            {
                foreach (var block in functionDiff.BeforeSymbol.Blocks)
                {
                    var placement = this.DiffSession.BeforeSession.LookupSymbolPlacementInBinary(block, token);
                    placementTasks.Add(placement);
                    beforePlacements.Add(block, placement);
                }
            }

            if (functionDiff.AfterSymbol != null)
            {
                foreach (var block in functionDiff.AfterSymbol.Blocks)
                {
                    var placement = this.DiffSession.AfterSession.LookupSymbolPlacementInBinary(block, token);
                    placementTasks.Add(placement);
                    afterPlacements.Add(block, placement);
                }
            }

            await Task.WhenAll(placementTasks);

            this.BeforeBlockPlacements.AddRange(beforePlacements.OrderBy(kvp => kvp.Key.RVA).Select(kvp => new KeyValuePair<CodeBlockSymbol, SymbolPlacement>(kvp.Key, kvp.Value.Result)));
            this.AfterBlockPlacements.AddRange(afterPlacements.OrderBy(kvp => kvp.Key.RVA).Select(kvp => new KeyValuePair<CodeBlockSymbol, SymbolPlacement>(kvp.Key, kvp.Value.Result)));
        });
    }

    private Task LookupFoldedFunctions(FunctionCodeSymbolDiff functionDiff)
    {
        return this._uiTaskScheduler.StartLongRunningUITask("Looking up any functions that may have folded with this one", async (token) =>
        {
            if (functionDiff.BeforeSymbol != null)
            {
                // Things that may have folded here include simple functions and primary code blocks, so we'll check for both and 'zoom out' to their parent function if it's a block in a
                // function with separated code.
                var beforeFoldedBlocks = (await this.DiffSession.BeforeSession.EnumerateAllSymbolsFoldedAtRVA(functionDiff.BeforeSymbol.PrimaryBlock.RVA, token)).ToList();
                var beforeFoldedFunctions = new List<IFunctionCodeSymbol>();

                foreach (var beforeFoldedBlock in beforeFoldedBlocks)
                {
                    if (beforeFoldedBlock is IFunctionCodeSymbol beforeFunctionCodeSymbol)
                    {
                        beforeFoldedFunctions.Add(beforeFunctionCodeSymbol);
                    }
                    else if (beforeFoldedBlock is PrimaryCodeBlockSymbol beforePrimaryBlock)
                    {
                        beforeFoldedFunctions.Add(beforePrimaryBlock.ParentFunction);
                    }
                }

                // The function we started with will of course be found, so we only care if at least 2 things were found.
                if (beforeFoldedFunctions.Count > 1)
                {
                    this.BeforeFoldedFunctions = beforeFoldedFunctions.OrderBy(fn => fn.FullName).ToList();
                }
            }

            if (functionDiff.AfterSymbol != null)
            {
                var afterFoldedBlocks = (await this.DiffSession.AfterSession.EnumerateAllSymbolsFoldedAtRVA(functionDiff.AfterSymbol.PrimaryBlock.RVA, token)).ToList();
                var afterFoldedFunctions = new List<IFunctionCodeSymbol>();

                foreach (var afterFoldedBlock in afterFoldedBlocks)
                {
                    if (afterFoldedBlock is IFunctionCodeSymbol afterFunctionCodeSymbol)
                    {
                        afterFoldedFunctions.Add(afterFunctionCodeSymbol);
                    }
                    else if (afterFoldedBlock is PrimaryCodeBlockSymbol afterPrimaryBlock)
                    {
                        afterFoldedFunctions.Add(afterPrimaryBlock.ParentFunction);
                    }
                }

                // The function we started with will of course be found, so we only care if at least 2 things were found.
                if (afterFoldedFunctions.Count > 1)
                {
                    this.AfterFoldedFunctions = afterFoldedFunctions.OrderBy(fn => fn.FullName).ToList();
                }
            }
        });
    }

    private void CreateExplanatoryText(IFunctionCodeSymbol before, IFunctionCodeSymbol after)
    {
        var sb = new StringBuilder();
        sb.Append("This function is somewhat different between these two binaries.  ");
        if (before is SimpleFunctionCodeSymbol)
        {
            sb.Append("In the 'before' binary this function is simple with just one block.  ");
        }
        else if (before is ComplexFunctionCodeSymbol)
        {
            sb.Append("In the 'before' binary this function is composed of multiple blocks.  ");
        }
        else
        {
            throw new NotImplementedException("Something has gone wrong in constructing this page.  This is a bug in SizeBench.");
        }

        if (after is SimpleFunctionCodeSymbol)
        {
            sb.Append("In the 'after' binary this function is simple with just one block.  ");
        }
        else if (after is ComplexFunctionCodeSymbol)
        {
            sb.Append("In the 'after' binary this function is composed of multiple blocks.  ");
        }
        else
        {
            throw new NotImplementedException("Something has gone wrong in constructing this page.  This is a bug in SizeBench.");
        }

        sb.Append("This usually happens when Profile Guided Optimization makes different decisions between these two versions of the binary - but it can make comparing them somewhat difficult.  Keep that in mind when reviewing things below.");

        this.ExplanatoryText = sb.ToString();
    }

    private static string FormatFunctionAttributes(IFunctionCodeSymbol? function)
    {
        if (function is null)
        {
            return String.Empty;
        }

        var attributes = new List<string>();

        if (Enum.IsDefined(typeof(AccessModifier), function.AccessModifier))
        {
            attributes.Add($"{function.AccessModifier.ToString().ToLowerInvariant()} access modifier");
        }

        if (function.IsVirtual && function.IsIntroVirtual)
        {
            attributes.Add("virtual function");
        }

        if (function.IsVirtual && !function.IsIntroVirtual)
        {
            attributes.Add("virtual function (overriding from a base type)");
        }

        if (function.IsPGO)
        {
            attributes.Add("has been PGO'd");
        }

        if (function.IsOptimizedForSpeed)
        {
            attributes.Add("has been optimized for speed");
        }

        if (attributes.Count > 0)
        {
            return "Attributes: " + String.Join(", ", attributes);
        }

        return String.Empty;
    }
}
