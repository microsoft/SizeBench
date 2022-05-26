using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.pdb")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.pdb")]
[TestClass]
public sealed class DiffSession_EnumerateSymbolsInLibTests
{
    public TestContext? TestContext { get; set; }
    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

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
        var libDiffs = await diffSession.EnumerateLibDiffs(this.CancellationToken);
        foreach (var libDiff in libDiffs)
        {
            var symbolDiffs = await diffSession.EnumerateSymbolDiffsInLibDiff(libDiff, this.CancellationToken);

            foreach (var symbol in symbolDiffs)
            {
                Assert.AreEqual(0, symbol.SizeDiff);
            }
        }
    }

    [TestMethod]
    public async Task SymbolDiffsCanBeEnumeratedWithinALib()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var libDiffs = await diffSession.EnumerateLibDiffs(this.CancellationToken);
        var dllMainLibDiff = libDiffs.Single(ld => ld.ShortName == "dllmain");
        var symbolDiffs = await diffSession.EnumerateSymbolDiffsInLibDiff(dllMainLibDiff, this.CancellationToken);

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

        // Try a basic symbol like a function
        var DllMainDiff = symbolDiffs.Single(s => s.Name == "DllMain(HINSTANCE__*, unsigned long, void*)");
        Assert.AreEqual("int DllMain(HINSTANCE__* hModule, unsigned long ul_reason_for_call, void* lpReserved)", ((CodeBlockSymbolDiff)DllMainDiff).ParentFunctionDiff.FullName);
        Assert.IsNotNull(DllMainDiff.BeforeSymbol);
        Assert.IsNotNull(DllMainDiff.AfterSymbol);
        Assert.AreEqual(RealPETestingConstants.DllMainFunctionSymbolSizeAfter - RealPETestingConstants.DllMainFunctionSymbolSizeBefore, DllMainDiff.SizeDiff);

        // Try something in .bss to make sure VirtualSize didn't mess us up
        var intArrayInBssDiff = symbolDiffs.Single(s => s.Name == "intArrayInBss");
        Assert.IsNull(intArrayInBssDiff.BeforeSymbol);
        Assert.IsNotNull(intArrayInBssDiff.AfterSymbol);
        Assert.AreEqual(0, intArrayInBssDiff.SizeDiff);
        Assert.AreEqual(RealPETestingConstants.intArrayInBssVirtualSizeAfter - RealPETestingConstants.intArrayInBssVirtualSizeBefore, intArrayInBssDiff.VirtualSizeDiff);

        // Try something in .pdata since PDATA is parsed specially
        var pdataForDllMainDiff = symbolDiffs.Single(s => s.Name.StartsWith("[pdata] DllMain(", StringComparison.Ordinal));
        Assert.IsNotNull(pdataForDllMainDiff.BeforeSymbol);
        Assert.IsNotNull(pdataForDllMainDiff.AfterSymbol);
        Assert.AreEqual(0, pdataForDllMainDiff.SizeDiff);

        // Try something in .text$x since funclets are kinda weird
        // There are no symbols in .text$x in 'before' so just grab the first one, since the names are so ugly and templated...
        var textXSymbolDiff = symbolDiffs.First(s => s.AfterSymbol!.RVA >= textXCGDiff.AfterCOFFGroup!.RVA &&
                                                            s.AfterSymbol.RVAEnd <= (textXCGDiff.AfterCOFFGroup.RVA + textXCGDiff.AfterCOFFGroup.Size));
        Assert.IsNull(textXSymbolDiff.BeforeSymbol);
        Assert.IsNotNull(textXSymbolDiff.AfterSymbol);
        Assert.IsTrue(textXSymbolDiff.SizeDiff > 0);

        // Try something in .xdata since XDATA is parsed specially
        // The xdata symbols have really ugly names due to templates, so just grabbing the one and only tryMap to make the test code
        // more readable.
        var xdataTryMapDiff = symbolDiffs.Single(s => s.Name.Contains("[tryMap]", StringComparison.Ordinal));
        Assert.IsNull(xdataTryMapDiff.BeforeSymbol);
        Assert.IsNotNull(xdataTryMapDiff.AfterSymbol);
        Assert.AreEqual(xdataTryMapSizeAfter - 0, xdataTryMapDiff.SizeDiff);
    }

    [TestMethod]
    public async Task SymbolDiffsCanBeEnumeratedWithinALibOnlyPresentInBefore()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var libDiffs = await diffSession.EnumerateLibDiffs(this.CancellationToken);
        var staticLib2 = libDiffs.Single(ld => ld.ShortName == "StaticLib2");
        var symbolDiffs = await diffSession.EnumerateSymbolDiffsInLibDiff(staticLib2, this.CancellationToken);

        Assert.IsTrue(symbolDiffs.Count > 0);

        foreach (var sym in symbolDiffs)
        {
            Assert.IsNotNull(sym.BeforeSymbol);
            Assert.IsNull(sym.AfterSymbol);
            Assert.IsTrue(sym.SizeDiff < 0 || (sym.SizeDiff == 0 && sym.VirtualSizeDiff < 0));
        }
    }

    [TestMethod]
    public async Task SymbolDiffsCanBeEnumeratedWithinALibOnlyPresentInAfter()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var libDiffs = await diffSession.EnumerateLibDiffs(this.CancellationToken);
        var staticLib3 = libDiffs.Single(ld => ld.ShortName == "StaticLib3");
        var symbolDiffs = await diffSession.EnumerateSymbolDiffsInLibDiff(staticLib3, this.CancellationToken);

        Assert.IsTrue(symbolDiffs.Count > 0);

        foreach (var sym in symbolDiffs)
        {
            Assert.IsNull(sym.BeforeSymbol);
            Assert.IsNotNull(sym.AfterSymbol);
            Assert.IsTrue(sym.SizeDiff > 0 || (sym.SizeDiff == 0 && sym.VirtualSizeDiff > 0));
        }
    }
}
