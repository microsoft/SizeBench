using System.IO;
using SizeBench.AnalysisEngine;
using SizeBench.Logging;

namespace PEParser.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb")]
[TestClass]
public sealed class DiffRegressionTests
{
    public TestContext? TestContext { get; set; }

    public string MakePath(string binary) => Path.Combine(this.TestContext!.DeploymentDirectory!, binary);

    [TestMethod]
    public async Task SymbolDiffsForArraysWorkWhenArrayElementCountChanges()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(MakePath(@"SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll"),
                                                               MakePath(@"SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb"),
                                                               MakePath(@"SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.dll"),
                                                               MakePath(@"SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb"),
                                                               logger);
        // Regression test for an array that grows in size, as we had a bug previously that this wouldn't diff as "the same symbol" across the two binaries.
        var rdataSectionDiff = await diffSession.LoadBinarySectionDiffByName(".rdata", CancellationToken.None);
        Assert.IsNotNull(rdataSectionDiff);
        var rdataSymbolDiffs = await diffSession.EnumerateSymbolDiffsInBinarySectionDiff(rdataSectionDiff, CancellationToken.None);
        var arrayThatGrowsDiff = rdataSymbolDiffs.Single(s => s.Name == "arrayThatGrows");
        Assert.IsNotNull(arrayThatGrowsDiff.BeforeSymbol);
        Assert.IsNotNull(arrayThatGrowsDiff.AfterSymbol);
        Assert.AreEqual(2, arrayThatGrowsDiff.SizeDiff); // This grew from 2 unsigned shorts to 3 unsigned shorts, so it grew by 2 bytes
        Assert.AreEqual(2, arrayThatGrowsDiff.VirtualSizeDiff);
    }
}
