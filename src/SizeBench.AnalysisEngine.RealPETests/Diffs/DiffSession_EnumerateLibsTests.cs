using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.pdb")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.pdb")]
[TestClass]
public sealed class DiffSession_EnumerateLibsTests
{
    public TestContext? TestContext { get; set; }

    private string BeforeBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "CppTestCases_BasicDiffObjectsBefore.dll");

    private string BeforePDBPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "CppTestCases_BasicDiffObjectsBefore.pdb");

    private string AfterBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "CppTestCases_BasicDiffObjectsAfter.dll");

    private string AfterPDBPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "CppTestCases_BasicDiffObjectsAfter.pdb");

    [TestMethod]
    public async Task DiffWithSelfHasZeroSizeDiff()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.BeforeBinaryPath, this.BeforePDBPath,
                                                               logger);
        var libDiffs = await diffSession.EnumerateLibDiffs(CancellationToken.None);
        Assert.IsNotNull(libDiffs);

        Assert.AreEqual(RealPETestingConstants.LibsShared + RealPETestingConstants.LibsUniquelyInBefore +
                        RealPETestingConstants.ObjsDirectlyInDLLShared + RealPETestingConstants.ObjsDirectlyInDLLUniquelyInBefore, libDiffs.Count);

        foreach (var libDiff in libDiffs)
        {
            Assert.AreEqual(0, libDiff.SizeDiff);
            foreach (var sectionContrib in libDiff.SectionContributionDiffs.Values)
            {
                Assert.AreEqual(0, sectionContrib.SizeDiff);
            }

            foreach (var sectionContribByName in libDiff.SectionContributionDiffsByName.Values)
            {
                Assert.AreEqual(0, sectionContribByName.SizeDiff);
            }

            foreach (var coffGroupContrib in libDiff.COFFGroupContributionDiffs.Values)
            {
                Assert.AreEqual(0, coffGroupContrib.SizeDiff);
            }

            foreach (var coffGroupContribByName in libDiff.COFFGroupContributionDiffsByName.Values)
            {
                Assert.AreEqual(0, coffGroupContribByName.SizeDiff);
            }

            foreach (var compilandDiff in libDiff.CompilandDiffs.Values)
            {
                Assert.AreEqual(0, compilandDiff.SizeDiff);

                foreach (var sectionContrib in compilandDiff.SectionContributionDiffs.Values)
                {
                    Assert.AreEqual(0, sectionContrib.SizeDiff);
                }

                foreach (var sectionContribByName in compilandDiff.SectionContributionDiffsByName.Values)
                {
                    Assert.AreEqual(0, sectionContribByName.SizeDiff);
                }

                foreach (var coffGroupContrib in compilandDiff.COFFGroupContributionDiffs.Values)
                {
                    Assert.AreEqual(0, coffGroupContrib.SizeDiff);
                }

                foreach (var coffGroupContribByName in compilandDiff.COFFGroupContributionDiffsByName.Values)
                {
                    Assert.AreEqual(0, coffGroupContribByName.SizeDiff);
                }
            }
        }
    }

    [TestMethod]
    public async Task LibDiffsWorkForPDataAndXData()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var libDiffs = await diffSession.EnumerateLibDiffs(CancellationToken.None);
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(CancellationToken.None);
        Assert.IsNotNull(libDiffs);

        var pdataSection = sectionDiffs.First(sd => sd.Name == ".pdata");
        var xdataCG = sectionDiffs.First(sd => sd.Name == ".rdata").COFFGroupDiffs.First(cgd => cgd.Name == ".xdata");

        var staticLib1 = libDiffs.Single(ld => ld.ShortName == "StaticLib1");
        var staticLib2 = libDiffs.Single(ld => ld.ShortName == "StaticLib2");
        var staticLib3 = libDiffs.Single(ld => ld.ShortName == "StaticLib3");

        // StaticLib1 for now is the same, so everything should be 0, but this assumption will probably change as
        // lib diffing tests are brought online more, don't be afraid to change these assertions.
        Assert.AreEqual(0, staticLib1.SectionContributionDiffs[pdataSection].SizeDiff);
        Assert.AreEqual(0, staticLib1.SectionContributionDiffsByName[".pdata"].SizeDiff);
        Assert.AreEqual(0, staticLib1.COFFGroupContributionDiffs[xdataCG].SizeDiff);
        Assert.AreEqual(0, staticLib1.COFFGroupContributionDiffsByName[".xdata"].SizeDiff);

        // StaticLib2 is deleted in 'after' so we should see a reduction in size
        Assert.AreEqual(-12, staticLib2.SectionContributionDiffs[pdataSection].SizeDiff);
        Assert.AreEqual(-12, staticLib2.SectionContributionDiffsByName[".pdata"].SizeDiff);
        Assert.AreEqual(-8, staticLib2.COFFGroupContributionDiffs[xdataCG].SizeDiff);
        Assert.AreEqual(-8, staticLib2.COFFGroupContributionDiffsByName[".xdata"].SizeDiff);

        // StaticLib3 is added in 'after' so we should see an increase in size
        Assert.AreEqual(12, staticLib3.SectionContributionDiffs[pdataSection].SizeDiff);
        Assert.AreEqual(12, staticLib3.SectionContributionDiffsByName[".pdata"].SizeDiff);
        Assert.AreEqual(8, staticLib3.COFFGroupContributionDiffs[xdataCG].SizeDiff);
        Assert.AreEqual(8, staticLib3.COFFGroupContributionDiffsByName[".xdata"].SizeDiff);
    }

    [TestMethod]
    public async Task ImportLibDiffsCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var libDiffs = await diffSession.EnumerateLibDiffs(CancellationToken.None);
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(CancellationToken.None);
        Assert.IsNotNull(libDiffs);

        Assert.AreEqual(RealPETestingConstants.LibsShared + RealPETestingConstants.LibsUniquelyInBefore + RealPETestingConstants.LibsUniquelyInAfter +
                        RealPETestingConstants.ObjsDirectlyInDLLShared + RealPETestingConstants.ObjsDirectlyInDLLUniquelyInBefore + RealPETestingConstants.ObjsDirectlyInDLLUniquelyInAfter, libDiffs.Count);

        var textSectionDiff = sectionDiffs.First(sd => sd.Name == ".text");
        var textMnCGDiff = textSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".text$mn");

        var rdataSectionDiff = sectionDiffs.First(sd => sd.Name == ".rdata");
        var xdataCGDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".xdata");
        var idata2CGDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".idata$2");
        var idata3CGDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".idata$3");
        var idata4CGDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".idata$4");
        var idata5CGDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".idata$5");
        var idata6CGDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".idata$6");

        var pdataSectionDiff = sectionDiffs.First(sd => sd.Name == ".pdata");

        // Check that import libs diff correctly, vcruntimed happens to be very convenient since it hits most bases
        var vcruntimedLibDiff = libDiffs.First(ld => ld.ShortName == "vcruntimed");

        // Negative tests
        Assert.IsFalse(vcruntimedLibDiff.SectionContributionDiffs.ContainsKey(pdataSectionDiff));
        Assert.IsFalse(vcruntimedLibDiff.SectionContributionDiffsByName.ContainsKey(".pdata"));
        Assert.IsFalse(vcruntimedLibDiff.COFFGroupContributionDiffs.ContainsKey(xdataCGDiff));
        Assert.IsFalse(vcruntimedLibDiff.COFFGroupContributionDiffsByName.ContainsKey(".xdata"));

        const int beforeVCruntimedTextSize = 12;
        const int beforeVCruntimedTextMnSize = 12;
        const int beforeVCruntimedRdataSize = 162;
        const int beforeVCruntimedIdata3Size = 20;
        const int beforeVCruntimedIdata4Size = 24;
        const int beforeVCruntimedIdata5Size = 24;
        const int beforeVCruntimedIdata6Size = 74;

        const int afterVCruntimedTextSize = 24;
        const int afterVCruntimedTextMnSize = 24;
        const int afterVCruntimedRdataSize = 214;
        const int afterVCruntimedIdata4Size = 40;
        const int afterVCruntimedIdata5Size = 40;
        const int afterVCruntimedIdata6Size = 114;

        Assert.AreEqual(afterVCruntimedTextSize - beforeVCruntimedTextSize, vcruntimedLibDiff.SectionContributionDiffsByName[".text"].SizeDiff);
        Assert.AreEqual(afterVCruntimedTextMnSize - beforeVCruntimedTextMnSize, vcruntimedLibDiff.COFFGroupContributionDiffsByName[".text$mn"].SizeDiff);

        Assert.AreEqual(afterVCruntimedRdataSize - beforeVCruntimedRdataSize, vcruntimedLibDiff.SectionContributionDiffsByName[".rdata"].SizeDiff);
        Assert.AreEqual(0, vcruntimedLibDiff.COFFGroupContributionDiffsByName[".idata$2"].SizeDiff);
        Assert.AreEqual(0 - beforeVCruntimedIdata3Size, vcruntimedLibDiff.COFFGroupContributionDiffsByName[".idata$3"].SizeDiff);
        Assert.IsNull(vcruntimedLibDiff.COFFGroupContributionDiffsByName[".idata$3"].AfterContribution);
        Assert.AreEqual(afterVCruntimedIdata4Size - beforeVCruntimedIdata4Size, vcruntimedLibDiff.COFFGroupContributionDiffsByName[".idata$4"].SizeDiff);
        Assert.AreEqual(afterVCruntimedIdata5Size - beforeVCruntimedIdata5Size, vcruntimedLibDiff.COFFGroupContributionDiffsByName[".idata$5"].SizeDiff);
        Assert.AreEqual(afterVCruntimedIdata6Size - beforeVCruntimedIdata6Size, vcruntimedLibDiff.COFFGroupContributionDiffsByName[".idata$6"].SizeDiff);

        // Now check an import lib with no diff in size
        var kernel32LibDiff = libDiffs.First(ld => ld.ShortName == "kernel32");
        Assert.AreEqual(0, kernel32LibDiff.SizeDiff);
        foreach (var sectionContrib in kernel32LibDiff.SectionContributionDiffs.Values)
        {
            Assert.AreEqual(0, sectionContrib.SizeDiff);
        }

        foreach (var sectionContrib in kernel32LibDiff.SectionContributionDiffsByName.Values)
        {
            Assert.AreEqual(0, sectionContrib.SizeDiff);
        }

        foreach (var cgContrib in kernel32LibDiff.COFFGroupContributionDiffs.Values)
        {
            Assert.AreEqual(0, cgContrib.SizeDiff);
        }

        foreach (var cgContrib in kernel32LibDiff.COFFGroupContributionDiffsByName.Values)
        {
            Assert.AreEqual(0, cgContrib.SizeDiff);
        }

        // And lastly, check an import lib that is only in 'after' - currently there are none only in 'before' so not testing that, though
        // ideally we would.  Just too lazy to add that test at the moment.
        var msvcprtdLibDiff = libDiffs.First(ld => ld.ShortName == "msvcprtd");
        Assert.IsNull(msvcprtdLibDiff.BeforeLib);
        Assert.IsNotNull(msvcprtdLibDiff.AfterLib);
        Assert.AreEqual(608, msvcprtdLibDiff.SizeDiff);
        Assert.AreEqual(1, msvcprtdLibDiff.SectionContributionDiffs.Count);
        Assert.IsTrue(msvcprtdLibDiff.SectionContributionDiffs.ContainsKey(rdataSectionDiff));
    }

    [TestMethod]
    public async Task DiffsOfStaticLibsAndObjsReportedAsLibsCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var libDiffs = await diffSession.EnumerateLibDiffs(CancellationToken.None);
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(CancellationToken.None);
        Assert.IsNotNull(libDiffs);

        var textSectionDiff = sectionDiffs.First(sd => sd.Name == ".text");
        var textMnCGDiff = textSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".text$mn");
        var textXCGDiff = textSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".text$x");

        var rdataSectionDiff = sectionDiffs.First(sd => sd.Name == ".rdata");
        var rdataCGDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".rdata");
        var xdataCGDiff = rdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".xdata");

        var pdataSectionDiff = sectionDiffs.First(sd => sd.Name == ".pdata");
        var pdataCGDiff = pdataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".pdata");

        var dataSectionDiff = sectionDiffs.First(sd => sd.Name == ".data");
        var bssCGDiff = dataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".bss");

        // Start with dllmain.obj, which is detected as a lib since it's directly in the dll
        Assert.AreEqual(1, libDiffs.Where(ld => ld.ShortName == "dllmain").Count());
        var dllMainLibDiff = libDiffs.First(ld => ld.ShortName == "dllmain");

        Assert.AreEqual(1, dllMainLibDiff.CompilandDiffs.Count);
        Assert.AreEqual("dllmain.obj", dllMainLibDiff.CompilandDiffs.Values.First().ShortName);
        Assert.AreEqual(dllMainLibDiff.SizeDiff, dllMainLibDiff.CompilandDiffs.Values.First().SizeDiff);

        const int beforeDllMainTextSize = 216;
        const int beforeDllMainRdataSize = 67;
        const int beforeDllMainPdataSize = 24;
        const int beforeDllMainTextMnSize = 216;
        const int beforeDllMainRdataCGSize = 47;
        const int beforeDllMainPdataCGSize = 24;
        const int beforeDllMainXdataSize = 20;
        const int beforeDllMainBssSize = 8;

        const int afterDllMainTextSize = 879;
        const int afterDllMainRdataSize = 489;
        const int afterDllMainPdataSize = 72;
        const int afterDllMainTextMnSize = 799;
        const int afterDllMainTextXSize = 80;
        const int afterDllMainRdataCGSize = 181;
        const int afterDllMainPdataCGSize = 72;
        const int afterDllMainXdataSize = 308;
        const int afterDllMainBssSize = 20;

        Assert.AreEqual(afterDllMainTextSize - beforeDllMainTextSize, dllMainLibDiff.SectionContributionDiffs[textSectionDiff].SizeDiff);
        Assert.AreEqual(afterDllMainRdataSize - beforeDllMainRdataSize, dllMainLibDiff.SectionContributionDiffs[rdataSectionDiff].SizeDiff);
        Assert.AreEqual(afterDllMainPdataSize - beforeDllMainPdataSize, dllMainLibDiff.SectionContributionDiffs[pdataSectionDiff].SizeDiff);

        Assert.AreEqual(afterDllMainTextMnSize - beforeDllMainTextMnSize, dllMainLibDiff.COFFGroupContributionDiffs[textMnCGDiff].SizeDiff);
        Assert.AreEqual(afterDllMainTextXSize - 0, dllMainLibDiff.COFFGroupContributionDiffs[textXCGDiff].SizeDiff);
        Assert.IsNull(dllMainLibDiff.COFFGroupContributionDiffs[textXCGDiff].BeforeCOFFGroupContribution);
        Assert.AreEqual(afterDllMainRdataCGSize - beforeDllMainRdataCGSize, dllMainLibDiff.COFFGroupContributionDiffs[rdataCGDiff].SizeDiff);
        Assert.AreEqual(afterDllMainPdataCGSize - beforeDllMainPdataCGSize, dllMainLibDiff.COFFGroupContributionDiffs[pdataCGDiff].SizeDiff);
        Assert.AreEqual(afterDllMainXdataSize - beforeDllMainXdataSize, dllMainLibDiff.COFFGroupContributionDiffs[xdataCGDiff].SizeDiff);
        Assert.AreEqual(afterDllMainBssSize - beforeDllMainBssSize, dllMainLibDiff.COFFGroupContributionDiffs[bssCGDiff].VirtualSizeDiff);
        Assert.AreEqual(0, dllMainLibDiff.COFFGroupContributionDiffs[bssCGDiff].SizeDiff);



        // Move on to SourceFile2.obj, which is only in 'before'
        Assert.AreEqual(1, libDiffs.Where(ld => ld.ShortName == "SourceFile2").Count());
        var sourceFile2LibDiff = libDiffs.First(ld => ld.ShortName == "SourceFile2");
        Assert.IsNotNull(sourceFile2LibDiff.BeforeLib);
        Assert.IsNull(sourceFile2LibDiff.AfterLib);

        Assert.AreEqual(1, sourceFile2LibDiff.CompilandDiffs.Count);
        Assert.AreEqual("SourceFile2.obj", sourceFile2LibDiff.CompilandDiffs.Values.First().ShortName);
        Assert.AreEqual(sourceFile2LibDiff.SizeDiff, sourceFile2LibDiff.CompilandDiffs.Values.First().SizeDiff);

        const int sourceFile2RDataSize = 79;

        Assert.AreEqual(0 - sourceFile2RDataSize, sourceFile2LibDiff.SectionContributionDiffs[rdataSectionDiff].SizeDiff);
        Assert.AreEqual(0 - sourceFile2RDataSize, sourceFile2LibDiff.COFFGroupContributionDiffs[rdataCGDiff].SizeDiff);



        // And then hit SourceFile3.obj, which is only in 'after'
        Assert.AreEqual(1, libDiffs.Where(ld => ld.ShortName == "SourceFile3").Count());
        var sourceFile3LibDiff = libDiffs.First(ld => ld.ShortName == "SourceFile3");
        Assert.IsNull(sourceFile3LibDiff.BeforeLib);
        Assert.IsNotNull(sourceFile3LibDiff.AfterLib);

        Assert.AreEqual(1, sourceFile3LibDiff.CompilandDiffs.Count);
        Assert.AreEqual("SourceFile3.obj", sourceFile3LibDiff.CompilandDiffs.Values.First().ShortName);
        Assert.AreEqual(sourceFile3LibDiff.SizeDiff, sourceFile3LibDiff.CompilandDiffs.Values.First().SizeDiff);

        const int sourceFile3RDataSize = 199;

        Assert.AreEqual(sourceFile3RDataSize, sourceFile3LibDiff.SectionContributionDiffs[rdataSectionDiff].SizeDiff);
        Assert.AreEqual(sourceFile3RDataSize, sourceFile3LibDiff.COFFGroupContributionDiffs[rdataCGDiff].SizeDiff);

        // Now look at static libs 1, 2, and 3
        Assert.AreEqual(1, libDiffs.Where(ld => ld.ShortName == "StaticLib1").Count());
        var staticLib1LibDiff = libDiffs.First(ld => ld.ShortName == "StaticLib1");
        Assert.IsNotNull(staticLib1LibDiff.BeforeLib);
        Assert.IsNotNull(staticLib1LibDiff.AfterLib);

        Assert.AreEqual(1, libDiffs.Where(ld => ld.ShortName == "StaticLib2").Count());
        var staticLib2LibDiff = libDiffs.First(ld => ld.ShortName == "StaticLib2");
        Assert.IsNotNull(staticLib2LibDiff.BeforeLib);
        Assert.IsNull(staticLib2LibDiff.AfterLib);

        Assert.AreEqual(1, libDiffs.Where(ld => ld.ShortName == "StaticLib3").Count());
        var staticLib3LibDiff = libDiffs.First(ld => ld.ShortName == "StaticLib3");
        Assert.IsNull(staticLib3LibDiff.BeforeLib);
        Assert.IsNotNull(staticLib3LibDiff.AfterLib);
    }
}
