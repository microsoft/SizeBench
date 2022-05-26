using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.DiffSessionTasks.Tests;

[TestClass]
public sealed class EnumerateTemplateFoldabilityDiffsSessionTaskTests : IDisposable
{
    private readonly DiffTestDataGenerator _generator;
    private readonly TestDIAAdapter BeforeDIAAdapter;
    private readonly TestDIAAdapter AfterDIAAdapter;

    public EnumerateTemplateFoldabilityDiffsSessionTaskTests()
    {
        this.BeforeDIAAdapter = new TestDIAAdapter();
        this.AfterDIAAdapter = new TestDIAAdapter();

        this._generator = new DiffTestDataGenerator(beforeDIAAdapter: this.BeforeDIAAdapter, afterDIAAdapter: this.AfterDIAAdapter);
    }

    [TestMethod]
    public async Task CanExecuteWithoutProgressReporting()
    {
        var diffList = this._generator.GenerateTemplateFoldabilityItemDiffs(out _, out _);

        var beforeTFIs = new List<TemplateFoldabilityItem>();
        foreach (var diff in diffList.Where(tfiDiff => tfiDiff.BeforeTemplateFoldabilityItem != null))
        {
            beforeTFIs.Add(diff.BeforeTemplateFoldabilityItem!);
        }

        var afterTFIs = new List<TemplateFoldabilityItem>();
        foreach (var diff in diffList.Where(tfiDiff => tfiDiff.AfterTemplateFoldabilityItem != null))
        {
            afterTFIs.Add(diff.AfterTemplateFoldabilityItem!);
        }

        this._generator.MockBeforeSession.Setup(s => s.EnumerateTemplateFoldabilityItems(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                         .Returns(() => Task.FromResult(beforeTFIs as IReadOnlyList<TemplateFoldabilityItem>));
        this._generator.MockAfterSession.Setup(s => s.EnumerateTemplateFoldabilityItems(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                        .Returns(() => Task.FromResult(afterTFIs as IReadOnlyList<TemplateFoldabilityItem>));

        var task = new EnumerateTemplateFoldabilityDiffsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => this._generator.MockBeforeSession.Object.EnumerateTemplateFoldabilityItems(CancellationToken.None, logger),
            (logger) => this._generator.MockAfterSession.Object.EnumerateTemplateFoldabilityItems(CancellationToken.None, logger),
            null,
            CancellationToken.None);

        using var logger = new NoOpLogger();
        var results = await task.ExecuteAsync(logger);

        Assert.AreEqual(3, results.Count);
        Assert.AreEqual(1, results.Count(tfiDiff => tfiDiff.BeforeTemplateFoldabilityItem is null));
        Assert.AreEqual(1, results.Count(tfiDiff => tfiDiff.AfterTemplateFoldabilityItem is null));
        Assert.AreEqual(1, results.Count(tfiDiff => tfiDiff.BeforeTemplateFoldabilityItem != null && tfiDiff.AfterTemplateFoldabilityItem != null));
        Assert.AreEqual(0, results.Count(tfiDiff => tfiDiff.BeforeTemplateFoldabilityItem is null && tfiDiff.AfterTemplateFoldabilityItem is null));
    }

    public void Dispose() => this._generator.Dispose();
}
