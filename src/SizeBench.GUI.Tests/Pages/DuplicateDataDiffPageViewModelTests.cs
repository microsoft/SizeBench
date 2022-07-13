using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class DuplicateDataDiffPageViewModelTests : IDisposable
{
    private DiffTestDataGenerator TestDataGenerator = new DiffTestDataGenerator();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestDataGenerator = new DiffTestDataGenerator();

        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task BeforeDuplicateRVAInQueryStringWorksForSymbolPresentBothBeforeAndAfter()
    {
        var allDupes = this.TestDataGenerator.GenerateDuplicateDataItemDiffs(out var beforeDDIList, out var afterDDIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateDuplicateDataItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allDupes as IReadOnlyList<DuplicateDataItemDiff>));

        var testDupe = allDupes.First(ddiDiff => ddiDiff.SymbolDiff.BeforeSymbol != null && ddiDiff.SymbolDiff.AfterSymbol != null);

        var viewmodel = new DuplicateDataDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BeforeDuplicateRVA", testDupe.SymbolDiff.BeforeSymbol!.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(testDupe, viewmodel.DuplicateDataItemDiff));
    }

    [TestMethod]
    public async Task BeforeDuplicateRVAInQueryStringWorksForSymbolPresentOnlyInBefore()
    {
        var allDupes = this.TestDataGenerator.GenerateDuplicateDataItemDiffs(out var beforeDDIList, out var afterDDIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateDuplicateDataItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allDupes as IReadOnlyList<DuplicateDataItemDiff>));

        var testDupe = allDupes.First(ddiDiff => ddiDiff.SymbolDiff.BeforeSymbol != null && ddiDiff.SymbolDiff.AfterSymbol is null);

        var viewmodel = new DuplicateDataDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BeforeDuplicateRVA", testDupe.SymbolDiff.BeforeSymbol!.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(testDupe, viewmodel.DuplicateDataItemDiff));
    }

    [TestMethod]
    public async Task AfterDuplicateRVAInQueryStringWorksForSymbolPresentBothBeforeAndAfter()
    {
        var allDupes = this.TestDataGenerator.GenerateDuplicateDataItemDiffs(out var beforeDDIList, out var afterDDIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateDuplicateDataItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allDupes as IReadOnlyList<DuplicateDataItemDiff>));

        var testDupe = allDupes.First(ddiDiff => ddiDiff.SymbolDiff.BeforeSymbol != null && ddiDiff.SymbolDiff.AfterSymbol != null);

        var viewmodel = new DuplicateDataDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "AfterDuplicateRVA", testDupe.SymbolDiff.AfterSymbol!.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(testDupe, viewmodel.DuplicateDataItemDiff));
    }

    [TestMethod]
    public async Task AfterDuplicateRVAInQueryStringWorksForSymbolPresentOnlyInAfter()
    {
        var allDupes = this.TestDataGenerator.GenerateDuplicateDataItemDiffs(out var beforeDDIList, out var afterDDIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateDuplicateDataItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allDupes as IReadOnlyList<DuplicateDataItemDiff>));

        var testDupe = allDupes.First(ddiDiff => ddiDiff.SymbolDiff.BeforeSymbol is null && ddiDiff.SymbolDiff.AfterSymbol != null);

        var viewmodel = new DuplicateDataDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "AfterDuplicateRVA", testDupe.SymbolDiff.AfterSymbol!.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(testDupe, viewmodel.DuplicateDataItemDiff));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
