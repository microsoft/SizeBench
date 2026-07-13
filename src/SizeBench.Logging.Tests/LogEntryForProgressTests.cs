using System.IO;

namespace SizeBench.Logging.Tests;

[TestClass]
public class LogEntryForProgressTests
{
    [TestMethod]
    public void AppendToTextWriterContainsAllExpectedInformation()
    {
        var callingMember = "TestCallingMember";
        var message = "Starting...";
        var logLevel = LogLevel.Info;

        var entry = new LogEntryForProgress(callingMember, message, logLevel);
        using var writer = new StringWriter();
        Assert.AreEqual(String.Empty, writer.ToString());
        entry.AppendToTextWriter(writer, indentLevel: 3);

        var output = writer.ToString();

        var linesOfOutput = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(1, linesOfOutput);

        Assert.StartsWith("\t\t\t", output, StringComparison.Ordinal);
        Assert.Contains(callingMember, output, StringComparison.Ordinal);
        Assert.Contains(message, output, StringComparison.Ordinal);
        Assert.Contains(logLevel.ToString(), output, StringComparison.Ordinal);

        writer.GetStringBuilder().Clear();
        entry.UpdateProgress("5% done");
        entry.AppendToTextWriter(writer, indentLevel: 3);

        output = writer.ToString();
        linesOfOutput = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(1, linesOfOutput);

        Assert.StartsWith("\t\t\t", output, StringComparison.Ordinal);
        Assert.Contains(callingMember, output, StringComparison.Ordinal);
        Assert.Contains("5% done", output, StringComparison.Ordinal);
        Assert.IsFalse(output.Contains(message, StringComparison.Ordinal)); // We've moved on in progress, should not still contain the original string
        Assert.Contains(logLevel.ToString(), output, StringComparison.Ordinal);
    }
}
