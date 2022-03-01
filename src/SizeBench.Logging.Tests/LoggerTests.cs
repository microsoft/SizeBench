using System.Collections.ObjectModel;
using System.Collections.Specialized;
using SizeBench.AsyncInfrastructure;

namespace SizeBench.Logging.Tests;

[TestClass]
public class LoggerTests
{
    [TestMethod]
    public void CanConstructTaskLoggerWithoutSynchronizationContext()
    {
        using ILogger logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null);
    }

    [ExpectedException(typeof(ObjectDisposedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void LoggingAfterDisposeThrows()
    {
        ILogger logger;
        using (logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null))
        {
        }

        logger.Log("should throw");
    }

    [TestMethod]
    public void LogWithoutSynchronizationContextIsImmediate()
    {
        using var logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null);
        Assert.AreEqual(0, logger.Entries.Count());
        logger.Log("test");
        Assert.AreEqual(1, logger.Entries.Count());
        var entry = logger.Entries.First();
        Assert.AreEqual("test", entry.Message);
        Assert.AreEqual("LogWithoutSynchronizationContextIsImmediate", entry.CallingMember);
    }

    [TestMethod]
    public void StartingTaskLogWithoutSynchronizationContextIsImmediate()
    {
        using var logger = new Logger("Test Session", new List<LogEntry>(), new List<LogEntry>(), null, null);
        Assert.IsNotInstanceOfType(logger.Entries, typeof(INotifyCollectionChanged));
        Assert.AreEqual(0, logger.Entries.Count());
        using (var taskLogger = logger.StartTaskLog("Test Task"))
        {
            Assert.AreEqual(1, logger.Entries.Count());
            Assert.AreEqual(0, taskLogger.Entries.Count());
            taskLogger.Log("Test log within the task");
        }

        Assert.AreEqual(1, logger.Entries.Count()); // The task logger should not log to the parent session logger
        var entry = (TaskLogEntry)logger.Entries.First();
        Assert.AreEqual("StartingTaskLogWithoutSynchronizationContextIsImmediate", entry.CallingMember);
        Assert.AreEqual("Test Task", entry.Message);
        Assert.AreEqual(1, entry.Entries.Count());
    }

    [TestMethod]
    public void LogWithSynchronizationContextPosts()
    {
        AsyncPump.Run(async delegate
        {
            using ILogger logger = new Logger("Test Task", new ObservableCollection<LogEntry>(), new ObservableCollection<LogEntry>(), SynchronizationContext.Current, null);
            var testThreadId = Environment.CurrentManagedThreadId;
            var observable = (INotifyCollectionChanged)logger.Entries;
            var completionSource = new TaskCompletionSource<Tuple<NotifyCollectionChangedEventArgs, int>>();
            observable.CollectionChanged += (s, e) => completionSource.SetResult(new Tuple<NotifyCollectionChangedEventArgs, int>(e, Environment.CurrentManagedThreadId));

            await Task.Run(() => logger.Log("test posting", LogLevel.Warning));

            var tuple = await completionSource.Task;
            var args = tuple.Item1;
            Assert.AreEqual(testThreadId, tuple.Item2);
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.AreEqual(1, args.NewItems!.Count);
            Assert.AreEqual(LogLevel.Warning, (args.NewItems[0] as LogEntry)!.LogLevel);

            Assert.AreEqual(1, logger.Entries.Count());
        });
    }

    [TestMethod]
    public void LoggingToChildTaskLogWithSynchronizationContextPosts()
    {
        AsyncPump.Run(async delegate
        {
            using ILogger logger = new Logger("Test Task", new ObservableCollection<LogEntry>(), new ObservableCollection<LogEntry>(), SynchronizationContext.Current, null);
            var testThreadId = Environment.CurrentManagedThreadId;
            Assert.IsInstanceOfType(logger.Entries, typeof(INotifyCollectionChanged));
            var observable = (INotifyCollectionChanged)logger.Entries;
            var completionSource = new TaskCompletionSource<Tuple<NotifyCollectionChangedEventArgs, int>>();
            observable.CollectionChanged += (s, e) => completionSource.SetResult(new Tuple<NotifyCollectionChangedEventArgs, int>(e, Environment.CurrentManagedThreadId));

            await Task.Run(() =>
            {
                using var taskLogger = logger.StartTaskLog("Test Task started off-thread");
                taskLogger.Log("Test entry");
            });

            var tuple = await completionSource.Task;
            var args = tuple.Item1;
            Assert.AreEqual(testThreadId, tuple.Item2);
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.AreEqual(1, args.NewItems!.Count);
            Assert.AreEqual(LogLevel.Info, (args.NewItems[0] as TaskLogEntry)!.LogLevel);
            Assert.AreEqual("Test Task started off-thread", (args.NewItems[0] as TaskLogEntry)!.Message);

            Assert.AreEqual(1, logger.Entries.Count());
        });
    }

    [TestMethod]
    public void SubtaskLoggerCanInheritParentSynchronizationContext()
    {
        var testContext = new SynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(testContext);
        using ILogger logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), testContext, null);
        var inheritSync = logger.StartTaskLog("Test Subtask With Inheritance");
        Assert.AreEqual(testContext, inheritSync.SynchronizationContext);
    }

    [TestMethod]
    public void LoggingExceptionWorks()
    {
        using var logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null);
        var exceptionToLog = new InvalidOperationException();
        Assert.AreEqual(0, logger.Entries.Count());
        logger.LogException("test", exceptionToLog);
        Assert.AreEqual(1, logger.Entries.Count());
        var entry = (LogExceptionEntry)logger.Entries.First();
        Assert.AreEqual("LoggingExceptionWorks", entry.CallingMember);
        StringAssert.Contains(entry.Message, "test", StringComparison.Ordinal);
        Assert.AreEqual(exceptionToLog, entry.Exception);
    }

    [TestMethod]
    public void StartProgressLogEntryWorks()
    {
        using var logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null);
        Assert.AreEqual(0, logger.Entries.Count());
        logger.StartProgressLogEntry("Starting...");
        Assert.AreEqual(1, logger.Entries.Count());
        var entry = (LogEntryForProgress)logger.Entries.First();
        Assert.AreEqual("StartProgressLogEntryWorks", entry.CallingMember);
        Assert.AreEqual("Starting...", entry.Message);

        entry.UpdateProgress("5% done");
        Assert.AreEqual("5% done", entry.Message);
    }

    [TestMethod]
    public void DisposedLoggerThrowsOnEverything()
    {
        ILogger logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null);
        logger.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => logger.Log("test"));
        Assert.ThrowsException<ObjectDisposedException>(() => logger.LogException("test", new Exception()));
        Assert.ThrowsException<ObjectDisposedException>(() => logger.StartProgressLogEntry("Starting..."));
        Assert.ThrowsException<ObjectDisposedException>(() => logger.StartTaskLog("sub task"));
    }
}
