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
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = compiland.Size);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = compiland.VirtualSize);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = compiland.SectionContributions);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = compiland.SectionContributionsByName);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = compiland.COFFGroupContributions);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = compiland.COFFGroupContributionsByName);
    }

    [TestMethod]
    public void CompilandThrowsOnModificationAfterFullyConstructed()
    {
        using var generator = new SingleBinaryDataGenerator();

        object dummy;
        Assert.ThrowsExactly<ObjectFullyConstructedAlreadyException>(() => dummy = generator.A1Compiland.GetOrCreateSectionContribution(generator.TextSection));
        Assert.ThrowsExactly<ObjectFullyConstructedAlreadyException>(() => dummy = generator.A1Compiland.GetOrCreateCOFFGroupContribution(generator.TextMnCG));
    }
}
