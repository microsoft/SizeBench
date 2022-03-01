using System.Runtime.CompilerServices;

namespace SizeBench.Logging;

public sealed class NoOpLogger : ILogger
{
    public IEnumerable<LogEntry> Entries => Enumerable.Empty<LogEntry>();

    public string Name { get; set; } = String.Empty;

    public SynchronizationContext? SynchronizationContext => null;

    public ILogger StartTaskLog(string taskName, [CallerMemberName] string callingMember = "") => new NoOpLogger();

    public void Log(string message, LogLevel logLevel = LogLevel.Info, [CallerMemberName] string callerMemberName = "")
    {
    }

    public void LogException(string message, Exception ex, [CallerMemberName] string callerMemberName = "")
    {
    }

    public LogEntryForProgress StartProgressLogEntry(string initialProgressMessage, [CallerMemberName] string callerMemberName = "") => new LogEntryForProgress(callerMemberName, initialProgressMessage, LogLevel.Info);

    #region IDisposable Support

    public void Dispose() { }

    #endregion
}
