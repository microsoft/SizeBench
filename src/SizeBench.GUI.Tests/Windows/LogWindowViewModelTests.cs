using System.Collections.ObjectModel;
using SizeBench.Logging;
using SizeBench.TestInfrastructure;

namespace SizeBench.GUI.ViewModels.Tests;

[TestClass]
public class LogWindowViewModelTests
{
    [TestMethod]
    public void LogScopesInitiallyContainsApplication()
    {
        using var appLogger = new TestNoOpApplicationLogger()
        {
            Name = "Test App",
            SessionLogs = new ObservableCollection<NoOpLogger>()
        };
        var vm = new LogWindowViewModel(appLogger);
        Assert.AreEqual(1, vm.LogScopesSourceCollection.Count);
        Assert.AreEqual(appLogger, vm.LogScopesSourceCollection[0]);

        Assert.AreEqual(0, vm.LogScopes.CurrentPosition);
    }

    [TestMethod]
    public void LogScopesInitiallyContainsAllSessions()
    {
        using var appLogger = new TestNoOpApplicationLogger()
        {
            Name = "Test App",
            SessionLogs = new ObservableCollection<NoOpLogger>()
                {
                    new NoOpLogger() { Name = "Fake Session 1" }
                }
        };
        var vm = new LogWindowViewModel(appLogger);
        Assert.AreEqual(2, vm.LogScopesSourceCollection.Count);
        Assert.AreEqual(appLogger, vm.LogScopesSourceCollection[0]);
        Assert.AreEqual(appLogger.SessionLogs.ElementAt(0), vm.LogScopesSourceCollection[1]);

        Assert.AreEqual(0, vm.LogScopes.CurrentPosition);
    }

    [TestMethod]
    public void LogScopesUpdatesWhenSessionAdded()
    {
        using var appLogger = new TestNoOpApplicationLogger()
        {
            Name = "Test App",
            SessionLogs = new ObservableCollection<NoOpLogger>()
                {
                    new NoOpLogger() { Name = "Fake Session 1" }
                }
        };
        var vm = new LogWindowViewModel(appLogger);
        Assert.AreEqual(2, vm.LogScopesSourceCollection.Count);
        Assert.AreEqual(appLogger, vm.LogScopesSourceCollection[0]);
        Assert.AreEqual(appLogger.SessionLogs.ElementAt(0), vm.LogScopesSourceCollection[1]);

        Assert.AreEqual(0, vm.LogScopes.CurrentPosition);

        (appLogger.SessionLogs as ObservableCollection<NoOpLogger>)!.Add(new NoOpLogger() { Name = "Fake Session 2" });

        Assert.AreEqual(3, vm.LogScopesSourceCollection.Count);
        Assert.AreEqual(appLogger, vm.LogScopesSourceCollection[0]);
        Assert.AreEqual(appLogger.SessionLogs.ElementAt(0), vm.LogScopesSourceCollection[1]);
        Assert.AreEqual(appLogger.SessionLogs.ElementAt(1), vm.LogScopesSourceCollection[2]);

        // Verify that we maintain currency across additions
        Assert.AreEqual(0, vm.LogScopes.CurrentPosition);
    }

    [TestMethod]
    public void LogScopesCurrencyIsMaintainedWhenSessionAdded()
    {
        using var appLogger = new TestNoOpApplicationLogger()
        {
            Name = "Test App",
            SessionLogs = new ObservableCollection<NoOpLogger>()
                {
                    new NoOpLogger() { Name = "Fake Session 1" }
                }
        };
        var vm = new LogWindowViewModel(appLogger);
        Assert.AreEqual(2, vm.LogScopesSourceCollection.Count);
        Assert.AreEqual(appLogger, vm.LogScopesSourceCollection[0]);
        Assert.AreEqual(appLogger.SessionLogs.ElementAt(0), vm.LogScopesSourceCollection[1]);

        vm.LogScopes.MoveCurrentTo(vm.LogScopesSourceCollection[1]);
        Assert.AreEqual(1, vm.LogScopes.CurrentPosition);
        Assert.AreEqual(appLogger.SessionLogs.ElementAt(0), vm.LogScopes.CurrentItem);

        (appLogger.SessionLogs as ObservableCollection<NoOpLogger>)!.Add(new NoOpLogger() { Name = "Fake Session 2" });

        Assert.AreEqual(3, vm.LogScopesSourceCollection.Count);
        Assert.AreEqual(appLogger, vm.LogScopesSourceCollection[0]);
        Assert.AreEqual(appLogger.SessionLogs.ElementAt(0), vm.LogScopesSourceCollection[1]);
        Assert.AreEqual(appLogger.SessionLogs.ElementAt(1), vm.LogScopesSourceCollection[2]);

        // Verify that we maintain currency across additions
        Assert.AreEqual(1, vm.LogScopes.CurrentPosition);
        Assert.AreEqual(appLogger.SessionLogs.ElementAt(0), vm.LogScopes.CurrentItem);
    }

}
