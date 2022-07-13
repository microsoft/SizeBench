using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Pages.Symbols;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class SymbolPageViewModelTests : IDisposable
{
    public SingleBinaryDataGenerator Generator = new SingleBinaryDataGenerator();
    public Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.Generator = new SingleBinaryDataGenerator();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task SymbolPassesThrough()
    {
        const int rva = 5798;
        var sym = new PublicSymbol(this.Generator.DataCache, "test sym", rva: rva, size: 50, isVirtualSize: false, symIndexId: 0, targetRva: 0);

        this.Generator.MockSession.Setup(s => s.LoadSymbolByRVA(rva)).Returns(Task.FromResult<ISymbol?>(sym));
        var tcsPlacement = new TaskCompletionSource<SymbolPlacement>();
        tcsPlacement.SetCanceled();
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(sym, It.IsAny<CancellationToken>())).Returns(tcsPlacement.Task);

        var viewmodel = new SymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "RVA", rva.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.DoesSymbolExist);
        Assert.AreEqual(sym, viewmodel.Symbol);
        Assert.IsNull(viewmodel.BinarySection);
        Assert.IsNull(viewmodel.COFFGroup);
        Assert.IsNull(viewmodel.Lib);
        Assert.IsNull(viewmodel.Compiland);
        Assert.AreEqual("Symbol: test sym", viewmodel.PageTitle);
    }

    [TestMethod]
    public async Task SymbolPlacementLookupWorks()
    {
        const int rva = 5798;
        var sym = new PublicSymbol(this.Generator.DataCache, "a different test sym", rva: rva, size: 50, isVirtualSize: false, symIndexId: 0, targetRva: 0);

        var placement = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextMnCG, this.Generator.ALib, this.Generator.A1Compiland, this.Generator.A1CppSourceFile);

        this.Generator.MockSession.Setup(s => s.LoadSymbolByRVA(rva)).Returns(Task.FromResult<ISymbol?>(sym));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(sym, It.IsAny<CancellationToken>())).Returns(Task.FromResult(placement));

        var viewmodel = new SymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "RVA", rva.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.DoesSymbolExist);
        Assert.AreEqual(sym, viewmodel.Symbol);
        Assert.AreEqual(this.Generator.TextSection, viewmodel.BinarySection);
        Assert.AreEqual(this.Generator.TextMnCG, viewmodel.COFFGroup);
        Assert.AreEqual(this.Generator.ALib, viewmodel.Lib);
        Assert.AreEqual(this.Generator.A1Compiland, viewmodel.Compiland);
        Assert.AreEqual(this.Generator.A1CppSourceFile, viewmodel.SourceFile);
        Assert.AreEqual("Symbol: a different test sym", viewmodel.PageTitle);
    }

    [TestMethod]
    public async Task NonexistentSymbolDoesItsBest()
    {
        var viewmodel = new SymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "RVA", "0" },
                { "Name", "Rando Symbol" }
            });
        await viewmodel.InitializeAsync();

        Assert.IsFalse(viewmodel.DoesSymbolExist);
        Assert.AreEqual("Rando Symbol", viewmodel.NameOfNonexistentSymbol);
        Assert.IsNull(viewmodel.Symbol);
        Assert.IsNull(viewmodel.BinarySection);
        Assert.IsNull(viewmodel.COFFGroup);
        Assert.IsNull(viewmodel.Lib);
        Assert.IsNull(viewmodel.Compiland);
        Assert.AreEqual("Symbol: Rando Symbol", viewmodel.PageTitle);
    }

    public void Dispose() => this.Generator.Dispose();
}
