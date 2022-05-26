using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[TestClass]
public sealed class ManagedBinaryTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory, filename);

    private string BinaryPath => MakePath("SizeBench.AnalysisEngine.RealPETests.dll");

    private string PDBPath => MakePath("SizeBench.AnalysisEngine.RealPETests.pdb");

    [ExpectedException(typeof(BinaryNotAnalyzableException), AllowDerivedTypes = false)]
    [TestMethod]
    public async Task ManagedBinaryIsRejected()
    {
        // SizeBench hasn't been taught how to inspect managed binaries yet, so we should throw to make our limitations clear.
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        // We shouldn't get this far, as opening the session should throw
        Assert.Fail();
    }

}
