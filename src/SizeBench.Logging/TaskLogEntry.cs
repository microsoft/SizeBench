using System.Diagnostics;
using System.IO;

namespace SizeBench.Logging;

public sealed class TaskLogEntry : LogEntry
{
    public IEnumerable<LogEntry> Entries { get; }

    public Stopwatch Stopwatch { get; }

    public TaskLogEntry(IEnumerable<LogEntry> logEntries, Stopwatch stopwatch, string callingMember, string message, LogLevel logLevel)
        : base(callingMember, message, logLevel)
    {
        Debug.Assert(logEntries != null);
        this.Entries = logEntries;
        this.Stopwatch = stopwatch;
    }

    public override void AppendToTextWriter(TextWriter writer, int indentLevel)
    {
        writer.WriteLine($"{new string('\t', indentLevel)}{this.CallingMember} - {this.LogLevel} - {this.Message} (elapsed: {this.Stopwatch.Elapsed})");

        foreach (var logEntry in this.Entries)
        {
            logEntry.AppendToTextWriter(writer, indentLevel + 1);
        }
    }
}
