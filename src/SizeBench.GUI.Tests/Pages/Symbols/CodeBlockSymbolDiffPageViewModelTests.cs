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
public sealed class CodeBlockSymbolDiffPageViewModelTests : IDisposable
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
    public async Task BlockLoadsEvenIfPlacementsAreCanceled()
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

        var viewmodel = new CodeBlockSymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BeforeRVA", blockDiff.BeforeSymbol!.RVA.ToString(CultureInfo.InvariantCulture) },
                { "AfterRVA", blockDiff.AfterSymbol!.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsNull(viewmodel.BlocksOfDifferentTypeWarningText);
        Assert.IsTrue(viewmodel.DoesBeforeSymbolExist);
        Assert.IsTrue(viewmodel.DoesAfterSymbolExist);
        Assert.IsTrue(ReferenceEquals(blockDiff, viewmodel.SymbolDiff));
        Assert.IsFalse(viewmodel.IsBeforeBlockCodeUsedForMultipleBlocks);
        Assert.IsFalse(viewmodel.IsAfterBlockCodeUsedForMultipleBlocks);
        Assert.IsNull(viewmodel.BeforeFoldedBlocks);
        Assert.IsNull(viewmodel.AfterFoldedBlocks);
        Assert.AreEqual($"Symbol Diff: {blockDiff.Name}", viewmodel.PageTitle);
        Assert.IsNull(viewmodel.BeforePlacement);
        Assert.IsNull(viewmodel.AfterPlacement);
        Assert.IsTrue(ReferenceEquals(expectedDiff, viewmodel.ParentFunctionSymbolDiff));
        Assert.IsFalse(viewmodel.IsBeforeParentFunctionComplex);
        Assert.IsFalse(viewmodel.IsAfterParentFunctionComplex);
    }

    [TestMethod]
    public async Task BlockPlacementLookupWorks()
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

        var viewmodel = new CodeBlockSymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BeforeRVA", blockDiff.BeforeSymbol!.RVA.ToString(CultureInfo.InvariantCulture) },
                { "AfterRVA", blockDiff.AfterSymbol!.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsNull(viewmodel.BlocksOfDifferentTypeWarningText);
        Assert.IsTrue(viewmodel.DoesBeforeSymbolExist);
        Assert.IsTrue(viewmodel.DoesAfterSymbolExist);
        Assert.IsTrue(ReferenceEquals(blockDiff, viewmodel.SymbolDiff));
        Assert.IsFalse(viewmodel.IsBeforeBlockCodeUsedForMultipleBlocks);
        Assert.IsFalse(viewmodel.IsAfterBlockCodeUsedForMultipleBlocks);
        Assert.IsNull(viewmodel.BeforeFoldedBlocks);
        Assert.IsNull(viewmodel.AfterFoldedBlocks);
        Assert.AreEqual($"Symbol Diff: {blockDiff.Name}", viewmodel.PageTitle);
        Assert.IsTrue(ReferenceEquals(beforePlacement, viewmodel.BeforePlacement));
        Assert.IsTrue(ReferenceEquals(afterPlacement, viewmodel.AfterPlacement));
        Assert.IsTrue(ReferenceEquals(expectedDiff, viewmodel.ParentFunctionSymbolDiff));
        Assert.IsFalse(viewmodel.IsBeforeParentFunctionComplex);
        Assert.IsFalse(viewmodel.IsAfterParentFunctionComplex);
    }

    [TestMethod]
    public async Task NonexistentCodeBlockDoesItsBest()
    {
        var viewmodel = new CodeBlockSymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "Name", "MyType::HasAFunction" }
            });
        await viewmodel.InitializeAsync();

        Assert.IsNull(viewmodel.BlocksOfDifferentTypeWarningText);
        Assert.IsFalse(viewmodel.DoesBeforeSymbolExist);
        Assert.IsFalse(viewmodel.DoesAfterSymbolExist);
        Assert.IsNull(viewmodel.SymbolDiff);
        Assert.IsFalse(viewmodel.IsBeforeBlockCodeUsedForMultipleBlocks);
        Assert.IsFalse(viewmodel.IsAfterBlockCodeUsedForMultipleBlocks);
        Assert.IsNull(viewmodel.BeforeFoldedBlocks);
        Assert.IsNull(viewmodel.AfterFoldedBlocks);
        Assert.AreEqual("Symbol Diff: MyType::HasAFunction", viewmodel.PageTitle);
        Assert.IsNull(viewmodel.BeforePlacement);
        Assert.IsNull(viewmodel.AfterPlacement);
        Assert.IsNull(viewmodel.ParentFunctionSymbolDiff);
        Assert.IsFalse(viewmodel.IsBeforeParentFunctionComplex);
        Assert.IsFalse(viewmodel.IsAfterParentFunctionComplex);
    }

    [TestMethod]
    public async Task BlockThatIsFoldedLoadsAllBlocksAtRVA()
    {
        var functionDiffs = this.Generator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);
        var expectedDiff = functionDiffs.Single(fnDiff => fnDiff.FunctionName == "ComplexBeforeComplexAfter");
        var primaryBlockDiff = expectedDiff.CodeBlockDiffs[0];
        var separatedBlockDiff = expectedDiff.CodeBlockDiffs[1];

        // Now we'll make two functions (complex and simple) that fold perfectly with the 'before'
        var foldedBeforeComplex = this.Generator.GenerateComplexFunctionCodeSymbolInBefore("FoldedBeforeComplex", primaryBlockDiff.BeforeSymbol!.RVA, primaryBlockDiff.BeforeSymbol.Size, separatedBlockDiff.BeforeSymbol!.RVA, separatedBlockDiff.BeforeSymbol.Size,
                                                                                           functionType: expectedDiff.BeforeSymbol!.FunctionType!);
        var foldedBeforeSimple = this.Generator.GenerateSimpleFunctionCodeSymbolInBefore("FoldedBeforeSimple", primaryBlockDiff.BeforeSymbol.RVA, primaryBlockDiff.BeforeSymbol.Size,
                                                                                         functionType: expectedDiff.BeforeSymbol.FunctionType!);
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
                                                                                           functionType: expectedDiff.AfterSymbol.FunctionType!);
        var foldedAfterSimple = this.Generator.GenerateSimpleFunctionCodeSymbolInAfter("FoldedAfterSimple", primaryBlockDiff.AfterSymbol.RVA, primaryBlockDiff.AfterSymbol.Size,
                                                                                         functionType: expectedDiff.AfterSymbol.FunctionType!);
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

        this.Generator.MockDiffSession.Setup(s => s.LoadSymbolDiffByBeforeAndAfterRVA(expectedDiff.BeforeSymbol!.PrimaryBlock.RVA, expectedDiff.AfterSymbol.PrimaryBlock.RVA, It.IsAny<CancellationToken>()))
                                      .Returns(Task.FromResult<SymbolDiff?>(primaryBlockDiff));
        this.Generator.MockBeforeSession.Setup(s => s.LookupSymbolPlacementInBinary(primaryBlockDiff.BeforeSymbol, It.IsAny<CancellationToken>())).Returns(Task.FromResult(beforePlacement));
        this.Generator.MockBeforeSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(primaryBlockDiff.BeforeSymbol.RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(allBeforeFoldedFunctions));
        this.Generator.MockAfterSession.Setup(s => s.LookupSymbolPlacementInBinary(primaryBlockDiff.AfterSymbol, It.IsAny<CancellationToken>())).Returns(Task.FromResult(afterPlacement));
        this.Generator.MockAfterSession.Setup(s => s.EnumerateAllSymbolsFoldedAtRVA(primaryBlockDiff.AfterSymbol.RVA, It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<ISymbol>>(allAfterFoldedFunctions));

        var viewmodel = new CodeBlockSymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BeforeRVA", primaryBlockDiff.BeforeSymbol.RVA.ToString(CultureInfo.InvariantCulture) },
                { "AfterRVA", primaryBlockDiff.AfterSymbol.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsNull(viewmodel.BlocksOfDifferentTypeWarningText);
        Assert.IsTrue(viewmodel.DoesBeforeSymbolExist);
        Assert.IsTrue(viewmodel.DoesAfterSymbolExist);
        Assert.IsTrue(ReferenceEquals(primaryBlockDiff, viewmodel.SymbolDiff));
        Assert.IsTrue(viewmodel.IsBeforeBlockCodeUsedForMultipleBlocks);
        Assert.IsTrue(viewmodel.IsAfterBlockCodeUsedForMultipleBlocks);
        Assert.AreEqual(3, viewmodel.BeforeFoldedBlocks!.Count);
        Assert.AreEqual("Block of code in ComplexBeforeComplexAfter(bool)", viewmodel.BeforeFoldedBlocks[0].Name);
        Assert.AreEqual("Block of code in FoldedBeforeComplex(bool)", viewmodel.BeforeFoldedBlocks[1].Name);
        Assert.AreEqual("FoldedBeforeSimple(bool)", viewmodel.BeforeFoldedBlocks[2].Name);
        Assert.AreEqual(4, viewmodel.AfterFoldedBlocks!.Count);
        Assert.AreEqual("Block of code in ComplexBeforeComplexAfter(bool)", viewmodel.AfterFoldedBlocks[0].Name);
        Assert.AreEqual("Block of code in FoldedAfterComplex1(bool)", viewmodel.AfterFoldedBlocks[1].Name);
        Assert.AreEqual("Block of code in FoldedAfterComplex2(bool)", viewmodel.AfterFoldedBlocks[2].Name);
        Assert.AreEqual("FoldedAfterSimple(bool)", viewmodel.AfterFoldedBlocks[3].Name);
        Assert.AreEqual($"Symbol Diff: {primaryBlockDiff.Name}", viewmodel.PageTitle);
        Assert.IsTrue(ReferenceEquals(beforePlacement, viewmodel.BeforePlacement));
        Assert.IsTrue(ReferenceEquals(afterPlacement, viewmodel.AfterPlacement));
        Assert.IsTrue(ReferenceEquals(expectedDiff, viewmodel.ParentFunctionSymbolDiff));
        Assert.IsTrue(viewmodel.IsBeforeParentFunctionComplex);
        Assert.IsTrue(viewmodel.IsAfterParentFunctionComplex);
    }

    [TestMethod]
    public async Task WriteTestForBlocksOfDifferentTypeWarningText()
    {
        // If the "before" is a simple function and after is a primary code block (because in after perhaps the binary got PGO'd and now
        // split the code), we should still load things but also display warning text.
        var functionDiffs = this.Generator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);
        var expectedDiff = functionDiffs.First(fnDiff => fnDiff.BeforeSymbol is SimpleFunctionCodeSymbol && fnDiff.AfterSymbol is ComplexFunctionCodeSymbol);
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

        var viewmodel = new CodeBlockSymbolDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.Generator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BeforeRVA", blockDiff.BeforeSymbol!.RVA.ToString(CultureInfo.InvariantCulture) },
                { "AfterRVA", blockDiff.AfterSymbol!.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        // We should have generated warning text with at least some helpful bits of information.
        Assert.IsNotNull(viewmodel.BlocksOfDifferentTypeWarningText);
        StringAssert.Contains(viewmodel.BlocksOfDifferentTypeWarningText, "the symbol in the 'before' binary is an entire function", StringComparison.Ordinal);
        StringAssert.Contains(viewmodel.BlocksOfDifferentTypeWarningText, "the symbol in the 'after' binary is only the primary block of a function that is separated into multiple blocks (such as by PGO)", StringComparison.Ordinal);

        Assert.IsTrue(viewmodel.DoesBeforeSymbolExist);
        Assert.IsTrue(viewmodel.DoesAfterSymbolExist);
        Assert.IsTrue(ReferenceEquals(blockDiff, viewmodel.SymbolDiff));
        Assert.IsFalse(viewmodel.IsBeforeBlockCodeUsedForMultipleBlocks);
        Assert.IsFalse(viewmodel.IsAfterBlockCodeUsedForMultipleBlocks);
        Assert.IsNull(viewmodel.BeforeFoldedBlocks);
        Assert.IsNull(viewmodel.AfterFoldedBlocks);
        Assert.AreEqual($"Symbol Diff: {blockDiff.Name}", viewmodel.PageTitle);
        Assert.IsTrue(ReferenceEquals(beforePlacement, viewmodel.BeforePlacement));
        Assert.IsTrue(ReferenceEquals(afterPlacement, viewmodel.AfterPlacement));
        Assert.IsTrue(ReferenceEquals(expectedDiff, viewmodel.ParentFunctionSymbolDiff));
        Assert.IsFalse(viewmodel.IsBeforeParentFunctionComplex);
        Assert.IsTrue(viewmodel.IsAfterParentFunctionComplex);
    }

    public void Dispose() => this.Generator.Dispose();
}
