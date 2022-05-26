using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.DiffSessionTasks.Tests;

[TestClass]
public sealed class LoadSymbolDiffByBeforeAndAfterRVAsSessionTaskTests : IDisposable
{
    private DiffTestDataGenerator _generator = new DiffTestDataGenerator();
    private TestDIAAdapter BeforeDIAAdapter = new TestDIAAdapter();
    private TestDIAAdapter AfterDIAAdapter = new TestDIAAdapter();

    [TestInitialize]
    public void TestInitialize()
    {
        this.BeforeDIAAdapter = new TestDIAAdapter();
        this.AfterDIAAdapter = new TestDIAAdapter();

        this._generator = new DiffTestDataGenerator(beforeDIAAdapter: this.BeforeDIAAdapter, afterDIAAdapter: this.AfterDIAAdapter);
    }

    [TestMethod]
    public async Task CanExecuteWithoutProgressReporting()
    {
        var allSymbolDiffsInBinarySectionDiff = this._generator.GenerateSymbolDiffsInBinarySectionList(this._generator.TextSectionDiff);
        var diffToFind = allSymbolDiffsInBinarySectionDiff.First(sd => sd.BeforeSymbol != null && sd.AfterSymbol != null && sd.SizeDiff != 0);

        var task = new LoadSymbolDiffByBeforeAndAfterRVAsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => Task.FromResult(diffToFind.BeforeSymbol),
            (logger) => Task.FromResult(diffToFind.AfterSymbol),
            progress: null,
            token: CancellationToken.None);

        using var logger = new NoOpLogger();
        var results = await task.ExecuteAsync(logger);

        Assert.IsNotNull(results);
        Assert.AreEqual(diffToFind.Name, results.Name);
        Assert.IsNotNull(results.BeforeSymbol);
        Assert.IsNotNull(results.AfterSymbol);
        Assert.AreEqual(diffToFind.BeforeSymbol, results.BeforeSymbol);
        Assert.AreEqual(diffToFind.AfterSymbol, results.AfterSymbol);
        Assert.AreEqual(diffToFind.SizeDiff, results.SizeDiff);
        Assert.AreEqual(diffToFind.VirtualSizeDiff, results.VirtualSizeDiff);
    }

    [TestMethod]
    public async Task CanLoadSymbolDiffPresentOnlyInBefore()
    {
        var allSymbolDiffsInBinarySectionDiff = this._generator.GenerateSymbolDiffsInBinarySectionList(this._generator.TextSectionDiff);
        var diffToFind = allSymbolDiffsInBinarySectionDiff.First(sd => sd.BeforeSymbol != null && sd.AfterSymbol is null && sd.SizeDiff != 0);

        var task = new LoadSymbolDiffByBeforeAndAfterRVAsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => Task.FromResult(diffToFind.BeforeSymbol),
            (logger) => Task.FromResult<ISymbol?>(null),
            progress: null,
            token: CancellationToken.None);

        using var logger = new NoOpLogger();
        var results = await task.ExecuteAsync(logger);

        Assert.IsNotNull(results);
        Assert.AreEqual(diffToFind.Name, results.Name);
        Assert.IsNotNull(results.BeforeSymbol);
        Assert.IsNull(results.AfterSymbol);
        Assert.AreEqual(diffToFind.BeforeSymbol, results.BeforeSymbol);
        Assert.AreEqual(diffToFind.SizeDiff, results.SizeDiff);
        Assert.AreEqual(diffToFind.VirtualSizeDiff, results.VirtualSizeDiff);
    }

    [TestMethod]
    public async Task CanLoadSymbolDiffPresentOnlyInAfter()
    {
        var allSymbolDiffsInBinarySectionDiff = this._generator.GenerateSymbolDiffsInBinarySectionList(this._generator.TextSectionDiff);
        var diffToFind = allSymbolDiffsInBinarySectionDiff.First(sd => sd.BeforeSymbol is null && sd.AfterSymbol != null && sd.SizeDiff != 0);

        var task = new LoadSymbolDiffByBeforeAndAfterRVAsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => Task.FromResult<ISymbol?>(null),
            (logger) => Task.FromResult(diffToFind.AfterSymbol),
            progress: null,
            token: CancellationToken.None);

        using var logger = new NoOpLogger();
        var results = await task.ExecuteAsync(logger);

        Assert.IsNotNull(results);
        Assert.AreEqual(diffToFind.Name, results.Name);
        Assert.IsNull(results.BeforeSymbol);
        Assert.IsNotNull(results.AfterSymbol);
        Assert.AreEqual(diffToFind.AfterSymbol, results.AfterSymbol);
        Assert.AreEqual(diffToFind.SizeDiff, results.SizeDiff);
        Assert.AreEqual(diffToFind.VirtualSizeDiff, results.VirtualSizeDiff);
    }

    [TestMethod]
    public async Task ReturnsNullWhenSymbolNotFoundInBeforeOrAfter()
    {
        var task = new LoadSymbolDiffByBeforeAndAfterRVAsSessionTask(
            this._generator.DiffSessionTaskParameters,
            (logger) => Task.FromResult<ISymbol?>(null),
            (logger) => Task.FromResult<ISymbol?>(null),
            progress: null,
            token: CancellationToken.None);

        using var logger = new NoOpLogger();
        var results = await task.ExecuteAsync(logger);

        Assert.IsNull(results);
    }

    public void Dispose() => this._generator.Dispose();
}
