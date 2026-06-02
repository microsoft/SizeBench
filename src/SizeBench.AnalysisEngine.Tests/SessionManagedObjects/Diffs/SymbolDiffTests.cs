using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Diffs.Tests;

[TestClass]
public sealed class SymbolDiffTests : IDisposable
{
    private DiffTestDataGenerator _testGenerator = new DiffTestDataGenerator();

    //TODO: could this be ClassInitialize to improve test execution time?  I think all this state would be immutable between tests...right?
    [TestInitialize]
    public void TestInitialize() => this._testGenerator = new DiffTestDataGenerator();

    [TestMethod]
    public void WorksWithNullAfter()
    {
        uint nextSymIndexId = 0;
        var beforeSymbol = new Symbol(this._testGenerator.BeforeDataCache, "someSymbol", rva: 0, size: 20, isVirtualSize: false, symIndexId: nextSymIndexId++);

        var symbolDiff = new SymbolDiff(beforeSymbol, null);

        Assert.AreEqual("someSymbol", symbolDiff.Name);
        Assert.AreEqual(-20, symbolDiff.SizeDiff);
        Assert.IsTrue(ReferenceEquals(symbolDiff.BeforeSymbol, beforeSymbol));
        Assert.IsNull(symbolDiff.AfterSymbol);
    }

    [TestMethod]
    public void WorksWithNullBefore()
    {
        uint nextSymIndexId = 0;
        var afterSymbol = new Symbol(this._testGenerator.AfterDataCache, "someSymbol", rva: 0, size: 20, isVirtualSize: false, symIndexId: nextSymIndexId++);

        var symbolDiff = new SymbolDiff(null, afterSymbol);

        Assert.AreEqual("someSymbol", symbolDiff.Name);
        Assert.AreEqual(20, symbolDiff.SizeDiff);
        Assert.IsTrue(ReferenceEquals(symbolDiff.AfterSymbol, afterSymbol));
        Assert.IsNull(symbolDiff.BeforeSymbol);
    }

    [TestMethod]
    public void ThrowsIfBothBeforeAndAfterNull() => Assert.ThrowsException<ArgumentException>(() => new SymbolDiff(null, null));

    [TestMethod]
    public void WorksWithPESymbols()
    {
        var beforeThunk = new ThunkSymbol(this._testGenerator.BeforeDataCache, "symbol123", rva: 300, size: 12, symIndexId: 0);
        var beforeSymbol = new TryMapSymbol(beforeThunk, targetStartRVA: 123, rva: 500, size: 120, SymbolSourcesSupported.All);

        var afterThunk = new ThunkSymbol(this._testGenerator.AfterDataCache, "symbol123", rva: 300, size: 12, symIndexId: 0);
        var afterSymbol = new TryMapSymbol(afterThunk, targetStartRVA: 600, rva: 1000, size: 64, SymbolSourcesSupported.All);

        var symbolDiff = new SymbolDiff(beforeSymbol, afterSymbol);

        Assert.AreEqual("[tryMap] symbol123", symbolDiff.Name);
        Assert.AreEqual(64 - 120, symbolDiff.SizeDiff);
        Assert.IsTrue(ReferenceEquals(symbolDiff.BeforeSymbol, beforeSymbol));
        Assert.IsTrue(ReferenceEquals(symbolDiff.AfterSymbol, afterSymbol));

        // Try it the other way around just to be sure size can be positive
        symbolDiff = new SymbolDiff(afterSymbol, beforeSymbol);
        Assert.AreEqual("[tryMap] symbol123", symbolDiff.Name);
        Assert.AreEqual(120 - 64, symbolDiff.SizeDiff);
        Assert.IsTrue(ReferenceEquals(symbolDiff.BeforeSymbol, afterSymbol));
        Assert.IsTrue(ReferenceEquals(symbolDiff.AfterSymbol, beforeSymbol));
    }

    [TestMethod]
    public void WorksWithSessionSymbols()
    {
        uint nextSymIndexId = 0;
        var beforeSymbol = new SimpleFunctionCodeSymbol(this._testGenerator.BeforeDataCache, "CFoo::ABC", rva: 500, size: 800, symIndexId: nextSymIndexId++);
        var afterSymbol = new SimpleFunctionCodeSymbol(this._testGenerator.BeforeDataCache, "CFoo::ABC", rva: 100, size: 250, symIndexId: nextSymIndexId++);

        var symbolDiff = new SymbolDiff(beforeSymbol, afterSymbol);

        Assert.AreEqual("CFoo::ABC()", symbolDiff.Name);
        Assert.AreEqual(250 - 800, symbolDiff.SizeDiff);
        Assert.IsTrue(ReferenceEquals(symbolDiff.BeforeSymbol, beforeSymbol));
        Assert.IsTrue(ReferenceEquals(symbolDiff.AfterSymbol, afterSymbol));

        // Try it the other way around just to be sure size can be positive
        symbolDiff = new SymbolDiff(afterSymbol, beforeSymbol);
        Assert.AreEqual("CFoo::ABC()", symbolDiff.Name);
        Assert.AreEqual(800 - 250, symbolDiff.SizeDiff);
        Assert.IsTrue(ReferenceEquals(symbolDiff.BeforeSymbol, afterSymbol));
        Assert.IsTrue(ReferenceEquals(symbolDiff.AfterSymbol, beforeSymbol));
    }

    public void Dispose() => this._testGenerator.Dispose();
}
