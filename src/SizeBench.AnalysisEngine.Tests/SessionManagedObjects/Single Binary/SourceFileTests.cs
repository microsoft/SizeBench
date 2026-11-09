using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Tests;

[TestClass]
public class SourceFileTests
{
    [TestMethod]
    public void SourceFilePropertiesThrowBeforeFullyConstructed()
    {
        using var sdc = new SessionDataCache();
        var sourceFile = new SourceFile(sdc, "a.cpp", fileId: 1, compilands: new List<Compiland>() { });

        object dummy;
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.Size);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.VirtualSize);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.Compilands);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.SectionContributions);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.SectionContributionsByName);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.COFFGroupContributions);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.COFFGroupContributionsByName);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.CompilandContributions);
    }

    [TestMethod]
    public void SourceFileThrowsOnModificationAfterFullyConstructed()
    {
        using var generator = new SingleBinaryDataGenerator();

        object dummy;
        Assert.ThrowsExactly<ObjectFullyConstructedAlreadyException>(() => dummy = generator.A1CppSourceFile.GetOrCreateSectionContribution(generator.TextSection));
        Assert.ThrowsExactly<ObjectFullyConstructedAlreadyException>(() => dummy = generator.A1CppSourceFile.GetOrCreateCOFFGroupContribution(generator.TextMnCG));
        Assert.ThrowsExactly<ObjectFullyConstructedAlreadyException>(() => dummy = generator.A1CppSourceFile.GetOrCreateCompilandContribution(generator.A1Compiland));
    }
}
