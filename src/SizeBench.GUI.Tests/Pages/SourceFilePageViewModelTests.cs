using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Pages;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBenchV2.ViewModels.Tests;

[TestClass]
public class SourceFilePageViewModelTests
{
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [Timeout(5 * 1000)]
    [TestMethod]
    public async Task SymbolsInitializeAsyncFromConstruction()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateSourceFiles(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.SourceFiles as IReadOnlyList<SourceFile>));
        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var symbols = new List<ISymbol>
            {
                new Mock<ISymbol>().Object,
                new Mock<ISymbol>().Object
            };

        var tcsSymbolsReady = new TaskCompletionSource<IReadOnlyList<ISymbol>>();
        generator.MockSession.Setup(s => s.EnumerateSymbolsInSourceFile(generator.A1CppSourceFile, It.IsAny<CancellationToken>()))
                             .Returns(tcsSymbolsReady.Task);

        var viewmodel = new SourceFilePageViewModel(this.MockUITaskScheduler.Object,
                                                    new Mock<IExcelExporter>().Object,
                                                    generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "SourceFile", generator.A1CppSourceFile.Name },
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
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateSourceFiles(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.SourceFiles as IReadOnlyList<SourceFile>));
        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var tcsSymbolsReady = new TaskCompletionSource<IReadOnlyList<ISymbol>>();
        generator.MockSession.Setup(s => s.EnumerateSymbolsInSourceFile(generator.A1CppSourceFile, It.IsAny<CancellationToken>()))
                             .Returns(tcsSymbolsReady.Task);

        var viewmodel = new SourceFilePageViewModel(this.MockUITaskScheduler.Object,
                                                    new Mock<IExcelExporter>().Object,
                                                    generator.MockSession.Object);

        viewmodel.PropertyChanged += (s, e) => Assert.Fail("No property changes should happen if Symbol loading is canceled");

        tcsSymbolsReady.SetCanceled();

        Assert.IsNull(viewmodel.Symbols);
    }

    [Timeout(1000 * 5)] // 5s
    [TestMethod]
    public async Task CanExportSymbolsToExcel()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateSourceFiles(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.SourceFiles as IReadOnlyList<SourceFile>));
        var tcsTestResultsComplete = new TaskCompletionSource<object>();

        var symbols = new List<ISymbol>
            {
                new Mock<ISymbol>().Object,
                new Mock<ISymbol>().Object
            };

        generator.MockSession.Setup(s => s.EnumerateSymbolsInSourceFile(generator.A1CppSourceFile, It.IsAny<CancellationToken>()))
                             .Returns(Task.FromResult(symbols as IReadOnlyList<ISymbol>));

        var mockExcelExporter = new Mock<IExcelExporter>();

        var viewmodel = new SourceFilePageViewModel(this.MockUITaskScheduler.Object,
                                                    mockExcelExporter.Object,
                                                    generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "SourceFile", generator.A1CppSourceFile.Name },
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ExportSymbolsToExcelCommand.CanExecute());

        viewmodel.ExportSymbolsToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(mockExcelExporter.Object, symbols), Times.Exactly(1));
    }
}
