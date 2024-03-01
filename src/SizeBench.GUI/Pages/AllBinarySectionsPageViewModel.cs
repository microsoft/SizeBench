using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class AllBinarySectionsPageViewModel : SingleBinaryViewModelBase
{
    private IReadOnlyList<BinarySection>? _binarySectionsList;

    public IReadOnlyList<BinarySection>? BinarySections
    {
        get => this._binarySectionsList;
        private set { this._binarySectionsList = value; RaisePropertyChanged(); }
    }

    public DelegateCommand ExportToExcelCommand { get; }

    public AllBinarySectionsPageViewModel(IUITaskScheduler taskScheduler,
                                          ISession session,
                                          IExcelExporter excelExporter) : base(session)
    {
        this.ExportToExcelCommand = new DelegateCommand(async () => await taskScheduler.StartExcelExport(excelExporter, this.BinarySections));
        LoadBinarySections(taskScheduler);
    }

    private async void LoadBinarySections(IUITaskScheduler taskScheduler)
    {
        await taskScheduler.StartLongRunningUITask($"Enumerating all Binary Sections",
            async (token) => this.BinarySections = await this.Session.EnumerateBinarySectionsAndCOFFGroups(token));
    }
}
