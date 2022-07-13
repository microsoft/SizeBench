using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public class LibPageViewModelTests
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
        this.MockExcelExporter = new Mock<IExcelExporter>();
    }

    private LibPageViewModel CreateViewModelForTest()
    {
        return new LibPageViewModel(this.MockUITaskScheduler.Object,
                                    this.MockExcelExporter.Object,
                                    this.MockSession.Object);
    }

    [Timeout(5 * 1000)]
    [TestMethod]
    public async Task SymbolsInitializeAsyncFromConstruction()
    {
        var lib = new Library("1.lib");
        lib.MarkFullyConstructed();
        var libs = new List<Library>() { lib };
        this.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(libs as IReadOnlyList<Library>));
        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var symbols = new List<ISymbol>
            {
                new Mock<ISymbol>().Object,
                new Mock<ISymbol>().Object
            };

        var tcsSymbolsReady = new TaskCompletionSource<IReadOnlyList<ISymbol>>();
        this.MockSession.Setup(s => s.EnumerateSymbolsInLib(lib, It.IsAny<CancellationToken>()))
                        .Returns(tcsSymbolsReady.Task);

        var viewmodel = CreateViewModelForTest();
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "Lib", lib.Name }
            });
        var initTask = viewmodel.InitializeAsync();

        var propertyNameResult = String.Empty;

        viewmodel.PropertyChanged += (s, e) =>
        {
            propertyNameResult = e.PropertyName;
            tcsTestResultsComplete.SetResult(new object());
        };

        tcsSymbolsReady.SetResult(symbols);
        await tcsTestResultsComplete.Task;
        await initTask;

        Assert.AreEqual("Symbols", propertyNameResult);
        CollectionAssert.AreEqual(symbols, viewmodel.Symbols!.ToList());
    }

    [TestMethod]
    public void CancelingSymbolLoadingIsFine()
    {
        var lib = new Library("1.lib");
        lib.MarkFullyConstructed();
        var libs = new List<Library>() { lib };
        this.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(libs as IReadOnlyList<Library>));
        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var tcsSymbolsReady = new TaskCompletionSource<IReadOnlyList<ISymbol>>();
        this.MockSession.Setup(s => s.EnumerateSymbolsInLib(lib, It.IsAny<CancellationToken>()))
                        .Returns(tcsSymbolsReady.Task);

        var viewmodel = CreateViewModelForTest();

        viewmodel.PropertyChanged += (s, e) => Assert.Fail("No property changes should happen if Symbol loading is canceled");

        tcsSymbolsReady.SetCanceled();

        Assert.IsNull(viewmodel.Symbols);
    }

    [Timeout(1000 * 5)] // 5s
    [TestMethod]
    public async Task CanExportSymbolsToExcel()
    {
        var lib = new Library("1.lib");
        lib.MarkFullyConstructed();
        var libs = new List<Library>() { lib };
        this.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(libs as IReadOnlyList<Library>));
        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var symbols = new List<ISymbol>
            {
                new Mock<ISymbol>().Object,
                new Mock<ISymbol>().Object
            };

        this.MockSession.Setup(s => s.EnumerateSymbolsInLib(lib, It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult(symbols as IReadOnlyList<ISymbol>));

        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(this.MockExcelExporter.Object, symbols));

        var viewmodel = CreateViewModelForTest();
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "Lib", lib.Name }
            });

        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ExportSymbolsToExcelCommand.CanExecute());

        viewmodel.ExportSymbolsToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(this.MockExcelExporter.Object, symbols), Times.Exactly(1));
    }
}
