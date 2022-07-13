using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class ContributionDiffPageViewModelTests : IDisposable
{
    private DiffTestDataGenerator TestDataGenerator = new DiffTestDataGenerator();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestDataGenerator = new DiffTestDataGenerator();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();

        this.MockExcelExporter = new Mock<IExcelExporter>();
    }

    [TestMethod]
    public async Task CanLoadFromCOFFGroupAndLibNames()
    {
        var viewmodel = new ContributionDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                          this.MockExcelExporter.Object,
                                                          this.TestDataGenerator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "COFFGroup", ".text$mn" },
                { "Lib", this.TestDataGenerator.BLibDiff.Name }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(this.TestDataGenerator.BLibDiff.COFFGroupContributionDiffsByName[".text$mn"], viewmodel.ContributionDiff));
    }

    [TestMethod]
    public async Task CanLoadFromSectionAndLibNames()
    {
        var viewmodel = new ContributionDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                          this.MockExcelExporter.Object,
                                                          this.TestDataGenerator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection", ".data" },
                { "Lib", this.TestDataGenerator.ALibDiff.Name }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(this.TestDataGenerator.ALibDiff.SectionContributionDiffsByName[".data"], viewmodel.ContributionDiff));
    }

    [TestMethod]
    public async Task CanLoadFromCOFFGroupAndCompilandNames()
    {
        var viewmodel = new ContributionDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                          this.MockExcelExporter.Object,
                                                          this.TestDataGenerator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "COFFGroup", ".text$mn" },
                { "Compiland", this.TestDataGenerator.A2CompilandDiff.Name },
                { "Lib", this.TestDataGenerator.ALibDiff.Name }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(this.TestDataGenerator.A2CompilandDiff.COFFGroupContributionDiffsByName[".text$mn"], viewmodel.ContributionDiff));
    }

    [TestMethod]
    public async Task CanLoadFromSectionAndCompilandNames()
    {
        var viewmodel = new ContributionDiffPageViewModel(this.MockUITaskScheduler.Object,
                                                          this.MockExcelExporter.Object,
                                                          this.TestDataGenerator.MockDiffSession.Object);

        viewmodel.SetQueryString(new Dictionary<string, string>()
            {
                { "BinarySection", ".data" },
                { "Compiland", this.TestDataGenerator.B1CompilandDiff.Name },
                { "Lib", this.TestDataGenerator.BLibDiff.Name }
            });
        await viewmodel.InitializeAsync();

        Assert.IsTrue(ReferenceEquals(this.TestDataGenerator.B1CompilandDiff.SectionContributionDiffsByName[".data"], viewmodel.ContributionDiff));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
