using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Tests;

[TestClass]
public class LibTests
{
    [TestMethod]
    public void LibPropertiesThrowBeforeFullyConstructed()
    {
        using var generator = new SingleBinaryDataGenerator();
        var lib = new Library("c.lib");

        object dummy;
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = lib.Size);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = lib.VirtualSize);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = lib.Compilands);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = lib.SectionContributions);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = lib.SectionContributionsByName);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = lib.COFFGroupContributions);
        Assert.ThrowsExactly<ObjectNotYetFullyConstructedException>(() => dummy = lib.COFFGroupContributionsByName);
    }

    [TestMethod]
    public void LibThrowsOnModificationAfterFullyConstructed()
    {
        using var generator = new SingleBinaryDataGenerator();

        Assert.ThrowsExactly<ObjectFullyConstructedAlreadyException>(() => generator.ALib.GetOrCreateCOFFGroupContribution(generator.BssCG));
        Assert.ThrowsExactly<ObjectFullyConstructedAlreadyException>(() => generator.ALib.GetOrCreateSectionContribution(generator.TextSection));
        Assert.ThrowsExactly<ObjectFullyConstructedAlreadyException>(() => generator.ALib.GetOrCreateCompiland(generator.DataCache, "blah.obj", generator._nextSymIndexId++, generator.DIAAdapter));
    }
}
