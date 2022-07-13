using System.Globalization;
using Dia2Lib;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Pages.Symbols;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBenchV2.ViewModels.Tests;

[TestClass]
public sealed class FunctionSymbolPageViewModelTests : IDisposable
{
    internal SingleBinaryDataGenerator Generator = new SingleBinaryDataGenerator();
    internal Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.Generator = new SingleBinaryDataGenerator();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task FunctionLoadsEvenIfPlacementsAreCanceled()
    {
        const int rva = 5798;
        var function = new SimpleFunctionCodeSymbol(this.Generator.DataCache, "test function", rva: rva, size: 50, symIndexId: this.Generator._nextSymIndexId++);

        this.Generator.MockSession.Setup(s => s.LoadSymbolByRVA(rva)).Returns(Task.FromResult<ISymbol?>(function));
        var tcsPlacement = new TaskCompletionSource<SymbolPlacement>();
        tcsPlacement.SetCanceled();
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(function, It.IsAny<CancellationToken>())).Returns(tcsPlacement.Task);
        this.Generator.MockSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(rva, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(new List<ISymbol>()));

        var viewmodel = new FunctionSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                        this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "FunctionRVA", rva.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.DoesFunctionExist);
        Assert.IsFalse(viewmodel.DoesFunctionContainMultipleCodeBlocks);
        Assert.IsFalse(viewmodel.IsFunctionCodeUsedForMultipleFunctions);
        Assert.IsNull(viewmodel.FoldedFunctions);
        Assert.AreEqual(function, viewmodel.Function);
        Assert.AreEqual(0, viewmodel.BlockPlacements.Count);
        Assert.AreEqual("Function Symbol: test function", viewmodel.PageTitle);
        Assert.AreEqual("", viewmodel.FunctionAttributes);
    }

    [TestMethod]
    public async Task ComplexFunctionsLookUpPlacementOfAllBlocks()
    {
        const uint primaryRva = 5798;
        const uint separatedRva1 = 1234;
        const uint separatedRva2 = 11;
        var primaryBlock = new PrimaryCodeBlockSymbol(this.Generator.DataCache, rva: primaryRva, size: 50, symIndexId: this.Generator._nextSymIndexId++);
        var separatedBlocks = new List<SeparatedCodeBlockSymbol>()
            {
                new SeparatedCodeBlockSymbol(this.Generator.DataCache, rva: separatedRva1, size: 20, symIndexId: this.Generator._nextSymIndexId++, parentFunctionSymIndexId: primaryBlock.SymIndexId),
                new SeparatedCodeBlockSymbol(this.Generator.DataCache, rva: separatedRva2, size: 20, symIndexId: this.Generator._nextSymIndexId++, parentFunctionSymIndexId: primaryBlock.SymIndexId),
            };
        var function = new ComplexFunctionCodeSymbol(this.Generator.DataCache, "CFoo::DoTheThing", primaryBlock, separatedBlocks,
                                                 accessModifier: AccessModifier.Private, isVirtual: true, isPGO: true, isOptimizedForSpeed: true);

        var primaryPlacement = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextMnCG, this.Generator.ALib, this.Generator.A1Compiland, this.Generator.A1CppSourceFile);
        var separatedPlacement1 = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextZzCG, this.Generator.BLib, this.Generator.B1Compiland, this.Generator.A1CppSourceFile);
        var separatedPlacement2 = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextMnCG, this.Generator.ALib, this.Generator.A1Compiland, this.Generator.XHSourceFile);

        this.Generator.MockSession.Setup(s => s.LoadSymbolByRVA(primaryRva)).Returns(Task.FromResult<ISymbol?>(primaryBlock));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(primaryBlock, It.IsAny<CancellationToken>())).Returns(Task.FromResult(primaryPlacement));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(separatedBlocks[0], It.IsAny<CancellationToken>())).Returns(Task.FromResult(separatedPlacement1));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(separatedBlocks[1], It.IsAny<CancellationToken>())).Returns(Task.FromResult(separatedPlacement2));
        this.Generator.MockSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(primaryRva, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(new List<ISymbol>()));

        var viewmodel = new FunctionSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                        this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "FunctionRVA", primaryRva.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.DoesFunctionExist);
        Assert.IsTrue(viewmodel.DoesFunctionContainMultipleCodeBlocks);
        Assert.IsFalse(viewmodel.IsFunctionCodeUsedForMultipleFunctions);
        Assert.IsNull(viewmodel.FoldedFunctions);
        Assert.AreEqual(function, viewmodel.Function);
        Assert.AreEqual(3, viewmodel.BlockPlacements.Count);
        // These should come out in block RVA order, so see above for the RVAs for why this order is right
        Assert.AreEqual(separatedBlocks[1], viewmodel.BlockPlacements[0].Key);
        Assert.AreEqual(separatedBlocks[0], viewmodel.BlockPlacements[1].Key);
        Assert.AreEqual(primaryBlock, viewmodel.BlockPlacements[2].Key);

        Assert.AreEqual(this.Generator.TextSection, viewmodel.BlockPlacements[0].Value.BinarySection);
        Assert.AreEqual(this.Generator.TextMnCG, viewmodel.BlockPlacements[0].Value.COFFGroup);
        Assert.AreEqual(this.Generator.ALib, viewmodel.BlockPlacements[0].Value.Lib);
        Assert.AreEqual(this.Generator.A1Compiland, viewmodel.BlockPlacements[0].Value.Compiland);
        Assert.AreEqual(this.Generator.XHSourceFile, viewmodel.BlockPlacements[0].Value.SourceFile);

        Assert.AreEqual(this.Generator.TextSection, viewmodel.BlockPlacements[1].Value.BinarySection);
        Assert.AreEqual(this.Generator.TextZzCG, viewmodel.BlockPlacements[1].Value.COFFGroup);
        Assert.AreEqual(this.Generator.BLib, viewmodel.BlockPlacements[1].Value.Lib);
        Assert.AreEqual(this.Generator.B1Compiland, viewmodel.BlockPlacements[1].Value.Compiland);
        Assert.AreEqual(this.Generator.A1CppSourceFile, viewmodel.BlockPlacements[1].Value.SourceFile);

        Assert.AreEqual(this.Generator.TextSection, viewmodel.BlockPlacements[2].Value.BinarySection);
        Assert.AreEqual(this.Generator.TextMnCG, viewmodel.BlockPlacements[2].Value.COFFGroup);
        Assert.AreEqual(this.Generator.ALib, viewmodel.BlockPlacements[2].Value.Lib);
        Assert.AreEqual(this.Generator.A1Compiland, viewmodel.BlockPlacements[2].Value.Compiland);
        Assert.AreEqual(this.Generator.A1CppSourceFile, viewmodel.BlockPlacements[2].Value.SourceFile);
        Assert.AreEqual("Function Symbol: CFoo::DoTheThing", viewmodel.PageTitle);
        Assert.AreEqual("private access modifier, virtual function (overriding from a base type), has been PGO'd, has been optimized for speed", viewmodel.FunctionAttributes);
    }

    [TestMethod]
    public async Task FunctionPlacementLookupWorks()
    {
        const int rva = 5798;
        var function = new SimpleFunctionCodeSymbol(this.Generator.DataCache, "CFoo::DoTheThing", rva: rva, size: 50, symIndexId: this.Generator._nextSymIndexId++,
                                                accessModifier: AccessModifier.Private, isVirtual: true, isPGO: true, isOptimizedForSpeed: true);

        var placement = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextMnCG, this.Generator.ALib, this.Generator.A1Compiland, this.Generator.A1CppSourceFile);

        this.Generator.MockSession.Setup(s => s.LoadSymbolByRVA(rva)).Returns(Task.FromResult<ISymbol?>(function));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(function, It.IsAny<CancellationToken>())).Returns(Task.FromResult(placement));
        this.Generator.MockSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(rva, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(new List<ISymbol>()));

        var viewmodel = new FunctionSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                        this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "FunctionRVA", rva.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.DoesFunctionExist);
        Assert.IsFalse(viewmodel.DoesFunctionContainMultipleCodeBlocks);
        Assert.IsFalse(viewmodel.IsFunctionCodeUsedForMultipleFunctions);
        Assert.IsNull(viewmodel.FoldedFunctions);
        Assert.AreEqual(function, viewmodel.Function);
        Assert.AreEqual(1, viewmodel.BlockPlacements.Count);
        Assert.AreEqual(function, viewmodel.BlockPlacements[0].Key);
        Assert.AreEqual(this.Generator.TextSection, viewmodel.BlockPlacements[0].Value.BinarySection);
        Assert.AreEqual(this.Generator.TextMnCG, viewmodel.BlockPlacements[0].Value.COFFGroup);
        Assert.AreEqual(this.Generator.ALib, viewmodel.BlockPlacements[0].Value.Lib);
        Assert.AreEqual(this.Generator.A1Compiland, viewmodel.BlockPlacements[0].Value.Compiland);
        Assert.AreEqual(this.Generator.A1CppSourceFile, viewmodel.BlockPlacements[0].Value.SourceFile);
        Assert.AreEqual("Function Symbol: CFoo::DoTheThing", viewmodel.PageTitle);
        Assert.AreEqual("private access modifier, virtual function (overriding from a base type), has been PGO'd, has been optimized for speed", viewmodel.FunctionAttributes);
    }

    [TestMethod]
    public async Task NonexistentFunctionDoesItsBest()
    {
        var viewmodel = new FunctionSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                        this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "FunctionRVA", "0" },
                { "Name", "MyType::HasAFunction" }
            });
        await viewmodel.InitializeAsync();

        Assert.IsFalse(viewmodel.DoesFunctionExist);
        Assert.IsFalse(viewmodel.DoesFunctionContainMultipleCodeBlocks);
        Assert.IsFalse(viewmodel.IsFunctionCodeUsedForMultipleFunctions);
        Assert.IsNull(viewmodel.FoldedFunctions);
        Assert.AreEqual("MyType::HasAFunction", viewmodel.NameOfNonexistentFunction);
        Assert.IsNull(viewmodel.Function);
        Assert.AreEqual(0, viewmodel.BlockPlacements.Count);
        Assert.AreEqual("Function Symbol: MyType::HasAFunction", viewmodel.PageTitle);
        Assert.AreEqual(String.Empty, viewmodel.FunctionAttributes);
    }

    [TestMethod]
    public async Task FunctionThatIsFoldedLoadsAllFunctionsAtRVA()
    {
        const int rva = 5798;
        var function = new SimpleFunctionCodeSymbol(this.Generator.DataCache, "CFoo::DoTheThing", rva: rva, size: 50, symIndexId: this.Generator._nextSymIndexId++,
                                                accessModifier: AccessModifier.Private, isVirtual: true, isPGO: true, isOptimizedForSpeed: true);

        var foldedFunction1 = new SimpleFunctionCodeSymbol(this.Generator.DataCache, "CFoo::XYZ", rva: rva, size: 50, symIndexId: this.Generator._nextSymIndexId++,
                                                           accessModifier: AccessModifier.Public);
        var foldedFunction2 = new SimpleFunctionCodeSymbol(this.Generator.DataCache, "FunctionInAnotherPlaceEntirely", rva: rva, size: 50, symIndexId: this.Generator._nextSymIndexId++,
                                                           accessModifier: AccessModifier.Public, isStatic: true);
        var allFunctionsAtThisRVA = new List<ISymbol>() { function, foldedFunction2, foldedFunction1 };

        var nameCanonicalization = new NameCanonicalization();
        nameCanonicalization.AddName(function.SymIndexId, function.FullName, SymTagEnum.SymTagFunction);
        nameCanonicalization.AddName(foldedFunction1.SymIndexId, foldedFunction1.FullName, SymTagEnum.SymTagFunction);
        nameCanonicalization.AddName(foldedFunction2.SymIndexId, foldedFunction2.FullName, SymTagEnum.SymTagFunction);
        nameCanonicalization.Canonicalize();
        this.Generator.DataCache.AllCanonicalNames = new SortedList<uint, NameCanonicalization>
            {
                { rva, nameCanonicalization }
            };

        var placement = new SymbolPlacement(this.Generator.TextSection, this.Generator.TextMnCG, this.Generator.ALib, this.Generator.A1Compiland, this.Generator.A1CppSourceFile);

        this.Generator.MockSession.Setup(s => s.LoadSymbolByRVA(rva)).Returns(Task.FromResult<ISymbol?>(function));
        this.Generator.MockSession.Setup(s => s.LookupSymbolPlacementInBinary(function, It.IsAny<CancellationToken>())).Returns(Task.FromResult(placement));
        this.Generator.MockSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(rva, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(allFunctionsAtThisRVA));

        var viewmodel = new FunctionSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                        this.Generator.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "FunctionRVA", rva.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.DoesFunctionExist);
        Assert.IsFalse(viewmodel.DoesFunctionContainMultipleCodeBlocks);
        Assert.IsTrue(viewmodel.IsFunctionCodeUsedForMultipleFunctions);
        Assert.AreEqual(function, viewmodel.Function);
        Assert.AreEqual(3, viewmodel.FoldedFunctions!.Count);
        // Check that they're in alphabetical order
        Assert.AreEqual("CFoo::DoTheThing", viewmodel.FoldedFunctions[0].FormattedName.IncludeParentType);
        Assert.AreEqual("CFoo::XYZ", viewmodel.FoldedFunctions[1].FormattedName.IncludeParentType);
        Assert.AreEqual("FunctionInAnotherPlaceEntirely", viewmodel.FoldedFunctions[2].FunctionName);
    }

    public void Dispose() => this.Generator.Dispose();
}
