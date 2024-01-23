using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Converters.Tests;

[TestClass]
public sealed class BinarySectionToDescriptionConverterTests
{
    [TestMethod]
    public void KnownBinarySectionDescriptionFound()
    {
        using var cache = new SessionDataCache();
        var textSection = new BinarySection(cache, ".text", 100, 100, 100, 100, 100, SectionCharacteristics.MemExecute | SectionCharacteristics.MemRead);
        Assert.AreEqual("Code", BinarySectionToDescriptionConverter.Instance.Convert(textSection, typeof(string), null, null));
    }

    [TestMethod]
    public void UnknownBinarySectionIsEmptyString()
    {
        using var cache = new SessionDataCache();
        var unknownSection = new BinarySection(cache, ".unknown_section", 100, 100, 100, 100, 100, SectionCharacteristics.MemExecute | SectionCharacteristics.MemRead);
        Assert.AreEqual(String.Empty, BinarySectionToDescriptionConverter.Instance.Convert(unknownSection, typeof(string), null, null));
    }

    [TestMethod]
    public void KnownBinarySectionDiffDescriptionFound()
    {
        using var beforeCache = new SessionDataCache();
        var beforeRdataSection = new BinarySection(beforeCache, ".rdata", 100, 100, 100, 100, 100, SectionCharacteristics.MemExecute | SectionCharacteristics.MemRead);
        beforeRdataSection.MarkFullyConstructed();

        using var afterCache = new SessionDataCache();
        var afterRdataSection = new BinarySection(afterCache, ".rdata", 100, 100, 100, 100, 100, SectionCharacteristics.MemExecute | SectionCharacteristics.MemRead);
        afterRdataSection.MarkFullyConstructed();

        using var diffCache = new DiffSessionDataCache();
        var rdataSectionDiff = new BinarySectionDiff(beforeRdataSection, afterRdataSection, diffCache);

        Assert.AreEqual("Read-only data", BinarySectionToDescriptionConverter.Instance.Convert(rdataSectionDiff, typeof(string), null, null));
    }

    [TestMethod]
    public void NullBinarySectionIsEmptyString()
    {
        Assert.AreEqual(String.Empty, BinarySectionToDescriptionConverter.Instance.Convert(null, typeof(string), null, null));
    }
}
