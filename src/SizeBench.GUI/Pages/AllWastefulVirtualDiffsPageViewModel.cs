using System.ComponentModel;
using System.Windows.Data;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllWastefulVirtualDiffsPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;
    private IReadOnlyList<WastefulVirtualItemDiff>? _wastefulVirtualList;
    private ICollectionView? _wastefulVirtualCollectionView;

    public ICollectionView? WastefulVirtualItemDiffs
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

    public AllWastefulVirtualDiffsPageViewModel(IUITaskScheduler taskScheduler,
                                                IDiffSession diffSession,
                                                IExcelExporter excelExporter) : base(diffSession)
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

        await this._uiTaskScheduler.StartLongRunningUITask($"Diffing Wasteful Virtuals",
            async (token) =>
            {
                this._wastefulVirtualList = await this.DiffSession.EnumerateWastefulVirtualItemDiffs(token);
                this.WastefulVirtualItemDiffs = CollectionViewSource.GetDefaultView(this._wastefulVirtualList);
                using var deferRefresh = this.WastefulVirtualItemDiffs.DeferRefresh();
                this.WastefulVirtualItemDiffs.SortDescriptions.Add(new SortDescription(nameof(WastefulVirtualItemDiff.WastedSizeDiff), ListSortDirection.Descending));
                this.WastefulVirtualItemDiffs.Filter = (wvid) =>
                {
                    if (this.ExcludeCOMTypes)
                    {
                        return !((WastefulVirtualItemDiff)wvid).IsCOMType;
                    }
                    else
                    {
                        return true;
                    }
                };
            });
    }

    private async void ExportToExcel()
    {
        if (this.WastefulVirtualItemDiffs != null)
        {
            await this._uiTaskScheduler.StartExcelExport(this._excelExporter, this.WastefulVirtualItemDiffs.Cast<WastefulVirtualItemDiff>().ToList());
        }
    }
}
