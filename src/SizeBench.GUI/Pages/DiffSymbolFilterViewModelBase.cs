using System.ComponentModel;
using System.Windows.Data;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal abstract class DiffSymbolFilterViewModelBase : BinaryDiffViewModelBase
{
    private ICollectionView? _filteredSymbolDiffs;
    public ICollectionView? FilteredSymbolDiffs
    {
        get => this._filteredSymbolDiffs;
        private set { this._filteredSymbolDiffs = value; RaisePropertyChanged(); }
    }

    private bool _includeSymbolsOnlyInBaseline = true;
    public bool IncludeSymbolsOnlyInBaseline
    {
        get => this._includeSymbolsOnlyInBaseline;
        set
        {
            if (this._includeSymbolsOnlyInBaseline != value)
            {
                this._includeSymbolsOnlyInBaseline = value;
                RaisePropertyChanged();
                RefreshFilteredSymbolDiffs();
            }
        }
    }

    private bool _includeModifiedSymbols = true;
    public bool IncludeModifiedSymbols
    {
        get => this._includeModifiedSymbols;
        set
        {
            if (this._includeModifiedSymbols != value)
            {
                this._includeModifiedSymbols = value;
                RaisePropertyChanged();
                RefreshFilteredSymbolDiffs();
            }
        }
    }

    private bool _includeIdenticalSymbols = true;
    public bool IncludeIdenticalSymbols
    {
        get => this._includeIdenticalSymbols;
        set
        {
            if (this._includeIdenticalSymbols != value)
            {
                this._includeIdenticalSymbols = value;
                RaisePropertyChanged();
                RefreshFilteredSymbolDiffs();
            }
        }
    }

    private bool _includeSymbolsOnlyInUpdate = true;
    public bool IncludeSymbolsOnlyInUpdate
    {
        get => this._includeSymbolsOnlyInUpdate;
        set
        {
            if (this._includeSymbolsOnlyInUpdate != value)
            {
                this._includeSymbolsOnlyInUpdate = value;
                RaisePropertyChanged();
                RefreshFilteredSymbolDiffs();
            }
        }
    }

    protected DiffSymbolFilterViewModelBase(IDiffSession diffSession)
        : base(diffSession)
    {
    }

    protected void UpdateFilteredSymbolDiffsView(IEnumerable<SymbolDiff>? symbolDiffs, params SortDescription[] sortDescriptions)
    {
        if (symbolDiffs is null)
        {
            this.FilteredSymbolDiffs = null;
            return;
        }

        var view = CollectionViewSource.GetDefaultView(symbolDiffs);
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            foreach (var sortDescription in sortDescriptions)
            {
                view.SortDescriptions.Add(sortDescription);
            }

            view.Filter = static _ => true;
            view.Filter = (item) => item is SymbolDiff symbolDiff && ShouldIncludeSymbolDiff(symbolDiff);
        }

        this.FilteredSymbolDiffs = view;
    }

    protected IReadOnlyList<SymbolDiff> GetFilteredSymbolDiffsForExport() =>
        this.FilteredSymbolDiffs != null ? this.FilteredSymbolDiffs.Cast<SymbolDiff>().ToList()
                                         : Array.Empty<SymbolDiff>();

    private void RefreshFilteredSymbolDiffs() => this.FilteredSymbolDiffs?.Refresh();

    private bool ShouldIncludeSymbolDiff(SymbolDiff symbolDiff)
    {
        if (symbolDiff.BeforeSymbol != null && symbolDiff.AfterSymbol != null)
        {
            if (symbolDiff.SizeDiff == 0 && symbolDiff.VirtualSizeDiff == 0)
            {
                return this.IncludeIdenticalSymbols;
            }

            return this.IncludeModifiedSymbols;
        }

        if (symbolDiff.BeforeSymbol != null)
        {
            return this.IncludeSymbolsOnlyInBaseline;
        }

        return this.IncludeSymbolsOnlyInUpdate;
    }
}
