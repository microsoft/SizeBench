using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class CompilandDiffPageViewModelTests : IDisposable
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

    [Timeout(5 * 1000, CooperativeCancellation = true)]
    [TestMethod]
    public async Task SymbolsInitializeAsync()
    {
        var a1Compiland = this.TestDataGenerator.A1CompilandDiff;
        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateCompilandDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.CompilandDiffs as IReadOnlyList<CompilandDiff>));

        var allSymbolsInCompilandDiff = this.TestDataGenerator.GenerateSymbolDiffsInCompilandList(a1Compiland, null);

        var tcsSymbolsReady = new TaskCompletionSource<IReadOnlyList<SymbolDiff>>();
        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateSymbolDiffsInCompilandDiff(a1Compiland, It.IsAny<CancellationToken>()))
                              .Returns(tcsSymbolsReady.Task);

        var viewmodel = new CompilandDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                       this.MockExcelExporter.Object,
                                                       this.TestDataGenerator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "Compiland", a1Compiland.Name },
                { "Lib", a1Compiland.LibDiff.Name }
            });
        var initTask = viewmodel.InitializeAsync();

        var sawSymbolDiffsPropertyChange = false;

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CompilandDiffPageViewModel.SymbolDiffs))
            {
                sawSymbolDiffsPropertyChange = true;
                tcsTestResultsComplete.TrySetResult(new object());
            }
        };

        tcsSymbolsReady.SetResult(allSymbolsInCompilandDiff);
        await tcsTestResultsComplete.Task;
        await initTask;

        Assert.IsTrue(sawSymbolDiffsPropertyChange);
        Assert.AreSequenceEqual(allSymbolsInCompilandDiff, viewmodel.SymbolDiffs!.ToList());
        Assert.AreEqual(allSymbolsInCompilandDiff.Count, viewmodel.FilteredSymbolDiffs!.Cast<SymbolDiff>().Count());
    }

    [Timeout(1000 * 5, CooperativeCancellation = true)] // 5s
    [TestMethod]
    public async Task CanExportSymbolsToExcel()
    {
        var a1Compiland = this.TestDataGenerator.A1CompilandDiff;

        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateCompilandDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.CompilandDiffs as IReadOnlyList<CompilandDiff>));

        var allSymbolsInCompilandDiff = this.TestDataGenerator.GenerateSymbolDiffsInCompilandList(a1Compiland, null);

        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateSymbolDiffsInCompilandDiff(a1Compiland, It.IsAny<CancellationToken>()))
                              .Returns(Task.FromResult(allSymbolsInCompilandDiff as IReadOnlyList<SymbolDiff>));

        var viewmodel = new CompilandDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                       this.MockExcelExporter.Object,
                                                       this.TestDataGenerator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "Compiland", a1Compiland.Name },
                { "Lib", a1Compiland.LibDiff.Name }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ExportSymbolsToExcelCommand.CanExecute());

        viewmodel.ExportSymbolsToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(this.MockExcelExporter.Object,
                                                                      It.Is<IReadOnlyList<SymbolDiff>>(symbols => symbols.Count == allSymbolsInCompilandDiff.Count &&
                                                                                                                  !symbols.Except(allSymbolsInCompilandDiff).Any())),
                                        Times.Exactly(1));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
