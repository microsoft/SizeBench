using Dia2Lib;

namespace SizeBench.AnalysisEngine.Symbols.Tests;

[TestClass]
public sealed class NameCanonicalizationTests
{
    [TestMethod]
    public void CanonicalizationPicksCorrectName()
    {
        var canonical = new NameCanonicalization();
        canonical.AddName(20u, "Test", SymTagEnum.SymTagFunction);
        canonical.AddName(18u, "AnotherName", SymTagEnum.SymTagFunction);
        canonical.AddName(19u, "ACanonicalName", SymTagEnum.SymTagFunction);
        canonical.AddName(21u, "CFoo::DoTheThing", SymTagEnum.SymTagFunction);
        canonical.Canonicalize();

        Assert.AreEqual("ACanonicalName", canonical.CanonicalName);
        Assert.AreEqual(19u, canonical.CanonicalSymIndexID);
    }

    [TestMethod]
    public void PublicSymbolNamesAreUsedIfNoOtherSymbolTagsWereFound()
    {
        var canonical = new NameCanonicalization();
        canonical.AddName(20u, "public: virtual void Test", SymTagEnum.SymTagPublicSymbol);
        canonical.AddName(18u, "public: virtual void AnotherName", SymTagEnum.SymTagPublicSymbol);
        canonical.AddName(19u, "public: virtual void ACanonicalName", SymTagEnum.SymTagPublicSymbol);
        canonical.AddName(21u, "public: virtual void CFoo::DoTheThing", SymTagEnum.SymTagPublicSymbol);
        canonical.Canonicalize();

        Assert.AreEqual("public: virtual void ACanonicalName", canonical.CanonicalName);
        Assert.AreEqual(19u, canonical.CanonicalSymIndexID);
    }

    [TestMethod]
    public void PublicSymbolNamesAreIgnoredIfNamesAreFoundViaAnotherSymTag()
    {
        var canonical = new NameCanonicalization();
        canonical.AddName(20u, "Test", SymTagEnum.SymTagFunction);
        canonical.AddName(25u, "public: virtual void Test", SymTagEnum.SymTagPublicSymbol); // This would sort first alphabetically but it's not useful and should be discarded
        canonical.Canonicalize();

        Assert.AreEqual(1, canonical.NamesBySymIndexID.Count);
        Assert.AreEqual("Test", canonical.CanonicalName);
        Assert.AreEqual(20u, canonical.CanonicalSymIndexID);
    }
}
