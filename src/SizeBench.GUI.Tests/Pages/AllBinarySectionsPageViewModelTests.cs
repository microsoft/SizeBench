using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.PE;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public class AllBinarySectionsPageViewModelTests
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.MockExcelExporter = new Mock<IExcelExporter>();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();

        // Synchronously complete any task given to us
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [Timeout(30 * 1000)] // 30s
    [TestMethod]
    public void CanExportToExcel()
    {
        using var cache = new SessionDataCache();
        var firstSection = new BinarySection(cache, ".text", size: 0, virtualSize: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: DataSectionFlags.MemoryExecute);
        var secondSection = new BinarySection(cache, ".rdata", size: 0, virtualSize: 200, rva: 200, fileAlignment: 0, sectionAlignment: 0, characteristics: DataSectionFlags.MemoryRead);
        var sections = new List<BinarySection>() { firstSection, secondSection };

        this.MockSession.Setup(s => s.EnumerateBinarySectionsAndCOFFGroups(It.IsAny<CancellationToken>())).Returns(Task.FromResult(sections as IReadOnlyList<BinarySection>));
        this.MockUITaskScheduler.Setup(uits => uits.StartExcelExport(this.MockExcelExporter.Object, sections));

        var viewmodel = new AllBinarySectionsPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.MockSession.Object,
                                                           this.MockExcelExporter.Object);

        Assert.IsTrue(viewmodel.ExportToExcelCommand.CanExecute());

        viewmodel.ExportToExcelCommand.Execute();
        this.MockUITaskScheduler.Verify(uits => uits.StartExcelExport(this.MockExcelExporter.Object, sections), Times.Exactly(1));
    }
}
