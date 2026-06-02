using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine.Tests.PE.Symbols;

[TestClass]
public sealed class PESymbolBaseTests : IDisposable
{
    SessionDataCache DataCache = new SessionDataCache();

    [TestInitialize]
    public void TestInitialize() => this.DataCache = new SessionDataCache()
    {
        AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
    };

    [TestMethod]
    public void PESymbolsOfDifferentTypeAreNotVeryLikelyTheSame()
    {
        var thunk = new ThunkSymbol(this.DataCache, "symbol123", rva: 123, size: 12, symIndexId: 0);
        var pdata = new PDataSymbol(targetStartRVA: 123, unwindInfoStartRVA: 0, rva: 1, size: 12, SymbolSourcesSupported.All);
        pdata.UpdateTargetSymbol(thunk);
        var tryMap = new TryMapSymbol(thunk, targetStartRVA: 456, rva: 100, size: 12, SymbolSourcesSupported.All);

        Assert.IsFalse(pdata.IsVeryLikelyTheSameAs(tryMap));
        Assert.IsFalse(tryMap.IsVeryLikelyTheSameAs(pdata));
    }

    [TestMethod]
    public void PESymbolsAreTheSameWhenTypesAndNamesMatch()
    {
        var thunk = new ThunkSymbol(this.DataCache, "symbol123", rva: 300, size: 12, symIndexId: 0);
        var tryMap1 = new TryMapSymbol(thunk, targetStartRVA: 123, rva: 1, size: 12, SymbolSourcesSupported.All);
        var tryMap2 = new TryMapSymbol(thunk, targetStartRVA: 456, rva: 100, size: 12, SymbolSourcesSupported.All);

        Assert.IsTrue(tryMap1.IsVeryLikelyTheSameAs(tryMap2));
        Assert.IsTrue(tryMap2.IsVeryLikelyTheSameAs(tryMap1));
    }

    [TestMethod]
    public void PESymbolNameInferredFromTargetSymbol()
    {
        uint nextSymIndexId = 0;
        var thunk = new ThunkSymbol(this.DataCache, "symbol123", rva: 300, size: 12, symIndexId: nextSymIndexId++);
        var tryMap1 = new TryMapSymbol(thunk, targetStartRVA: 123, rva: 1, size: 12, SymbolSourcesSupported.All);

        // For functions, we use the FullName, so test this too
        var returnValueType = new BasicTypeSymbol(this.DataCache, "void", 0, nextSymIndexId++);
        var args = new TypeSymbol[]
        {
                new BasicTypeSymbol(this.DataCache, "int", 4, nextSymIndexId++),
                new ModifiedTypeSymbol(this.DataCache,
                    new ModifiedTypeSymbol(this.DataCache, new BasicTypeSymbol(this.DataCache, "float", 4, nextSymIndexId++),"float&", size: 4, nextSymIndexId++),
                    "const float&", size: 4, nextSymIndexId++)
        };
        var function = new SimpleFunctionCodeSymbol(this.DataCache, "Namespace::Function1", rva: 500, size: 50, symIndexId: nextSymIndexId++,
                                                    new FunctionTypeSymbol(this.DataCache, "", 0, nextSymIndexId++, isConst: true, isVolatile: false, args, returnValueType));
        var tryMap2 = new TryMapSymbol(function, targetStartRVA: 456, rva: 100, size: 12, SymbolSourcesSupported.All);

        Assert.AreEqual("[tryMap] symbol123", tryMap1.Name);
        Assert.AreEqual("[tryMap] Namespace::Function1(int, const float&) const", tryMap2.Name);
    }

    [TestMethod]
    public void PESymbolNameIsConjuredIfNoTargetSymbolAvailable()
    {
        var tryMap = new TryMapSymbol(null, targetStartRVA: 1549, rva: 1, size: 12, SymbolSourcesSupported.All);

        Assert.AreEqual($"[tryMap] <unnamed code at 0x{1549:X}>", tryMap.Name);
    }

    public void Dispose() => this.DataCache.Dispose();
}
