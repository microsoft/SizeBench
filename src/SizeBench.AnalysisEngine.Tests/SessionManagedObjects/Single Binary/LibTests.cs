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
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = lib.Size);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = lib.VirtualSize);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = lib.Compilands);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = lib.SectionContributions);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = lib.SectionContributionsByName);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = lib.COFFGroupContributions);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = lib.COFFGroupContributionsByName);
    }

    [TestMethod]
    public void LibThrowsOnModificationAfterFullyConstructed()
    {
        using var generator = new SingleBinaryDataGenerator();

        Assert.ThrowsException<ObjectFullyConstructedAlreadyException>(() => generator.ALib.GetOrCreateCOFFGroupContribution(generator.BssCG));
        Assert.ThrowsException<ObjectFullyConstructedAlreadyException>(() => generator.ALib.GetOrCreateSectionContribution(generator.TextSection));
        Assert.ThrowsException<ObjectFullyConstructedAlreadyException>(() => generator.ALib.GetOrCreateCompiland(generator.DataCache, "blah.obj", generator._nextSymIndexId++, generator.DIAAdapter));
    }
}
