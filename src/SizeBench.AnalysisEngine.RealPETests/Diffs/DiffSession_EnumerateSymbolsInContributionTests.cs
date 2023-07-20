using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.pdb")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.pdb")]
[TestClass]
public sealed class DiffSession_EnumerateSymbolsInContributionTests
{
    public TestContext? TestContext { get; set; }
    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

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
        var libDiffs = await diffSession.EnumerateLibDiffs(this.CancellationToken);
        foreach (var libDiff in libDiffs)
        {
            foreach (var sectionContribDiff in libDiff.SectionContributionDiffs.Values)
            {
                var symbolDiffs = await diffSession.EnumerateSymbolDiffsInContributionDiff(sectionContribDiff, this.CancellationToken);

                foreach (var symbol in symbolDiffs)
                {
                    Assert.AreEqual(0, symbol.SizeDiff);
                }
            }
        }
    }

    [TestMethod]
    public async Task SymbolDiffsCanBeEnumeratedWithinAContribution()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var compilandDiffs = await diffSession.EnumerateCompilandDiffs(this.CancellationToken);
        var dllMainCompilandDiff = compilandDiffs.Single(cd => cd.ShortName == "dllmain.obj");
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(this.CancellationToken);

        var textSectionDiff = sectionDiffs.Single(sd => sd.Name == ".text");
        var rdataSectionDiff = sectionDiffs.Single(sd => sd.Name == ".rdata");
        var dataSectionDiff = sectionDiffs.Single(sd => sd.Name == ".data");
        var pdataSectionDiff = sectionDiffs.Single(sd => sd.Name == ".pdata");

        var bssCGDiff = dataSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".bss");
        var textMnCGDiff = textSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".text$mn");
        var textXCGDiff = textSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".text$x");
        var xdataCGDiff = rdataSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".xdata");

        const int xdataTryMapSizeAfter = 20;

        // Try a basic case in a contribution with Size (not VirtualSize)
        var symbolDiffs = await diffSession.EnumerateSymbolDiffsInContributionDiff(dllMainCompilandDiff.COFFGroupContributionDiffs[textMnCGDiff], this.CancellationToken);
        Assert.AreEqual(6, symbolDiffs.Count);

        var DllMainDiff = symbolDiffs.Single(s => s.Name == "DllMain(HINSTANCE__*, unsigned long, void*)");
        Assert.AreEqual("int DllMain(HINSTANCE__* hModule, unsigned long ul_reason_for_call, void* lpReserved)", ((CodeBlockSymbolDiff)DllMainDiff).ParentFunctionDiff.FullName);
        Assert.IsNotNull(DllMainDiff.BeforeSymbol);
        Assert.IsNotNull(DllMainDiff.AfterSymbol);
        Assert.AreEqual(RealPETestingConstants.DllMainFunctionSymbolSizeAfter - RealPETestingConstants.DllMainFunctionSymbolSizeBefore, DllMainDiff.SizeDiff);

        // Try something in .bss to make sure VirtualSize didn't mess us up
        symbolDiffs = await diffSession.EnumerateSymbolDiffsInContributionDiff(dllMainCompilandDiff.COFFGroupContributionDiffs[bssCGDiff], this.CancellationToken);
        Assert.AreEqual(2, symbolDiffs.Count);

        var intArrayInBssDiff = symbolDiffs.Single(s => s.Name == "intArrayInBss");
        Assert.IsNull(intArrayInBssDiff.BeforeSymbol);
        Assert.IsNotNull(intArrayInBssDiff.AfterSymbol);
        Assert.AreEqual(0, intArrayInBssDiff.SizeDiff);
        Assert.AreEqual(RealPETestingConstants.intArrayInBssVirtualSizeAfter - RealPETestingConstants.intArrayInBssVirtualSizeBefore, intArrayInBssDiff.VirtualSizeDiff);

        // Try something in .pdata since PDATA is parsed specially
        symbolDiffs = await diffSession.EnumerateSymbolDiffsInContributionDiff(dllMainCompilandDiff.SectionContributionDiffs[pdataSectionDiff], this.CancellationToken);
        Assert.AreEqual(6, symbolDiffs.Count);

        var pdataForDllMainDiff = symbolDiffs.Single(s => s.Name.StartsWith("[pdata] DllMain(", StringComparison.Ordinal));
        Assert.IsNotNull(pdataForDllMainDiff.BeforeSymbol);
        Assert.IsNotNull(pdataForDllMainDiff.AfterSymbol);
        Assert.AreEqual(0, pdataForDllMainDiff.SizeDiff);

        // Try something in .xdata since XDATA is parsed specially
        // The xdata symbols have really ugly names due to templates, so just grabbing the one and only tryMap to make the test code
        // more readable.
        symbolDiffs = await diffSession.EnumerateSymbolDiffsInContributionDiff(dllMainCompilandDiff.COFFGroupContributionDiffs[xdataCGDiff], this.CancellationToken);

        var xdataTryMapDiff = symbolDiffs.Single(s => s.Name.Contains("[tryMap]", StringComparison.Ordinal));
        Assert.IsNull(xdataTryMapDiff.BeforeSymbol);
        Assert.IsNotNull(xdataTryMapDiff.AfterSymbol);
        Assert.AreEqual(xdataTryMapSizeAfter - 0, xdataTryMapDiff.SizeDiff);
    }

    [TestMethod]
    public async Task SymbolDiffsCanBeEnumeratedWithinAContributionOnlyPresentInBefore()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var compilandDiffs = await diffSession.EnumerateCompilandDiffs(this.CancellationToken);
        var sourceFile2CompilandDiff = compilandDiffs.Single(cd => cd.ShortName == "SourceFile2.obj");
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(this.CancellationToken);

        var rdataSectionDiff = sectionDiffs.Single(sd => sd.Name == ".rdata");

        var symbolDiffs = await diffSession.EnumerateSymbolDiffsInContributionDiff(sourceFile2CompilandDiff.SectionContributionDiffs[rdataSectionDiff], this.CancellationToken);

        Assert.AreEqual(1, symbolDiffs.Count);
        Assert.IsTrue(symbolDiffs[0].Name.Contains("dummy print from source file 2", StringComparison.Ordinal));

        foreach (var sym in symbolDiffs)
        {
            Assert.IsNotNull(sym.BeforeSymbol);
            Assert.IsNull(sym.AfterSymbol);
            Assert.IsTrue(sym.SizeDiff < 0);
        }
    }

    [TestMethod]
    public async Task SymbolDiffsCanBeEnumeratedWithinAContributionOnlyPresentInAfter()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var compilandDiffs = await diffSession.EnumerateCompilandDiffs(this.CancellationToken);
        var dllMainCompilandDiff = compilandDiffs.Single(cd => cd.ShortName == "dllmain.obj");
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(this.CancellationToken);

        var textSectionDiff = sectionDiffs.Single(sd => sd.Name == ".text");
        var textXCGDiff = textSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".text$x");

        var symbolDiffs = await diffSession.EnumerateSymbolDiffsInContributionDiff(dllMainCompilandDiff.COFFGroupContributionDiffs[textXCGDiff], this.CancellationToken);

        // map file shows 4 symbols, but it's only 3 because the "catch$8" and "[catch]" symbols are the same thing
        Assert.AreEqual(3, symbolDiffs.Count);

        foreach (var sym in symbolDiffs)
        {
            Assert.IsNull(sym.BeforeSymbol);
            Assert.IsNotNull(sym.AfterSymbol);
            Assert.IsTrue(sym.SizeDiff > 0);
        }
    }
}
