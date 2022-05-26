using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DiffSessionTasks;

internal class EnumerateSymbolDiffsBetweenTwoSymbolListsSessionTask : DiffSessionTask<List<SymbolDiff>>
{
    private readonly Func<ILogger, Task<IReadOnlyList<ISymbol>?>> _beforeSymbolsTaskFactory;
    private readonly Func<ILogger, Task<IReadOnlyList<ISymbol>?>> _afterSymbolsTaskFactory;

    public EnumerateSymbolDiffsBetweenTwoSymbolListsSessionTask(DiffSessionTaskParameters parameters,
                                                                Func<ILogger, Task<IReadOnlyList<ISymbol>?>> beforeSymbolsTaskFactory,
                                                                Func<ILogger, Task<IReadOnlyList<ISymbol>?>> afterSymbolsTaskFactory,
                                                                string nameOfThingBeingEnumerated,
                                                                IProgress<SessionTaskProgress>? progress,
                                                                CancellationToken token)
                                                                : base(parameters, progress, token)
    {
        this.TaskName = $"Enumerate Symbol Diffs in {nameOfThingBeingEnumerated}";
        this._beforeSymbolsTaskFactory = beforeSymbolsTaskFactory;
        this._afterSymbolsTaskFactory = afterSymbolsTaskFactory;
    }

    protected override async Task<List<SymbolDiff>> ExecuteCoreAsync(ILogger logger)
    {

        ReportProgress("Enumerating symbols in 'before' and 'after'", 0, null);

        List<ISymbol> beforeSymbols;
        List<ISymbol> afterSymbols;

        using (var beforeAndAfterLog = logger.StartTaskLog("Enumerating symbols in 'before' and 'after'"))
        {
            var beforeTask = this._beforeSymbolsTaskFactory(beforeAndAfterLog);
            var afterTask = this._afterSymbolsTaskFactory(beforeAndAfterLog);

            var results = await Task.WhenAll(beforeTask, afterTask).WaitAsync(this.CancellationToken).ConfigureAwait(true);

            beforeSymbols = results[0]?.ToList() ?? new List<ISymbol>();
            afterSymbols = results[1]?.ToList() ?? new List<ISymbol>();
        }

        var symbolDiffs = new List<SymbolDiff>();

        uint beforeSymbolsParsed = 0;
        uint afterSymbolsParsed = 0;
        const int loggerOutputVelocity = 100;
        var nextLoggerOutput = loggerOutputVelocity;

        var beforeSymbolsCount = (uint)beforeSymbols.Count;
        var afterSymbolsCount = (uint)afterSymbols.Count;
        var totalSymbolsToDiff = beforeSymbolsCount + afterSymbolsCount;

        // We'll group the symbols by type, to only attempt comparisons of symbols of the same type.
        // When comparing large symbol lists in large binaries this partitions the problem up
        // and speeds things up a ton.
        var groupedBeforeSymbols = beforeSymbols.GroupBy(sym => sym.SymbolComparisonClass);
        var groupedAfterSymbols = afterSymbols.GroupBy(sym => sym.SymbolComparisonClass);

        using (var beforeSymbolLoopLog = logger.StartTaskLog("Looping over 'before' symbols"))
        {
            foreach (var beforeSymbolTypeGroup in groupedBeforeSymbols)
            {
                var beforeSymbolsOfThisType = beforeSymbolTypeGroup.ToList();
                var afterGroup = groupedAfterSymbols.FirstOrDefault(group => group.Key == beforeSymbolTypeGroup.Key);
                var afterSymbolsOfThisType = afterGroup?.ToList() ?? new List<ISymbol>();
                foreach (var beforeSymbol in beforeSymbolsOfThisType)
                {
                    this.CancellationToken.ThrowIfCancellationRequested();

                    if (beforeSymbolsParsed >= nextLoggerOutput)
                    {
                        ReportProgress($"Parsed {beforeSymbolsParsed}/{beforeSymbolsCount} 'before' symbols and {afterSymbolsParsed}/{afterSymbolsCount} 'after' symbols into diffs.", beforeSymbolsParsed + afterSymbolsParsed, totalSymbolsToDiff);
                        nextLoggerOutput += loggerOutputVelocity;
                    }

                    beforeSymbolsParsed++;

                    // Find the matching symbol in 'after' - the rules for 'matching' are complex and depend on the symbol type so each
                    // symbol type implements IsVeryLikelyTheSameAs independently.
                    //
                    // By removing the found matching symbol from the afterSymbols list, we gain two benefits - one is that each subsequent
                    // loop of this foreach is faster by looking at less options, and second it means we tend to almost entirely exhaust the
                    // afterSymbols list before getting into the second loop below, avoiding almost a full pass.
                    var matchingAfterSymbol = afterSymbolsOfThisType.FirstOrDefault(beforeSymbol.IsVeryLikelyTheSameAs);
                    if (matchingAfterSymbol != null)
                    {
                        afterSymbolsOfThisType.Remove(matchingAfterSymbol); // Make the list smaller so the operation isn't a full two passes (one here, one in the loop below)
                        afterSymbols.Remove(matchingAfterSymbol);
                        afterSymbolsParsed++; // Because we removed one, let's keep the total marching towards "totalSymbolsToDiff"
                    }

                    symbolDiffs.Add(SymbolDiffFactory.CreateSymbolDiff(beforeSymbol, matchingAfterSymbol, this.DataCache));
                }
            }
        }

        nextLoggerOutput = loggerOutputVelocity;

        using (var afterSymbolLoopLog = logger.StartTaskLog("Looping over 'after' symbols"))
        {
            // Now catch any symbols that are in 'after' but weren't in 'before'
            foreach (var afterSymbol in afterSymbols)
            {
                this.CancellationToken.ThrowIfCancellationRequested();

                if (afterSymbolsParsed >= nextLoggerOutput)
                {
                    ReportProgress($"Parsed {afterSymbolsParsed}/{afterSymbolsCount} 'after' symbols into diffs.", afterSymbolsParsed + beforeSymbolsCount, totalSymbolsToDiff);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                afterSymbolsParsed++;

                // This one wasn't found in 'before' so it's new in the 'after'
                symbolDiffs.Add(SymbolDiffFactory.CreateSymbolDiff(null, afterSymbol, this.DataCache));
            }
        }

        // One final progress report so the log shows a nice summary at the end
        ReportProgress($"Parsed {beforeSymbolsCount} 'before' symbols and {afterSymbolsCount} 'after' symbols, generating  {symbolDiffs.Count} diffs.", totalSymbolsToDiff, totalSymbolsToDiff);

        logger.Log($"Finished enumerating {symbolDiffs.Count} symbol diffs.");

        return symbolDiffs;
    }
}
