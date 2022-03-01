using System.Runtime.CompilerServices;

namespace SizeBench.Logging;

public interface ILogger : IDisposable
{
    string Name { get; }
    SynchronizationContext? SynchronizationContext { get; }
    IEnumerable<LogEntry> Entries { get; }

    void Log(string message, LogLevel logLevel = LogLevel.Info, [CallerMemberName] string callerMemberName = "");

    void LogException(string message, Exception ex, [CallerMemberName] string callerMemberName = "");

    LogEntryForProgress StartProgressLogEntry(string initialProgressMessage, [CallerMemberName] string callerMemberName = "");

    ILogger StartTaskLog(string taskName, [CallerMemberName] string callingMember = "");
}
