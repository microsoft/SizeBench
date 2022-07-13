using System.ComponentModel;
using System.Windows.Data;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllWastefulVirtualsPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;
    private IReadOnlyList<WastefulVirtualItem>? _wastefulVirtualList;
    private ICollectionView? _wastefulVirtualCollectionView;

    public ICollectionView? WastefulVirtualItems
    {
        get => this._wastefulVirtualCollectionView;
        private set { this._wastefulVirtualCollectionView = value; RaisePropertyChanged(); }
    }

    private bool _excludeCOMTypes;

    public bool ExcludeCOMTypes
    {
        get => this._excludeCOMTypes;
        set
        {
            this._excludeCOMTypes = value;
            this._wastefulVirtualCollectionView?.Refresh();
            OnRequestFragmentNavigation(this._excludeCOMTypes ? nameof(this.ExcludeCOMTypes) : String.Empty);
            RaisePropertyChanged();
        }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public AllWastefulVirtualsPageViewModel(IUITaskScheduler taskScheduler,
                                            ISession session,
                                            IExcelExporter excelExporter) : base(session)
    {
        this._uiTaskScheduler = taskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
    }

    protected override Task OnCurrentFragmentChanged()
    {
        this.ExcludeCOMTypes = this.CurrentFragment == nameof(this.ExcludeCOMTypes);
        return Task.CompletedTask;
    }

    protected internal override async Task InitializeAsync()
    {
        if (this.CurrentFragment == nameof(this.ExcludeCOMTypes))
        {
            this.ExcludeCOMTypes = true;
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating Wasteful Virtuals",
            async (token) =>
            {
                this._wastefulVirtualList = await this.Session.EnumerateWastefulVirtuals(token);
                this.WastefulVirtualItems = CollectionViewSource.GetDefaultView(this._wastefulVirtualList);
                using var deferRefresh = this.WastefulVirtualItems.DeferRefresh();
                this.WastefulVirtualItems.SortDescriptions.Add(new SortDescription(nameof(WastefulVirtualItem.WastedSize), ListSortDirection.Descending));
                this.WastefulVirtualItems.Filter = (wvi) =>
                {
                    if (this.ExcludeCOMTypes)
                    {
                        return !((WastefulVirtualItem)wvi).IsCOMType;
                    }
                    else
                    {
                        return true;
                    }
                };
            });
    }

    #region Excel Export

    private async void ExportToExcel()
    {
        if (this.WastefulVirtualItems != null)
        {
            GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);
            await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
        }
    }

    public void GenerateFormattedDataForExcelExport(out string[] columnHeaders,
                                                    out IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>> preformattedData)
    {
        columnHeaders = new string[5]
        {
                "Type Name",
                "Waste Per Slot",
                "Wasted Size Total",
                "Wasteful pure virtuals with exactly one override",
                "Wasteful virtuals with no overrides"
        };

        var itemsAfterFiltering = this.WastefulVirtualItems!.Cast<WastefulVirtualItem>().ToList();

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(itemsAfterFiltering.Count);
        foreach (var wastefulVirtualType in itemsAfterFiltering)
        {
            var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Type Name", wastefulVirtualType.UserDefinedType.Name },
                    { "Waste Per Slot", wastefulVirtualType.WastePerSlot },
                    { "Wasted Size Total", wastefulVirtualType.WastedSize },
                    { "Wasteful pure virtuals with exactly one override", String.Join(Environment.NewLine, from wastedOverride
                                                                                                           in wastefulVirtualType.WastedOverridesPureWithExactlyOneOverride
                                                                                                           select wastedOverride.FormattedName.GetFormattedName(WastefulVirtualItem.NameFormattingForWastedOverrides)) },
                    { "Wasteful virtuals with no overrides", String.Join(Environment.NewLine, from wastedOverride
                                                                                              in wastefulVirtualType.WastedOverridesNonPureWithNoOverrides
                                                                                              select wastedOverride.FormattedName.GetFormattedName(WastefulVirtualItem.NameFormattingForWastedOverrides)) }
                };
            preformattedData.Add(formattedData);
        }
    }

    #endregion Excel Export
}
