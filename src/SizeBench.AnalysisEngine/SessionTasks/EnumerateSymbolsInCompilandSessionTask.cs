using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal class EnumerateSymbolsInCompilandSessionTask : SessionTask<List<ISymbol>>
{
    private readonly SessionTaskParameters _sessionTaskParameters;
    private readonly Compiland _compiland;
    private readonly SymbolEnumerationOptions _options;

    public EnumerateSymbolsInCompilandSessionTask(SessionTaskParameters parameters,
                                                  CancellationToken token,
                                                  IProgress<SessionTaskProgress>? progress,
                                                  Compiland compiland,
                                                  SymbolEnumerationOptions? options = null)
        : base(parameters, progress, token)
    {
        this.TaskName = $"Enumerate Symbols in Compiland '{compiland.Name}'";
        this._sessionTaskParameters = parameters;
        this._compiland = compiland;
        this._options = options ?? new SymbolEnumerationOptions();
    }

    protected override List<ISymbol> ExecuteCore(ILogger logger)
    {
        var symbolsEnumerated = new List<ISymbol>();
        const int loggerOutputVelocity = 50;
        var nextLoggerOutput = loggerOutputVelocity;
        uint numRangesEnumerated = 0;

        var totalRVARanges = this._compiland.SectionContributions.Values.Sum(sc => sc.RVARanges.Count);

        foreach (var sectionContribution in this._compiland.SectionContributions.Values)
        {
            // If we're only enumerating code symbols, and this section does not contain code, move along
            if (this._options.OnlyCodeSymbols && (sectionContribution.BinarySection.Characteristics & SectionCharacteristics.MemExecute) != SectionCharacteristics.MemExecute)
            {
                continue;
            }

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

                // We skip progress reporting on each RVA Range since it can be very noisy in the logs and doesn't have a lot of value.
                symbolsEnumerated.AddRange(enumRVARange.Execute(logger, shouldReportInitialProgress: false, shouldReportProgress: false));

                numRangesEnumerated++;
            }
        }

        return symbolsEnumerated;
    }
}
