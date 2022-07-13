using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;


namespace SizeBench.GUI.Navigation.Tests;

[TestClass]
public sealed class DiffModelToUriConverterTests : IDisposable
{
    DiffTestDataGenerator _testGenerator = new DiffTestDataGenerator();
    IDiffSession? _diffSession;

    [TestInitialize]
    public void TestInitialize()
    {
        this._testGenerator = new DiffTestDataGenerator();
        this._diffSession = this._testGenerator.MockDiffSession.Object;
    }

    [TestMethod]
    public void NavigatingToUriPassesThroughToCurrentPage()
    {
        var expectedUri = new Uri(@"A\B\C.xaml?id=123", UriKind.Relative);

        Assert.AreEqual(expectedUri, DiffModelToUriConverter.ModelToUri(expectedUri, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToBinarySection()
    {
        Assert.AreEqual(new Uri(@"BinarySectionDiff/.text", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.TextSectionDiff.BeforeSection!, this._diffSession!));
        Assert.AreEqual(new Uri(@"BinarySectionDiff/.text", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.TextSectionDiff.AfterSection!, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToBinarySectionDiff() => Assert.AreEqual(new Uri(@"BinarySectionDiff/.text", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.TextSectionDiff, this._diffSession!));

    [TestMethod]
    public void CanNavigateToCOFFGroup()
    {
        Assert.AreEqual(new Uri(@"COFFGroupDiff/.text$zz", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.TextZzCGDiff.BeforeCOFFGroup!, this._diffSession!));
        Assert.AreEqual(new Uri(@"COFFGroupDiff/.text$zz", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.TextZzCGDiff.AfterCOFFGroup!, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToCOFFGroupDiff() => Assert.AreEqual(new Uri(@"COFFGroupDiff/.text$zz", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.TextZzCGDiff, this._diffSession!));

    [TestMethod]
    public void CanNavigateToLib()
    {
        Assert.AreEqual(new Uri($@"LibDiff/a.lib", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.ALibDiff.BeforeLib!, this._diffSession!));
        Assert.AreEqual(new Uri($@"LibDiff/a.lib", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.ALibDiff.AfterLib!, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToLibDiff() => Assert.AreEqual(new Uri($@"LibDiff/a.lib", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.ALibDiff, this._diffSession!));

    [TestMethod]
    public void CanNavigateToCompiland()
    {
        Assert.AreEqual(new Uri($@"CompilandDiff/{Uri.EscapeDataString(@"c:\a\a1.obj")}?Lib={Uri.EscapeDataString(this._testGenerator.ALibDiff.Name)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.A1CompilandDiff.BeforeCompiland!, this._diffSession!));
        Assert.AreEqual(new Uri($@"CompilandDiff/{Uri.EscapeDataString(@"c:\a\a1.obj")}?Lib={Uri.EscapeDataString(this._testGenerator.ALibDiff.Name)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.A1CompilandDiff.AfterCompiland!, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToCompilandDiff() => Assert.AreEqual(new Uri($@"CompilandDiff/{Uri.EscapeDataString(@"c:\a\a1.obj")}?Lib={Uri.EscapeDataString(this._testGenerator.ALibDiff.Name)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.A1CompilandDiff, this._diffSession!));

    [TestMethod]
    public void CanNavigateToListOfBinarySectionDiffs() => Assert.AreEqual(new Uri($@"AllBinarySectionDiffs", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.BinarySectionDiffs, this._diffSession!));

    [TestMethod]
    public void CanNavigateToListOfLibDiffs() => Assert.AreEqual(new Uri($@"AllLibDiffs", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.LibDiffs, this._diffSession!));

    [TestMethod]
    public void CanNavigateToListOfCompilandDiffs() => Assert.AreEqual(new Uri($@"AllCompilandDiffs", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.CompilandDiffs, this._diffSession!));

    [TestMethod]
    public void CanNavigateToLibSectionContribution()
    {
        Assert.AreEqual(new Uri($@"ContributionDiff?BinarySection=.text&Lib=a.lib", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.ALibDiff.SectionContributionDiffsByName[".text"].BeforeSectionContribution!, this._diffSession!));
        Assert.AreEqual(new Uri($@"ContributionDiff?BinarySection=.text&Lib=a.lib", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.ALibDiff.SectionContributionDiffsByName[".text"].AfterSectionContribution!, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToLibSectionContributionDiff() => Assert.AreEqual(new Uri($@"ContributionDiff?BinarySection=.text&Lib=a.lib", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.ALibDiff.SectionContributionDiffsByName[".text"], this._diffSession!));

    [TestMethod]
    public void CanNavigateToLibCOFFGroupContribution()
    {
        Assert.AreEqual(new Uri($@"ContributionDiff?COFFGroup=.text$zz&Lib=a.lib", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.ALibDiff.COFFGroupContributionDiffsByName[".text$zz"].BeforeCOFFGroupContribution!, this._diffSession!));
        Assert.AreEqual(new Uri($@"ContributionDiff?COFFGroup=.text$zz&Lib=a.lib", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.ALibDiff.COFFGroupContributionDiffsByName[".text$zz"].AfterCOFFGroupContribution!, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToLibCOFFGroupContributionDiff() => Assert.AreEqual(new Uri($@"ContributionDiff?COFFGroup=.text$zz&Lib=a.lib", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.ALibDiff.COFFGroupContributionDiffsByName[".text$zz"], this._diffSession!));

    [TestMethod]
    public void CanNavigateToCompilandSectionContribution()
    {
        Assert.AreEqual(new Uri($@"ContributionDiff?BinarySection=.text&Compiland={Uri.EscapeDataString(@"c:\a\a1.obj")}&Lib={Uri.EscapeDataString(this._testGenerator.ALibDiff.Name)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.A1CompilandDiff.SectionContributionDiffsByName[".text"].BeforeSectionContribution!, this._diffSession!));
        Assert.AreEqual(new Uri($@"ContributionDiff?BinarySection=.text&Compiland={Uri.EscapeDataString(@"c:\a\a1.obj")}&Lib={Uri.EscapeDataString(this._testGenerator.ALibDiff.Name)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.A1CompilandDiff.SectionContributionDiffsByName[".text"].AfterSectionContribution!, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToCompilandSectionContributionDiff() => Assert.AreEqual(new Uri($@"ContributionDiff?BinarySection=.text&Compiland={Uri.EscapeDataString(@"c:\a\a1.obj")}&Lib={Uri.EscapeDataString(this._testGenerator.ALibDiff.Name)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.A1CompilandDiff.SectionContributionDiffsByName[".text"], this._diffSession!));

    [TestMethod]
    public void CanNavigateToCompilandCOFFGroupContribution()
    {
        Assert.AreEqual(new Uri($@"ContributionDiff?COFFGroup=.text$zz&Compiland={Uri.EscapeDataString(@"c:\a\a1.obj")}&Lib={Uri.EscapeDataString(this._testGenerator.ALibDiff.Name)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.A1CompilandDiff.COFFGroupContributionDiffsByName[".text$zz"].BeforeCOFFGroupContribution!, this._diffSession!));
        Assert.AreEqual(new Uri($@"ContributionDiff?COFFGroup=.text$zz&Compiland={Uri.EscapeDataString(@"c:\a\a1.obj")}&Lib={Uri.EscapeDataString(this._testGenerator.ALibDiff.Name)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.A1CompilandDiff.COFFGroupContributionDiffsByName[".text$zz"].AfterCOFFGroupContribution!, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToCompilandCOFFGroupContributionDiff() => Assert.AreEqual(new Uri($@"ContributionDiff?COFFGroup=.text$zz&Compiland={Uri.EscapeDataString(@"c:\a\a1.obj")}&Lib={Uri.EscapeDataString(this._testGenerator.ALibDiff.Name)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(this._testGenerator.A1CompilandDiff.COFFGroupContributionDiffsByName[".text$zz"], this._diffSession!));

    [TestMethod]
    public void CanNavigateToDuplicateDataItem()
    {
        var ddiDiffs = this._testGenerator.GenerateDuplicateDataItemDiffs(out var beforeDDIList, out var afterDDIList);

        this._testGenerator.MockDiffSession.Setup(ds => ds.GetDuplicateDataItemDiffFromDuplicateDataItem(It.IsAny<DuplicateDataItem>()))
                                           .Returns((DuplicateDataItem ddi) => ddiDiffs.SingleOrDefault(diff => diff.BeforeDuplicate == ddi || diff.AfterDuplicate == ddi));

        var ddiDiff = ddiDiffs.First(diff => diff.BeforeDuplicate != null);
        Assert.AreEqual(new Uri($@"DuplicateDataDiff?BeforeDuplicateRVA={Uri.EscapeDataString(ddiDiff.BeforeDuplicate!.Symbol.RVA.ToString(CultureInfo.InvariantCulture))}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(ddiDiff.BeforeDuplicate, this._diffSession!));

        ddiDiff = ddiDiffs.First(diff => diff.BeforeDuplicate is null && diff.AfterDuplicate != null);
        Assert.AreEqual(new Uri($@"DuplicateDataDiff?AfterDuplicateRVA={Uri.EscapeDataString(ddiDiff.AfterDuplicate!.Symbol.RVA.ToString(CultureInfo.InvariantCulture))}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(ddiDiff.AfterDuplicate, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToDuplicateDataItemDiff()
    {
        var ddiDiffs = this._testGenerator.GenerateDuplicateDataItemDiffs(out var beforeDDIList, out var afterDDIList);

        // Test when before != null
        var ddiDiffWithValidBeforeRVA = ddiDiffs.First(ddid => ddid.BeforeDuplicate != null);
        Assert.AreEqual(new Uri($@"DuplicateDataDiff?BeforeDuplicateRVA={Uri.EscapeDataString(ddiDiffWithValidBeforeRVA.BeforeDuplicate!.Symbol.RVA.ToString(CultureInfo.InvariantCulture))}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(ddiDiffWithValidBeforeRVA, this._diffSession!));

        // Test when before is null, so we fall back to after
        var ddiDiffWithNullBeforeRVA = ddiDiffs.First(ddid => ddid.BeforeDuplicate is null);
        Assert.AreEqual(new Uri($@"DuplicateDataDiff?AfterDuplicateRVA={Uri.EscapeDataString(ddiDiffWithNullBeforeRVA.AfterDuplicate!.Symbol.RVA.ToString(CultureInfo.InvariantCulture))}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(ddiDiffWithNullBeforeRVA, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToWastefulVirtualItem()
    {
        var wviDiffs = this._testGenerator.GenerateWastefulVirtualItemDiffs(out var beforeWVIList, out var afterWVIList);

        this._testGenerator.MockDiffSession.Setup(ds => ds.GetWastefulVirtualItemDiffFromWastefulVirtualItem(It.IsAny<WastefulVirtualItem>()))
                                           .Returns((WastefulVirtualItem wvi) => wviDiffs.SingleOrDefault(diff => diff.BeforeWastefulVirtual == wvi || diff.AfterWastefulVirtual == wvi));

        var wviDiff = wviDiffs.First();
        Assert.AreEqual(new Uri($@"WastefulVirtualDiff?TypeName={Uri.EscapeDataString(wviDiff.TypeName)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(wviDiff.BeforeWastefulVirtual!, this._diffSession!));
        Assert.AreEqual(new Uri($@"WastefulVirtualDiff?TypeName={Uri.EscapeDataString(wviDiff.TypeName)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(wviDiff.AfterWastefulVirtual!, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToWastefulVirtualItemDiff()
    {
        var wviDiffs = this._testGenerator.GenerateWastefulVirtualItemDiffs(out _, out _);

        var wviDiff = wviDiffs.First();
        Assert.AreEqual(new Uri($@"WastefulVirtualDiff?TypeName={Uri.EscapeDataString(wviDiff.TypeName)}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(wviDiff, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToTemplateFoldabilityItem()
    {
        var tfiDiffs = this._testGenerator.GenerateTemplateFoldabilityItemDiffs(out var beforeTFIList, out var afterTFIList);

        this._testGenerator.MockDiffSession.Setup(ds => ds.GetTemplateFoldabilityItemDiffFromTemplateFoldabilityItem(It.IsAny<TemplateFoldabilityItem>()))
                                           .Returns((TemplateFoldabilityItem tfi) => tfiDiffs.SingleOrDefault(diff => diff.BeforeTemplateFoldabilityItem == tfi || diff.AfterTemplateFoldabilityItem == tfi));

        var tfiDiff = tfiDiffs.First(diff => diff.BeforeTemplateFoldabilityItem != null && diff.AfterTemplateFoldabilityItem != null);
        Assert.AreEqual(new Uri($@"TemplateFoldabilityDiff?TemplateName=FoldableVolatile%3CT1%3E%28T1%2A%29%20volatile", UriKind.Relative), DiffModelToUriConverter.ModelToUri(tfiDiff.BeforeTemplateFoldabilityItem!, this._diffSession!));
        Assert.AreEqual(new Uri($@"TemplateFoldabilityDiff?TemplateName=FoldableVolatile%3CT1%3E%28T1%2A%29%20volatile", UriKind.Relative), DiffModelToUriConverter.ModelToUri(tfiDiff.AfterTemplateFoldabilityItem!, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToTemplateFoldabilityItemDiff()
    {
        var tfiDiffs = this._testGenerator.GenerateTemplateFoldabilityItemDiffs(out _, out _);

        Assert.AreEqual(new Uri($@"TemplateFoldabilityDiff?TemplateName=SomeNamespace%3A%3AMyType%3A%3AFoldableFunction%3CT1%2CT2%3E%28T2%2C%20T1%29", UriKind.Relative), DiffModelToUriConverter.ModelToUri(tfiDiffs.First(), this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToSymbol()
    {
        var allSymbolDiffsInSectionDiff = this._testGenerator.GenerateSymbolDiffsInBinarySectionList(this._testGenerator.TextSectionDiff);

        this._testGenerator.MockDiffSession.Setup(ds => ds.GetSymbolDiffFromSymbol(It.IsAny<ISymbol>()))
                                           .Returns((ISymbol sym) => allSymbolDiffsInSectionDiff.SingleOrDefault(diff => diff.BeforeSymbol == sym || diff.AfterSymbol == sym));

        var symbolDiff = allSymbolDiffsInSectionDiff.First(sd => sd.BeforeSymbol != null && sd.AfterSymbol != null);
        Assert.AreEqual(new Uri($@"Symbols/SymbolDiff?BeforeRVA={symbolDiff.BeforeSymbol!.RVA}&AfterRVA={symbolDiff.AfterSymbol!.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(symbolDiff.BeforeSymbol, this._diffSession!));
        Assert.AreEqual(new Uri($@"Symbols/SymbolDiff?BeforeRVA={symbolDiff.BeforeSymbol.RVA}&AfterRVA={symbolDiff.AfterSymbol.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(symbolDiff.AfterSymbol, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToCodeBlockSymbol()
    {
        var functionDiffs = this._testGenerator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);

        var codeBlockDiff = functionDiffs.Where(fnDiff => fnDiff.BeforeSymbol is ComplexFunctionCodeSymbol && fnDiff.AfterSymbol is ComplexFunctionCodeSymbol).Select(fnDiff => fnDiff.CodeBlockDiffs.First(codeBlockDiff => codeBlockDiff.BeforeSymbol is SeparatedCodeBlockSymbol && codeBlockDiff.AfterSymbol is SeparatedCodeBlockSymbol)).First();
        var allCodeBlockDiffs = new List<CodeBlockSymbolDiff>() { codeBlockDiff };

        this._testGenerator.MockDiffSession.Setup(ds => ds.GetSymbolDiffFromSymbol(It.IsAny<ISymbol>()))
                                           .Returns((ISymbol sym) => allCodeBlockDiffs.SingleOrDefault(diff => diff.BeforeSymbol == sym || diff.AfterSymbol == sym));

        Assert.AreEqual(new Uri($@"Symbols/CodeBlockSymbolDiff?BeforeRVA={codeBlockDiff.BeforeSymbol!.RVA}&AfterRVA={codeBlockDiff.AfterSymbol!.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(codeBlockDiff.BeforeSymbol, this._diffSession!));
        Assert.AreEqual(new Uri($@"Symbols/CodeBlockSymbolDiff?BeforeRVA={codeBlockDiff.BeforeSymbol.RVA}&AfterRVA={codeBlockDiff.AfterSymbol.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(codeBlockDiff.AfterSymbol, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToCodeBlockSymbolDiff()
    {
        var functionDiffs = this._testGenerator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);

        var codeBlockDiff = functionDiffs.Where(fnDiff => fnDiff.BeforeSymbol is ComplexFunctionCodeSymbol && fnDiff.AfterSymbol is ComplexFunctionCodeSymbol).Select(fnDiff => fnDiff.CodeBlockDiffs.First(codeBlockDiff => codeBlockDiff.BeforeSymbol is SeparatedCodeBlockSymbol && codeBlockDiff.AfterSymbol is SeparatedCodeBlockSymbol)).First();
        Assert.AreEqual(new Uri($@"Symbols/CodeBlockSymbolDiff?BeforeRVA={codeBlockDiff.BeforeSymbol!.RVA}&AfterRVA={codeBlockDiff.AfterSymbol!.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(codeBlockDiff, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToCodeBlockSymbolDiffWithNullBeforeSymbol()
    {
        var functionDiffs = this._testGenerator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);

        var codeBlockDiff = functionDiffs.Where(fnDiff => fnDiff.BeforeSymbol is SimpleFunctionCodeSymbol && fnDiff.AfterSymbol is ComplexFunctionCodeSymbol).Select(fnDiff => fnDiff.CodeBlockDiffs.First(codeBlockDiff => codeBlockDiff.BeforeSymbol is null && codeBlockDiff.AfterSymbol is SeparatedCodeBlockSymbol)).First();
        Assert.AreEqual(new Uri($@"Symbols/CodeBlockSymbolDiff?AfterRVA={codeBlockDiff.AfterSymbol!.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(codeBlockDiff, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToCodeBlockSymbolDiffWithNullAfterSymbol()
    {
        var functionDiffs = this._testGenerator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);

        var codeBlockDiff = functionDiffs.Where(fnDiff => fnDiff.BeforeSymbol is ComplexFunctionCodeSymbol && fnDiff.AfterSymbol is SimpleFunctionCodeSymbol).Select(fnDiff => fnDiff.CodeBlockDiffs.First(codeBlockDiff => codeBlockDiff.BeforeSymbol is SeparatedCodeBlockSymbol && codeBlockDiff.AfterSymbol is null)).First();
        Assert.AreEqual(new Uri($@"Symbols/CodeBlockSymbolDiff?BeforeRVA={codeBlockDiff.BeforeSymbol!.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(codeBlockDiff, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToFunctionCodeSymbolDiff()
    {
        var functionDiffs = this._testGenerator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);

        var functionDiff = functionDiffs.First(fnDiff => fnDiff.BeforeSymbol != null && fnDiff.AfterSymbol != null);
        Assert.AreEqual(new Uri($@"Symbols/FunctionCodeSymbolDiff?BeforeRVA={functionDiff.BeforeSymbol!.PrimaryBlock.RVA}&AfterRVA={functionDiff.AfterSymbol!.PrimaryBlock.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(functionDiff, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToFunctionCodeSymbolDiffWithNullBeforeSymbol()
    {
        var functionDiffs = this._testGenerator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);

        var functionDiff = functionDiffs.First(fnDiff => fnDiff.BeforeSymbol is null && fnDiff.AfterSymbol != null);
        Assert.AreEqual(new Uri($@"Symbols/FunctionCodeSymbolDiff?AfterRVA={functionDiff.AfterSymbol!.PrimaryBlock.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(functionDiff, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToFunctionCodeSymbolDiffWithNullAfterSymbol()
    {
        var functionDiffs = this._testGenerator.GenerateFunctionCodeSymbolDiffs(out var beforeFunctionsList, out var afterFunctionsList);

        var functionDiff = functionDiffs.First(fnDiff => fnDiff.BeforeSymbol != null && fnDiff.AfterSymbol is null);
        Assert.AreEqual(new Uri($@"Symbols/FunctionCodeSymbolDiff?BeforeRVA={functionDiff.BeforeSymbol!.PrimaryBlock.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(functionDiff, this._diffSession!));
    }

    [TestMethod]
    public void CanNavigateToSymbolDiff()
    {
        var allSymbolDiffsInSectionDiff = this._testGenerator.GenerateSymbolDiffsInBinarySectionList(this._testGenerator.TextSectionDiff);
        var symbolDiff = allSymbolDiffsInSectionDiff.First(sd => sd.BeforeSymbol != null && sd.AfterSymbol != null);

        Assert.AreEqual(new Uri($@"Symbols/SymbolDiff?BeforeRVA={symbolDiff.BeforeSymbol!.RVA}&AfterRVA={symbolDiff.AfterSymbol!.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(symbolDiff, this._diffSession!));
    }

    [TestMethod]
    public void NavigatingToSymbolDiffWithNullBeforeSymbolWorks()
    {
        var allSymbolDiffsInSectionDiff = this._testGenerator.GenerateSymbolDiffsInBinarySectionList(this._testGenerator.TextSectionDiff);
        var symbolDiff = allSymbolDiffsInSectionDiff.First(sd => sd.BeforeSymbol is null && sd.AfterSymbol != null && sd.AfterSymbol.RVA > 0);

        Assert.AreEqual(new Uri($@"Symbols/SymbolDiff?AfterRVA={symbolDiff.AfterSymbol!.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(symbolDiff, this._diffSession!));
    }

    [TestMethod]
    public void NavigatingToSymbolDiffWithNullAfterSymbolWorks()
    {
        var allSymbolDiffsInSectionDiff = this._testGenerator.GenerateSymbolDiffsInBinarySectionList(this._testGenerator.TextSectionDiff);
        var symbolDiff = allSymbolDiffsInSectionDiff.First(sd => sd.BeforeSymbol != null && sd.BeforeSymbol.RVA > 0 && sd.AfterSymbol is null);

        Assert.AreEqual(new Uri($@"Symbols/SymbolDiff?BeforeRVA={symbolDiff.BeforeSymbol!.RVA}", UriKind.Relative), DiffModelToUriConverter.ModelToUri(symbolDiff, this._diffSession!));
    }

    public class TestContributionDiff : ContributionDiff
    {
        public TestContributionDiff(Contribution before, Contribution after) : base("test contribution diff", before, after)
        { }
    }

    [ExpectedException(typeof(InvalidOperationException), AllowDerivedTypes = false)]
    [TestMethod]
    public void NavigatingToContributionDiffThrows() =>
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.  This test is explicitly testing null
            _ = DiffModelToUriConverter.ModelToUri(new TestContributionDiff(null, null), this._diffSession!);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.


    public class TestContribution : Contribution
    {
        public TestContribution() : base("test contribution")
        { }
    }

    [ExpectedException(typeof(InvalidOperationException), AllowDerivedTypes = false)]
    [TestMethod]
    public void NavigatingToContributionThrows() => _ = DiffModelToUriConverter.ModelToUri(new TestContribution(), this._diffSession!);

    [TestMethod]
    public void NavigatingToOtherStuffReturnsError()
    {
        Assert.AreEqual(new Uri("Error/System.Object", UriKind.Relative), DiffModelToUriConverter.ModelToUri(new object(), this._diffSession!));
        Assert.AreEqual(new Uri("Error/SizeBench.AnalysisEngine.RVARange", UriKind.Relative), DiffModelToUriConverter.ModelToUri(new RVARange(0, 0), this._diffSession!));
    }

    public void Dispose() => this._testGenerator.Dispose();
}
