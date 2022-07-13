using System.Diagnostics;
using System.Globalization;
using SizeBench.Logging;

namespace SizeBench.GUI.ViewModels.Tests;
#nullable disable // WPF's IValueConverter is not correctly nullable-annotated, so we disable nullable for the source and tests of the value converters.

[TestClass]
public sealed class LogEntryToLogWindowTreeViewDisplayConverterTests
{
    [TestMethod]
    public void BadInputThrowsException()
    {
        var converter = new LogEntryToLogWindowTreeViewDisplayConverter();
        Assert.ThrowsException<NotSupportedException>(() => converter.Convert(null, typeof(string), null, null));
        Assert.ThrowsException<NotSupportedException>(() => converter.Convert(42, typeof(string), null, null));
    }

    [TestMethod]
    public void BasicLogEntryReturnsValidDisplayString()
    {
        var converter = new LogEntryToLogWindowTreeViewDisplayConverter();
        var logEntry = new LogEntry("CallingClass.Function", "my message here", LogLevel.Error);
        Assert.AreEqual("CallingClass.Function: my message here", converter.Convert(logEntry, typeof(string), null, null));
    }

    [TestMethod]
    public void TaskLogEntryReturnsValidDisplayStringWhenRunningOrCompleted()
    {
        var converter = new LogEntryToLogWindowTreeViewDisplayConverter();
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        Thread.Sleep(10);
        var logEntry = new TaskLogEntry(Enumerable.Empty<LogEntry>(), stopwatch, "CallingClass.Function", "my message here", LogLevel.Error);
        Assert.AreEqual($"CallingClass.Function: my message here (still running)", converter.Convert(logEntry, typeof(string), null, null));
        stopwatch.Stop();
        Assert.AreEqual($"CallingClass.Function: my message here (elapsed: {stopwatch.Elapsed.ToString(@"mm\:ss\:fff", CultureInfo.InvariantCulture)})", converter.Convert(logEntry, typeof(string), null, null));
    }
}
