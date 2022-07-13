using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class WastefulVirtualPageViewModelTests : IDisposable
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private SessionDataCache DataCache = new SessionDataCache();
    UserDefinedTypeSymbol? UDT;
    UserDefinedTypeSymbol? DerivedUDT1;
    UserDefinedTypeSymbol? DerivedUDT2;
    SimpleFunctionCodeSymbol? WastedFunction1;
    private List<WastefulVirtualItem> WastefulVirtualItems = new List<WastefulVirtualItem>();
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
        this.nextSymIndexId = 0;

        var diaAdapter = new TestDIAAdapter();
        this.UDT = new UserDefinedTypeSymbol(this.DataCache, diaAdapter, this.MockSession.Object, name: "CBase", instanceSize: 100, symIndexId: this.nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var baseclasses = new Dictionary<uint, uint>(capacity: 1)
            {
                { this.UDT.SymIndexId, 0 }
            };
        this.DerivedUDT1 = new UserDefinedTypeSymbol(this.DataCache, diaAdapter, this.MockSession.Object, name: "CDerived1", instanceSize: 120, symIndexId: this.nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: baseclasses);
        this.DerivedUDT2 = new UserDefinedTypeSymbol(this.DataCache, diaAdapter, this.MockSession.Object, name: "CDerived2", instanceSize: 110, symIndexId: this.nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: baseclasses);

        this.WastedFunction1 = new SimpleFunctionCodeSymbol(this.DataCache, "WastedFunction1", rva: 100, size: 50, symIndexId: this.nextSymIndexId++);

        var item = new WastefulVirtualItem(this.UDT, isCOMType: false, bytesPerWord: 4);
        item.AddWastedOverrideThatIsNotPureWithNoOverrides(this.WastedFunction1);
        this.UDT.AddDerivedType(this.DerivedUDT1);
        this.UDT.AddDerivedType(this.DerivedUDT2);
        this.UDT.MarkDerivedTypesLoaded();

        this.WastefulVirtualItems = new List<WastefulVirtualItem>() { item };

        this.MockSession.Setup(s => s.EnumerateWastefulVirtuals(It.IsAny<CancellationToken>())).Returns(Task.FromResult((IReadOnlyList<WastefulVirtualItem>)this.WastefulVirtualItems));
    }

    [TestMethod]
    public async Task ViewModelPropertiesInitializeCorrectly()
    {
        var viewmodel = new WastefulVirtualPageViewModel(this.MockUITaskScheduler.Object,
                                                         this.MockSession.Object);
        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "TypeName", "CBase" }
            });
        await viewmodel.InitializeAsync();

        var wviExpected = this.WastefulVirtualItems.Single(wvi => wvi.UserDefinedType.Name == "CBase");

        Assert.IsTrue(ReferenceEquals(wviExpected, viewmodel.WastefulVirtualItem));
    }

    public void Dispose() => this.DataCache.Dispose();
}
