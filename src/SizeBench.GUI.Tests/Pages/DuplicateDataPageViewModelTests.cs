using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class DuplicateDataPageViewModelTests : IDisposable
{
    public Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private StaticDataSymbol? DataOfDupe;
    private SessionDataCache DataCache = new SessionDataCache();
    private uint NextSymIndexId;

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.MockSession.SetupAllProperties();

        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();

        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };

        this.DataOfDupe = new StaticDataSymbol(this.DataCache, "test dupe", rva: 6789,
                                               size: 0, isVirtualSize: false, symIndexId: this.NextSymIndexId++,
                                               dataKind: DataKind.DataIsFileStatic,
                                               type: null, referencedIn: null, functionParent: null);
    }

    [TestMethod]
    public async Task ViewModelPropertiesInitializeCorrectly()
    {
        var lib = new Library("test lib name");
        var compiland = new Compiland(this.DataCache, "1.obj", lib, CommonCommandLines.NullCommandLine, compilandSymIndex: this.NextSymIndexId++);
        var dupe = new DuplicateDataItem(this.DataOfDupe!, compiland);

        var duplicates = new List<DuplicateDataItem>() { dupe };

        this.MockSession.Setup(s => s.EnumerateDuplicateDataItems(It.IsAny<CancellationToken>())).Returns(Task.FromResult(duplicates as IReadOnlyList<DuplicateDataItem>));

        var viewmodel = new DuplicateDataPageViewModel(this.MockUITaskScheduler.Object,
                                                       this.MockSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "DuplicateRVA", dupe.Symbol.RVA.ToString(CultureInfo.InvariantCulture) }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(dupe, viewmodel.DuplicateDataItem));
        Assert.AreEqual(this.DataOfDupe!.RVA, viewmodel.DuplicateDataItem!.Symbol.RVA);
        Assert.AreEqual(this.DataOfDupe.Name, viewmodel.DuplicateDataItem.Symbol.Name);
    }

    public void Dispose() => this.DataCache.Dispose();
}
