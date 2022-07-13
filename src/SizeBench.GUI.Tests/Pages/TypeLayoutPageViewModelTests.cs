using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Pages;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBenchV2.ViewModels.Tests;

[TestClass]
public sealed class TypeLayoutPageViewModelTests
{
    public Mock<ISession> MockSession = new Mock<ISession>();
    public Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    public Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockSession.SetupAllProperties();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
        this.MockExcelExporter = new Mock<IExcelExporter>();
    }

    private TypeLayoutPageViewModel CreateViewModelForTest()
    {
        return new TypeLayoutPageViewModel(this.MockUITaskScheduler.Object,
                                           this.MockExcelExporter.Object,
                                           this.MockSession.Object);
    }

    [TestMethod]
    public void InitialConstructionIsValid()
    {
        var vm = CreateViewModelForTest();

        Assert.IsNotNull(vm.LoadTypeCommand);
        Assert.IsNotNull(vm.ViewLayoutsOfSpecificTypesCommand);
        Assert.IsNull(vm.TypeNameToLoad);
        Assert.AreEqual(0, vm.TypeLayoutItems.Count);
        Assert.AreEqual("Type Layout", vm.PageTitle);
        Assert.IsFalse(vm.ExportToExcelCommand.CanExecute());
    }

    [TestMethod]
    public async Task WildcardFragmentLoadsAllLayouts()
    {
        var allLayouts = new List<TypeLayoutItem>();
        var vm = CreateViewModelForTest();
        var tli = CreateSomeTypeLayoutsAndSetupMockAllTypeLayouts();
        await vm.SetCurrentFragment("*");
        Assert.AreEqual("Type Layout: *", vm.PageTitle);
        this.MockSession.Verify(s => s.LoadAllTypeLayouts(It.IsAny<CancellationToken>()), Times.Once());
        CollectionAssert.AreEqual(new List<TypeLayoutItem>() { tli }, vm.TypeLayoutItems.ToList());
    }

    [TestMethod]
    public async Task TypeNameInFragmentIsLoaded()
    {
        using var cache = new SessionDataCache();
        var mockDIAAdapter = new Mock<IDIAAdapter>();
        var udt = new UserDefinedTypeSymbol(cache, mockDIAAdapter.Object, this.MockSession.Object, "TypeNameToLoad", 4, symIndexId: 1, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);

        var tli = new TypeLayoutItem(udt, 0, 0, baseTypeLayouts: null, memberLayouts: null);

        var allLayouts = new List<TypeLayoutItem>()
            {
                tli
            };
        this.MockSession.Setup(s => s.LoadTypeLayoutsByName("TypeNameToLoad", It.IsAny<CancellationToken>())).Returns(Task.FromResult(allLayouts as IReadOnlyList<TypeLayoutItem>));
        var vm = CreateViewModelForTest();
        await vm.SetCurrentFragment("TypeNameToLoad");
        Assert.AreEqual("Type Layout: TypeNameToLoad", vm.PageTitle);
        this.MockSession.Verify(s => s.LoadTypeLayoutsByName("TypeNameToLoad", It.IsAny<CancellationToken>()), Times.Once());
        CollectionAssert.AreEqual(allLayouts, vm.TypeLayoutItems.ToList());
    }

    [TestMethod]
    public async Task UninterestingTypesAreNotShownInTreeView()
    {
        var tli = CreateSomeTypeLayoutsAndSetupMockAllTypeLayouts();

        var vm = CreateViewModelForTest();
        await vm.SetCurrentFragment("*");
        this.MockSession.Verify(s => s.LoadAllTypeLayouts(It.IsAny<CancellationToken>()), Times.Once());
        CollectionAssert.AreEqual(new TypeLayoutItem[] { tli }, vm.TypeLayoutItems.ToList());
    }

    private TypeLayoutItem CreateSomeTypeLayoutsAndSetupMockAllTypeLayouts()
    {
        using var cache = new SessionDataCache();
        var mockDIAAdapter = new Mock<IDIAAdapter>();
        uint nextSymIndexId = 1;
        // 0-size members are not interesting to see in the TreeView, they add clutter, so we'll filter them out
        var udtZeroSize = new UserDefinedTypeSymbol(cache, mockDIAAdapter.Object, this.MockSession.Object, "ZeroSize", 0, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var udt = new UserDefinedTypeSymbol(cache, mockDIAAdapter.Object, this.MockSession.Object, "TypeNameToLoad", 4, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        // A member that has a size of 1, with only an alignment member, is very likely an empty base class, which again is not interesting for looking at
        // size in the UI, so it's filtered out.
        var udtEBC = new UserDefinedTypeSymbol(cache, mockDIAAdapter.Object, this.MockSession.Object, "EBC", 1, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);

        var tliZeroSize = new TypeLayoutItem(udtZeroSize, 0, 0, null, null);
        var tli = new TypeLayoutItem(udt, 0, 0, baseTypeLayouts: null, memberLayouts: null);
        var ebcAlignmentMemberLayout = new TypeLayoutItemMember[]
        {
                TypeLayoutItemMember.CreateAlignmentMember(1, 0, false, 0, isTailSlop: true)
        };

        var tliEBC = new TypeLayoutItem(udtEBC, 1, 0, baseTypeLayouts: null, memberLayouts: ebcAlignmentMemberLayout);
        var allLayouts = new List<TypeLayoutItem>()
            {
                tliZeroSize,
                tli,
                tliEBC
            };
        this.MockSession.Setup(s => s.LoadAllTypeLayouts(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allLayouts as IReadOnlyList<TypeLayoutItem>));
        return tli;
    }

    [TestMethod]
    public void UDTCanLoad()
    {
        using var cache = new SessionDataCache();
        var mockDIAAdapter = new Mock<IDIAAdapter>();
        var udt = new UserDefinedTypeSymbol(cache, mockDIAAdapter.Object, this.MockSession.Object, "MyTypeName", 4, symIndexId: 1, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var vm = CreateViewModelForTest();

        var tli = new TypeLayoutItem(udt, 0, 0, baseTypeLayouts: null, memberLayouts: null);
        this.MockSession.Setup(s => s.LoadTypeLayout(udt, It.IsAny<CancellationToken>())).Returns(Task.FromResult(tli));
        vm.LoadTypeCommand.Execute(udt);

        Assert.AreEqual("MyTypeName", vm.TypeNameToLoad);
        this.MockSession.Verify(s => s.LoadTypeLayout(udt, It.IsAny<CancellationToken>()), Times.Exactly(1));
        Assert.AreEqual(1, vm.TypeLayoutItems.Count);
        Assert.IsTrue(ReferenceEquals(tli, vm.TypeLayoutItems[0]));
    }

    [TestMethod]
    public void ModifiedTypesCanBeLoaded()
    {
        using var cache = new SessionDataCache();
        var mockDIAAdapter = new Mock<IDIAAdapter>();
        uint nextSymIndexId = 1;
        var udt = new UserDefinedTypeSymbol(cache, mockDIAAdapter.Object, this.MockSession.Object, "MyTypeName", 4, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var pointerToUDT = new PointerTypeSymbol(cache, udt, "MyTypeName*", 8, nextSymIndexId++);
        var constUDT = new ModifiedTypeSymbol(cache, udt, "const MyTypeName", 4, nextSymIndexId++);
        var arrayOfUDTs = new ArrayTypeSymbol(cache, "MyTypeName[]", udt.InstanceSize * 3, nextSymIndexId++, udt, elementCount: 3);
        var pointerToConstUDT = new PointerTypeSymbol(cache, constUDT, "const MyTypeName*", 8, nextSymIndexId++);
        var arrayOfPointerToConstUDT = new ArrayTypeSymbol(cache, "const MyTypeName*[]", 8 * 3, nextSymIndexId++, pointerToConstUDT, elementCount: 3);
        var vm = CreateViewModelForTest();

        var tli = new TypeLayoutItem(udt, 0, 0, baseTypeLayouts: null, memberLayouts: null);
        this.MockSession.Setup(s => s.LoadTypeLayout(udt, It.IsAny<CancellationToken>())).Returns(Task.FromResult(tli));
        this.MockSession.Setup(s => s.LoadTypeLayout(pointerToUDT, It.IsAny<CancellationToken>())).Returns(Task.FromResult(tli));
        this.MockSession.Setup(s => s.LoadTypeLayout(constUDT, It.IsAny<CancellationToken>())).Returns(Task.FromResult(tli));
        this.MockSession.Setup(s => s.LoadTypeLayout(arrayOfUDTs, It.IsAny<CancellationToken>())).Returns(Task.FromResult(tli));
        this.MockSession.Setup(s => s.LoadTypeLayout(arrayOfPointerToConstUDT, It.IsAny<CancellationToken>())).Returns(Task.FromResult(tli));

        vm.LoadTypeCommand.Execute(pointerToUDT);
        this.MockSession.Verify(s => s.LoadTypeLayout(pointerToUDT, It.IsAny<CancellationToken>()), Times.Exactly(1));
        Assert.AreEqual(udt.Name, vm.TypeNameToLoad);
        Assert.AreEqual(1, vm.TypeLayoutItems.Count);
        Assert.IsTrue(ReferenceEquals(tli, vm.TypeLayoutItems[0]));

        vm.TypeNameToLoad = "dummy";

        vm.LoadTypeCommand.Execute(constUDT);
        this.MockSession.Verify(s => s.LoadTypeLayout(constUDT, It.IsAny<CancellationToken>()), Times.Exactly(1));
        Assert.AreEqual(udt.Name, vm.TypeNameToLoad);
        Assert.AreEqual(1, vm.TypeLayoutItems.Count);
        Assert.IsTrue(ReferenceEquals(tli, vm.TypeLayoutItems[0]));

        vm.TypeNameToLoad = "dummy";

        vm.LoadTypeCommand.Execute(arrayOfUDTs);
        this.MockSession.Verify(s => s.LoadTypeLayout(arrayOfUDTs, It.IsAny<CancellationToken>()), Times.Exactly(1));
        Assert.AreEqual(udt.Name, vm.TypeNameToLoad);
        Assert.AreEqual(1, vm.TypeLayoutItems.Count);
        Assert.IsTrue(ReferenceEquals(tli, vm.TypeLayoutItems[0]));

        vm.TypeNameToLoad = "dummy";

        vm.LoadTypeCommand.Execute(arrayOfPointerToConstUDT);
        this.MockSession.Verify(s => s.LoadTypeLayout(arrayOfPointerToConstUDT, It.IsAny<CancellationToken>()), Times.Exactly(1));
        Assert.AreEqual(udt.Name, vm.TypeNameToLoad);
        Assert.AreEqual(1, vm.TypeLayoutItems.Count);
        Assert.IsTrue(ReferenceEquals(tli, vm.TypeLayoutItems[0]));
    }

    [TestMethod]
    public async Task ExportToExcelWorks()
    {
        var tli = CreateSomeTypeLayoutsAndSetupMockAllTypeLayouts();

        var vm = CreateViewModelForTest();
        await vm.SetCurrentFragment("*");

        Assert.IsTrue(vm.ExportToExcelCommand.CanExecute());

        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                         It.IsAny<IList<string>>(),
                                                                                         It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()));

        vm.ExportToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                          It.IsAny<IList<string>>(),
                                                                                          It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()), Times.Exactly(1));
    }
}
