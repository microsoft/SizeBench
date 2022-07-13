using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class AllUserDefinedTypesPageViewModelTests : IDisposable
{
    private SessionDataCache DataCache = new SessionDataCache();
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    private UserDefinedTypeSymbol? someTypeUDT;
    private UserDefinedTypeSymbol? typeOfIntUDT;
    private UserDefinedTypeSymbol? typeOfFloatUDT;
    private TemplatedUserDefinedTypeSymbol? typeOfT1TemplatedUDT;

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

        uint nextSymIndexId = 1;

        // A non-templated UDT
        this.someTypeUDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "SomeType", instanceSize: 100, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var UDT1Function1 = new SimpleFunctionCodeSymbol(this.DataCache, "SuperImporantFunction1", rva: 100, size: 50, symIndexId: nextSymIndexId++);
        var UDT1Function2 = new SimpleFunctionCodeSymbol(this.DataCache, "SuperImportantFunction2", rva: 150, size: 100, symIndexId: nextSymIndexId++);
        this.MockSession.Setup(s => s.EnumerateFunctionsFromUserDefinedType(this.someTypeUDT, It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult<IReadOnlyList<IFunctionCodeSymbol>>(new List<IFunctionCodeSymbol>() { UDT1Function1, UDT1Function2 }));

        // Two UDTs that are from the same template
        this.typeOfIntUDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "ANamespace::Type<int>", instanceSize: 20, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var UDT2Function1 = new SimpleFunctionCodeSymbol(this.DataCache, "Fn1", rva: 200, size: 50, symIndexId: nextSymIndexId++);
        this.MockSession.Setup(s => s.EnumerateFunctionsFromUserDefinedType(this.typeOfIntUDT, It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult<IReadOnlyList<IFunctionCodeSymbol>>(new List<IFunctionCodeSymbol>() { UDT2Function1 }));

        this.typeOfFloatUDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "ANamespace::Type<float>", instanceSize: 20, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var UDT3Function1 = new SimpleFunctionCodeSymbol(this.DataCache, "Fn1", rva: 0, size: 0, symIndexId: nextSymIndexId++); // RVA == 0 because this folded with the one from the <int> version
        var UDT3Function2 = new SimpleFunctionCodeSymbol(this.DataCache, "Fn2", rva: 250, size: 10, symIndexId: nextSymIndexId++); // RVA == 0 because this folded with the one from the <int> version
        this.MockSession.Setup(s => s.EnumerateFunctionsFromUserDefinedType(this.typeOfFloatUDT, It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult<IReadOnlyList<IFunctionCodeSymbol>>(new List<IFunctionCodeSymbol>() { UDT3Function1, UDT3Function2 }));

        var allGroupings = new List<UserDefinedTypeGrouping>()
            {
                new UserDefinedTypeGrouping(this.someTypeUDT.Name, new List<UserDefinedTypeSymbol>() { this.someTypeUDT }),
                new UserDefinedTypeGrouping(SymbolNameHelper.UserDefinedTypeToGenericTemplatedName(this.typeOfIntUDT), new List<UserDefinedTypeSymbol>() { this.typeOfIntUDT, this.typeOfFloatUDT }),
            };

        this.typeOfT1TemplatedUDT = allGroupings[1].TemplatedUserDefinedType!;

        this.MockSession.Setup(s => s.EnumerateAllUserDefinedTypeGroupings(It.IsAny<CancellationToken>())).Returns(Task.FromResult(allGroupings as IReadOnlyList<UserDefinedTypeGrouping>));
    }

    private void AssertStateWhenShowEachTemplateExpansionSeparatelyIsTrue(AllUserDefinedTypesPageViewModel viewmodel)
    {
        Assert.AreEqual(3, viewmodel.UDTGroupings.Count);

        var someType = viewmodel.UDTGroupings.Single(grouping => grouping.Name == "SomeType");
        Assert.AreEqual<uint>(50 + 100, someType.TotalSizeOfFunctions); // SuperImportantFunction1 and 2
        Assert.AreEqual(1, someType.CountOfTypes);
        Assert.AreEqual(this.someTypeUDT, someType.LinkTarget);

        var typeOfInt = viewmodel.UDTGroupings.Single(grouping => grouping.Name == "ANamespace::Type<int>");
        Assert.AreEqual<uint>(50, typeOfInt.TotalSizeOfFunctions); // Fn1
        Assert.AreEqual(1, typeOfInt.CountOfTypes);
        Assert.AreEqual(this.typeOfIntUDT, typeOfInt.LinkTarget);

        var typeOfFloat = viewmodel.UDTGroupings.Single(grouping => grouping.Name == "ANamespace::Type<float>");
        Assert.AreEqual<uint>(0 + 10, typeOfFloat.TotalSizeOfFunctions); // Fn1 (folded) + Fn2
        Assert.AreEqual(1, typeOfFloat.CountOfTypes);
        Assert.AreEqual(this.typeOfFloatUDT, typeOfFloat.LinkTarget);
    }

    private void AssertStateWhenShowEachTemplateExpansionSeparatelyIsFalse(AllUserDefinedTypesPageViewModel viewmodel)
    {
        Assert.AreEqual(2, viewmodel.UDTGroupings.Count);

        var someType = viewmodel.UDTGroupings.Single(grouping => grouping.Name == "SomeType");
        Assert.AreEqual<uint>(50 + 100, someType.TotalSizeOfFunctions); // SuperImportantFunction1 and 2
        Assert.AreEqual(1, someType.CountOfTypes);
        Assert.AreEqual(this.someTypeUDT, someType.LinkTarget);

        var typeOfT1 = viewmodel.UDTGroupings.Single(grouping => grouping.Name == "ANamespace::Type<T1>");
        Assert.AreEqual<uint>(50 + 0 + 10, typeOfT1.TotalSizeOfFunctions); // Fn1 + Fn1 (folded) + Fn2
        Assert.AreEqual(2, typeOfT1.CountOfTypes);
        Assert.AreEqual(this.typeOfT1TemplatedUDT, typeOfT1.LinkTarget);
    }

    [TestMethod]
    public async Task ExcelExportIsFormattedUsefully()
    {
        var viewmodel = new AllUserDefinedTypesPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.MockSession.Object,
                                                             this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        // Check formatting is good when we're grouping templaes together
        Assert.IsFalse(viewmodel.ShowEachTemplateExpansionSeparately);
        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        Assert.AreEqual(3, columnHeaders.Length);
        Assert.AreEqual("Type Name", columnHeaders[0]);
        Assert.AreEqual("# Types", columnHeaders[1]);
        Assert.AreEqual("Total Size of Member Functions", columnHeaders[2]);

        Assert.AreEqual("SomeType", preformattedData[0]["Type Name"]);
        Assert.AreEqual(1, preformattedData[0]["# Types"]);
        Assert.AreEqual<uint>(100 + 50, (uint)preformattedData[0]["Total Size of Member Functions"]);

        Assert.AreEqual("ANamespace::Type<T1>", preformattedData[1]["Type Name"]);
        Assert.AreEqual(2, preformattedData[1]["# Types"]);
        Assert.AreEqual<uint>(50 + 0 + 10, (uint)preformattedData[1]["Total Size of Member Functions"]);

        Assert.IsTrue(viewmodel.ExportToExcelCommand.CanExecute());
        viewmodel.ExportToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                          It.IsAny<IList<string>>(),
                                                                                          It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()),
                                        Times.Exactly(1));

        // Now check formatting is good when each template expansion is shown separately
        viewmodel.ShowEachTemplateExpansionSeparately = true;
        viewmodel.GenerateFormattedDataForExcelExport(out columnHeaders, out preformattedData);

        Assert.AreEqual(2, columnHeaders.Length);
        Assert.AreEqual("Type Name", columnHeaders[0]);
        Assert.AreEqual("Total Size of Member Functions", columnHeaders[1]);

        Assert.AreEqual("SomeType", preformattedData[0]["Type Name"]);
        Assert.AreEqual<uint>(100 + 50, (uint)preformattedData[0]["Total Size of Member Functions"]);

        Assert.AreEqual("ANamespace::Type<int>", preformattedData[1]["Type Name"]);
        Assert.AreEqual<uint>(50, (uint)preformattedData[1]["Total Size of Member Functions"]);

        Assert.AreEqual("ANamespace::Type<float>", preformattedData[2]["Type Name"]);
        Assert.AreEqual<uint>(0 + 10, (uint)preformattedData[2]["Total Size of Member Functions"]);

        Assert.IsTrue(viewmodel.ExportToExcelCommand.CanExecute());
        viewmodel.ExportToExcelCommand.Execute();

        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExportWithPreformattedData(this.MockExcelExporter.Object,
                                                                                          It.IsAny<IList<string>>(),
                                                                                          It.IsAny<IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>>>()),
                                        Times.Exactly(2));
    }

    [TestMethod]
    public async Task DefaultIsToShowTemplatesRolledUp()
    {
        var viewmodel = new AllUserDefinedTypesPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.MockSession.Object,
                                                             this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        Assert.IsFalse(viewmodel.ShowEachTemplateExpansionSeparately);
        AssertStateWhenShowEachTemplateExpansionSeparatelyIsFalse(viewmodel);
    }

    [TestMethod]
    public async Task TogglingShowEachTemplateExpansionSeparatelyRefreshesView()
    {
        var viewmodel = new AllUserDefinedTypesPageViewModel(this.MockUITaskScheduler.Object,
                                                             this.MockSession.Object,
                                                             this.MockExcelExporter.Object);
        await viewmodel.SetCurrentFragment("ShowEachTemplateExpansionSeparately");
        await viewmodel.InitializeAsync();

        var vmPropertyChangesSeen = new List<string>();
        viewmodel.PropertyChanged += (s, e) => vmPropertyChangesSeen.Add(e.PropertyName!);

        Assert.IsTrue(viewmodel.ShowEachTemplateExpansionSeparately);
        AssertStateWhenShowEachTemplateExpansionSeparatelyIsTrue(viewmodel);

        viewmodel.ShowEachTemplateExpansionSeparately = false;

        Assert.AreEqual(1, vmPropertyChangesSeen.Count(prop => prop == nameof(AllUserDefinedTypesPageViewModel.ShowEachTemplateExpansionSeparately)));
        AssertStateWhenShowEachTemplateExpansionSeparatelyIsFalse(viewmodel);

        // Toggling back should restore everything to the way it was
        viewmodel.ShowEachTemplateExpansionSeparately = true;
        Assert.AreEqual(2, vmPropertyChangesSeen.Count(prop => prop == nameof(AllUserDefinedTypesPageViewModel.ShowEachTemplateExpansionSeparately)));
        AssertStateWhenShowEachTemplateExpansionSeparatelyIsTrue(viewmodel);
    }

    public void Dispose() => this.DataCache.Dispose();
}
