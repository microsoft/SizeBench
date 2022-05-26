using System.Globalization;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[TestClass]
public sealed class LoadSymbolByRVASessionTaskTests : IDisposable
{
    private SessionTaskParameters? SessionTaskParameters;
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private Mock<ISession> MockSession = new Mock<ISession>();
    private SessionDataCache DataCache = new SessionDataCache();

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestDIAAdapter = new TestDIAAdapter();
        this.MockSession = new Mock<ISession>();
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };

        this.SessionTaskParameters = new SessionTaskParameters(
            this.MockSession.Object,
            this.TestDIAAdapter,
            this.DataCache);

        this.DataCache.PDataRVARange = new RVARange(0, 0);
        this.DataCache.PDataSymbolsByRVA = new SortedList<uint, PDataSymbol>();
        this.DataCache.XDataRVARanges = new RVARangeSet();
        this.DataCache.XDataSymbolsByRVA = new SortedList<uint, XDataSymbol>();
        this.DataCache.RsrcRVARange = new RVARange(0, 0);
        this.DataCache.RsrcSymbolsByRVA = new SortedList<uint, RsrcSymbolBase>();
        this.DataCache.OtherPESymbolsRVARanges = new RVARangeSet();
        this.DataCache.OtherPESymbolsByRVA = new SortedList<uint, ISymbol>();
    }

    [TestMethod]
    public void WorksWhenSymbolIsNotInPDATAButIsInDIA()
    {
        const uint expectedRVA = 0x500;
        const uint expectedLength = 0x200u;

        this.TestDIAAdapter.SymbolsToFindByRVA.Add(expectedRVA, new SimpleFunctionCodeSymbol(this.SessionTaskParameters!.DataCache, "dummySymbol", rva: expectedRVA, size: expectedLength, symIndexId: 0));

        var task = new LoadSymbolByRVASessionTask(this.SessionTaskParameters,
            expectedRVA,
            null /* progress */,
            CancellationToken.None);
        Assert.IsTrue(task.TaskName.Contains(expectedRVA.ToString("X", CultureInfo.InvariantCulture), StringComparison.Ordinal));

        using var logger = new NoOpLogger();
        var symbol = task.Execute(logger);
        Assert.IsNotNull(symbol);
        Assert.AreEqual(expectedRVA, symbol.RVA);
        Assert.AreEqual("dummySymbol()", symbol.Name);
        Assert.AreEqual(expectedLength, symbol.Size);
    }

    [TestMethod]
    public void WorksWhenSymbolIsInPDATA()
    {
        const uint expectedRVA = 0x500;
        const uint expectedSize = 0x200u;
        var pdataSymbol = new PDataSymbol(targetStartRVA: 1234, unwindInfoStartRVA: 0, rva: expectedRVA, size: expectedSize);
        this.DataCache.PDataSymbolsByRVA = new SortedList<uint, PDataSymbol>()
            {
                { expectedRVA, pdataSymbol }
            };

        var task = new LoadSymbolByRVASessionTask(this.SessionTaskParameters!,
            expectedRVA,
            null /* progress */,
            CancellationToken.None);
        Assert.IsTrue(task.TaskName.Contains(expectedRVA.ToString("X", CultureInfo.InvariantCulture), StringComparison.Ordinal));

        using var logger = new NoOpLogger();
        var symbol = task.Execute(logger);
        Assert.IsNotNull(symbol);
        Assert.AreEqual(pdataSymbol, symbol);
        Assert.AreEqual(expectedRVA, symbol.RVA);
        Assert.AreEqual(expectedSize, symbol.Size);
    }

    [TestMethod]
    public void ReturnsNullWhenNoSymbolToBeFound()
    {
        const uint expectedRVA = 0x500;

        var task = new LoadSymbolByRVASessionTask(this.SessionTaskParameters!,
            expectedRVA,
            null /* progress */,
            CancellationToken.None);
        Assert.IsTrue(task.TaskName.Contains(expectedRVA.ToString("X", CultureInfo.InvariantCulture), StringComparison.Ordinal));

        using var logger = new NoOpLogger();
        var symbol = task.Execute(logger);
        Assert.IsNull(symbol);
    }

    public void Dispose() => this.DataCache.Dispose();
}
