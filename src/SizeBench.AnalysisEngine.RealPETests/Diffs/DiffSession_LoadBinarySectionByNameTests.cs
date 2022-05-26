using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsBefore.pdb")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.dll")]
[DeploymentItem(@"Test PEs\CppTestCases_BasicDiffObjectsAfter.pdb")]
[TestClass]
public sealed class DiffSession_LoadBinarySectionByNameTests
{
    public TestContext? TestContext { get; set; }

    private string BeforeBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsBefore.dll");

    private string BeforePDBPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsBefore.pdb");

    private string AfterBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsAfter.dll");

    private string AfterPDBPath => Path.Combine(this.TestContext!.DeploymentDirectory, "CppTestCases_BasicDiffObjectsAfter.pdb");

    [TestMethod]
    public async Task LoadBinarySectionByNameUsesCacheIfPopulated()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var textSection = await diffSession.LoadBinarySectionDiffByName(".text", CancellationToken.None);
        Assert.IsNotNull(textSection);

        var load2 = diffSession.LoadBinarySectionDiffByName(".text", CancellationToken.None);
        Assert.IsTrue(load2.IsCompleted);
        Assert.IsTrue(ReferenceEquals(textSection, await load2));
    }

    [TestMethod]
    public async Task LoadBinarySectionByNameWorks()
    {
        using var logger = new NoOpLogger();
        await using var diffSession = await DiffSession.Create(this.BeforeBinaryPath, this.BeforePDBPath,
                                                               this.AfterBinaryPath, this.AfterPDBPath,
                                                               logger);
        var textSectionDiff = await diffSession.LoadBinarySectionDiffByName(".text", CancellationToken.None);
        var rdataSectionDiff = await diffSession.LoadBinarySectionDiffByName(".rdata", CancellationToken.None);
        Assert.IsNotNull(textSectionDiff);
        Assert.IsNotNull(rdataSectionDiff);

        Assert.AreEqual(RealPETestingConstants.AfterTextSize - RealPETestingConstants.BeforeTextSize, textSectionDiff.SizeDiff);
        Assert.AreEqual(RealPETestingConstants.AfterRdataSize - RealPETestingConstants.BeforeRdataSize, rdataSectionDiff.SizeDiff);
    }
}
