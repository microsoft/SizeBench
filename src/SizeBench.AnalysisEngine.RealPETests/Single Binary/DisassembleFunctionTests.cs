using System.IO;
using Nito.AsyncEx;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestInfrastructure;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\External\x64\ReactNativeXaml.dll")]
[DeploymentItem(@"Test PEs\External\x64\ReactNativeXaml.pdb")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[DeploymentItem(@"Single Binary\Disassemblies\EventInfo_lambda1071_operator()_Disassembly.txt")]
[DeploymentItem(@"Single Binary\Disassemblies\asmVeryLongBasicBlock_Disassembly.txt")]
[TestCategory(CommonTestCategories.SlowTests)]
[STATestClass]
public sealed class DisassembleFunctionTests
{
    public TestContext? TestContext { get; set; }

    private string MakePath(string binary) => Path.Combine(this.TestContext!.DeploymentDirectory!, binary);

    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

    // This function in ReactNativeXaml.dll exposes bugs in DbgX, so trying to disassemble it ensures we correctly work around that
    [Timeout(90 * 1000)]
    [STATestMethod]
    public void ReactNativeXamlNoReturnFunctionDisassemblesCorrectly()
    {
        AsyncContext.Run(async () =>
        {
            using var logger = new NoOpLogger();
            await using var session = await Session.Create(MakePath("ReactNativeXaml.dll"), MakePath("ReactNativeXaml.pdb"), logger);
            var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken);
            var symbolsInText = await session.EnumerateSymbolsInBinarySection(sections.Single(s => s.Name == ".text"), this.CancellationToken);
            var lambda1071Function = symbolsInText.OfType<SimpleFunctionCodeSymbol>().Single(s => s.FormattedName.IncludeParentType == "EventInfo::<lambda_1071>::operator()");
            Assert.AreEqual(lambda1071Function.Name, lambda1071Function.CanonicalName);

            var options = new DisassembleFunctionOptions();
            var disassembly = await session.DisassembleFunction(lambda1071Function, options, this.CancellationToken);
            VerifyDisassembly(disassembly, @"EventInfo_lambda1071_operator()_Disassembly.txt");
        });

        // Force GC since these big binaries create so much memory pressure in the ADO pipelines
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
    }

    // This tests a basic block with more than 100 instructions in it, which triggers the code in DisassembleFunction that handles basic blocks too long to get all instructions in one query to DbgX
    [Timeout(30 * 1000)]
    [STATestMethod]
    public void VeryLongBasicBlockDisassemblesCorrectly()
    {
        AsyncContext.Run(async () =>
        {
            using var logger = new NoOpLogger();
            await using var session = await Session.Create(MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll"), MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb"), logger);
            var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken);
            var symbolsInText = await session.EnumerateSymbolsInBinarySection(sections.Single(s => s.Name == ".text"), this.CancellationToken);
            var asmVeryLongBasicBlockFunction = symbolsInText.OfType<SimpleFunctionCodeSymbol>().Single(s => s.FormattedName.IncludeParentType == "asmVeryLongBasicBlock");
            Assert.AreEqual(asmVeryLongBasicBlockFunction.Name, asmVeryLongBasicBlockFunction.CanonicalName);

            var options = new DisassembleFunctionOptions();
            var disassembly = await session.DisassembleFunction(asmVeryLongBasicBlockFunction, options, this.CancellationToken);
            VerifyDisassembly(disassembly, @"asmVeryLongBasicBlock_Disassembly.txt");
        });

        // Force GC since these big binaries create so much memory pressure in the ADO pipelines
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
    }

    private void VerifyDisassembly(string disassembly, string filenameOfExpectedOutput)
    {
        var expectedLines = File.ReadAllText(MakePath(filenameOfExpectedOutput))
                                .Trim()
                                .Replace("\r\n", "\r", StringComparison.OrdinalIgnoreCase)
                                .Replace("\n\r", "\r", StringComparison.OrdinalIgnoreCase)
                                .Replace("\n", "\r", StringComparison.OrdinalIgnoreCase)
                                .Split('\r', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var actualLines = disassembly.Trim().Replace("\r\n", "\r", StringComparison.OrdinalIgnoreCase)
                                            .Replace("\n\r", "\r", StringComparison.OrdinalIgnoreCase)
                                            .Replace("\n", "\r", StringComparison.OrdinalIgnoreCase)
                                            .Split('\r', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.AreEqual(expectedLines.Length, actualLines.Length);
        
        for(var i = 0; i < expectedLines.Length; i++)
        {
            Assert.AreEqual(expectedLines[i].Trim(), actualLines[i].Trim(), $"Line {i} of the disassembly differed.  Expected: '{expectedLines[i].Trim()}', Actual: '{actualLines[i].Trim()}'");
        }
    }
}
