using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllTemplateFoldabilityPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;
    private IReadOnlyList<TemplateFoldabilityItem>? _templateFoldabilityItems;

    public IReadOnlyList<TemplateFoldabilityItem>? TemplateFoldabilityItems
    {
        get => this._templateFoldabilityItems;
        set { this._templateFoldabilityItems = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public AllTemplateFoldabilityPageViewModel(IUITaskScheduler taskScheduler,
                                               ISession session,
                                               IExcelExporter excelExporter) : base(session)
    {
        this._uiTaskScheduler = taskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
        LoadTemplateFoldabilityItems(taskScheduler);
    }

    private async void LoadTemplateFoldabilityItems(IUITaskScheduler taskScheduler)
    {
        await taskScheduler.StartLongRunningUITask($"Exploring Template Foldability",
            async (token) => this.TemplateFoldabilityItems = await this.Session.EnumerateTemplateFoldabilityItems(token));
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
        columnHeaders = new string[7]
        {
                "Template Name",
                "Total Size",
                "Wasted Size",
                "# Symbols",
                "# Unique Symbols (post-folding)",
                "% Similarity",
                "Example Symbols",
        };

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.TemplateFoldabilityItems?.Count ?? 1);
        if (this.TemplateFoldabilityItems != null)
        {
            foreach (var tfi in this.TemplateFoldabilityItems)
            {
                var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Template Name", tfi.TemplateName },
                    { "Total Size", tfi.TotalSize },
                    { "Wasted Size", tfi.WastedSize },
                    { "# Symbols", tfi.Symbols.Count },
                    { "# Unique Symbols (post-folding)", tfi.UniqueSymbols.Count },
                    { "% Similarity", tfi.PercentageSimilarity.ToString("P1", CultureInfo.InvariantCulture.NumberFormat) },
                    { "Example Symbols", String.Join(Environment.NewLine, (from symbol in tfi.Symbols select symbol.FullName).Take(5)) }
                };
                preformattedData.Add(formattedData);
            }
        }
    }

    #endregion Excel Export
}
