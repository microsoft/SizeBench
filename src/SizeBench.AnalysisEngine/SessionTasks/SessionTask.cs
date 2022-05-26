using System.Diagnostics;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal class SessionTaskParameters
{
    public ISession Session { get; }
    public IDIAAdapter DIAAdapter { get; }
    public SessionDataCache DataCache { get; }

    public SessionTaskParameters(ISession session, IDIAAdapter dIAAdapter, SessionDataCache dataCache)
    {
        this.Session = session;
        this.DIAAdapter = dIAAdapter;
        this.DataCache = dataCache;
    }
}

[DebuggerDisplay("Session Task, Name = {TaskName}")]
internal abstract class SessionTask
{
    protected readonly CancellationToken CancellationToken;
    protected readonly ISession Session;
    protected readonly IDIAAdapter DIAAdapter;
    protected readonly IProgress<SessionTaskProgress>? ProgressReporter;
    protected readonly SessionDataCache DataCache;
    protected LogEntryForProgress? LogEntryForProgress;
    public string TaskName { get; set; } = String.Empty;
    public SessionTask(SessionTaskParameters parameters, IProgress<SessionTaskProgress>? progress, CancellationToken token)
    {
        this.Session = parameters.Session;
        this.DIAAdapter = parameters.DIAAdapter;
        this.DataCache = parameters.DataCache;
        this.CancellationToken = token;
        this.ProgressReporter = progress;
    }

    public void ExecuteWithoutResults(ILogger sessionLogger, bool shouldReportInitialProgress = true)
    {
        Debug.Assert(!String.IsNullOrEmpty(this.TaskName));
        using var taskLogger = sessionLogger.StartTaskLog(this.TaskName);
        this.LogEntryForProgress = taskLogger.StartProgressLogEntry("Starting Up...");

        if (shouldReportInitialProgress)
        {
            ReportProgress("Starting Up...", 0, null);
        }

        ExecuteCoreWithoutResults(taskLogger);
    }

    protected virtual void ExecuteCoreWithoutResults(ILogger logger)
    {
    }

    protected void ReportProgress(string message, uint itemsComplete, uint? itemsTotal)
    {
        this.ProgressReporter?.Report(new SessionTaskProgress(message, itemsComplete, itemsTotal));
        this.LogEntryForProgress?.UpdateProgress(message);
    }
}

internal abstract class SessionTask<T> : SessionTask
{
    public SessionTask(SessionTaskParameters parameters, IProgress<SessionTaskProgress>? progress, CancellationToken token)
        : base(parameters, progress, token)
    {
    }

    public T Execute(ILogger sessionLogger, bool shouldReportInitialProgress = true, bool shouldReportProgress = true)
    {
        Debug.Assert(!String.IsNullOrEmpty(this.TaskName));
        using var taskLogger = shouldReportProgress ?
                                        sessionLogger.StartTaskLog(this.TaskName) :
                                        new NoOpLogger();

        if (shouldReportProgress)
        {
            this.LogEntryForProgress = taskLogger.StartProgressLogEntry("Starting Up...");
        }

        if (shouldReportProgress && shouldReportInitialProgress)
        {
            ReportProgress("Starting Up...", 0, null);
        }

        return ExecuteCore(taskLogger);
    }

    protected abstract T ExecuteCore(ILogger logger);
}
