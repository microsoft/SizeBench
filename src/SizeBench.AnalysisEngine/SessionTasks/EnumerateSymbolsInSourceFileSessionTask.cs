using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal class EnumerateSymbolsInSourceFileSessionTask : SessionTask<List<ISymbol>>
{
    private readonly SessionTaskParameters _sessionTaskParameters;
    private readonly SourceFile _sourceFile;

    public EnumerateSymbolsInSourceFileSessionTask(SessionTaskParameters parameters,
                                                   CancellationToken token,
                                                   IProgress<SessionTaskProgress>? progress,
                                                   SourceFile sourceFile)
        : base(parameters, progress, token)
    {
        this.TaskName = $"Enumerate Symbols in Source File '{sourceFile.Name}'";
        this._sessionTaskParameters = parameters;
        this._sourceFile = sourceFile;
    }

    protected override List<ISymbol> ExecuteCore(ILogger logger)
    {
        var symbolsEnumerated = new List<ISymbol>();
        const int loggerOutputVelocity = 50;
        var nextLoggerOutput = loggerOutputVelocity;
        uint numRangesEnumerated = 0;

        var totalRVARanges = this._sourceFile.SectionContributions.Values.Sum(sc => sc.RVARanges.Count);

        foreach (var sectionContribution in this._sourceFile.SectionContributions.Values)
        {
            foreach (var rvaRange in sectionContribution.RVARanges)
            {
                this.CancellationToken.ThrowIfCancellationRequested();
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
