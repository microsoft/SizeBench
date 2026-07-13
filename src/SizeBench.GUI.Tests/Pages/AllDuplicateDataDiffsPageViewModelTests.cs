using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class AllDuplicateDataDiffsPageViewModelTests : IDisposable
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
        var duplicateDiffList = this.TestDataGenerator.GenerateDuplicateDataItemDiffs(out var beforeDDIList, out var afterDDIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateDuplicateDataItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(duplicateDiffList as IReadOnlyList<DuplicateDataItemDiff>));

        var viewmodel = new AllDuplicateDataDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                               this.TestDataGenerator.MockDiffSession.Object,
                                                               this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);

        var symbolNameIndex = columnHeadersList.IndexOf("Symbol Name");
        Assert.IsGreaterThanOrEqualTo(0, symbolNameIndex);
        var totalSizeDiffIndex = columnHeadersList.IndexOf("Total Size Diff");
        Assert.IsGreaterThanOrEqualTo(0, totalSizeDiffIndex);
        var wastedSizeIndex = columnHeadersList.IndexOf("Wasted Size Diff");
        Assert.IsGreaterThanOrEqualTo(0, wastedSizeIndex);
        var remainingWastedSizeIndex = columnHeadersList.IndexOf("Remaining Wasted Size");
        Assert.IsGreaterThanOrEqualTo(0, remainingWastedSizeIndex);

        Assert.HasCount(15, preformattedData);
    }

    [Timeout(5 * 1000, CooperativeCancellation = true)] // 5s
    [TestMethod]
    public async Task CanExportToExcel()
    {
        var duplicateDiffList = this.TestDataGenerator.GenerateDuplicateDataItemDiffs(out var beforeDDIList, out var afterDDIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateDuplicateDataItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(duplicateDiffList as IReadOnlyList<DuplicateDataItemDiff>));

        var viewmodel = new AllDuplicateDataDiffsPageViewModel(this.MockUITaskScheduler.Object,
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
