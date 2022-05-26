using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal class EnumerateSymbolsInLibSessionTask : SessionTask<List<ISymbol>>
{
    private readonly SessionTaskParameters _sessionTaskParameters;
    private readonly Library _lib;

    public EnumerateSymbolsInLibSessionTask(SessionTaskParameters parameters,
                                            CancellationToken token,
                                            IProgress<SessionTaskProgress>? progress,
                                            Library lib)
        : base(parameters, progress, token)
    {
        this.TaskName = $"Enumerate Symbols in Lib '{lib.Name}'";
        this._sessionTaskParameters = parameters;
        this._lib = lib;
    }

    protected override List<ISymbol> ExecuteCore(ILogger logger)
    {
        var symbolsEnumerated = new List<ISymbol>();
        const int loggerOutputVelocity = 50;
        var nextLoggerOutput = loggerOutputVelocity;
        uint numRangesEnumerated = 0;

        var totalRVARanges = this._lib.SectionContributions.Values.Sum(sc => sc.RVARanges.Count);

        foreach (var sectionContribution in this._lib.SectionContributions.Values)
        {
            foreach (var rvaRange in sectionContribution.RVARanges)
            {
                if (numRangesEnumerated > nextLoggerOutput)
                {
                    ReportProgress($"Parsed {numRangesEnumerated}/{totalRVARanges} RVA Ranges.  So far, {symbolsEnumerated.Count} symbols have been found.", numRangesEnumerated, (uint)totalRVARanges);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                var enumRVARange = new EnumerateSymbolsInRVARangeSessionTask(this._sessionTaskParameters,
                                                                             this.CancellationToken,
                                                                             this.ProgressReporter,
                                                                             rvaRange);
                symbolsEnumerated.AddRange(enumRVARange.Execute(logger, shouldReportInitialProgress: false));

                numRangesEnumerated++;
            }
        }

        return symbolsEnumerated;
    }
}
