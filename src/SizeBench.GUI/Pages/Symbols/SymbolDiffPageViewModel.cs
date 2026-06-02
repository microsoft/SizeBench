using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages.Symbols;

internal sealed class SymbolDiffPageViewModel : BinaryDiffViewModelBase
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

    private SymbolDiff? _symbolDiff;
    public SymbolDiff? SymbolDiff
    {
        get => this._symbolDiff;
        private set { this._symbolDiff = value; RaisePropertyChanged(); }
    }

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

    private string _pageTitle = "Symbol Diff";
    public string PageTitle
    {
        get => this._pageTitle;
        private set { this._pageTitle = value; RaisePropertyChanged(); }
    }

    public SymbolDiffPageViewModel(IUITaskScheduler uiTaskScheduler,
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
            async (token) => this.SymbolDiff = await this.DiffSession.LoadSymbolDiffByBeforeAndAfterRVA(beforeSymbolRVA, afterSymbolRVA, token));

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
    }
}
