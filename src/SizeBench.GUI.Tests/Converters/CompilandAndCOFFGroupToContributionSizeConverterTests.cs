using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine;
using SizeBench.TestDataCommon;

namespace SizeBench.GUI.Converters.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public sealed class CompilandAndCOFFGroupToContributionSizeConverterTests : IDisposable
{
    Library TestLib;
    Compiland TestCompiland1;
    Compiland TestCompiland2;
    COFFGroup TestCOFFGroup1;
    COFFGroup TestCOFFGroup2;
    SessionDataCache DataCache;

    [TestInitialize]
    public void TestInitialize()
    {
        this.DataCache = new SessionDataCache();

        var textSection = new BinarySection(this.DataCache, ".text", size: 0x1000, virtualSize: 0, rva: 0x500, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        this.TestCOFFGroup1 = new COFFGroup(this.DataCache, ".text$zz", size: 0x1000, rva: 0x500, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute)
        {
            Section = textSection
        };

        textSection.AddCOFFGroup(this.TestCOFFGroup1);
        this.TestCOFFGroup1.MarkFullyConstructed();
        textSection.MarkFullyConstructed();

        var rdataSection = new BinarySection(this.DataCache, ".rdata", size: 0x300, virtualSize: 0, rva: 0x0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead);
        this.TestCOFFGroup2 = new COFFGroup(this.DataCache, ".rdata$zz", size: 0x300, rva: 0x0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead)
        {
            Section = rdataSection
        };

        rdataSection.AddCOFFGroup(this.TestCOFFGroup2);
        this.TestCOFFGroup2.MarkFullyConstructed();
        rdataSection.MarkFullyConstructed();

        this.TestLib = new Library(@"c:\foo\blah.lib");

        this.TestCompiland1 = new Compiland(this.DataCache, @"c:\foo\bar.obj", this.TestLib, CommonCommandLines.NullCommandLine, compilandSymIndex: 1);
        var cg1Contrib = this.TestCompiland1.GetOrCreateCOFFGroupContribution(this.TestCOFFGroup1);
        // TestCompiland1.COFFGroupContributions[COFFGroup1].Size == 0x15 == 21
        cg1Contrib.AddRVARange(RVARange.FromRVAAndSize(this.TestCOFFGroup1.RVA, 0x10));
        cg1Contrib.AddRVARange(RVARange.FromRVAAndSize(this.TestCOFFGroup1.RVA + 0x10, 0x5));
        cg1Contrib.MarkFullyConstructed();
        var sectionContrib = this.TestCompiland1.GetOrCreateSectionContribution(this.TestCOFFGroup1.Section);
        sectionContrib.AddRVARanges(cg1Contrib.RVARanges);
        sectionContrib.MarkFullyConstructed();
        var cg2Contrib = this.TestCompiland1.GetOrCreateCOFFGroupContribution(this.TestCOFFGroup2);
        // TestCompiland1.COFFGroupContributions[COFFGroup2].Size == 0x100 == 256
        cg2Contrib.AddRVARange(RVARange.FromRVAAndSize(this.TestCOFFGroup2.RVA, 0x100));
        cg2Contrib.MarkFullyConstructed();
        sectionContrib = this.TestCompiland1.GetOrCreateSectionContribution(this.TestCOFFGroup2.Section);
        sectionContrib.AddRVARanges(cg2Contrib.RVARanges);
        sectionContrib.MarkFullyConstructed();
        this.TestCompiland1.MarkFullyConstructed();

        this.TestCompiland2 = new Compiland(this.DataCache, @"c:\foo\baz.obj", this.TestLib, CommonCommandLines.NullCommandLine, compilandSymIndex: 2);
        cg1Contrib = this.TestCompiland2.GetOrCreateCOFFGroupContribution(this.TestCOFFGroup1);
        // TestCompiland2.COFFGroupContributions[COFFGroup2].Size == 0x22 == 34
        cg1Contrib.AddRVARange(RVARange.FromRVAAndSize(this.TestCOFFGroup1.RVA + 0x10 + 0x5, 0x22));
        cg1Contrib.MarkFullyConstructed();
        sectionContrib = this.TestCompiland2.GetOrCreateSectionContribution(this.TestCOFFGroup1.Section);
        sectionContrib.AddRVARanges(cg1Contrib.RVARanges);
        sectionContrib.MarkFullyConstructed();
        this.TestCompiland2.MarkFullyConstructed();
    }

    [ExpectedException(typeof(ArgumentException), AllowDerivedTypes = false)]
    [TestMethod]
    public void ConvertOnlyTakesCompilandAndCOFFGroupValuesInThatOrder()
        => CompilandAndCOFFGroupToContributionSizeConverter.Instance.Convert(new object[] { this.TestCOFFGroup1, this.TestCompiland1 }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */);

    [TestMethod]
    public void ReturnsCorrectSizeWhenContributionExists()
    {
        Assert.AreEqual("21 bytes", CompilandAndCOFFGroupToContributionSizeConverter.Instance.Convert(new object[] { this.TestCompiland1, this.TestCOFFGroup1 }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */));
        Assert.AreEqual("256 bytes", CompilandAndCOFFGroupToContributionSizeConverter.Instance.Convert(new object[] { this.TestCompiland1, this.TestCOFFGroup2 }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */));
    }

    [TestMethod]
    public void ReturnsZeroWhenNoContributionExists()
        => Assert.AreEqual("0 bytes", CompilandAndCOFFGroupToContributionSizeConverter.Instance.Convert(new object[] { this.TestCompiland2, this.TestCOFFGroup2 }, typeof(string), null /* ConverterParameter */, null /* CultureInfo */));

    public void Dispose() => this.DataCache.Dispose();
}
