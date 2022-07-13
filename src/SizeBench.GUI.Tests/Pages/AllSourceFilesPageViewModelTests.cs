using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public class AllSourceFilesPageViewModelTests
{
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }
    [TestMethod]
    public async Task BinarySectionsInSourceFilesDeDuplicates()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateSourceFiles(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.SourceFiles as IReadOnlyList<SourceFile>));

        var viewmodel = new AllSourceFilesPageViewModel(this.MockUITaskScheduler.Object,
                                                        generator.MockSession.Object,
                                                        new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        var sectionsInSourceFiles = viewmodel.BinarySectionsInSourceFiles().Select(s => s.Name).ToList();

        Assert.AreEqual(2, sectionsInSourceFiles.Count);
        Assert.IsTrue(sectionsInSourceFiles.Contains(".text"));
        Assert.IsTrue(sectionsInSourceFiles.Contains(".data"));
    }

    [TestMethod]
    public async Task COFFGroupsInSourceFilesDeDuplicates()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateSourceFiles(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.SourceFiles as IReadOnlyList<SourceFile>));

        var viewmodel = new AllSourceFilesPageViewModel(this.MockUITaskScheduler.Object,
                                                        generator.MockSession.Object,
                                                        new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        var coffGroupsInSourceFiles = viewmodel.COFFGroupsInSourceFiles().Select(cg => cg.Name).ToList();

        Assert.AreEqual(4, coffGroupsInSourceFiles.Count);
        Assert.IsTrue(coffGroupsInSourceFiles.Contains(".text$mn"));
        Assert.IsTrue(coffGroupsInSourceFiles.Contains(".text$zz"));
        Assert.IsTrue(coffGroupsInSourceFiles.Contains(".data$xx"));
        Assert.IsTrue(coffGroupsInSourceFiles.Contains(".data$zz"));
    }

    [TestMethod]
    public async Task ExcelExportDataForSizeIsFormattedUsefully()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.EnumerateSourceFiles(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.SourceFiles as IReadOnlyList<SourceFile>));

        var viewmodel = new AllSourceFilesPageViewModel(this.MockUITaskScheduler.Object,
                                                        generator.MockSession.Object,
                                                        new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ShouldDisplaySize);
        Assert.IsFalse(viewmodel.ShouldDisplayVirtualSize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);
        Assert.AreEqual(9, columnHeadersList.Count);

        var sourceFileNameIndex = columnHeadersList.IndexOf("Source File Name");
        Assert.IsTrue(sourceFileNameIndex >= 0);
        var sourceFileShortNameIndex = columnHeadersList.IndexOf("Source File Short Name");
        Assert.IsTrue(sourceFileShortNameIndex >= 0);
        var sourceFileTotalSizeIndex = columnHeadersList.IndexOf("Source File Total Size on Disk");
        Assert.IsTrue(sourceFileTotalSizeIndex >= 0);

        Assert.IsTrue(columnHeadersList.Contains("Section: .text"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .text$mn"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .text$zz"));
        Assert.IsTrue(columnHeadersList.Contains("Section: .data"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .data$xx"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .data$zz"));

        Assert.AreEqual(2, preformattedData.Count);
        var a1Preformatted = preformattedData.Single(d => d["Source File Short Name"].ToString() == "a1.cpp");
        var xHeaderPreformatted = preformattedData.Single(d => d["Source File Short Name"].ToString() == "x.h");

        Assert.AreEqual(500u, Convert.ToUInt32(a1Preformatted["Source File Total Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(500u, Convert.ToUInt32(a1Preformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(300u, Convert.ToUInt32(a1Preformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200u, Convert.ToUInt32(a1Preformatted["COFF Group: .text$zz"], CultureInfo.InvariantCulture));

        Assert.AreEqual(825u, Convert.ToUInt32(xHeaderPreformatted["Source File Total Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(500u, Convert.ToUInt32(xHeaderPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(500u, Convert.ToUInt32(xHeaderPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(325u, Convert.ToUInt32(xHeaderPreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(300u, Convert.ToUInt32(xHeaderPreformatted["COFF Group: .data$xx"], CultureInfo.InvariantCulture));
        Assert.AreEqual(25u, Convert.ToUInt32(xHeaderPreformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task ExcelExportDataForVirtualSizeIsFormattedUsefully()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.EnumerateSourceFiles(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.SourceFiles as IReadOnlyList<SourceFile>));

        var viewmodel = new AllSourceFilesPageViewModel(this.MockUITaskScheduler.Object,
                                                       generator.MockSession.Object,
                                                       new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        viewmodel.SelectedDisplayModeIndex = 1;

        Assert.IsFalse(viewmodel.ShouldDisplaySize);
        Assert.IsTrue(viewmodel.ShouldDisplayVirtualSize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);
        Assert.AreEqual(9, columnHeadersList.Count);

        var sourceFileNameIndex = columnHeadersList.IndexOf("Source File Name");
        Assert.IsTrue(sourceFileNameIndex >= 0);
        var sourceFileShortNameIndex = columnHeadersList.IndexOf("Source File Short Name");
        Assert.IsTrue(sourceFileShortNameIndex >= 0);
        var sourceFileTotalSizeIndex = columnHeadersList.IndexOf("Source File Total Size in Memory");
        Assert.IsTrue(sourceFileTotalSizeIndex >= 0);

        Assert.IsTrue(columnHeadersList.Contains("Section: .text"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .text$mn"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .text$zz"));
        Assert.IsTrue(columnHeadersList.Contains("Section: .data"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .data$xx"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .data$zz"));

        Assert.AreEqual(2, preformattedData.Count);
        var a1Preformatted = preformattedData.Single(d => d["Source File Short Name"].ToString() == "a1.cpp");
        var xHeaderPreformatted = preformattedData.Single(d => d["Source File Short Name"].ToString() == "x.h");

        Assert.AreEqual(500u, Convert.ToUInt32(a1Preformatted["Source File Total Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(500u, Convert.ToUInt32(a1Preformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(300u, Convert.ToUInt32(a1Preformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200u, Convert.ToUInt32(a1Preformatted["COFF Group: .text$zz"], CultureInfo.InvariantCulture));

        Assert.AreEqual(825u, Convert.ToUInt32(xHeaderPreformatted["Source File Total Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(500u, Convert.ToUInt32(xHeaderPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(500u, Convert.ToUInt32(xHeaderPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(325u, Convert.ToUInt32(xHeaderPreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(300u, Convert.ToUInt32(xHeaderPreformatted["COFF Group: .data$xx"], CultureInfo.InvariantCulture));
        Assert.AreEqual(25u, Convert.ToUInt32(xHeaderPreformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));
    }
}
