using SizeBench.AnalysisEngine;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class WastefulVirtualDiffPageViewModelTests : IDisposable
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
    public async Task TypeNameInQueryStringWorksForWastefulTypePresentBothBeforeAndAfter()
    {
        var wviDiffList = this.TestDataGenerator.GenerateWastefulVirtualItemDiffs(out var beforeWVIList, out var afterWVIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateWastefulVirtualItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(wviDiffList as IReadOnlyList<WastefulVirtualItemDiff>));

        var testWVIDiff = wviDiffList.First(wviDiff => wviDiff.BeforeWastefulVirtual != null && wviDiff.AfterWastefulVirtual != null);

        var viewmodel = new WastefulVirtualDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "TypeName", testWVIDiff.TypeName }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(testWVIDiff, viewmodel.WastefulVirtualItemDiff));
    }

    [TestMethod]
    public async Task TypeNameInQueryStringWorksForWastefulTypePresentOnlyInBefore()
    {
        var allwviDiffs = this.TestDataGenerator.GenerateWastefulVirtualItemDiffs(out var beforeTFIList, out var afterTFIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateWastefulVirtualItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allwviDiffs as IReadOnlyList<WastefulVirtualItemDiff>));

        var testWVIDiff = allwviDiffs.First(wviDiff => wviDiff.BeforeWastefulVirtual != null && wviDiff.AfterWastefulVirtual is null);

        var viewmodel = new WastefulVirtualDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "TypeName", testWVIDiff.TypeName }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(testWVIDiff, viewmodel.WastefulVirtualItemDiff));
    }

    [TestMethod]
    public async Task TypeNameInQueryStringWorksForWastefulTypePresentOnlyInAfter()
    {
        var allwviDiffs = this.TestDataGenerator.GenerateWastefulVirtualItemDiffs(out var beforeTFIList, out var afterTFIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateWastefulVirtualItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allwviDiffs as IReadOnlyList<WastefulVirtualItemDiff>));

        var testWVIDiff = allwviDiffs.First(wviDiff => wviDiff.BeforeWastefulVirtual is null && wviDiff.AfterWastefulVirtual != null);

        var viewmodel = new WastefulVirtualDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "TypeName", testWVIDiff.TypeName }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(testWVIDiff, viewmodel.WastefulVirtualItemDiff));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
