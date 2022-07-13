using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public class AllAnnotationsPageViewModelTests
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.MockExcelExporter = new Mock<IExcelExporter>();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();

        // Synchronously complete any task given to us
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [Timeout(30 * 1000)] // 30s
    [TestMethod]
    public async Task CanExportToExcel()
    {
        using var cache = new SessionDataCache();
        uint nextSymIndexId = 0;
        var firstAnnotation = new AnnotationSymbol(cache, "annotation text here", null, 123, false, symIndexId: nextSymIndexId++);
        var secondAnnotation = new AnnotationSymbol(cache, "another annotation!", null, 456, true, symIndexId: nextSymIndexId++);
        var annotations = new List<AnnotationSymbol>() { firstAnnotation, secondAnnotation };

        this.MockSession.Setup(s => s.EnumerateAnnotations(It.IsAny<CancellationToken>())).Returns(Task.FromResult(annotations as IReadOnlyList<AnnotationSymbol>));
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                         It.IsAny<IList<string>>(),
                                                                                         It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()));

        var viewmodel = new AllAnnotationsPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.MockSession.Object,
                                                           this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ExportToExcelCommand.CanExecute());

        viewmodel.ExportToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                          It.IsAny<IList<string>>(),
                                                                                          It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()),
                                        Times.Exactly(1));
    }
}
