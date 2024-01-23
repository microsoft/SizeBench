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

    [ExpectedException(typeof(ObjectDisposedException), AllowDerivedTypes = false)]
    [TestMethod]
    public void LoggingAfterDisposeThrows()
    {
        ApplicationLogger logger;
        using (logger = new ApplicationLogger("Test App", null))
        {
        }

        logger.Log("should throw");
    }

    [TestMethod]
    public void LogWithoutSynchronizationContextIsImmediate()
    {
        using var logger = new ApplicationLogger("Test App", null);
        Assert.IsFalse(logger.Entries is INotifyCollectionChanged);
        Assert.AreEqual(0, logger.Entries.Count());
        logger.Log("test");
        Assert.AreEqual(1, logger.Entries.Count());
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
        Assert.AreEqual(0, logger.SessionLogs.Count());

        using (var session = logger.CreateSessionLog("Session 1"))
        {
            Assert.AreEqual(1, logger.SessionLogs.Count());
            Assert.AreEqual("Session 1", logger.SessionLogs.ElementAt(0).Name);
        }
        Assert.AreEqual(0, logger.SessionLogs.Count());
    }

    [TestMethod]
    public void AddingSessionLogDoesFireChangeNotificationWhenSynchronizationContextIsPresent()
    {
        AsyncPump.Run(async delegate
        {
            using var logger = new ApplicationLogger("Test App", SynchronizationContext.Current);
            Assert.IsTrue(logger.SessionLogs is INotifyCollectionChanged);
            Assert.AreEqual(0, logger.SessionLogs.Count());

            var firedCollectionChanged = false;

            (logger.SessionLogs as INotifyCollectionChanged)!.CollectionChanged +=
                (s, e) => firedCollectionChanged = true;

            using (var session = await Task.Run<ILogger>(() => logger.CreateSessionLog("Session 1")))
            {
                Assert.IsTrue(firedCollectionChanged);
                firedCollectionChanged = false;
                Assert.AreEqual(1, logger.SessionLogs.Count());
                Assert.AreEqual("Session 1", logger.SessionLogs.ElementAt(0).Name);
            }
            Assert.AreEqual(0, logger.SessionLogs.Count());
            Assert.IsTrue(firedCollectionChanged);
        });
    }

    [TestMethod]
    public void StartingTaskLogDoesNotFireChangeNotificationsWhenSynchronizationContextIsNull()
    {
        using var logger = new ApplicationLogger("Test App", null);
        Assert.IsFalse(logger.Entries is INotifyCollectionChanged);
        Assert.AreEqual(0, logger.Entries.Count());

        using (var task = logger.StartTaskLog("Task 1"))
        {
            Assert.AreEqual(1, logger.Entries.Count());
            Assert.AreEqual("Task 1", logger.Entries.ElementAt(0).Message);
            task.Log("Test nested message");
        }

        Assert.AreEqual(1, logger.Entries.Count());
        var taskLogEntry = (TaskLogEntry)logger.Entries.ElementAt(0);
        Assert.AreEqual(1, taskLogEntry.Entries.Count());
        Assert.AreEqual("Test nested message", taskLogEntry.Entries.ElementAt(0).Message);
    }

    [TestMethod]
    public void StartingTaskLogDoesFireChangeNotificationWhenSynchronizationContextIsPresent()
    {
        AsyncPump.Run(async delegate
        {
            using var logger = new ApplicationLogger("Test App", SynchronizationContext.Current);
            Assert.IsTrue(logger.Entries is INotifyCollectionChanged);
            Assert.AreEqual(0, logger.Entries.Count());

            var firedCollectionChanged = false;

            (logger.Entries as INotifyCollectionChanged)!.CollectionChanged +=
                (s, e) => firedCollectionChanged = true;

            using (var task = await Task.Run<ILogger>(() => { var taskLog = logger.StartTaskLog("Task 1"); taskLog.Log("Test nested message"); return taskLog; }))
            {
                Assert.IsTrue(firedCollectionChanged);
                Assert.IsTrue(task.Entries is INotifyCollectionChanged);
                firedCollectionChanged = false;
                Assert.AreEqual(1, logger.Entries.Count());
                Assert.AreEqual("Task 1", logger.Entries.ElementAt(0).Message);

                Assert.AreEqual(1, task.Entries.Count());
                Assert.AreEqual("Test nested message", task.Entries.ElementAt(0).Message);
            }
            Assert.AreEqual(1, logger.Entries.Count());
            Assert.IsFalse(firedCollectionChanged);
        });
    }

    [TestMethod]
    public void LoggingExceptionWorks()
    {
        using var logger = new ApplicationLogger("Test App", null);
        var exceptionToLog = new InvalidOperationException();
        Assert.IsFalse(logger.Entries is INotifyCollectionChanged);
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
        using var logger = new ApplicationLogger("Test App", null);
        Assert.IsFalse(logger.Entries is INotifyCollectionChanged);
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
        Assert.IsTrue(session1NameIndex > -1);
        Assert.IsTrue(session2NameIndex > -1);
        Assert.IsTrue(testLog1Index > -1);
        Assert.IsTrue(testLog2Index > -1);
        Assert.IsTrue(testLog3Index > -1);
        Assert.IsTrue(testTaskLogIndex > -1);
        Assert.IsTrue(taskPart1LogIndex > -1);
        Assert.IsTrue(taskPart2LogIndex > -1);
        Assert.IsTrue(startingThing1Index > -1);
        Assert.AreEqual(-1, startingThing2Index); // This should have been replaced with "5% done" when we updated progress
        Assert.IsTrue(uhOhIndex > -1);
        Assert.IsTrue(uhOhAgainIndex > -1);
        Assert.IsTrue(fivePercentDoneIndex > -1);

        // Now assert ordering
        Assert.IsTrue(session1NameIndex < testLog1Index);
        Assert.IsTrue(testLog1Index < testTaskLogIndex);
        Assert.IsTrue(testTaskLogIndex < taskPart1LogIndex);
        Assert.IsTrue(taskPart1LogIndex < taskPart2LogIndex);
        Assert.IsTrue(taskPart2LogIndex < startingThing1Index);
        Assert.IsTrue(startingThing1Index < uhOhIndex);

        Assert.IsTrue(uhOhIndex < session2NameIndex);
        Assert.IsTrue(session2NameIndex < testLog2Index);
        Assert.IsTrue(testLog2Index < fivePercentDoneIndex);
        Assert.IsTrue(fivePercentDoneIndex < uhOhAgainIndex);
        Assert.IsTrue(uhOhAgainIndex < testLog3Index);
    }

    [TestMethod]
    public void DisposedLoggerThrowsOnEverything()
    {
        using var logger = new ApplicationLogger("Test App", null);
        logger.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => logger.Log("test"));
        Assert.ThrowsException<ObjectDisposedException>(() => logger.LogException("test", new Exception()));
        Assert.ThrowsException<ObjectDisposedException>(() => logger.StartProgressLogEntry("Starting..."));
        Assert.ThrowsException<ObjectDisposedException>(() => logger.StartTaskLog("sub task"));
        Assert.ThrowsException<ObjectDisposedException>(() => logger.CreateSessionLog("session name"));
        using var writer = new StringWriter();
        Assert.ThrowsException<ObjectDisposedException>(() => logger.WriteLog(writer));
    }

    [TestMethod]
    public void DisposingSessionLoggerRemovesItFromApplicationLogger()
    {
        using var appLogger = new ApplicationLogger("Test App", null);
        Assert.AreEqual(0, appLogger.SessionLogs.Count());

        ILogger? sessionLogger;
        using (sessionLogger = appLogger.CreateSessionLog("session 1"))
        {
            Assert.IsFalse(((Logger)sessionLogger).IsDisposed);
            Assert.AreEqual(1, appLogger.SessionLogs.Count());
            Assert.IsTrue(ReferenceEquals(sessionLogger, appLogger.SessionLogs.First()));
        }

        Assert.AreEqual(0, appLogger.SessionLogs.Count());
        Assert.IsTrue(((Logger)sessionLogger).IsDisposed);
        Assert.IsFalse(appLogger.IsDisposed);
    }
}
