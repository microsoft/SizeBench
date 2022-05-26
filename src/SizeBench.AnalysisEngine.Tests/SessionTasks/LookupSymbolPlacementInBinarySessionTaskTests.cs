using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[TestClass]
public sealed class LookupSymbolPlacementInBinarySessionTaskTests : IDisposable
{
    private DiffTestDataGenerator _generator = new DiffTestDataGenerator();
    private SessionTaskParameters? SessionTaskParameters;
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();

    [TestInitialize]
    public void TestInitialize()
    {
        this._generator = new DiffTestDataGenerator();
        this.TestDIAAdapter = new TestDIAAdapter();
        this.SessionTaskParameters = new SessionTaskParameters(
            this._generator.MockBeforeSession.Object,
            this.TestDIAAdapter,
            this._generator.BeforeDataCache);

        this._generator.BeforeDataCache.AllBinarySections = this._generator.BeforeSections;
        this._generator.BeforeDataCache.AllLibs = this._generator.BeforeLibs;
        this._generator.BeforeDataCache.AllCompilands = this._generator.BeforeCompilands;
        this._generator.BeforeDataCache.AllCOFFGroups = this._generator.BeforeCOFFGroups;
        this._generator.BeforeDataCache.AllSourceFiles = new List<SourceFile>();
    }

    [TestMethod]
    public void SimpleLookupWorks()
    {
        var mockSymbolToLookup = new Mock<ISymbol>();
        mockSymbolToLookup.Setup(s => s.RVA).Returns(0);
        mockSymbolToLookup.Setup(s => s.RVAEnd).Returns(99);
        mockSymbolToLookup.Setup(s => s.Size).Returns(100);
        mockSymbolToLookup.Setup(s => s.VirtualSize).Returns(100);

        using var logger = new NoOpLogger();
        var output = new LookupSymbolPlacementInBinarySessionTask(mockSymbolToLookup.Object, parameters: this.SessionTaskParameters!, token: CancellationToken.None, progress: null).Execute(logger);

        Assert.AreEqual(this._generator.BeforeTextSection, output.BinarySection);
        Assert.AreEqual(this._generator.BeforeTextMnCG, output.COFFGroup);
        Assert.AreEqual(this._generator.BeforeALib, output.Lib);
        Assert.AreEqual(this._generator.BeforeA1Compiland, output.Compiland);
    }

    [TestMethod]
    public void LookupInMiddleOfImageWorks()
    {
        // Very similar to the "SimpleLookupWorks" test except it doesn't test RVA==0, which may be too easy

        var mockSymbolToLookup = new Mock<ISymbol>();
        mockSymbolToLookup.Setup(s => s.RVA).Returns(11000);
        mockSymbolToLookup.Setup(s => s.RVAEnd).Returns(11009);
        mockSymbolToLookup.Setup(s => s.Size).Returns(10);
        mockSymbolToLookup.Setup(s => s.VirtualSize).Returns(10);

        using var logger = new NoOpLogger();
        var output = new LookupSymbolPlacementInBinarySessionTask(mockSymbolToLookup.Object, parameters: this.SessionTaskParameters!, token: CancellationToken.None, progress: null).Execute(logger);

        Assert.IsTrue(ReferenceEquals(this._generator.BeforeRDataSection, output.BinarySection));
        Assert.IsTrue(ReferenceEquals(this._generator.BeforeRDataBefCG, output.COFFGroup));
        Assert.AreEqual(this._generator.BeforeALib, output.Lib);
        Assert.AreEqual(this._generator.BeforeA3Compiland, output.Compiland);
    }

    public void Dispose() => this._generator.Dispose();
}
