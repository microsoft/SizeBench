using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class EnumerateAnnotationsSessionTask : SessionTask<List<AnnotationSymbol>>
{
    public EnumerateAnnotationsSessionTask(SessionTaskParameters parameters,
                                           CancellationToken token,
                                           IProgress<SessionTaskProgress>? progressReporter)
        : base(parameters, progressReporter, token)
    {
        this.TaskName = "Enumerate Annotations";
    }

    protected override List<AnnotationSymbol> ExecuteCore(ILogger logger)
    {
        if (this.DataCache.AllAnnotations != null)
        {
            logger.Log("Found annotations in the cache, re-using them, hooray!");
            return this.DataCache.AllAnnotations;
        }

        ReportProgress("Enumerating all annotations in the binary...", 0, null);

        var allAnnotations = this.DIAAdapter.FindAllAnnotations(logger, this.CancellationToken);

        // If the DIA Adapter didn't already put this into the cache, do it here
        if (this.DataCache.AllAnnotations is null)
        {
            this.DataCache.AllAnnotations = allAnnotations.ToList();
        }

        return this.DataCache.AllAnnotations;
    }
}
