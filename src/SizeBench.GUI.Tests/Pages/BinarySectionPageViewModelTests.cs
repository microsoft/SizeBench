using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public class BinarySectionPageViewModelTests
{
    public Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [Timeout(5 * 1000)] // 5s
    [TestMethod]
    public async Task LibsInitializeWhenTabSelected()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.LoadBinarySectionByName(generator.TextSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySection?>(generator.TextSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var viewmodel = new BinarySectionPageViewModel(this.MockUITaskScheduler.Object,
                                                       new Mock<IExcelExporter>().Object,
                                                       generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection", generator.TextSection.Name }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            Assert.AreEqual("Libs", e.PropertyName);
            Assert.IsTrue(ReferenceEquals(generator.Libs, viewmodel.Libs));
            tcsTestResultsComplete.SetResult(new object());
        };

        generator.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Libs as IReadOnlyCollection<Library>));

        Assert.IsNull(viewmodel.Libs);
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.LibsTab;

        await tcsTestResultsComplete.Task;
        Assert.AreEqual(generator.Libs, viewmodel.Libs);

        // We should have started 2 long-running tasks, one for the binary section load and one for the libs
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Timeout(5 * 1000)] // 5s
    [TestMethod]
    public async Task LibsLoadOnlyOnceEvenIfYouSwitchTabsABunch()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.LoadBinarySectionByName(generator.TextSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySection?>(generator.TextSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var viewmodel = new BinarySectionPageViewModel(this.MockUITaskScheduler.Object,
                                                       new Mock<IExcelExporter>().Object,
                                                       generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection", generator.TextSection.Name }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            Assert.AreEqual("Libs", e.PropertyName);
            Assert.IsTrue(ReferenceEquals(generator.Libs, viewmodel.Libs));
            tcsTestResultsComplete.SetResult(new object());
        };

        generator.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Libs as IReadOnlyCollection<Library>));

        Assert.IsNull(viewmodel.Libs);
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.LibsTab;

        await tcsTestResultsComplete.Task;
        Assert.AreEqual(generator.Libs, viewmodel.Libs);

        // Now let's switch back to the COFF Groups tab, then back to Libs, a couple times - we should still only have loaded the libs once
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.LibsTab;
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.LibsTab;

        // We should have started 2 long-running tasks, one for the binary section load and one for the libs
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Timeout(5 * 1000)] // 5s
    [TestMethod]
    public async Task CompilandnsInitializeWhenTabSelected()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.LoadBinarySectionByName(generator.TextSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySection?>(generator.TextSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var viewmodel = new BinarySectionPageViewModel(this.MockUITaskScheduler.Object,
                                                       new Mock<IExcelExporter>().Object,
                                                       generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection",generator.TextSection.Name }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BinarySectionPageViewModel.Compilands))
            {
                tcsTestResultsComplete.SetResult(new object());
            }
        };

        generator.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Libs as IReadOnlyCollection<Library>));
        generator.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Compilands as IReadOnlyCollection<Compiland>));

        Assert.IsNull(viewmodel.Compilands);
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.CompilandsTab;

        await tcsTestResultsComplete.Task;
        Assert.AreEqual(4, viewmodel.Compilands!.Count);
        Assert.AreEqual(1, viewmodel.Compilands.Where(viewmodel.CompilandFilter).Count());
        Assert.IsTrue(viewmodel.Compilands.Where(viewmodel.CompilandFilter).Contains(generator.A1Compiland));

        // We should have started 2 long-running tasks, one for the binary section load and one for the libs/compilands
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Timeout(5 * 1000)] // 5s
    [TestMethod]
    public async Task CompilandsLoadOnlyOnceEvenIfYouSwitchTabsABunch()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.LoadBinarySectionByName(generator.TextSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySection?>(generator.TextSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var viewmodel = new BinarySectionPageViewModel(this.MockUITaskScheduler.Object,
                                                       new Mock<IExcelExporter>().Object,
                                                       generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection",generator.TextSection.Name }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BinarySectionPageViewModel.Compilands))
            {
                tcsTestResultsComplete.SetResult(new object());
            }
        };

        generator.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Libs as IReadOnlyCollection<Library>));
        generator.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Compilands as IReadOnlyCollection<Compiland>));

        Assert.IsNull(viewmodel.Compilands);
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.CompilandsTab;

        await tcsTestResultsComplete.Task;
        Assert.AreEqual(4, viewmodel.Compilands!.Count);
        Assert.AreEqual(1, viewmodel.Compilands.Where(viewmodel.CompilandFilter).Count());
        Assert.IsTrue(viewmodel.Compilands.Where(viewmodel.CompilandFilter).Contains(generator.A1Compiland));

        // Now let's switch back to the COFF Groups tab, then back to Compilands, a couple times - we should still only have loaded the compilands once
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.CompilandsTab;
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.CompilandsTab;

        // We should have started 2 long-running tasks, one for the binary section load and one for the libs/compilands
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Timeout(5 * 1000)] // 5s
    [TestMethod]
    public async Task SymbolsInitializeWhenTabSelected()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.LoadBinarySectionByName(generator.TextSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySection?>(generator.TextSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var typeSymbol = new BasicTypeSymbol(generator.DataCache, "int", size: 4, symIndexId: generator._nextSymIndexId++);

        var symbols = new List<StaticDataSymbol>()
            {
                new StaticDataSymbol(generator.DataCache, "test 2", rva: 6, size: 4, isVirtualSize: false, symIndexId: generator._nextSymIndexId++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: generator.A1Compiland, functionParent: null),
                new StaticDataSymbol(generator.DataCache, "test 1", rva: 10, size: 1, isVirtualSize: false, symIndexId: generator._nextSymIndexId++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: generator.A1Compiland, functionParent: null),
            };

        var viewmodel = new BinarySectionPageViewModel(this.MockUITaskScheduler.Object,
                                                       new Mock<IExcelExporter>().Object,
                                                       generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection",generator.TextSection.Name }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BinarySectionPageViewModel.Symbols))
            {
                tcsTestResultsComplete.SetResult(new object());
            }
        };

        generator.MockSession.Setup(s => s.EnumerateSymbolsInBinarySection(generator.TextSection, It.IsAny<CancellationToken>())).Returns(Task.FromResult(symbols as IReadOnlyList<ISymbol>));

        Assert.IsNull(viewmodel.Symbols);
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.SymbolsTab;

        await tcsTestResultsComplete.Task;
        Assert.AreEqual(2, viewmodel.Symbols!.Count);
        Assert.IsTrue(viewmodel.Symbols.Contains(symbols[0]));
        Assert.IsTrue(viewmodel.Symbols.Contains(symbols[1]));

        // We should have started 2 long-running tasks, one for the binary section load and one for the symbols
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Timeout(5 * 1000)] // 5s
    [TestMethod]
    public async Task SymbolsLoadOnlyOnceEvenIfYouSwitchTabsABunch()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.LoadBinarySectionByName(generator.TextSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySection?>(generator.TextSection));

        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var typeSymbol = new BasicTypeSymbol(generator.DataCache, "int", size: 4, symIndexId: generator._nextSymIndexId++);

        var symbols = new List<StaticDataSymbol>()
            {
                new StaticDataSymbol(generator.DataCache, "test 2", rva: 6, size: 4, isVirtualSize: false, symIndexId: generator._nextSymIndexId++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: generator.A1Compiland, functionParent: null),
                new StaticDataSymbol(generator.DataCache, "test 1", rva: 10, size: 1, isVirtualSize: false, symIndexId: generator._nextSymIndexId++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: generator.A1Compiland, functionParent: null),
            };

        var viewmodel = new BinarySectionPageViewModel(this.MockUITaskScheduler.Object,
                                                       new Mock<IExcelExporter>().Object,
                                                       generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection",generator.TextSection.Name }
            });
        await viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BinarySectionPageViewModel.Symbols))
            {
                tcsTestResultsComplete.SetResult(new object());
            }
        };

        generator.MockSession.Setup(s => s.EnumerateSymbolsInBinarySection(generator.TextSection, It.IsAny<CancellationToken>())).Returns(Task.FromResult(symbols as IReadOnlyList<ISymbol>));

        Assert.IsNull(viewmodel.Symbols);
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.SymbolsTab;

        await tcsTestResultsComplete.Task;
        Assert.AreEqual(2, viewmodel.Symbols!.Count);
        Assert.IsTrue(viewmodel.Symbols.Contains(symbols[0]));
        Assert.IsTrue(viewmodel.Symbols.Contains(symbols[1]));

        // Now let's switch back to the COFF Groups tab, then back to Symbols, a couple times - we should still only have loaded the symbols once
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.SymbolsTab;
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.COFFGroupsTab;
        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.SymbolsTab;

        // We should have started 2 long-running tasks, one for the binary section load and one for the symbols
        this.MockUITaskScheduler.Verify(uits => uits.StartLongRunningUITask(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    //[Timeout(5 * 1000)] // 5s
    [TestMethod]
    public async Task ExcelExportWorksForEveryTab()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.LoadBinarySectionByName(generator.TextSection.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult<BinarySection?>(generator.TextSection));
        generator.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Libs as IReadOnlyCollection<Library>));
        generator.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Compilands as IReadOnlyCollection<Compiland>));

        var typeSymbol = new BasicTypeSymbol(generator.DataCache, "int", size: 4, symIndexId: generator._nextSymIndexId++);

        var symbols = new List<StaticDataSymbol>()
            {
                new StaticDataSymbol(generator.DataCache, "test 2", rva: 6, size: 4, isVirtualSize: false, symIndexId: generator._nextSymIndexId++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: generator.A1Compiland, functionParent: null),
                new StaticDataSymbol(generator.DataCache, "test 1", rva: 10, size: 1, isVirtualSize: false, symIndexId: generator._nextSymIndexId++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: generator.A1Compiland, functionParent: null),
            };

        generator.MockSession.Setup(s => s.EnumerateSymbolsInBinarySection(generator.TextSection, It.IsAny<CancellationToken>())).Returns(Task.FromResult(symbols as IReadOnlyList<ISymbol>));

        var mockExcelExporter = new Mock<IExcelExporter>();

        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(mockExcelExporter.Object, generator.TextSection.COFFGroups));
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(mockExcelExporter.Object, generator.Libs));
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(mockExcelExporter.Object, generator.Compilands));
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(mockExcelExporter.Object, symbols));

        var viewmodel = new BinarySectionPageViewModel(this.MockUITaskScheduler.Object,
                                                       mockExcelExporter.Object,
                                                       generator.MockSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection",generator.TextSection.Name }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ExportCOFFGroupsToExcelCommand.CanExecute());

        viewmodel.ExportCOFFGroupsToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(mockExcelExporter.Object, generator.TextSection.COFFGroups), Times.Exactly(1));

        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.LibsTab;
        Assert.IsTrue(viewmodel.ExportLibsToExcelCommand.CanExecute());
        viewmodel.ExportLibsToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(mockExcelExporter.Object, It.IsAny<IList<string>>(), It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()), Times.Exactly(1));

        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.CompilandsTab;
        Assert.IsTrue(viewmodel.ExportCompilandsToExcelCommand.CanExecute());
        viewmodel.ExportCompilandsToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(mockExcelExporter.Object, It.IsAny<IList<string>>(), It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()), Times.Exactly(2));

        viewmodel.SelectedTab = (int)BinarySectionPageViewModel.BinarySectionPageTabIndex.SymbolsTab;
        Assert.IsTrue(viewmodel.ExportSymbolsToExcelCommand.CanExecute());
        viewmodel.ExportSymbolsToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(mockExcelExporter.Object, symbols as IReadOnlyList<ISymbol>), Times.Exactly(1));
    }
}
