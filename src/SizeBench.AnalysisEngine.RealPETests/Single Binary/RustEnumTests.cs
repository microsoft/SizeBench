using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests.Single_Binary;

[DeploymentItem(@"Test PEs\SizeBenchV2_AnalysisEngine_Tests_Rust.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2_AnalysisEngine_Tests_Rust.pdb")]
[TestClass]
public class RustEnumTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory, filename);

    private string BinaryPath => MakePath("SizeBenchV2_AnalysisEngine_Tests_Rust.dll");
    private string PDBPath => MakePath("SizeBenchV2_AnalysisEngine_Tests_Rust.pdb");

    [TestMethod]
    public async Task RustEnumsWithMethodsCanBeParsed()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.TestContext!.CancellationTokenSource.Token);
        var textSection = sections.Single(s => s.Name == ".text");

        var textSymbols = await session.EnumerateSymbolsInBinarySection(textSection, this.TestContext.CancellationTokenSource.Token);

        var testRustEnumWithMethod_Call = textSymbols.OfType<SimpleFunctionCodeSymbol>().Single(s => s.Name.Contains("TestRustEnumWithMethod::call", StringComparison.OrdinalIgnoreCase));

        Assert.IsNull(testRustEnumWithMethod_Call.ArgumentNames);
        Assert.AreEqual(1, testRustEnumWithMethod_Call.Blocks.Count);
        Assert.IsTrue(testRustEnumWithMethod_Call.CanBeFolded);
        Assert.IsFalse(testRustEnumWithMethod_Call.IsCOMDATFolded);
        Assert.IsFalse(testRustEnumWithMethod_Call.IsIntroVirtual);
        Assert.IsTrue(testRustEnumWithMethod_Call.IsMemberFunction);
        Assert.IsFalse(testRustEnumWithMethod_Call.IsOptimizedForSpeed);
        Assert.IsFalse(testRustEnumWithMethod_Call.IsPGO);
        Assert.IsFalse(testRustEnumWithMethod_Call.IsPure);
        Assert.IsFalse(testRustEnumWithMethod_Call.IsSealed);
        Assert.IsFalse(testRustEnumWithMethod_Call.IsStatic);
        Assert.IsFalse(testRustEnumWithMethod_Call.IsVirtual);
        Assert.IsNotNull(testRustEnumWithMethod_Call.ParentType);
        Assert.IsInstanceOfType(testRustEnumWithMethod_Call.ParentType, typeof(EnumTypeSymbol));
    }
}
