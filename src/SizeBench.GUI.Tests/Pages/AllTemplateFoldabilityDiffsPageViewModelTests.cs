using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Pages;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBenchV2.ViewModels.Tests;

[TestClass]
public sealed class AllTemplateFoldabilityDiffsPageViewModelTests : IDisposable
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

    [TestMethod]
    public async Task ExcelExportDataIsFormattedUsefully()
    {
        var tfiDiffList = this.TestDataGenerator.GenerateTemplateFoldabilityItemDiffs(out var beforeTFIList, out var afterTFIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateTemplateFoldabilityItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(tfiDiffList as IReadOnlyList<TemplateFoldabilityItemDiff>));

        var viewmodel = new AllTemplateFoldabilityDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                                     this.TestDataGenerator.MockDiffSession.Object,
                                                                     this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);

        var symbolNameIndex = columnHeadersList.IndexOf("Template Name");
        Assert.IsTrue(symbolNameIndex >= 0);
        var totalSizeDiffIndex = columnHeadersList.IndexOf("Total Size Diff");
        Assert.IsTrue(totalSizeDiffIndex >= 0);
        var wastedSizeIndex = columnHeadersList.IndexOf("Wasted Size Diff");
        Assert.IsTrue(wastedSizeIndex >= 0);
        var remainingWastedSizeIndex = columnHeadersList.IndexOf("Remaining Wasted Size");
        Assert.IsTrue(remainingWastedSizeIndex >= 0);

        Assert.AreEqual(3, preformattedData.Count);
    }

    [Timeout(5 * 1000)] // 5s
    [TestMethod]
    public async Task CanExportToExcel()
    {
        var tfiDiffList = this.TestDataGenerator.GenerateTemplateFoldabilityItemDiffs(out var beforeTFIList, out var afterTFIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateTemplateFoldabilityItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(tfiDiffList as IReadOnlyList<TemplateFoldabilityItemDiff>));

        var viewmodel = new AllTemplateFoldabilityDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                                     this.TestDataGenerator.MockDiffSession.Object,
                                                                     this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ExportToExcelCommand.CanExecute());

        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                         It.IsAny<IList<string>>(),
                                                                                         It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()));

        viewmodel.ExportToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                          It.IsAny<IList<string>>(),
                                                                                          It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()),
                                        Times.Exactly(1));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
