using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb")]
[TestClass]
public class MismatchedBinaryAndPDBTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory, filename);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");

    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesAfter.pdb");

    [ExpectedException(typeof(BinaryAndPDBSignatureMismatchException), AllowDerivedTypes = false)]
    [TestMethod]
    public async Task MismatchedBinaryAndPDBGetRejected()
    {
        // When a PDB and binary mismatch in their debug signature, they are rejected outright
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);

        // We shouldn't get this far, as opening the session should throw
        Assert.Fail();
    }
}
