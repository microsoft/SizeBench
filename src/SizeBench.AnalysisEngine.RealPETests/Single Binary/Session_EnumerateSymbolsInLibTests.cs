using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppDll.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppDll.pdb")]
[TestClass]
public sealed class Session_EnumerateSymbolsInLibTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppDll.dll");

    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppDll.pdb");

    [TestMethod]
    public async Task CppDllSymbolsInLibCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var libs = await session.EnumerateLibs(CancellationToken.None);
        Assert.IsNotNull(libs);

        var dllMainLib = (from s in libs where s.Name.Contains("dllmain.obj", StringComparison.Ordinal) select s).FirstOrDefault();
        Assert.IsNotNull(dllMainLib);

        var symbolsInDllMain = await session.EnumerateSymbolsInLib(dllMainLib, CancellationToken.None);
        Assert.IsNotNull(symbolsInDllMain);
    }
}
