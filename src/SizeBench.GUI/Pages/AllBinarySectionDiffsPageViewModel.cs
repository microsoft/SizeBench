using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllBinarySectionDiffsPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private IReadOnlyList<BinarySectionDiff>? _binarySectionDiffsList;

    public IReadOnlyList<BinarySectionDiff>? BinarySectionDiffs
    {
        get => this._binarySectionDiffsList;
        private set { this._binarySectionDiffsList = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public AllBinarySectionDiffsPageViewModel(IUITaskScheduler taskScheduler,
                                              IDiffSession diffSession,
                                              IExcelExporter excelExporter) : base(diffSession)
    {
        this.ExportToExcelCommand = new DelegateCommand(async () => await taskScheduler.StartExcelExport(excelExporter, this.BinarySectionDiffs));
        this._uiTaskScheduler = taskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        await this._uiTaskScheduler.StartLongRunningUITask($"Enumerating all Binary Section Diffs",
            async (token) => this.BinarySectionDiffs = await this.DiffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(token));
    }
}
