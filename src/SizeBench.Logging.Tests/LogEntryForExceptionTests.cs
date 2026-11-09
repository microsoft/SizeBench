using System.IO;

namespace SizeBench.Logging.Tests;

[TestClass]
public class LogEntryForExceptionTests
{
    [TestMethod]
    public void AppendToTextWriterContainsAllExpectedInformation()
    {
        var callingMember = "TestCallingMember";
        var message = "test message";
        var innerException = new AccessViolationException("test inner exception");
        var exceptionToLog = new InvalidOperationException("test outer exception", innerException);

        var entry = new LogExceptionEntry(callingMember, message, exceptionToLog);
        using var writer = new StringWriter();
        Assert.AreEqual(String.Empty, writer.ToString());
        entry.AppendToTextWriter(writer, indentLevel: 2);

        var output = writer.ToString();

        var linesOfOutput = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in linesOfOutput)
        {
            Assert.StartsWith("\t\t", line, StringComparison.Ordinal);
        }

        // The first line should just contain the basics, not the specific exception details - thos are on lines
        // that follow.  Otherwise it's harder to read.
        Assert.Contains(callingMember, linesOfOutput[0], StringComparison.Ordinal);
        Assert.Contains(message, linesOfOutput[0], StringComparison.Ordinal);
        Assert.Contains(LogLevel.Error.ToString(), linesOfOutput[0], StringComparison.Ordinal);
        Assert.IsFalse(linesOfOutput[0].Contains("InvalidOperationException", StringComparison.Ordinal));
        Assert.IsFalse(linesOfOutput[0].Contains("AccessViolationException", StringComparison.Ordinal));
        Assert.IsFalse(linesOfOutput[0].Contains("HRESULT", StringComparison.Ordinal));

        // Exception should be indented one level beyond the main error, it's more readable that way
        // in a long log.
        Assert.Contains("\t\t\tSystem.InvalidOperationException: test outer exception", output, StringComparison.Ordinal);
        Assert.Contains("\t\t\tHRESULT: 0x80131509", output, StringComparison.Ordinal);

        // InnerException is indented one more level
        Assert.Contains("\t\t\t\tSystem.AccessViolationException: test inner exception", output, StringComparison.Ordinal);
        Assert.Contains("\t\t\t\tHRESULT: 0x80004003", output, StringComparison.Ordinal);
    }
}
