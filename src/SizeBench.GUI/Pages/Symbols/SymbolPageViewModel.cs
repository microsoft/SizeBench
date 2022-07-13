using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages.Symbols;

internal sealed class SymbolPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private bool _doesSymbolExist;
    public bool DoesSymbolExist
    {
        get => this._doesSymbolExist;
        private set { this._doesSymbolExist = value; RaisePropertyChanged(); }
    }

    private string _nameOfNonExistentSymbol = String.Empty;
    public string NameOfNonexistentSymbol
    {
        get => this._nameOfNonExistentSymbol;
        private set { this._nameOfNonExistentSymbol = value; RaisePropertyChanged(); }
    }

    private ISymbol? _symbol;
    public ISymbol? Symbol
    {
        get => this._symbol;
        private set { this._symbol = value; RaisePropertyChanged(); }
    }

    private BinarySection? _section;
    public BinarySection? BinarySection
    {
        get => this._section;
        private set { this._section = value; RaisePropertyChanged(); }
    }

    private COFFGroup? _coffGroup;
    public COFFGroup? COFFGroup
    {
        get => this._coffGroup;
        private set { this._coffGroup = value; RaisePropertyChanged(); }
    }

    private Compiland? _compiland;
    public Compiland? Compiland
    {
        get => this._compiland;
        private set { this._compiland = value; RaisePropertyChanged(); }
    }

    private Library? _lib;
    public Library? Lib
    {
        get => this._lib;
        private set { this._lib = value; RaisePropertyChanged(); }
    }

    private SourceFile? _sourceFile;
    public SourceFile? SourceFile
    {
        get => this._sourceFile;
        private set { this._sourceFile = value; RaisePropertyChanged(); }
    }

    private string _pageTitle = "Symbol";
    public string PageTitle
    {
        get => this._pageTitle;
        private set { this._pageTitle = value; RaisePropertyChanged(); }
    }

    public SymbolPageViewModel(IUITaskScheduler uiTaskScheduler,
                               ISession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var symbolRVA = Convert.ToUInt32(this.QueryString["RVA"], CultureInfo.InvariantCulture);

        this.DoesSymbolExist = symbolRVA != 0;

        if (!this.DoesSymbolExist)
        {
            this.NameOfNonexistentSymbol = this.QueryString["Name"];
            this.PageTitle = $"Symbol: {this.NameOfNonexistentSymbol}";
            return;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up symbol at {symbolRVA:X}",
            async (token) => this.Symbol = await this.Session.LoadSymbolByRVA(symbolRVA));

        // It's possible no symbol was found, such as if the user cancels the loading before it finishes.
        if (this.Symbol is null)
        {
            return;
        }

        this.PageTitle = $"Symbol: {this.Symbol.Name}";

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up the location of {this.Symbol.Name}", async (token) =>
        {
            var placement = await this.Session.LookupSymbolPlacementInBinary(this.Symbol, token);
            this.BinarySection = placement.BinarySection;
            this.COFFGroup = placement.COFFGroup;
            this.Lib = placement.Lib;
            this.Compiland = placement.Compiland;
            this.SourceFile = placement.SourceFile;
        });
    }
}
