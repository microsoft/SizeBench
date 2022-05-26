using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests.Single_Binary;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.ForceIntegrityBit.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.ForceIntegrityBit.pdb")]
[TestClass]
public class ForceIntegrityBitTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory, filename);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.ForceIntegrityBit.dll");

    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.ForceIntegrityBit.pdb");

    [TestMethod]
    public async Task BinaryWithForceIntegrityBitSetCanBeLoaded()
    {
        // Binaries with the force-integrity-bit set should be loadable, we'll just need to do extra work to load them
        // (by stripping that bit in a temp file location)
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
    }
}
