using System.IO;
using System.Runtime.InteropServices;
using SizeBench.AnalysisEngine.PE;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.pdb")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb")]
[TestClass]
public sealed class DiffSession_EnumerateSymbolsInCOFFGroupTests
{
    public TestContext? TestContext { get; set; }
    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

    public string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory, filename);

    private string BeforeBinaryPath => MakePath("CppTestCases_BasicDiffObjectsBefore.dll");

    private string BeforePDBPath => MakePath("CppTestCases_BasicDiffObjectsBefore.pdb");

    private string AfterBinaryPath => MakePath("CppTestCases_BasicDiffObjectsAfter.dll");

    private string AfterPDBPath => MakePath("CppTestCases_BasicDiffObjectsAfter.pdb");

    private static long RoundSizeUpTo8ByteAlignment(ISymbol sym) => RoundSizeUpToAlignment(sym.Size, 8);

    private static long RoundSizeUpToAlignment(uint size, uint alignment)
    {
        if (size % alignment == 0)
        {
            return size;
        }

        return size + (alignment - (size % alignment));
    }

    [TestMethod]
    public async Task DiffWithSelfHasZeroSizeDiff()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.BeforeBinaryPath, this.BeforePDBPath,
                                                               logger);
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(this.CancellationToken);

        foreach (var sectionDiff in sectionDiffs)
        {
            foreach (var coffGroupDiff in sectionDiff.COFFGroupDiffs)
            {
                var symbolDiffs = await diffSession.EnumerateSymbolDiffsInCOFFGroupDiff(coffGroupDiff, this.CancellationToken);

                foreach (var symbol in symbolDiffs)
                {
                    Assert.AreEqual(0, symbol.SizeDiff);
                }
            }
        }
    }

    [TestMethod]
    public async Task SymbolDiffsInRegularCOFFGroupCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var textSectionDiff = await diffSession.LoadBinarySectionDiffByName(".text", this.CancellationToken);
        Assert.IsNotNull(textSectionDiff);
        var symbolDiffs = await diffSession.EnumerateSymbolDiffsInCOFFGroupDiff(textSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".text$mn"), this.CancellationToken);

        const uint dllmain_crt_process_attachSize = 331;
        const int FunctionInStaticLib1SizeBefore = 50;
        const int FunctionInStaticLib1SizeAfter = 50;
        const int FunctionInStaticLib2Size = 50;
        const int FunctionInStaticLib3Size = 50;

        var dllmain_crt_process_attachDiff = symbolDiffs.Single(s => s.Name.StartsWith("dllmain_crt_process_attach", StringComparison.Ordinal));
        Assert.IsNotNull(dllmain_crt_process_attachDiff.BeforeSymbol);
        Assert.IsNotNull(dllmain_crt_process_attachDiff.AfterSymbol);
        Assert.AreEqual(0, dllmain_crt_process_attachDiff.SizeDiff);
        Assert.AreEqual(dllmain_crt_process_attachSize, dllmain_crt_process_attachDiff.BeforeSymbol.Size);

        var DllMainDiff = symbolDiffs.Single(s => s.Name == "DllMain(HINSTANCE__*, unsigned long, void*)");
        Assert.AreEqual("int DllMain(HINSTANCE__* hModule, unsigned long ul_reason_for_call, void* lpReserved)", ((CodeBlockSymbolDiff)DllMainDiff).ParentFunctionDiff.FullName);
        Assert.IsNotNull(DllMainDiff.BeforeSymbol);
        Assert.IsNotNull(DllMainDiff.AfterSymbol);
        Assert.AreEqual(RealPETestingConstants.DllMainFunctionSymbolSizeAfter - RealPETestingConstants.DllMainFunctionSymbolSizeBefore, DllMainDiff.SizeDiff);

        var FunctionInStaticLib1Diff = symbolDiffs.Single(s => s.Name == "FunctionInStaticLib1(int)");
        Assert.IsNotNull(FunctionInStaticLib1Diff.BeforeSymbol);
        Assert.IsNotNull(FunctionInStaticLib1Diff.AfterSymbol);
        Assert.AreEqual(FunctionInStaticLib1SizeAfter - FunctionInStaticLib1SizeBefore, FunctionInStaticLib1Diff.SizeDiff);

        var FunctionInStaticLib2Diff = symbolDiffs.Single(s => s.Name == "FunctionInStaticLib2(int)");
        Assert.IsNotNull(FunctionInStaticLib2Diff.BeforeSymbol);
        Assert.IsNull(FunctionInStaticLib2Diff.AfterSymbol);
        Assert.AreEqual(0 - FunctionInStaticLib2Size, FunctionInStaticLib2Diff.SizeDiff);

        var FunctionInStaticLib3Diff = symbolDiffs.Single(s => s.Name == "FunctionInStaticLib3(int)");
        Assert.IsNull(FunctionInStaticLib3Diff.BeforeSymbol);
        Assert.IsNotNull(FunctionInStaticLib3Diff.AfterSymbol);
        Assert.AreEqual(FunctionInStaticLib3Size - 0, FunctionInStaticLib3Diff.SizeDiff);

        // Let's try another COFF Group just for fun
        symbolDiffs = await diffSession.EnumerateSymbolDiffsInCOFFGroupDiff(textSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".text$x"), this.CancellationToken);

        Assert.AreEqual(1, symbolDiffs.Count(sd => sd.Name.Contains("catch$", StringComparison.Ordinal)));
        Assert.AreEqual(56, symbolDiffs.First(sd => sd.Name.Contains("catch$", StringComparison.Ordinal)).SizeDiff);
    }

    [TestMethod]
    public async Task SymbolDiffsInXDATACOFFGroupCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var textSectionDiff = await diffSession.LoadBinarySectionDiffByName(".rdata", this.CancellationToken);
        Assert.IsNotNull(textSectionDiff);
        var symbolDiffs = await diffSession.EnumerateSymbolDiffsInCOFFGroupDiff(textSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".xdata"), this.CancellationToken);

        string[] xdataSymbolPrefixes =
        {
                    "[ip2state]",
                    "[stateUnwindMap]",
                    "[unwind]",
                    "[handlerMap]",
                    "[chain-unwind]",
                    "[stateUnwindMap]",
                    "[tryMap]",
                };

        foreach (var sym in symbolDiffs)
        {
            var startsWithXDataPrefix = false;
            foreach (var prefix in xdataSymbolPrefixes)
            {
                if (sym.Name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    startsWithXDataPrefix = true;
                    break;
                }
            }

            Assert.IsTrue(startsWithXDataPrefix, $"'{sym.Name}' does not begin with a prefix expected to be seen in XDATA");
        }

        var unwindForFunctionInStaticLib1Diff = symbolDiffs.Single(s => s.Name.Contains("[unwind]", StringComparison.Ordinal) &&
                                                                        s.Name.Contains("FunctionInStaticLib1", StringComparison.Ordinal));
        Assert.IsNotNull(unwindForFunctionInStaticLib1Diff.BeforeSymbol);
        Assert.IsNotNull(unwindForFunctionInStaticLib1Diff.AfterSymbol);
        Assert.AreEqual(0, unwindForFunctionInStaticLib1Diff.SizeDiff);

        var unwindForFunctionInStaticLib2Diff = symbolDiffs.Single(s => s.Name.Contains("[unwind]", StringComparison.Ordinal) &&
                                                                        s.Name.Contains("FunctionInStaticLib2", StringComparison.Ordinal));
        Assert.IsNotNull(unwindForFunctionInStaticLib2Diff.BeforeSymbol);
        Assert.IsNull(unwindForFunctionInStaticLib2Diff.AfterSymbol);
        Assert.AreEqual(-8, unwindForFunctionInStaticLib2Diff.SizeDiff);

        var unwindForFunctionInStaticLib3Diff = symbolDiffs.Single(s => s.Name.Contains("[unwind]", StringComparison.Ordinal) &&
                                                                        s.Name.Contains("FunctionInStaticLib3", StringComparison.Ordinal));
        Assert.IsNull(unwindForFunctionInStaticLib3Diff.BeforeSymbol);
        Assert.IsNotNull(unwindForFunctionInStaticLib3Diff.AfterSymbol);
        Assert.AreEqual(8, unwindForFunctionInStaticLib3Diff.SizeDiff);

        // C++ exceptions are only used by 'after' so we can look for tryMap as a signal that we found xdata that exists only in 'after'
        foreach (var sym in symbolDiffs.Where(sd => sd.Name.Contains("[tryMap]", StringComparison.Ordinal)))
        {
            Assert.IsNull(sym.BeforeSymbol);
            Assert.IsNotNull(sym.AfterSymbol);
            Assert.IsTrue(sym.SizeDiff > 0);
        }
    }

    [TestMethod]
    public async Task SymbolDiffsInRSRCCOFFGroupCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll"),
                                                               MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb"),
                                                               MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll"),
                                                               MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb"),
                                                               logger);
        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(this.CancellationToken);
        var rsrcSectionDiff = sectionDiffs.Single(bsd => bsd.Name == ".rsrc");
        Assert.AreEqual(2, rsrcSectionDiff.COFFGroupDiffs.Count);
        var rsrc01CGDiff = rsrcSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".rsrc$01");
        var rsrc02CGDiff = rsrcSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".rsrc$02");

        // Sizes from link /dump /headers /coffgroup on the two test binaries, for .rsrc and .rsrc$[01|02]
        Assert.AreEqual(0xAE00 - 0xF000, rsrcSectionDiff.SizeDiff);
        Assert.AreEqual(0xADB8 - 0xEFB0, rsrcSectionDiff.VirtualSizeDiff);

        Assert.AreEqual(0x240 - 0x310, rsrc01CGDiff.SizeDiff);
        Assert.AreEqual(rsrc01CGDiff.SizeDiff, rsrc01CGDiff.VirtualSizeDiff);

        Assert.AreEqual(0xAB78 - 0xECA0, rsrc02CGDiff.SizeDiff);
        Assert.AreEqual(rsrc02CGDiff.SizeDiff, rsrc02CGDiff.VirtualSizeDiff);

        // Now we'll look at individual symbols to see if we diff'd those correctly
        var rsrcSymbolDiffs = await diffSession.EnumerateSymbolDiffsInCOFFGroupDiff(rsrc02CGDiff, this.CancellationToken);

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CURSOR - one cursor image added to a group
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        var groupCursorDiff = rsrcSymbolDiffs.Single(sym => sym.BeforeSymbol is RsrcGroupCursorDataSymbol);
        Assert.AreEqual(2, (groupCursorDiff.BeforeSymbol as RsrcGroupCursorDataSymbol)!.Cursors.Count);
        Assert.AreEqual(3, (groupCursorDiff.AfterSymbol as RsrcGroupCursorDataSymbol)!.Cursors.Count);
        // We added one cursor, which was the 16x16 1bpp, so the size diff should be equal to the size of that
        // cursor + one CURSORRESDIR
        var cursor16x16_1bppAfter = (groupCursorDiff.AfterSymbol as RsrcGroupCursorDataSymbol)!.Cursors.Single(cursor => cursor.Width == 16 && cursor.Height == 16 && cursor.BitsPerPixel == 1);
        Assert.AreEqual(RoundSizeUpTo8ByteAlignment(cursor16x16_1bppAfter) + Marshal.SizeOf<CURSORRESDIR>(), groupCursorDiff.SizeDiff);

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // ICON - one icon removed from a group
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        var groupIconDiff = rsrcSymbolDiffs.Single(sym => sym.BeforeSymbol is RsrcGroupIconDataSymbol);
        Assert.AreEqual(7, (groupIconDiff.BeforeSymbol as RsrcGroupIconDataSymbol)!.Icons.Count);
        Assert.AreEqual(3, (groupIconDiff.AfterSymbol as RsrcGroupIconDataSymbol)!.Icons.Count);

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // VERSION - entirely removed in 'after'
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        var versionDiff = rsrcSymbolDiffs.Single(sym => sym.BeforeSymbol!.Name.Contains("VERSION", StringComparison.Ordinal));
        Assert.IsNotNull(groupIconDiff.BeforeSymbol);
        Assert.IsNull(versionDiff.AfterSymbol);
        Assert.IsTrue(versionDiff.SizeDiff < 0);
    }
}
