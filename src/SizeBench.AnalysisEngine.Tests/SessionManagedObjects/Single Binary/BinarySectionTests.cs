using System.Reflection.PortableExecutable;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Tests;

[TestClass]
public class BinarySectionTests
{
    [TestMethod]
    public void TotalAlignmentPaddingBetweenCOFFGroupsIsZeroWhenTightlyPacked()
    {
        using var sdc = new SessionDataCache();
        var bs = new BinarySection(sdc, ".rdata",
                                   0x400 /* size, including file alignment */,
                                   0x450 /* virtualSize */,
                                   0x0 /* rva */,
                                   0x200 /* fileAlignment */,
                                   0x1000 /* sectionAlignment */,
                                   characteristics: SectionCharacteristics.MemRead);

        // 0x000-0x0F9
        var cg1 = new COFFGroup(sdc, ".rdata$r",
                                0x100 /* size */,
                                0x000 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg1.MarkFullyConstructed();

        // 0x100-0x2F9
        var cg2 = new COFFGroup(sdc, ".rdata$x",
                                0x200 /* size */,
                                0x100 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg2.MarkFullyConstructed();
        // 0x300 - 0x349
        var cg3 = new COFFGroup(sdc, ".rdata$00",
                                0x150 /* size */,
                                0x300 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg3.MarkFullyConstructed();

        bs.AddCOFFGroup(cg1);
        bs.AddCOFFGroup(cg2);
        bs.AddCOFFGroup(cg3);

        bs.MarkFullyConstructed();

        Assert.AreEqual(cg3.TailSlopSizeAlignment, (uint)bs.COFFGroups.Sum(cg => cg.TailSlopSizeAlignment)); // The only Tail Slop for Size is the very end of the section
        Assert.AreEqual(bs.TailSlopVirtualSizeAlignment, (uint)bs.COFFGroups.Sum(cg => cg.TailSlopVirtualSizeAlignment)); // The only Tail Slop for VirtualSize is the very end of the section
        Assert.AreEqual(0x400u, bs.Size);
        Assert.AreEqual(0x450u, bs.VirtualSize);
        Assert.AreEqual(0x1000u, bs.VirtualSizeIncludingPadding);
        Assert.AreEqual(0x1000u, bs.SectionAlignment);
        Assert.AreEqual(0x200u, bs.FileAlignment);
    }

    [TestMethod]
    public void TotalAlignmentPaddingBetweenCOFFGroupsIsCorrect()
    {
        using var sdc = new SessionDataCache();
        var bs = new BinarySection(sdc, ".rdata",
                                   0x400 /* size, including file alignment */,
                                   0x100 + 0x200 + 0x50 /* virtualSize */,
                                   0x0 /* rva */,
                                   0x200 /* fileAlignment */,
                                   0x1000 /* sectionAlignment */,
                                   characteristics: SectionCharacteristics.MemRead);

        // 0x000-0x0F4, assume 16 byte alignment will kick it up to 0x100
        var cg1 = new COFFGroup(sdc, ".rdata$r",
                                0x0F4 /* size */,
                                0x000 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg1.MarkFullyConstructed();

        // 0x100-0x2F4, again 16-byte alignment will kick it up to 0x300
        var cg2 = new COFFGroup(sdc, ".rdata$x",
                                0x1F4 /* size */,
                                0x100 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg2.MarkFullyConstructed();
        // 0x300 - 0x349
        var cg3 = new COFFGroup(sdc, ".rdata$00",
                                0x050 /* size */,
                                0x300 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg3.MarkFullyConstructed();

        bs.AddCOFFGroup(cg1);
        bs.AddCOFFGroup(cg2);
        bs.AddCOFFGroup(cg3);

        bs.MarkFullyConstructed();

        // There should be a total of 24 bytes of alignment (0x100-0xF4, and 0x300-0x2F4), plus the padding to the end of the section
        Assert.AreEqual(24u + cg3.TailSlopSizeAlignment, (uint)bs.COFFGroups.Sum(cg => cg.TailSlopSizeAlignment));
        Assert.AreEqual(24u + cg3.TailSlopVirtualSizeAlignment, (uint)bs.COFFGroups.Sum(cg => cg.TailSlopVirtualSizeAlignment));
        Assert.AreEqual(0x400u, bs.Size);
        Assert.AreEqual(0x350u, bs.VirtualSize);
        Assert.AreEqual(0x1000u - 0x350u, bs.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(0x1000u, bs.VirtualSizeIncludingPadding);
    }

    [TestMethod]
    public void TotalAlignmentPaddingBetweenCOFFGroupsCanBoostSizeByOneFileAlignmentChunk()
    {
        const uint fileAlignment = 0x200;

        using var sdc = new SessionDataCache();
        var bs = new BinarySection(sdc, ".rdata",
                                   0x600 /* size, including file alignment */,
                                   0xF8 + 0x1F8 + 0x102 /* virtualSize */,
                                   0x0 /* rva */,
                                   fileAlignment,
                                   0x1000 /* sectionAlignment */,
                                   characteristics: SectionCharacteristics.MemRead);

        // 0x000-0x0F7, assume 16 byte alignment will kick it up to 0x100
        var cg1 = new COFFGroup(sdc, ".rdata$r",
                                0x0F8 /* size */,
                                0x000 /* rva */,
                                fileAlignment,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData)
        {
            Section = bs
        };
        cg1.MarkFullyConstructed();

        // 0x100-0x2F7, again 16-byte alignment will kick it up to 0x300
        var cg2 = new COFFGroup(sdc, ".rdata$x",
                                0x1F8 /* size */,
                                0x100 /* rva */,
                                fileAlignment,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData)
        {
            Section = bs
        };
        cg2.MarkFullyConstructed();

        // 0x300 - 0x401
        var cg3 = new COFFGroup(sdc, ".rdata$00",
                                0x102 /* size */,
                                0x300 /* rva */,
                                fileAlignment,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData)
        {
            Section = bs
        };
        cg3.MarkFullyConstructed();

        bs.AddCOFFGroup(cg1);
        bs.AddCOFFGroup(cg2);
        bs.AddCOFFGroup(cg3);

        bs.MarkFullyConstructed();

        // There should be a total of 16 bytes of alignment (0x100-0xF7, and 0x300-0x2F7) plus the Tail Slop alignment to the FileAlignment (0x401-0x600)
        Assert.AreEqual((0x100 - 0xF7 - 1) + (0x300 - 0x2F7 - 1) + (0x600 - 0x401 - 1), bs.COFFGroups.Sum(cg => cg.TailSlopSizeAlignment));
        // VirtualSize has more padding, as it's aligned to SectionAlignment (0x1000)
        Assert.AreEqual(16u + (0x1000 - 0x401 - 1), bs.COFFGroups.Sum(cg => cg.TailSlopVirtualSizeAlignment));
        Assert.IsTrue(cg1.Size + cg2.Size + cg3.Size < (2 * fileAlignment)); // only 2 FileAlignment-sized chunks come from the COFF Groups
        Assert.AreEqual(3 * fileAlignment, bs.Size); // Yet the binary section is 3 FileAlignment chunks in size because of the padding
        Assert.AreEqual(0x1000u, bs.VirtualSize + bs.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(0x1000u, bs.VirtualSizeIncludingPadding);
    }

    [TestMethod]
    public void MarkFullyConstructedThrowsOnLargeGapsBetweenCOFFGroups()
    {
        using var sdc = new SessionDataCache();
        var bs = new BinarySection(sdc, ".rdata",
                                   0x400 /* size, including file alignment */,
                                   0x400 /* virtualSize */,
                                   0x0 /* rva */,
                                   0x200 /* fileAlignment */,
                                   0x1000 /* sectionAlignment */,
                                   characteristics: SectionCharacteristics.MemRead);

        // 0x000-0x0F4, assume 16 byte alignment will kick it up to 0x100
        var cg1 = new COFFGroup(sdc, ".rdata$r",
                                0x0F4 /* size */,
                                0x000 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg1.MarkFullyConstructed();

        // 0x100-0x29B, 4096-byte alignment (more than the FileAlignment of 512 bytes or 0x200) will kick it up to 0x1000
        var cg2 = new COFFGroup(sdc, ".rdata$x",
                                0x19C /* size */,
                                0x100 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg2.MarkFullyConstructed();

        // 0x1000 - 0x1049
        var cg3 = new COFFGroup(sdc, ".rdata$00",
                                0x050 /* size */,
                                0x1000 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg3.MarkFullyConstructed();

        bs.AddCOFFGroup(cg1);
        bs.AddCOFFGroup(cg2);
        bs.AddCOFFGroup(cg3);

        var exceptionThrown = false;
        try
        {
            bs.MarkFullyConstructed();
        }
        catch (Exception e)
        {
            exceptionThrown = true;
            StringAssert.Contains(e.Message, "gap between COFF Groups", StringComparison.Ordinal);
            StringAssert.Contains(e.Message, cg2.Name, StringComparison.Ordinal);
            StringAssert.Contains(e.Message, cg3.Name, StringComparison.Ordinal);
        }

        Assert.IsTrue(exceptionThrown);
    }

    [TestMethod]
    public void BinarySectionThrowsOnModificationAfterFullyConstructed()
    {
        using var sdc = new SessionDataCache();
        var bs = new BinarySection(sdc, ".rdata",
                                   0x400 /* size, including file alignment */,
                                   0x450 /* virtualSize */,
                                   0x0 /* rva */,
                                   0x200 /* fileAlignment */,
                                   0x1000 /* sectionAlignment */,
                                   characteristics: SectionCharacteristics.MemRead);

        // 0x000-0x0F9
        var cg1 = new COFFGroup(sdc, ".rdata$r",
                                0x100 /* size */,
                                0x000 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg1.MarkFullyConstructed();

        // 0x100-0x2F9
        var cg2 = new COFFGroup(sdc, ".rdata$x",
                                0x200 /* size */,
                                0x100 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg2.MarkFullyConstructed();
        // 0x300 - 0x349
        var cg3 = new COFFGroup(sdc, ".rdata$00",
                                0x150 /* size */,
                                0x300 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg3.MarkFullyConstructed();

        bs.AddCOFFGroup(cg1);
        bs.AddCOFFGroup(cg2);
        bs.AddCOFFGroup(cg3);

        bs.MarkFullyConstructed();

        Assert.ThrowsException<ObjectFullyConstructedAlreadyException>(() => bs.AddCOFFGroup(cg1));
    }

    [TestMethod]
    public void BinarySectionPropertiesThrowBeforeFullyConstructed()
    {
        using var sdc = new SessionDataCache();
        var bs = new BinarySection(sdc, ".rdata",
                                   0x400 /* size, including file alignment */,
                                   0x450 /* virtualSize */,
                                   0x0 /* rva */,
                                   0x200 /* fileAlignment */,
                                   0x1000 /* sectionAlignment */,
                                   characteristics: SectionCharacteristics.MemRead);

        // 0x000-0x0F9
        var cg1 = new COFFGroup(sdc, ".rdata$r",
                                0x100 /* size */,
                                0x000 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg1.MarkFullyConstructed();

        // 0x100-0x2F9
        var cg2 = new COFFGroup(sdc, ".rdata$x",
                                0x200 /* size */,
                                0x100 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg2.MarkFullyConstructed();
        // 0x300 - 0x349
        var cg3 = new COFFGroup(sdc, ".rdata$00",
                                0x150 /* size */,
                                0x300 /* rva */,
                                0x200 /* fileAlignment */,
                                0x1000 /* sectionAlignment */,
                                SectionCharacteristics.ContainsInitializedData);
        cg3.MarkFullyConstructed();

        bs.AddCOFFGroup(cg1);
        bs.AddCOFFGroup(cg2);
        bs.AddCOFFGroup(cg3);

        object dummy;
        Assert.ThrowsException<ObjectNotYetFullyConstructedException>(() => dummy = bs.COFFGroups);

        // And these properties should be accessible even before fully-constructed as they are safe as soon as the constructor runs:
        dummy = bs.Size;
        dummy = bs.VirtualSize;
        dummy = bs.VirtualSizeIncludingPadding;
        dummy = bs.TailSlopVirtualSizeAlignment;
    }
}
