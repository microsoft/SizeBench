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
            StringAssert.StartsWith(line, "\t\t", StringComparison.Ordinal);
        }

        // The first line should just contain the basics, not the specific exception details - thos are on lines
        // that follow.  Otherwise it's harder to read.
        StringAssert.Contains(linesOfOutput[0], callingMember, StringComparison.Ordinal);
        StringAssert.Contains(linesOfOutput[0], message, StringComparison.Ordinal);
        StringAssert.Contains(linesOfOutput[0], LogLevel.Error.ToString(), StringComparison.Ordinal);
        Assert.IsFalse(linesOfOutput[0].Contains("InvalidOperationException", StringComparison.Ordinal));
        Assert.IsFalse(linesOfOutput[0].Contains("AccessViolationException", StringComparison.Ordinal));
        Assert.IsFalse(linesOfOutput[0].Contains("HRESULT", StringComparison.Ordinal));

        // Exception should be indented one level beyond the main error, it's more readable that way
        // in a long log.
        StringAssert.Contains(output, "\t\t\tSystem.InvalidOperationException: test outer exception", StringComparison.Ordinal);
        StringAssert.Contains(output, "\t\t\tHRESULT: 0x80131509", StringComparison.Ordinal);

        // InnerException is indented one more level
        StringAssert.Contains(output, "\t\t\t\tSystem.AccessViolationException: test inner exception", StringComparison.Ordinal);
        StringAssert.Contains(output, "\t\t\t\tHRESULT: 0x80004003", StringComparison.Ordinal);
    }
}
