using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.TestDataCommon;
using SizeBench.GUI.Core;
using SizeBench.GUI.Tests;

namespace SizeBench.GUI.Pages.Tests;

[TestClass]
public sealed class AllCompilandDiffsPageViewModelTests : IDisposable
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
    public async Task BinarySectionsInCompilandsDeDuplicates()
    {
        var compilandDiffList = this.TestDataGenerator.CompilandDiffs;
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateCompilandDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(compilandDiffList as IReadOnlyList<CompilandDiff>));

        var viewmodel = new AllCompilandDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object,
                                                           this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        IList<string> sectionsInCompilands = viewmodel.BinarySectionsInCompilands().Select(s => s.Name).ToList();

        Assert.AreEqual(5, sectionsInCompilands.Count);
        Assert.IsTrue(sectionsInCompilands.Contains(".text"));
        Assert.IsTrue(sectionsInCompilands.Contains(".data"));
        Assert.IsTrue(sectionsInCompilands.Contains(".rdata"));
        Assert.IsTrue(sectionsInCompilands.Contains(".virt"));
        Assert.IsTrue(sectionsInCompilands.Contains(".rsrc"));
    }

    [TestMethod]
    public async Task COFFGroupsInCompilandsDeDuplicates()
    {
        var compilandDiffList = this.TestDataGenerator.CompilandDiffs;
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateCompilandDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(compilandDiffList as IReadOnlyList<CompilandDiff>));

        var viewmodel = new AllCompilandDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object,
                                                           this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        IList<string> coffGroupsInCompilands = viewmodel.COFFGroupsInCompilands().Select(s => s.Name).ToList();

        Assert.AreEqual(12, coffGroupsInCompilands.Count);
        Assert.IsTrue(coffGroupsInCompilands.Contains(".text$mn"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".text$zz"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".data$xx"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".data$zz"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".bss"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".rdata$xx"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".rdata$zz"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".rdata$foo"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".rdata$bef"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".rdata$aft"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".virt"));
        Assert.IsTrue(coffGroupsInCompilands.Contains(".rsrc"));
    }

    [TestMethod]
    public async Task ExcelExportDataForSizeIsFormattedUsefully()
    {
        var compilandDiffList = this.TestDataGenerator.CompilandDiffs;
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateCompilandDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(compilandDiffList as IReadOnlyList<CompilandDiff>));

        var viewmodel = new AllCompilandDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object,
                                                           this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        Assert.IsFalse(viewmodel.ShouldDisplayVirtualSize);
        Assert.IsTrue(viewmodel.ShouldDisplaySize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);

        var libNameIndex = columnHeadersList.IndexOf("Compiland Name");
        Assert.IsTrue(libNameIndex >= 0);
        var libShortNameIndex = columnHeadersList.IndexOf("Compiland Short Name");
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
        Assert.AreEqual(8, preformattedData.Count);
        // Lib present only in 'before'

        var compilandOnlyInBeforePreformatted = preformattedData.Single(d => d["Compiland Name"].ToString() == "a3.obj");
        // Lib present only in 'after'
        var compilandOnlyInAfterPreformatted = preformattedData.Single(d => d["Compiland Name"].ToString() == "a4.obj");
        // Lib present in both 'before' and 'after'
        var compilandInBeforeAndAfterPreformatted = preformattedData.Single(d => d["Compiland Name"].ToString() == "b1.obj");

        Assert.AreEqual(800, Convert.ToInt32(compilandOnlyInBeforePreformatted["Total Before Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInBeforePreformatted["Total After Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-800, Convert.ToInt32(compilandOnlyInBeforePreformatted["Total Size on Disk Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInBeforePreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-800, Convert.ToInt32(compilandOnlyInBeforePreformatted["Section: .rdata"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-300, Convert.ToInt32(compilandOnlyInBeforePreformatted["COFF Group: .rdata$bef"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-500, Convert.ToInt32(compilandOnlyInBeforePreformatted["COFF Group: .rdata$foo"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInBeforePreformatted["Section: .rsrc"], CultureInfo.InvariantCulture));

        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInAfterPreformatted["Total Before Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(800, Convert.ToInt32(compilandOnlyInAfterPreformatted["Total After Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(800, Convert.ToInt32(compilandOnlyInAfterPreformatted["Total Size on Disk Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInAfterPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInAfterPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(800, Convert.ToInt32(compilandOnlyInAfterPreformatted["Section: .rdata"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInAfterPreformatted["COFF Group: .rdata$bef"], CultureInfo.InvariantCulture));
        Assert.AreEqual(300, Convert.ToInt32(compilandOnlyInAfterPreformatted["COFF Group: .rdata$aft"], CultureInfo.InvariantCulture));
        Assert.AreEqual(500, Convert.ToInt32(compilandOnlyInAfterPreformatted["COFF Group: .rdata$foo"], CultureInfo.InvariantCulture));

        Assert.AreEqual(1900, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["Total Before Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(1000, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["Total After Size on Disk"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-900, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["Total Size on Disk Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(100, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(100, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["COFF Group: .text$zz"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-1000, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-25, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["COFF Group: .data$xx"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-975, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task ExcelExportDataForVirtualSizeIsFormattedUsefully()
    {
        var compilandDiffList = this.TestDataGenerator.CompilandDiffs;
        this.TestDataGenerator.MockDiffSession.Setup(ds => ds.EnumerateCompilandDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult(compilandDiffList as IReadOnlyList<CompilandDiff>));

        var viewmodel = new AllCompilandDiffsPageViewModel(this.MockUITaskScheduler.Object,
                                                           this.TestDataGenerator.MockDiffSession.Object,
                                                           this.MockExcelExporter.Object);
        await viewmodel.InitializeAsync();

        viewmodel.SelectedDisplayModeIndex = 1;

        // Verify we'll be exporting VirtualSize
        Assert.IsTrue(viewmodel.ShouldDisplayVirtualSize);
        Assert.IsFalse(viewmodel.ShouldDisplaySize);

        viewmodel.GenerateFormattedDataForExcelExport(out var columnHeaders, out var preformattedData);

        var columnHeadersList = new List<string>(columnHeaders);

        var libNameIndex = columnHeadersList.IndexOf("Compiland Name");
        Assert.IsTrue(libNameIndex >= 0);
        var libShortNameIndex = columnHeadersList.IndexOf("Compiland Short Name");
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
        Assert.AreEqual(8, preformattedData.Count);
        // Lib present only in 'before'

        var compilandOnlyInBeforePreformatted = preformattedData.Single(d => d["Compiland Name"].ToString() == "a3.obj");
        // Lib present only in 'after'
        var compilandOnlyInAfterPreformatted = preformattedData.Single(d => d["Compiland Name"].ToString() == "a4.obj");
        // Lib present in both 'before' and 'after'
        var compilandInBeforeAndAfterPreformatted = preformattedData.Single(d => d["Compiland Name"].ToString() == "b1.obj");

        Assert.AreEqual(1100, Convert.ToInt32(compilandOnlyInBeforePreformatted["Total Before Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInBeforePreformatted["Total After Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-1100, Convert.ToInt32(compilandOnlyInBeforePreformatted["Total Size in Memory Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInBeforePreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-300, Convert.ToInt32(compilandOnlyInBeforePreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-300, Convert.ToInt32(compilandOnlyInBeforePreformatted["COFF Group: .bss"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-800, Convert.ToInt32(compilandOnlyInBeforePreformatted["Section: .rdata"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-300, Convert.ToInt32(compilandOnlyInBeforePreformatted["COFF Group: .rdata$bef"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-500, Convert.ToInt32(compilandOnlyInBeforePreformatted["COFF Group: .rdata$foo"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInBeforePreformatted["Section: .rsrc"], CultureInfo.InvariantCulture));

        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInAfterPreformatted["Total Before Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(1200, Convert.ToInt32(compilandOnlyInAfterPreformatted["Total After Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(1200, Convert.ToInt32(compilandOnlyInAfterPreformatted["Total Size in Memory Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInAfterPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInAfterPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(400, Convert.ToInt32(compilandOnlyInAfterPreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(400, Convert.ToInt32(compilandOnlyInAfterPreformatted["COFF Group: .bss"], CultureInfo.InvariantCulture));
        Assert.AreEqual(800, Convert.ToInt32(compilandOnlyInAfterPreformatted["Section: .rdata"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandOnlyInAfterPreformatted["COFF Group: .rdata$bef"], CultureInfo.InvariantCulture));
        Assert.AreEqual(300, Convert.ToInt32(compilandOnlyInAfterPreformatted["COFF Group: .rdata$aft"], CultureInfo.InvariantCulture));
        Assert.AreEqual(500, Convert.ToInt32(compilandOnlyInAfterPreformatted["COFF Group: .rdata$foo"], CultureInfo.InvariantCulture));

        Assert.AreEqual(1900, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["Total Before Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(1000, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["Total After Size in Memory"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-900, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["Total Size in Memory Diff"], CultureInfo.InvariantCulture));
        Assert.AreEqual(100, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["Section: .text"], CultureInfo.InvariantCulture));
        Assert.AreEqual(100, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["COFF Group: .text$mn"], CultureInfo.InvariantCulture));
        Assert.AreEqual(0, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["COFF Group: .text$zz"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-1000, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["Section: .data"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-25, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["COFF Group: .data$xx"], CultureInfo.InvariantCulture));
        Assert.AreEqual(-975, Convert.ToInt32(compilandInBeforeAndAfterPreformatted["COFF Group: .data$zz"], CultureInfo.InvariantCulture));
    }

    public void Dispose() => this.TestDataGenerator.Dispose();
}
