using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Dllx64CustomAlign.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Dllx64CustomAlign.pdb")]
[TestClass]
public class CustomAlignTests
{
    public TestContext? TestContext { get; set; }

    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);
    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.Dllx64CustomAlign.dll");
    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.Dllx64CustomAlign.pdb");

    [TestMethod]
    public async Task CustomAlignCanBeParsed()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var textSection = await session.LoadBinarySectionByName(".text", CancellationToken.None);
        Assert.IsNotNull(textSection);
        Assert.AreEqual(0x400u, textSection.RVA);

        var rdataSection = await session.LoadBinarySectionByName(".rdata", CancellationToken.None);
        Assert.IsNotNull(rdataSection);
        Assert.AreEqual(0x1E00u, rdataSection.RVA);
        Assert.AreEqual(textSection.RVA + textSection.Size, rdataSection.RVA); // sections should be tightly packed to the /align

        var dataSection = await session.LoadBinarySectionByName(".data", CancellationToken.None);
        Assert.IsNotNull(dataSection);
        Assert.AreEqual(0x800u, dataSection.Size);
        Assert.AreEqual(0x648u, dataSection.VirtualSize);
    }
}
