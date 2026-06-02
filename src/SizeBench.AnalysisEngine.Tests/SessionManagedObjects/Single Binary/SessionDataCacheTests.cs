using Dia2Lib;

namespace SizeBench.AnalysisEngine.Tests;

[TestClass]
public sealed class SessionDataCacheTests : IDisposable
{
    internal Mock<IDiaSession> MockDIASession = new Mock<IDiaSession>();
    internal SessionDataCache Cache = new SessionDataCache();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockDIASession = new Mock<IDiaSession>();
        this.Cache = new SessionDataCache();
    }

    [TestMethod]
    public void InitiallyEmpty()
    {
        Assert.IsNull(this.Cache.AllBinarySections);
        Assert.IsNull(this.Cache.AllCOFFGroups);
        Assert.IsNull(this.Cache.AllCompilands);
        Assert.IsNull(this.Cache.AllLibs);
        Assert.IsNull(this.Cache.AllSourceFiles);
    }

    [TestMethod]
    public void TryFindSymIndicesFindsNothingIfNeverInitialized()
    {
        Assert.IsFalse(this.Cache.TryFindSymIndicesInRVARange(RVARange.FromRVAAndSize(0, 100), out var symIndicesByRVA, out var minIdx, out var maxIdx));
        Assert.IsNull(symIndicesByRVA);
        Assert.AreEqual(0, minIdx);
        Assert.AreEqual(0, maxIdx);
    }

    [TestMethod]
    public void TryFindSymIndicesFindsNothingIfEmpty()
    {
        this.Cache.InitializeRVARanges(new Dictionary<uint, List<uint>>(), new HashSet<uint>());

        Assert.IsFalse(this.Cache.TryFindSymIndicesInRVARange(RVARange.FromRVAAndSize(0, 100), out var symIndicesByRVA, out var minIdx, out var maxIdx));
        Assert.IsNull(symIndicesByRVA);
        Assert.AreEqual(0, minIdx);
        Assert.AreEqual(0, maxIdx);
    }

    [TestMethod]
    public void TryFindSymIndicesFindsNothingIfRangeBeforeFirst()
    {
        var initialSymIndicesByRVA = new Dictionary<uint, List<uint>>
        {
            { 100, [1, 2, 3] },
            { 200, [4, 5, 6] },
        };
        this.Cache.InitializeRVARanges(initialSymIndicesByRVA, new HashSet<uint>());

        Assert.IsFalse(this.Cache.TryFindSymIndicesInRVARange(RVARange.FromRVAAndSize(0, 50), out var symIndicesByRVA, out var minIdx, out var maxIdx));
        Assert.IsNull(symIndicesByRVA);
        Assert.AreEqual(0, minIdx);
        Assert.AreEqual(0, maxIdx);
    }

    [TestMethod]
    public void TryFindSymIndicesFindsCorrectRanges()
    {
        var initialSymIndicesByRVA = new Dictionary<uint, List<uint>>
        {
            { 100, [1, 2, 3] },
            { 200, [4, 5, 6] },
            { 300, [7, 8, 9] },
        };
        this.Cache.InitializeRVARanges(initialSymIndicesByRVA, new HashSet<uint>());

        // The range we are trying to find ends exactly at the first RVA we have sym indices for - we should get those
        Assert.IsTrue(this.Cache.TryFindSymIndicesInRVARange(new RVARange(0, 100), out var symIndicesByRVA, out var minIdx, out var maxIdx));
        Assert.IsNotNull(symIndicesByRVA);
        Assert.AreEqual(0, minIdx);
        Assert.AreEqual(0, maxIdx);

        // The range includes a couple of the RVAs we have symIndices for
        Assert.IsTrue(this.Cache.TryFindSymIndicesInRVARange(new RVARange(200, 300), out symIndicesByRVA, out minIdx, out maxIdx));
        Assert.IsNotNull(symIndicesByRVA);
        Assert.AreEqual(1, minIdx);
        Assert.AreEqual(2, maxIdx);

        // The range is between two RVAs we have indices for, so we find nothing
        Assert.IsFalse(this.Cache.TryFindSymIndicesInRVARange(new RVARange(101, 199), out symIndicesByRVA, out minIdx, out maxIdx));
        Assert.IsNull(symIndicesByRVA);
        Assert.AreEqual(0, minIdx);
        Assert.AreEqual(0, maxIdx);

        // The range starts between two RVAs but includes at least one
        Assert.IsTrue(this.Cache.TryFindSymIndicesInRVARange(new RVARange(150, 250), out symIndicesByRVA, out minIdx, out maxIdx));
        Assert.IsNotNull(symIndicesByRVA);
        Assert.AreEqual(1, minIdx);
        Assert.AreEqual(1, maxIdx);

        // The range is after the last one we have indices for, we find nothing
        Assert.IsFalse(this.Cache.TryFindSymIndicesInRVARange(new RVARange(301, 1000), out symIndicesByRVA, out minIdx, out maxIdx));
        Assert.IsNull(symIndicesByRVA);
        Assert.AreEqual(0, minIdx);
        Assert.AreEqual(0, maxIdx);
    }

    [TestMethod]
    public void LabelExistsAtRVAWorks()
    {
        this.Cache.InitializeRVARanges(new Dictionary<uint, List<uint>>(), new HashSet<uint>() { 123, 456 });

        Assert.IsFalse(this.Cache.LabelExistsAtRVA(0));
        Assert.IsFalse(this.Cache.LabelExistsAtRVA(321));
        Assert.IsTrue(this.Cache.LabelExistsAtRVA(123));
        Assert.IsTrue(this.Cache.LabelExistsAtRVA(456));
    }

    public void Dispose() => this.Cache.Dispose();
}
