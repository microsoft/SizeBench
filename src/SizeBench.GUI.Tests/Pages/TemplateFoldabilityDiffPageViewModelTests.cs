using SizeBench.AnalysisEngine;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class TemplateFoldabilityDiffPageViewModelTests : IDisposable
{
    private DiffTestDataGenerator TestDataGenerator = new DiffTestDataGenerator();
    private TestDIAAdapter BeforeDIAAdapter = new TestDIAAdapter();
    private TestDIAAdapter AfterDIAAdapter = new TestDIAAdapter();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.BeforeDIAAdapter = new TestDIAAdapter();
        this.AfterDIAAdapter = new TestDIAAdapter();
        this.TestDataGenerator = new DiffTestDataGenerator(beforeDIAAdapter: this.BeforeDIAAdapter, afterDIAAdapter: this.AfterDIAAdapter);

        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task TemplateNameInQueryStringWorksForTemplatePresentBothBeforeAndAfter()
    {
        var allTFIDiffs = this.TestDataGenerator.GenerateTemplateFoldabilityItemDiffs(out var beforeTFIList, out var afterTFIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateTemplateFoldabilityItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allTFIDiffs as IReadOnlyList<TemplateFoldabilityItemDiff>));

        var testTFIDiff = allTFIDiffs.First(tfiDiff => tfiDiff.BeforeTemplateFoldabilityItem != null && tfiDiff.AfterTemplateFoldabilityItem != null);

        var viewmodel = new TemplateFoldabilityDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                                 this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "TemplateName", testTFIDiff.TemplateName }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(testTFIDiff, viewmodel.TemplateFoldabilityItemDiff));
    }

    [TestMethod]
    public async Task TemplateNameInQueryStringWorksForTemplatePresentOnlyInBefore()
    {
        var allTFIDiffs = this.TestDataGenerator.GenerateTemplateFoldabilityItemDiffs(out var beforeTFIList, out var afterTFIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateTemplateFoldabilityItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allTFIDiffs as IReadOnlyList<TemplateFoldabilityItemDiff>));

        var testTFIDiff = allTFIDiffs.First(tfiDiff => tfiDiff.BeforeTemplateFoldabilityItem != null && tfiDiff.AfterTemplateFoldabilityItem is null);

        var viewmodel = new TemplateFoldabilityDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                                 this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "TemplateName", testTFIDiff.TemplateName }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(testTFIDiff, viewmodel.TemplateFoldabilityItemDiff));
    }

    [TestMethod]
    public async Task TemplateNameInQueryStringWorksForTemplatePresentOnlyInAfter()
    {
        var allTFIDiffs = this.TestDataGenerator.GenerateTemplateFoldabilityItemDiffs(out var beforeTFIList, out var afterTFIList);
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateTemplateFoldabilityItemDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allTFIDiffs as IReadOnlyList<TemplateFoldabilityItemDiff>));

        var testTFIDiff = allTFIDiffs.First(tfiDiff => tfiDiff.BeforeTemplateFoldabilityItem is null && tfiDiff.AfterTemplateFoldabilityItem != null);

        var viewmodel = new TemplateFoldabilityDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                                 this.TestDataGenerator.MockDiffSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "TemplateName", testTFIDiff.TemplateName }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(testTFIDiff, viewmodel.TemplateFoldabilityItemDiff));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
