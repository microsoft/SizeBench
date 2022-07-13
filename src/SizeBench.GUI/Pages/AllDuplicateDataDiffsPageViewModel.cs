using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllDuplicateDataDiffsPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;
    private IReadOnlyList<DuplicateDataItemDiff>? _duplicateDataList;

    public IReadOnlyList<DuplicateDataItemDiff>? DuplicateDataItemDiffs
    {
        get => this._duplicateDataList;
        private set { this._duplicateDataList = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public AllDuplicateDataDiffsPageViewModel(IUITaskScheduler taskScheduler,
                                              IDiffSession diffSession,
                                              IExcelExporter excelExporter) : base(diffSession)
    {
        this._uiTaskScheduler = taskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
    }

    protected internal override async Task InitializeAsync()
    {
        await this._uiTaskScheduler.StartLongRunningUITask($"Diffing Duplicate Data",
            async (token) => this.DuplicateDataItemDiffs = await this.DiffSession.EnumerateDuplicateDataItemDiffs(token));
    }

    #region Excel Export

    private async void ExportToExcel()
    {
        GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);
        await this._uiTaskScheduler.StartExcelExportWithPreformattedData(this._excelExporter, columnHeaders, preformattedData);
    }

    public void GenerateFormattedDataForExcelExport(out string[] columnHeaders,
                                                    out IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>> preformattedData)
    {
        columnHeaders = new string[4]
        {
                "Symbol Name",
                "Total Size Diff",
                "Wasted Size Diff",
                "Remaining Wasted Size",
            // TODO: consider adding some way of showing the diff of ReferencedIn compilands
        };

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.DuplicateDataItemDiffs?.Count ?? 1);
        if (this.DuplicateDataItemDiffs != null)
        {
            foreach (var dupe in this.DuplicateDataItemDiffs)
            {
                var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Symbol Name", dupe.SymbolDiff.Name },
                    { "Total Size Diff", dupe.SizeDiff },
                    { "Wasted Size Diff", dupe.WastedSizeDiff },
                    { "Remaining Wasted Size", dupe.WastedSizeRemaining }
                };
                preformattedData.Add(formattedData);
            }
        }
    }

    #endregion Excel Export
}
