using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CodePageWin32Rsrc.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CodePageWin32Rsrc.pdb")]
[TestClass]
public sealed class RsrcTests
{
    public TestContext? TestContext { get; set; }
    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CodePageWin32Rsrc.dll");

    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CodePageWin32Rsrc.pdb");

    [TestMethod]
    public async Task Win32ResourcesCanBeParsedForLANG_NEUTRAL_SUBLANG_DEFAULT()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken);
        var rsrcSection = sections.Single(section => section.Name == ".rsrc");
        var rsrcSymbols = await session.EnumerateSymbolsInBinarySection(rsrcSection, this.CancellationToken);

        // The ICON in here should be "LANG_NEUTRAL" even though the ID we find is 0x400 (LANGUAGE_NEUTRAL / SUBLANG_DEFAULT)
        var iconGroup = rsrcSymbols.OfType<RsrcGroupIconDataSymbol>().Single();

        StringAssert.Contains(iconGroup.Name, "LANG_NEUTRAL", StringComparison.Ordinal);
    }
}
