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
        Assert.AreEqual(1, linesOfOutput.Length);

        StringAssert.StartsWith(output, "\t\t\t", StringComparison.Ordinal);
        StringAssert.Contains(output, callingMember, StringComparison.Ordinal);
        StringAssert.Contains(output, message, StringComparison.Ordinal);
        StringAssert.Contains(output, logLevel.ToString(), StringComparison.Ordinal);

        writer.GetStringBuilder().Clear();
        entry.UpdateProgress("5% done");
        entry.AppendToTextWriter(writer, indentLevel: 3);

        output = writer.ToString();
        linesOfOutput = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(1, linesOfOutput.Length);

        StringAssert.StartsWith(output, "\t\t\t", StringComparison.Ordinal);
        StringAssert.Contains(output, callingMember, StringComparison.Ordinal);
        StringAssert.Contains(output, "5% done", StringComparison.Ordinal);
        Assert.IsFalse(output.Contains(message, StringComparison.Ordinal)); // We've moved on in progress, should not still contain the original string
        StringAssert.Contains(output, logLevel.ToString(), StringComparison.Ordinal);
    }
}
