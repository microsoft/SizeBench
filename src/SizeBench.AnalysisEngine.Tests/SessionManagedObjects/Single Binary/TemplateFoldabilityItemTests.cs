using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Tests;

[TestClass]
public sealed class TemplateFoldabilityItemTests : IDisposable
{
    SessionDataCache? DataCache;
    SimpleFunctionCodeSymbol? TemplatedFunction1;
    SimpleFunctionCodeSymbol? TemplatedFunction2;

    [TestInitialize]
    public void TestInitialize()
    {
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };

        this.TemplatedFunction1 = new SimpleFunctionCodeSymbol(this.DataCache, "TemplatedThing<MyType>", rva: 100, size: 50, symIndexId: 1);
        this.TemplatedFunction2 = new SimpleFunctionCodeSymbol(this.DataCache, "TemplatedThing<AnotherType>", rva: 150, size: 100, symIndexId: 2);
    }

    [TestMethod]
    public void WastedSizeCalculatedCorrectly()
    {
        var symbols = new List<IFunctionCodeSymbol>() { this.TemplatedFunction1!, this.TemplatedFunction2! };
        var uniqueSymbols = new List<IFunctionCodeSymbol>() { this.TemplatedFunction1! };
        var item = new TemplateFoldabilityItem("TemplatedThing<T>", symbols, uniqueSymbols, totalSize: 150, percentageSimilarity: 0.86f);
        Assert.AreEqual(150u, item.TotalSize);
        Assert.AreEqual((uint)(150 * 0.86f), item.WastedSize);
        Assert.AreEqual(2, item.Symbols.Count);
        Assert.AreEqual(1, item.UniqueSymbols.Count);
        Assert.AreEqual(0.86f, item.PercentageSimilarity);
        CollectionAssert.AreEquivalent(symbols, item.Symbols.ToList());
        Assert.AreEqual("TemplatedThing<T>", item.TemplateName);
    }

    public void Dispose() => this.DataCache?.Dispose();
}
