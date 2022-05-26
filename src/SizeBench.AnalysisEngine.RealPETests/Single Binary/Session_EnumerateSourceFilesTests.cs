using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[TestClass]
public sealed class Session_EnumerateSourceFilesTests
{
    public TestContext? TestContext { get; set; }

    private string BinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");

    private string PDBPath => Path.Combine(this.TestContext!.DeploymentDirectory, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    [TestMethod]
    public async Task SourceFilesCanBeEnumeratedWithPDataAndXDataAttributedToCorrectSourceFile()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var sourceFiles = await session.EnumerateSourceFiles(CancellationToken.None);
        Assert.IsNotNull(sourceFiles);

        // <vector> is a good header file to look at, since it contributes to multiple source files and compilands, and also has
        // COMDAT folding going on between two compilands, so it hits a lot of interesting cases.
        var vectorSF = sourceFiles.Single(sf => sf.Name.Contains(@"\vector", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(3, vectorSF.Compilands.Count);

        var sourceFile1Compiland = vectorSF.Compilands.Single(c => c.ShortName == "SourceFile1.obj");
        var sourceFile2Compiland = vectorSF.Compilands.Single(c => c.ShortName == "SourceFile2.obj");
        var dllMainCompiland = vectorSF.Compilands.Single(c => c.ShortName == "dllmain.obj");

        Assert.AreEqual(2, vectorSF.CompilandContributions.Count);
        Assert.IsTrue(vectorSF.CompilandContributions.ContainsKey(sourceFile1Compiland));
        // Note SourceFile2 is not in the CompilandContributions, since everything was COMDAT-folded with other compilands
        Assert.IsTrue(vectorSF.CompilandContributions.ContainsKey(dllMainCompiland));

        Assert.IsTrue(vectorSF.SectionContributionsByName.ContainsKey(".text"));
        Assert.IsTrue(vectorSF.SectionContributionsByName.ContainsKey(".rdata"));
        Assert.IsTrue(vectorSF.SectionContributionsByName.ContainsKey(".pdata"));

        Assert.IsTrue(vectorSF.COFFGroupContributionsByName.ContainsKey(".text$mn"));
        Assert.IsTrue(vectorSF.COFFGroupContributionsByName.ContainsKey(".xdata"));
    }
}
