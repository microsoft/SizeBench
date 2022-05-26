using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.pdb")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.pdb")]
[TestClass]
public sealed class DiffSession_EnumerateSectionsAndCOFFGroupsTests
{
    public TestContext? TestContext { get; set; }

    private string BeforeBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsBefore.dll");

    private string BeforePDBPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsBefore.pdb");

    private string AfterBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsAfter.dll");

    private string AfterPDBPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsAfter.pdb");

    [TestMethod]
    public async Task DiffWithSelfHasZeroSizeDiff()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.BeforeBinaryPath, this.BeforePDBPath,
                                                               logger);
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(CancellationToken.None);
        Assert.IsNotNull(sectionDiffs);

        Assert.AreEqual(7, sectionDiffs.Count);

        // Validating this against known good output from "link /dump /headers CppTestCases_BasicDiffObjectsBefore.dll"
        var textSectionDiff = (from s in sectionDiffs where s.Name == ".text" select s).FirstOrDefault();
        var rdataSectionDiff = (from s in sectionDiffs where s.Name == ".rdata" select s).FirstOrDefault();
        var dataSectionDiff = (from s in sectionDiffs where s.Name == ".data" select s).FirstOrDefault();
        var relocSectionDiff = (from s in sectionDiffs where s.Name == ".reloc" select s).FirstOrDefault();
        var pdataSectionDiff = (from s in sectionDiffs where s.Name == ".pdata" select s).FirstOrDefault();
        var gfidsSectionDiff = (from s in sectionDiffs where s.Name == ".gfids" select s).FirstOrDefault();
        var rsrcSectionDiff = (from s in sectionDiffs where s.Name == ".rsrc" select s).FirstOrDefault();

        Assert.IsNotNull(textSectionDiff);
        Assert.IsNotNull(dataSectionDiff);
        Assert.IsNotNull(rdataSectionDiff);
        Assert.IsNotNull(relocSectionDiff);
        Assert.IsNotNull(pdataSectionDiff);
        Assert.IsNotNull(gfidsSectionDiff);
        Assert.IsNotNull(rsrcSectionDiff);

        Assert.AreEqual(0, textSectionDiff.SizeDiff);
        Assert.AreEqual(0, textSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(3, textSectionDiff.COFFGroupDiffs.Count);
        foreach (var cg in textSectionDiff.COFFGroupDiffs)
        {
            Assert.AreEqual(0, cg.SizeDiff);
            Assert.AreEqual(0, cg.VirtualSizeDiff);
        }

        Assert.AreEqual(0, dataSectionDiff.SizeDiff);
        Assert.AreEqual(0, dataSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(2, dataSectionDiff.COFFGroupDiffs.Count);
        foreach (var cg in dataSectionDiff.COFFGroupDiffs)
        {
            Assert.AreEqual(0, cg.SizeDiff);
            Assert.AreEqual(0, cg.VirtualSizeDiff);
        }

        Assert.AreEqual(0, rdataSectionDiff.SizeDiff);
        Assert.AreEqual(0, rdataSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(21, rdataSectionDiff.COFFGroupDiffs.Count);
        foreach (var cg in rdataSectionDiff.COFFGroupDiffs)
        {
            Assert.AreEqual(0, cg.SizeDiff);
            Assert.AreEqual(0, cg.VirtualSizeDiff);
        }

        Assert.AreEqual(0, relocSectionDiff.SizeDiff);
        Assert.AreEqual(0, relocSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(0, relocSectionDiff.COFFGroupDiffs.Count);

        Assert.AreEqual(0, pdataSectionDiff.SizeDiff);
        Assert.AreEqual(0, pdataSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(1, pdataSectionDiff.COFFGroupDiffs.Count);
        foreach (var cg in pdataSectionDiff.COFFGroupDiffs)
        {
            Assert.AreEqual(0, cg.SizeDiff);
            Assert.AreEqual(0, cg.VirtualSizeDiff);
        }

        Assert.AreEqual(0, gfidsSectionDiff.SizeDiff);
        Assert.AreEqual(0, gfidsSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(1, gfidsSectionDiff.COFFGroupDiffs.Count);
        foreach (var cg in gfidsSectionDiff.COFFGroupDiffs)
        {
            Assert.AreEqual(0, cg.SizeDiff);
            Assert.AreEqual(0, cg.VirtualSizeDiff);
        }

        Assert.AreEqual(0, rsrcSectionDiff.SizeDiff);
        Assert.AreEqual(0, rsrcSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(2, rsrcSectionDiff.COFFGroupDiffs.Count);
        foreach (var cg in rsrcSectionDiff.COFFGroupDiffs)
        {
            Assert.AreEqual(0, cg.SizeDiff);
            Assert.AreEqual(0, cg.VirtualSizeDiff);
        }

        var textmnCOFFGroupDiff = (from cg in textSectionDiff.COFFGroupDiffs where cg.Name == ".text$mn" select cg).FirstOrDefault();
        var xdataCOFFGroupDiff = (from cg in rdataSectionDiff.COFFGroupDiffs where cg.Name == ".xdata" select cg).FirstOrDefault();
        var bssCOFFGroupDiff = (from cg in dataSectionDiff.COFFGroupDiffs where cg.Name == ".bss" select cg).FirstOrDefault();
        var crtXCACOFFGroupDiff = (from cg in rdataSectionDiff.COFFGroupDiffs where cg.Name == ".CRT$XCA" select cg).FirstOrDefault();
        var nonexistentCOFFGroupDiff = (from cg in rdataSectionDiff.COFFGroupDiffs where cg.Name == ".CRT$ZZZ" select cg).FirstOrDefault();

        Assert.IsNotNull(textmnCOFFGroupDiff);
        Assert.IsNotNull(xdataCOFFGroupDiff);
        Assert.IsNotNull(bssCOFFGroupDiff);
        Assert.IsNotNull(crtXCACOFFGroupDiff);
        Assert.IsNull(nonexistentCOFFGroupDiff); // Negative test case - let's make sure not everything matches

        // Validating this against known good output from "link /dump /headers /coffgroup CppTestCases_BasicDiffObjectsBefore.dll"
        Assert.AreEqual(0, textmnCOFFGroupDiff.SizeDiff);
        Assert.AreEqual(0, textmnCOFFGroupDiff.VirtualSizeDiff);
        Assert.AreEqual(".text", textmnCOFFGroupDiff.SectionDiff.Name);
        Assert.AreEqual(0, xdataCOFFGroupDiff.SizeDiff);
        Assert.AreEqual(0, xdataCOFFGroupDiff.VirtualSizeDiff);
        Assert.AreEqual(".rdata", xdataCOFFGroupDiff.SectionDiff.Name);
        Assert.AreEqual(0, bssCOFFGroupDiff.SizeDiff);
        Assert.AreEqual(0, bssCOFFGroupDiff.VirtualSizeDiff);
        Assert.AreEqual(0, bssCOFFGroupDiff.SectionDiff.SizeDiff);
        Assert.AreEqual(0, bssCOFFGroupDiff.SectionDiff.VirtualSizeDiff);
        Assert.AreEqual(".data", bssCOFFGroupDiff.SectionDiff.Name);
        Assert.AreEqual(0, crtXCACOFFGroupDiff.SizeDiff);
        Assert.AreEqual(0, crtXCACOFFGroupDiff.VirtualSizeDiff);
        Assert.AreEqual(".rdata", crtXCACOFFGroupDiff.SectionDiff.Name);
    }

    [TestMethod]
    public async Task SectionDiffsCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(CancellationToken.None);
        Assert.IsNotNull(sectionDiffs);

        var textSectionDiff = sectionDiffs.First(sd => sd.Name == ".text");
        var rdataSectionDiff = sectionDiffs.First(sd => sd.Name == ".rdata");
        var dataSectionDiff = sectionDiffs.First(sd => sd.Name == ".data");
        var relocSectionDiff = sectionDiffs.First(sd => sd.Name == ".reloc");
        var pdataSectionDiff = sectionDiffs.First(sd => sd.Name == ".pdata");
        var gfidsSectionDiff = sectionDiffs.First(sd => sd.Name == ".gfids");
        var rsrcSectionDiff = sectionDiffs.First(sd => sd.Name == ".rsrc");

        Assert.AreEqual(RealPETestingConstants.AfterTextSize - RealPETestingConstants.BeforeTextSize, textSectionDiff.SizeDiff);
        Assert.AreEqual(RealPETestingConstants.AfterTextVirtualSize - RealPETestingConstants.BeforeTextVirtualSize, textSectionDiff.VirtualSizeDiff);

        Assert.AreEqual(RealPETestingConstants.AfterRdataSize - RealPETestingConstants.BeforeRdataSize, rdataSectionDiff.SizeDiff);
        Assert.AreEqual(RealPETestingConstants.AfterRdataVirtualSize - RealPETestingConstants.BeforeRdataVirtualSize, rdataSectionDiff.VirtualSizeDiff);

        Assert.AreEqual(RealPETestingConstants.AfterDataSize - RealPETestingConstants.BeforeDataSize, dataSectionDiff.SizeDiff);
        Assert.AreEqual(RealPETestingConstants.AfterDataVirtualSize - RealPETestingConstants.BeforeDataVirtualSize, dataSectionDiff.VirtualSizeDiff);

        Assert.AreEqual(RealPETestingConstants.AfterPDataSize - RealPETestingConstants.BeforePDataSize, pdataSectionDiff.SizeDiff);
        Assert.AreEqual(RealPETestingConstants.AfterPDataVirtualSize - RealPETestingConstants.BeforePDataVirtualSize, pdataSectionDiff.VirtualSizeDiff);

        Assert.AreEqual(RealPETestingConstants.AfterGfidsSize - RealPETestingConstants.BeforeGfidsSize, gfidsSectionDiff.SizeDiff);
        Assert.AreEqual(RealPETestingConstants.AfterGfidsVirtualSize - RealPETestingConstants.BeforeGfidsVirtualSize, gfidsSectionDiff.VirtualSizeDiff);

        Assert.AreEqual(RealPETestingConstants.AfterRsrcSize - RealPETestingConstants.BeforeRsrcSize, rsrcSectionDiff.SizeDiff);
        Assert.AreEqual(RealPETestingConstants.AfterRsrcVirtualSize - RealPETestingConstants.BeforeRsrcVirtualSize, rsrcSectionDiff.VirtualSizeDiff);

        Assert.AreEqual(RealPETestingConstants.AfterRelocSize - RealPETestingConstants.BeforeRelocSize, relocSectionDiff.SizeDiff);
        Assert.AreEqual(RealPETestingConstants.AfterRelocVirtualSize - RealPETestingConstants.BeforeRelocVirtualSize, relocSectionDiff.VirtualSizeDiff);

        var textmnCOFFGroupDiff = textSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".text$mn");
        var textxCOFFGroupDiff = textSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".text$x");
        var idata5COFFGroupDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".idata$5");
        var rdataCOFFGroupDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".rdata");
        var xdataCOFFGroupDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".xdata");
        var pdataCOFFGroupDiff = pdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".pdata");
        var bssCOFFGroupDiff = dataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".bss");
        var crtXCACOFFGroupDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".CRT$XCA");
        var nonexistentCOFFGroupDiff = rdataSectionDiff.COFFGroupDiffs.FirstOrDefault(cgd => cgd.Name == ".CRT$ZZZ");

        Assert.IsNull(nonexistentCOFFGroupDiff); // Negative test case - let's make sure not everything matches

        Assert.AreEqual(RealPETestingConstants.AfterTextMnSize - RealPETestingConstants.BeforeTextMnSize, textmnCOFFGroupDiff.SizeDiff);
        Assert.AreEqual(".text", textmnCOFFGroupDiff.SectionDiff.Name);

        Assert.AreEqual(RealPETestingConstants.AfterTextXSize - RealPETestingConstants.BeforeTextXSize, textxCOFFGroupDiff.SizeDiff);
        Assert.AreEqual(".text", textxCOFFGroupDiff.SectionDiff.Name);

        Assert.AreEqual(RealPETestingConstants.AfterIdata5Size - RealPETestingConstants.BeforeIdata5Size, idata5COFFGroupDiff.SizeDiff);
        Assert.AreEqual(".rdata", idata5COFFGroupDiff.SectionDiff.Name);

        Assert.AreEqual(RealPETestingConstants.AfterRdataCGSize - RealPETestingConstants.BeforeRdataCGSize, rdataCOFFGroupDiff.SizeDiff);
        Assert.AreEqual(".rdata", rdataCOFFGroupDiff.SectionDiff.Name);

        Assert.AreEqual(RealPETestingConstants.AfterXdataSize - RealPETestingConstants.BeforeXdataSize, xdataCOFFGroupDiff.SizeDiff);
        Assert.AreEqual(".rdata", xdataCOFFGroupDiff.SectionDiff.Name);

        Assert.AreEqual(RealPETestingConstants.AfterBssVirtualSize - RealPETestingConstants.BeforeBssVirtualSize, bssCOFFGroupDiff.VirtualSizeDiff);
        Assert.AreEqual(0, bssCOFFGroupDiff.SizeDiff);
        Assert.AreEqual(".data", bssCOFFGroupDiff.SectionDiff.Name);

        Assert.AreEqual(RealPETestingConstants.AfterPDataCOFFGroupSize - RealPETestingConstants.BeforePDataCOFFGroupSize, pdataCOFFGroupDiff.SizeDiff);
        Assert.AreEqual(".pdata", pdataCOFFGroupDiff.SectionDiff.Name);
    }
}
