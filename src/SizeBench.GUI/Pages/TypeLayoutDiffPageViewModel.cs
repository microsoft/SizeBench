using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class TypeLayoutDiffPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;

    private IReadOnlyList<TypeLayoutItemDiff>? _typeLayoutItemDiffsList;
    private ICollectionView? _typeLayoutCollectionView;
    public ICollectionView? TypeLayoutItemDiffs
    {
        get => this._typeLayoutCollectionView;
        set
        {
            this._typeLayoutCollectionView = value;
            RaisePropertyChanged();
        }
    }

    private IReadOnlyList<TypeLayoutItem> _beforeTypeLayouts = new List<TypeLayoutItem>();
    public IReadOnlyList<TypeLayoutItem> BeforeTypeLayoutItems
    {
        get => this._beforeTypeLayouts;
        private set
        {
            this._beforeTypeLayouts = value;
            RaisePropertyChanged();
        }
    }

    private IReadOnlyList<TypeLayoutItem> _afterTypeLayouts = new List<TypeLayoutItem>();
    public IReadOnlyList<TypeLayoutItem> AfterTypeLayoutItems
    {
        get => this._afterTypeLayouts;
        private set
        {
            this._afterTypeLayouts = value;
            RaisePropertyChanged();
        }
    }

    private string? _typeNameToLoad;
    public string? TypeNameToLoad
    {
        get => this._typeNameToLoad;
        set
        {
            this._typeNameToLoad = value;
            RaisePropertyChanged();
        }
    }

    private string _pageTitle = "Type Layout Diff";
    public string PageTitle
    {
        get => this._pageTitle;
        set
        {
            this._pageTitle = value;
            RaisePropertyChanged();
        }
    }

    private bool _excludeUnchangedTypes;
    public bool ExcludeUnchangedTypes
    {
        get => this._excludeUnchangedTypes;
        set
        {
            this._excludeUnchangedTypes = value;

            this._typeLayoutCollectionView?.Refresh();
            RefreshBeforeAndAfterLists();
            //TODO: consider encoding this into the fragment like AllWastefulVirtualDiffs does with ExcludeCOMTypes,
            //      but then I'll have to encode both this and the type name in the fragment and parse them separately.
            //      Doesn't seem worth it at the moment.
            RaisePropertyChanged();
        }
    }

    public DelegateCommand ViewLayoutsOfSpecificTypesCommand { get; }
    public DelegateCommand<TypeSymbolDiff> LoadDiffTypeCommand { get; }
    public DelegateCommand ExportToExcelCommand { get; }

    public TypeLayoutDiffPageViewModel(IUITaskScheduler taskScheduler,
                                       IExcelExporter excelExporter,
                                       IDiffSession diffSession) : base(diffSession)
    {
        this._uiTaskScheduler = taskScheduler;
        this._excelExporter = excelExporter;
        this.ViewLayoutsOfSpecificTypesCommand = new DelegateCommand(() => SetCurrentFragment(this.TypeNameToLoad));
        this.LoadDiffTypeCommand = new DelegateCommand<TypeSymbolDiff>(LoadTypeLayoutByDiffType);
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel, () => this.TypeLayoutItemDiffs?.Cast<TypeLayoutItemDiff>().Count() > 0);
    }

    protected override Task OnCurrentFragmentChanged()
    {
        this.TypeNameToLoad = this.CurrentFragment;
        return LoadTypeLayoutsByName();
    }

    private static string ResolveProperTypeName(TypeSymbol typeSymbol)
    {
        if (typeSymbol is UserDefinedTypeSymbol)
        {
            return typeSymbol.Name;
        }
        else if (typeSymbol is PointerTypeSymbol ptrType)
        {
            return ResolveProperTypeName(ptrType.PointerTargetType);
        }
        else if (typeSymbol is ModifiedTypeSymbol modType)
        {
            return ResolveProperTypeName(modType.UnmodifiedTypeSymbol);
        }
        else if (typeSymbol is ArrayTypeSymbol arrType)
        {
            return ResolveProperTypeName(arrType.ElementType);
        }
        else
        {
            throw new InvalidOperationException("We shouldn't be trying to do this - how did we get here?");
        }
    }

    private async void LoadTypeLayoutByDiffType(TypeSymbolDiff typeSymbol)
    {
        this.TypeNameToLoad = ResolveProperTypeName(typeSymbol.AfterSymbol ?? typeSymbol.BeforeSymbol!);
        await this._uiTaskScheduler.StartLongRunningUITask($"Loading Type Layout for {typeSymbol.Name}",
            async (token) =>
            {
                OnRequestFragmentNavigation(this.TypeNameToLoad);
                SetNewLayoutItems(new List<TypeLayoutItemDiff>() { await this.DiffSession.LoadTypeLayoutDiff(typeSymbol, token) });
            });
    }

    private async Task LoadTypeLayoutsByName()
    {
        if (this.TypeNameToLoad == "*")
        {
            await this._uiTaskScheduler.StartLongRunningUITask($"Loading Type Layout Diffs for all types",
            async (token) =>
            {
                OnRequestFragmentNavigation(this.TypeNameToLoad);
                SetNewLayoutItems(await this.DiffSession.LoadAllTypeLayoutDiffs(token));
            });
        }
        else if (!String.IsNullOrWhiteSpace(this.TypeNameToLoad))
        {
            await this._uiTaskScheduler.StartLongRunningUITask($"Loading Type Layout Diff(s) for {this.TypeNameToLoad}",
                async (token) =>
                {
                    OnRequestFragmentNavigation(this.TypeNameToLoad);
                    SetNewLayoutItems(await this.DiffSession.LoadTypeLayoutDiffsByName(this.TypeNameToLoad, token));
                });
        }
    }

    private void SetNewLayoutItems(IReadOnlyList<TypeLayoutItemDiff> typeLayoutItemDiffs)
    {
        this._typeLayoutItemDiffsList = typeLayoutItemDiffs;
        this.TypeLayoutItemDiffs = CollectionViewSource.GetDefaultView(this._typeLayoutItemDiffsList);
        using (this.TypeLayoutItemDiffs.DeferRefresh())
        {
            this.TypeLayoutItemDiffs.SortDescriptions.Add(new SortDescription("UserDefinedType.Name", ListSortDirection.Ascending));
            this.TypeLayoutItemDiffs.Filter = (tli) => LayoutIsWorthViewingInTreeView((TypeLayoutItemDiff)tli);

            this.PageTitle = this._typeLayoutItemDiffsList.Count > 0 ? $"Type Layout Diff: {this.TypeNameToLoad}" : "Type Layout Diff";
            this.ExportToExcelCommand.RaiseCanExecuteChanged();
        }
        RefreshBeforeAndAfterLists();
    }

    private void RefreshBeforeAndAfterLists()
    {
        var newBefores = new List<TypeLayoutItem>();
        var newAfters = new List<TypeLayoutItem>();

        if (this.TypeLayoutItemDiffs != null)
        {
            foreach (var layoutDiff in this.TypeLayoutItemDiffs.Cast<TypeLayoutItemDiff>())
            {
                if (layoutDiff.BeforeTypeLayout != null)
                {
                    newBefores.Add(layoutDiff.BeforeTypeLayout);
                }

                if (layoutDiff.AfterTypeLayout != null)
                {
                    newAfters.Add(layoutDiff.AfterTypeLayout);
                }
            }
        }

        this.BeforeTypeLayoutItems = newBefores;
        this.AfterTypeLayoutItems = newAfters;
    }

    private bool LayoutIsWorthViewingInTreeView(TypeLayoutItemDiff typeLayoutDiff)
    {
        if (typeLayoutDiff.BeforeTypeLayout?.UserDefinedType.InstanceSize == 0 &&
            typeLayoutDiff.AfterTypeLayout?.UserDefinedType.InstanceSize == 0)
        {
            return false;
        }

        if (this.ExcludeUnchangedTypes && typeLayoutDiff.IsUnchanged)
        {
            return false;
        }

        // TODO: Consider implementing the additional case that the single binary page does, to hide
        // more uninteresting type layouts.

        return true;
    }

    #region Excel Export

    private async void ExportToExcel()
    {
        if (this.TypeLayoutItemDiffs != null)
        {
            GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);
            await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
        }
    }

    private void GenerateFormattedDataForExcelExport(out string[] columnHeaders,
                                                     out IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>> preformattedData)
    {
        columnHeaders = new string[]
        {
                "Type Name",
                "Instance Size Before",
                "Instance Size After",
                "Instance Size Diff",
                "Alignment Waste (exclusive) Before",
                "Alignment Waste (exclusive) After",
                "Alignment Waste (exclusive) Diff",
                "Alignment Waste (including base types) Before",
                "Alignment Waste (including base types) After",
                "Alignment Waste (including base types) Diff",
                "Used For vfptr (exclusive) Before",
                "Used For vfptr (exclusive) After",
                "Used For vfptr (exclusive) Diff",
                "Used For vfptr (including base types) Before",
                "Used For vfptr (including base types) After",
                "Used For vfptr (including base types) Diff",
        };

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>();
        foreach (var tli in this.TypeLayoutItemDiffs!.Cast<TypeLayoutItemDiff>())
        {
            var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Type Name", tli.UserDefinedType.Name },
                    { "Instance Size Before", tli.BeforeTypeLayout?.UserDefinedType.InstanceSize.ToString(CultureInfo.InvariantCulture) ?? String.Empty },
                    { "Instance Size After", tli.AfterTypeLayout?.UserDefinedType.InstanceSize.ToString(CultureInfo.InvariantCulture) ?? String.Empty },
                    { "Instance Size Diff", tli.InstanceSizeDiff },
                    { "Alignment Waste (exclusive) Before", tli.BeforeTypeLayout?.AlignmentWasteExclusive ?? 0},
                    { "Alignment Waste (exclusive) After", tli.AfterTypeLayout?.AlignmentWasteExclusive ?? 0 },
                    { "Alignment Waste (exclusive) Diff", tli.AlignmentWasteExclusiveDiff },
                    { "Alignment Waste (including base types) Before", tli.BeforeTypeLayout?.AlignmentWasteIncludingBaseTypes ?? 0 },
                    { "Alignment Waste (including base types) After", tli.AfterTypeLayout?.AlignmentWasteIncludingBaseTypes ?? 0 },
                    { "Alignment Waste (including base types) Diff", tli.AlignmentWasteIncludingBaseTypesDiff },
                    { "Used For vfptr (exclusive) Before", tli.BeforeTypeLayout?.UsedForVFPtrsExclusive ?? 0 },
                    { "Used For vfptr (exclusive) After", tli.AfterTypeLayout?.UsedForVFPtrsExclusive ?? 0 },
                    { "Used For vfptr (exclusive) Diff", tli.UsedForVFPtrsExclusiveDiff },
                    { "Used For vfptr (including base types) Before", tli.BeforeTypeLayout?.UsedForVFPtrsIncludingBaseTypes ?? 0 },
                    { "Used For vfptr (including base types) After", tli.AfterTypeLayout?.UsedForVFPtrsIncludingBaseTypes ?? 0 },
                    { "Used For vfptr (including base types) Diff", tli.UsedForVFPtrsIncludingBaseTypesDiff },
                };
            preformattedData.Add(formattedData);
        }
    }

    #endregion
}
