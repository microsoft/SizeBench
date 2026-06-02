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
public sealed class BlockSymbolPageViewModelTests : IDisposable
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
    public async Task BlockLoadsEvenIfPlacementIsCanceled()
    {
        const int rva = 5798;

        var function = BuildComplexFunction("testName", primaryBlockRva: rva);

        this.Generator.MockSession.Setup(s => s.LoadSymbolByRVA(rva)).Returns(Task.FromResult<ISymbol?>(function.PrimaryBlock));
        var tcsPlacement = new TaskCompletionSource<SymbolPlacement>();
        tcsPlacement.SetCanceled();
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(function.PrimaryBlock, It.IsAny<CancellationToken>())).Returns(tcsPlacement.Task);
        this.Generator.MockSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(rva, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(new List<ISymbol>() { function.PrimaryBlock }));

        var viewmodel = new BlockSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                     this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "RVA", rva.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsFalse(viewmodel.IsBlockCodeUsedForMultipleBlocks);
        Assert.IsNull(viewmodel.FoldedBlocks);
        Assert.AreEqual(function.PrimaryBlock, viewmodel.Block);
        Assert.IsNull(viewmodel.BinarySection);
        Assert.IsNull(viewmodel.COFFGroup);
        Assert.IsNull(viewmodel.Compiland);
        Assert.IsNull(viewmodel.Lib);
        Assert.IsNull(viewmodel.SourceFile);
        Assert.AreEqual("Block Symbol: Block of code in testName()", viewmodel.PageTitle);
        Assert.IsFalse(viewmodel.IsSeparatedBlock);
        Assert.AreEqual(function, viewmodel.ParentFunction);
    }

    [TestMethod]
    public async Task PrimaryBlockWorks()
    {
        const int rva = 5798;

        var function = BuildComplexFunction("testName", primaryBlockRva: rva);

        var primaryPlacement = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextMnCG, this.Generator.ALib, this.Generator.A1Compiland, this.Generator.A1CppSourceFile);
        var separatedPlacement1 = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextZzCG, this.Generator.BLib, this.Generator.B1Compiland, this.Generator.A1CppSourceFile);
        var separatedPlacement2 = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextMnCG, this.Generator.ALib, this.Generator.A1Compiland, this.Generator.XHSourceFile);

        this.Generator.MockSession.Setup(s => s.LoadSymbolByRVA(rva)).Returns(Task.FromResult<ISymbol?>(function.PrimaryBlock));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(function.PrimaryBlock, It.IsAny<CancellationToken>())).Returns(Task.FromResult(primaryPlacement));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(function.SeparatedBlocks[0], It.IsAny<CancellationToken>())).Returns(Task.FromResult(separatedPlacement1));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(function.SeparatedBlocks[1], It.IsAny<CancellationToken>())).Returns(Task.FromResult(separatedPlacement2));
        this.Generator.MockSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(rva, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(new List<ISymbol>()));

        var viewmodel = new BlockSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                     this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "RVA", rva.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsFalse(viewmodel.IsBlockCodeUsedForMultipleBlocks);
        Assert.IsNull(viewmodel.FoldedBlocks);
        Assert.AreEqual(function.PrimaryBlock, viewmodel.Block);
        Assert.AreEqual(this.Generator.TextSection, viewmodel.BinarySection);
        Assert.AreEqual(this.Generator.TextMnCG, viewmodel.COFFGroup);
        Assert.AreEqual(this.Generator.A1Compiland, viewmodel.Compiland);
        Assert.AreEqual(this.Generator.ALib, viewmodel.Lib);
        Assert.AreEqual(this.Generator.A1CppSourceFile, viewmodel.SourceFile);
        Assert.AreEqual("Block Symbol: Block of code in testName()", viewmodel.PageTitle);
        Assert.IsFalse(viewmodel.IsSeparatedBlock);
        Assert.AreEqual(function, viewmodel.ParentFunction);
    }

    [TestMethod]
    public async Task SeparatedBlockWorks()
    {
        const int rva = 5798;

        var function = BuildComplexFunction("testName", primaryBlockRva: rva);

        var primaryPlacement = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextMnCG, this.Generator.ALib, this.Generator.A1Compiland, this.Generator.A1CppSourceFile);
        var separatedPlacement1 = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextZzCG, this.Generator.BLib, this.Generator.B1Compiland, this.Generator.A1CppSourceFile);
        var separatedPlacement2 = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextMnCG, this.Generator.ALib, this.Generator.A1Compiland, this.Generator.XHSourceFile);

        this.Generator.MockSession.Setup(s => s.LoadSymbolByRVA(function.SeparatedBlocks[0].RVA)).Returns(Task.FromResult<ISymbol?>(function.SeparatedBlocks[0]));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(function.PrimaryBlock, It.IsAny<CancellationToken>())).Returns(Task.FromResult(primaryPlacement));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(function.SeparatedBlocks[0], It.IsAny<CancellationToken>())).Returns(Task.FromResult(separatedPlacement1));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(function.SeparatedBlocks[1], It.IsAny<CancellationToken>())).Returns(Task.FromResult(separatedPlacement2));
        this.Generator.MockSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(function.SeparatedBlocks[0].RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(new List<ISymbol>()));

        var viewmodel = new BlockSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                     this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "RVA", function.SeparatedBlocks[0].RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsFalse(viewmodel.IsBlockCodeUsedForMultipleBlocks);
        Assert.IsNull(viewmodel.FoldedBlocks);
        Assert.AreEqual(function.SeparatedBlocks[0], viewmodel.Block);
        Assert.AreEqual(this.Generator.TextSection, viewmodel.BinarySection);
        Assert.AreEqual(this.Generator.TextZzCG, viewmodel.COFFGroup);
        Assert.AreEqual(this.Generator.B1Compiland, viewmodel.Compiland);
        Assert.AreEqual(this.Generator.BLib, viewmodel.Lib);
        Assert.AreEqual(this.Generator.A1CppSourceFile, viewmodel.SourceFile);
        Assert.AreEqual("Block Symbol: Block of code in testName()", viewmodel.PageTitle);
        Assert.IsTrue(viewmodel.IsSeparatedBlock);
        Assert.AreEqual(function, viewmodel.ParentFunction);
    }

    [TestMethod]
    public async Task FoldedBlocksFoundCorrectly()
    {
        var function = BuildComplexFunction("testName", primaryBlockRva: 5798);

        var primaryBlockOfFoldedBlock1 = new PrimaryCodeBlockSymbol(this.Generator.DataCache, rva: function.PrimaryBlock.RVA + 10000, size: 50u, symIndexId: this.Generator._nextSymIndexId++);
        var foldedBlock1 = new SeparatedCodeBlockSymbol(this.Generator.DataCache, function.SeparatedBlocks[0].RVA, function.SeparatedBlocks[0].Size, this.Generator._nextSymIndexId++, primaryBlockOfFoldedBlock1.SymIndexId);
        var foldedFunction1 = new ComplexFunctionCodeSymbol(this.Generator.DataCache, "CFoo::XYZ", primaryBlockOfFoldedBlock1, new List<SeparatedCodeBlockSymbol>() { foldedBlock1 }, isPGO: true);

        var primaryBlockOfFoldedBlock2 = new PrimaryCodeBlockSymbol(this.Generator.DataCache, rva: function.PrimaryBlock.RVA + 20000, size: 50u, symIndexId: this.Generator._nextSymIndexId++);
        var foldedBlock2 = new SeparatedCodeBlockSymbol(this.Generator.DataCache, function.SeparatedBlocks[0].RVA, function.SeparatedBlocks[0].Size, this.Generator._nextSymIndexId++, primaryBlockOfFoldedBlock2.SymIndexId);
        var foldedFunction2 = new ComplexFunctionCodeSymbol(this.Generator.DataCache, "FunctionInAnotherPlaceEntirely", primaryBlockOfFoldedBlock2, new List<SeparatedCodeBlockSymbol>() { foldedBlock2 }, isPGO: true);

        var allBlocksAtThisRVA = new List<ISymbol>() { function.SeparatedBlocks[0], foldedBlock1, foldedBlock2 };

        var nameCanonicalization = new NameCanonicalization();
        nameCanonicalization.AddName(function.SeparatedBlocks[0].SymIndexId, SymTagEnum.SymTagBlock, name: function.SeparatedBlocks[0].Name);
        nameCanonicalization.AddName(foldedBlock1.SymIndexId, SymTagEnum.SymTagBlock, name: foldedBlock1.Name);
        nameCanonicalization.AddName(foldedBlock2.SymIndexId, SymTagEnum.SymTagBlock, name: foldedBlock2.Name);
        nameCanonicalization.Canonicalize();
        this.Generator.DataCache.AllCanonicalNames = new SortedList<uint, NameCanonicalization>
            {
                { function.SeparatedBlocks[0].RVA, nameCanonicalization }
            };

        var placement = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextMnCG, this.Generator.BLib, this.Generator.B1Compiland, this.Generator.XHSourceFile);

        this.Generator.MockSession.Setup(s => s.LoadSymbolByRVA(function.SeparatedBlocks[0].RVA)).Returns(Task.FromResult<ISymbol?>(function.SeparatedBlocks[0]));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(function.SeparatedBlocks[0], It.IsAny<CancellationToken>())).Returns(Task.FromResult(placement));
        this.Generator.MockSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(function.SeparatedBlocks[0].RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(allBlocksAtThisRVA));

        var viewmodel = new BlockSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                     this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "RVA", function.SeparatedBlocks[0].RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.IsBlockCodeUsedForMultipleBlocks);
        Assert.IsNotNull(viewmodel.FoldedBlocks);
        Assert.AreEqual(3, viewmodel.FoldedBlocks.Count);
        // Check that they're in alphabetical order
        Assert.AreEqual("CFoo::XYZ", ((SeparatedCodeBlockSymbol)viewmodel.FoldedBlocks[0]).ParentFunction.FormattedName.IncludeParentType);
        Assert.AreEqual("FunctionInAnotherPlaceEntirely", ((SeparatedCodeBlockSymbol)viewmodel.FoldedBlocks[1]).ParentFunction.FunctionName);
        Assert.AreEqual("testName", ((SeparatedCodeBlockSymbol)viewmodel.FoldedBlocks[2]).ParentFunction.FormattedName.IncludeParentType);
        Assert.AreEqual(function.SeparatedBlocks[0], viewmodel.Block);
        Assert.AreEqual(this.Generator.TextSection, viewmodel.BinarySection);
        Assert.AreEqual(this.Generator.TextMnCG, viewmodel.COFFGroup);
        Assert.AreEqual(this.Generator.B1Compiland, viewmodel.Compiland);
        Assert.AreEqual(this.Generator.BLib, viewmodel.Lib);
        Assert.AreEqual(this.Generator.XHSourceFile, viewmodel.SourceFile);
        Assert.AreEqual("Block Symbol: Block of code in testName()", viewmodel.PageTitle);
        Assert.IsTrue(viewmodel.IsSeparatedBlock);
        Assert.AreEqual(function, viewmodel.ParentFunction);
    }

    private ComplexFunctionCodeSymbol BuildComplexFunction(string name, uint primaryBlockRva, bool isVirtual = true, bool isIntroVirtual = false, bool isSealed = false,
                                                           bool isStatic = false,
                                                           SessionDataCache? sessionDataCache = null,
                                                           FunctionTypeSymbol? functionType = null,
                                                           ParameterDataSymbol[]? argumentNames = null)
    {
        var primaryBlock = new PrimaryCodeBlockSymbol(sessionDataCache ?? this.Generator.DataCache, rva: primaryBlockRva, size: 50u, symIndexId: this.Generator._nextSymIndexId++);
        var separatedBlocks = new List<SeparatedCodeBlockSymbol>()
            {
                new SeparatedCodeBlockSymbol(sessionDataCache ?? this.Generator.DataCache, rva: primaryBlockRva + 100 , size: 20u, symIndexId: this.Generator._nextSymIndexId++, parentFunctionSymIndexId: primaryBlock.SymIndexId),
                new SeparatedCodeBlockSymbol(sessionDataCache ?? this.Generator.DataCache, rva: primaryBlockRva + 1000, size: 20u, symIndexId: this.Generator._nextSymIndexId++, parentFunctionSymIndexId: primaryBlock.SymIndexId),
            };
        return new ComplexFunctionCodeSymbol(sessionDataCache ?? this.Generator.DataCache,
                                             name,
                                             primaryBlock,
                                             separatedBlocks,
                                             functionType: functionType,
                                             argumentNames: argumentNames,
                                             accessModifier: AccessModifier.Private,
                                             isVirtual: isVirtual,
                                             isIntroVirtual: isIntroVirtual,
                                             isSealed: isSealed,
                                             isStatic: isStatic,
                                             isPGO: true);
    }

    public void Dispose() => this.Generator.Dispose();
}
