using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal class EnumerateSymbolsInContributionSessionTask : SessionTask<List<ISymbol>>
{
    private readonly SessionTaskParameters _sessionTaskParameters;
    private readonly Contribution _contribution;

    public EnumerateSymbolsInContributionSessionTask(SessionTaskParameters parameters,
                                                     CancellationToken token,
                                                     IProgress<SessionTaskProgress>? progress,
                                                     Contribution contribution)
        : base(parameters, progress, token)
    {
        this.TaskName = $"Enumerate Symbols in Contribution '{contribution.Name}'";
        this._sessionTaskParameters = parameters;
        this._contribution = contribution;
    }

    protected override List<ISymbol> ExecuteCore(ILogger logger)
    {
        var symbolsEnumerated = new List<ISymbol>();
        const int loggerOutputVelocity = 50;
        var nextLoggerOutput = loggerOutputVelocity;
        uint numRangesEnumerated = 0;

        var rvaRanges = this._contribution.RVARanges;

        foreach (var rvaRange in rvaRanges)
        {
            if (numRangesEnumerated > nextLoggerOutput)
            {
                ReportProgress($"Parsed {numRangesEnumerated:N0}/{rvaRanges.Count:N0} RVA Ranges.  So far, {symbolsEnumerated.Count:N0} symbols have been found.", numRangesEnumerated, (uint)rvaRanges.Count);
                nextLoggerOutput += loggerOutputVelocity;
            }

            var enumRVARange = new EnumerateSymbolsInRVARangeSessionTask(this._sessionTaskParameters,
                                                                         this.CancellationToken,
                                                                         this.ProgressReporter,
                                                                         rvaRange);
            symbolsEnumerated.AddRange(enumRVARange.Execute(logger, shouldReportInitialProgress: false));

            numRangesEnumerated++;
        }

        return symbolsEnumerated;
    }
}
