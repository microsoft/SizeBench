using Dia2Lib;

namespace SizeBench.AnalysisEngine.Symbols.Tests;

[TestClass]
public class SymbolTests
{
    [TestMethod]
    public void SymbolPropertiesFlowThroughAsExpected()
    {
        using var cache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        var symbol = new Symbol(cache, "Test name", rva: 1000u, size: 500u, isVirtualSize: false, symIndexId: 0);
        Assert.AreEqual("Test name", symbol.Name);
        Assert.AreEqual("Test name", symbol.CanonicalName);
        Assert.AreEqual(1000u, symbol.RVA);
        Assert.AreEqual(500u, symbol.Size);
    }

    [TestMethod]
    public void SymbolCanonicalNameFoundCorrectly()
    {
        using var cache = new SessionDataCache();
        var nameCanonicalization = new NameCanonicalization();
        nameCanonicalization.AddName(23, "Test name", SymTagEnum.SymTagFunction);
        nameCanonicalization.AddName(34, "CFoo::DoTheThing", SymTagEnum.SymTagFunction);
        nameCanonicalization.AddName(12, "CFoo::DoAnotherThing", SymTagEnum.SymTagFunction);
        nameCanonicalization.Canonicalize();
        cache.AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
            {
                { 1000u, nameCanonicalization }
            };
        var symbol = new Symbol(cache, "Test name", rva: 1000u, size: 500u, isVirtualSize: false, symIndexId: 23);
        Assert.AreEqual("Test name", symbol.Name);
        Assert.IsTrue(symbol.IsCOMDATFolded);
        Assert.AreEqual(nameCanonicalization.CanonicalName, symbol.CanonicalName);
        Assert.AreEqual(1000u, symbol.RVA);
        Assert.AreEqual(1000u, symbol.RVAEnd);
        Assert.AreEqual(0u, symbol.Size);
        Assert.AreEqual(0u, symbol.VirtualSize);
    }
}
