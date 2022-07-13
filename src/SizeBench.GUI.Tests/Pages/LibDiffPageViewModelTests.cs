using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Pages;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBenchV2.ViewModels.Tests;

[TestClass]
public sealed class LibDiffPageViewModelTests : IDisposable
{
    private DiffTestDataGenerator TestDataGenerator = new DiffTestDataGenerator();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestDataGenerator = new DiffTestDataGenerator();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
        this.MockExcelExporter = new Mock<IExcelExporter>();
    }

    [Timeout(5 * 1000)]
    [TestMethod]
    public async Task SymbolsInitializeAsyncFromConstruction()
    {
        var aLib = this.TestDataGenerator.ALibDiff;
        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.LibDiffs as IReadOnlyList<LibDiff>));
        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var allSymbolDiffsInLibDiff = this.TestDataGenerator.GenerateSymbolDiffsInLibList(aLib);

        var tcsSymbolsReady = new TaskCompletionSource<IReadOnlyList<SymbolDiff>>();
        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateSymbolDiffsInLibDiff(aLib, It.IsAny<CancellationToken>()))
                              .Returns(tcsSymbolsReady.Task);

        var viewmodel = new LibDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                 this.MockExcelExporter.Object,
                                                 this.TestDataGenerator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "Lib", aLib.Name }
            });
        var initTask = viewmodel.InitializeAsync();

        var propertyNameResult = String.Empty;

        viewmodel.PropertyChanged += (s, e) =>
        {
            propertyNameResult = e.PropertyName;
            tcsTestResultsComplete.SetResult(new object());
        };

        tcsSymbolsReady.SetResult(allSymbolDiffsInLibDiff);
        await tcsTestResultsComplete.Task;
        await initTask;

        Assert.AreEqual(nameof(LibDiffPageViewModel.SymbolDiffs), propertyNameResult);
        CollectionAssert.AreEqual(allSymbolDiffsInLibDiff, viewmodel.SymbolDiffs!.ToList());
    }

    [Timeout(1000 * 5)] // 5s
    [TestMethod]
    public async Task CanExportSymbolsToExcel()
    {
        var aLib = this.TestDataGenerator.ALibDiff;

        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.LibDiffs as IReadOnlyList<LibDiff>));

        var allSymbolsInLibDiff = this.TestDataGenerator.GenerateSymbolDiffsInLibList(aLib);

        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateSymbolDiffsInLibDiff(aLib, It.IsAny<CancellationToken>()))
                              .Returns(Task.FromResult(allSymbolsInLibDiff as IReadOnlyList<SymbolDiff>));

        var viewmodel = new LibDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                 this.MockExcelExporter.Object,
                                                 this.TestDataGenerator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "Lib", aLib.Name }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ExportSymbolsToExcelCommand.CanExecute());

        viewmodel.ExportSymbolsToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(this.MockExcelExporter.Object, allSymbolsInLibDiff), Times.Exactly(1));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
