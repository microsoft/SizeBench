using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllAnnotationsPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;
    private IReadOnlyList<AnnotationSymbol>? _allAnnotations;

    public IReadOnlyList<AnnotationSymbol>? Annotations
    {
        get => this._allAnnotations;
        private set
        {
            this._allAnnotations = value;
            RaisePropertyChanged();
        }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public AllAnnotationsPageViewModel(IUITaskScheduler taskScheduler,
                                       ISession session,
                                       IExcelExporter excelExporter) : base(session)
    {
        this._uiTaskScheduler = taskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
    }

    protected internal override Task InitializeAsync()
    {
        return this._uiTaskScheduler.StartLongRunningUITask($"Enumerating Annotations",
            async (token) => this.Annotations = await this.Session.EnumerateAnnotations(token));
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
        columnHeaders = new string[3]
        {
                "Annotation Text",
                "Source File",
                "Line Number",
        };

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.Annotations?.Count ?? 1);
        if (this.Annotations != null)
        {
            foreach (var annotation in this.Annotations)
            {
                var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Annotation Text", annotation.Text },
                    { "Source File", annotation.SourceFile?.Name ?? String.Empty },
                    { "Line Number", annotation.LineNumber }
                };
                preformattedData.Add(formattedData);
            }
        }
    }

    #endregion
}
