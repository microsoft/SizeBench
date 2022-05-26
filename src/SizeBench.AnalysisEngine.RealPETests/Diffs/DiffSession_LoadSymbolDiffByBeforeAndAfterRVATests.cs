using System.IO;
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
public sealed class DiffSession_LoadSymbolDiffByBeforeAndAfterRVATests
{
    public TestContext? TestContext { get; set; }

    private string BeforeBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsBefore.dll");

    private string BeforePDBPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsBefore.pdb");

    private string AfterBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsAfter.dll");

    private string AfterPDBPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsAfter.pdb");

    [TestMethod]
    public async Task SymbolDiffsCanBeLoadedByRVAs()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var textSectionDiff = await diffSession.LoadBinarySectionDiffByName(".text", CancellationToken.None);
        Assert.IsNotNull(textSectionDiff);
        var symbolDiffs = await diffSession.EnumerateSymbolDiffsInBinarySectionDiff(textSectionDiff, CancellationToken.None);

        // We'll try one that is null in 'before'
        var FunctionInStaticLib3Diff = symbolDiffs.Single(s => s.Name == "FunctionInStaticLib3(int)");
        Assert.IsNull(FunctionInStaticLib3Diff.BeforeSymbol);
        Assert.IsNotNull(FunctionInStaticLib3Diff.AfterSymbol);
        Assert.IsTrue(ReferenceEquals(FunctionInStaticLib3Diff, await diffSession.LoadSymbolDiffByBeforeAndAfterRVA(null, FunctionInStaticLib3Diff.AfterSymbol.RVA, CancellationToken.None)));

        // We'll try one that is null in 'after'
        var FunctionInStaticLib2Diff = symbolDiffs.Single(s => s.Name == "FunctionInStaticLib2(int)");
        Assert.IsNotNull(FunctionInStaticLib2Diff.BeforeSymbol);
        Assert.IsNull(FunctionInStaticLib2Diff.AfterSymbol);
        Assert.IsTrue(ReferenceEquals(FunctionInStaticLib2Diff, await diffSession.LoadSymbolDiffByBeforeAndAfterRVA(FunctionInStaticLib2Diff.BeforeSymbol.RVA, null, CancellationToken.None)));

        // And finally one that is non-null in both before and after
        var DllMainDiff = symbolDiffs.Single(s => s.Name == "DllMain(HINSTANCE__*, unsigned long, void*)");
        Assert.AreEqual("int DllMain(HINSTANCE__* hModule, unsigned long ul_reason_for_call, void* lpReserved)", ((CodeBlockSymbolDiff)DllMainDiff).ParentFunctionDiff.FullName);
        Assert.IsNotNull(DllMainDiff.BeforeSymbol);
        Assert.IsNotNull(DllMainDiff.AfterSymbol);
        Assert.IsTrue(ReferenceEquals(DllMainDiff, await diffSession.LoadSymbolDiffByBeforeAndAfterRVA(DllMainDiff.BeforeSymbol.RVA, DllMainDiff.AfterSymbol.RVA, CancellationToken.None)));
    }

    [TestMethod]
    public async Task SymbolDiffsCanBeLoadedByRVAsInRSRC()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll"),
                                                               Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb"),
                                                               Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll"),
                                                               Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb"),
                                                               logger);

        var cursorDiff = await diffSession.LoadSymbolDiffByBeforeAndAfterRVA(0xB310, 0x7240, CancellationToken.None);
        Assert.IsNotNull(cursorDiff);
        Assert.IsInstanceOfType(cursorDiff.BeforeSymbol, typeof(RsrcGroupCursorDataSymbol));
        Assert.IsInstanceOfType(cursorDiff.AfterSymbol, typeof(RsrcGroupCursorDataSymbol));
        Assert.IsTrue(cursorDiff.SizeDiff > 0);

        var iconDiff = await diffSession.LoadSymbolDiffByBeforeAndAfterRVA(0x100A0, 0xC090, CancellationToken.None);
        Assert.IsNotNull(iconDiff);
        Assert.IsInstanceOfType(iconDiff.BeforeSymbol, typeof(RsrcGroupIconDataSymbol));
        Assert.IsInstanceOfType(iconDiff.AfterSymbol, typeof(RsrcGroupIconDataSymbol));
        Assert.IsTrue(iconDiff.SizeDiff < 0);

        var versionDiff = await diffSession.LoadSymbolDiffByBeforeAndAfterRVA(0x19AA0, null, CancellationToken.None);
        Assert.IsNotNull(versionDiff);
        Assert.IsInstanceOfType(versionDiff.BeforeSymbol, typeof(RsrcDataSymbol));
        Assert.IsTrue(versionDiff.BeforeSymbol!.Name.Contains("VERSION", StringComparison.Ordinal));
        Assert.IsNull(versionDiff.AfterSymbol);
        Assert.IsTrue(versionDiff.SizeDiff < 0);
    }
}
