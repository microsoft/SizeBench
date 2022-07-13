using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class AllWastefulVirtualsPageViewModelTests : IDisposable
{
    private SessionDataCache DataCache = new SessionDataCache();
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        this.TestDIAAdapter = new TestDIAAdapter();
        this.MockSession = new Mock<ISession>();
        this.MockSession.Setup(s => s.BytesPerWord).Returns(8);
        this.MockExcelExporter = new Mock<IExcelExporter>();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();

        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                         It.IsAny<IList<string>>(),
                                                                                         It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()));
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task ExcelExportIsFormattedUsefully()
    {
        var UDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "CBase", instanceSize: 100, symIndexId: 1, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var baseclasses = new Dictionary<uint, uint>(capacity: 1)
            {
                { UDT.SymIndexId, 0 }
            };
        var DerivedUDT1 = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "CDerived1", instanceSize: 120, symIndexId: 2, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: baseclasses);
        var DerivedUDT2 = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "CDerived2", instanceSize: 110, symIndexId: 3, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: baseclasses);

        var WastedFunction1 = new SimpleFunctionCodeSymbol(this.DataCache, "WastedFunction1", rva: 100, size: 50, symIndexId: 4);
        var WastedFunction2 = new SimpleFunctionCodeSymbol(this.DataCache, "WastedFunction2", rva: 150, size: 100, symIndexId: 5);
        var WastedFunction3 = new SimpleFunctionCodeSymbol(this.DataCache, "WastedFunction3", rva: 250, size: 100, symIndexId: 6);

        var item = new WastefulVirtualItem(UDT, isCOMType: false, bytesPerWord: 8);
        UDT.AddDerivedType(DerivedUDT1);
        UDT.AddDerivedType(DerivedUDT2);
        UDT.MarkDerivedTypesLoaded();

        item.AddWastedOverrideThatIsNotPureWithNoOverrides(WastedFunction1);
        item.AddWastedOverrideThatIsNotPureWithNoOverrides(WastedFunction2);
        item.AddWastedOverrideThatIsPureWithExactlyOneOverride(WastedFunction3);

        var allWastefulVirtuals = new List<WastefulVirtualItem>()
            {
                item
            };

        this.MockSession.Setup(s => s.EnumerateWastefulVirtuals(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allWastefulVirtuals as IReadOnlyList<WastefulVirtualItem>));

        var viewmodel = new AllWastefulVirtualsPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.MockSession.Object,
                                                             this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();
        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        Assert.AreEqual(5, columnHeaders.Length);
        Assert.AreEqual("Type Name", columnHeaders[0]);
        Assert.AreEqual("Waste Per Slot", columnHeaders[1]);
        Assert.AreEqual("Wasted Size Total", columnHeaders[2]);
        Assert.AreEqual("Wasteful pure virtuals with exactly one override", columnHeaders[3]);
        Assert.AreEqual("Wasteful virtuals with no overrides", columnHeaders[4]);

        Assert.AreEqual("CBase", preformattedData[0]["Type Name"]);
        Assert.AreEqual(8 /* bytes per word */ * 3 /* # base+derived types */, preformattedData[0]["Waste Per Slot"]);
        Assert.AreEqual(8 /* bytes per word */ * 3 /* # base+derived types */ * 3u /* # wasteful virtual functions */, preformattedData[0]["Wasted Size Total"]);
        Assert.AreEqual("WastedFunction3()", preformattedData[0]["Wasteful pure virtuals with exactly one override"]);
        Assert.AreEqual($"WastedFunction1(){Environment.NewLine}WastedFunction2()", preformattedData[0]["Wasteful virtuals with no overrides"]);

        Assert.IsTrue(viewmodel.ExportToExcelCommand.CanExecute());
        viewmodel.ExportToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                     It.IsAny<IList<string>>(),
                                                                                     It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()),
                                        Times.Exactly(1));
    }

    [TestMethod]
    public async Task TogglingExcludeCOMTypesRefreshesView()
    {
        uint nextSymIndexId = 1;
        var UDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "CBase", instanceSize: 100, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var baseclasses = new Dictionary<uint, uint>(capacity: 1)
            {
                { UDT.SymIndexId, 0 }
            };
        var DerivedUDT1 = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "CDerived1", instanceSize: 120, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: baseclasses);
        var DerivedUDT2 = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "CDerived2", instanceSize: 110, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: baseclasses);

        var WastedFunction1 = new SimpleFunctionCodeSymbol(this.DataCache, "WastedFunction1", rva: 100, size: 50, symIndexId: nextSymIndexId++);
        var WastedFunction2 = new SimpleFunctionCodeSymbol(this.DataCache, "WastedFunction2", rva: 150, size: 100, symIndexId: nextSymIndexId++);

        // A non-COM type
        var nonCOMType1 = new WastefulVirtualItem(UDT, isCOMType: false, bytesPerWord: 8);
        UDT.AddDerivedType(DerivedUDT1);
        UDT.AddDerivedType(DerivedUDT2);
        UDT.MarkDerivedTypesLoaded();
        nonCOMType1.AddWastedOverrideThatIsNotPureWithNoOverrides(WastedFunction1);
        nonCOMType1.AddWastedOverrideThatIsNotPureWithNoOverrides(WastedFunction2);

        // A COM type
        var iUnk = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "IUnknown", instanceSize: 8, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtStruct, baseTypeIDs: null);
        var baseIUnk = new Dictionary<uint, uint>(capacity: 1)
            {
                { iUnk.SymIndexId, 0 }
            };
        var UDT2 = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "MyCOMType", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: baseIUnk);
        UDT2.MarkDerivedTypesLoaded();
        var COMType1 = new WastefulVirtualItem(UDT2, isCOMType: true, bytesPerWord: 8);
        COMType1.AddWastedOverrideThatIsNotPureWithNoOverrides(WastedFunction1);

        var UDT3 = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "MyCOMType2", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: baseIUnk);
        UDT3.MarkDerivedTypesLoaded();
        var COMType2 = new WastefulVirtualItem(UDT3, isCOMType: true, bytesPerWord: 8);
        COMType2.AddWastedOverrideThatIsNotPureWithNoOverrides(WastedFunction1);

        var allWastefulVirtuals = new List<WastefulVirtualItem>()
            {
                nonCOMType1,
                COMType1,
                COMType2
            };

        this.MockSession.Setup(s => s.EnumerateWastefulVirtuals(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allWastefulVirtuals as IReadOnlyList<WastefulVirtualItem>));

        var viewmodel = new AllWastefulVirtualsPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.MockSession.Object,
                                                             this.MockExcelExporter.Object);
        await viewmodel.SetCurrentFragment("ExcludeCOMTypes");
        await viewmodel.InitializeAsync();

        var vmPropertyChangesSeen = new List<string>();
        viewmodel.PropertyChanged += (s, e) => vmPropertyChangesSeen.Add(e.PropertyName!);
        var collectionChangesSeen = 0;
        viewmodel.WastefulVirtualItems!.CollectionChanged += (s, e) => collectionChangesSeen++;

        Assert.IsTrue(viewmodel.ExcludeCOMTypes);
        Assert.AreEqual(1, viewmodel.WastefulVirtualItems.Cast<WastefulVirtualItem>().ToList().Count);

        viewmodel.ExcludeCOMTypes = false;

        Assert.AreEqual(1, vmPropertyChangesSeen.Count);
        Assert.AreEqual(nameof(AllWastefulVirtualsPageViewModel.ExcludeCOMTypes), vmPropertyChangesSeen[0]);
        Assert.AreEqual(3, viewmodel.WastefulVirtualItems.Cast<WastefulVirtualItem>().ToList().Count);
        Assert.AreEqual(1, collectionChangesSeen); // Even though we filtered out multiple items, should just see one INCC due to the DeferRefresh, to keep the UI responsive

        // Toggling back should restore everything to the way it was
        viewmodel.ExcludeCOMTypes = true;
        Assert.AreEqual(2, vmPropertyChangesSeen.Count);
        Assert.AreEqual(nameof(AllWastefulVirtualsPageViewModel.ExcludeCOMTypes), vmPropertyChangesSeen[0]);
        Assert.AreEqual(nameof(AllWastefulVirtualsPageViewModel.ExcludeCOMTypes), vmPropertyChangesSeen[1]);
        Assert.AreEqual(1, viewmodel.WastefulVirtualItems.Cast<WastefulVirtualItem>().ToList().Count);
        Assert.AreEqual(2, collectionChangesSeen);
    }

    public void Dispose() => this.DataCache.Dispose();
}
