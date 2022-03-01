using System.IO;

namespace SizeBench.Logging;

public sealed class LogExceptionEntry : LogEntry
{
    public Exception Exception { get; }
    private readonly string _originalMessage;

    public LogExceptionEntry(string callingMember, string message, Exception ex)
        : base(callingMember, ex.GetFormattedTextForLogging(message, Environment.NewLine), LogLevel.Error)
    {
        this.Exception = ex;
        this._originalMessage = message;
    }

    public override void AppendToTextWriter(TextWriter writer, int indentLevel)
    {
        writer.WriteLine($"{new string('\t', indentLevel)}{this.CallingMember} - {this.LogLevel} - {this._originalMessage}");
        writer.WriteLine(this.Exception.GetFormattedTextForLogging(String.Empty, Environment.NewLine, indentLevel + 1));
    }
}
