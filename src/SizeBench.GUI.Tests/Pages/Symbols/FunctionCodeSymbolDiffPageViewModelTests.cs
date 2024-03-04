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
public sealed class FunctionCodeSymbolDiffPageViewModelTests : IDisposable
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
    public async Task FunctionLoadsEvenIfPlacementsAreCanceled()
    {
        var functionDiffs = this.Generator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);
        var expectedDiff = functionDiffs.First(fnDiff => fnDiff.BeforeSymbol is SimpleFunctionCodeSymbol && fnDiff.AfterSymbol is SimpleFunctionCodeSymbol);
        var blockDiff = expectedDiff.CodeBlockDiffs[0];

        this.Generator.MockDiffSession.Setup(s => s.LoadSymbolDiffByBeforeAndAfterRVA(expectedDiff.BeforeSymbol!.PrimaryBlock.RVA, expectedDiff.AfterSymbol!.PrimaryBlock.RVA, It.IsAny<CancellationToken>()))
                                      .Returns(Task.FromResult<SymbolDiff?>(blockDiff));
        var tcsPlacement = new TaskCompletionSource<SymbolPlacement>();
        tcsPlacement.SetCanceled();
        this.Generator.MockBeforeSession.Setup(s => s.LookupSymbolPlacementInBinary(blockDiff.BeforeSymbol!, It.IsAny<CancellationToken>())).Returns(tcsPlacement.Task);
        this.Generator.MockBeforeSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(blockDiff.BeforeSymbol!.RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(new List<ISymbol>()));
        this.Generator.MockAfterSession.Setup(s => s.LookupSymbolPlacementInBinary(blockDiff.AfterSymbol!, It.IsAny<CancellationToken>())).Returns(tcsPlacement.Task);
        this.Generator.MockAfterSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(blockDiff.AfterSymbol!.RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(new List<ISymbol>()));

        var viewmodel = new FunctionCodeSymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                                this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BeforeRVA", blockDiff.BeforeSymbol!.RVA.ToString(CultureInfo.InvariantCulture) },
                { "AfterRVA", blockDiff.AfterSymbol!.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.DoesBeforeSymbolExist);
        Assert.IsTrue(viewmodel.DoesAfterSymbolExist);
        Assert.IsFalse(viewmodel.BeforeFunctionContainsMultipleCodeBlocks);
        Assert.IsFalse(viewmodel.AfterFunctionContainsMultipleCodeBlocks);
        Assert.IsFalse(viewmodel.IsBeforeFunctionCodeUsedForMultipleFunctions);
        Assert.IsFalse(viewmodel.IsAfterFunctionCodeUsedForMultipleFunctions);
        Assert.IsNull(viewmodel.BeforeFoldedFunctions);
        Assert.IsNull(viewmodel.AfterFoldedFunctions);
        Assert.AreEqual($"Function Diff: {expectedDiff.FormattedName.IncludeParentType}", viewmodel.PageTitle);
        Assert.AreEqual(0, viewmodel.BeforeBlockPlacements.Count);
        Assert.AreEqual(0, viewmodel.AfterBlockPlacements.Count);
        Assert.IsTrue(ReferenceEquals(expectedDiff, viewmodel.FunctionDiff));
        Assert.AreEqual(String.Empty, viewmodel.BeforeAttributes);
        Assert.AreEqual(String.Empty, viewmodel.AfterAttributes);
    }

    [TestMethod]
    public async Task FunctionPlacementLookupWorks()
    {
        var functionDiffs = this.Generator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);
        var expectedDiff = functionDiffs.First(fnDiff => fnDiff.BeforeSymbol is SimpleFunctionCodeSymbol && fnDiff.AfterSymbol is SimpleFunctionCodeSymbol);
        var blockDiff = expectedDiff.CodeBlockDiffs[0];

        var beforePlacement = new SymbolPlacement(this.Generator.BeforeTextSection, this.Generator.BeforeTextMnCG, this.Generator.BeforeALib, this.Generator.BeforeA1Compiland, null);

        // For sake of testing something interesting, we'll say the symbol moved from .text$mn -> .text$zz, a.lib -> b.lib, and a1.obj -> b1.obj
        var afterPlacement = new SymbolPlacement(this.Generator.AfterTextSection, this.Generator.AfterTextZzCG, this.Generator.BeforeBLib, this.Generator.BeforeB1Compiland, null);

        this.Generator.MockDiffSession.Setup(s => s.LoadSymbolDiffByBeforeAndAfterRVA(expectedDiff.BeforeSymbol!.PrimaryBlock.RVA, expectedDiff.AfterSymbol!.PrimaryBlock.RVA, It.IsAny<CancellationToken>()))
                                      .Returns(Task.FromResult<SymbolDiff?>(blockDiff));
        this.Generator.MockBeforeSession.Setup(s => s.LookupSymbolPlacementInBinary(blockDiff.BeforeSymbol!, It.IsAny<CancellationToken>())).Returns(Task.FromResult(beforePlacement));
        this.Generator.MockBeforeSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(blockDiff.BeforeSymbol!.RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(new List<ISymbol>()));
        this.Generator.MockAfterSession.Setup(s => s.LookupSymbolPlacementInBinary(blockDiff.AfterSymbol!, It.IsAny<CancellationToken>())).Returns(Task.FromResult(afterPlacement));
        this.Generator.MockAfterSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(blockDiff.AfterSymbol!.RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(new List<ISymbol>()));

        var viewmodel = new FunctionCodeSymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                                this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BeforeRVA", blockDiff.BeforeSymbol!.RVA.ToString(CultureInfo.InvariantCulture) },
                { "AfterRVA", blockDiff.AfterSymbol!.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.DoesBeforeSymbolExist);
        Assert.IsTrue(viewmodel.DoesAfterSymbolExist);
        Assert.IsFalse(viewmodel.BeforeFunctionContainsMultipleCodeBlocks);
        Assert.IsFalse(viewmodel.AfterFunctionContainsMultipleCodeBlocks);
        Assert.IsFalse(viewmodel.IsBeforeFunctionCodeUsedForMultipleFunctions);
        Assert.IsFalse(viewmodel.IsAfterFunctionCodeUsedForMultipleFunctions);
        Assert.IsNull(viewmodel.BeforeFoldedFunctions);
        Assert.IsNull(viewmodel.AfterFoldedFunctions);
        Assert.AreEqual($"Function Diff: {expectedDiff.FormattedName.IncludeParentType}", viewmodel.PageTitle);
        Assert.AreEqual(1, viewmodel.BeforeBlockPlacements.Count);
        Assert.IsTrue(ReferenceEquals(viewmodel.BeforeBlockPlacements[0].Key, blockDiff.BeforeSymbol));
        Assert.IsTrue(ReferenceEquals(viewmodel.BeforeBlockPlacements[0].Value, beforePlacement));
        Assert.AreEqual(1, viewmodel.AfterBlockPlacements.Count);
        Assert.IsTrue(ReferenceEquals(viewmodel.AfterBlockPlacements[0].Key, blockDiff.AfterSymbol));
        Assert.IsTrue(ReferenceEquals(viewmodel.AfterBlockPlacements[0].Value, afterPlacement));
        Assert.IsTrue(ReferenceEquals(expectedDiff, viewmodel.FunctionDiff));
        Assert.AreEqual(String.Empty, viewmodel.BeforeAttributes);
        Assert.AreEqual(String.Empty, viewmodel.AfterAttributes);
    }

    [TestMethod]
    public async Task NonexistentFunctionDoesItsBest()
    {
        var viewmodel = new FunctionCodeSymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                                this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "Name", "MyType::HasAFunction" }
            });
        await viewmodel.InitializeAsync();

        Assert.IsFalse(viewmodel.DoesBeforeSymbolExist);
        Assert.IsFalse(viewmodel.DoesAfterSymbolExist);
        Assert.IsFalse(viewmodel.BeforeFunctionContainsMultipleCodeBlocks);
        Assert.IsFalse(viewmodel.AfterFunctionContainsMultipleCodeBlocks);
        Assert.IsFalse(viewmodel.IsBeforeFunctionCodeUsedForMultipleFunctions);
        Assert.IsFalse(viewmodel.IsAfterFunctionCodeUsedForMultipleFunctions);
        Assert.IsNull(viewmodel.BeforeFoldedFunctions);
        Assert.IsNull(viewmodel.AfterFoldedFunctions);
        Assert.AreEqual("Symbol Diff: MyType::HasAFunction", viewmodel.PageTitle);
        Assert.AreEqual(0, viewmodel.BeforeBlockPlacements.Count);
        Assert.AreEqual(0, viewmodel.AfterBlockPlacements.Count);
        Assert.IsNull(viewmodel.FunctionDiff);
        Assert.AreEqual(String.Empty, viewmodel.BeforeAttributes);
        Assert.AreEqual(String.Empty, viewmodel.AfterAttributes);
    }

    [TestMethod]
    public async Task FunctionThatIsFoldedLoadsAllFunctionsAtRVA()
    {
        var functionDiffs = this.Generator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);
        var expectedDiff = functionDiffs.Single(fnDiff => fnDiff.FunctionName == "ComplexBeforeComplexAfter");
        var primaryBlockDiff = expectedDiff.CodeBlockDiffs[0];
        var separatedBlockDiff = expectedDiff.CodeBlockDiffs[1];

        // Now we'll make two functions (complex and simple) that fold perfectly with the 'before'
        var foldedBeforeComplex = this.Generator.GenerateComplexFunctionCodeSymbolInBefore("FoldedBeforeComplex", primaryBlockDiff.BeforeSymbol!.RVA, primaryBlockDiff.BeforeSymbol.Size, separatedBlockDiff.BeforeSymbol!.RVA, separatedBlockDiff.BeforeSymbol.Size,
                                                                                           functionType: expectedDiff.BeforeSymbol!.FunctionType!);
        var foldedBeforeSimple = this.Generator.GenerateSimpleFunctionCodeSymbolInBefore("FoldedBeforeSimple", primaryBlockDiff.BeforeSymbol.RVA, primaryBlockDiff.BeforeSymbol.Size,
                                                                                         functionType: expectedDiff.BeforeSymbol!.FunctionType!);
        var allBeforeFoldedFunctions = new List<ISymbol>() { expectedDiff.BeforeSymbol.PrimaryBlock, foldedBeforeComplex.PrimaryBlock, foldedBeforeSimple };
        var nameCanonicalization = new NameCanonicalization();
        nameCanonicalization.AddName(primaryBlockDiff.BeforeCodeBlockSymbol!.SymIndexId, SymTagEnum.SymTagFunction, name: expectedDiff.FullName);
        nameCanonicalization.AddName(foldedBeforeComplex.PrimaryBlock.SymIndexId, SymTagEnum.SymTagFunction, name: foldedBeforeComplex.FullName);
        nameCanonicalization.AddName(foldedBeforeSimple.SymIndexId, SymTagEnum.SymTagFunction, name: foldedBeforeSimple.FullName);
        nameCanonicalization.Canonicalize();
        this.Generator.BeforeDataCache.AllCanonicalNames = new SortedList<uint, NameCanonicalization>
            {
                { primaryBlockDiff.BeforeSymbol.RVA, nameCanonicalization }
            };

        // And three that fold perfectly with the 'after', of both complex and simple types
        var foldedAfterComplex1 = this.Generator.GenerateComplexFunctionCodeSymbolInAfter("FoldedAfterComplex1", primaryBlockDiff.AfterSymbol!.RVA, primaryBlockDiff.AfterSymbol.Size, separatedBlockDiff.AfterSymbol!.RVA, separatedBlockDiff.AfterSymbol.Size,
                                                                                           functionType: expectedDiff.AfterSymbol!.FunctionType!);
        var foldedAfterComplex2 = this.Generator.GenerateComplexFunctionCodeSymbolInAfter("FoldedAfterComplex2", primaryBlockDiff.AfterSymbol.RVA, primaryBlockDiff.AfterSymbol.Size, separatedBlockDiff.AfterSymbol.RVA, separatedBlockDiff.AfterSymbol.Size,
                                                                                           functionType: expectedDiff.AfterSymbol!.FunctionType!);
        var foldedAfterSimple = this.Generator.GenerateSimpleFunctionCodeSymbolInAfter("FoldedAfterSimple", primaryBlockDiff.AfterSymbol.RVA, primaryBlockDiff.AfterSymbol.Size,
                                                                                         functionType: expectedDiff.AfterSymbol!.FunctionType!);
        var allAfterFoldedFunctions = new List<ISymbol>() { expectedDiff.AfterSymbol.PrimaryBlock, foldedAfterComplex1.PrimaryBlock, foldedAfterComplex2.PrimaryBlock, foldedAfterSimple };
        nameCanonicalization = new NameCanonicalization();
        nameCanonicalization.AddName(primaryBlockDiff.AfterCodeBlockSymbol!.SymIndexId, SymTagEnum.SymTagFunction, name: expectedDiff.FullName);
        nameCanonicalization.AddName(foldedAfterComplex1.PrimaryBlock.SymIndexId, SymTagEnum.SymTagFunction, name: foldedAfterComplex1.FullName);
        nameCanonicalization.AddName(foldedAfterComplex2.PrimaryBlock.SymIndexId, SymTagEnum.SymTagFunction, name: foldedAfterComplex2.FullName);
        nameCanonicalization.AddName(foldedAfterSimple.SymIndexId, SymTagEnum.SymTagFunction, name: foldedAfterSimple.FullName);
        nameCanonicalization.Canonicalize();
        this.Generator.AfterDataCache.AllCanonicalNames = new SortedList<uint, NameCanonicalization>
            {
                { primaryBlockDiff.AfterSymbol.RVA, nameCanonicalization }
            };

        var beforePlacement = new SymbolPlacement(this.Generator.BeforeTextSection, this.Generator.BeforeTextMnCG, this.Generator.BeforeALib, this.Generator.BeforeA1Compiland, null);

        // For sake of testing something interesting, we'll say the symbol moved from .text$mn -> .text$zz, a.lib -> b.lib, and a1.obj -> b1.obj
        var afterPlacement = new SymbolPlacement(this.Generator.AfterTextSection, this.Generator.AfterTextZzCG, this.Generator.BeforeBLib, this.Generator.BeforeB1Compiland, null);

        this.Generator.MockDiffSession.Setup(s => s.LoadSymbolDiffByBeforeAndAfterRVA(expectedDiff.BeforeSymbol.PrimaryBlock.RVA, expectedDiff.AfterSymbol.PrimaryBlock.RVA, It.IsAny<CancellationToken>()))
                                      .Returns(Task.FromResult<SymbolDiff?>(primaryBlockDiff));
        this.Generator.MockBeforeSession.Setup(s => s.LookupSymbolPlacementInBinary(primaryBlockDiff.BeforeSymbol, It.IsAny<CancellationToken>())).Returns(Task.FromResult(beforePlacement));
        this.Generator.MockBeforeSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(primaryBlockDiff.BeforeSymbol.RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(allBeforeFoldedFunctions));
        this.Generator.MockAfterSession.Setup(s => s.LookupSymbolPlacementInBinary(primaryBlockDiff.AfterSymbol, It.IsAny<CancellationToken>())).Returns(Task.FromResult(afterPlacement));
        this.Generator.MockAfterSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(primaryBlockDiff.AfterSymbol.RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(allAfterFoldedFunctions));

        var viewmodel = new FunctionCodeSymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                                this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BeforeRVA", primaryBlockDiff.BeforeSymbol.RVA.ToString(CultureInfo.InvariantCulture) },
                { "AfterRVA", primaryBlockDiff.AfterSymbol.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.DoesBeforeSymbolExist);
        Assert.IsTrue(viewmodel.DoesAfterSymbolExist);
        Assert.IsTrue(viewmodel.BeforeFunctionContainsMultipleCodeBlocks);
        Assert.IsTrue(viewmodel.AfterFunctionContainsMultipleCodeBlocks);
        Assert.IsTrue(viewmodel.IsBeforeFunctionCodeUsedForMultipleFunctions);
        Assert.IsTrue(viewmodel.IsAfterFunctionCodeUsedForMultipleFunctions);
        Assert.AreEqual(3, viewmodel.BeforeFoldedFunctions!.Count);
        Assert.AreEqual("ComplexBeforeComplexAfter", viewmodel.BeforeFoldedFunctions[0].FunctionName);
        Assert.AreEqual("FoldedBeforeComplex", viewmodel.BeforeFoldedFunctions[1].FunctionName);
        Assert.AreEqual("FoldedBeforeSimple", viewmodel.BeforeFoldedFunctions[2].FunctionName);
        Assert.AreEqual(4, viewmodel.AfterFoldedFunctions!.Count);
        Assert.AreEqual("ComplexBeforeComplexAfter", viewmodel.AfterFoldedFunctions[0].FunctionName);
        Assert.AreEqual("FoldedAfterComplex1", viewmodel.AfterFoldedFunctions[1].FunctionName);
        Assert.AreEqual("FoldedAfterComplex2", viewmodel.AfterFoldedFunctions[2].FunctionName);
        Assert.AreEqual("FoldedAfterSimple", viewmodel.AfterFoldedFunctions[3].FunctionName);
        Assert.AreEqual($"Function Diff: {expectedDiff.FormattedName.IncludeParentType}", viewmodel.PageTitle);
        Assert.AreEqual(2, viewmodel.BeforeBlockPlacements.Count);
        Assert.AreEqual(2, viewmodel.AfterBlockPlacements.Count);
        Assert.AreEqual("Attributes: has been PGO'd, has been optimized for speed", viewmodel.BeforeAttributes);
        Assert.AreEqual("Attributes: has been PGO'd, has been optimized for speed", viewmodel.AfterAttributes);
    }

    public void Dispose() => this.Generator.Dispose();
}
