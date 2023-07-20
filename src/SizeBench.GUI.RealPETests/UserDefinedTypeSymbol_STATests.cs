using System.IO;
using Nito.AsyncEx;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestInfrastructure;

namespace SizeBench.GUI.RealPETests;

// UserDefinedTypeSymbol does some fancy lazy-loading and previously ended up creating a deadlock between the DIA thread and the UI thread due to how
// Tasks were awaited.  Thus, this test class is in an STA and explicitly runs a test in a way that would simulate the deadlock, to ensure we don't
// do that if the Functions are accessed for the very first time from the UI thread before any other AnalysisEngine DIA thread code does so.

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[STATestClass]
public sealed class UserDefinedTypeSymbol_STATests
{

    public TestContext? TestContext { get; set; }
    public CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

    [Timeout(30 * 1000)]
    [STATestMethod]
    public void AccessingFunctionsForTheFirstTimeOnUIThreadDoesNotDeadlock()
    {
        AsyncContext.Run(async () =>
        {
            await using var session = await Session.Create(Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll"),
                                                           Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb"),
                                                           new NoOpLogger());

            var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken);

            var textSymbols = await session.EnumerateSymbolsInBinarySection(sections.Single(s => s.Name == ".text"), this.CancellationToken);

            var vector_Reallocate = (SimpleFunctionCodeSymbol)textSymbols.Single(sym => sym.Name == "std::vector<xstack<int> *,std::allocator<xstack<int> *> >::_Reallocate(unsigned int64)");

            var udt = (UserDefinedTypeSymbol)vector_Reallocate.ParentType!;

            // Simply accessing the Functions before anything else on the DIA thread has done so, should force them to lazy-load without deadlocking
            var functions = await udt.GetFunctionsAsync(this.CancellationToken);
            Assert.IsTrue(functions.Count > 1);
        });
    }
}
