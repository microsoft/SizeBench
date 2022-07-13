using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class AllLibDiffsPageViewModelTests : IDisposable
{
    private DiffTestDataGenerator TestDataGenerator = new DiffTestDataGenerator();
    private Mock<IExcelExporter> MockExcelExporter = new Mock<IExcelExporter>();
    private Mock<IUITaskScheduler> MockUITaskScheduler = new Mock<IUITaskScheduler>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestDataGenerator = new DiffTestDataGenerator();
        this.MockExcelExporter = new Mock<IExcelExporter>();
        this.MockUITaskScheduler = new Mock<IUITaskScheduler>();
        this.MockUITaskScheduler.SetupForSynchronousCompletionOfLongRunningUITasks();
    }

    [TestMethod]
    public async Task BinarySectionsInLibsDeDuplicates()
    {
        var libDiffList = this.TestDataGenerator.LibDiffs;
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(libDiffList as IReadOnlyList<LibDiff>));

        var viewmodel = new AllLibDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                     this.TestDataGenerator.MockDiffSession.Object,
                                                     this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        IList<string> sectionsInLibs = viewmodel.BinarySectionsInLibs().Select(s => s.Name).ToList();

        Assert.AreEqual(5, sectionsInLibs.Count);
        Assert.IsTrue(sectionsInLibs.Contains(".text"));
        Assert.IsTrue(sectionsInLibs.Contains(".data"));
        Assert.IsTrue(sectionsInLibs.Contains(".rdata"));
        Assert.IsTrue(sectionsInLibs.Contains(".virt"));
        Assert.IsTrue(sectionsInLibs.Contains(".rsrc"));
    }

    [TestMethod]
    public async Task COFFGroupsInCompilandsDeDuplicates()
    {
        var libDiffList = this.TestDataGenerator.LibDiffs;
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(libDiffList as IReadOnlyList<LibDiff>));

        var viewmodel = new AllLibDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                     this.TestDataGenerator.MockDiffSession.Object,
                                                     this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        IList<string> coffGroupsInLibs = viewmodel.COFFGroupsInLibs().Select(cg => cg.Name).ToList();

        Assert.AreEqual(12, coffGroupsInLibs.Count);
        Assert.IsTrue(coffGroupsInLibs.Contains(".text$mn"));
        Assert.IsTrue(coffGroupsInLibs.Contains(".text$zz"));
        Assert.IsTrue(coffGroupsInLibs.Contains(".data$xx"));
        Assert.IsTrue(coffGroupsInLibs.Contains(".data$zz"));
        Assert.IsTrue(coffGroupsInLibs.Contains(".bss"));
        Assert.IsTrue(coffGroupsInLibs.Contains(".rdata$xx"));
        Assert.IsTrue(coffGroupsInLibs.Contains(".rdata$zz"));
        Assert.IsTrue(coffGroupsInLibs.Contains(".rdata$foo"));
        Assert.IsTrue(coffGroupsInLibs.Contains(".rdata$bef"));
        Assert.IsTrue(coffGroupsInLibs.Contains(".rdata$aft"));
        Assert.IsTrue(coffGroupsInLibs.Contains(".virt"));
        Assert.IsTrue(coffGroupsInLibs.Contains(".rsrc"));
    }

    [TestMethod]
    public async Task ExcelExportDataForSizeIsFormattedUsefully()
    {
        var libDiffList = this.TestDataGenerator.LibDiffs;
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(libDiffList as IReadOnlyList<LibDiff>));

        var viewmodel = new AllLibDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                     this.TestDataGenerator.MockDiffSession.Object,
                                                     this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        Assert.IsFalse(viewmodel.ShouldDisplayVirtualSize);
        Assert.IsTrue(viewmodel.ShouldDisplaySize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);

        var libNameIndex = columnHeadersList.IndexOf("Lib Name");
        Assert.IsTrue(libNameIndex >= 0);
        var libShortNameIndex = columnHeadersList.IndexOf("Lib Short Name");
        Assert.IsTrue(libShortNameIndex >= 0);
        var libBeforeSizeIndex = columnHeadersList.IndexOf("Total Before Size on Disk");
        Assert.IsTrue(libBeforeSizeIndex >= 0);
        var libAfterSizeIndex = columnHeadersList.IndexOf("Total After Size on Disk");
        Assert.IsTrue(libAfterSizeIndex >= 0);
        var libTotalSizeIndex = columnHeadersList.IndexOf("Total Size on Disk Diff");
        Assert.IsTrue(libTotalSizeIndex >= 0);

        var sectionNames = new List<string>();
        var coffGroupNames = new List<string>();
        foreach (var section in this.TestDataGenerator.BinarySectionDiffs)
        {
            sectionNames.Add(section.Name);
            foreach (var cg in section.COFFGroupDiffs)
            {
                coffGroupNames.Add(cg.Name);
            }
        }

        // Every section and COFF Group should be represented, and we should never repeat a column name as that would be confusing
        // (so if there's a section named .rdata and a COFF Group named .rdata, they need prefixes to distinguish)
        foreach (var sectionName in sectionNames)
        {
            Assert.IsTrue(columnHeadersList.Contains($"Section: {sectionName}"));
        }

        foreach (var coffGroupName in coffGroupNames)
        {
            Assert.IsTrue(columnHeadersList.Contains($"COFF Group: {coffGroupName}"));
        }

        // And now let's spot-check some of the data - not exhaustive because that's not really necessary
        Assert.AreEqual(4, preformattedData.Count);
        // Lib present only in 'before'
        var libOnlyInBeforePreformatted = preformattedData.Single(d => d["Lib Name"].ToString() == "c.lib");
        // Lib present only in 'after'
        var libOnlyInAfterPreformatted = preformattedData.Single(d => d["Lib Name"].ToString() == "d.lib");
        // Lib present in both 'before' and 'after'
        var libInBeforeAndAfterPreformatted = preformattedData.Single(d => d["Lib Name"].ToString() == "a.lib");

        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["Total Before Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["Total After Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["Total Size on Disk Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["Section: .virt"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["COFF Group: .virt"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["COFF Group: .data$xx"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["Section: .rsrc"], CultureInfo.InvariantCulture));

        Assert.AreEqual(0, Convert.ToInt32(libOnlyInAfterPreformatted["Total Before Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200, Convert.ToInt32(libOnlyInAfterPreformatted["Total After Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200, Convert.ToInt32(libOnlyInAfterPreformatted["Total Size on Disk Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInAfterPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInAfterPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200, Convert.ToInt32(libOnlyInAfterPreformatted["Section: .rsrc"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200, Convert.ToInt32(libOnlyInAfterPreformatted["COFF Group: .rsrc"], CultureInfo.InvariantCulture));

        Assert.AreEqual(2300, Convert.ToInt32(libInBeforeAndAfterPreformatted["Total Before Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(2700, Convert.ToInt32(libInBeforeAndAfterPreformatted["Total After Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(400, Convert.ToInt32(libInBeforeAndAfterPreformatted["Total Size on Disk Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(400, Convert.ToInt32(libInBeforeAndAfterPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libInBeforeAndAfterPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(400, Convert.ToInt32(libInBeforeAndAfterPreformatted["COFF Group: .text$zz"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libInBeforeAndAfterPreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(25, Convert.ToInt32(libInBeforeAndAfterPreformatted["COFF Group: .data$xx"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-25, Convert.ToInt32(libInBeforeAndAfterPreformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task ExcelExportDataForVirtualSizeIsFormattedUsefully()
    {
        var libDiffList = this.TestDataGenerator.LibDiffs;
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(libDiffList as IReadOnlyList<LibDiff>));

        var viewmodel = new AllLibDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                     this.TestDataGenerator.MockDiffSession.Object,
                                                     this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        viewmodel.SelectedDisplayModeIndex = 1;

        Assert.IsTrue(viewmodel.ShouldDisplayVirtualSize);
        Assert.IsFalse(viewmodel.ShouldDisplaySize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);

        var libNameIndex = columnHeadersList.IndexOf("Lib Name");
        Assert.IsTrue(libNameIndex >= 0);
        var libShortNameIndex = columnHeadersList.IndexOf("Lib Short Name");
        Assert.IsTrue(libShortNameIndex >= 0);
        var libBeforeSizeIndex = columnHeadersList.IndexOf("Total Before Size in Memory");
        Assert.IsTrue(libBeforeSizeIndex >= 0);
        var libAfterSizeIndex = columnHeadersList.IndexOf("Total After Size in Memory");
        Assert.IsTrue(libAfterSizeIndex >= 0);
        var libTotalSizeIndex = columnHeadersList.IndexOf("Total Size in Memory Diff");
        Assert.IsTrue(libTotalSizeIndex >= 0);

        var sectionNames = new List<string>();
        var coffGroupNames = new List<string>();
        foreach (var section in this.TestDataGenerator.BinarySectionDiffs)
        {
            sectionNames.Add(section.Name);
            foreach (var cg in section.COFFGroupDiffs)
            {
                coffGroupNames.Add(cg.Name);
            }
        }

        // Every section and COFF Group should be represented, and we should never repeat a column name as that would be confusing
        // (so if there's a section named .rdata and a COFF Group named .rdata, they need prefixes to distinguish)
        foreach (var sectionName in sectionNames)
        {
            Assert.IsTrue(columnHeadersList.Contains($"Section: {sectionName}"));
        }

        foreach (var coffGroupName in coffGroupNames)
        {
            Assert.IsTrue(columnHeadersList.Contains($"COFF Group: {coffGroupName}"));
        }

        // And now let's spot-check some of the data - not exhaustive because that's not really necessary
        Assert.AreEqual(4, preformattedData.Count);
        // Lib present only in 'before'
        var libOnlyInBeforePreformatted = preformattedData.Single(d => d["Lib Name"].ToString() == "c.lib");
        // Lib present only in 'after'
        var libOnlyInAfterPreformatted = preformattedData.Single(d => d["Lib Name"].ToString() == "d.lib");
        // Lib present in both 'before' and 'after'
        var libInBeforeAndAfterPreformatted = preformattedData.Single(d => d["Lib Name"].ToString() == "a.lib");

        Assert.AreEqual(300, Convert.ToInt32(libOnlyInBeforePreformatted["Total Before Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["Total After Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-300, Convert.ToInt32(libOnlyInBeforePreformatted["Total Size in Memory Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-300, Convert.ToInt32(libOnlyInBeforePreformatted["Section: .virt"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-300, Convert.ToInt32(libOnlyInBeforePreformatted["COFF Group: .virt"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["COFF Group: .data$xx"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInBeforePreformatted["Section: .rsrc"], CultureInfo.InvariantCulture));

        Assert.AreEqual(0, Convert.ToInt32(libOnlyInAfterPreformatted["Total Before Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200, Convert.ToInt32(libOnlyInAfterPreformatted["Total After Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200, Convert.ToInt32(libOnlyInAfterPreformatted["Total Size in Memory Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInAfterPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libOnlyInAfterPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200, Convert.ToInt32(libOnlyInAfterPreformatted["Section: .rsrc"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200, Convert.ToInt32(libOnlyInAfterPreformatted["COFF Group: .rsrc"], CultureInfo.InvariantCulture));

        Assert.AreEqual(2800, Convert.ToInt32(libInBeforeAndAfterPreformatted["Total Before Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(3400, Convert.ToInt32(libInBeforeAndAfterPreformatted["Total After Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(600, Convert.ToInt32(libInBeforeAndAfterPreformatted["Total Size in Memory Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(400, Convert.ToInt32(libInBeforeAndAfterPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(libInBeforeAndAfterPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(400, Convert.ToInt32(libInBeforeAndAfterPreformatted["COFF Group: .text$zz"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200, Convert.ToInt32(libInBeforeAndAfterPreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(25, Convert.ToInt32(libInBeforeAndAfterPreformatted["COFF Group: .data$xx"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-25, Convert.ToInt32(libInBeforeAndAfterPreformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));
        Assert.AreEqual(200, Convert.ToInt32(libInBeforeAndAfterPreformatted["COFF Group: .bss"], CultureInfo.InvariantCulture));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
