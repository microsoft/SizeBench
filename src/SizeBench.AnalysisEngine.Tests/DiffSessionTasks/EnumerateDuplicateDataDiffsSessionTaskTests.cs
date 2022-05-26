using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.DiffSessionTasks.Tests;

[TestClass]
public sealed class EnumerateDuplicateDataDiffsSessionTaskTests : IDisposable
{
    private DiffTestDataGenerator _generator = new DiffTestDataGenerator();

    [TestInitialize]
    public void TestInitialize() => this._generator = new DiffTestDataGenerator();

    [TestMethod]
    public async Task CanExecuteWithoutProgressReporting()
    {
        this._generator.GenerateDuplicateDataItemDiffs(out var beforeDDIs, out var afterDDIs);

        this._generator.MockBeforeSession.Setup(s => s.EnumerateDuplicateDataItems(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                         .Returns(() => Task.FromResult(beforeDDIs as IReadOnlyList<DuplicateDataItem>));
        this._generator.MockAfterSession.Setup(s => s.EnumerateDuplicateDataItems(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                        .Returns(() => Task.FromResult(afterDDIs as IReadOnlyList<DuplicateDataItem>));

        var task = new EnumerateDuplicateDataDiffsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => this._generator.MockBeforeSession.Object.EnumerateDuplicateDataItems(CancellationToken.None, logger),
            (logger) => this._generator.MockAfterSession.Object.EnumerateDuplicateDataItems(CancellationToken.None, logger),
            null,
            CancellationToken.None);

        using var logger = new NoOpLogger();
        var results = await task.ExecuteAsync(logger);

        Assert.AreEqual(15, results.Count);
        Assert.AreEqual(5, results.Where(ddiDiff => ddiDiff.BeforeDuplicate is null).Count());
        Assert.AreEqual(5, results.Where(ddiDiff => ddiDiff.AfterDuplicate is null).Count());
        Assert.AreEqual(5, results.Where(ddiDiff => ddiDiff.BeforeDuplicate != null && ddiDiff.AfterDuplicate != null).Count());
        Assert.AreEqual(0, results.Where(ddiDiff => ddiDiff.BeforeDuplicate is null && ddiDiff.AfterDuplicate is null).Count());
    }

    public void Dispose() => this._generator.Dispose();
}
