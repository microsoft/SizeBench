using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SizeBench.Logging;

public sealed class Logger : ILogger
{
    private readonly ApplicationLogger? _parentApplicationLogger;
    public string Name { get; }
    public SynchronizationContext? SynchronizationContext { get; }
    private readonly IList<LogEntry> _entries;
    public IEnumerable<LogEntry> Entries => this._entries;

    private readonly IList<LogEntry> _pendingEntries;
    private readonly Stopwatch? _stopwatch;

    public Logger(string name,
                  IList<LogEntry> logEntries,
                  IList<LogEntry> pendingEntries,
                  SynchronizationContext? synchronizationContext,
                  ApplicationLogger? applicationLogger)
    {
        this._parentApplicationLogger = applicationLogger;
        this.Name = name;
        this.SynchronizationContext = synchronizationContext;
        this._entries = logEntries;
        this._pendingEntries = pendingEntries;
    }

    public Logger(string name,
                  Stopwatch stopwatch,
                  IList<LogEntry> logEntries,
                  IList<LogEntry> pendingEntries,
                  SynchronizationContext? synchronizationContext,
                  ApplicationLogger? applicationLogger)
        : this(name, logEntries, pendingEntries, synchronizationContext, applicationLogger)
    {
        this._stopwatch = stopwatch;
    }

    public void Log(string message, LogLevel logLevel = LogLevel.Info, [CallerMemberName] string callerMemberName = "")
    {
        if (this.IsDisposed)
        {
            throw new ObjectDisposedException(this.Name);
        }

        var logEntry = new LogEntry(callerMemberName, message, logLevel);
        LogWithSynchronizationContext(logEntry);
    }

    public void LogException(string message, Exception ex, [CallerMemberName] string callerMemberName = "")
    {
        if (this.IsDisposed)
        {
            throw new ObjectDisposedException(this.Name);
        }

        var logEntry = new LogExceptionEntry(callerMemberName, message, ex);
        LogWithSynchronizationContext(logEntry);
    }

    public LogEntryForProgress StartProgressLogEntry(string initialProgressMessage, [CallerMemberName] string callerMemberName = "")
    {
        if (this.IsDisposed)
        {
            throw new ObjectDisposedException(this.Name);
        }

        var logEntry = new LogEntryForProgress(callerMemberName, initialProgressMessage, LogLevel.Info);
        LogWithSynchronizationContext(logEntry);
        return logEntry;
    }

    private void LogWithSynchronizationContext(LogEntry logEntry)
    {
        if (this.SynchronizationContext != null && this.SynchronizationContext != SynchronizationContext.Current)
        {
            lock (this._pendingEntries)
            {
                this._pendingEntries.Add(logEntry);
            }

            this.SynchronizationContext.Post((o) =>
            {
                lock (this._pendingEntries)
                {
                    this._pendingEntries.Remove(logEntry);
                }

                this._entries.Add(logEntry);
            }, null);
        }
        else
        {
            this._entries.Add(logEntry);
        }
    }

    private ILogger StartTaskLogCommon(string taskName, string callingMember)
    {
        if (this.IsDisposed)
        {
            throw new ObjectDisposedException(this.Name);
        }

        IList<LogEntry> taskLogEntries;
        IList<LogEntry> pendingTaskLogEntries;
        if (this.SynchronizationContext is null)
        {
            taskLogEntries = new List<LogEntry>();
            pendingTaskLogEntries = new List<LogEntry>();
        }
        else
        {
            taskLogEntries = new ObservableCollection<LogEntry>();
            pendingTaskLogEntries = new List<LogEntry>();
        }

        var taskStopwatch = new Stopwatch();
        taskStopwatch.Start();

        var newEntry = new TaskLogEntry(taskLogEntries, taskStopwatch, callingMember, taskName, LogLevel.Info);
        if (this.SynchronizationContext != null && this.SynchronizationContext != SynchronizationContext.Current)
        {
            this.SynchronizationContext.Post((o) => this._entries.Add(newEntry), null);
        }
        else
        {
            this._entries.Add(newEntry);
        }

        var taskLog = new Logger(taskName, taskStopwatch, taskLogEntries, pendingTaskLogEntries, this.SynchronizationContext, null);

        return taskLog;
    }

    public ILogger StartTaskLog(string taskName, [CallerMemberName] string callingMember = "") => StartTaskLogCommon(taskName, callingMember);

    #region IDisposable Support

    public bool IsDisposed { get; private set; } // To detect redundant calls

    private void Dispose(bool disposing)
    {
        if (!this.IsDisposed)
        {
            this._stopwatch?.Stop();

            if (disposing)
            {
                this._parentApplicationLogger?.RemoveSessionLog(this);
                // Despite the application logger being IDisposable, we don't want to dispose it here - it has a lifetime that explicitly exceeds the SessionLoggers that it creates.
            }

            this.IsDisposed = true;
        }
    }

    ~Logger()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(false);
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion IDisposable Support
}
