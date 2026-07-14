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

    [TestMethod]
    public async Task ManagedBinaryIsRejected()
    {
        // SizeBench hasn't been taught how to inspect managed binaries yet, so we should throw to make our limitations clear.
        using var logger = new NoOpLogger();
        await Assert.ThrowsExactlyAsync<BinaryNotAnalyzableException>(async () =>
        {
            await using var session = await Session.Create(BinaryPath, PDBPath, logger);
        });
    }

}
