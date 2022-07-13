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

        generator.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Compilands as IReadOnlyList<Compiland>));

        var viewmodel = new AllCompilandsPageViewModel(this.MockUITaskScheduler.Object,
                                                       generator.MockSession.Object,
                                                       new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        var sectionsInCompilands = viewmodel.BinarySectionsInCompilands().Select(s => s.Name).ToList();

        Assert.AreEqual(2, sectionsInCompilands.Count);
        Assert.IsTrue(sectionsInCompilands.Contains(".text"));
        Assert.IsTrue(sectionsInCompilands.Contains(".data"));
    }

    [TestMethod]
    public async Task COFFGroupsInCompilandsDeDuplicates()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Compilands as IReadOnlyList<Compiland>));

        var viewmodel = new AllCompilandsPageViewModel(this.MockUITaskScheduler.Object,
                                                       generator.MockSession.Object,
                                                       new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        var coffGroupsInCompilands = viewmodel.COFFGroupsInCompilands().Select(cg => cg.Name).ToList();

        Assert.AreEqual(5, coffGroupsInCompilands.Count);
        Assert.IsTrue(coffGroupsInCompilands.Contains(".text$mn"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".text$zz"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".data$xx"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".data$zz"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".bss"));
    }

    [TestMethod]
    public async Task ExcelExportDataForSizeIsFormattedUsefully()
    {
        using var generator = new SingleBinaryDataGenerator();
        generator.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Compilands as IReadOnlyList<Compiland>));

        var viewmodel = new AllCompilandsPageViewModel(this.MockUITaskScheduler.Object,
                                                       generator.MockSession.Object,
                                                       new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ShouldDisplaySize);
        Assert.IsFalse(viewmodel.ShouldDisplayVirtualSize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);
        Assert.AreEqual(11, columnHeadersList.Count);

        var compilandNameIndex = columnHeadersList.IndexOf("Compiland Name");
        Assert.IsTrue(compilandNameIndex >= 0);
        var compilandShortNameIndex = columnHeadersList.IndexOf("Compiland Short Name");
        Assert.IsTrue(compilandShortNameIndex >= 0);
        var compilandTotalSizeIndex = columnHeadersList.IndexOf("Compiland Total Size on Disk");
        Assert.IsTrue(compilandTotalSizeIndex >= 0);

        Assert.IsTrue(columnHeadersList.Contains("Lib Name"));
        Assert.IsTrue(columnHeadersList.Contains("Lib Short Name"));
        Assert.IsTrue(columnHeadersList.Contains("Section: .text"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .text$mn"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .text$zz"));
        Assert.IsTrue(columnHeadersList.Contains("Section: .data"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .data$xx"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .data$zz"));

        // And now let's spot-check some of the data - not exhaustive because that's not really necessary
        Assert.AreEqual(3, preformattedData.Count);
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
        generator.MockSession.Setup(s => s.EnumerateCompilands(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Compilands as IReadOnlyList<Compiland>));

        var viewmodel = new AllCompilandsPageViewModel(this.MockUITaskScheduler.Object,
                                                       generator.MockSession.Object,
                                                       new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        viewmodel.SelectedDisplayModeIndex = 1;

        Assert.IsFalse(viewmodel.ShouldDisplaySize);
        Assert.IsTrue(viewmodel.ShouldDisplayVirtualSize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);
        Assert.AreEqual(12, columnHeadersList.Count);

        var compilandNameIndex = columnHeadersList.IndexOf("Compiland Name");
        Assert.IsTrue(compilandNameIndex >= 0);
        var compilandShortNameIndex = columnHeadersList.IndexOf("Compiland Short Name");
        Assert.IsTrue(compilandShortNameIndex >= 0);
        var compilandTotalSizeIndex = columnHeadersList.IndexOf("Compiland Total Size in Memory");
        Assert.IsTrue(compilandTotalSizeIndex >= 0);

        Assert.IsTrue(columnHeadersList.Contains("Lib Name"));
        Assert.IsTrue(columnHeadersList.Contains("Lib Short Name"));
        Assert.IsTrue(columnHeadersList.Contains("Section: .text"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .text$mn"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .text$zz"));
        Assert.IsTrue(columnHeadersList.Contains("Section: .data"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .data$xx"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .data$zz"));
        Assert.IsTrue(columnHeadersList.Contains("COFF Group: .bss"));

        // And now let's spot-check some of the data - not exhaustive because that's not really necessary
        Assert.AreEqual(4, preformattedData.Count);
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
