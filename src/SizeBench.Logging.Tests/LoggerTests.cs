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
        using var logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null);
    }

    [TestMethod]
    public void LoggingAfterDisposeThrows()
    {
        Logger logger;
        using (logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null))
        {
        }

        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.Log("should throw"));
    }

    [TestMethod]
    public void LogWithoutSynchronizationContextIsImmediate()
    {
        using var logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null);
        Assert.IsEmpty(logger.Entries);
        logger.Log("test");
        Assert.HasCount(1, logger.Entries);
        var entry = logger.Entries.First();
        Assert.AreEqual("test", entry.Message);
        Assert.AreEqual("LogWithoutSynchronizationContextIsImmediate", entry.CallingMember);
    }

    [TestMethod]
    public void StartingTaskLogWithoutSynchronizationContextIsImmediate()
    {
        using var logger = new Logger("Test Session", new List<LogEntry>(), new List<LogEntry>(), null, null);
        Assert.IsNotInstanceOfType<INotifyCollectionChanged>(logger.Entries);
        Assert.IsEmpty(logger.Entries);
        using (var taskLogger = logger.StartTaskLog("Test Task"))
        {
            Assert.HasCount(1, logger.Entries);
            Assert.IsEmpty(taskLogger.Entries);
            taskLogger.Log("Test log within the task");
        }

        Assert.HasCount(1, logger.Entries); // The task logger should not log to the parent session logger
        var entry = (TaskLogEntry)logger.Entries.First();
        Assert.AreEqual("StartingTaskLogWithoutSynchronizationContextIsImmediate", entry.CallingMember);
        Assert.AreEqual("Test Task", entry.Message);
        Assert.HasCount(1, entry.Entries);
    }

    [TestMethod]
    public void LogWithSynchronizationContextPosts()
    {
        AsyncPump.Run(async delegate
        {
            using var logger = new Logger("Test Task", new ObservableCollection<LogEntry>(), new ObservableCollection<LogEntry>(), SynchronizationContext.Current, null);
            var testThreadId = Environment.CurrentManagedThreadId;
            var observable = (INotifyCollectionChanged)logger.Entries;
            var completionSource = new TaskCompletionSource<Tuple<NotifyCollectionChangedEventArgs, int>>();
            observable.CollectionChanged += (s, e) => completionSource.SetResult(new Tuple<NotifyCollectionChangedEventArgs, int>(e, Environment.CurrentManagedThreadId));

            await Task.Run(() => logger.Log("test posting", LogLevel.Warning), this.TestContext.CancellationToken);

            var tuple = await completionSource.Task;
            var args = tuple.Item1;
            Assert.AreEqual(testThreadId, tuple.Item2);
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.HasCount(1, args.NewItems!);
            Assert.AreEqual(LogLevel.Warning, (args.NewItems![0] as LogEntry)!.LogLevel);

            Assert.HasCount(1, logger.Entries);
        });
    }

    [TestMethod]
    public void LoggingToChildTaskLogWithSynchronizationContextPosts()
    {
        AsyncPump.Run(async delegate
        {
            using var logger = new Logger("Test Task", new ObservableCollection<LogEntry>(), new ObservableCollection<LogEntry>(), SynchronizationContext.Current, null);
            var testThreadId = Environment.CurrentManagedThreadId;
            Assert.IsInstanceOfType<INotifyCollectionChanged>(logger.Entries);
            var observable = (INotifyCollectionChanged)logger.Entries;
            var completionSource = new TaskCompletionSource<Tuple<NotifyCollectionChangedEventArgs, int>>();
            observable.CollectionChanged += (s, e) => completionSource.SetResult(new Tuple<NotifyCollectionChangedEventArgs, int>(e, Environment.CurrentManagedThreadId));

            await Task.Run(() =>
            {
                using var taskLogger = logger.StartTaskLog("Test Task started off-thread");
                taskLogger.Log("Test entry");
            }, this.TestContext.CancellationToken);

            var tuple = await completionSource.Task;
            var args = tuple.Item1;
            Assert.AreEqual(testThreadId, tuple.Item2);
            Assert.AreEqual(NotifyCollectionChangedAction.Add, args.Action);
            Assert.HasCount(1, args.NewItems!);
            Assert.AreEqual(LogLevel.Info, (args.NewItems![0] as TaskLogEntry)!.LogLevel);
            Assert.AreEqual("Test Task started off-thread", (args.NewItems![0] as TaskLogEntry)!.Message);

            Assert.HasCount(1, logger.Entries);
        });
    }

    [TestMethod]
    public void SubtaskLoggerCanInheritParentSynchronizationContext()
    {
        var testContext = new SynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(testContext);
        using var logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), testContext, null);
        var inheritSync = logger.StartTaskLog("Test Subtask With Inheritance");
        Assert.AreEqual(testContext, inheritSync.SynchronizationContext);
    }

    [TestMethod]
    public void LoggingExceptionWorks()
    {
        using var logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null);
        var exceptionToLog = new InvalidOperationException();
        Assert.IsEmpty(logger.Entries);
        logger.LogException("test", exceptionToLog);
        Assert.HasCount(1, logger.Entries);
        var entry = (LogExceptionEntry)logger.Entries.First();
        Assert.AreEqual("LoggingExceptionWorks", entry.CallingMember);
        Assert.Contains("test", entry.Message, StringComparison.Ordinal);
        Assert.AreEqual(exceptionToLog, entry.Exception);
    }

    [TestMethod]
    public void StartProgressLogEntryWorks()
    {
        using var logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null);
        Assert.IsEmpty(logger.Entries);
        logger.StartProgressLogEntry("Starting...");
        Assert.HasCount(1, logger.Entries);
        var entry = (LogEntryForProgress)logger.Entries.First();
        Assert.AreEqual("StartProgressLogEntryWorks", entry.CallingMember);
        Assert.AreEqual("Starting...", entry.Message);

        entry.UpdateProgress("5% done");
        Assert.AreEqual("5% done", entry.Message);
    }

    [TestMethod]
    public void DisposedLoggerThrowsOnEverything()
    {
        var logger = new Logger("Test Task", new List<LogEntry>(), new List<LogEntry>(), null, null);
        logger.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.Log("test"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.LogException("test", new Exception()));
        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.StartProgressLogEntry("Starting..."));
        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.StartTaskLog("sub task"));
    }

    public TestContext TestContext { get; set; }
}
