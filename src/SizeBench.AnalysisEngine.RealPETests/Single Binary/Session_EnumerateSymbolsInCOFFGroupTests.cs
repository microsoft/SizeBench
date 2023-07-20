using System.IO;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppDll.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppDll.pdb")]
[TestClass]
public sealed class Session_EnumerateSymbolsInCOFFGroupTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppDll.dll");

    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppDll.pdb");

    [TestMethod]
    public async Task CppDllSymbolsInCOFFGroupCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);

        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None);
        Assert.IsNotNull(sections);

        var textSection = (from s in sections where s.Name == ".text" select s).FirstOrDefault();
        Assert.IsNotNull(textSection);

        var textmnCOFFGroup = (from cg in textSection.COFFGroups where cg.Name == ".text$mn" select cg).FirstOrDefault();
        Assert.IsNotNull(textmnCOFFGroup);

        var symbolsInTextMn = await session.EnumerateSymbolsInCOFFGroup(textmnCOFFGroup, CancellationToken.None);
        Assert.IsNotNull(symbolsInTextMn);
    }
}
