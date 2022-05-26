using System.Diagnostics;
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
public sealed class DiffSession_EnumerateSymbolsInBinarySectionTests
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

        foreach(var sectionDiff in sectionDiffs)
        {
            var symbolDiffs = await diffSession.EnumerateSymbolDiffsInBinarySectionDiff(sectionDiff, this.CancellationToken);

            foreach (var symbol in symbolDiffs)
            {
                Assert.AreEqual(0, symbol.SizeDiff);
            }
        }
    }

    [TestMethod]
    public async Task SymbolDiffsInRegularSectionCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var textSectionDiff = await diffSession.LoadBinarySectionDiffByName(".text", this.CancellationToken);
        Assert.IsNotNull(textSectionDiff);
        var symbolDiffs = await diffSession.EnumerateSymbolDiffsInBinarySectionDiff(textSectionDiff, this.CancellationToken);

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

        var FunctionInStaticLib1BlockDiff = (CodeBlockSymbolDiff)symbolDiffs.Single(s => s.Name == "FunctionInStaticLib1(int)");

        Assert.IsNotNull(FunctionInStaticLib1BlockDiff.BeforeSymbol);
        Assert.IsNotNull(FunctionInStaticLib1BlockDiff.AfterSymbol);
        Assert.AreEqual(FunctionInStaticLib1SizeAfter - FunctionInStaticLib1SizeBefore, FunctionInStaticLib1BlockDiff.SizeDiff);
        var FunctionInStaticLib1Diff = FunctionInStaticLib1BlockDiff.ParentFunctionDiff;
        Assert.IsNotNull(FunctionInStaticLib1Diff);
        Assert.IsNotNull(FunctionInStaticLib1Diff.BeforeSymbol);
        Assert.IsNotNull(FunctionInStaticLib1Diff.AfterSymbol);
        Assert.AreEqual(1, FunctionInStaticLib1Diff.CodeBlockDiffs.Count);
        Assert.IsTrue(ReferenceEquals(FunctionInStaticLib1Diff.CodeBlockDiffs[0], FunctionInStaticLib1BlockDiff));
        Assert.AreEqual("FunctionInStaticLib1", FunctionInStaticLib1Diff.FunctionName);
        Assert.IsNotNull(FunctionInStaticLib1Diff.BeforeSymbol.ArgumentNames);
        Assert.IsNotNull(FunctionInStaticLib1Diff.AfterSymbol.ArgumentNames);
        Assert.AreEqual(1, FunctionInStaticLib1Diff.BeforeSymbol.ArgumentNames.Count);
        Assert.AreEqual(1, FunctionInStaticLib1Diff.AfterSymbol.ArgumentNames.Count);
        Assert.AreEqual("x", FunctionInStaticLib1Diff.BeforeSymbol.ArgumentNames[0].Name);
        Assert.AreEqual("int", FunctionInStaticLib1Diff.BeforeSymbol.ArgumentNames[0].Type.Name);
        Assert.AreEqual("x", FunctionInStaticLib1Diff.AfterSymbol.ArgumentNames[0].Name);
        Assert.AreEqual("int", FunctionInStaticLib1Diff.AfterSymbol.ArgumentNames[0].Type.Name);
        Assert.AreEqual("void FunctionInStaticLib1(int x)", FunctionInStaticLib1Diff.FullName);
        Assert.AreEqual(0, FunctionInStaticLib1Diff.SizeDiff);

        var FunctionInStaticLib2Diff = symbolDiffs.Single(s => s.Name == "FunctionInStaticLib2(int)");
        Assert.IsNotNull(FunctionInStaticLib2Diff.BeforeSymbol);
        Assert.IsNull(FunctionInStaticLib2Diff.AfterSymbol);
        Assert.AreEqual(0 - FunctionInStaticLib2Size, FunctionInStaticLib2Diff.SizeDiff);

        var FunctionInStaticLib3Diff = symbolDiffs.Single(s => s.Name == "FunctionInStaticLib3(int)");
        Assert.IsNull(FunctionInStaticLib3Diff.BeforeSymbol);
        Assert.IsNotNull(FunctionInStaticLib3Diff.AfterSymbol);
        Assert.AreEqual(FunctionInStaticLib3Size - 0, FunctionInStaticLib3Diff.SizeDiff);
    }

    [TestMethod]
    public async Task SymbolDiffsInPDATASectionCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var pdataSectionDiff = await diffSession.LoadBinarySectionDiffByName(".pdata", this.CancellationToken);
        Assert.IsNotNull(pdataSectionDiff);
        var symbolDiffs = await diffSession.EnumerateSymbolDiffsInBinarySectionDiff(pdataSectionDiff, this.CancellationToken);

        // This is pretty noisy but helps with debugging this test for now.
        Trace.WriteLine("Names of the pdata symbol diffs found:");
        foreach (var sym in symbolDiffs)
        {
            Trace.WriteLine(sym.Name);
        }

        foreach (var sym in symbolDiffs)
        {
            Assert.IsTrue(sym.Name.StartsWith("[pdata]", StringComparison.Ordinal));
        }

        var pdataForFunctionInStaticLib1Diff = symbolDiffs.Single(s => s.Name == "[pdata] FunctionInStaticLib1(int)");
        Assert.IsNotNull(pdataForFunctionInStaticLib1Diff.BeforeSymbol);
        Assert.IsNotNull(pdataForFunctionInStaticLib1Diff.AfterSymbol);
        Assert.AreEqual(0, pdataForFunctionInStaticLib1Diff.SizeDiff);

        var pdataForFunctionInStaticLib2Diff = symbolDiffs.Single(s => s.Name == "[pdata] FunctionInStaticLib2(int)");
        Assert.IsNotNull(pdataForFunctionInStaticLib2Diff.BeforeSymbol);
        Assert.IsNull(pdataForFunctionInStaticLib2Diff.AfterSymbol);
        Assert.AreEqual(-12, pdataForFunctionInStaticLib2Diff.SizeDiff);

        var pdataForFunctionInStaticLib3Diff = symbolDiffs.Single(s => s.Name == "[pdata] FunctionInStaticLib3(int)");
        Assert.IsNull(pdataForFunctionInStaticLib3Diff.BeforeSymbol);
        Assert.IsNotNull(pdataForFunctionInStaticLib3Diff.AfterSymbol);
        Assert.AreEqual(12, pdataForFunctionInStaticLib3Diff.SizeDiff);
    }

    [TestMethod]
    public async Task SymbolDiffsInRSRCSectionCanBeEnumerated()
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
        var rsrcSymbolDiffs = await diffSession.EnumerateSymbolDiffsInBinarySectionDiff(rsrcSectionDiff, this.CancellationToken);

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
        var versionDiff = rsrcSymbolDiffs.Single(sym => sym.BeforeSymbol is RsrcDataSymbol && sym.BeforeSymbol.Name.Contains("VERSION", StringComparison.Ordinal));
        Assert.IsNotNull(groupIconDiff.BeforeSymbol);
        Assert.IsNull(versionDiff.AfterSymbol);
        Assert.IsTrue(versionDiff.SizeDiff < 0);
    }

    [TestMethod]
    public async Task SymbolDiffsInImportsCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll"),
                                                               MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb"),
                                                               MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll"),
                                                               MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb"),
                                                               logger);

        var sectionDiffs = await diffSession.EnumerateBinarySectionsAndCOFFGroupDiffs(this.CancellationToken);
        var rdataSectionDiff = sectionDiffs.Single(s => s.Name == ".rdata");

        // First we'll check import descriptors - there's only 4 so we'll just check them all.  One should have a difference as the 'after' binary dropped
        // the dependency on msvcp140d.dll
        var idata2CGDiff = rdataSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".idata$2");
        var symbols = await diffSession.EnumerateSymbolDiffsInCOFFGroupDiff(idata2CGDiff, this.CancellationToken);

        Assert.AreEqual(4, symbols.Count);

        var descriptor = symbols.Single(sd => sd.Name == "[import descriptor] VCRUNTIME140D.dll");
        Assert.IsNotNull(descriptor.BeforeSymbol);
        Assert.IsNotNull(descriptor.AfterSymbol);
        Assert.AreEqual(0, descriptor.SizeDiff);
        Assert.AreEqual(0, descriptor.VirtualSizeDiff);

        descriptor = symbols.Single(sd => sd.Name == "[import descriptor] ucrtbased.dll");
        Assert.IsNotNull(descriptor.BeforeSymbol);
        Assert.IsNotNull(descriptor.AfterSymbol);
        Assert.AreEqual(0, descriptor.SizeDiff);
        Assert.AreEqual(0, descriptor.VirtualSizeDiff);

        descriptor = symbols.Single(sd => sd.Name == "[import descriptor] KERNEL32.dll");
        Assert.IsNotNull(descriptor.BeforeSymbol);
        Assert.IsNotNull(descriptor.AfterSymbol);
        Assert.AreEqual(0, descriptor.SizeDiff);
        Assert.AreEqual(0, descriptor.VirtualSizeDiff);

        descriptor = symbols.Single(sd => sd.Name == "[import descriptor] MSVCP140D.dll");
        Assert.IsNotNull(descriptor.BeforeSymbol);
        Assert.IsNull(descriptor.AfterSymbol);
        Assert.AreEqual(-20, descriptor.SizeDiff);
        Assert.AreEqual(-20, descriptor.VirtualSizeDiff);

        // The null thunk should be in both and no size diff
        var idata3CGDiff = rdataSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".idata$3");
        symbols = await diffSession.EnumerateSymbolDiffsInCOFFGroupDiff(idata3CGDiff, this.CancellationToken);

        Assert.AreEqual(1, symbols.Count);

        descriptor = symbols[0];
        Assert.AreEqual("[import descriptor] null terminator", descriptor.Name);
        Assert.IsNotNull(descriptor.BeforeSymbol);
        Assert.IsNotNull(descriptor.AfterSymbol);
        Assert.AreEqual(0, descriptor.SizeDiff);
        Assert.AreEqual(0, descriptor.VirtualSizeDiff);

        // Now for some thunks
        var idata4CGDiff = rdataSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".idata$4");
        symbols = await diffSession.EnumerateSymbolDiffsInCOFFGroupDiff(idata4CGDiff, this.CancellationToken);

        // A thunk that is in KERNEL32.dll, which both binaries import, but this thunk is only in the 'before'
        var thunk = symbols.Single(sd => sd.Name == "[import thunk] KERNEL32.dll ExitProcess, ordinal 352");
        Assert.IsNotNull(thunk.BeforeSymbol);
        Assert.IsNull(thunk.AfterSymbol);
        Assert.AreEqual(-8, thunk.SizeDiff);
        Assert.AreEqual(-8, thunk.VirtualSizeDiff);

        // A thunk that is present in both
        thunk = symbols.Single(sd => sd.Name == "[import thunk] KERNEL32.dll GetCurrentProcess, ordinal 537");
        Assert.IsNotNull(thunk.BeforeSymbol);
        Assert.IsNotNull(thunk.AfterSymbol);
        Assert.AreEqual(0, thunk.SizeDiff);
        Assert.AreEqual(0, thunk.VirtualSizeDiff);

        // And a thunk that's in a descriptor that is only in 'before'
        thunk = symbols.Single(sd => sd.Name == "[import thunk] MSVCP140D.dll ?_Xbad_alloc@std@@YAXXZ, ordinal 659");
        Assert.IsNotNull(thunk.BeforeSymbol);
        Assert.IsNull(thunk.AfterSymbol);
        Assert.AreEqual(-8, thunk.SizeDiff);
        Assert.AreEqual(-8, thunk.VirtualSizeDiff);

        // And finally some IMAGE_IMPORT_BY_NAMEs
        var idata6CGDiff = rdataSectionDiff.COFFGroupDiffs.Single(cgd => cgd.Name == ".idata$6");
        symbols = await diffSession.EnumerateSymbolDiffsInCOFFGroupDiff(idata6CGDiff, this.CancellationToken);

        // An ImportByName that is present only in 'before'
        var byNameString = symbols.Single(sd => sd.Name == "`string': \"ExitProcess\"");
        Assert.IsNotNull(byNameString.BeforeSymbol);
        Assert.IsNull(byNameString.AfterSymbol);
        Assert.AreEqual(-("ExitProcess".Length + 3), byNameString.SizeDiff);
        Assert.AreEqual(-("ExitProcess".Length + 3), byNameString.VirtualSizeDiff);

        // One present in both
        byNameString = symbols.Single(sd => sd.Name == "`string': \"GetCurrentProcess\"");
        Assert.IsNotNull(byNameString.BeforeSymbol);
        Assert.IsNotNull(byNameString.AfterSymbol);
        Assert.AreEqual(0, byNameString.SizeDiff);
        Assert.AreEqual(0, byNameString.VirtualSizeDiff);

        // And lastly, one present in a descriptor that is only in 'before'
        byNameString = symbols.Single(sd => sd.Name == "`string': \"?_Xbad_alloc@std@@YAXXZ\"");
        Assert.IsNotNull(byNameString.BeforeSymbol);
        Assert.IsNull(byNameString.AfterSymbol);
        Assert.AreEqual(-("?_Xbad_alloc@std@@YAXXZ".Length + 3), byNameString.SizeDiff);
        Assert.AreEqual(-("?_Xbad_alloc@std@@YAXXZ".Length + 3), byNameString.VirtualSizeDiff);
    }
}
