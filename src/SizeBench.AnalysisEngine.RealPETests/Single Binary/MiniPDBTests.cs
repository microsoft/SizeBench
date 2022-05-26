using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Dllx64MinimalPDB.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Dllx64MinimalPDB.pdb")]
[TestClass]
public class MiniPDBTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory, filename);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.Dllx64MinimalPDB.dll");

    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.Dllx64MinimalPDB.pdb");

    [ExpectedException(typeof(PDBNotSuitableForAnalysisException), AllowDerivedTypes = false)]
    [TestMethod]
    public async Task MiniPDBIsRejected()
    {
        // Minimal PDBs are unsuitable for static analysis, we should reject them outright
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);

        // We shouldn't get this far, as opening the session should throw
        Assert.Fail();
    }
}
