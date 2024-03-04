using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;
using SizeBench.GUI.Models;

namespace SizeBench.GUI.Pages;

internal sealed class AllInlinesPageViewModel : SingleBinaryViewModelBase
{
    private IReadOnlyList<InlineSiteSymbol>? _inlineSites;
    private List<InlineSiteGroup> _inlineSiteGroups = new List<InlineSiteGroup>();

    public List<InlineSiteGroup> InlineSiteGroups
    {
        get => this._inlineSiteGroups;
        private set { this._inlineSiteGroups = value; RaisePropertyChanged(); }
    }

    private readonly IUITaskScheduler _uiTaskScheduler;
#pragma warning disable IDE0052 // Remove unread private members - will be used by the time this page is fully implemented
    private readonly IExcelExporter _excelExporter;
#pragma warning restore IDE0052 // Remove unread private members

    public DelegateCommand ExportToExcelCommand { get; }

    public AllInlinesPageViewModel(IUITaskScheduler uiTaskScheduler,
                                   ISession session,
                                   IExcelExporter excelExporter) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
        this._excelExporter = excelExporter;
        this.ExportToExcelCommand = new DelegateCommand(ExportToExcel);
    }

    protected internal override async Task InitializeAsync()
    {
        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Inline Sites",
            async (token) => this._inlineSites = await this.Session.EnumerateAllInlineSites(token));

        if (this._inlineSites is not null)
        {
            this.InlineSiteGroups = this._inlineSites
                .GroupBy(s => s.Name)
                .Select(g => new InlineSiteGroup(g.Key, g.ToList()))
                .OrderByDescending(g => g.TotalSize)
                .ToList();
        }
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
            "Name of Inlined Function",
            "# Inline Sites",
            "Total Size",
        };

        preformattedData = new List<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>(this.InlineSiteGroups?.Count ?? 1);
        if (this.InlineSiteGroups is not null)
        {
            foreach (var inlineSiteGroup in this.InlineSiteGroups)
            {
                var formattedData = new DictionaryThatDoesntThrowWhenKeyNotPresent<object>
                {
                    { "Name of Inlined Function", inlineSiteGroup.InlinedFunctionName },
                    { "# Inline Sites", inlineSiteGroup.InlineSites.Count },
                    { "Total Size", inlineSiteGroup.TotalSize }
                };
                preformattedData.Add(formattedData);
            }
        }
    }

    #endregion
}
