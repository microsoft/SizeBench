using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Pages;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBenchV2.ViewModels.Tests;

[TestClass]
public sealed class TypeLayoutDiffPageViewModelTests : IDisposable
{
    private DiffTestDataGenerator _testDataGenerator = new DiffTestDataGenerator();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();
    private List<TypeLayoutItemDiff> AllTypeLayoutDiffs = new List<TypeLayoutItemDiff>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
        this.MockExcelExporter = new Mock<IExcelExporter>();
        this._testDataGenerator = new DiffTestDataGenerator();

        this.AllTypeLayoutDiffs = this._testDataGenerator.GenerateTypeLayoutItemDiffs(out _, out _);

        this._testDataGenerator.MockDiffSession.Setup(ds => ds.LoadAllTypeLayoutDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.AllTypeLayoutDiffs as IReadOnlyList<TypeLayoutItemDiff>));
    }

    private TypeLayoutDiffPageViewModel CreateViewModelForTest()
    {
        return new TypeLayoutDiffPageViewModel(this.MockUITaskScheduler.Object,
                                               this.MockExcelExporter.Object,
                                               this._testDataGenerator.MockDiffSession.Object);
    }

    [TestMethod]
    public void InitialConstructionIsValid()
    {
        var vm = CreateViewModelForTest();

        Assert.IsNotNull(vm.LoadDiffTypeCommand);
        Assert.IsNotNull(vm.ViewLayoutsOfSpecificTypesCommand);
        Assert.IsNull(vm.TypeNameToLoad);
        Assert.IsNull(vm.TypeLayoutItemDiffs);
        Assert.AreEqual("Type Layout Diff", vm.PageTitle);
        Assert.IsFalse(vm.ExportToExcelCommand.CanExecute());
    }

    [TestMethod]
    public async Task WildcardFragmentLoadsAllLayouts()
    {
        var vm = CreateViewModelForTest();
        await vm.SetCurrentFragment("*");
        Assert.AreEqual("Type Layout Diff: *", vm.PageTitle);
        this._testDataGenerator.MockDiffSession.Verify(s => s.LoadAllTypeLayoutDiffs(It.IsAny<CancellationToken>()), Times.Once());
        CollectionAssert.AreEquivalent(this.AllTypeLayoutDiffs, vm.TypeLayoutItemDiffs!.Cast<TypeLayoutItemDiff>().ToList());
    }

    [TestMethod]
    public async Task TypeNameInFragmentIsLoaded()
    {
        this._testDataGenerator.MockDiffSession.Setup(s => s.LoadTypeLayoutDiffsByName("TypeNameToLoad", It.IsAny<CancellationToken>())).Returns(Task.FromResult(this.AllTypeLayoutDiffs as IReadOnlyList<TypeLayoutItemDiff>));
        var vm = CreateViewModelForTest();
        await vm.SetCurrentFragment("TypeNameToLoad");
        Assert.AreEqual("Type Layout Diff: TypeNameToLoad", vm.PageTitle);
        this._testDataGenerator.MockDiffSession.Verify(s => s.LoadTypeLayoutDiffsByName("TypeNameToLoad", It.IsAny<CancellationToken>()), Times.Once());
        CollectionAssert.AreEquivalent(this.AllTypeLayoutDiffs, vm.TypeLayoutItemDiffs!.Cast<TypeLayoutItemDiff>().ToList());
    }

    [TestMethod]
    public void UDTCanLoadFromDiff()
    {
        var vm = CreateViewModelForTest();

        var tlidResult = this.AllTypeLayoutDiffs.First(tlid => tlid.BeforeTypeLayout != null && tlid.AfterTypeLayout != null);

        var udtTypeDiff = tlidResult.UserDefinedTypeDiff;

        this._testDataGenerator.MockDiffSession.Setup(s => s.LoadTypeLayoutDiff(udtTypeDiff, It.IsAny<CancellationToken>())).Returns(Task.FromResult(tlidResult));
        vm.LoadDiffTypeCommand.Execute(udtTypeDiff);

        Assert.AreEqual(tlidResult.UserDefinedType.Name, vm.TypeNameToLoad);
        Assert.AreEqual($"Type Layout Diff: {tlidResult.UserDefinedType.Name}", vm.PageTitle);
        this._testDataGenerator.MockDiffSession.Verify(s => s.LoadTypeLayoutDiff(udtTypeDiff, It.IsAny<CancellationToken>()), Times.Exactly(1));
        Assert.AreEqual(1, vm.TypeLayoutItemDiffs!.Cast<TypeLayoutItemDiff>().Count());
        Assert.IsTrue(ReferenceEquals(tlidResult, vm.TypeLayoutItemDiffs!.Cast<TypeLayoutItemDiff>().ToList()[0]));
    }

    [TestMethod]
    public async Task ExportToExcelWorks()
    {
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

    public void Dispose() => this._testDataGenerator.Dispose();
}
