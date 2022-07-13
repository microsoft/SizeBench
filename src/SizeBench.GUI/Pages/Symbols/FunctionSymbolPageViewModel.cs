using System.Collections.ObjectModel;
using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages.Symbols;

internal sealed class FunctionSymbolPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private bool _doesFunctionExist;
    public bool DoesFunctionExist
    {
        get => this._doesFunctionExist;
        private set { this._doesFunctionExist = value; RaisePropertyChanged(); }
    }

    private string _nameOfNonExistentFunction = String.Empty;
    public string NameOfNonexistentFunction
    {
        get => this._nameOfNonExistentFunction;
        private set { this._nameOfNonExistentFunction = value; RaisePropertyChanged(); }
    }

    private IFunctionCodeSymbol? _function;
    public IFunctionCodeSymbol? Function
    {
        get => this._function;
        private set
        {
            this._function = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.DoesFunctionContainMultipleCodeBlocks));
        }
    }

    public bool DoesFunctionContainMultipleCodeBlocks => this.Function?.Blocks.Count > 1;

    public bool IsFunctionCodeUsedForMultipleFunctions => this.FoldedFunctions?.Count > 1;

    private IReadOnlyList<IFunctionCodeSymbol>? _foldedFunctions;
    public IReadOnlyList<IFunctionCodeSymbol>? FoldedFunctions
    {
        get => this._foldedFunctions;
        set
        {
            this._foldedFunctions = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.IsFunctionCodeUsedForMultipleFunctions));
        }
    }

    public ObservableCollection<KeyValuePair<CodeBlockSymbol, SymbolPlacement>> BlockPlacements { get; } = new ObservableCollection<KeyValuePair<CodeBlockSymbol, SymbolPlacement>>();

    private string _functionAttributes = String.Empty;
    public string FunctionAttributes
    {
        get => this._functionAttributes;
        private set { this._functionAttributes = value; RaisePropertyChanged(); }
    }

    private string _disassembly = String.Empty;
    public string Disassembly
    {
        get => this._disassembly;
        private set { this._disassembly = value; RaisePropertyChanged(); }
    }

    private string _pageTitle = "Function Symbol";
    public string PageTitle
    {
        get => this._pageTitle;
        private set { this._pageTitle = value; RaisePropertyChanged(); }
    }

    public FunctionSymbolPageViewModel(IUITaskScheduler uiTaskScheduler,
                                       ISession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var functionRVA = Convert.ToUInt32(this.QueryString["FunctionRVA"], CultureInfo.InvariantCulture);

        this.DoesFunctionExist = functionRVA != 0;

        if (!this.DoesFunctionExist)
        {
            this.NameOfNonexistentFunction = this.QueryString["Name"];
            this.PageTitle = $"Function Symbol: {this.NameOfNonexistentFunction}";
            return;
        }

        ISymbol? rawSymbol = null;
        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up function at {functionRVA:X}",
            async (token) => rawSymbol = await this.Session.LoadSymbolByRVA(functionRVA));

        // It's possible no function was found.  For example, if the FunctionSymbol represents a pure virtual function
        // or a function that was removed by /OPT:REF, then it will have an RVA of 0 which will then cause the lookup
        // above to fail.  Or it's possible that someone tries to type an RVA into the address bar and fat-fingers it.
        // So in cases where we can't find a function, let's just stop and show an empty UI, not crash trying to deref
        // the Function below.
#pragma warning disable CA1508 // Code Analysis seems to think that rawSymbol is always null, it can't tell that it gets set inside StartLongRunningUITask
        if (rawSymbol is null)
#pragma warning restore CA1508
        {
            return;
        }

        if (rawSymbol is IFunctionCodeSymbol rawFunction)
        {
            this.Function = rawFunction;
        }
        else if (rawSymbol is CodeBlockSymbol block)
        {
            this.Function = block.ParentFunction;
        }
        else
        {
            return;
        }

        this.PageTitle = $"Function Symbol: {this.Function.FormattedName.IncludeParentType}";

        var attributes = new List<string>();
        if (Enum.IsDefined(typeof(AccessModifier), this.Function.AccessModifier))
        {
            attributes.Add($"{this.Function.AccessModifier.ToString().ToLowerInvariant()} access modifier");
        }

        if (this.Function.IsVirtual && this.Function.IsIntroVirtual)
        {
            attributes.Add("virtual function");
        }

        if (this.Function.IsVirtual && !this.Function.IsIntroVirtual)
        {
            attributes.Add("virtual function (overriding from a base type)");
        }

        if (this.Function.IsPGO)
        {
            attributes.Add("has been PGO'd");
        }

        if (this.Function.IsOptimizedForSpeed)
        {
            attributes.Add("has been optimized for speed");
        }

        this.FunctionAttributes = String.Join(", ", attributes);

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up the location of {this.Function.FullName}", async (token) =>
        {
            var childPlacements = new Dictionary<CodeBlockSymbol, SymbolPlacement>();
            foreach (var block in this.Function.Blocks)
            {
                var placement = await this.Session.LookupSymbolPlacementInBinary(block, token);
                childPlacements.Add(block, placement);
            }
            this.BlockPlacements.AddRange(childPlacements.OrderBy(kvp => kvp.Key.RVA));
        });

        await this._uiTaskScheduler.StartLongRunningUITask("Looking up any functions that may have folded with this one", async (token) =>
        {
            var foldedFunctions = (await this.Session.EnumerateAllSymbolsFoldedAtRVA(functionRVA, token)).OfType<IFunctionCodeSymbol>().ToList();

            // The function we started with will of course be found, so we only care if at least 2 things were found.
            if (foldedFunctions.Count > 1)
            {
                this.FoldedFunctions = foldedFunctions.OrderBy(fn => fn.FullName).ToList();
            }
        });

        await this._uiTaskScheduler.StartLongRunningUITask($"Disassembling {this.Function.FullName}", async (token)
            => this.Disassembly = await this.Session.DisassembleFunction(this.Function, new DisassembleFunctionOptions(), token));
    }
}
