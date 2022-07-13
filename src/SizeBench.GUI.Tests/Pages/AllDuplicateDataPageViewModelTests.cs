using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class AllDuplicateDataPageViewModelTests : IDisposable
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private SessionDataCache DataCache = new SessionDataCache();

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

        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                         It.IsAny<IList<string>>(),
                                                                                         It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()));
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task ExcelExportIsFormattedUsefully()
    {
        var lib = new Library("test lib name");
        var compiland1 = new Compiland(this.DataCache, @"c:\foo\1.obj", lib, CommonCommandLines.NullCommandLine, compilandSymIndex: 0);
        var compiland2 = new Compiland(this.DataCache, @"c:\foo\2.obj", lib, CommonCommandLines.NullCommandLine, compilandSymIndex: 1);
        var compiland3 = new Compiland(this.DataCache, @"c:\foo\3.obj", lib, CommonCommandLines.NullCommandLine, compilandSymIndex: 2);

        var data = new StaticDataSymbol(this.DataCache, "test dupe 1", rva: 1234, size: 12, isVirtualSize: false,
                                        symIndexId: 3, dataKind: DataKind.DataIsFileStatic,
                                        type: null, referencedIn: null, functionParent: null);
        var dupe1 = new DuplicateDataItem(data, compiland1);
        dupe1.AddReferencedCompilandIfNecessary(compiland2, 1235);
        dupe1.AddReferencedCompilandIfNecessary(compiland3, 1236);

        data = new StaticDataSymbol(this.DataCache, "test dupe 2", rva: 2345, size: 256, isVirtualSize: false,
                                    symIndexId: 4, dataKind: DataKind.DataIsFileStatic,
                                    type: null, referencedIn: null, functionParent: null);
        var dupe2 = new DuplicateDataItem(data, compiland2);
        dupe2.AddReferencedCompilandIfNecessary(compiland3, 2346);

        var duplicates = new List<DuplicateDataItem>() { dupe1, dupe2 };
        this.MockSession.Setup(s => s.EnumerateDuplicateDataItems(It.IsAny<CancellationToken>())).Returns(Task.FromResult(duplicates as IReadOnlyList<DuplicateDataItem>));

        var viewmodel = new AllDuplicateDataPageViewModel(this.MockUITaskScheduler.Object,
                                                          this.MockSession.Object,
                                                          this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();
        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        Assert.AreEqual(4, columnHeaders.Length);
        Assert.AreEqual("Symbol Name", columnHeaders[0]);
        Assert.AreEqual("Size", columnHeaders[1]);
        Assert.AreEqual("Wasted Size", columnHeaders[2]);
        Assert.AreEqual("Referenced In", columnHeaders[3]);

        Assert.AreEqual("test dupe 1", preformattedData[0]["Symbol Name"]);
        Assert.AreEqual(12u, preformattedData[0]["Size"]);
        Assert.AreEqual(24u, preformattedData[0]["Wasted Size"]);
        Assert.AreEqual("1.obj, 2.obj, 3.obj", preformattedData[0]["Referenced In"]);

        Assert.AreEqual("test dupe 2", preformattedData[1]["Symbol Name"]);
        Assert.AreEqual(256u, preformattedData[1]["Size"]);
        Assert.AreEqual(256u, preformattedData[1]["Wasted Size"]);
        Assert.AreEqual("2.obj, 3.obj", preformattedData[1]["Referenced In"]);

        Assert.IsTrue(viewmodel.ExportToExcelCommand.CanExecute());
        viewmodel.ExportToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                          It.IsAny<IList<string>>(),
                                                                                          It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()),
                                        Times.Exactly(1));
    }

    public void Dispose() => this.DataCache.Dispose();
}
