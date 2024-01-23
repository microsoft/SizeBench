using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public class COFFGroupPageViewModelTests
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private COFFGroup? textMnCG;

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.MockExcelExporter = new Mock<IExcelExporter>();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();

        using var cache = new SessionDataCache();
        var textSection = new BinarySection(cache, ".text", size: 0, virtualSize: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        this.textMnCG = new COFFGroup(cache, ".text$mn", size: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute)
        {
            Section = textSection
        };
        this.textMnCG.MarkFullyConstructed();
        textSection.AddCOFFGroup(this.textMnCG);
        textSection.MarkFullyConstructed();

        var sections = new List<BinarySection>() { textSection };
        this.MockSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroups(It.IsAny<CancellationToken>())).Returns(Task.FromResult(sections as IReadOnlyList<BinarySection>));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        this.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromCanceled<IReadOnlyList<Library>>(cts.Token));
        this.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromCanceled<IReadOnlyList<Compiland>>(cts.Token));
    }

    [Timeout(5 * 1000)]
    [TestMethod]
    public async Task SymbolsInitializeAsyncFromConstruction()
    {
        var tcsTestResultsComplete = new TaskCompletionSource<object>();
        var symbols = new List<ISymbol>
            {
                new Mock<ISymbol>().Object,
                new Mock<ISymbol>().Object
            };
        var tcsSymbolsReady = new TaskCompletionSource<IReadOnlyList<ISymbol>>();
        this.MockSession.Setup(s => s.EnumerateSymbolsInCOFFGroup(this.textMnCG!, It.IsAny<CancellationToken>()))
                        .Returns(tcsSymbolsReady.Task);

        var viewmodel = new COFFGroupPageViewModel(this.MockUITaskScheduler.Object,
                                                   this.MockSession.Object,
                                                   this.MockExcelExporter.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "COFFGroup", ".text$mn" }
            });

        var initTask = viewmodel.InitializeAsync();

        var sawSymbolsPropertyChange = false;

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(COFFGroupPageViewModel.Symbols))
            {
                sawSymbolsPropertyChange = true;
                tcsTestResultsComplete.SetResult(new object());
            }
        };

        tcsSymbolsReady.SetResult(symbols);
        await tcsTestResultsComplete.Task;
        await initTask;

        Assert.IsTrue(sawSymbolsPropertyChange);
        Assert.IsTrue(ReferenceEquals(symbols, viewmodel.Symbols));
    }

    [TestMethod]
    public void CancelingSymbolLoadingIsFine()
    {
        var tcsTestResultsComplete = new TaskCompletionSource<object>();
        var tcsSymbolsReady = new TaskCompletionSource<IReadOnlyList<ISymbol>>();
        this.MockSession.Setup(s => s.EnumerateSymbolsInCOFFGroup(this.textMnCG!, It.IsAny<CancellationToken>()))
                   .Returns(tcsSymbolsReady.Task);

        var viewmodel = new COFFGroupPageViewModel(this.MockUITaskScheduler.Object,
                                                   this.MockSession.Object,
                                                   this.MockExcelExporter.Object);

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(COFFGroupPageViewModel.Symbols))
            {
                Assert.Fail("Symbols property should not change if Symbol loading is canceled");
            }
        };

        tcsSymbolsReady.SetCanceled();

        Assert.IsNull(viewmodel.Symbols);
    }
}
