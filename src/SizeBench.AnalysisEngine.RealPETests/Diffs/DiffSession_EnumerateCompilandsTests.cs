using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.pdb")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.pdb")]
[TestClass]
public sealed class DiffSession_EnumerateCompilandsTests
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
        var compilandDiffs = await diffSession.EnumerateCompilandDiffs(CancellationToken.None);
        Assert.IsNotNull(compilandDiffs);

        var totalExpectedCompilands = RealPETestingConstants.ObjsInStaticLib1Before + RealPETestingConstants.ObjsInStaticLib2 +
                                      (RealPETestingConstants.CompilandsInEachImportLib * RealPETestingConstants.ImportLibsBefore) +
                                      RealPETestingConstants.ObjsInMSVCRTD + RealPETestingConstants.ObjsFromLinkerBefore +
                                      RealPETestingConstants.ObjsDirectlyInDLLBefore;

        Assert.AreEqual(totalExpectedCompilands, compilandDiffs.Count);

        foreach (var compilandDiff in compilandDiffs)
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

    [TestMethod]
    public async Task CompilandDiffsWorkForPDataAndXData()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var compilandDiffs = await diffSession.EnumerateCompilandDiffs(CancellationToken.None);
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(CancellationToken.None);
        Assert.IsNotNull(compilandDiffs);

        var pdataSection = sectionDiffs.First(sd => sd.Name == ".pdata");
        var xdataCG = sectionDiffs.First(sd => sd.Name == ".rdata").COFFGroupDiffs.First(cgd => cgd.Name == ".xdata");

        var sourceFile1StaticLib1 = compilandDiffs.First(cd => cd.ShortName == "SourceFile1.obj" && cd.LibDiff.ShortName == "StaticLib1");
        var sourceFile1StaticLib2 = compilandDiffs.First(cd => cd.ShortName == "SourceFile1.obj" && cd.LibDiff.ShortName == "StaticLib2");
        var sourceFile1StaticLib3 = compilandDiffs.First(cd => cd.ShortName == "SourceFile1.obj" && cd.LibDiff.ShortName == "StaticLib3");

        // StaticLib1 for now is the same, so everything should be 0, but this assumption will probably change as
        // lib diffing tests are brought online more, don't be afraid to change these assertions.
        Assert.AreEqual(0, sourceFile1StaticLib1.SectionContributionDiffs[pdataSection].SizeDiff);
        Assert.AreEqual(0, sourceFile1StaticLib1.SectionContributionDiffsByName[".pdata"].SizeDiff);
        Assert.AreEqual(0, sourceFile1StaticLib1.COFFGroupContributionDiffs[xdataCG].SizeDiff);
        Assert.AreEqual(0, sourceFile1StaticLib1.COFFGroupContributionDiffsByName[".xdata"].SizeDiff);

        // StaticLib2 is deleted in 'after' so we should see a reduction in size
        Assert.AreEqual(-12, sourceFile1StaticLib2.SectionContributionDiffs[pdataSection].SizeDiff);
        Assert.AreEqual(-12, sourceFile1StaticLib2.SectionContributionDiffsByName[".pdata"].SizeDiff);
        Assert.AreEqual(-8, sourceFile1StaticLib2.COFFGroupContributionDiffs[xdataCG].SizeDiff);
        Assert.AreEqual(-8, sourceFile1StaticLib2.COFFGroupContributionDiffsByName[".xdata"].SizeDiff);

        // StaticLib3 is added in 'after' so we should see an increase in size
        Assert.AreEqual(12, sourceFile1StaticLib3.SectionContributionDiffs[pdataSection].SizeDiff);
        Assert.AreEqual(12, sourceFile1StaticLib3.SectionContributionDiffsByName[".pdata"].SizeDiff);
        Assert.AreEqual(8, sourceFile1StaticLib3.COFFGroupContributionDiffs[xdataCG].SizeDiff);
        Assert.AreEqual(8, sourceFile1StaticLib3.COFFGroupContributionDiffsByName[".xdata"].SizeDiff);
    }

    [TestMethod]
    public async Task DiffsOfRegularCompilandsWorks()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var compilandDiffs = await diffSession.EnumerateCompilandDiffs(CancellationToken.None);
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(CancellationToken.None);
        Assert.IsNotNull(compilandDiffs);

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
        Assert.AreEqual(1, compilandDiffs.Where(cd => cd.ShortName == "dllmain.obj").Count());
        var dllMainCompilandDiff = compilandDiffs.First(cd => cd.ShortName == "dllmain.obj");

        Assert.IsNotNull(dllMainCompilandDiff.BeforeCompiland);
        Assert.IsNotNull(dllMainCompilandDiff.AfterCompiland);
        Assert.AreEqual("dllmain", dllMainCompilandDiff.LibDiff.ShortName);

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

        Assert.AreEqual(afterDllMainTextSize - beforeDllMainTextSize, dllMainCompilandDiff.SectionContributionDiffs[textSectionDiff].SizeDiff);
        Assert.AreEqual(afterDllMainRdataSize - beforeDllMainRdataSize, dllMainCompilandDiff.SectionContributionDiffs[rdataSectionDiff].SizeDiff);
        Assert.AreEqual(afterDllMainPdataSize - beforeDllMainPdataSize, dllMainCompilandDiff.SectionContributionDiffs[pdataSectionDiff].SizeDiff);

        Assert.AreEqual(afterDllMainTextMnSize - beforeDllMainTextMnSize, dllMainCompilandDiff.COFFGroupContributionDiffs[textMnCGDiff].SizeDiff);
        Assert.AreEqual(afterDllMainTextXSize - 0, dllMainCompilandDiff.COFFGroupContributionDiffs[textXCGDiff].SizeDiff);
        Assert.IsNull(dllMainCompilandDiff.COFFGroupContributionDiffs[textXCGDiff].BeforeCOFFGroupContribution);
        Assert.AreEqual(afterDllMainRdataCGSize - beforeDllMainRdataCGSize, dllMainCompilandDiff.COFFGroupContributionDiffs[rdataCGDiff].SizeDiff);
        Assert.AreEqual(afterDllMainPdataCGSize - beforeDllMainPdataCGSize, dllMainCompilandDiff.COFFGroupContributionDiffs[pdataCGDiff].SizeDiff);
        Assert.AreEqual(afterDllMainXdataSize - beforeDllMainXdataSize, dllMainCompilandDiff.COFFGroupContributionDiffs[xdataCGDiff].SizeDiff);
        Assert.AreEqual(afterDllMainBssSize - beforeDllMainBssSize, dllMainCompilandDiff.COFFGroupContributionDiffs[bssCGDiff].VirtualSizeDiff);
        Assert.AreEqual(0, dllMainCompilandDiff.COFFGroupContributionDiffs[bssCGDiff].SizeDiff);



        // Move on to SourceFile2.obj, which is only in 'before'
        Assert.AreEqual(1, compilandDiffs.Where(cd => cd.ShortName == "SourceFile2.obj" && cd.LibDiff.ShortName == "SourceFile2").Count());
        var sourceFile2InDLLCompilandDiff = compilandDiffs.First(cd => cd.ShortName == "SourceFile2.obj" && cd.LibDiff.ShortName == "SourceFile2");
        Assert.IsNotNull(sourceFile2InDLLCompilandDiff.BeforeCompiland);
        Assert.IsNull(sourceFile2InDLLCompilandDiff.AfterCompiland);

        const int sourceFile2RDataSize = 79;

        Assert.AreEqual(0 - sourceFile2RDataSize, sourceFile2InDLLCompilandDiff.SectionContributionDiffs[rdataSectionDiff].SizeDiff);
        Assert.AreEqual(0 - sourceFile2RDataSize, sourceFile2InDLLCompilandDiff.COFFGroupContributionDiffs[rdataCGDiff].SizeDiff);



        // And then hit SourceFile3.obj, which is only in 'after'
        Assert.AreEqual(1, compilandDiffs.Where(cd => cd.ShortName == "SourceFile3.obj" && cd.LibDiff.ShortName == "SourceFile3").Count());
        var sourceFile3InDLLCompilandDiff = compilandDiffs.First(cd => cd.ShortName == "SourceFile3.obj" && cd.LibDiff.ShortName == "SourceFile3");
        Assert.IsNull(sourceFile3InDLLCompilandDiff.BeforeCompiland);
        Assert.IsNotNull(sourceFile3InDLLCompilandDiff.AfterCompiland);

        const int sourceFile3RDataSize = 199;

        Assert.AreEqual(sourceFile3RDataSize, sourceFile3InDLLCompilandDiff.SectionContributionDiffs[rdataSectionDiff].SizeDiff);
        Assert.AreEqual(sourceFile3RDataSize, sourceFile3InDLLCompilandDiff.COFFGroupContributionDiffs[rdataCGDiff].SizeDiff);

        // Now look at static libs 1, 2, and 3
        Assert.AreEqual(1, compilandDiffs.Where(cd => cd.ShortName == "SourceFile1.obj" && cd.LibDiff.ShortName == "StaticLib1").Count());
        var sourceFile1StaticLib1Diff = compilandDiffs.First(cd => cd.ShortName == "SourceFile1.obj" && cd.LibDiff.ShortName == "StaticLib1");
        Assert.IsNotNull(sourceFile1StaticLib1Diff.BeforeCompiland);
        Assert.IsNotNull(sourceFile1StaticLib1Diff.AfterCompiland);

        Assert.AreEqual(1, compilandDiffs.Where(cd => cd.ShortName == "SourceFile1.obj" && cd.LibDiff.ShortName == "StaticLib2").Count());
        var sourceFile1StaticLib2Diff = compilandDiffs.First(cd => cd.ShortName == "SourceFile1.obj" && cd.LibDiff.ShortName == "StaticLib2");
        Assert.IsNotNull(sourceFile1StaticLib2Diff.BeforeCompiland);
        Assert.IsNull(sourceFile1StaticLib2Diff.AfterCompiland);

        Assert.AreEqual(1, compilandDiffs.Where(cd => cd.ShortName == "SourceFile1.obj" && cd.LibDiff.ShortName == "StaticLib3").Count());
        var sourceFile1StaticLib3Diff = compilandDiffs.First(cd => cd.ShortName == "SourceFile1.obj" && cd.LibDiff.ShortName == "StaticLib3");
        Assert.IsNull(sourceFile1StaticLib3Diff.BeforeCompiland);
        Assert.IsNotNull(sourceFile1StaticLib3Diff.AfterCompiland);
    }

}
