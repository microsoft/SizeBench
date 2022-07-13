using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.PE;

namespace SizeBench.GUI.Converters.Tests;

[TestClass]
public sealed class COFFGroupToDescriptionConverterTests
{
    [TestMethod]
    public void SimpleCOFFGroupDescriptionFound()
    {
        using var cache = new SessionDataCache();
        var textDiCG = new COFFGroup(cache, ".text$di", 100, 100, 100, 100, DataSectionFlags.MemoryExecute | DataSectionFlags.MemoryRead);
        Assert.AreEqual("Dynamic Initializers", COFFGroupToDescriptionConverter.Instance.Convert(textDiCG, typeof(string), null, null));
    }

    [TestMethod]
    public void RegexBasedCOFFGroupDescriptionFound()
    {
        using var cache = new SessionDataCache();
        var pri7CG = new COFFGroup(cache, ".text$lp00mybinary.dll_pri7", 100, 100, 100, 100, DataSectionFlags.MemoryExecute | DataSectionFlags.MemoryRead);
        Assert.AreEqual("Code executed during Pri7 PGO training", COFFGroupToDescriptionConverter.Instance.Convert(pri7CG, typeof(string), null, null));
    }

    [TestMethod]
    public void UnknownCOFFGroupIsEmptyString()
    {
        using var cache = new SessionDataCache();
        var unknownCG = new COFFGroup(cache, ".unknown_coff_group", 100, 100, 100, 100, DataSectionFlags.MemoryExecute | DataSectionFlags.MemoryRead);
        Assert.AreEqual(String.Empty, COFFGroupToDescriptionConverter.Instance.Convert(unknownCG, typeof(string), null, null));
    }

    [TestMethod]
    public void SimpleCOFFGroupDiffDescriptionFound()
    {
        using var beforeCache = new SessionDataCache();
        var beforeTextSection = new BinarySection(beforeCache, ".text", 100, 100, 100, 100, 100, DataSectionFlags.MemoryExecute | DataSectionFlags.MemoryRead);
        var beforeTextDiCG = new COFFGroup(beforeCache, ".text$di", 100, 100, 100, 100, DataSectionFlags.MemoryExecute | DataSectionFlags.MemoryRead);
        beforeTextSection.AddCOFFGroup(beforeTextDiCG);
        beforeTextDiCG.MarkFullyConstructed();
        beforeTextSection.MarkFullyConstructed();

        using var afterCache = new SessionDataCache();
        var afterTextSection = new BinarySection(afterCache, ".text", 100, 100, 100, 100, 100, DataSectionFlags.MemoryExecute | DataSectionFlags.MemoryRead);
        var afterTextDiCG = new COFFGroup(afterCache, ".text$di", 100, 100, 100, 100, DataSectionFlags.MemoryExecute | DataSectionFlags.MemoryRead);
        afterTextSection.AddCOFFGroup(afterTextDiCG);
        afterTextDiCG.MarkFullyConstructed();
        afterTextSection.MarkFullyConstructed();

        using var diffCache = new DiffSessionDataCache();
        var textSectionDiff = new BinarySectionDiff(beforeTextSection, afterTextSection, diffCache);

        var textDiCGDiff = textSectionDiff.COFFGroupDiffs.Single(cgDiff => cgDiff.Name == ".text$di");

        Assert.AreEqual("Dynamic Initializers", COFFGroupToDescriptionConverter.Instance.Convert(textDiCGDiff, typeof(string), null, null));
    }

    [TestMethod]
    public void RegexBasedCOFFGroupDiffDescriptionFound()
    {
        using var beforeCache = new SessionDataCache();
        var beforeTextSection = new BinarySection(beforeCache, ".text", 100, 100, 100, 100, 100, DataSectionFlags.MemoryExecute | DataSectionFlags.MemoryRead);
        var beforeTextPri7CG = new COFFGroup(beforeCache, ".text$lp00mybinary.dll_pri7", 100, 100, 100, 100, DataSectionFlags.MemoryExecute | DataSectionFlags.MemoryRead);
        beforeTextSection.AddCOFFGroup(beforeTextPri7CG);
        beforeTextPri7CG.MarkFullyConstructed();
        beforeTextSection.MarkFullyConstructed();

        using var afterCache = new SessionDataCache();
        var afterTextSection = new BinarySection(afterCache, ".text", 100, 100, 100, 100, 100, DataSectionFlags.MemoryExecute | DataSectionFlags.MemoryRead);
        var afterTextPri7CG = new COFFGroup(afterCache, ".text$lp00mybinary.dll_pri7", 100, 100, 100, 100, DataSectionFlags.MemoryExecute | DataSectionFlags.MemoryRead);
        afterTextSection.AddCOFFGroup(afterTextPri7CG);
        afterTextPri7CG.MarkFullyConstructed();
        afterTextSection.MarkFullyConstructed();

        using var diffCache = new DiffSessionDataCache();
        var textSectionDiff = new BinarySectionDiff(beforeTextSection, afterTextSection, diffCache);

        var textPri7CGDiff = textSectionDiff.COFFGroupDiffs.Single(cgDiff => cgDiff.Name == ".text$lp00mybinary.dll_pri7");

        Assert.AreEqual("Code executed during Pri7 PGO training", COFFGroupToDescriptionConverter.Instance.Convert(textPri7CGDiff, typeof(string), null, null));
    }

    [TestMethod]
    public void NullCOFFGroupIsEmptyString()
    {
        Assert.AreEqual(String.Empty, COFFGroupToDescriptionConverter.Instance.Convert(null, typeof(string), null, null));
    }
}
