namespace SizeBench.AnalysisEngine.Symbols.Tests;

[TestClass]
public sealed class InlineSiteSymbolTests
{
    [TestMethod]
    public void Foo()
    {
        using var cache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        var nextSymIndexId = 123u;
        var blockInlinedInto = new SimpleFunctionCodeSymbol(cache, "functionInlinedInto", 0, 100, nextSymIndexId++);
        var rvaRangesOccupied = RVARangeSet.FromListOfRVARanges(new[] { RVARange.FromRVAAndSize(100u, 10u), RVARange.FromRVAAndSize(120u, 5u) }, maxPaddingToMerge: 1);
        var inlineSite = new InlineSiteSymbol(cache, "someInlinedFunction", nextSymIndexId++, blockInlinedInto,
                                              canonicalSymbolInlinedInto: blockInlinedInto,
                                              rvaRangesOccupied);

        Assert.AreEqual(124u, inlineSite.SymIndexId);
        Assert.AreEqual("someInlinedFunction", inlineSite.Name);
        Assert.AreEqual("functionInlinedInto()", inlineSite.BlockInlinedInto.Name);
        Assert.AreEqual(2, inlineSite.RVARanges.Count());
        Assert.AreEqual(10u + 5u, inlineSite.Size);
    }
}
