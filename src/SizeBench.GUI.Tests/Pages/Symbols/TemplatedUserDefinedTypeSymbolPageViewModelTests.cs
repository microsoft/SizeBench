using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Pages.Symbols;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBenchV2.ViewModels.Tests;

[TestClass]
public sealed class TemplatedUserDefinedTypeSymbolPageViewModelTests : IDisposable
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private SessionDataCache DataCache = new SessionDataCache();
    public Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.TestDIAAdapter = new TestDIAAdapter();
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task WorksWhenTypeIsFoundByName()
    {
        uint nextSymIndexId = 0;
        // Two UDTs that are from the same template
        var typeOfIntUDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "ANamespace::Type<int>", instanceSize: 20, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var fn1OfInt = new SimpleFunctionCodeSymbol(this.DataCache, "Fn1", rva: 200, size: 50, symIndexId: nextSymIndexId++);
        this.MockSession.Setup(s => s.EnumerateFunctionsFromUserDefinedType(typeOfIntUDT, It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult<IReadOnlyList<IFunctionCodeSymbol>>(new List<IFunctionCodeSymbol>() { fn1OfInt }));

        var typeOfFloatUDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, name: "ANamespace::Type<float>", instanceSize: 20, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var fn1OfFloat = new SimpleFunctionCodeSymbol(this.DataCache, "Fn1", rva: 0, size: 0, symIndexId: nextSymIndexId++); // RVA == 0 because this folded with the one from the <int> version
        var fn2OfFloat = new SimpleFunctionCodeSymbol(this.DataCache, "Fn2", rva: 250, size: 10, symIndexId: nextSymIndexId++); // RVA == 0 because this folded with the one from the <int> version
        this.MockSession.Setup(s => s.EnumerateFunctionsFromUserDefinedType(typeOfFloatUDT, It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult<IReadOnlyList<IFunctionCodeSymbol>>(new List<IFunctionCodeSymbol>() { fn1OfFloat, fn2OfFloat }));

        var groupings = new List<UserDefinedTypeGrouping>()
            {
                new UserDefinedTypeGrouping(SymbolNameHelper.UserDefinedTypeToGenericTemplatedName(typeOfIntUDT), new List<UserDefinedTypeSymbol>() { typeOfIntUDT, typeOfFloatUDT }),
            };

        this.MockSession.Setup(s => s.EnumerateAllUserDefinedTypeGroupings(It.IsAny<CancellationToken>())).Returns(Task.FromResult(groupings as IReadOnlyList<UserDefinedTypeGrouping>));

        var viewmodel = new TemplatedUserDefinedTypeSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                                        this.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "TemplateName", groupings[0].TemplatedUserDefinedType!.TemplateName }
            });
        await viewmodel.InitializeAsync();

        var templatedUDTExpected = groupings[0].TemplatedUserDefinedType;

        Assert.AreEqual(templatedUDTExpected, viewmodel.TemplatedUDT);
        Assert.AreEqual(2, viewmodel.UDTs.Count);
        var typeOfIntViewModel = viewmodel.UDTs.Single(udtVM => udtVM.UDT == typeOfIntUDT);
        Assert.AreEqual<uint>(50, typeOfIntViewModel.TotalSizeOfFunctions);
        var typeOfFloatViewModel = viewmodel.UDTs.Single(udtVM => udtVM.UDT == typeOfFloatUDT);
        Assert.AreEqual<uint>(0 + 10, typeOfFloatViewModel.TotalSizeOfFunctions);

        StringAssert.Contains(viewmodel.PageTitle, templatedUDTExpected!.TemplateName, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task WhenTypeNameIsNotFoundItDoesItsBest()
    {
        var groupings = new List<UserDefinedTypeGrouping>();

        this.MockSession.Setup(s => s.EnumerateAllUserDefinedTypeGroupings(It.IsAny<CancellationToken>())).Returns(Task.FromResult(groupings as IReadOnlyList<UserDefinedTypeGrouping>));

        var viewmodel = new TemplatedUserDefinedTypeSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                                        this.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "TemplateName", "FunctionThatIsNotFound<T1>" }
            });
        await viewmodel.InitializeAsync();

        Assert.IsNull(viewmodel.TemplatedUDT);
        Assert.AreEqual(0, viewmodel.UDTs.Count);
        Assert.AreEqual("Templated User Defined Type", viewmodel.PageTitle);
    }

    public void Dispose() => this.DataCache.Dispose();
}
