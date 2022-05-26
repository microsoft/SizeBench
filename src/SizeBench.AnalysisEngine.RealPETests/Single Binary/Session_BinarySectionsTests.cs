using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppDll.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppDll.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Cpp32BitDll.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Cpp32BitDll.pdb")]
[TestClass]
public sealed class Session_BinarySectionsTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory, filename);

    private string CppDllBinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppDll.dll");

    private string CppDllPDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppDll.pdb");

    private string Cpp32BitDllBinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.Cpp32BitDll.dll");

    private string Cpp32BitDllPDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.Cpp32BitDll.pdb");

    [TestMethod]
    public async Task CppDllSectionsCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.CppDllBinaryPath, this.CppDllPDBPath, logger);
        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None);
        Assert.IsNotNull(sections);

        // Validating this against known good output from "link /dump /headers SizeBench.AnalysisEngine.Tests.CppDll.dll"
        var textSection = (from s in sections where s.Name == ".text" select s).FirstOrDefault();
        var rdataSection = (from s in sections where s.Name == ".rdata" select s).FirstOrDefault();
        var dataSection = (from s in sections where s.Name == ".data" select s).FirstOrDefault();
        var relocSection = (from s in sections where s.Name == ".reloc" select s).FirstOrDefault();

        Assert.IsNotNull(textSection);
        Assert.IsNotNull(rdataSection);
        Assert.IsNotNull(dataSection);
        Assert.IsNotNull(relocSection);

        Assert.AreEqual(0x1A00u, textSection.Size);
        Assert.IsTrue(textSection.COFFGroups.Count > 0);
        Assert.AreEqual(0xC00u, rdataSection.Size);
        Assert.IsTrue(rdataSection.COFFGroups.Count > 0);
        Assert.AreEqual(0x200u, relocSection.Size);
        Assert.AreEqual(0, relocSection.COFFGroups.Count);

        var textmnCOFFGroup = (from cg in textSection.COFFGroups where cg.Name == ".text$mn" select cg).First();
        var xdataCOFFGroup = (from cg in rdataSection.COFFGroups where cg.Name == ".xdata" select cg).First();
        var bssCOFFGroup = (from cg in dataSection.COFFGroups where cg.Name == ".bss" select cg).First();
        var crtXCACOFFGroup = (from cg in rdataSection.COFFGroups where cg.Name == ".CRT$XCA" select cg).First();
        var nonexistentCOFFGroup = (from cg in rdataSection.COFFGroups where cg.Name == ".CRT$ZZZ" select cg).FirstOrDefault();

        Assert.IsNull(nonexistentCOFFGroup); // Negative test case - let's make sure not everything matches

        // Validating this against known good output from "link /dump /headers /coffgroup SizeBench.AnalysisEngine.Tests.CppDll.dll"
        Assert.AreEqual(0x1780u, textmnCOFFGroup.Size);
        Assert.AreEqual(".text", textmnCOFFGroup.Section.Name);
        Assert.AreEqual(0x194u, xdataCOFFGroup.Size);
        Assert.AreEqual(".rdata", xdataCOFFGroup.Section.Name);
        Assert.AreEqual(0u, bssCOFFGroup.Size);
        Assert.AreEqual(0x608u, bssCOFFGroup.VirtualSize);
        Assert.AreEqual(0x200u, bssCOFFGroup.Section.Size);
        Assert.AreEqual(0x648u, bssCOFFGroup.Section.VirtualSize);
        Assert.AreEqual(".data", bssCOFFGroup.Section.Name);
        Assert.AreEqual(0x8u, crtXCACOFFGroup.Size);
        Assert.AreEqual(".rdata", crtXCACOFFGroup.Section.Name);
    }

    [TestMethod]
    public async Task Cpp32BitDllSectionsCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.Cpp32BitDllBinaryPath, this.Cpp32BitDllPDBPath, logger);
        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None);
        Assert.IsNotNull(sections);

        // Validating this against known good output from "link /dump /headers SizeBench.AnalysisEngine.Tests.Cpp32BitDll.dll"
        var textSection = (from s in sections where s.Name == ".text" select s).FirstOrDefault();
        var rdataSection = (from s in sections where s.Name == ".rdata" select s).FirstOrDefault();
        var dataSection = (from s in sections where s.Name == ".data" select s).FirstOrDefault();
        var relocSection = (from s in sections where s.Name == ".reloc" select s).FirstOrDefault();

        Assert.IsNotNull(textSection);
        Assert.IsNotNull(rdataSection);
        Assert.IsNotNull(dataSection);
        Assert.IsNotNull(relocSection);

        Assert.AreEqual(0x1600u, textSection.Size);
        Assert.IsTrue(textSection.COFFGroups.Count > 0);
        Assert.AreEqual(0xA00u, rdataSection.Size);
        Assert.IsTrue(rdataSection.COFFGroups.Count > 0);
        Assert.AreEqual(0x200u, relocSection.Size);
        Assert.AreEqual(0, relocSection.COFFGroups.Count);

        var textmnCOFFGroup = (from cg in textSection.COFFGroups where cg.Name == ".text$mn" select cg).FirstOrDefault();
        var xdataCOFFGroup = (from cg in rdataSection.COFFGroups where cg.Name == ".xdata" select cg).FirstOrDefault();
        var bssCOFFGroup = (from cg in dataSection.COFFGroups where cg.Name == ".bss" select cg).FirstOrDefault();
        var crtXCACOFFGroup = (from cg in rdataSection.COFFGroups where cg.Name == ".CRT$XCA" select cg).FirstOrDefault();
        var nonexistentCOFFGroup = (from cg in rdataSection.COFFGroups where cg.Name == ".CRT$ZZZ" select cg).FirstOrDefault();

        Assert.IsNotNull(textmnCOFFGroup);
        Assert.IsNull(xdataCOFFGroup); // xdata should not exist for 32-bit binaries
        Assert.IsNotNull(bssCOFFGroup);
        Assert.IsNotNull(crtXCACOFFGroup);
        Assert.IsNull(nonexistentCOFFGroup); // Negative test case - let's make sure not everything matches

        // Validating this against known good output from "link /dump /headers /coffgroup SizeBench.AnalysisEngine.Tests.CppDll.dll"
        Assert.AreEqual(0x1427u, textmnCOFFGroup.Size);
        Assert.AreEqual(".text", textmnCOFFGroup.Section.Name);
        Assert.AreEqual(0u, bssCOFFGroup.Size);
        Assert.AreEqual(0x374u, bssCOFFGroup.VirtualSize);
        Assert.AreEqual(".data", bssCOFFGroup.Section.Name);
        Assert.AreEqual(0x4u, crtXCACOFFGroup.Size);
        Assert.AreEqual(".rdata", crtXCACOFFGroup.Section.Name);
    }
}
