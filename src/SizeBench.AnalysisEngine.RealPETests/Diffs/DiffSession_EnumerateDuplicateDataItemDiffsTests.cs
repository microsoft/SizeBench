using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb")]
[TestClass]
public sealed class DiffSession_EnumerateDuplicateDataItemDiffsTests
{
    public TestContext? TestContext { get; set; }

    private string BeforeBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");

    private string BeforePDBPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    private string AfterBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll");

    private string AfterPDBPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb");

    [TestMethod]
    public async Task DiffWithSelfHasZeroSizeDiff()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.BeforeBinaryPath, this.BeforePDBPath,
                                                               logger);
        var ddiDiffs = await diffSession.EnumerateDuplicateDataItemDiffs(CancellationToken.None);
        Assert.IsNotNull(ddiDiffs);

        Assert.AreEqual(3, ddiDiffs.Count);

        foreach (var ddiDiff in ddiDiffs)
        {
            Assert.AreEqual(0, ddiDiff.SizeDiff);
            Assert.AreEqual(0, ddiDiff.WastedSizeDiff);
        }
    }

    [TestMethod]
    public async Task DiffsFoundCorrectly()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var ddiDiffs = await diffSession.EnumerateDuplicateDataItemDiffs(CancellationToken.None);
        Assert.IsNotNull(ddiDiffs);

        Assert.AreEqual(4, ddiDiffs.Count);

        // Duplication that got worse from before -> after, but existed in both
        var duplicatedPoint = ddiDiffs.Single(ddiDiff => ddiDiff.SymbolDiff.Name == "duplicatedPoint");
        Assert.AreEqual(32u, duplicatedPoint.WastedSizeRemaining);
        Assert.AreEqual(8, duplicatedPoint.WastedSizeDiff);
        Assert.AreEqual(8, duplicatedPoint.SizeDiff);

        // Duplication that got better from before -> after, but existed in both
        var duplicatedPointArray = ddiDiffs.Single(ddiDiff => ddiDiff.SymbolDiff.Name == "duplicatedPointArray");
        Assert.AreEqual(48u, duplicatedPointArray.WastedSizeRemaining);
        Assert.AreEqual(-24, duplicatedPointArray.WastedSizeDiff);
        Assert.AreEqual(-24, duplicatedPointArray.SizeDiff);

        // Duplication that is only in 'after' (to check for null-before things)
        var duplicatedOnlyInAfter = ddiDiffs.Single(ddiDiff => ddiDiff.SymbolDiff.Name == "duplicatedOnlyInAfter");
        Assert.AreEqual(4u, duplicatedOnlyInAfter.WastedSizeRemaining);
        Assert.AreEqual(4, duplicatedOnlyInAfter.WastedSizeDiff);
        Assert.AreEqual(8, duplicatedOnlyInAfter.SizeDiff); // Because we go from 0->2 copies

        // Duplication that is only in 'before' (to check for null-after things)
        var duplicatedOnlyInBefore = ddiDiffs.Single(ddiDiff => ddiDiff.SymbolDiff.Name == "duplicatedOnlyInBefore");
        Assert.AreEqual(0u, duplicatedOnlyInBefore.WastedSizeRemaining);
        Assert.AreEqual(-4, duplicatedOnlyInBefore.WastedSizeDiff);
        Assert.AreEqual(-8, duplicatedOnlyInBefore.SizeDiff); // Because we go from 2->0 copies
    }
}
