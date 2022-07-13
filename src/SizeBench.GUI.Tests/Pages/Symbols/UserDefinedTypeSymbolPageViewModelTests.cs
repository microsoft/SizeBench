using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.GUI.Pages.Symbols;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBenchV2.ViewModels.Tests;

[TestClass]
public sealed class UserDefinedTypeSymbolPageViewModelTests : IDisposable
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
        var aComplexTypeOfSomeUDT = new UserDefinedTypeSymbol(this.DataCache, this.TestDIAAdapter, this.MockSession.Object, "AComplex::Type<SomeUDT>", instanceSize: 10, symIndexId: nextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass, baseTypeIDs: null);
        var superImportantFunction1 = new SimpleFunctionCodeSymbol(this.DataCache, "SuperImporantFunction1", rva: 100, size: 50, symIndexId: nextSymIndexId++);
        var superImportantFunction2 = new SimpleFunctionCodeSymbol(this.DataCache, "SuperImportantFunction2", rva: 150, size: 100, symIndexId: nextSymIndexId++);
        var functionRemovedInFinalBinary = new SimpleFunctionCodeSymbol(this.DataCache, "FunctionRemovedInFinalBinary", rva: 0, size: 0, symIndexId: nextSymIndexId++);

        this.MockSession.Setup(s => s.EnumerateFunctionsFromUserDefinedType(aComplexTypeOfSomeUDT, It.IsAny<CancellationToken>()))
                        .Returns(Task.FromResult<IReadOnlyList<IFunctionCodeSymbol>>(new List<IFunctionCodeSymbol>() { superImportantFunction1, superImportantFunction2, functionRemovedInFinalBinary }));

        var typeLayouts = new List<TypeLayoutItem>()
            {
                new TypeLayoutItem(aComplexTypeOfSomeUDT, 0.0M, 0, null, null),
            };

        this.MockSession.Setup(s => s.LoadTypeLayoutsByName(aComplexTypeOfSomeUDT.Name, It.IsAny<CancellationToken>())).Returns(Task.FromResult(typeLayouts as IReadOnlyList<TypeLayoutItem>));

        var viewmodel = new UserDefinedTypeSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                               this.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "Name", aComplexTypeOfSomeUDT.Name }
            });
        await viewmodel.InitializeAsync();

        Assert.AreEqual(aComplexTypeOfSomeUDT, viewmodel.UDT);
        Assert.AreEqual(typeLayouts, viewmodel.TypeLayoutItems);
        Assert.AreEqual(3, viewmodel.Functions.Count);
        var fn1ViewModel = viewmodel.Functions.Single(fnVM => fnVM.FunctionCodeSymbol == superImportantFunction1);
        Assert.IsTrue(fn1ViewModel.IsInFinalBinary);
        var fn2ViewModel = viewmodel.Functions.Single(fnVM => fnVM.FunctionCodeSymbol == superImportantFunction2);
        Assert.IsTrue(fn2ViewModel.IsInFinalBinary);
        var fnRemovedViewModel = viewmodel.Functions.Single(fnVM => fnVM.FunctionCodeSymbol == functionRemovedInFinalBinary);
        Assert.IsFalse(fnRemovedViewModel.IsInFinalBinary);
        Assert.AreEqual<uint>(50 + 100, viewmodel.TotalSizeOfAllFunctions);
        StringAssert.Contains(viewmodel.PageTitle, aComplexTypeOfSomeUDT.Name, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task WhenTypeNameIsNotFoundItDoesItsBest()
    {
        var typeLayouts = new List<TypeLayoutItem>();

        this.MockSession.Setup(s => s.LoadTypeLayoutsByName(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(typeLayouts as IReadOnlyList<TypeLayoutItem>));

        var viewmodel = new UserDefinedTypeSymbolPageViewModel(this.MockUITaskScheduler.Object,
                                                               this.MockSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "Name", "FunctionThatIsNotFound" }
            });
        await viewmodel.InitializeAsync();

        Assert.IsNull(viewmodel.UDT);
        Assert.AreEqual(0, viewmodel.TypeLayoutItems.Count);
        Assert.AreEqual(0, viewmodel.Functions.Count);
        Assert.AreEqual<uint>(0, viewmodel.TotalSizeOfAllFunctions);
        Assert.AreEqual("User Defined Type", viewmodel.PageTitle);
    }

    public void Dispose() => this.DataCache.Dispose();
}
