using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.DiffSessionTasks.Tests;

[TestClass]
public sealed class EnumerateBinarySectionsAndCOFFGroupDiffsSessionTaskTests : IDisposable
{
    private DiffTestDataGenerator _generator = new DiffTestDataGenerator(initializeDiffObjects: false);

    [TestInitialize]
    public void TestInitialize() => this._generator = new DiffTestDataGenerator(initializeDiffObjects: false);

    [TestMethod]
    public async Task CanExecuteWithoutProgressReporting()
    {
        this._generator.MockBeforeSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroups(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                         .Returns(() => Task.FromResult(this._generator.BeforeSections as IReadOnlyList<BinarySection>));
        this._generator.MockAfterSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroups(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                        .Returns(() => Task.FromResult(this._generator.AfterSections as IReadOnlyList<BinarySection>));

        var task = new EnumerateBinarySectionsAndCOFFGroupDiffsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => this._generator.MockBeforeSession.Object.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None, logger),
            (logger) => this._generator.MockAfterSession.Object.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None, logger),
            CancellationToken.None);

        using var logger = new NoOpLogger();
        var results = await task.ExecuteAsync(logger);

        Assert.AreEqual(5, results.Count);
        Assert.AreEqual(1, results.Where(sectionDiff => sectionDiff.BeforeSection is null).Count());
        Assert.AreEqual(".virt", results.First(sectionDiff => sectionDiff.AfterSection is null).Name);
        Assert.AreEqual(1, results.Where(sectionDiff => sectionDiff.AfterSection is null).Count());
        Assert.AreEqual(".rsrc", results.First(sectionDiff => sectionDiff.BeforeSection is null).Name);
        Assert.AreEqual(0, results.Where(sectionDiff => sectionDiff.BeforeSection is null && sectionDiff.AfterSection is null).Count());

        Assert.AreEqual(500, results.First(sectionDiff => sectionDiff.Name == ".text").SizeDiff);
        Assert.AreEqual(-1000, results.First(sectionDiff => sectionDiff.Name == ".data").SizeDiff);
        Assert.AreEqual(0, results.First(sectionDiff => sectionDiff.Name == ".rdata").SizeDiff);
    }

    [TestMethod]
    public async Task CacheIsFilledInAndReusedAfterFirstCall()
    {
        Assert.IsNull(this._generator.DiffDataCache.AllBinarySectionDiffs);

        this._generator.MockBeforeSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroups(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                         .Returns(() => Task.FromResult(this._generator.BeforeSections as IReadOnlyList<BinarySection>));
        this._generator.MockAfterSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroups(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                        .Returns(() => Task.FromResult(this._generator.AfterSections as IReadOnlyList<BinarySection>));

        var task = new EnumerateBinarySectionsAndCOFFGroupDiffsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => this._generator.MockBeforeSession.Object.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None, logger),
            (logger) => this._generator.MockAfterSession.Object.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None, logger),
            CancellationToken.None);

        using var logger = new NoOpLogger();
        var results = await task.ExecuteAsync(logger);

        Assert.IsNotNull(this._generator.DiffDataCache.AllBinarySectionDiffs);
        Assert.AreEqual(5, this._generator.DiffDataCache.AllBinarySectionDiffs.Count);

        var list = this._generator.DiffDataCache.AllBinarySectionDiffs;

        task = new EnumerateBinarySectionsAndCOFFGroupDiffsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => this._generator.MockBeforeSession.Object.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None, logger),
            (logger) => this._generator.MockAfterSession.Object.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None, logger),
            CancellationToken.None);

        var output2 = await task.ExecuteAsync(logger);

        Assert.IsNotNull(this._generator.DiffDataCache.AllBinarySectionDiffs);
        Assert.AreEqual(5, this._generator.DiffDataCache.AllBinarySectionDiffs.Count);
        Assert.IsTrue(ReferenceEquals(list, this._generator.DiffDataCache.AllBinarySectionDiffs));
    }

    public void Dispose() => this._generator.Dispose();
}
