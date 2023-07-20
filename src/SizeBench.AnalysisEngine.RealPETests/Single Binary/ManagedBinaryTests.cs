using System.IO;
using System.Reflection;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[TestClass]
public sealed class ManagedBinaryTests
{
    public TestContext? TestContext { get; set; }
    private static string MakePath(string filename) => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, filename);

    private static string BinaryPath => MakePath("SizeBench.AnalysisEngine.RealPETests.dll");

    private static string PDBPath => MakePath("SizeBench.AnalysisEngine.RealPETests.pdb");

    [ExpectedException(typeof(BinaryNotAnalyzableException), AllowDerivedTypes = false)]
    [TestMethod]
    public async Task ManagedBinaryIsRejected()
    {
        // SizeBench hasn't been taught how to inspect managed binaries yet, so we should throw to make our limitations clear.
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(BinaryPath, PDBPath, logger);
        // We shouldn't get this far, as opening the session should throw
        Assert.Fail();
    }

}
