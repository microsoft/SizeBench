using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Tests;

[TestClass]
public class CompilandTests
{
    [TestMethod]
    public void CompilandPropertiesThrowBeforeFullyConstructed()
    {
        using var sdc = new SessionDataCache();
        var lib = new Library("a.lib");
        var compiland = new Compiland(sdc, "a.obj", lib, CommonCommandLines.NullCommandLine, 1);

        object dummy;
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = compiland.Size);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = compiland.VirtualSize);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = compiland.SectionContributions);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = compiland.SectionContributionsByName);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = compiland.COFFGroupContributions);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = compiland.COFFGroupContributionsByName);
    }

    [TestMethod]
    public void CompilandThrowsOnModificationAfterFullyConstructed()
    {
        using var generator = new SingleBinaryDataGenerator();

        object dummy;
        Assert.ThrowsException<ObjectFullyConstructedAlreadyException>(() => dummy = generator.A1Compiland.GetOrCreateSectionContribution(generator.TextSection));
        Assert.ThrowsException<ObjectFullyConstructedAlreadyException>(() => dummy = generator.A1Compiland.GetOrCreateCOFFGroupContribution(generator.TextMnCG));
    }
}
