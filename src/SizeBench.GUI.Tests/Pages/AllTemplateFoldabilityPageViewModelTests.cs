using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class AllTemplateFoldabilityPageViewModelTests : IDisposable
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private SessionDataCache DataCache = new SessionDataCache();
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.MockExcelExporter = new Mock<IExcelExporter>();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        this.TestDIAAdapter = new TestDIAAdapter();

        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                         It.IsAny<IList<string>>(),
                                                                                         It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()));
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task ExcelExportIsFormattedUsefully()
    {
        uint nextSymIndexId = 1;
        var foldables = TestTemplateFoldabilityItems.GenerateSomeTemplateFoldabilityItems(this.MockSession, this.DataCache, this.TestDIAAdapter, ref nextSymIndexId, CancellationToken.None);

        this.MockSession.Setup(s => s.EnumerateTemplateFoldabilityItems(It.IsAny<CancellationToken>())).Returns(Task.FromResult(foldables as IReadOnlyList<TemplateFoldabilityItem>));

        var viewmodel = new AllTemplateFoldabilityPageViewModel(this.MockUITaskScheduler.Object,
                                                                this.MockSession.Object,
                                                                this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();
        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        Assert.AreEqual(7, columnHeaders.Length);
        Assert.AreEqual("Template Name", columnHeaders[0]);
        Assert.AreEqual("Total Size", columnHeaders[1]);
        Assert.AreEqual("Wasted Size", columnHeaders[2]);
        Assert.AreEqual("# Symbols", columnHeaders[3]);
        Assert.AreEqual("# Unique Symbols (post-folding)", columnHeaders[4]);
        Assert.AreEqual("% Similarity", columnHeaders[5]);
        Assert.AreEqual("Example Symbols", columnHeaders[6]);

        var i = viewmodel.TemplateFoldabilityItems!.ToList().IndexOf(viewmodel.TemplateFoldabilityItems!.Single(tfi => tfi.TemplateName == "SomeNamespace::MyType::FoldableFunction<T1,T2>(T2, T1)"));

        Assert.AreEqual("SomeNamespace::MyType::FoldableFunction<T1,T2>(T2, T1)", preformattedData[i]["Template Name"]);
        Assert.AreEqual(20u, preformattedData[i]["Total Size"]);
        Assert.AreEqual((uint)(20 * 0.8f), preformattedData[i]["Wasted Size"]);
        Assert.AreEqual(3, preformattedData[i]["# Symbols"]);
        Assert.AreEqual(2, preformattedData[i]["# Unique Symbols (post-folding)"]);
        Assert.AreEqual("80.0 %", preformattedData[i]["% Similarity"]);
        StringAssert.Contains(preformattedData[i]["Example Symbols"].ToString(), "SomeNamespace::MyType::FoldableFunction<AComplex::Type<int>,bool>(bool, AComplex::Type<int>)", StringComparison.Ordinal);
        StringAssert.Contains(preformattedData[i]["Example Symbols"].ToString(), "SomeNamespace::MyType::FoldableFunction<AComplex::Type<float>,bool>(bool, AComplex::Type<float>)", StringComparison.Ordinal);
        StringAssert.Contains(preformattedData[i]["Example Symbols"].ToString(), "SomeNamespace::MyType::FoldableFunction<AComplex::Type<SomeUDT>,bool>(bool, AComplex::Type<SomeUDT>)", StringComparison.Ordinal);

        Assert.IsTrue(viewmodel.ExportToExcelCommand.CanExecute());
        viewmodel.ExportToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                     It.IsAny<IList<string>>(),
                                                                                     It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()),
                                        Times.Exactly(1));
    }

    public void Dispose() => this.DataCache.Dispose();
}
