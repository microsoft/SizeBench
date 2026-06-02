using System.IO;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace PEParser.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Cpp17.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.Cpp17.pdb")]
[TestClass]
public class Cpp17Tests
{
    public TestContext? TestContext { get; set; }

    public string Cpp17BinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.Cpp17.dll");
    public string Cpp17PdbPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.Cpp17.pdb");

    [TestMethod]
    public async Task Cpp17XDATACanBeParsed()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.Cpp17BinaryPath, this.Cpp17PdbPath, logger);
        // This tests an unwind symbol that uses __GSHandlerCheck_SEH_noexcept (for Structured Exception Handling with /GS data)
        var Cpp17_CppxdataUsage_dtorUnwind = (UnwindInfoSymbol)session.DataCache.XDataSymbolsByRVA[0x35A8];
        Assert.AreEqual(0x1080u, Cpp17_CppxdataUsage_dtorUnwind.TargetStartRVA);
        Assert.AreEqual(52u, Cpp17_CppxdataUsage_dtorUnwind.Size);
        Assert.IsTrue(Cpp17_CppxdataUsage_dtorUnwind.Name.Contains("Cpp17_CppxdataUsage::~Cpp17_CppxdataUsage", StringComparison.Ordinal));
        Assert.IsTrue(Cpp17_CppxdataUsage_dtorUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(Cpp17_CppxdataUsage_dtorUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        // This tests an unwind symbol that uses __C_specific_handler_noexcept
        var FreeFunctionUsingSEHUnwind = (UnwindInfoSymbol)session.DataCache.XDataSymbolsByRVA[0x3568];
        Assert.AreEqual(0x1000u, FreeFunctionUsingSEHUnwind.TargetStartRVA);
        Assert.AreEqual(48u, FreeFunctionUsingSEHUnwind.Size);
        Assert.IsTrue(FreeFunctionUsingSEHUnwind.Name.Contains("FreeFunctionUsingSEH", StringComparison.Ordinal));
        Assert.IsTrue(FreeFunctionUsingSEHUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(FreeFunctionUsingSEHUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind
    }
}
