using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Dllx64MinimalPDB.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Dllx64MinimalPDB.pdb")]
[TestClass]
public class MiniPDBTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.Dllx64MinimalPDB.dll");

    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.Dllx64MinimalPDB.pdb");

    [TestMethod]
    public async Task MiniPDBIsRejected()
    {
        // Minimal PDBs are unsuitable for static analysis, we should reject them outright
        using var logger = new NoOpLogger();
        await Assert.ThrowsExactlyAsync<PDBNotSuitableForAnalysisException>(async () =>
        {
            await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        });
    }
}
