using System.IO;

namespace SizeBench.Logging.Tests;

[TestClass]
public class LogEntryTests
{
    [TestMethod]
    public void LogEntryPassesThrough()
    {
        var entry = new LogEntry("TestCallingMember", "test message", LogLevel.Warning);
        Assert.AreEqual("TestCallingMember", entry.CallingMember);
        Assert.AreEqual("test message", entry.Message);
        Assert.AreEqual(LogLevel.Warning, entry.LogLevel);
    }

    [TestMethod]
    public void AppendToTextWriterContainsAllInformationOnOneLine()
    {
        var callingMember = "TestCallingMember";
        var message = "test message";
        var logLevel = LogLevel.Info;

        var entry = new LogEntry(callingMember, message, logLevel);
        using var writer = new StringWriter();
        Assert.AreEqual(String.Empty, writer.ToString());
        entry.AppendToTextWriter(writer, 2);

        var output = writer.ToString();

        var linesOfOutput = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(1, linesOfOutput);

        Assert.StartsWith("\t\t", output, StringComparison.Ordinal);
        Assert.Contains(callingMember, output, StringComparison.Ordinal);
        Assert.Contains(message, output, StringComparison.Ordinal);
        Assert.Contains(logLevel.ToString(), output, StringComparison.Ordinal);
    }
}
