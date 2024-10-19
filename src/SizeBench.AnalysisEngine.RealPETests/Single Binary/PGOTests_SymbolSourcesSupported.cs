using System.IO;
using SizeBench.TestInfrastructure;

namespace SizeBench.AnalysisEngine.RealPETests.Single_Binary;

[DeploymentItem(@"Test PEs\External\x64\Microsoft.UI.Xaml.dll")]
[DeploymentItem(@"Test PEs\External\x64\Microsoft.UI.Xaml.pdb")]
[DoNotParallelize]
[TestCategory(CommonTestCategories.SlowTests)]
[TestClass]
public sealed class PGOTests_SymbolSourcesSupported
{
    public TestContext? TestContext { get; set; }

    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

    public static IEnumerable<object[]> DynamicDataSourceForSymbolSourcesSupportedTests_Slimmed =>
    SymbolSourcesSupportedCommonTests.DynamicDataSourceForSymbolSourcesSupportedTests_Slimmed;

    [TestMethod]
    [DynamicData(nameof(DynamicDataSourceForSymbolSourcesSupportedTests_Slimmed))]
    public Task SymbolSourcesSupportedWorks(SymbolSourcesSupported symbolSources) =>
        SymbolSourcesSupportedCommonTests.VerifyNoUnexpectedSymbolTypesCanBeMaterialized(
            Path.Combine(this.TestContext!.DeploymentDirectory!, "Microsoft.UI.Xaml.dll"),
            Path.Combine(this.TestContext!.DeploymentDirectory!, "Microsoft.UI.Xaml.pdb"),
            symbolSources,
            this.CancellationToken);
}
