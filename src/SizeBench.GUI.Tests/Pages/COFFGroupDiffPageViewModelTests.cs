using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Pages;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBenchV2.ViewModels.Tests;

[TestClass]
public sealed class COFFGroupDiffPageViewModelTests : IDisposable
{
    private DiffTestDataGenerator TestDataGenerator = new DiffTestDataGenerator();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestDataGenerator = new DiffTestDataGenerator();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockExcelExporter = new Mock<IExcelExporter>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    private COFFGroupDiffPageViewModel CreateViewmodelForTesting(COFFGroupDiff cgDiff,
                                                                 out TaskCompletionSource<object> tcsTestResultsComplete,
                                                                 out List<SymbolDiff> symbolList,
                                                                 out TaskCompletionSource<IReadOnlyList<BinarySectionDiff>> tcsCOFFGroupReady,
                                                                 out TaskCompletionSource<IReadOnlyList<SymbolDiff>> tcsSymbolsReady,
                                                                 out TaskCompletionSource<IReadOnlyList<LibDiff>> tcsLibsReady,
                                                                 out TaskCompletionSource<IReadOnlyList<CompilandDiff>> tcsCompilandsReady)
    {
        tcsTestResultsComplete = new TaskCompletionSource<object>();
        symbolList = this.TestDataGenerator
                         .GenerateSymbolDiffsInCOFFGroupList(cgDiff);
        tcsCOFFGroupReady = new TaskCompletionSource<IReadOnlyList<BinarySectionDiff>>();
        tcsSymbolsReady = new TaskCompletionSource<IReadOnlyList<SymbolDiff>>();
        tcsLibsReady = new TaskCompletionSource<IReadOnlyList<LibDiff>>();
        tcsCompilandsReady = new TaskCompletionSource<IReadOnlyList<CompilandDiff>>();
        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroupDiffs(It.IsAny<CancellationToken>()))
                                              .Returns(tcsCOFFGroupReady.Task);
        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateSymbolDiffsInCOFFGroupDiff(cgDiff, It.IsAny<CancellationToken>()))
                                              .Returns(tcsSymbolsReady.Task);
        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateLibDiffs(It.IsAny<CancellationToken>()))
                                              .Returns(tcsLibsReady.Task);
        this.TestDataGenerator.MockDiffSession.Setup(s => s.EnumerateCompilandDiffs(It.IsAny<CancellationToken>()))
                                              .Returns(tcsCompilandsReady.Task);

        return new COFFGroupDiffPageViewModel(this.MockUITaskScheduler.Object,
                                              this.TestDataGenerator.MockDiffSession.Object,
                                              this.MockExcelExporter.Object);
    }

    private async Task AssertInitialStateThenFillInAsyncLoads(COFFGroupDiffPageViewModel viewmodel,
                                                              TaskCompletionSource<object> tcsTestResultsComplete,
                                                              List<SymbolDiff>? symbolList,
                                                              TaskCompletionSource<IReadOnlyList<BinarySectionDiff>> tcsCOFFGroupReady,
                                                              TaskCompletionSource<IReadOnlyList<SymbolDiff>> tcsSymbolsReady,
                                                              TaskCompletionSource<IReadOnlyList<LibDiff>> tcsLibsReady,
                                                              TaskCompletionSource<IReadOnlyList<CompilandDiff>> tcsCompilandsReady)
    {
        Assert.AreEqual(String.Empty, viewmodel.ContributionSizeSortMemberPath);
        Assert.AreEqual(String.Empty, viewmodel.ContributionVirtualSizeSortMemberPath);
        Assert.IsNull(viewmodel.COFFGroupDiff);
        Assert.IsNull(viewmodel.CompilandDiffs);
        Assert.IsNull(viewmodel.LibDiffs);
        Assert.IsNull(viewmodel.SymbolDiffs);

        var sawCOFFGroupPropertyChange = false;
        var sawSymbolsPropertyChange = false;
        var sawCompilandsPropertyChange = false;
        var sawLibsPropertyChange = false;

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(COFFGroupDiffPageViewModel.COFFGroupDiff))
            {
                sawCOFFGroupPropertyChange = true;
            }
            else if (e.PropertyName == nameof(COFFGroupDiffPageViewModel.SymbolDiffs))
            {
                sawSymbolsPropertyChange = true;
            }
            else if (e.PropertyName == nameof(COFFGroupDiffPageViewModel.CompilandDiffs))
            {
                sawCompilandsPropertyChange = true;
            }
            else if (e.PropertyName == nameof(COFFGroupDiffPageViewModel.LibDiffs))
            {
                sawLibsPropertyChange = true;
            }

            if (sawCOFFGroupPropertyChange &&
                (sawSymbolsPropertyChange || symbolList is null) &&
                sawCompilandsPropertyChange &&
                sawLibsPropertyChange)
            {
                tcsTestResultsComplete.SetResult(new object());
            }
        };

        tcsCOFFGroupReady.SetResult(this.TestDataGenerator.BinarySectionDiffs);
        if (symbolList is null)
        {
            tcsSymbolsReady.SetCanceled();
        }
        else
        {
            tcsSymbolsReady.SetResult(symbolList);
        }

        tcsLibsReady.SetResult(this.TestDataGenerator.LibDiffs);
        tcsCompilandsReady.SetResult(this.TestDataGenerator.CompilandDiffs);

        // This won't fire until we see all the property changes that we expect to see
        await tcsTestResultsComplete.Task;
    }

    [Timeout(5 * 1000)]
    [TestMethod]
    public async Task CompilandsLibsAndSymbolsInitializeAsyncFromConstruction()
    {
        var textMnCGDiff = this.TestDataGenerator.TextMnCGDiff;
        var viewmodel = CreateViewmodelForTesting(textMnCGDiff,
                                                                         out var tcsTestResultsComplete,
                                                                         out var symbolList,
                                                                         out var tcsCOFFGroupReady,
                                                                         out var tcsSymbolsReady,
                                                                         out var tcsLibsReady,
                                                                         out var tcsCompilandsReady);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "COFFGroup", ".text$mn" }
            });

        var initTask = viewmodel.InitializeAsync();

        await AssertInitialStateThenFillInAsyncLoads(viewmodel,
                                                     tcsTestResultsComplete,
                                                     symbolList,
                                                     tcsCOFFGroupReady,
                                                     tcsSymbolsReady,
                                                     tcsLibsReady,
                                                     tcsCompilandsReady);

        await initTask;

        Assert.IsTrue(ReferenceEquals(textMnCGDiff, viewmodel.COFFGroupDiff));
        // It's not worth having a whole test just for this one property on the VM, so test it here even though it's not really
        // related to the async loading stuff.
        Assert.AreEqual("COFFGroupContributionDiffsByName[.text$mn].SizeDiff", viewmodel.ContributionSizeSortMemberPath);
        Assert.AreEqual("COFFGroupContributionDiffsByName[.text$mn].VirtualSizeDiff", viewmodel.ContributionVirtualSizeSortMemberPath);
        Assert.IsTrue(ReferenceEquals(symbolList, viewmodel.SymbolDiffs));

        // Verify libs are filtered to the CG
        Assert.AreEqual(4, this.TestDataGenerator.LibDiffs.Count);
        CollectionAssert.AreEquivalent(this.TestDataGenerator.LibDiffs.Where(ld => ld.COFFGroupContributionDiffs.ContainsKey(textMnCGDiff)).ToList(), viewmodel.LibDiffs!.Cast<LibDiff>().ToList());

        // Verify compilands are filtered to the CG
        Assert.AreEqual(8, this.TestDataGenerator.CompilandDiffs.Count);
        Assert.AreEqual(3, viewmodel.CompilandDiffs!.Count);
        Assert.IsTrue(viewmodel.CompilandDiffs.Contains(this.TestDataGenerator.A1CompilandDiff));
        Assert.IsTrue(viewmodel.CompilandDiffs.Contains(this.TestDataGenerator.A2CompilandDiff));
        Assert.IsTrue(viewmodel.CompilandDiffs.Contains(this.TestDataGenerator.B1CompilandDiff));
    }

    [Timeout(5 * 1000)]
    [TestMethod]
    public async Task CancelingSymbolLoadingIsFine()
    {
        var textMnCGDiff = this.TestDataGenerator.TextMnCGDiff;
        var viewmodel = CreateViewmodelForTesting(textMnCGDiff,
                                                                         out var tcsTestResultsComplete,
                                                                         out var symbolList,
                                                                         out var tcsCOFFGroupReady,
                                                                         out var tcsSymbolsReady,
                                                                         out var tcsLibsReady,
                                                                         out var tcsCompilandsReady);



        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(COFFGroupDiffPageViewModel.SymbolDiffs))
            {
                Assert.Fail("Symbols property should not change if Symbol loading is canceled");
            }
        };

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "COFFGroup", ".text$mn" }
            });

        var initTask = viewmodel.InitializeAsync();

        await AssertInitialStateThenFillInAsyncLoads(viewmodel,
                                                     tcsTestResultsComplete,
                                                     null /* will cause the symbol loading task to set as canceled */,
                                                     tcsCOFFGroupReady,
                                                     tcsSymbolsReady,
                                                     tcsLibsReady,
                                                     tcsCompilandsReady);

        await initTask;

        // The symbol loading was canceled, so it's null, but the rest can still succeed
        Assert.IsNull(viewmodel.SymbolDiffs);

        Assert.IsTrue(ReferenceEquals(textMnCGDiff, viewmodel.COFFGroupDiff));
        // It's not worth having a whole test just for this one property on the VM, so test it here even though it's not really
        // related to the async loading stuff.
        Assert.AreEqual("COFFGroupContributionDiffsByName[.text$mn].SizeDiff", viewmodel.ContributionSizeSortMemberPath);
        Assert.AreEqual("COFFGroupContributionDiffsByName[.text$mn].VirtualSizeDiff", viewmodel.ContributionVirtualSizeSortMemberPath);

        // Verify libs are filtered to the CG
        Assert.AreEqual(4, this.TestDataGenerator.LibDiffs.Count);
        CollectionAssert.AreEquivalent(this.TestDataGenerator.LibDiffs.Where(ld => ld.COFFGroupContributionDiffs.ContainsKey(textMnCGDiff)).ToList(), viewmodel.LibDiffs!.Cast<LibDiff>().ToList());

        // Verify compilands are filtered to the CG
        Assert.AreEqual(8, this.TestDataGenerator.CompilandDiffs.Count);
        Assert.AreEqual(3, viewmodel.CompilandDiffs!.Count);
        Assert.IsTrue(viewmodel.CompilandDiffs.Contains(this.TestDataGenerator.A1CompilandDiff));
        Assert.IsTrue(viewmodel.CompilandDiffs.Contains(this.TestDataGenerator.A2CompilandDiff));
        Assert.IsTrue(viewmodel.CompilandDiffs.Contains(this.TestDataGenerator.B1CompilandDiff));
    }

    [Timeout(5 * 1000)]
    [TestMethod]
    public async Task ExportToExcelWorks()
    {
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(this.MockExcelExporter.Object, It.IsAny<IReadOnlyList<SymbolDiff>>()));
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(this.MockExcelExporter.Object, It.IsAny<IReadOnlyList<LibDiff>>()));
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(this.MockExcelExporter.Object, It.IsAny<IReadOnlyList<CompilandDiff>>()));

        var textMnCGDiff = this.TestDataGenerator.TextMnCGDiff;
        var viewmodel = CreateViewmodelForTesting(textMnCGDiff,
                                                                         out var tcsTestResultsComplete,
                                                                         out var symbolList,
                                                                         out var tcsCOFFGroupReady,
                                                                         out var tcsSymbolsReady,
                                                                         out var tcsLibsReady,
                                                                         out var tcsCompilandsReady);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "COFFGroup", ".text$mn" }
            });

        var initTask = viewmodel.InitializeAsync();

        await AssertInitialStateThenFillInAsyncLoads(viewmodel,
                                                     tcsTestResultsComplete,
                                                     symbolList,
                                                     tcsCOFFGroupReady,
                                                     tcsSymbolsReady,
                                                     tcsLibsReady,
                                                     tcsCompilandsReady);

        await initTask;

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(this.MockExcelExporter.Object, viewmodel.SymbolDiffs), Times.Never());
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object, It.IsAny<IList<string>>(), It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()), Times.Never());

        Assert.IsTrue(viewmodel.ExportSymbolsToExcelCommand.CanExecute());
        Assert.IsTrue(viewmodel.ExportLibsToExcelCommand.CanExecute());
        Assert.IsTrue(viewmodel.ExportCompilandsToExcelCommand.CanExecute());

        viewmodel.ExportSymbolsToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object, It.IsAny<IList<string>>(), It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()), Times.Exactly(1));
        this.MockUITaskScheduler.Invocations.Clear();

        viewmodel.ExportLibsToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(this.MockExcelExporter.Object, viewmodel.SymbolDiffs), Times.Never());
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object, It.IsAny<IList<string>>(), It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()), Times.Exactly(1));
        this.MockUITaskScheduler.Invocations.Clear();

        viewmodel.ExportCompilandsToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(this.MockExcelExporter.Object, viewmodel.SymbolDiffs), Times.Never());
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object, It.IsAny<IList<string>>(), It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()), Times.Exactly(1));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
