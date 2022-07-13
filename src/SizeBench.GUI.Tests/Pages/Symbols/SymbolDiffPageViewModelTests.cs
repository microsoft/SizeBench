using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Pages.Symbols;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class SymbolDiffPageViewModelTests : IDisposable
{
    private DiffTestDataGenerator Generator = new DiffTestDataGenerator();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.Generator = new DiffTestDataGenerator();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task SymbolDiffLoadsBothPlacements()
    {
        var allSymbolDiffsInBinarySectionDiff = this.Generator.GenerateSymbolDiffsInBinarySectionList(this.Generator.TextSectionDiff);
        var symbolDiffToFind = allSymbolDiffsInBinarySectionDiff.First(sd => sd.BeforeSymbol != null && sd.AfterSymbol != null);

        var beforePlacement = new SymbolPlacement(this.Generator.BeforeTextSection, this.Generator.BeforeTextMnCG, this.Generator.BeforeALib, this.Generator.BeforeA1Compiland, null);
        var afterPlacement = new SymbolPlacement(this.Generator.AfterTextSection, this.Generator.AfterTextZzCG, this.Generator.AfterBLib, this.Generator.AfterB1Compiland, null);

        this.Generator.MockDiffSession.Setup(s => s.LoadSymbolDiffByBeforeAndAfterRVA(symbolDiffToFind.BeforeSymbol!.RVA, symbolDiffToFind.AfterSymbol!.RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<SymbolDiff?>(symbolDiffToFind));
        this.Generator.MockBeforeSession.Setup(s => s.LookupSymbolPlacementInBinary(symbolDiffToFind.BeforeSymbol!, It.IsAny<CancellationToken>())).Returns(Task.FromResult(beforePlacement));
        this.Generator.MockAfterSession.Setup(s => s.LookupSymbolPlacementInBinary(symbolDiffToFind.AfterSymbol!, It.IsAny<CancellationToken>())).Returns(Task.FromResult(afterPlacement));

        this.Generator.MockDiffSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroupDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.Generator.BinarySectionDiffs as IReadOnlyList<BinarySectionDiff>));
        this.Generator.MockDiffSession.Setup(s => s.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.Generator.LibDiffs as IReadOnlyList<LibDiff>));

        var viewmodel = new SymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                    this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BeforeRVA", symbolDiffToFind.BeforeSymbol!.RVA.ToString(CultureInfo.InvariantCulture) },
                { "AfterRVA", symbolDiffToFind.AfterSymbol!.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.DoesBeforeSymbolExist);
        Assert.IsTrue(viewmodel.DoesAfterSymbolExist);
        Assert.AreEqual(symbolDiffToFind, viewmodel.SymbolDiff);
        Assert.AreEqual(this.Generator.TextSectionDiff.BeforeSection, viewmodel.BeforePlacement!.BinarySection);
        Assert.AreEqual(this.Generator.TextMnCGDiff.BeforeCOFFGroup, viewmodel.BeforePlacement.COFFGroup);
        Assert.AreEqual(this.Generator.ALibDiff.BeforeLib, viewmodel.BeforePlacement.Lib);
        Assert.AreEqual(this.Generator.A1CompilandDiff.BeforeCompiland, viewmodel.BeforePlacement.Compiland);
        Assert.AreEqual(this.Generator.TextSectionDiff.AfterSection, viewmodel.AfterPlacement!.BinarySection);
        Assert.AreEqual(this.Generator.TextZzCGDiff.AfterCOFFGroup, viewmodel.AfterPlacement.COFFGroup);
        Assert.AreEqual(this.Generator.BLibDiff.AfterLib, viewmodel.AfterPlacement.Lib);
        Assert.AreEqual(this.Generator.B1CompilandDiff.AfterCompiland, viewmodel.AfterPlacement.Compiland);
        Assert.AreEqual($"Symbol Diff: {symbolDiffToFind.Name}", viewmodel.PageTitle);
    }

    [TestMethod]
    public async Task SymbolDiffWithOnlyBeforeLoadsPlacement()
    {
        var allSymbolDiffsInBinarySectionDiff = this.Generator.GenerateSymbolDiffsInBinarySectionList(this.Generator.TextSectionDiff);
        var symbolDiffToFind = allSymbolDiffsInBinarySectionDiff.First(sd => sd.BeforeSymbol != null && sd.AfterSymbol is null);

        var beforePlacement = new SymbolPlacement(this.Generator.BeforeTextSection, this.Generator.BeforeTextMnCG, this.Generator.BeforeALib, this.Generator.BeforeA1Compiland, null);

        this.Generator.MockDiffSession.Setup(s => s.LoadSymbolDiffByBeforeAndAfterRVA(symbolDiffToFind.BeforeSymbol!.RVA, null, It.IsAny<CancellationToken>())).Returns(Task.FromResult<SymbolDiff?>(symbolDiffToFind));
        this.Generator.MockBeforeSession.Setup(s => s.LookupSymbolPlacementInBinary(symbolDiffToFind.BeforeSymbol!, It.IsAny<CancellationToken>())).Returns(Task.FromResult(beforePlacement));

        this.Generator.MockDiffSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroupDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.Generator.BinarySectionDiffs as IReadOnlyList<BinarySectionDiff>));
        this.Generator.MockDiffSession.Setup(s => s.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.Generator.LibDiffs as IReadOnlyList<LibDiff>));

        var viewmodel = new SymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                    this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BeforeRVA", symbolDiffToFind.BeforeSymbol!.RVA.ToString(CultureInfo.InvariantCulture) },
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.DoesBeforeSymbolExist);
        Assert.IsFalse(viewmodel.DoesAfterSymbolExist);
        Assert.AreEqual(symbolDiffToFind, viewmodel.SymbolDiff);
        Assert.AreEqual(this.Generator.TextSectionDiff.BeforeSection, viewmodel.BeforePlacement!.BinarySection);
        Assert.AreEqual(this.Generator.TextMnCGDiff.BeforeCOFFGroup, viewmodel.BeforePlacement.COFFGroup);
        Assert.AreEqual(this.Generator.ALibDiff.BeforeLib, viewmodel.BeforePlacement.Lib);
        Assert.AreEqual(this.Generator.A1CompilandDiff.BeforeCompiland, viewmodel.BeforePlacement.Compiland);
        Assert.IsNull(viewmodel.AfterPlacement);
        Assert.AreEqual($"Symbol Diff: {symbolDiffToFind.Name}", viewmodel.PageTitle);
    }

    [TestMethod]
    public async Task SymbolDiffWithOnlyAfterLoadsPlacement()
    {
        var allSymbolDiffsInBinarySectionDiff = this.Generator.GenerateSymbolDiffsInBinarySectionList(this.Generator.TextSectionDiff);
        var symbolDiffToFind = allSymbolDiffsInBinarySectionDiff.First(sd => sd.BeforeSymbol is null && sd.AfterSymbol != null);

        var afterPlacement = new SymbolPlacement(this.Generator.AfterTextSection, this.Generator.AfterTextZzCG, this.Generator.AfterBLib, this.Generator.AfterB1Compiland, null);

        this.Generator.MockDiffSession.Setup(s => s.LoadSymbolDiffByBeforeAndAfterRVA(null, symbolDiffToFind.AfterSymbol!.RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<SymbolDiff?>(symbolDiffToFind));
        this.Generator.MockAfterSession.Setup(s => s.LookupSymbolPlacementInBinary(symbolDiffToFind.AfterSymbol!, It.IsAny<CancellationToken>())).Returns(Task.FromResult(afterPlacement));

        this.Generator.MockDiffSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroupDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.Generator.BinarySectionDiffs as IReadOnlyList<BinarySectionDiff>));
        this.Generator.MockDiffSession.Setup(s => s.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.Generator.LibDiffs as IReadOnlyList<LibDiff>));

        var viewmodel = new SymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                    this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "AfterRVA", symbolDiffToFind.AfterSymbol!.RVA.ToString(CultureInfo.InvariantCulture) },
            });
        await viewmodel.InitializeAsync();

        Assert.IsFalse(viewmodel.DoesBeforeSymbolExist);
        Assert.IsTrue(viewmodel.DoesAfterSymbolExist);
        Assert.AreEqual(symbolDiffToFind, viewmodel.SymbolDiff);
        Assert.IsNull(viewmodel.BeforePlacement);
        Assert.AreEqual(this.Generator.TextSectionDiff.AfterSection, viewmodel.AfterPlacement!.BinarySection);
        Assert.AreEqual(this.Generator.TextZzCGDiff.AfterCOFFGroup, viewmodel.AfterPlacement.COFFGroup);
        Assert.AreEqual(this.Generator.BLibDiff.AfterLib, viewmodel.AfterPlacement.Lib);
        Assert.AreEqual(this.Generator.B1CompilandDiff.AfterCompiland, viewmodel.AfterPlacement.Compiland);
        Assert.AreEqual($"Symbol Diff: {symbolDiffToFind.Name}", viewmodel.PageTitle);
    }

    public void Dispose() => this.Generator.Dispose();
}
