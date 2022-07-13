using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class TemplateFoldabilityItemPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private TemplateFoldabilityItem? _templateFoldabilityItem;
    public TemplateFoldabilityItem? TemplateFoldabilityItem
    {
        get => this._templateFoldabilityItem;
        set { this._templateFoldabilityItem = value; RaisePropertyChanged(); }
    }

    private List<IFunctionCodeSymbol>? _uniqueSymbols;
    public List<IFunctionCodeSymbol>? UniqueSymbols
    {
        get => this._uniqueSymbols;
        private set { this._uniqueSymbols = value; RaisePropertyChanged(); }
    }

    private IFunctionCodeSymbol? _disassembly1Symbol;
    public IFunctionCodeSymbol? Disassembly1Symbol
    {
        get => this._disassembly1Symbol;
        set
        {
            this._disassembly1Symbol = value;
            RaisePropertyChanged();
            LoadDisassembly();
        }
    }

    private IFunctionCodeSymbol? _disassembly2Symbol;
    public IFunctionCodeSymbol? Disassembly2Symbol
    {
        get => this._disassembly2Symbol;
        set
        {
            this._disassembly2Symbol = value;
            RaisePropertyChanged();
            LoadDisassembly();
        }
    }

    private string? _disassembly1;
    public string? Disassembly1
    {
        get => this._disassembly1;
        private set { this._disassembly1 = value; RaisePropertyChanged(); }
    }

    private string? _disassembly2;
    public string? Disassembly2
    {
        get => this._disassembly2;
        private set { this._disassembly2 = value; RaisePropertyChanged(); }
    }

    public TemplateFoldabilityItemPageViewModel(IUITaskScheduler taskScheduler,
                                                ISession session) : base(session)
    {
        this._uiTaskScheduler = taskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var templateName = this.QueryString["TemplateName"];

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Template Foldability Item",
            async (token) =>
            {
                this.TemplateFoldabilityItem = (await this.Session.EnumerateTemplateFoldabilityItems(token)).FirstOrDefault(tfi => tfi.TemplateName == templateName);
                if (this.TemplateFoldabilityItem != null)
                {
                    this.UniqueSymbols = this.TemplateFoldabilityItem.UniqueSymbols.OrderBy(f => f.Size).ThenBy(f => f.FormattedName.UniqueSignatureWithNoPrefixes).ToList();
                }
            });

        // If we can find at least two things that have the same size in bytes, we'll pre-select those as it's most likely to be a reasonable thing
        // to see the disassembly text for.
        var firstGroupOfSymbolsBySize = this.UniqueSymbols?.GroupBy(f => f.Size).Where(group => group.Count() > 1).FirstOrDefault();
        if (firstGroupOfSymbolsBySize != null)
        {
            this.Disassembly1Symbol = firstGroupOfSymbolsBySize.First();
            this.Disassembly2Symbol = firstGroupOfSymbolsBySize.Skip(1).First();
        }
    }

    private async void LoadDisassembly()
    {
        if (this.Disassembly1Symbol is null)
        {
            this.Disassembly1 = null;
        }

        if (this.Disassembly2Symbol is null)
        {
            this.Disassembly2 = null;
        }

        if (this.Disassembly1Symbol == this.Disassembly2Symbol)
        {
            this.Disassembly1 = null;
            this.Disassembly2 = null;
        }
        else if (this.Disassembly1Symbol != null && this.Disassembly2Symbol != null && this.TemplateFoldabilityItem != null)
        {
            // TODO: it'd be nice if the debugger engine callbacks/outputs/progress were somehow piped to the dialog UI so the user got more feedback.
            await this._uiTaskScheduler.StartLongRunningUITask($"Loading Disassembly",
                async (token) =>
                {
                    var options = new DisassembleFunctionOptions()
                    {
                        ReplaceFunctionNameWith = this.TemplateFoldabilityItem.TemplateName,
                        StripAbsoluteAddressForFunctionLocalReferences = true
                    };

                    options.FunctionsThatShareAnRVAWithDisassembledFunction.AddRange(
                        from func in this.TemplateFoldabilityItem.Symbols
                        where func.PrimaryBlock.RVA == this.Disassembly1Symbol.PrimaryBlock.RVA
                        select func
                        );

                    this.Disassembly1 = await this.Session.DisassembleFunction(this.Disassembly1Symbol, options, token);

                    options.FunctionsThatShareAnRVAWithDisassembledFunction.Clear();
                    options.FunctionsThatShareAnRVAWithDisassembledFunction.AddRange(
                        from func in this.TemplateFoldabilityItem.Symbols
                        where func.PrimaryBlock.RVA == this.Disassembly2Symbol.PrimaryBlock.RVA
                        select func
                        );

                    this.Disassembly2 = await this.Session.DisassembleFunction(this.Disassembly2Symbol, options, token);
                });
        }
    }
}
