using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllDuplicateDataPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private readonly IExcelExporter _excelExporter;
    private IReadOnlyList<DuplicateDataItem>? _duplicateDataList;

    public IReadOnlyList<DuplicateDataItem>? DuplicateDataItems
    {
        get => this._duplicateDataList;
        private set { this._duplicateDataList = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public AllDuplicateDataPageViewModel(IUITaskScheduler taskScheduler,
                                         ISession session,
                                         IExcelExporter excelExporter) : base(session)
    {
        this._uiTaskScheduler = taskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
    }

    protected internal override async Task InitializeAsync()
    {
        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating Duplicate Data",
            async (token) => this.DuplicateDataItems = await this.Session.EnumerateDuplicateDataItems(token));
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
                "Size",
                "Wasted Size",
                "Referenced In",
        };

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.DuplicateDataItems?.Count ?? 1);
        if (this.DuplicateDataItems != null)
        {
            foreach (var dupe in this.DuplicateDataItems)
            {
                var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Symbol Name", dupe.Symbol.Name },
                    { "Size", dupe.Symbol.Size },
                    { "Wasted Size", dupe.WastedSize },
                    { "Referenced In", String.Join(", ", from reference in dupe.ReferencedIn select reference.ShortName) }
                };
                preformattedData.Add(formattedData);
            }
        }
    }

    #endregion Excel Export
}
