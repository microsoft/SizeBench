using System.Collections.Specialized;
using System.IO;
using SizeBench.AsyncInfrastructure;

namespace SizeBench.Logging.Tests;

[TestClass]
public class ApplicationLoggerTests
{
    [TestMethod]
    public void CanConstructApplicationLoggerWithoutSynchronizationContext()
    {
        using IApplicationLogger logger = new ApplicationLogger("Test App", null);
    }

    [TestMethod]
    public void LoggingAfterDisposeThrows()
    {
        ApplicationLogger logger;
        using (logger = new ApplicationLogger("Test App", null))
        {
        }

        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.Log("should throw"));
    }

    [TestMethod]
    public void LogWithoutSynchronizationContextIsImmediate()
    {
        using var logger = new ApplicationLogger("Test App", null);
        Assert.IsFalse(logger.Entries is INotifyCollectionChanged);
        Assert.IsEmpty(logger.Entries);
        logger.Log("test");
        Assert.HasCount(1, logger.Entries);
        var entry = logger.Entries.First();
        Assert.AreEqual("LogWithoutSynchronizationContextIsImmediate", entry.CallingMember);
    }

    [TestMethod]
    public void LogWithSynchronizationContextPosts()
    {
        AsyncPump.Run(async delegate
        {
            using var logger = new ApplicationLogger("Test App", SynchronizationContext.Current);
            var testThreadId = Environment.CurrentManagedThreadId;
            Assert.IsTrue(logger.Entries is INotifyCollectionChanged);
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
    public void DisposingCausesAllSessionLoggersToDispose()
    {
        Logger? sessionLogger;
        using (var logger = new ApplicationLogger("Test App", null))
        {
            sessionLogger = logger.CreateSessionLog("Session 1") as Logger;
        }
        Assert.IsTrue(sessionLogger?.IsDisposed);
    }

    [TestMethod]
    public void SessionLoggerCanInheritParentSynchronizationContext()
    {
        var testContext = new SynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(testContext);
        using var logger = new ApplicationLogger("Test App", testContext);
        var inheritSync = logger.CreateSessionLog("Test Session With Inheritance");
        Assert.AreEqual(testContext, inheritSync.SynchronizationContext);
    }

    [TestMethod]
    public void AddingSessionLogDoesNotFireChangeNotificationWhenSynchronizationContextIsNull()
    {
        using var logger = new ApplicationLogger("Test App", null);
        Assert.IsFalse(logger.SessionLogs is INotifyCollectionChanged);
        Assert.IsEmpty(logger.SessionLogs);

        using (var session = logger.CreateSessionLog("Session 1"))
        {
            Assert.HasCount(1, logger.SessionLogs);
            Assert.AreEqual("Session 1", logger.SessionLogs.ElementAt(0).Name);
        }
        Assert.IsEmpty(logger.SessionLogs);
    }

    [TestMethod]
    public void AddingSessionLogDoesFireChangeNotificationWhenSynchronizationContextIsPresent()
    {
        AsyncPump.Run(async delegate
        {
            using var logger = new ApplicationLogger("Test App", SynchronizationContext.Current);
            Assert.IsTrue(logger.SessionLogs is INotifyCollectionChanged);
            Assert.IsEmpty(logger.SessionLogs);

            var firedCollectionChanged = false;

            (logger.SessionLogs as INotifyCollectionChanged)!.CollectionChanged +=
                (s, e) => firedCollectionChanged = true;

            using (var session = await Task.Run<ILogger>(() => logger.CreateSessionLog("Session 1")))
            {
                Assert.IsTrue(firedCollectionChanged);
                firedCollectionChanged = false;
                Assert.HasCount(1, logger.SessionLogs);
                Assert.AreEqual("Session 1", logger.SessionLogs.ElementAt(0).Name);
            }
            Assert.IsEmpty(logger.SessionLogs);
            Assert.IsTrue(firedCollectionChanged);
        });
    }

    [TestMethod]
    public void StartingTaskLogDoesNotFireChangeNotificationsWhenSynchronizationContextIsNull()
    {
        using var logger = new ApplicationLogger("Test App", null);
        Assert.IsFalse(logger.Entries is INotifyCollectionChanged);
        Assert.IsEmpty(logger.Entries);

        using (var task = logger.StartTaskLog("Task 1"))
        {
            Assert.HasCount(1, logger.Entries);
            Assert.AreEqual("Task 1", logger.Entries.ElementAt(0).Message);
            task.Log("Test nested message");
        }

        Assert.HasCount(1, logger.Entries);
        var taskLogEntry = (TaskLogEntry)logger.Entries.ElementAt(0);
        Assert.HasCount(1, taskLogEntry.Entries);
        Assert.AreEqual("Test nested message", taskLogEntry.Entries.ElementAt(0).Message);
    }

    [TestMethod]
    public void StartingTaskLogDoesFireChangeNotificationWhenSynchronizationContextIsPresent()
    {
        AsyncPump.Run(async delegate
        {
            using var logger = new ApplicationLogger("Test App", SynchronizationContext.Current);
            Assert.IsTrue(logger.Entries is INotifyCollectionChanged);
            Assert.IsEmpty(logger.Entries);

            var firedCollectionChanged = false;

            (logger.Entries as INotifyCollectionChanged)!.CollectionChanged +=
                (s, e) => firedCollectionChanged = true;

            using (var task = await Task.Run<ILogger>(() => { var taskLog = logger.StartTaskLog("Task 1"); taskLog.Log("Test nested message"); return taskLog; }))
            {
                Assert.IsTrue(firedCollectionChanged);
                Assert.IsTrue(task.Entries is INotifyCollectionChanged);
                firedCollectionChanged = false;
                Assert.HasCount(1, logger.Entries);
                Assert.AreEqual("Task 1", logger.Entries.ElementAt(0).Message);

                Assert.HasCount(1, task.Entries);
                Assert.AreEqual("Test nested message", task.Entries.ElementAt(0).Message);
            }
            Assert.HasCount(1, logger.Entries);
            Assert.IsFalse(firedCollectionChanged);
        });
    }

    [TestMethod]
    public void LoggingExceptionWorks()
    {
        using var logger = new ApplicationLogger("Test App", null);
        var exceptionToLog = new InvalidOperationException();
        Assert.IsFalse(logger.Entries is INotifyCollectionChanged);
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
        using var logger = new ApplicationLogger("Test App", null);
        Assert.IsFalse(logger.Entries is INotifyCollectionChanged);
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
    public void WriteLogSpitsOutAllSessionLogsAndTasks()
    {
        using var appLogger = new ApplicationLogger("Test App", null);
        var session1Logger = appLogger.CreateSessionLog("Session 1");
        var session2Logger = appLogger.CreateSessionLog("Session 2");

        session1Logger.Log("test log 1");
        var taskLog = session1Logger.StartTaskLog("test task");
        taskLog.Log("task part 1");
        taskLog.Log("task part 2");
        session1Logger.StartProgressLogEntry("Starting thing 1...");
        session1Logger.LogException("Uh-oh!", new InvalidOperationException("test outer exception", new AccessViolationException("test inner exception")));

        session2Logger.Log("test log 2");
        var progress = session2Logger.StartProgressLogEntry("Starting thing 2...");
        session2Logger.LogException("Uh-oh again!", new IndexOutOfRangeException("test outer exception 2", new EntryPointNotFoundException("test inner exception 2")));
        progress.UpdateProgress("5% done");
        session2Logger.Log("test log 3");

        using var writer = new StringWriter();
        Assert.AreEqual(String.Empty, writer.ToString());
        appLogger.WriteLog(writer);

        var output = writer.ToString();

        // Log should look roughly like this:
        //     Session 1
        //         test log 1
        //         test task
        //             task part 1
        //             task part 2
        //         Starting thing 1... (progress)
        //         Uh-oh!  (exception)
        //     Session 2
        //         test log 2
        //         5% done (progress, after update)
        //         Uh-oh again! (exception)
        //         test log 3
        //
        //
        // I don't want to validate everything super-carefully, but ordering is the important bit.
        // Beyond that we can rely on the tests for each log entry type that those are formatted more carefully.

        var session1NameIndex = output.IndexOf("Session 1", StringComparison.Ordinal);
        var session2NameIndex = output.IndexOf("Session 2", StringComparison.Ordinal);
        var testLog1Index = output.IndexOf("test log 1", StringComparison.Ordinal);
        var testLog2Index = output.IndexOf("test log 2", StringComparison.Ordinal);
        var testLog3Index = output.IndexOf("test log 3", StringComparison.Ordinal);
        var testTaskLogIndex = output.IndexOf("test task", StringComparison.Ordinal);
        var taskPart1LogIndex = output.IndexOf("task part 1", StringComparison.Ordinal);
        var taskPart2LogIndex = output.IndexOf("task part 2", StringComparison.Ordinal);
        var startingThing1Index = output.IndexOf("Starting thing 1...", StringComparison.Ordinal);
        var startingThing2Index = output.IndexOf("Starting thing 2...", StringComparison.Ordinal);
        var uhOhIndex = output.IndexOf("Uh-oh!", StringComparison.Ordinal);
        var uhOhAgainIndex = output.IndexOf("Uh-oh again!", StringComparison.Ordinal);
        var fivePercentDoneIndex = output.IndexOf("5% done", StringComparison.Ordinal);

        // This verifies that all the expected general inforamtion is present.
        // Exact formatting per entry is left to the LogEntry tests.
        Assert.IsGreaterThan(-1, session1NameIndex);
        Assert.IsGreaterThan(-1, session2NameIndex);
        Assert.IsGreaterThan(-1, testLog1Index);
        Assert.IsGreaterThan(-1, testLog2Index);
        Assert.IsGreaterThan(-1, testLog3Index);
        Assert.IsGreaterThan(-1, testTaskLogIndex);
        Assert.IsGreaterThan(-1, taskPart1LogIndex);
        Assert.IsGreaterThan(-1, taskPart2LogIndex);
        Assert.IsGreaterThan(-1, startingThing1Index);
        Assert.AreEqual(-1, startingThing2Index); // This should have been replaced with "5% done" when we updated progress
        Assert.IsGreaterThan(-1, uhOhIndex);
        Assert.IsGreaterThan(-1, uhOhAgainIndex);
        Assert.IsGreaterThan(-1, fivePercentDoneIndex);

        // Now assert ordering
        Assert.IsLessThan(testLog1Index, session1NameIndex);
        Assert.IsLessThan(testTaskLogIndex, testLog1Index);
        Assert.IsLessThan(taskPart1LogIndex, testTaskLogIndex);
        Assert.IsLessThan(taskPart2LogIndex, taskPart1LogIndex);
        Assert.IsLessThan(startingThing1Index, taskPart2LogIndex);
        Assert.IsLessThan(uhOhIndex, startingThing1Index);

        Assert.IsLessThan(session2NameIndex, uhOhIndex);
        Assert.IsLessThan(testLog2Index, session2NameIndex);
        Assert.IsLessThan(fivePercentDoneIndex, testLog2Index);
        Assert.IsLessThan(uhOhAgainIndex, fivePercentDoneIndex);
        Assert.IsLessThan(testLog3Index, uhOhAgainIndex);
    }

    [TestMethod]
    public void DisposedLoggerThrowsOnEverything()
    {
        using var logger = new ApplicationLogger("Test App", null);
        logger.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.Log("test"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.LogException("test", new Exception()));
        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.StartProgressLogEntry("Starting..."));
        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.StartTaskLog("sub task"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.CreateSessionLog("session name"));
        using var writer = new StringWriter();
        Assert.ThrowsExactly<ObjectDisposedException>(() => logger.WriteLog(writer));
    }

    [TestMethod]
    public void DisposingSessionLoggerRemovesItFromApplicationLogger()
    {
        using var appLogger = new ApplicationLogger("Test App", null);
        Assert.IsEmpty(appLogger.SessionLogs);

        ILogger? sessionLogger;
        using (sessionLogger = appLogger.CreateSessionLog("session 1"))
        {
            Assert.IsFalse(((Logger)sessionLogger).IsDisposed);
            Assert.HasCount(1, appLogger.SessionLogs);
            Assert.IsTrue(ReferenceEquals(sessionLogger, appLogger.SessionLogs.First()));
        }

        Assert.IsEmpty(appLogger.SessionLogs);
        Assert.IsTrue(((Logger)sessionLogger).IsDisposed);
        Assert.IsFalse(appLogger.IsDisposed);
    }

    public TestContext TestContext { get; set; }
}
