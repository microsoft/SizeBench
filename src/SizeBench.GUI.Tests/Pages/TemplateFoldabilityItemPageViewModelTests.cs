using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class TemplateFoldabilityPageViewModelTests : IDisposable
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private SessionDataCache DataCache = new SessionDataCache();
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private List<TemplateFoldabilityItem> TemplateFoldabilityItems = new List<TemplateFoldabilityItem>();
    private uint nextSymIndexId;

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
        this.TestDIAAdapter = new TestDIAAdapter();

        this.nextSymIndexId = 0;
        this.TemplateFoldabilityItems = TestTemplateFoldabilityItems.GenerateSomeTemplateFoldabilityItems(this.MockSession, this.DataCache, this.TestDIAAdapter, ref this.nextSymIndexId, CancellationToken.None);

        this.MockSession.Setup(s => s.EnumerateTemplateFoldabilityItems(It.IsAny<CancellationToken>())).Returns(Task.FromResult((IReadOnlyList<TemplateFoldabilityItem>)this.TemplateFoldabilityItems));
    }

    [Timeout(5 * 1000)]
    [TestMethod]
    public async Task SettingBothDisassemblySymbolsKicksOffDisassemblyProcess()
    {
        var viewmodel = new TemplateFoldabilityItemPageViewModel(this.MockUITaskScheduler.Object,
                                                                 this.MockSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "TemplateName", "SomeNamespace::MyType::FoldableFunction<T1,T2>(T2, T1)" }
            });
        await viewmodel.InitializeAsync();

        var tfiExpected = this.TemplateFoldabilityItems.Single(tfi => tfi.TemplateName == "SomeNamespace::MyType::FoldableFunction<T1,T2>(T2, T1)");
        var function1 = viewmodel.UniqueSymbols![0];
        var function2 = viewmodel.UniqueSymbols![1];

        var tcsDisassembly1 = new TaskCompletionSource<bool>();
        var tcsDisassembly2 = new TaskCompletionSource<bool>();
        this.MockSession.Setup(s => s.DisassembleFunction(function1, It.IsAny<DisassembleFunctionOptions>(), CancellationToken.None)).Returns(Task.FromResult("disasm 1"));
        this.MockSession.Setup(s => s.DisassembleFunction(function2, It.IsAny<DisassembleFunctionOptions>(), CancellationToken.None)).Returns(Task.FromResult("disasm 2"));

        viewmodel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TemplateFoldabilityItemPageViewModel.Disassembly1))
            {
                tcsDisassembly1.TrySetResult(true);
            }
            else if (e.PropertyName == nameof(TemplateFoldabilityItemPageViewModel.Disassembly2))
            {
                tcsDisassembly2.TrySetResult(true);
            }
        };

        viewmodel.Disassembly1Symbol = function1;
        viewmodel.Disassembly2Symbol = function2;

        await tcsDisassembly1.Task;
        await tcsDisassembly2.Task;

        Assert.AreEqual("disasm 1", viewmodel.Disassembly1);
        Assert.AreEqual("disasm 2", viewmodel.Disassembly2);

        // Resetting one of the symbols to null causes the disassembly to go away
        tcsDisassembly1 = new TaskCompletionSource<bool>();
        tcsDisassembly2 = new TaskCompletionSource<bool>();
        viewmodel.Disassembly1Symbol = null;

        await tcsDisassembly1.Task;

        Assert.IsNull(viewmodel.Disassembly1);
        Assert.AreEqual("disasm 2", viewmodel.Disassembly2);

        // Resetting the other does the same
        tcsDisassembly1 = new TaskCompletionSource<bool>();
        tcsDisassembly2 = new TaskCompletionSource<bool>();
        viewmodel.Disassembly1Symbol = function1;
        viewmodel.Disassembly2Symbol = null;

        await tcsDisassembly1.Task;
        await tcsDisassembly2.Task;

        Assert.AreEqual("disasm 1", viewmodel.Disassembly1);
        Assert.IsNull(viewmodel.Disassembly2);

        // When both are set to the same thing, that also leaves us with no good disassembly, so null.
        tcsDisassembly1 = new TaskCompletionSource<bool>();
        tcsDisassembly2 = new TaskCompletionSource<bool>();
        viewmodel.Disassembly2Symbol = function1;

        await tcsDisassembly2.Task;

        Assert.IsNull(viewmodel.Disassembly1);
        Assert.IsNull(viewmodel.Disassembly2);
    }

    public void Dispose() => this.DataCache.Dispose();
}
