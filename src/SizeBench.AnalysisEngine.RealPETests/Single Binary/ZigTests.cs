using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests.Single_Binary;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Zig.exe")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Zig.pdb")]
[TestClass]
public sealed class ZigTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.Zig.exe");
    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.Zig.pdb");

    [TestMethod]
    public async Task Float128CanBeParsed()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.TestContext!.CancellationTokenSource.Token);
        var textSection = sections.Single(s => s.Name == ".text");

        var textSymbols = await session.EnumerateSymbolsInBinarySection(textSection, this.TestContext.CancellationTokenSource.Token);

        var fmaq = textSymbols.OfType<SimpleFunctionCodeSymbol>().Single(s => s.Name.Contains("fmaq", StringComparison.OrdinalIgnoreCase));

        Assert.AreEqual("__float128 fmaq(__float128, __float128, __float128)", fmaq.FullName);
        Assert.IsNull(fmaq.ArgumentNames);
        Assert.AreEqual(1, fmaq.Blocks.Count);
        Assert.IsTrue(fmaq.CanBeFolded);
        Assert.IsFalse(fmaq.IsCOMDATFolded);
        Assert.IsFalse(fmaq.IsIntroVirtual);
        Assert.IsFalse(fmaq.IsMemberFunction);
        Assert.IsTrue(fmaq.IsOptimizedForSpeed);
        Assert.IsFalse(fmaq.IsPGO);
        Assert.IsFalse(fmaq.IsPure);
        Assert.IsFalse(fmaq.IsSealed);
        Assert.IsFalse(fmaq.IsStatic);
        Assert.IsFalse(fmaq.IsVirtual);
        Assert.IsNull(fmaq.ParentType);

        // Now look at things about the function's type, to check we calculated the right size for __float128 params
        var fmaqType = fmaq.FunctionType;
        Assert.IsNotNull(fmaqType);
        Assert.IsNotNull(fmaqType.ArgumentTypes);
        Assert.AreEqual(3, fmaqType.ArgumentTypes.Count);
        Assert.AreEqual("__float128", fmaqType.ArgumentTypes[0].Name);
        Assert.AreEqual(16u, fmaqType.ArgumentTypes[0].InstanceSize);
        Assert.AreEqual("__float128", fmaqType.ReturnValueType.Name);
        Assert.AreEqual(16u, fmaqType.ReturnValueType.InstanceSize);
    }

    [TestMethod]
    public async Task ZigCompilandLanguageIsDetectedFromToolName()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);

        var compilands = await session.EnumerateCompilands(this.TestContext!.CancellationTokenSource.Token);
        var zigCompiland = compilands.Single(c => c.Name.Contains("SizeBenchV2.AnalysisEngine.Tests.Zig.exe.obj", StringComparison.Ordinal));

        Assert.AreEqual(ToolLanguage.Zig, zigCompiland.ToolLanguage);
        Assert.AreEqual("zig 0.11.0", zigCompiland.ToolName);
    }

    public static IEnumerable<object[]> DynamicDataSourceForSymbolSourcesSupportedTests => SymbolSourcesSupportedCommonTests.DynamicDataSourceForSymbolSourcesSupportedTests;

    [TestMethod]
    [DynamicData(nameof(DynamicDataSourceForSymbolSourcesSupportedTests))]
    public Task SymbolSourcesSupportedWorks(SymbolSourcesSupported symbolSources) =>
        SymbolSourcesSupportedCommonTests.VerifyNoUnexpectedSymbolTypesCanBeMaterialized(
            this.BinaryPath, this.PDBPath, symbolSources,
            this.TestContext!.CancellationTokenSource.Token);
}
