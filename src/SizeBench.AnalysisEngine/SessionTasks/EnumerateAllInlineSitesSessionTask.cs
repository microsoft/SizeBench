using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class EnumerateAllInlineSitesSessionTask : SessionTask<IReadOnlyList<InlineSiteSymbol>>
{
    public EnumerateAllInlineSitesSessionTask(SessionTaskParameters parameters,
                                              IProgress<SessionTaskProgress>? progressReporter,
                                              CancellationToken token)
        : base(parameters, progressReporter, token)
    {
        this.TaskName = $"Enumerate All Inline Sites in the binary";
    }

    protected override IReadOnlyList<InlineSiteSymbol> ExecuteCore(ILogger logger)
    {
        ReportProgress($"Discovering all inline sites within the binary", 0, null);

        return this.DIAAdapter.FindAllInlineSites(this.CancellationToken);
    }
}
