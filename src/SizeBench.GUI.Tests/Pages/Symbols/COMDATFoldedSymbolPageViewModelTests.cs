using System.Globalization;
using Dia2Lib;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Pages.Symbols;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class COMDATFoldedSymbolPageViewModelTests : IDisposable
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

        var canonicalSymIndexId = this.Generator._nextSymIndexId++;
        var testSymIndexId = this.Generator._nextSymIndexId++;
        var thirdSymIndexId = this.Generator._nextSymIndexId++;

        var nameCanonicalization = new NameCanonicalization();
        nameCanonicalization.AddName(testSymIndexId, "test symbol", SymTagEnum.SymTagData);
        nameCanonicalization.AddName(canonicalSymIndexId, "canonicalName", SymTagEnum.SymTagData);
        nameCanonicalization.AddName(thirdSymIndexId, "thirdSymbol", SymTagEnum.SymTagData);
        nameCanonicalization.Canonicalize();
        this.Generator.DataCache.AllCanonicalNames!.Add(rva, nameCanonicalization);

        var sym = new StaticDataSymbol(this.Generator.DataCache, "test symbol", rva, size: 100, isVirtualSize: false, testSymIndexId, DataKind.DataIsFileStatic, type: null, referencedIn: null, functionParent: null);
        var canonicalSym = new StaticDataSymbol(this.Generator.DataCache, "canonicalName", rva, size: 100, isVirtualSize: false, canonicalSymIndexId, DataKind.DataIsFileStatic, type: null, referencedIn: null, functionParent: null);
        var thirdSym = new StaticDataSymbol(this.Generator.DataCache, "thirdSymbol", rva, size: 100, isVirtualSize: false, thirdSymIndexId, DataKind.DataIsFileStatic, type: null, referencedIn: null, functionParent: null);

        Assert.IsTrue(sym.IsCOMDATFolded);
        Assert.IsTrue(thirdSym.IsCOMDATFolded);
        Assert.IsFalse(canonicalSym.IsCOMDATFolded);

        var allSymsFolded = new List<ISymbol>() { sym, canonicalSym, thirdSym };

        this.Generator.MockSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(rva, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(allSymsFolded));

        var viewmodel = new COMDATFoldedSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                            this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "RVA", rva.ToString(CultureInfo.InvariantCulture) },
                { "Name", sym.Name }
            });
        await viewmodel.InitializeAsync();

        Assert.AreEqual(sym, viewmodel.Symbol);
        Assert.AreEqual("Symbol: test symbol", viewmodel.PageTitle);
        Assert.AreEqual(3, viewmodel.FoldedSymbols.Count);
        CollectionAssert.AreEquivalent(allSymsFolded, viewmodel.FoldedSymbols);
        Assert.AreEqual(canonicalSym, viewmodel.CanonicalSymbol);
    }

    public void Dispose() => this.Generator.Dispose();
}
