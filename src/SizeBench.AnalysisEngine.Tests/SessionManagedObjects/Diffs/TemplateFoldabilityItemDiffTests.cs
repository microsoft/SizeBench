using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionManagedObjects.Diffs.Tests;

[TestClass]
public sealed class TemplateFoldabilityItemDiffTests : IDisposable
{
    private DiffTestDataGenerator _generator = new DiffTestDataGenerator();
    private TestDIAAdapter BeforeDIAAdapter = new TestDIAAdapter();
    private TestDIAAdapter AfterDIAAdapter = new TestDIAAdapter();

    //TODO: could this be ClassInitialize to improve test execution time?  I think all this state would be immutable between tests...right?
    [TestInitialize]
    public void TestInitialize()
    {
        this.BeforeDIAAdapter = new TestDIAAdapter();
        this.AfterDIAAdapter = new TestDIAAdapter();

        this._generator = new DiffTestDataGenerator(beforeDIAAdapter: this.BeforeDIAAdapter, afterDIAAdapter: this.AfterDIAAdapter);
    }

    [TestMethod]
    public void BeforeAndAfterNullThrows() => Assert.ThrowsException<ArgumentException>(() => new TemplateFoldabilityItemDiff(null, null));

    [TestMethod]
    public void BasicPropertiesCalculatedCorrectly()
    {
        var tfiDiffList = this._generator.GenerateTemplateFoldabilityItemDiffs(out var beforeTFIList, out var afterTFIList);
        var tfiDiff = tfiDiffList.First(tfid => tfid.BeforeTemplateFoldabilityItem != null && tfid.AfterTemplateFoldabilityItem != null);

        Assert.IsNotNull(tfiDiff.BeforeTemplateFoldabilityItem);
        Assert.AreEqual(tfiDiff.BeforeTemplateFoldabilityItem.TemplateName, tfiDiff.TemplateName);
        Assert.AreEqual(0, tfiDiff.WastedSizeDiff);
        Assert.AreEqual(0, tfiDiff.SizeDiff);
        Assert.IsNotNull(tfiDiff.AfterTemplateFoldabilityItem);
        Assert.AreEqual(tfiDiff.AfterTemplateFoldabilityItem.WastedSize, tfiDiff.WastedSizeRemaining);
    }

    [TestMethod]
    public void BeforeTFINullWorks()
    {
        var tfiDiffList = this._generator.GenerateTemplateFoldabilityItemDiffs(out var beforeTFIList, out var afterTFIList);
        var tfiDiff = tfiDiffList.First(tfid => tfid.BeforeTemplateFoldabilityItem is null);

        Assert.IsNotNull(tfiDiff.AfterTemplateFoldabilityItem);
        Assert.AreEqual<long>(tfiDiff.AfterTemplateFoldabilityItem.WastedSize, tfiDiff.WastedSizeDiff);
        Assert.AreEqual<long>(tfiDiff.AfterTemplateFoldabilityItem.TotalSize, tfiDiff.SizeDiff);
        Assert.AreEqual<long>(tfiDiff.AfterTemplateFoldabilityItem.WastedSize, tfiDiff.WastedSizeRemaining);
        Assert.AreEqual(tfiDiff.AfterTemplateFoldabilityItem.TemplateName, tfiDiff.TemplateName);
    }

    [TestMethod]
    public void AfterTFINullWorks()
    {
        var tfiDiffList = this._generator.GenerateTemplateFoldabilityItemDiffs(out var beforeTFIList, out var afterTFIList);
        var tfiDiff = tfiDiffList.First(tfid => tfid.AfterTemplateFoldabilityItem is null);

        Assert.IsNotNull(tfiDiff.BeforeTemplateFoldabilityItem);
        Assert.AreEqual(0 - (long)tfiDiff.BeforeTemplateFoldabilityItem.WastedSize, tfiDiff.WastedSizeDiff);
        Assert.AreEqual(0 - (long)tfiDiff.BeforeTemplateFoldabilityItem.TotalSize, tfiDiff.SizeDiff);
        Assert.AreEqual(0ul, tfiDiff.WastedSizeRemaining);
        Assert.AreEqual(tfiDiff.BeforeTemplateFoldabilityItem.TemplateName, tfiDiff.TemplateName);
    }

    public void Dispose() => this._generator.Dispose();
}
