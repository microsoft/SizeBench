namespace SizeBench.AnalysisEngine.SessionManagedObjects.Tests;

[TestClass]
public class COFFGroupTests
{
    [TestMethod]
    public void COFFGroupThrowsOnModificationAfterFullyConstructed()
    {
        using var sdc = new SessionDataCache();
        var bs = new BinarySection(sdc, ".rdata",
                                   0x400 /* size, including file alignment */,
                                   0x450 /* virtualSize */,
                                   0x0 /* rva */,
                                   0x200 /* fileAlignment */,
                                   0x1000 /* sectionAlignment */,
                                   characteristics: PE.DataSectionFlags.MemoryRead);

        // 0x000-0x0F9
        var cg1 = new COFFGroup(sdc, ".rdata$r",
                                0x100 /* size */,
                                0x000 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                PE.DataSectionFlags.ContentInitializedData)
        {
            Section = bs,
            TailSlopVirtualSizeAlignment = 20
        };
        cg1.MarkFullyConstructed();

        Assert.ThrowsException<ObjectFullyConstructedAlreadyException>(() => cg1.Section = bs);

        // We throw not only on modification, but also on trying to use the RawSize property since it is meant only for pre-fully-constructed scenarios.
        // Post-full-construction, only Size or VirtualSize should be used, as they can be correctly calculated by then.
        object dummy;
        Assert.ThrowsException<ObjectFullyConstructedAlreadyException>(() => dummy = cg1.RawSize);
    }

    [TestMethod]
    public void COFFGroupPropertiesThrowBeforeFullyConstructed()
    {
        using var sdc = new SessionDataCache();
        var bs = new BinarySection(sdc, ".rdata",
                                   0x400 /* size, including file alignment */,
                                   0x450 /* virtualSize */,
                                   0x0 /* rva */,
                                   0x200 /* fileAlignment */,
                                   0x1000 /* sectionAlignment */,
                                   characteristics: PE.DataSectionFlags.MemoryRead);

        // 0x000-0x0F9
        var cg1 = new COFFGroup(sdc, ".rdata$r",
                                0x100 /* size */,
                                0x000 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                PE.DataSectionFlags.ContentInitializedData)
        {
            Section = bs
        };

        object dummy;
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = cg1.Section);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = cg1.IsVirtualSizeOnly);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = cg1.Size);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = cg1.VirtualSize);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = cg1.VirtualSizeIncludingPadding);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = cg1.TailSlopSizeAlignment);
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = cg1.TailSlopVirtualSizeAlignment);

        // And these properties should be accessible even before fully-constructed as they are safe as soon as the constructor runs:
        dummy = cg1.FileAlignment;
        dummy = cg1.SectionAlignment;
    }
}
