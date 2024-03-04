using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.DiffSessionTasks.Tests;

[TestClass]
public sealed class EnumerateLibsAndCompilandDiffsSessionTaskTests : IDisposable
{
    private DiffTestDataGenerator _generator = new DiffTestDataGenerator(initializeDiffObjects: false);

    [TestInitialize]
    public void TestInitialize()
    {
        this._generator = new DiffTestDataGenerator(initializeDiffObjects: false);

        this._generator.MockBeforeSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroups(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                         .Returns(() => Task.FromResult(this._generator.BeforeSections as IReadOnlyList<BinarySection>));
        this._generator.MockAfterSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroups(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                        .Returns(() => Task.FromResult(this._generator.AfterSections as IReadOnlyList<BinarySection>));
        this._generator.MockBeforeSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                         .Returns(() => Task.FromResult(this._generator.BeforeLibs as IReadOnlyCollection<Library>));
        this._generator.MockAfterSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>(), It.IsAny<ILogger>()))
                                        .Returns(() => Task.FromResult(this._generator.AfterLibs as IReadOnlyCollection<Library>));
    }

    [TestMethod]
    public async Task CanExecuteWithoutProgressReporting()
    {
        var task = new EnumerateLibsAndCompilandDiffsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => this._generator.MockBeforeSession.Object.EnumerateLibs(CancellationToken.None, logger),
            (logger) => this._generator.MockAfterSession.Object.EnumerateLibs(CancellationToken.None, logger),
            CancellationToken.None,
            progress: null);

        using var logger = new NoOpLogger();
        var results = await task.ExecuteAsync(logger);

        Assert.AreEqual(4, results.Count);
        Assert.AreEqual(1, results.Where(libDiff => libDiff.BeforeLib is null).Count());
        Assert.AreEqual("c.lib", results.First(libDiff => libDiff.AfterLib is null).Name);
        Assert.AreEqual(1, results.Where(libDiff => libDiff.AfterLib is null).Count());
        Assert.AreEqual("d.lib", results.First(libDiff => libDiff.BeforeLib is null).Name);
        Assert.AreEqual(0, results.Where(libDiff => libDiff.BeforeLib is null && libDiff.AfterLib is null).Count());

        Assert.AreEqual(400, results.First(libDiff => libDiff.Name == "a.lib").SizeDiff);
        Assert.AreEqual(600, results.First(libDiff => libDiff.Name == "a.lib").VirtualSizeDiff);
        Assert.AreEqual(-900, results.First(libDiff => libDiff.Name == "b.lib").SizeDiff);
        Assert.AreEqual(-900, results.First(libDiff => libDiff.Name == "b.lib").VirtualSizeDiff);
        Assert.AreEqual(0, results.First(libDiff => libDiff.Name == "c.lib").SizeDiff);
        Assert.AreEqual(-300, results.First(libDiff => libDiff.Name == "c.lib").VirtualSizeDiff);
        Assert.AreEqual(200, results.First(libDiff => libDiff.Name == "d.lib").SizeDiff);
        Assert.AreEqual(200, results.First(libDiff => libDiff.Name == "d.lib").VirtualSizeDiff);
    }

    [TestMethod]
    public async Task CacheIsFilledInAndReusedAfterFirstCall()
    {
        Assert.IsNull(this._generator.DiffDataCache.AllLibDiffsInList);

        var task = new EnumerateLibsAndCompilandDiffsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => this._generator.MockBeforeSession.Object.EnumerateLibs(CancellationToken.None, logger),
            (logger) => this._generator.MockAfterSession.Object.EnumerateLibs(CancellationToken.None, logger),
            CancellationToken.None,
            progress: null);

        using var logger = new NoOpLogger();
        var output = await task.ExecuteAsync(logger);

        Assert.IsNotNull(this._generator.DiffDataCache.AllLibDiffsInList);
        Assert.AreEqual(4, this._generator.DiffDataCache.AllLibDiffsInList.Count);

        var list = this._generator.DiffDataCache.AllLibDiffsInList;

        task = new EnumerateLibsAndCompilandDiffsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => this._generator.MockBeforeSession.Object.EnumerateLibs(CancellationToken.None, logger),
            (logger) => this._generator.MockAfterSession.Object.EnumerateLibs(CancellationToken.None, logger),
            CancellationToken.None,
            progress: null);

        var output2 = await task.ExecuteAsync(logger);

        Assert.IsNotNull(this._generator.DiffDataCache.AllLibDiffsInList);
        Assert.AreEqual(4, this._generator.DiffDataCache.AllLibDiffsInList.Count);
        Assert.IsTrue(ReferenceEquals(list, this._generator.DiffDataCache.AllLibDiffsInList));
    }

    public void Dispose() => this._generator.Dispose();
}
