using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[TestClass]
public sealed class EnumerateSymbolsInBinarySectionSessionTaskTests : IDisposable
{
    private SessionTaskParameters? SessionTaskParameters;
    private SessionDataCache DataCache = new SessionDataCache();
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestDIAAdapter = new TestDIAAdapter();
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };

        this.SessionTaskParameters = new SessionTaskParameters(
            new Mock<ISession>().Object,
            this.TestDIAAdapter,
            this.DataCache);

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
        var section = new BinarySection(this.SessionTaskParameters!.DataCache, ".text", size: 0, virtualSize: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        section.MarkFullyConstructed();
        var task = new EnumerateSymbolsInBinarySectionSessionTask(this.SessionTaskParameters,
                                                                  CancellationToken.None,
                                                                  null /* progress */,
                                                                  section);

        Assert.IsTrue(task.TaskName.Contains(section.Name, StringComparison.Ordinal));
        using var logger = new NoOpLogger();
        var symbols = task.Execute(logger);
        Assert.IsNotNull(symbols);
    }

    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = false)]
    [TestMethod]
    public void CanCancelInTheMiddleOfEnumerationWhenRVAsAreMonotonicallyIncreasing()
    {
        var section = new BinarySection(this.SessionTaskParameters!.DataCache, ".text", size: 0x1000, virtualSize: 0, rva: 0x500, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute);
        section.MarkFullyConstructed();
        var cancellationTokenSource = new CancellationTokenSource();

        var symbolsToFind = new List<ValueTuple<ISymbol, uint>>()
            {
                new ValueTuple<ISymbol, uint>(new Mock<ISymbol>().Object, 0 /* RVAs searched */),
                new ValueTuple<ISymbol, uint>(new Mock<ISymbol>().Object, 10 /* RVAs searched */),
                new ValueTuple<ISymbol, uint>(new Mock<ISymbol>().Object, 15 /* RVAs searched */)
            };

        this.TestDIAAdapter.SymbolsToFindByRVARange.Add(RVARange.FromRVAAndSize(section.RVA, section.Size),
                                                        symbolsToFind.EnumerateListButCancelInTheMiddleOfEnumerating(cancellationTokenSource, cancelAfter: 1));

        var task = new EnumerateSymbolsInBinarySectionSessionTask(this.SessionTaskParameters,
                                                                  cancellationTokenSource.Token,
                                                                  null /* progress */,
                                                                  section);

        IReadOnlyList<ISymbol>? symbols = null;
        OperationCanceledException? capturedException = null;
        try
        {
            symbols = task.Execute(new NoOpLogger());
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
        var section = new BinarySection(this.SessionTaskParameters!.DataCache, ".text", size: 0x200, virtualSize: expectedSymbolCount, rva: 0x500, fileAlignment: 0x200, sectionAlignment: 0x1000, characteristics: SectionCharacteristics.MemExecute);
        section.MarkFullyConstructed();
        var mockProgress = new Mock<IProgress<SessionTaskProgress>>();
        mockProgress.Setup(p => p.Report(It.IsAny<SessionTaskProgress>())).Verifiable();

        var symbolsToFind = new List<ValueTuple<ISymbol, uint>>();

        for (uint i = 0; i < expectedSymbolCount; i++)
        {
            ISymbol symbolToFind = new SimpleFunctionCodeSymbol(this.SessionTaskParameters.DataCache, $"test {i}", rva: section.RVA + i, size: 1, symIndexId: i);

            symbolsToFind.Add(new ValueTuple<ISymbol, uint>(symbolToFind, i));
        }

        this.TestDIAAdapter.SymbolsToFindByRVARange.Add(RVARange.FromRVAAndSize(section.RVA, section.Size), symbolsToFind);

        var task = new EnumerateSymbolsInBinarySectionSessionTask(this.SessionTaskParameters,
                                                                  CancellationToken.None,
                                                                  mockProgress.Object,
                                                                  section);

        using var logger = new NoOpLogger();
        var symbols = task.Execute(logger);
        Assert.AreEqual(expectedSymbolCount, symbols.Count);

        // We expect one "Starting Up..." progress report, then two real ones
        mockProgress.Verify(p => p.Report(It.IsAny<SessionTaskProgress>()), Times.Exactly(3));
    }

    public void Dispose() => this.DataCache.Dispose();
}
