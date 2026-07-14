using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class BinarySectionDiffPageViewModelTests : IDisposable
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

    [Timeout(5 * 1000, CooperativeCancellation = true)] // 5s
    [TestMethod]
    public async Task LibsInitializeWhenTabSelected()
    {
        var textSection = this.TestDataGenerator.TextSectionDiff;

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.LoadBinarySectionDiffByName(textSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySectionDiff?>(textSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var viewmodel = new BinarySectionDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.MockExcelExporter.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection", ".text" }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "LibDiffs")
            {
                tcsTestResultsComplete.TrySetResult(new object());
            }
        };

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.LibDiffs as IReadOnlyList<LibDiff>));

        Assert.IsNull(viewmodel.LibDiffs);
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.LibsTab;

        await tcsTestResultsComplete.Task;

        Assert.AreSequenceEqual(this.TestDataGenerator.LibDiffs.Where(l => l.SectionContributionDiffs.ContainsKey(textSection)).ToList(), viewmodel.LibDiffs, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);

        // We should have started 2 long-running tasks, one for the binary section load and one for the libs
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Timeout(5 * 1000, CooperativeCancellation = true)] // 5s
    [TestMethod]
    public async Task CompilandsInitializeWhenTabSelected()
    {
        var textSection = this.TestDataGenerator.TextSectionDiff;

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.LoadBinarySectionDiffByName(textSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySectionDiff?>(textSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var viewmodel = new BinarySectionDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.MockExcelExporter.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection", ".text" }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "CompilandDiffs")
            {
                tcsTestResultsComplete.TrySetResult(new object());
            }
        };

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.LibDiffs as IReadOnlyList<LibDiff>));
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateCompilandDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.CompilandDiffs as IReadOnlyList<CompilandDiff>));

        Assert.IsNull(viewmodel.CompilandDiffs);
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.CompilandsTab;

        await tcsTestResultsComplete.Task;

        Assert.AreSequenceEqual(this.TestDataGenerator.CompilandDiffs.Where(cd => cd.SectionContributionDiffs.ContainsKey(viewmodel.BinarySectionDiff!)).ToList(), viewmodel.CompilandDiffs, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);

        // We should have started 2 long-running tasks, one for the binary section load and one for the libs
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Timeout(5 * 1000, CooperativeCancellation = true)] // 5s
    [TestMethod]
    public async Task LibsLoadOnlyOnceEvenIfYouSwitchTabsABunch()
    {
        var textSection = this.TestDataGenerator.TextSectionDiff;

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.LoadBinarySectionDiffByName(textSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySectionDiff?>(textSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();
        var viewmodel = new BinarySectionDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.MockExcelExporter.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection", ".text" }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "LibDiffs")
            {
                tcsTestResultsComplete.TrySetResult(new object());
            }
        };

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.LibDiffs as IReadOnlyList<LibDiff>));

        Assert.IsNull(viewmodel.LibDiffs);
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.LibsTab;

        await tcsTestResultsComplete.Task;

        Assert.AreSequenceEqual(this.TestDataGenerator.LibDiffs.Where(l => l.SectionContributionDiffs.ContainsKey(textSection)).ToList(), viewmodel.LibDiffs, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);

        // Now let's switch back to the COFF Groups tab, then back to Libs, a couple times - we should still only have loaded the libs once
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.LibsTab;
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.LibsTab;

        // We should have started 2 long-running tasks, one for the binary section load and one for the libs
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Timeout(5 * 1000, CooperativeCancellation = true)] // 5s
    [TestMethod]
    public async Task CompilandsLoadOnlyOnceEvenIfYouSwitchTabsABunch()
    {
        var textSection = this.TestDataGenerator.TextSectionDiff;

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.LoadBinarySectionDiffByName(textSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySectionDiff?>(textSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();
        var viewmodel = new BinarySectionDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.MockExcelExporter.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection", ".text" }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "CompilandDiffs")
            {
                tcsTestResultsComplete.TrySetResult(new object());
            }
        };

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.LibDiffs as IReadOnlyList<LibDiff>));
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateCompilandDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.CompilandDiffs as IReadOnlyList<CompilandDiff>));

        Assert.IsNull(viewmodel.CompilandDiffs);
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.CompilandsTab;

        await tcsTestResultsComplete.Task;

        // Not all the compilands have a size diff in this section, so we won't see everything
        Assert.HasCount(4, viewmodel.CompilandDiffs!);
        Assert.ContainsSingle(cd => cd.Name.Contains("a1.obj", StringComparison.Ordinal), viewmodel.CompilandDiffs!);
        Assert.ContainsSingle(cd => cd.Name.Contains("a2.obj", StringComparison.Ordinal), viewmodel.CompilandDiffs!); // We find this one even though it's .text SizeDiff==0, because it could have COFF Groups, Libs, Symbols, etc. that differ and a user may want to see them...
        Assert.ContainsSingle(cd => cd.Name.Contains("b1.obj", StringComparison.Ordinal), viewmodel.CompilandDiffs!);
        Assert.ContainsSingle(cd => cd.Name.Contains("b2.obj", StringComparison.Ordinal), viewmodel.CompilandDiffs!); // We find this one even though it's .text SizeDiff==0, because it could have COFF Groups, Libs, Symbols, etc. that differ and a user may want to see them...

        // Now let's switch back to the COFF Groups tab, then back to Compilands, a couple times - we should still only have loaded the compilands once
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.CompilandsTab;
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.CompilandsTab;

        // We should have started 2 long-running tasks, one for the binary section load and one for the libs/compilands
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Timeout(5 * 1000, CooperativeCancellation = true)] // 5s
    [TestMethod]
    public async Task SymbolsInitializeWhenTabSelected()
    {
        var textSection = this.TestDataGenerator.TextSectionDiff;

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.LoadBinarySectionDiffByName(textSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySectionDiff?>(textSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var viewmodel = new BinarySectionDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.MockExcelExporter.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection", ".text" }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "Symbols")
            {
                tcsTestResultsComplete.TrySetResult(new object());
            }
        };

        var allSymbolDiffsInBinarySection = this.TestDataGenerator.GenerateSymbolDiffsInBinarySectionList(textSection);

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateSymbolDiffsInBinarySectionDiff(textSection, It.IsAny<CancellationToken>())).Returns(Task.FromResult(allSymbolDiffsInBinarySection as IReadOnlyList<SymbolDiff>));

        Assert.IsNull(viewmodel.Symbols);
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.SymbolsTab;

        await tcsTestResultsComplete.Task;

        Assert.AreSequenceEqual(allSymbolDiffsInBinarySection, viewmodel.Symbols, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
        Assert.AreEqual(allSymbolDiffsInBinarySection.Count, viewmodel.FilteredSymbolDiffs!.Cast<SymbolDiff>().Count());

        // We should have started 2 long-running tasks, one for the binary section load and one for the symbols
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Timeout(5 * 1000, CooperativeCancellation = true)] // 5s
    [TestMethod]
    public async Task SymbolsLoadOnlyOnceEvenIfYouSwitchTabsABunch()
    {
        var textSection = this.TestDataGenerator.TextSectionDiff;

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.LoadBinarySectionDiffByName(textSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySectionDiff?>(textSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();
        var viewmodel = new BinarySectionDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.MockExcelExporter.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection", ".text" }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "Symbols")
            {
                tcsTestResultsComplete.TrySetResult(new object());
            }
        };

        var allSymbolDiffsInBinarySection = this.TestDataGenerator.GenerateSymbolDiffsInBinarySectionList(textSection);

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateSymbolDiffsInBinarySectionDiff(textSection, It.IsAny<CancellationToken>())).Returns(Task.FromResult(allSymbolDiffsInBinarySection as IReadOnlyList<SymbolDiff>));

        Assert.IsNull(viewmodel.Symbols);
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.SymbolsTab;

        await tcsTestResultsComplete.Task;

        Assert.AreSequenceEqual(allSymbolDiffsInBinarySection, viewmodel.Symbols, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);

        // Now let's switch back to the COFF Groups tab, then back to Symbols, a couple times - we should still only have loaded the symbols once
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.SymbolsTab;
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionDiffPageViewModel.BinarySectionDiffPageTabIndex.SymbolsTab;

        // We should have started 2 long-running tasks, one for the binary section load and one for the symbols
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Timeout(5 * 1000, CooperativeCancellation = true)] // 5s
    [TestMethod]
    public async Task ExcelExportWorksForEveryTab()
    {
        var textSection = this.TestDataGenerator.TextSectionDiff;

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.LoadBinarySectionDiffByName(textSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySectionDiff?>(textSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();
        var viewmodel = new BinarySectionDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.MockExcelExporter.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection", ".text" }
            });
        await viewmodel.InitializeAsync();

        var allSymbolDiffsInBinarySection = this.TestDataGenerator.GenerateSymbolDiffsInBinarySectionList(textSection);

        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(this.MockExcelExporter.Object, textSection.COFFGroupDiffs));
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(this.MockExcelExporter.Object, allSymbolDiffsInBinarySection));

        Assert.IsTrue(viewmodel.ExportCOFFGroupsToExcelCommand.CanExecute());

        viewmodel.ExportCOFFGroupsToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(this.MockExcelExporter.Object, textSection.COFFGroupDiffs), Times.Exactly(1));

        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.LibDiffs as IReadOnlyList<LibDiff>));
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateCompilandDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.TestDataGenerator.CompilandDiffs as IReadOnlyList<CompilandDiff>));
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateSymbolDiffsInBinarySectionDiff(textSection, It.IsAny<CancellationToken>())).Returns(Task.FromResult(allSymbolDiffsInBinarySection as IReadOnlyList<SymbolDiff>));

        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.LibsTab;
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(this.MockExcelExporter.Object, viewmodel.LibDiffs));
        Assert.IsTrue(viewmodel.ExportLibsToExcelCommand.CanExecute());
        viewmodel.ExportLibsToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object, It.IsAny<IList<string>>(), It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()), Times.Exactly(1));

        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.CompilandsTab;
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(this.MockExcelExporter.Object, viewmodel.CompilandDiffs));
        Assert.IsTrue(viewmodel.ExportCompilandsToExcelCommand.CanExecute());
        viewmodel.ExportCompilandsToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object, It.IsAny<IList<string>>(), It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()), Times.Exactly(2));

        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.SymbolsTab;
        Assert.IsTrue(viewmodel.ExportSymbolsToExcelCommand.CanExecute());
        viewmodel.ExportSymbolsToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(this.MockExcelExporter.Object,
                                                                      It.Is<IReadOnlyList<SymbolDiff>>(symbols => symbols.Count == allSymbolDiffsInBinarySection.Count &&
                                                                                                                  !symbols.Except(allSymbolDiffsInBinarySection).Any())),
                                        Times.Exactly(1));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
