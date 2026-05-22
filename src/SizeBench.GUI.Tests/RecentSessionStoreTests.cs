using System.IO;
using SizeBench.AnalysisEngine;
using SizeBench.TestInfrastructure;

namespace SizeBench.GUI.Tests;

[TestClass]
public sealed class RecentSessionStoreTests
{
    [TestMethod]
    public void RecordSessionPersistsAndReloadsSingleAndDiffSessions()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var storagePath = Path.Combine(tempDirectory, "RecentSessions.json");
            using var logger = new TestNoOpApplicationLogger();
            var store = new RecentSessionStore(logger, storagePath);

            var singleSession = RecentSession.CreateSingle(@"c:\dev\foo.dll",
                                                           @"c:\dev\foo.pdb",
                                                           new SessionOptions() { SymbolSourcesSupported = SymbolSourcesSupported.Code | SymbolSourcesSupported.DataSymbols });
            var diffSession = RecentSession.CreateDiff(@"c:\dev\before.dll",
                                                       @"c:\dev\before.pdb",
                                                       @"c:\dev\after.dll",
                                                       @"c:\dev\after.pdb");

            store.RecordSession(singleSession);
            store.RecordSession(diffSession);

            var reloadedStore = new RecentSessionStore(logger, storagePath);
            var recentSessions = reloadedStore.GetRecentSessions();

            Assert.AreEqual(2, recentSessions.Count);
            Assert.AreEqual(RecentSessionKind.BinaryDiff, recentSessions[0].Kind);
            Assert.AreEqual(@"c:\dev\before.dll", recentSessions[0].BeforeBinaryPath);
            Assert.AreEqual(RecentSessionKind.SingleBinary, recentSessions[1].Kind);
            Assert.AreEqual(SymbolSourcesSupported.Code | SymbolSourcesSupported.DataSymbols, recentSessions[1].SymbolSourcesSupported);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void RecordSessionDedupesAndKeepsNewestCopy()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var storagePath = Path.Combine(tempDirectory, "RecentSessions.json");
            using var logger = new TestNoOpApplicationLogger();
            var store = new RecentSessionStore(logger, storagePath);

            var originalSession = RecentSession.CreateSingle(@"c:\dev\foo.dll", @"c:\dev\foo.pdb", new SessionOptions(), DateTimeOffset.UtcNow.AddDays(-2));
            var newerSession = RecentSession.CreateSingle(@"c:\dev\foo.dll", @"c:\dev\foo.pdb", new SessionOptions(), DateTimeOffset.UtcNow.AddDays(-1));

            store.RecordSession(originalSession);
            store.RecordSession(newerSession);

            var recentSessions = store.GetRecentSessions();

            Assert.AreEqual(1, recentSessions.Count);
            Assert.AreEqual(@"c:\dev\foo.dll", recentSessions[0].BinaryPath);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void RecordSessionCapsHistoryAtTenEntries()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var storagePath = Path.Combine(tempDirectory, "RecentSessions.json");
            using var logger = new TestNoOpApplicationLogger();
            var store = new RecentSessionStore(logger, storagePath);

            for (var i = 0; i < RecentSessionStore.MaximumStoredSessions + 2; i++)
            {
                store.RecordSession(RecentSession.CreateSingle($@"c:\dev\foo{i}.dll",
                                                               $@"c:\dev\foo{i}.pdb",
                                                               new SessionOptions(),
                                                               DateTimeOffset.UtcNow.AddMinutes(-i)));
            }

            var recentSessions = store.GetRecentSessions();

            Assert.AreEqual(RecentSessionStore.MaximumStoredSessions, recentSessions.Count);
            Assert.AreEqual(@"c:\dev\foo11.dll", recentSessions[0].BinaryPath);
            Assert.AreEqual(@"c:\dev\foo2.dll", recentSessions[^1].BinaryPath);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
