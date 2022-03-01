using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SizeBench.Logging;

public class LogEntry
{
    public LogEntry(string callingMember, string message, LogLevel logLevel)
    {
        this.CallingMember = callingMember;
        this.Message = message;
        this.LogLevel = logLevel;
    }

    public string CallingMember { get; }
    public string Message { get; protected set; }
    public LogLevel LogLevel { get; }

    public virtual void AppendToTextWriter([DisallowNull] TextWriter writer, int indentLevel)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine($"{new string('\t', indentLevel)}{this.CallingMember} - {this.LogLevel} - {this.Message}");
    }
}
