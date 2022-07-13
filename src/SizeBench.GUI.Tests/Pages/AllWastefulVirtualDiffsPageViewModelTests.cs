using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class AllWastefulVirtualDiffsPageViewModelTests : IDisposable
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

    [Timeout(5 * 1000)] // 5s
    [TestMethod]
    public async Task CanExportToExcel()
    {
        IReadOnlyList<WastefulVirtualItemDiff> wviDiffList = this.TestDataGenerator.GenerateWastefulVirtualItemDiffs(out var beforeWVIList, out var afterWVIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateWastefulVirtualItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(wviDiffList));

        var viewmodel = new AllWastefulVirtualDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                                 this.TestDataGenerator.MockDiffSession.Object,
                                                                 this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ExportToExcelCommand.CanExecute());

        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(this.MockExcelExporter.Object, It.IsAny<IReadOnlyList<WastefulVirtualItemDiff>>()));

        viewmodel.ExportToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(this.MockExcelExporter.Object, It.IsAny<IReadOnlyList<WastefulVirtualItemDiff>>()), Times.Exactly(1));
    }

    [TestMethod]
    public async Task TogglingExcludeCOMTypesRefreshesView()
    {
        var wviDiffList = this.TestDataGenerator.GenerateWastefulVirtualItemDiffs(out var beforeWVIList, out var afterWVIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateWastefulVirtualItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(wviDiffList as IReadOnlyList<WastefulVirtualItemDiff>));

        var numberOfCOMTypes = wviDiffList.Count(wviDiff => wviDiff.IsCOMType);
        var numberOfNonCOMTypes = wviDiffList.Count - numberOfCOMTypes;

        var viewmodel = new AllWastefulVirtualDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                                 this.TestDataGenerator.MockDiffSession.Object,
                                                                 this.MockExcelExporter.Object);
        await viewmodel.SetCurrentFragment("ExcludeCOMTypes");
        await viewmodel.InitializeAsync();

        var vmPropertyChangesSeen = new List<string>();
        viewmodel.PropertyChanged += (s, e) => vmPropertyChangesSeen.Add(e.PropertyName!);
        var collectionChangesSeen = 0;
        viewmodel.WastefulVirtualItemDiffs!.CollectionChanged += (s, e) => collectionChangesSeen++;

        Assert.IsTrue(viewmodel.ExcludeCOMTypes);
        Assert.AreEqual(numberOfNonCOMTypes, viewmodel.WastefulVirtualItemDiffs.Cast<WastefulVirtualItemDiff>().ToList().Count);

        viewmodel.ExcludeCOMTypes = false;

        Assert.AreEqual(1, vmPropertyChangesSeen.Count);
        Assert.AreEqual(nameof(AllWastefulVirtualDiffsPageViewModel.ExcludeCOMTypes), vmPropertyChangesSeen[0]);
        Assert.AreEqual(numberOfCOMTypes + numberOfNonCOMTypes, viewmodel.WastefulVirtualItemDiffs.Cast<WastefulVirtualItemDiff>().ToList().Count);
        Assert.AreEqual(1, collectionChangesSeen); // Even though we filtered out multiple items, should just see one INCC due to the DeferRefresh, to keep the UI responsive

        // Toggling back should restore everything to the way it was
        viewmodel.ExcludeCOMTypes = true;
        Assert.AreEqual(2, vmPropertyChangesSeen.Count);
        Assert.AreEqual(nameof(AllWastefulVirtualDiffsPageViewModel.ExcludeCOMTypes), vmPropertyChangesSeen[0]);
        Assert.AreEqual(nameof(AllWastefulVirtualDiffsPageViewModel.ExcludeCOMTypes), vmPropertyChangesSeen[1]);
        Assert.AreEqual(numberOfNonCOMTypes, viewmodel.WastefulVirtualItemDiffs.Cast<WastefulVirtualItemDiff>().ToList().Count);
        Assert.AreEqual(2, collectionChangesSeen);
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
