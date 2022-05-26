using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[TestClass]
public sealed class EnumerateSymbolsInSourceFileSessionTaskTests : IDisposable
{
    public TestContext? TestContext { get; set; }
    public CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

    private SingleBinaryDataGenerator Generator = new SingleBinaryDataGenerator();

    [TestInitialize]
    public void TestInitialize()
    {
        this.Generator = new SingleBinaryDataGenerator();
        this.Generator.MockSession.SetupGet(s => s.BytesPerWord).Returns(8);
        this.Generator.DataCache.PDataRVARange = new RVARange(0, 0);
        this.Generator.DataCache.PDataSymbolsByRVA = new SortedList<uint, PDataSymbol>();
        this.Generator.DataCache.XDataRVARanges = new RVARangeSet();
        this.Generator.DataCache.XDataSymbolsByRVA = new SortedList<uint, XDataSymbol>();
        this.Generator.DataCache.RsrcRVARange = new RVARange(0, 0);
        this.Generator.DataCache.RsrcSymbolsByRVA = new SortedList<uint, RsrcSymbolBase>();
        this.Generator.DataCache.OtherPESymbolsRVARanges = new RVARangeSet();
        this.Generator.DataCache.OtherPESymbolsByRVA = new SortedList<uint, ISymbol>();
    }

    [TestMethod]
    public void CanExecuteWithoutProgressReporting()
        => EnumerateSymbolsFromSourceFile(null);

    [TestMethod]
    public void CanEnumerateSymbolsFromEachSectionContributionWithinSourceFile() 
        => EnumerateSymbolsFromSourceFile(new Mock<IProgress<SessionTaskProgress>>().Object);

    private void EnumerateSymbolsFromSourceFile(IProgress<SessionTaskProgress>? progressReporter)
    {
        var nextSymIndexId = 0u;

        var expectedSymbolsInText = 0;
        foreach (var range in this.Generator.XHSourceFile.SectionContributionsByName[".text"].RVARanges)
        {
            var textSectionSymbols = new List<ValueTuple<ISymbol, uint>>();
            for (uint i = 0; i < range.VirtualSize / 2 /* because each symbol we generate is 2 bytes */; i++)
            {
                ISymbol symbolToFind = new SimpleFunctionCodeSymbol(this.Generator.SessionTaskParameters.DataCache, $"test .text symbol {i}", rva: range.RVAStart + (i * 2), size: 2, symIndexId: nextSymIndexId++);

                textSectionSymbols.Add(new ValueTuple<ISymbol, uint>(symbolToFind, i));
            }

            expectedSymbolsInText += textSectionSymbols.Count;
            this.Generator.DIAAdapter.SymbolsToFindByRVARange.Add(range, textSectionSymbols);
        }

        var expectedSymbolsInData = 0;
        foreach (var range in this.Generator.XHSourceFile.SectionContributionsByName[".data"].RVARanges)
        {
            var dataSectionSymbols = new List<(ISymbol, uint)>();
            for (uint i = 0; i < range.VirtualSize / 2 /* because each symbol we generate is 2 bytes */; i++)
            {
                ISymbol symbolToFind = new SimpleFunctionCodeSymbol(this.Generator.SessionTaskParameters.DataCache, $"test .data symbol {i}", rva: range.RVAStart + (i * 2), size: 2, symIndexId: nextSymIndexId++);

                dataSectionSymbols.Add(new ValueTuple<ISymbol, uint>(symbolToFind, i));
            }

            expectedSymbolsInData += dataSectionSymbols.Count;
            this.Generator.DIAAdapter.SymbolsToFindByRVARange.Add(range, dataSectionSymbols);
        }

        var expectedSymbolCount = expectedSymbolsInText + expectedSymbolsInData;

        var task = new EnumerateSymbolsInSourceFileSessionTask(this.Generator.SessionTaskParameters,
                                                               this.CancellationToken,
                                                               progressReporter,
                                                               this.Generator.XHSourceFile);

        Assert.IsTrue(task.TaskName.Contains(this.Generator.XHSourceFile.Name, StringComparison.Ordinal));
        using var logger = new NoOpLogger();
        var symbols = task.Execute(logger);

        Assert.AreEqual(expectedSymbolCount, symbols.Count);
    }

    public void Dispose() => this.Generator.Dispose();
}
