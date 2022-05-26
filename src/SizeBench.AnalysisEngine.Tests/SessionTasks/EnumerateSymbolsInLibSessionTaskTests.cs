using SizeBench.AnalysisEngine.PE;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[TestClass]
public sealed class EnumerateSymbolsInLibSessionTaskTests : IDisposable
{
    private Library? TestLib;
    private SessionTaskParameters? SessionTaskParameters;
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private SessionDataCache DataCache = new SessionDataCache();

    [TestInitialize]
    public void TestInitialize()
    {
        this.TestLib = new Library(@"c:\test\blah.lib");
        this.TestDIAAdapter = new TestDIAAdapter();
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };

        this.SessionTaskParameters = new SessionTaskParameters(
            new Mock<ISession>().Object,
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
    public void CanExecuteWithoutProgressReporting()
    {
        GenerateMockTextSection();
        this.TestLib!.MarkFullyConstructed();

        var task = new EnumerateSymbolsInLibSessionTask(this.SessionTaskParameters!,
                                                        CancellationToken.None,
                                                        null /* progress */,
                                                        this.TestLib);

        Assert.IsTrue(task.TaskName.Contains(this.TestLib.Name, StringComparison.Ordinal));
        using var logger = new NoOpLogger();
        var symbols = task.Execute(logger);
        Assert.IsNotNull(symbols);
    }

    [TestMethod]
    public void CanEnumerateSymbolsFromEachSectionContributionWithinLib()
    {
        var textSection = GenerateMockTextSection();
        var rdataSection = GenerateMockRDataSection();

        const uint expectedSymbolsPerSection = 0x1000 / 10;

        var textSectionSymbols = new List<ValueTuple<ISymbol, uint>>();
        for (uint i = 0; i < expectedSymbolsPerSection; i++)
        {
            ISymbol symbolToFind = new SimpleFunctionCodeSymbol(this.SessionTaskParameters!.DataCache, $"test .text symbol {i}", rva: textSection.RVA + (i * 10), size: 10, symIndexId: i);

            textSectionSymbols.Add(new ValueTuple<ISymbol, uint>(symbolToFind, i));
        }

        var rdataSectionSymbols = new List<(ISymbol, uint)>();
        for (uint i = 0; i < expectedSymbolsPerSection; i++)
        {
            ISymbol symbolToFind = new SimpleFunctionCodeSymbol(this.SessionTaskParameters!.DataCache, $"test .rdata symbol {i}", rva: rdataSection.RVA + (i * 10), size: 10, symIndexId: i + expectedSymbolsPerSection);

            rdataSectionSymbols.Add(new ValueTuple<ISymbol, uint>(symbolToFind, i));
        }

        this.TestDIAAdapter.SymbolsToFindByRVARange.Add(RVARange.FromRVAAndSize(textSection.RVA, textSection.Size), textSectionSymbols);
        this.TestDIAAdapter.SymbolsToFindByRVARange.Add(RVARange.FromRVAAndSize(rdataSection.RVA, rdataSection.Size), rdataSectionSymbols);

        this.TestLib!.MarkFullyConstructed();

        var mockProgress = new Mock<IProgress<SessionTaskProgress>>();

        // We should expect to see the following RVA Ranges (each with 1 symbol):
        // (0x1000, 0x1010), (0x1020, 0x1030), (0x1040,0x1050), ... (0x1780, 0x1790) (this totals 60 of these)
        // (0x4000, 0x4010), (0x4020, 0x4030), (0x4040,0x4050), ... (0x4780, 0x4790) (this totals 60 of these)
        const int expectedSymbolCount = 120;

        var task = new EnumerateSymbolsInLibSessionTask(this.SessionTaskParameters!,
                                                        CancellationToken.None,
                                                        mockProgress.Object,
                                                        this.TestLib);

        Assert.IsTrue(task.TaskName.Contains(this.TestLib.Name, StringComparison.Ordinal));
        using var logger = new NoOpLogger();
        var symbols = task.Execute(logger);

        Assert.AreEqual(expectedSymbolCount, symbols.Count);
    }

    [TestMethod]
    public void FiresProgressNotificationsWithoutSpamming()
    {
        GenerateMockTextSection();
        GenerateMockRDataSection();
        this.TestLib!.MarkFullyConstructed();

        var mockProgress = new Mock<IProgress<SessionTaskProgress>>();
        mockProgress.Setup(p => p.Report(It.IsAny<SessionTaskProgress>())).Verifiable();

        var task = new EnumerateSymbolsInLibSessionTask(this.SessionTaskParameters!,
                                                        CancellationToken.None,
                                                        mockProgress.Object,
                                                        this.TestLib);

        Assert.IsTrue(task.TaskName.Contains(this.TestLib.Name, StringComparison.Ordinal));
        using var logger = new NoOpLogger();
        var symbols = task.Execute(logger);

        // The mock text section has 60 RVA Ranges, and the mock rdata has 60 as well
        // so we should expect only 2 change notifications to fire for progress (once per 50)
        // to avoid it being spammy and killing perf.
        // Then we expect one additional progress report for the initial "Starting Up..."
        mockProgress.Verify(p => p.Report(It.IsAny<SessionTaskProgress>()), Times.Exactly(3));
    }

    private BinarySection GenerateMockTextSection()
    {
        var textSection = new BinarySection(this.SessionTaskParameters!.DataCache, ".text", size: 0x1000u, virtualSize: 0, rva: 0x4000u, fileAlignment: 0, sectionAlignment: 0, characteristics: DataSectionFlags.MemoryExecute);
        var textMnCG = new COFFGroup(this.SessionTaskParameters.DataCache, ".text$mn", size: 0x1000u, rva: 0x4000u, fileAlignment: 0, sectionAlignment: 0, characteristics: DataSectionFlags.MemoryExecute);

        var textRVARanges = new List<RVARange>();
        for (uint i = 0; i < 60; i++)
        {
            // Generate ranges like (4000, 4010) then (4020, 4030)
            textRVARanges.Add(new RVARange(0x4000u + i * 20, 0x4000u + i * 20 + 10));
        }
        var textSectionContribution = this.TestLib!.GetOrCreateSectionContribution(textSection);
        textSectionContribution.AddRVARanges(textRVARanges);
        textSectionContribution.MarkFullyConstructed();
        var textMnCGContribution = this.TestLib.GetOrCreateCOFFGroupContribution(textMnCG);
        textMnCGContribution.AddRVARanges(textSectionContribution.RVARanges);
        textMnCGContribution.MarkFullyConstructed();
        textMnCG.MarkFullyConstructed();
        textSection.MarkFullyConstructed();

        return textSection;
    }

    private BinarySection GenerateMockRDataSection()
    {
        var rdataSection = new BinarySection(this.SessionTaskParameters!.DataCache, ".rdata", size: 0x1000u, virtualSize: 0, rva: 0x1000u, fileAlignment: 0, sectionAlignment: 0, characteristics: DataSectionFlags.MemoryRead);
        var rdataZzCG = new COFFGroup(this.SessionTaskParameters.DataCache, ".rdata$zz", size: 0x1000u, rva: 0x1000u, fileAlignment: 0, sectionAlignment: 0, characteristics: DataSectionFlags.MemoryRead);

        var rdataRVARanges = new List<RVARange>();
        for (uint i = 0; i < 60; i++)
        {
            // Generate ranges like (1000, 1010) then (1020, 1030)
            rdataRVARanges.Add(new RVARange(0x1000u + i * 20, 0x1000u + i * 20 + 10));
        }
        var rdataSectionContribution = this.TestLib!.GetOrCreateSectionContribution(rdataSection);
        rdataSectionContribution.AddRVARanges(rdataRVARanges);
        rdataSectionContribution.MarkFullyConstructed();
        var rdataZzCGContribution = this.TestLib.GetOrCreateCOFFGroupContribution(rdataZzCG);
        rdataZzCGContribution.AddRVARanges(rdataSectionContribution.RVARanges);
        rdataZzCGContribution.MarkFullyConstructed();
        rdataZzCG.MarkFullyConstructed();
        rdataSection.MarkFullyConstructed();

        return rdataSection;
    }

    public void Dispose() => this.DataCache.Dispose();
}
