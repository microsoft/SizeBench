using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;
using System.Reflection.PortableExecutable;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public class ContributionPageViewModelTests
{
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private LibCOFFGroupContribution? testLibCOFFGroupContribution;
    private LibSectionContribution? testLibSectionContribution;
    private CompilandCOFFGroupContribution? testCompilandCOFFGroupContribution;
    private CompilandSectionContribution? testCompilandSectionContribution;
    private Library? testLib;
    private Compiland? testCompiland;

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestDIAAdapter = new TestDIAAdapter();
        this.MockSession = new Mock<ISession>();
        this.MockExcelExporter = new Mock<IExcelExporter>();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();

        using var cache = new SessionDataCache();
        var textSection = new BinarySection(cache, ".text", size: 0, virtualSize: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        var textMnCG = new COFFGroup(cache, ".text$mn", size: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute)
        {
            Section = textSection
        };
        textMnCG.MarkFullyConstructed();
        textSection.AddCOFFGroup(textMnCG);
        textSection.MarkFullyConstructed();

        this.testLib = new Library("test lib");
        this.testCompiland = this.testLib.GetOrCreateCompiland(cache, "1.obj", compilandSymIndexId: 1, diaAdapter: this.TestDIAAdapter);
        this.testCompilandCOFFGroupContribution = this.testCompiland.GetOrCreateCOFFGroupContribution(textMnCG);
        this.testCompilandSectionContribution = this.testCompiland.GetOrCreateSectionContribution(textSection);
        this.testCompiland.MarkFullyConstructed();
        this.testLibCOFFGroupContribution = this.testLib.GetOrCreateCOFFGroupContribution(textMnCG);
        this.testLibSectionContribution = this.testLib.GetOrCreateSectionContribution(textSection);
        this.testLib.MarkFullyConstructed();

        var sections = new List<BinarySection>() { textSection };
        var libs = new List<Library>() { this.testLib };
        var compilands = new List<Compiland>() { this.testCompiland };
        this.MockSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroups(It.IsAny<CancellationToken>())).Returns(Task.FromResult(sections as IReadOnlyList<BinarySection>));
        this.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(libs as IReadOnlyCollection<Library>));
        this.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(compilands as IReadOnlyCollection<Compiland>));
    }

    [TestMethod]
    [Timeout(5 * 1000)] // in ms
    public Task LibCOFFGroupContributionSymbolsInitializeAsyncFromConstruction()
    {
        var queryString = new Dictionary<string, string>()
            {
                { "COFFGroup", ".text$mn" },
                { "Lib", this.testLib!.Name }
            };
        return SymbolsInitializeAsyncFromConstruction(this.testLibCOFFGroupContribution!, queryString);
    }

    [TestMethod]
    [Timeout(5 * 1000)] // in ms
    public Task LibSectionContributionSymbolsInitializeAsyncFromConstruction()
    {
        var queryString = new Dictionary<string, string>()
            {
                { "BinarySection", ".text" },
                { "Lib", this.testLib!.Name }
            };
        return SymbolsInitializeAsyncFromConstruction(this.testLibSectionContribution!, queryString);
    }

    [TestMethod]
    [Timeout(5 * 1000)] // in ms
    public Task CompilandCOFFGroupContributionSymbolsInitializeAsyncFromConstruction()
    {
        var queryString = new Dictionary<string, string>()
            {
                { "COFFGroup", ".text$mn" },
                { "Compiland", this.testCompiland!.Name },
                { "Lib", this.testLib!.Name }
            };
        return SymbolsInitializeAsyncFromConstruction(this.testCompilandCOFFGroupContribution!, queryString);
    }

    [TestMethod]
    [Timeout(5 * 1000)] // in ms
    public Task CompilandSectionContributionSymbolsInitializeAsyncFromConstruction()
    {
        var queryString = new Dictionary<string, string>()
            {
                { "BinarySection", ".text" },
                { "Compiland", this.testCompiland!.Name },
                { "Lib", this.testLib!.Name }
            };
        return SymbolsInitializeAsyncFromConstruction(this.testCompilandSectionContribution!, queryString);
    }

    private async Task SymbolsInitializeAsyncFromConstruction(Contribution contribution, Dictionary<string, string> queryString)
    {
        var tcsTestResultsComplete = new TaskCompletionSource<object>();
        var symbols = new List<ISymbol>
            {
                new Mock<ISymbol>().Object,
                new Mock<ISymbol>().Object
            };
        var tcsSymbolsReady = new TaskCompletionSource<IReadOnlyList<ISymbol>>();
        this.MockSession.Setup(s => s.EnumerateSymbolsInContribution(contribution, It.IsAny<CancellationToken>()))
                        .Returns(tcsSymbolsReady.Task);

        var viewmodel = new ContributionPageViewModel(this.MockUITaskScheduler.Object,
                                                      this.MockExcelExporter.Object,
                                                      this.MockSession.Object);
        viewmodel.SetQueryString(queryString);
        var initTask = viewmodel.InitializeAsync();

        viewmodel.PropertyChanged += (s, e) =>
        {
            Assert.AreEqual("Symbols", e.PropertyName);
            Assert.IsTrue(ReferenceEquals(symbols, viewmodel.Symbols));
            tcsTestResultsComplete.SetResult(new object());
        };

        tcsSymbolsReady.SetResult(symbols);
        await tcsTestResultsComplete.Task;
        await initTask;
    }

    [TestMethod]
    public async Task CancelingSymbolLoadingIsFine()
    {
        var tcsTestResultsComplete = new TaskCompletionSource<object>();
        var tcsSymbolsReady = new TaskCompletionSource<IReadOnlyList<ISymbol>>();
        this.MockSession.Setup(s => s.EnumerateSymbolsInContribution(this.testLibCOFFGroupContribution!, It.IsAny<CancellationToken>()))
                        .Returns(tcsSymbolsReady.Task);

        var viewmodel = new ContributionPageViewModel(this.MockUITaskScheduler.Object,
                                                      this.MockExcelExporter.Object,
                                                      this.MockSession.Object);

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(viewmodel.Symbols))
            {
                Assert.Fail("Symbols property changes should not happen if Symbol loading is canceled");
            }
        };

        tcsSymbolsReady.SetCanceled();
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "COFFGroup", ".text$mn" },
                { "Lib", this.testLib!.Name }
            });
        await viewmodel.InitializeAsync();

        Assert.IsNull(viewmodel.Symbols);
    }
}
