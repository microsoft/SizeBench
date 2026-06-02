using SizeBench.AnalysisEngine.Symbols;
using SizeBench.AnalysisEngine;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;
using SizeBench.GUI.Pages.Symbols;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class InlineSiteGroupPageViewModelTests
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();

        // Synchronously complete any task given to us
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task LooksUpInlineSitesInGroupByName()
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

        var viewmodel = new InlineSiteGroupPageViewModel(this.MockUITaskScheduler.Object,
                                                         this.MockSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
        {
            { "Name", "someInlinedFunction" },
        });
        await viewmodel.InitializeAsync();

        Assert.IsNotNull(viewmodel.InlineSiteGroup);
        Assert.AreEqual("someInlinedFunction", viewmodel.InlineSiteGroup.InlinedFunctionName);
        Assert.AreEqual(2, viewmodel.InlineSiteGroup.InlineSites.Count);
        Assert.AreEqual("Inlined Function: someInlinedFunction", viewmodel.PageTitle);
    }
}
