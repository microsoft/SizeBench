using System.Diagnostics;

namespace SizeBench.Logging.Tests;

[TestClass]
public class TaskLogEntryTests
{
    [TestMethod]
    public void LogEntryPassesThrough()
    {
        var subEntries = new List<LogEntry>
            {
                new LogEntry("CallingMember1", "test 1", LogLevel.Info),
                new LogEntry("CallingMember2", "test 2", LogLevel.Info),
                new LogEntry("CallingMember3", "test 3", LogLevel.Info)
            };
        var entry = new TaskLogEntry(subEntries, new Stopwatch(), "TestCallingMember", "test message", LogLevel.Warning);
        Assert.AreEqual("TestCallingMember", entry.CallingMember);
        Assert.AreEqual("test message", entry.Message);
        Assert.AreEqual(LogLevel.Warning, entry.LogLevel);
        Assert.IsTrue(entry.Entries.SequenceEqual(subEntries));
    }
}
