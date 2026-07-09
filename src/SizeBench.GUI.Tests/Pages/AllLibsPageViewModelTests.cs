using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public class AllLibsPageViewModelTests
{
    public Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task BinarySectionsInLibsTest()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Libs as IReadOnlyCollection<Library>));

        var viewmodel = new AllLibsPageViewModel(this.MockUITaskScheduler.Object,
                                                 generator.MockSession.Object,
                                                 new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        var sectionsFound = viewmodel.BinarySectionsInLibs();
        Assert.HasCount(2, sectionsFound);
        Assert.Contains(s => s.Name == ".text", sectionsFound);
        Assert.Contains(s => s.Name == ".data", sectionsFound);
    }

    [TestMethod]
    public async Task COFFGroupsInLibsTest()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Libs as IReadOnlyCollection<Library>));

        var viewmodel = new AllLibsPageViewModel(this.MockUITaskScheduler.Object,
                                                 generator.MockSession.Object,
                                                 new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        var coffGroupsFound = viewmodel.COFFGroupsInLibs();
        Assert.HasCount(5, coffGroupsFound);
        Assert.Contains(cg => cg.Name == ".text$mn", coffGroupsFound);
        Assert.Contains(cg => cg.Name == ".text$zz", coffGroupsFound);
        Assert.Contains(cg => cg.Name == ".data$xx", coffGroupsFound);
        Assert.Contains(cg => cg.Name == ".data$zz", coffGroupsFound);
        Assert.Contains(cg => cg.Name == ".bss", coffGroupsFound);
    }

    [TestMethod]
    public async Task DataGridColumnsAreCreatedCorrectly()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Libs as IReadOnlyCollection<Library>));

        var viewmodel = new AllLibsPageViewModel(this.MockUITaskScheduler.Object,
                                                 generator.MockSession.Object,
                                                 new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        Assert.HasCount(2 /* sections */ + 4 /* COFF Groups */, viewmodel.DataGridSizeColumnDescriptions);
        Assert.IsNotNull(viewmodel.DataGridSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "Section: .text" &&
                                                                              cd.PropertyPath == "SectionContributionsByName[.text].Size").FirstOrDefault());
        Assert.IsNotNull(viewmodel.DataGridSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "Section: .data" &&
                                                                              cd.PropertyPath == "SectionContributionsByName[.data].Size").FirstOrDefault());
        Assert.IsNotNull(viewmodel.DataGridSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "COFF Group: .text$mn" &&
                                                                              cd.PropertyPath == "COFFGroupContributionsByName[.text$mn].Size").FirstOrDefault());
        Assert.IsNotNull(viewmodel.DataGridSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "COFF Group: .text$zz" &&
                                                                              cd.PropertyPath == "COFFGroupContributionsByName[.text$zz].Size").FirstOrDefault());
        Assert.IsNotNull(viewmodel.DataGridSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "COFF Group: .data$xx" &&
                                                                              cd.PropertyPath == "COFFGroupContributionsByName[.data$xx].Size").FirstOrDefault());
        Assert.IsNotNull(viewmodel.DataGridSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "COFF Group: .data$zz" &&
                                                                              cd.PropertyPath == "COFFGroupContributionsByName[.data$zz].Size").FirstOrDefault());

        Assert.HasCount(2 /* sections */ + 5 /* COFF Groups */, viewmodel.DataGridVirtualSizeColumnDescriptions);
        Assert.IsNotNull(viewmodel.DataGridVirtualSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "Section: .text" &&
                                                                                     cd.PropertyPath == "SectionContributionsByName[.text].VirtualSize").FirstOrDefault());
        Assert.IsNotNull(viewmodel.DataGridVirtualSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "Section: .data" &&
                                                                                     cd.PropertyPath == "SectionContributionsByName[.data].VirtualSize").FirstOrDefault());
        Assert.IsNotNull(viewmodel.DataGridVirtualSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "COFF Group: .text$mn" &&
                                                                                     cd.PropertyPath == "COFFGroupContributionsByName[.text$mn].VirtualSize").FirstOrDefault());
        Assert.IsNotNull(viewmodel.DataGridVirtualSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "COFF Group: .text$zz" &&
                                                                                     cd.PropertyPath == "COFFGroupContributionsByName[.text$zz].VirtualSize").FirstOrDefault());
        Assert.IsNotNull(viewmodel.DataGridVirtualSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "COFF Group: .data$xx" &&
                                                                                     cd.PropertyPath == "COFFGroupContributionsByName[.data$xx].VirtualSize").FirstOrDefault());
        Assert.IsNotNull(viewmodel.DataGridVirtualSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "COFF Group: .data$zz" &&
                                                                                     cd.PropertyPath == "COFFGroupContributionsByName[.data$zz].VirtualSize").FirstOrDefault());
        Assert.IsNotNull(viewmodel.DataGridVirtualSizeColumnDescriptions.Where(cd => cd.Header.ToString() == "COFF Group: .bss" &&
                                                                                     cd.PropertyPath == "COFFGroupContributionsByName[.bss].VirtualSize").FirstOrDefault());

        // Verify no two columns share a header - this would be confusing to a user
        foreach (var column in viewmodel.DataGridSizeColumnDescriptions)
        {
            var columnsWithSameName = viewmodel.DataGridSizeColumnDescriptions.Where(c => c.Header == column.Header);
            Assert.HasCount(1, columnsWithSameName);
        }
        foreach (var column in viewmodel.DataGridVirtualSizeColumnDescriptions)
        {
            var columnsWithSameName = viewmodel.DataGridVirtualSizeColumnDescriptions.Where(c => c.Header == column.Header);
            Assert.HasCount(1, columnsWithSameName);
        }
    }

    [TestMethod]
    public async Task ExcelExportDataForSizeIsFormattedUsefully()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Libs as IReadOnlyCollection<Library>));

        var viewmodel = new AllLibsPageViewModel(this.MockUITaskScheduler.Object,
                                                 generator.MockSession.Object,
                                                 new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        Assert.IsTrue(viewmodel.ShouldDisplaySize);
        Assert.IsFalse(viewmodel.ShouldDisplayVirtualSize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);
        Assert.HasCount(9, columnHeadersList);

        var libNameIndex = columnHeadersList.IndexOf("Lib Name");
        Assert.IsGreaterThanOrEqualTo(0, libNameIndex);
        var libShortNameIndex = columnHeadersList.IndexOf("Lib Short Name");
        Assert.IsGreaterThanOrEqualTo(0, libShortNameIndex);
        var libTotalSizeIndex = columnHeadersList.IndexOf("Lib Total Size on Disk");
        Assert.IsGreaterThanOrEqualTo(0, libTotalSizeIndex);

        Assert.Contains("Section: .text", columnHeadersList);
        Assert.Contains("COFF Group: .text$mn", columnHeadersList);
        Assert.Contains("COFF Group: .text$zz", columnHeadersList);
        Assert.Contains("Section: .data", columnHeadersList);
        Assert.Contains("COFF Group: .data$xx", columnHeadersList);
        Assert.Contains("COFF Group: .data$zz", columnHeadersList);

        // And now let's spot-check some of the data - not exhaustive because that's not really necessary
        Assert.HasCount(2, preformattedData);
        var aLibPreformatted = preformattedData.Single(d => d["Lib Short Name"].ToString() == "a");
        var bLibPreformatted = preformattedData.Single(d => d["Lib Short Name"].ToString() == "b");

        Assert.AreEqual(5925u, Convert.ToUInt32(aLibPreformatted["Lib Total Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(5000u, Convert.ToUInt32(aLibPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(3000u, Convert.ToUInt32(aLibPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(2000u, Convert.ToUInt32(aLibPreformatted["COFF Group: .text$zz"], CultureInfo.InvariantCulture));
        Assert.AreEqual(925u, Convert.ToUInt32(aLibPreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(425u, Convert.ToUInt32(aLibPreformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));

        Assert.AreEqual(25u, Convert.ToUInt32(bLibPreformatted["Lib Total Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(25u, Convert.ToUInt32(bLibPreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(25u, Convert.ToUInt32(bLibPreformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task ExcelExportDataForVirtualSizeIsFormattedUsefully()
    {
        using var generator = new SingleBinaryDataGenerator();

        generator.MockSession.Setup(s => s.EnumerateLibs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(generator.Libs as IReadOnlyCollection<Library>));

        var viewmodel = new AllLibsPageViewModel(this.MockUITaskScheduler.Object,
                                                 generator.MockSession.Object,
                                                 new Mock<IExcelExporter>().Object);

        await viewmodel.InitializeAsync();

        viewmodel.SelectedDisplayModeIndex = 1;

        Assert.IsFalse(viewmodel.ShouldDisplaySize);
        Assert.IsTrue(viewmodel.ShouldDisplayVirtualSize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);
        Assert.HasCount(10, columnHeadersList);

        var libNameIndex = columnHeadersList.IndexOf("Lib Name");
        Assert.IsGreaterThanOrEqualTo(0, libNameIndex);
        var libShortNameIndex = columnHeadersList.IndexOf("Lib Short Name");
        Assert.IsGreaterThanOrEqualTo(0, libShortNameIndex);
        var libTotalSizeIndex = columnHeadersList.IndexOf("Lib Total Size in Memory");
        Assert.IsGreaterThanOrEqualTo(0, libTotalSizeIndex);

        Assert.Contains("Section: .text", columnHeadersList);
        Assert.Contains("COFF Group: .text$mn", columnHeadersList);
        Assert.Contains("COFF Group: .text$zz", columnHeadersList);
        Assert.Contains("Section: .data", columnHeadersList);
        Assert.Contains("COFF Group: .data$xx", columnHeadersList);
        Assert.Contains("COFF Group: .data$zz", columnHeadersList);
        Assert.Contains("COFF Group: .bss", columnHeadersList);

        // And now let's spot-check some of the data - not exhaustive because that's not really necessary
        Assert.HasCount(2, preformattedData);
        var aLibPreformatted = preformattedData.Single(d => d["Lib Short Name"].ToString() == "a");
        var bLibPreformatted = preformattedData.Single(d => d["Lib Short Name"].ToString() == "b");

        Assert.AreEqual(6375u, Convert.ToUInt32(aLibPreformatted["Lib Total Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(5000u, Convert.ToUInt32(aLibPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(3000u, Convert.ToUInt32(aLibPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(2000u, Convert.ToUInt32(aLibPreformatted["COFF Group: .text$zz"], CultureInfo.InvariantCulture));
        Assert.AreEqual(1375u, Convert.ToUInt32(aLibPreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(425u, Convert.ToUInt32(aLibPreformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));
        Assert.AreEqual(450u, Convert.ToUInt32(aLibPreformatted["COFF Group: .bss"], CultureInfo.InvariantCulture));

        Assert.AreEqual(75u, Convert.ToUInt32(bLibPreformatted["Lib Total Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(75u, Convert.ToUInt32(bLibPreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(25u, Convert.ToUInt32(bLibPreformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));
        Assert.AreEqual(50u, Convert.ToUInt32(bLibPreformatted["COFF Group: .bss"], CultureInfo.InvariantCulture));
    }
}
