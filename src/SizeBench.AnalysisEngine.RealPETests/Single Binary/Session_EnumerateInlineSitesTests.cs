using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.InlineSites.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.InlineSites.pdb")]
[TestClass]
public sealed class Session_EnumerateInlineSitesTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.InlineSites.dll");

    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.InlineSites.pdb");

    [TestMethod]
    public async Task InlineSitesCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);

        var inlineSites = await session.EnumerateAllInlineSites(this.TestContext!.CancellationTokenSource.Token);

        Assert.AreEqual(3, inlineSites.Count);

        var forceInlinedFunction = inlineSites.Single(x => x.Name == "forceInlinedFunction");
        Assert.AreEqual("DllMain(HINSTANCE__*, unsigned long, void*)", forceInlinedFunction.BlockInlinedInto.Name);
        Assert.IsTrue(ReferenceEquals(forceInlinedFunction.BlockInlinedInto, forceInlinedFunction.CanonicalSymbolInlinedInto));
        Assert.AreEqual(2, forceInlinedFunction.RVARanges.Count());
    }

    [TestMethod]
    public async Task InlineSitesCanBeFoundForOneFunction()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);

        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.TestContext!.CancellationTokenSource.Token);
        var textSection = sections.Single(x => x.Name == ".text");
        var textSectionSymbols = await session.EnumerateSymbolsInBinarySection(textSection, this.TestContext.CancellationTokenSource.Token);
        var dllMainFunction = textSectionSymbols.OfType<SimpleFunctionCodeSymbol>().Single(x => x.Name == "DllMain(HINSTANCE__*, unsigned long, void*)");

        var inlineSites = await session.EnumerateAllInlineSitesInFunction(dllMainFunction, this.TestContext.CancellationTokenSource.Token);

        Assert.AreEqual(2, inlineSites.Count);

        var forceInlinedFunction = inlineSites.Single(x => x.Name == "forceInlinedFunction");
        Assert.AreEqual("DllMain(HINSTANCE__*, unsigned long, void*)", forceInlinedFunction.BlockInlinedInto.Name);
        Assert.IsTrue(ReferenceEquals(forceInlinedFunction.BlockInlinedInto, forceInlinedFunction.CanonicalSymbolInlinedInto));
        Assert.AreEqual(2, forceInlinedFunction.RVARanges.Count());

        var anotherForceInlinedFunction = inlineSites.Single(x => x.Name == "anotherForceInlinedFunction");
        Assert.AreEqual("DllMain(HINSTANCE__*, unsigned long, void*)", forceInlinedFunction.BlockInlinedInto.Name);
        Assert.IsTrue(ReferenceEquals(forceInlinedFunction.BlockInlinedInto, forceInlinedFunction.CanonicalSymbolInlinedInto));
        Assert.AreEqual(2, forceInlinedFunction.RVARanges.Count());
    }
}
