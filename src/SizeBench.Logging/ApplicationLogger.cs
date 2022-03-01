using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace SizeBench.Logging;

public sealed class ApplicationLogger : IApplicationLogger
{
    public string Name { get; }
    public SynchronizationContext? SynchronizationContext { get; }
    private readonly IList<Logger> _sessionLogs;
    public IEnumerable<ILogger> SessionLogs => this._sessionLogs;

    private readonly IList<LogEntry> _entries;
    public IEnumerable<LogEntry> Entries => this._entries;

    private readonly IList<LogEntry> _pendingEntries;

    public ApplicationLogger(string name, SynchronizationContext? synchronizationContext)
    {
        this.Name = name;
        this.SynchronizationContext = synchronizationContext;
        if (this.SynchronizationContext is null)
        {
            this._entries = new List<LogEntry>();
            this._pendingEntries = new List<LogEntry>();
            this._sessionLogs = new List<Logger>();
        }
        else
        {
            this._entries = new ObservableCollection<LogEntry>();
            this._pendingEntries = new List<LogEntry>();
            this._sessionLogs = new ObservableCollection<Logger>();
        }
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

    public ILogger CreateSessionLog(string sessionName)
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

        var sessionLog = new Logger(sessionName, taskLogEntries, pendingTaskLogEntries, this.SynchronizationContext, this);
        this._sessionLogs.Add(sessionLog);
        return sessionLog;
    }

    internal void RemoveSessionLog(Logger sessionLogger) => this._sessionLogs.Remove(sessionLogger);

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
        this._entries.Add(newEntry);

        var taskLog = new Logger(taskName, taskStopwatch, taskLogEntries, pendingTaskLogEntries, this.SynchronizationContext, null);

        return taskLog;
    }

    public ILogger StartTaskLog(string taskName, [CallerMemberName] string callingMember = "") => StartTaskLogCommon(taskName, callingMember);

    public void WriteLog(TextWriter writer)
    {
        if (this.IsDisposed)
        {
            throw new ObjectDisposedException(this.Name);
        }

        ArgumentNullException.ThrowIfNull(writer);

        var indentLevel = 0;
        foreach (var entry in this.Entries)
        {
            entry.AppendToTextWriter(writer, indentLevel);
        }
        foreach (var sessionLog in this.SessionLogs)
        {
            writer.WriteLine($"{new string('\t', indentLevel)}Log for - {sessionLog.Name}");
            indentLevel++;
            foreach (var entry in sessionLog.Entries)
            {
                entry.AppendToTextWriter(writer, indentLevel);
            }
            indentLevel--;
        }
    }

    #region IDisposable Support

    public bool IsDisposed { get; private set; }

    private void Dispose(bool disposing)
    {
        if (!this.IsDisposed)
        {
            if (disposing)
            {
                var toDispose = this.SessionLogs.ToList(); // Create a copy so we can enumerate it while the disposals modify the SessionLogs collection
                foreach (var sessionLogger in toDispose)
                {
                    sessionLogger.Dispose();
                }
            }

            this.IsDisposed = true;
        }
    }

    // Testing a destrucor is hard since it's at the whim of GC
    [ExcludeFromCodeCoverage]
    ~ApplicationLogger()
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
