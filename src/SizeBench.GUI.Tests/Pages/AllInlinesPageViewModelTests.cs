using SizeBench.AnalysisEngine.Symbols;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class AllInlinesPageViewModelTests
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
        using var cache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        uint nextSymIndexId = 0;
        var blockInlinedInto = new SimpleFunctionCodeSymbol(cache, "functionInlinedInto", 50u, 500u, nextSymIndexId++);
        var rvaRangesOccupied1 = RVARangeSet.FromListOfRVARanges([RVARange.FromRVAAndSize(100u, 10u), RVARange.FromRVAAndSize(120u, 5u)], maxPaddingToMerge: 1);
        var inlineSite1 = new InlineSiteSymbol(cache, "someInlinedFunction", nextSymIndexId++, blockInlinedInto,
                                               canonicalSymbolInlinedInto: blockInlinedInto,
                                               rvaRangesOccupied1);

        var rvaRangesOccupied2 = RVARangeSet.FromListOfRVARanges([RVARange.FromRVAAndSize(130u, 23u)], maxPaddingToMerge: 1);
        var inlineSite2 = new InlineSiteSymbol(cache, "anotherInlinedFunction", nextSymIndexId++, blockInlinedInto,
                                               canonicalSymbolInlinedInto: blockInlinedInto,
                                               rvaRangesOccupied2);

        var inlineSites = new List<InlineSiteSymbol>() { inlineSite1, inlineSite2 };

        this.MockSession.Setup(s => s.EnumerateAllInlineSites(It.IsAny<CancellationToken>())).Returns(Task.FromResult(inlineSites as IReadOnlyList<InlineSiteSymbol>));
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                         It.IsAny<IList<string>>(),
                                                                                         It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()));

        var viewmodel = new AllInlinesPageViewModel(this.MockUITaskScheduler.Object,
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

    [TestMethod]
    public async Task GroupsInlineSitesByName()
    {
        using var cache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        uint nextSymIndexId = 0;
        var blockInlinedInto1 = new SimpleFunctionCodeSymbol(cache, "functionInlinedInto", 50u, 500u, nextSymIndexId++);
        var rvaRangesOccupied1 = RVARangeSet.FromListOfRVARanges([RVARange.FromRVAAndSize(100u, 10u), RVARange.FromRVAAndSize(120u, 5u)], maxPaddingToMerge: 1);
        var inlineSite1 = new InlineSiteSymbol(cache, "someInlinedFunction", nextSymIndexId++, blockInlinedInto1,
                                               canonicalSymbolInlinedInto: blockInlinedInto1,
                                               rvaRangesOccupied1);

        var rvaRangesOccupied2 = RVARangeSet.FromListOfRVARanges([RVARange.FromRVAAndSize(130u, 23u)], maxPaddingToMerge: 1);
        var inlineSite2 = new InlineSiteSymbol(cache, "anotherInlinedFunction", nextSymIndexId++, blockInlinedInto1,
                                               canonicalSymbolInlinedInto: blockInlinedInto1,
                                               rvaRangesOccupied2);

        // A second copy of the same inlined function, but it's inlined into a second site, so we should see the group reflect that
        var blockInlinedInto2 = new SimpleFunctionCodeSymbol(cache, "anotherFunctionContainingInlineSites", 1000u, 500u, nextSymIndexId++);
        var rvaRangesOccupied3 = RVARangeSet.FromListOfRVARanges([RVARange.FromRVAAndSize(1010u, 12u)], maxPaddingToMerge: 1);
        var inlineSite3 = new InlineSiteSymbol(cache, "someInlinedFunction", nextSymIndexId++, blockInlinedInto2,
                                               canonicalSymbolInlinedInto: blockInlinedInto2,
                                               rvaRangesOccupied3);

        var inlineSites = new List<InlineSiteSymbol>() { inlineSite1, inlineSite2, inlineSite3 };

        this.MockSession.Setup(s => s.EnumerateAllInlineSites(It.IsAny<CancellationToken>())).Returns(Task.FromResult(inlineSites as IReadOnlyList<InlineSiteSymbol>));

        var viewmodel = new AllInlinesPageViewModel(this.MockUITaskScheduler.Object,
                                                    this.MockSession.Object,
                                                    this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        Assert.AreEqual(2, viewmodel.InlineSiteGroups.Count);

        var group1 = viewmodel.InlineSiteGroups.Single(x => x.InlinedFunctionName == "someInlinedFunction");
        Assert.AreEqual(2, group1.InlineSites.Count);
        Assert.AreEqual(10u + 5u + 12u, group1.TotalSize);

        var group2 = viewmodel.InlineSiteGroups.Single(x => x.InlinedFunctionName == "anotherInlinedFunction");
        Assert.AreEqual(1, group2.InlineSites.Count);
        Assert.AreEqual(23u, group2.TotalSize);
    }
}
