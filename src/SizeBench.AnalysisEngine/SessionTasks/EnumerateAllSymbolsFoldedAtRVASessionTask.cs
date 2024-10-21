using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class EnumerateAllSymbolsFoldedAtRVASessionTask : SessionTask<List<ISymbol>>
{
    private readonly SessionTaskParameters _sessionTaskParameters;
    private readonly uint RVA;

    public EnumerateAllSymbolsFoldedAtRVASessionTask(SessionTaskParameters parameters,
                                                     uint rva,
                                                     IProgress<SessionTaskProgress>? progressReporter,
                                                     CancellationToken token)
        : base(parameters, progressReporter, token)
    {
        this.TaskName = $"Enumerate All Symbols Folded At RVA 0x{rva:X}";
        this.RVA = rva;
        this._sessionTaskParameters = parameters;
    }

    protected override List<ISymbol> ExecuteCore(ILogger logger)
    {
        ReportProgress($"Discovering all symbols that are folded at RVA 0x{this.RVA:X}", 0, null);

        var primarySymbol = new LoadSymbolByRVASessionTask(this._sessionTaskParameters, this.RVA, this.ProgressReporter, this.CancellationToken).Execute(logger, shouldReportProgress: false);

        if (primarySymbol is null)
        {
            return new List<ISymbol>();
        }

        if (this.DataCache.AllCanonicalNames!.TryGetValue(primarySymbol.RVA, out var nameCanonicalization))
        {
            var list = new List<ISymbol>();
            foreach (var canonicalNameEntry in nameCanonicalization.NamesBySymIndexID)
            {
                this.CancellationToken.ThrowIfCancellationRequested();
                list.Add(this.DIAAdapter.FindSymbolBySymIndexId<ISymbol>(canonicalNameEntry.symIndexId, this.CancellationToken));
            }
            return list;
        }
        else
        {
            // There is not a NameCanonicalization for this RVA, so there must just be one symbol here.
            return new List<ISymbol>() { primarySymbol };
        }
    }
}
