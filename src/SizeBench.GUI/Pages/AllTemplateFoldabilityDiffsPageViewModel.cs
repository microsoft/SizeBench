using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllTemplateFoldabilityDiffsPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;
    private IReadOnlyList<TemplateFoldabilityItemDiff>? _templateFoldabilityList;

    public IReadOnlyList<TemplateFoldabilityItemDiff>? TemplateFoldabilityItemDiffs
    {
        get => this._templateFoldabilityList;
        private set { this._templateFoldabilityList = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public AllTemplateFoldabilityDiffsPageViewModel(IUITaskScheduler taskScheduler,
                                                    IDiffSession diffSession,
                                                    IExcelExporter excelExporter) : base(diffSession)
    {
        this._uiTaskScheduler = taskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
    }

    protected internal override async Task InitializeAsync()
    {
        await this._uiTaskScheduler.StartLongRunningUITask($"Diffing Template Foldability",
            async (token) => this.TemplateFoldabilityItemDiffs = await this.DiffSession.EnumerateTemplateFoldabilityItemDiffs(token));
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
                "Template Name",
                "Total Size Diff",
                "Wasted Size Diff",
                "Remaining Wasted Size",
        };

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.TemplateFoldabilityItemDiffs?.Count ?? 1);
        if (this.TemplateFoldabilityItemDiffs != null)
        {
            foreach (var tfiDiff in this.TemplateFoldabilityItemDiffs)
            {
                var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Template Name", tfiDiff.TemplateName },
                    { "Total Size Diff", tfiDiff.SizeDiff },
                    { "Wasted Size Diff", tfiDiff.WastedSizeDiff },
                    { "Remaining Wasted Size", tfiDiff.WastedSizeRemaining }
                };
                preformattedData.Add(formattedData);
            }
        }
    }

    #endregion Excel Export
}
