using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppDll.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppDll.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Cpp32BitDll.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Cpp32BitDll.pdb")]
[TestClass]
public class SessionTests
{
    public TestContext? TestContext { get; set; }

    private string CppDllBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppDll.dll");

    private string CppDllPDBPath => Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppDll.pdb");

    private string Cpp32BitDllBinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.Cpp32BitDll.dll");

    private string Cpp32BitDllPDBPath => Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.Cpp32BitDll.pdb");

    [TestMethod]
    public async Task BytesPerWordIsCorrectFor64Bit()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.CppDllBinaryPath, this.CppDllPDBPath, logger);
        Assert.AreEqual<uint>(8, session.BytesPerWord);
    }

    [TestMethod]
    public async Task BytesPerWordIsCorrectFor32Bit()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.Cpp32BitDllBinaryPath, this.Cpp32BitDllPDBPath, logger);
        Assert.AreEqual<uint>(4, session.BytesPerWord);
    }

    [TestMethod]
    public async Task SwappingBinaryAndPDBPathsProvidesHelpfulErrorMessage()
    {
        // I've seen too many cases where someone puts the DLL/EXE in the "PDB" box in the UI, and the PDB in the other one - so let's see if that
        // case generates at least a somewhat-helpful error message.
        try
        {
            using var logger = new NoOpLogger();
            await using var session = await Session.Create(this.Cpp32BitDllPDBPath, this.Cpp32BitDllBinaryPath, logger);
            Assert.Fail("It should not be possible to get here!");
        }
        catch (PDBNotSuitableForAnalysisException ex)
        {
            StringAssert.Contains(ex.Message, "E_PDB_FORMAT", StringComparison.Ordinal);
        }
    }
}
