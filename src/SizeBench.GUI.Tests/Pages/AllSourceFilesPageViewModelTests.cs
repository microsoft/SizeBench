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

        Assert.HasCount(2, sectionsInSourceFiles);
        Assert.Contains(".text", sectionsInSourceFiles);
        Assert.Contains(".data", sectionsInSourceFiles);
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

        Assert.HasCount(4, coffGroupsInSourceFiles);
        Assert.Contains(".text$mn", coffGroupsInSourceFiles);
        Assert.Contains(".text$zz", coffGroupsInSourceFiles);
        Assert.Contains(".data$xx", coffGroupsInSourceFiles);
        Assert.Contains(".data$zz", coffGroupsInSourceFiles);
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
        Assert.HasCount(9, columnHeadersList);

        var sourceFileNameIndex = columnHeadersList.IndexOf("Source File Name");
        Assert.IsGreaterThanOrEqualTo(0, sourceFileNameIndex);
        var sourceFileShortNameIndex = columnHeadersList.IndexOf("Source File Short Name");
        Assert.IsGreaterThanOrEqualTo(0, sourceFileShortNameIndex);
        var sourceFileTotalSizeIndex = columnHeadersList.IndexOf("Source File Total Size on Disk");
        Assert.IsGreaterThanOrEqualTo(0, sourceFileTotalSizeIndex);

        Assert.Contains("Section: .text", columnHeadersList);
        Assert.Contains("COFF Group: .text$mn", columnHeadersList);
        Assert.Contains("COFF Group: .text$zz", columnHeadersList);
        Assert.Contains("Section: .data", columnHeadersList);
        Assert.Contains("COFF Group: .data$xx", columnHeadersList);
        Assert.Contains("COFF Group: .data$zz", columnHeadersList);

        Assert.HasCount(2, preformattedData);
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
        Assert.HasCount(9, columnHeadersList);

        var sourceFileNameIndex = columnHeadersList.IndexOf("Source File Name");
        Assert.IsGreaterThanOrEqualTo(0, sourceFileNameIndex);
        var sourceFileShortNameIndex = columnHeadersList.IndexOf("Source File Short Name");
        Assert.IsGreaterThanOrEqualTo(0, sourceFileShortNameIndex);
        var sourceFileTotalSizeIndex = columnHeadersList.IndexOf("Source File Total Size in Memory");
        Assert.IsGreaterThanOrEqualTo(0, sourceFileTotalSizeIndex);

        Assert.Contains("Section: .text", columnHeadersList);
        Assert.Contains("COFF Group: .text$mn", columnHeadersList);
        Assert.Contains("COFF Group: .text$zz", columnHeadersList);
        Assert.Contains("Section: .data", columnHeadersList);
        Assert.Contains("COFF Group: .data$xx", columnHeadersList);
        Assert.Contains("COFF Group: .data$zz", columnHeadersList);

        Assert.HasCount(2, preformattedData);
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
