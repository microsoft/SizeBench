using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Pages;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBenchV2.ViewModels.Tests;

[TestClass]
public sealed class AllBinarySectionDiffsPageViewModelTests : IDisposable
{
    private DiffTestDataGenerator TestDataGenerator = new DiffTestDataGenerator();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestDataGenerator = new DiffTestDataGenerator();
        this.MockExcelExporter = new Mock<IExcelExporter>();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [Timeout(30 * 1000)] // 30s
    [TestMethod]
    public async Task CanExportToExcel()
    {
        var sectionDiffList = this.TestDataGenerator.BinarySectionDiffs;
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateBinarySectionsAndCOFFGroupDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(sectionDiffList as IReadOnlyList<BinarySectionDiff>));
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(this.MockExcelExporter.Object, sectionDiffList));

        var viewmodel = new AllBinarySectionDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                               this.TestDataGenerator.MockDiffSession.Object,
                                                               this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ExportToExcelCommand.CanExecute());

        viewmodel.ExportToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(this.MockExcelExporter.Object, sectionDiffList), Times.Exactly(1));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
