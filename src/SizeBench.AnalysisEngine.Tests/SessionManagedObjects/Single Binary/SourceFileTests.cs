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
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.Size);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.VirtualSize);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.Compilands);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.SectionContributions);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.SectionContributionsByName);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.COFFGroupContributions);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.COFFGroupContributionsByName);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = sourceFile.CompilandContributions);
    }

    [TestMethod]
    public void SourceFileThrowsOnModificationAfterFullyConstructed()
    {
        using var generator = new SingleBinaryDataGenerator();

        object dummy;
        Assert.ThrowsException<ObjectFullyConstructedAlreadyException>(() => dummy = generator.A1CppSourceFile.GetOrCreateSectionContribution(generator.TextSection));
        Assert.ThrowsException<ObjectFullyConstructedAlreadyException>(() => dummy = generator.A1CppSourceFile.GetOrCreateCOFFGroupContribution(generator.TextMnCG));
        Assert.ThrowsException<ObjectFullyConstructedAlreadyException>(() => dummy = generator.A1CppSourceFile.GetOrCreateCompilandContribution(generator.A1Compiland));
    }
}
