using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages.Symbols;

internal sealed class COMDATFoldedSymbolPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private ISymbol? _symbol;
    public ISymbol? Symbol
    {
        get => this._symbol;
        private set { this._symbol = value; RaisePropertyChanged(); }
    }

    private string _pageTitle = "Symbol";
    public string PageTitle
    {
        get => this._pageTitle;
        private set { this._pageTitle = value; RaisePropertyChanged(); }
    }

    private List<ISymbol> _foldedSymbols = new List<ISymbol>();
    public List<ISymbol> FoldedSymbols
    {
        get => this._foldedSymbols;
        private set { this._foldedSymbols = value; RaisePropertyChanged(); }
    }

    private ISymbol? _canonicalSymbol;
    public ISymbol? CanonicalSymbol
    {
        get => this._canonicalSymbol;
        private set { this._canonicalSymbol = value; RaisePropertyChanged(); }
    }

    public COMDATFoldedSymbolPageViewModel(IUITaskScheduler uiTaskScheduler,
                                           ISession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var symbolRVA = Convert.ToUInt32(this.QueryString["RVA"], CultureInfo.InvariantCulture);

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up which symbols are folded at 0x{symbolRVA:X}", async (token) =>
        {
            var foldedSymbols = await this.Session.EnumerateAllSymbolsFoldedAtRVA(symbolRVA, token);

            this.FoldedSymbols = foldedSymbols.OrderBy(sym =>
            {
                if (sym is IFunctionCodeSymbol fn)
                {
                    return fn.FullName;
                }

                return sym.Name;
            }).ToList();
        });

        // Now that we've found all the symbols at this RVA, we can pick the one the user actually navigated to by name
        this.Symbol = this.FoldedSymbols.FirstOrDefault(sym => sym.IsCOMDATFolded && sym.Name.Equals(this.QueryString["Name"], StringComparison.Ordinal));

        // It's possible no symbol was found, such as if the user cancels the loading before it finishes.
        if (this.Symbol is null)
        {
            return;
        }

        this.PageTitle = $"Symbol: {this.Symbol.Name}";
        this.CanonicalSymbol = this.FoldedSymbols.Single(sym => sym.IsCOMDATFolded == false);
    }
}
