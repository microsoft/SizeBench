using System.IO;
using System.Runtime.CompilerServices;
using SizeBench.Logging;

namespace SizeBench.TestInfrastructure;

public sealed class TestNoOpApplicationLogger : IApplicationLogger
{
    public IEnumerable<LogEntry> Entries => throw new NotImplementedException();

    public string Name { get; set; } = String.Empty;

    public IEnumerable<ILogger> SessionLogs { get; set; } = new List<ILogger>();

    public SynchronizationContext SynchronizationContext => throw new NotImplementedException();

    public ILogger CreateSessionLog(string sessionName)
    {
        var sessionLog = new NoOpLogger() { Name = sessionName };
        ((IList<ILogger>)this.SessionLogs).Add(sessionLog);
        return sessionLog;
    }

    public ILogger StartTaskLog(string taskName, [CallerMemberName] string callingMember = "")
        => new NoOpLogger();

    public void Log(string message, LogLevel logLevel = LogLevel.Info, [CallerMemberName] string callerMemberName = "")
    {
    }

    public void LogException(string message, Exception ex, [CallerMemberName] string callerMemberName = "")
    {
    }

    public LogEntryForProgress StartProgressLogEntry(string initialProgressMessage, [CallerMemberName] string callerMemberName = "")
        => new LogEntryForProgress(callerMemberName, initialProgressMessage, LogLevel.Info);

    public void WriteLog(TextWriter writer)
    {
    }

    #region IDisposable Support
    private bool IsDisposed; // To detect redundant calls

    void Dispose(bool disposing)
    {
        if (!this.IsDisposed)
        {
            if (disposing)
            {
                foreach (var sessionLog in this.SessionLogs)
                {
                    sessionLog.Dispose();
                }
            }

            this.IsDisposed = true;
        }
    }

    ~TestNoOpApplicationLogger()
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
    #endregion
}
