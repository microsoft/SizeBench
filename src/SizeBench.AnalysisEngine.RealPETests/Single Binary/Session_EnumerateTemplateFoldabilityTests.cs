using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[TestClass]
public sealed class Session_EnumerateTemplateFoldabilityTests
{
    public TestContext? TestContext { get; set; }
    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;
    private string MakePath(string binary) => Path.Combine(this.TestContext!.DeploymentDirectory, binary);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");
    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    [TestMethod]
    public async Task CppTestCasesBeforeTemplateFoldabilityCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var foldables = await session.EnumerateTemplateFoldabilityItems(this.CancellationToken);
        Assert.IsNotNull(foldables);

        Assert.AreEqual(2, foldables.Count);

        var vectorDestructor = foldables.Single(foldable => foldable.TemplateName == "std::vector<T1,T2>::~vector<T1,T2>()");

        Assert.AreEqual(4, vectorDestructor.Symbols.Count);
        Assert.AreEqual(3, vectorDestructor.UniqueSymbols.Count);
        Assert.AreEqual(65, Math.Floor(vectorDestructor.PercentageSimilarity * 100)); // Using a floor of the % * 100 to make it more stable than floating point comparison
        Assert.AreEqual(374u, vectorDestructor.TotalSize);
        Assert.AreEqual((uint)(374 * 0.652f), vectorDestructor.WastedSize);

        var vectorDestructorOfDouble = vectorDestructor.Symbols.OfType<SimpleFunctionCodeSymbol>().Single(s => s.Name == "std::vector<double,std::allocator<double> >::~vector<double,std::allocator<double> >()");
        Assert.IsFalse(vectorDestructorOfDouble.IsCOMDATFolded);
        Assert.AreEqual(vectorDestructorOfDouble.Name, vectorDestructorOfDouble.CanonicalName);

        var vectorDestructorOfXstack = vectorDestructor.Symbols.OfType<SimpleFunctionCodeSymbol>().Single(s => s.Name == "std::vector<xstack<int> *,std::allocator<xstack<int> *> >::~vector<xstack<int> *,std::allocator<xstack<int> *> >()");
        Assert.IsTrue(vectorDestructorOfXstack.IsCOMDATFolded);
        Assert.AreEqual(vectorDestructorOfDouble.Name, vectorDestructorOfXstack.CanonicalName);
    }
}
