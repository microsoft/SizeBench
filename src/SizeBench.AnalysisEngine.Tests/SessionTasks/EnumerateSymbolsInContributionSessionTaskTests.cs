using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[TestClass]
public sealed class EnumerateSymbolsInContributionSessionTaskTests : IDisposable
{
    public TestContext? TestContext { get; set; }
    public CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

    private LibCOFFGroupContribution? TestContribution;
    private Library? TestLib;
    private COFFGroup? TextMnCG;
    private SessionTaskParameters? SessionTaskParameters;
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private SessionDataCache DataCache = new SessionDataCache();
    private Mock<ISession> MockSession = new Mock<ISession>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestDIAAdapter = new TestDIAAdapter();
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        this.MockSession = new Mock<ISession>();

        this.SessionTaskParameters = new SessionTaskParameters(
            this.MockSession.Object,
            this.TestDIAAdapter,
            this.DataCache);

        this.TextMnCG = new COFFGroup(this.SessionTaskParameters.DataCache, ".text$mn", size: 0x0, rva: 0x0, fileAlignment: 0, sectionAlignment: 0, characteristics: PE.DataSectionFlags.MemoryExecute);
        this.TestLib = new Library("LIB blah");
        this.TestContribution = this.TestLib.GetOrCreateCOFFGroupContribution(this.TextMnCG);

        this.SessionTaskParameters.DataCache.PDataRVARange = new RVARange(0, 0);
        this.SessionTaskParameters.DataCache.PDataSymbolsByRVA = new SortedList<uint, PDataSymbol>();
        this.SessionTaskParameters.DataCache.XDataRVARanges = new RVARangeSet();
        this.SessionTaskParameters.DataCache.XDataSymbolsByRVA = new SortedList<uint, XDataSymbol>();
        this.SessionTaskParameters.DataCache.RsrcRVARange = new RVARange(0, 0);
        this.SessionTaskParameters.DataCache.RsrcSymbolsByRVA = new SortedList<uint, RsrcSymbolBase>();
        this.SessionTaskParameters.DataCache.OtherPESymbolsRVARanges = new RVARangeSet();
        this.SessionTaskParameters.DataCache.OtherPESymbolsByRVA = new SortedList<uint, ISymbol>();
    }

    [TestMethod]
    public void CanExecuteWithoutProgressReporting()
    {
        this.TestContribution!.MarkFullyConstructed();
        this.TestLib!.MarkFullyConstructed();
        this.TextMnCG!.MarkFullyConstructed();
        var task = new EnumerateSymbolsInContributionSessionTask(this.SessionTaskParameters!,
                                                                 this.CancellationToken,
                                                                 null /* progress */,
                                                                 this.TestContribution);

        Assert.IsTrue(task.TaskName.Contains(this.TestContribution.Name, StringComparison.Ordinal));
        using var logger = new NoOpLogger();
        var symbols = task.Execute(logger);
        Assert.IsNotNull(symbols);
    }

    [TestMethod]
    public void CanEnumerateSymbolsInPDATATable()
    {
        const uint rvaBegin = 500;
        const uint rvaEnd = 1000;
        const uint rvaGap = 100;
        this.TestContribution!.AddRVARange(new RVARange(rvaBegin, rvaEnd));
        this.TestContribution.MarkFullyConstructed();
        this.TestLib!.MarkFullyConstructed();
        this.TextMnCG!.MarkFullyConstructed();

        // Generate some symbols that start before rvaBegin and go beyond rvaEnd to ensure we don't
        // walk outside the expected range.
        SetupGeneratePDATASymbols(0, rvaEnd + 1000, rvaGap);

        var task = new EnumerateSymbolsInContributionSessionTask(this.SessionTaskParameters!,
                                                                 this.CancellationToken,
                                                                 null /* progress */,
                                                                 this.TestContribution);

        Assert.IsTrue(task.TaskName.Contains(this.TestContribution.Name, StringComparison.Ordinal));
        using var logger = new NoOpLogger();
        var symbols = task.Execute(logger);
        Assert.AreEqual<uint>((rvaEnd - rvaBegin) / rvaGap, (uint)symbols.Count); // Symbols at 500, 600, 700, 800, 900 RVA (should not enumerate 1000 since that symbol goes from 1000-1099 which goes beynd RVAEnd)
    }

    [TestMethod]
    public void CanEnumerateSymbolsMixedBetweenPDATAAndDIA()
    {
        const uint diaRvaBegin = 1800;
        const uint diaRvaEnd = 2000;
        const uint diaRvaGap = 100;
        const uint expectedDiaSymbolCount = (diaRvaEnd - diaRvaBegin) / diaRvaGap;
        const uint pdataRvaBegin = 500;
        const uint pdataRvaEnd = 1000;
        const uint pdataRvaGap = 100;
        const uint expectedPDATASymbolCount = (pdataRvaEnd - pdataRvaBegin) / pdataRvaGap;
        this.TestContribution!.AddRVARange(new RVARange(diaRvaBegin, diaRvaEnd)); // a range of symbols from the PDB (DIA)
        this.TestContribution.AddRVARange(new RVARange(pdataRvaBegin, pdataRvaEnd)); // a range of symbols in pdata
        this.TestContribution.MarkFullyConstructed();
        this.TestLib!.MarkFullyConstructed();
        this.TextMnCG!.MarkFullyConstructed();

        // Generate some symbols that start before rvaBegin and go beyond rvaEnd to ensure we don't
        // walk outside the expected range.
        SetupGeneratePDATASymbols(0, diaRvaBegin - 10, pdataRvaGap);

        // Now set up the DIA symbols
        var symbolsToFind = new List<ValueTuple<ISymbol, uint>>();

        for (uint i = 0; i < expectedDiaSymbolCount; i++)
        {
            ISymbol symbolToFind = new SimpleFunctionCodeSymbol(this.SessionTaskParameters!.DataCache, $"test DIA symbol {i}", rva: diaRvaBegin + (i * diaRvaGap), size: diaRvaGap, symIndexId: i);

            symbolsToFind.Add(new ValueTuple<ISymbol, uint>(symbolToFind, i * diaRvaGap));
        }

        this.TestDIAAdapter.SymbolsToFindByRVARange.Add(new RVARange(diaRvaBegin, diaRvaEnd), symbolsToFind);

        var task = new EnumerateSymbolsInContributionSessionTask(this.SessionTaskParameters!,
                                                                 this.CancellationToken,
                                                                 null /* progress */,
                                                                 this.TestContribution);

        Assert.IsTrue(task.TaskName.Contains(this.TestContribution.Name, StringComparison.Ordinal));
        using var logger = new NoOpLogger();
        var symbols = task.Execute(logger);
        Assert.AreEqual<uint>(expectedPDATASymbolCount + expectedDiaSymbolCount, (uint)symbols.Count);
    }

    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = false)]
    [TestMethod]
    public void CanCancelInTheMiddleOfEnumerationWhenRVAsAreMonotonicallyIncreasing()
    {
        this.TestContribution!.AddRVARange(RVARange.FromRVAAndSize(0x500, 0x1000));
        this.TestContribution.MarkFullyConstructed();
        this.TestLib!.MarkFullyConstructed();
        this.TextMnCG!.MarkFullyConstructed();
        var cancellationTokenSource = new CancellationTokenSource();

        var symbolsToFind = new List<ValueTuple<ISymbol, uint>>()
            {
                new ValueTuple<ISymbol, uint>(new Mock<ISymbol>().Object, 0 /* RVAs searched */),
                new ValueTuple<ISymbol, uint>(new Mock<ISymbol>().Object, 10 /* RVAs searched */),
                new ValueTuple<ISymbol, uint>(new Mock<ISymbol>().Object, 15 /* RVAs searched */)
            };

        this.TestDIAAdapter.SymbolsToFindByRVARange.Add(this.TestContribution.RVARanges[0],
                                                   symbolsToFind.EnumerateListButCancelInTheMiddleOfEnumerating(cancellationTokenSource, cancelAfter: 1));

        var task = new EnumerateSymbolsInContributionSessionTask(this.SessionTaskParameters!,
                                                                 cancellationTokenSource.Token,
                                                                 null /* progress */,
                                                                 this.TestContribution);

        Assert.IsTrue(task.TaskName.Contains(this.TestContribution.Name, StringComparison.Ordinal));
        IList<ISymbol>? symbols = null;
        OperationCanceledException? capturedException = null;
        try
        {
            using var logger = new NoOpLogger();
            symbols = task.Execute(logger);
        }
        catch (OperationCanceledException ex)
        {
            capturedException = ex;
        }
        Assert.IsNull(symbols);
        Assert.IsNotNull(capturedException);
        throw capturedException;
    }

    [TestMethod]
    public void FiresProgressNotificationsWithoutSpamming()
    {
        const int expectedSymbolCount = 250;
        this.TestContribution!.AddRVARange(RVARange.FromRVAAndSize(0x500, expectedSymbolCount));
        this.TestContribution.MarkFullyConstructed();
        this.TestLib!.MarkFullyConstructed();
        this.TextMnCG!.MarkFullyConstructed();
        var mockProgress = new Mock<IProgress<SessionTaskProgress>>();
        mockProgress.Setup(p => p.Report(It.IsAny<SessionTaskProgress>())).Verifiable();

        var symbolsToFind = new List<ValueTuple<ISymbol, uint>>();

        for (uint i = 0; i < expectedSymbolCount; i++)
        {
            ISymbol symbolToFind = new SimpleFunctionCodeSymbol(this.SessionTaskParameters!.DataCache, $"test {i}", rva: this.TestContribution.RVARanges[0].RVAStart + i, size: 1, symIndexId: i);

            symbolsToFind.Add(new ValueTuple<ISymbol, uint>(symbolToFind, i));
        }

        this.TestDIAAdapter.SymbolsToFindByRVARange.Add(this.TestContribution.RVARanges[0], symbolsToFind);

        var task = new EnumerateSymbolsInContributionSessionTask(this.SessionTaskParameters!,
                                                                 this.CancellationToken,
                                                                 mockProgress.Object,
                                                                 this.TestContribution);

        Assert.IsTrue(task.TaskName.Contains(this.TestContribution.Name, StringComparison.Ordinal));
        using var logger = new NoOpLogger();
        var symbols = task.Execute(logger);
        Assert.AreEqual(expectedSymbolCount, symbols.Count);

        // We expect one "Starting Up..." progress report, then two real ones
        mockProgress.Verify(p => p.Report(It.IsAny<SessionTaskProgress>()), Times.Exactly(3));
    }

    private void SetupGeneratePDATASymbols(uint rvaBegin, uint rvaEnd, uint rvaGap)
    {
        this.DataCache.PDataRVARange = new RVARange(rvaBegin, rvaEnd);

        for (var pdataSymbolRva = rvaBegin; pdataSymbolRva < rvaEnd; pdataSymbolRva += rvaGap)
        {
            var pdataSymbol = new PDataSymbol(targetStartRVA: 0, unwindInfoStartRVA: 0, rva: pdataSymbolRva, size: rvaGap);
            this.DataCache.PDataSymbolsByRVA!.Add(pdataSymbol.RVA, pdataSymbol);
        }
    }

    public void Dispose() => this.DataCache.Dispose();
}
