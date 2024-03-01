using Dia2Lib;

namespace SizeBench.AnalysisEngine.Symbols.Tests;

[TestClass]
public sealed class NameCanonicalizationTests
{
    [TestMethod]
    public void CanonicalizationPicksCorrectName()
    {
        var canonical = new NameCanonicalization();
        canonical.AddName(20u, SymTagEnum.SymTagFunction, name: "Test");
        canonical.AddName(18u, SymTagEnum.SymTagFunction, name: "AnotherName");
        canonical.AddName(19u, SymTagEnum.SymTagFunction, name: "ACanonicalName");
        canonical.AddName(21u, SymTagEnum.SymTagFunction, name: "CFoo::DoTheThing");
        canonical.Canonicalize();

        Assert.AreEqual("ACanonicalName", canonical.CanonicalName);
        Assert.AreEqual(19u, canonical.CanonicalSymIndexID);
    }

    [TestMethod]
    public void PublicSymbolNamesAreUsedIfNoOtherSymbolTagsWereFound()
    {
        var canonical = new NameCanonicalization();
        canonical.AddName(20u, SymTagEnum.SymTagPublicSymbol, name: "public: virtual void Test");
        canonical.AddName(18u, SymTagEnum.SymTagPublicSymbol, name: "public: virtual void AnotherName");
        canonical.AddName(19u, SymTagEnum.SymTagPublicSymbol, name: "public: virtual void ACanonicalName");
        canonical.AddName(21u, SymTagEnum.SymTagPublicSymbol, name: "public: virtual void CFoo::DoTheThing");
        canonical.Canonicalize();

        Assert.AreEqual("public: virtual void ACanonicalName", canonical.CanonicalName);
        Assert.AreEqual(19u, canonical.CanonicalSymIndexID);
    }

    [TestMethod]
    public void PublicSymbolNamesAreIgnoredIfNamesAreFoundViaAnotherSymTag()
    {
        var canonical = new NameCanonicalization();
        canonical.AddName(20u, SymTagEnum.SymTagFunction, name: "Test");
        canonical.AddName(25u, SymTagEnum.SymTagPublicSymbol, name: "public: virtual void Test"); // This would sort first alphabetically but it's not useful and should be discarded
        canonical.Canonicalize();

        Assert.AreEqual(1, canonical.NamesBySymIndexID.Count);
        Assert.AreEqual("Test", canonical.CanonicalName);
        Assert.AreEqual(20u, canonical.CanonicalSymIndexID);
    }

    [TestMethod]
    public void NameCreatorIsAvoidedIfPossibleForPerformance()
    {
        using var cache = new SessionDataCache();
        var nameCreatorCallCount = 0;
        string nameCreator(IDiaSymbol sym, IDiaSession diaSess, SessionDataCache cache)
        {
            nameCreatorCallCount++;
            return $"NameFromCreator{nameCreatorCallCount}";
        }

        var canonical = new NameCanonicalization();

        canonical.AddName(20u, SymTagEnum.SymTagFunction, name: "Test", nameCreator: nameCreator);
        Assert.AreEqual(0, nameCreatorCallCount);

        // We've added a function, so if we try to add a public symbol next, we should skip trying to get the name from the creator for perf
        canonical.AddName(25u, SymTagEnum.SymTagPublicSymbol, diaSymbol: new Mock<IDiaSymbol>().Object, diaSession: new Mock<IDiaSession>().Object, dataCache: cache, nameCreator: nameCreator);
        Assert.AreEqual(0, nameCreatorCallCount);

        // Similarly if we try to add a thunk
        canonical.AddName(30u, SymTagEnum.SymTagThunk, diaSymbol: new Mock<IDiaSymbol>().Object, diaSession: new Mock<IDiaSession>().Object, dataCache: cache, nameCreator: nameCreator);
        Assert.AreEqual(0, nameCreatorCallCount);

        // But if we add something else with a null name, we'll go to the name creator
        canonical.AddName(35u, SymTagEnum.SymTagData, diaSymbol: new Mock<IDiaSymbol>().Object, diaSession: new Mock<IDiaSession>().Object, dataCache: cache, nameCreator: nameCreator);
        Assert.AreEqual(1, nameCreatorCallCount);
    }

    [TestMethod]
    public void NameCreatorIsCalledIfNecessary()
    {
        using var cache = new SessionDataCache();
        var nameCreatorCallCount = 0;
        string nameCreator(IDiaSymbol sym, IDiaSession diaSess, SessionDataCache cache)
        {
            nameCreatorCallCount++;
            return $"NameFromCreator{nameCreatorCallCount}";
        }

        {
            // If the first thing added is a public symbol, we need to look up its name because we may not get a better one later
            nameCreatorCallCount = 0;
            var canonical = new NameCanonicalization();
            canonical.AddName(20u, SymTagEnum.SymTagPublicSymbol, diaSymbol: new Mock<IDiaSymbol>().Object, diaSession: new Mock<IDiaSession>().Object, dataCache: cache, nameCreator: nameCreator);
            Assert.AreEqual(1, nameCreatorCallCount);

            // If we add another public symbol, gotta look at that too, it might be better than the first one
            canonical.AddName(25u, SymTagEnum.SymTagPublicSymbol, diaSymbol: new Mock<IDiaSymbol>().Object, diaSession: new Mock<IDiaSession>().Object, dataCache: cache, nameCreator: nameCreator);
            Assert.AreEqual(2, nameCreatorCallCount);
        }

        {
            // Much like public symbols, we need to look at thunks if they're the first thing added
            nameCreatorCallCount = 0;
            var canonical = new NameCanonicalization();
            canonical.AddName(20u, SymTagEnum.SymTagThunk, diaSymbol: new Mock<IDiaSymbol>().Object, diaSession: new Mock<IDiaSession>().Object, dataCache: cache, nameCreator: nameCreator);
            Assert.AreEqual(1, nameCreatorCallCount);

            // If we add another thunk, gotta look at that too, it might be better than the first one
            canonical.AddName(25u, SymTagEnum.SymTagThunk, diaSymbol: new Mock<IDiaSymbol>().Object, diaSession: new Mock<IDiaSession>().Object, dataCache: cache, nameCreator: nameCreator);
            Assert.AreEqual(2, nameCreatorCallCount);
        }
    }
}
