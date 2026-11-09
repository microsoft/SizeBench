using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public class AllCompilandsPageViewModelTests
{
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }
    [TestMethod]
    public async Task BinarySectionsInCompilandsDeDuplicates()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Compilands as IReadOnlyCollection<Compiland>));

        var viewmodel = new AllCompilandsPageViewModel(this.MockUITaskScheduler.Object,
                                                       generator.MockSession.Object,
                                                       new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        var sectionsInCompilands = viewmodel.BinarySectionsInCompilands().Select(s => s.Name).ToList();

        Assert.HasCount(2, sectionsInCompilands);
        Assert.Contains(".text", sectionsInCompilands);
        Assert.Contains(".data", sectionsInCompilands);
    }

    [TestMethod]
    public async Task COFFGroupsInCompilandsDeDuplicates()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Compilands as IReadOnlyCollection<Compiland>));

        var viewmodel = new AllCompilandsPageViewModel(this.MockUITaskScheduler.Object,
                                                       generator.MockSession.Object,
                                                       new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        var coffGroupsInCompilands = viewmodel.COFFGroupsInCompilands().Select(cg => cg.Name).ToList();

        Assert.HasCount(5, coffGroupsInCompilands);
        Assert.Contains(".text$mn", coffGroupsInCompilands);
        Assert.Contains(".text$zz", coffGroupsInCompilands);
        Assert.Contains(".data$xx", coffGroupsInCompilands);
        Assert.Contains(".data$zz", coffGroupsInCompilands);
        Assert.Contains(".bss", coffGroupsInCompilands);
    }

    [TestMethod]
    public async Task ExcelExportDataForSizeIsFormattedUsefully()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Compilands as IReadOnlyCollection<Compiland>));

        var viewmodel = new AllCompilandsPageViewModel(this.MockUITaskScheduler.Object,
                                                       generator.MockSession.Object,
                                                       new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ShouldDisplaySize);
        Assert.IsFalse(viewmodel.ShouldDisplayVirtualSize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);
        Assert.HasCount(11, columnHeadersList);

        var compilandNameIndex = columnHeadersList.IndexOf("Compiland Name");
        Assert.IsGreaterThanOrEqualTo(0, compilandNameIndex);
        var compilandShortNameIndex = columnHeadersList.IndexOf("Compiland Short Name");
        Assert.IsGreaterThanOrEqualTo(0, compilandShortNameIndex);
        var compilandTotalSizeIndex = columnHeadersList.IndexOf("Compiland Total Size on Disk");
        Assert.IsGreaterThanOrEqualTo(0, compilandTotalSizeIndex);

        Assert.Contains("Lib Name", columnHeadersList);
        Assert.Contains("Lib Short Name", columnHeadersList);
        Assert.Contains("Section: .text", columnHeadersList);
        Assert.Contains("COFF Group: .text$mn", columnHeadersList);
        Assert.Contains("COFF Group: .text$zz", columnHeadersList);
        Assert.Contains("Section: .data", columnHeadersList);
        Assert.Contains("COFF Group: .data$xx", columnHeadersList);
        Assert.Contains("COFF Group: .data$zz", columnHeadersList);

        // And now let's spot-check some of the data - not exhaustive because that's not really necessary
        Assert.HasCount(3, preformattedData);
        var a1Preformatted = preformattedData.Single(d => d["Compiland Short Name"].ToString() == "a1.obj");
        // Note that a2.obj does not show up here since it does not contribute any size, only VirtualSize
        var a2Preformatted = preformattedData.Single(d => d["Compiland Short Name"].ToString() == "a3.obj");
        var b1Preformatted = preformattedData.Single(d => d["Compiland Short Name"].ToString() == "b1.obj");

        Assert.AreEqual(5025u, Convert.ToUInt32(a1Preformatted["Compiland Total Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(5000u, Convert.ToUInt32(a1Preformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(3000u, Convert.ToUInt32(a1Preformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(2000u, Convert.ToUInt32(a1Preformatted["COFF Group: .text$zz"], CultureInfo.InvariantCulture));
        Assert.AreEqual(25u, Convert.ToUInt32(a1Preformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));

        Assert.AreEqual(900u, Convert.ToUInt32(a2Preformatted["Compiland Total Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(900u, Convert.ToUInt32(a2Preformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(500u, Convert.ToUInt32(a2Preformatted["COFF Group: .data$xx"], CultureInfo.InvariantCulture));
        Assert.AreEqual(400u, Convert.ToUInt32(a2Preformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));

        Assert.AreEqual(25u, Convert.ToUInt32(b1Preformatted["Compiland Total Size on Disk"], CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task ExcelExportDataForVirtualSizeIsFormattedUsefully()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Compilands as IReadOnlyCollection<Compiland>));

        var viewmodel = new AllCompilandsPageViewModel(this.MockUITaskScheduler.Object,
                                                       generator.MockSession.Object,
                                                       new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        viewmodel.SelectedDisplayModeIndex = 1;

        Assert.IsFalse(viewmodel.ShouldDisplaySize);
        Assert.IsTrue(viewmodel.ShouldDisplayVirtualSize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);
        Assert.HasCount(12, columnHeadersList);

        var compilandNameIndex = columnHeadersList.IndexOf("Compiland Name");
        Assert.IsGreaterThanOrEqualTo(0, compilandNameIndex);
        var compilandShortNameIndex = columnHeadersList.IndexOf("Compiland Short Name");
        Assert.IsGreaterThanOrEqualTo(0, compilandShortNameIndex);
        var compilandTotalSizeIndex = columnHeadersList.IndexOf("Compiland Total Size in Memory");
        Assert.IsGreaterThanOrEqualTo(0, compilandTotalSizeIndex);

        Assert.Contains("Lib Name", columnHeadersList);
        Assert.Contains("Lib Short Name", columnHeadersList);
        Assert.Contains("Section: .text", columnHeadersList);
        Assert.Contains("COFF Group: .text$mn", columnHeadersList);
        Assert.Contains("COFF Group: .text$zz", columnHeadersList);
        Assert.Contains("Section: .data", columnHeadersList);
        Assert.Contains("COFF Group: .data$xx", columnHeadersList);
        Assert.Contains("COFF Group: .data$zz", columnHeadersList);
        Assert.Contains("COFF Group: .bss", columnHeadersList);

        // And now let's spot-check some of the data - not exhaustive because that's not really necessary
        Assert.HasCount(4, preformattedData);
        var a1Preformatted = preformattedData.Single(d => d["Compiland Short Name"].ToString() == "a1.obj");
        var a2Preformatted = preformattedData.Single(d => d["Compiland Short Name"].ToString() == "a2.obj");
        var a3Preformatted = preformattedData.Single(d => d["Compiland Short Name"].ToString() == "a3.obj");

        Assert.AreEqual(5025u, Convert.ToUInt32(a1Preformatted["Compiland Total Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(5000u, Convert.ToUInt32(a1Preformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(3000u, Convert.ToUInt32(a1Preformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(2000u, Convert.ToUInt32(a1Preformatted["COFF Group: .text$zz"], CultureInfo.InvariantCulture));
        Assert.AreEqual(25u, Convert.ToUInt32(a1Preformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));

        Assert.AreEqual(400u, Convert.ToUInt32(a2Preformatted["Compiland Total Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(400u, Convert.ToUInt32(a2Preformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(400u, Convert.ToUInt32(a2Preformatted["COFF Group: .bss"], CultureInfo.InvariantCulture));

        Assert.AreEqual(950u, Convert.ToUInt32(a3Preformatted["Compiland Total Size in Memory"], CultureInfo.InvariantCulture));
    }
}
