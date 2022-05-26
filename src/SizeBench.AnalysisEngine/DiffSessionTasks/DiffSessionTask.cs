using System.Diagnostics;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DiffSessionTasks;

internal class DiffSessionTaskParameters
{
    public IDiffSession DiffSession { get; }
    public DiffSessionDataCache DataCache { get; }

    public DiffSessionTaskParameters(IDiffSession diffSession, DiffSessionDataCache dataCache)
    {
        this.DiffSession = diffSession;
        this.DataCache = dataCache;
    }
}

[DebuggerDisplay("Diff Session Task, Name = {TaskName}")]
internal abstract class DiffSessionTask<T>
{
    protected readonly CancellationToken CancellationToken;
    protected readonly IDiffSession DiffSession;
    protected readonly IProgress<SessionTaskProgress>? ProgressReporter;
    protected readonly DiffSessionDataCache DataCache;
    protected readonly DiffSessionTaskParameters _diffSessionTaskParameters;
    private LogEntryForProgress? _logEntryForProgress;
    public string TaskName { get; protected set; } = String.Empty;

    public DiffSessionTask(DiffSessionTaskParameters parameters,
                           IProgress<SessionTaskProgress>? progress,
                           CancellationToken token)
    {
        this._diffSessionTaskParameters = parameters;
        this.DiffSession = parameters.DiffSession;
        this.DataCache = parameters.DataCache;
        this.CancellationToken = token;
        this.ProgressReporter = progress;
    }

    public Task<T> ExecuteAsync(ILogger sessionLogger, bool shouldReportInitialProgress = true)
    {
        Debug.Assert(!String.IsNullOrEmpty(this.TaskName));
        var taskLogger = sessionLogger.StartTaskLog(this.TaskName);
        var logDisposalQueuedUp = false;
        try
        {
            this._logEntryForProgress = taskLogger.StartProgressLogEntry("Starting Up...");

            if (shouldReportInitialProgress)
            {
                ReportProgress("Starting Up...", 0, null);
            }

            // Normally we'd do a "using" statement on the taskLogger, but that doesn't work with async code
            // since the Dispose will happen before all the async code runs (it'll run up to the first continuation
            // point only, then Dispose).  So, we go through some pains here to ensure the log is
            // disposed at the right time (after the task, or if the task fails to start).
            // In .NET 6 perhaps we could make the logger IAsyncDisposable but this code predates that.
            var returnVal = ExecuteCoreAsync(taskLogger);
            returnVal.ContinueWith((previousTask) => taskLogger.Dispose());
            logDisposalQueuedUp = true;

            return returnVal;
        }
        finally
        {
            if (!logDisposalQueuedUp)
            {
                taskLogger.Dispose();
            }
        }
    }

    protected abstract Task<T> ExecuteCoreAsync(ILogger logger);

    protected void ReportProgress(string message, uint itemsComplete, uint? itemsTotal)
    {
        this.ProgressReporter?.Report(new SessionTaskProgress(message, itemsComplete, itemsTotal));
        this._logEntryForProgress!.UpdateProgress(message);
    }
}
