using Castle.Windsor;
using SizeBench.AnalysisEngine;
using SizeBench.Logging;
using SizeBench.TestInfrastructure;
using SizeBench.GUI.ViewModels;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Tests;

[TestClass]
public sealed class MainWindowViewModelTests : IDisposable
{
    public IWindsorContainer WindsorContainer = new WindsorContainer();
    public Mock<ISession> MockSession = new Mock<ISession>();
    public Mock<ISessionFactory> MockSessionFactory = new Mock<ISessionFactory>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.MockSessionFactory = new Mock<ISessionFactory>();
        this.WindsorContainer = new WindsorContainer();
    }

    [TestMethod]
    public void DeeplinkWithOnlyBinaryPathDoesNothing()
    {
        this.MockSessionFactory.Setup(sf => sf.CreateSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()))
                               .Returns(Task.FromResult(new Mock<ISession>().Object));

        using var container = new WindsorContainer();
        using var appLogger = new TestNoOpApplicationLogger();
        var vm = new MainWindowViewModel(container,
                                         appLogger,
                                         this.MockSessionFactory.Object);
        this.MockSessionFactory.Verify(sf => sf.CreateSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never());
    }

    [TestMethod]
    public void DeeplinkWithOnlyPDBPathDoesNothing()
    {
        this.MockSessionFactory.Setup(sf => sf.CreateSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()))
                               .Returns(Task.FromResult(new Mock<ISession>().Object));

        using var container = new WindsorContainer();
        using var appLogger = new TestNoOpApplicationLogger();
        var vm = new MainWindowViewModel(container,
                                         appLogger,
                                         this.MockSessionFactory.Object);
        this.MockSessionFactory.Verify(sf => sf.CreateSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never());
    }

    [TestMethod]
    public async Task DeeplinkWithBothPathsOpensSession()
    {
        var expectedBinaryPath = @"c:\dev\blah.dll";
        var expectedPDBPath = @"c:\dev\other\blah.pdb";

        this.MockSessionFactory.Setup(sf => sf.CreateSession(expectedBinaryPath, expectedPDBPath, It.IsAny<SessionOptions>(), It.IsAny<ILogger>()))
                               .Returns(Task.FromResult<ISession>(new Mock<ISession>().Object));

        using var container = new WindsorContainer();
        using var appLogger = new TestNoOpApplicationLogger();
        var vm = new MainWindowViewModel(container,
                                         appLogger,
                                         this.MockSessionFactory.Object);
        await vm.TryResolveDeeplink(new Uri($"sizebench://2.0/Test?BinaryPath={Uri.EscapeDataString(expectedBinaryPath)}&PDBPath={Uri.EscapeDataString(expectedPDBPath)}"));
        this.MockSessionFactory.Verify(sf => sf.CreateSession(expectedBinaryPath, expectedPDBPath, It.IsAny<SessionOptions>(), It.IsAny<ILogger>()), Times.Exactly(1));
    }

    [TestMethod]
    public async Task DeeplinkWithBothPathsAndInAppPageGoesDeep()
    {
        var expectedBinaryPath = @"c:\dev\blah.dll";
        var expectedPDBPath = @"c:\dev\other\blah.pdb";
        var expectedInAppPage = @"BinarySection/.text";

        var tcsCreateSession = new TaskCompletionSource<ISession>();
        this.MockSessionFactory.Setup(sf => sf.CreateSession(expectedBinaryPath, expectedPDBPath, It.IsAny<SessionOptions>(), It.IsAny<ILogger>()))
                               .Returns(tcsCreateSession.Task);

        using var container = new WindsorContainer();
        using var appLogger = new TestNoOpApplicationLogger();
        var vm = new MainWindowViewModel(container,
                                         appLogger,
                                         this.MockSessionFactory.Object);
        var deeplinkResolutionTask = vm.TryResolveDeeplink(new Uri($"sizebench://2.0/{Uri.EscapeDataString(expectedInAppPage)}?BinaryPath={Uri.EscapeDataString(expectedBinaryPath)}&PDBPath={Uri.EscapeDataString(expectedPDBPath)}"));

        Assert.AreEqual(0, vm.OpenTabs.Count);
        Assert.IsNull(vm.SelectedTab);

        this.MockSessionFactory.Verify(sf => sf.CreateSession(expectedBinaryPath, expectedPDBPath, It.IsAny<SessionOptions>(), It.IsAny<ILogger>()), Times.Exactly(1));
        tcsCreateSession.SetResult(new Mock<ISession>().Object);

        await deeplinkResolutionTask;

        Assert.AreEqual(1, vm.OpenTabs.Count);
        Assert.IsTrue(ReferenceEquals(vm.SelectedTab, vm.OpenTabs[0]));
        Assert.IsInstanceOfType(vm.OpenTabs[0], typeof(SingleBinaryTab));
        Assert.AreEqual(new Uri(expectedInAppPage, UriKind.Relative), vm.SelectedTab!.CurrentPage);
    }

    [TestMethod]
    public void DeeplinkToDiffWithOnlyBeforeAndAfterBinaryPathsDoesNothing()
    {
        this.MockSessionFactory.Setup(sf => sf.CreateDiffSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()))
                               .Returns(Task.FromResult(new Mock<IDiffSession>().Object));

        using var container = new WindsorContainer();
        using var appLogger = new TestNoOpApplicationLogger();
        var vm = new MainWindowViewModel(container,
                                         appLogger,
                                         this.MockSessionFactory.Object);
        this.MockSessionFactory.Verify(sf => sf.CreateSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never());
        this.MockSessionFactory.Verify(sf => sf.CreateDiffSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never());
    }

    [TestMethod]
    public void DeeplinkToDiffWithOneBinaryPathAndBeforeAndAfterPDBPathsDoesNothing()
    {
        this.MockSessionFactory.Setup(sf => sf.CreateDiffSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()))
                               .Returns(Task.FromResult(new Mock<IDiffSession>().Object));

        using var container = new WindsorContainer();
        using var appLogger = new TestNoOpApplicationLogger();
        var vm = new MainWindowViewModel(container,
                                         appLogger,
                                         this.MockSessionFactory.Object);
        this.MockSessionFactory.Verify(sf => sf.CreateSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never());
        this.MockSessionFactory.Verify(sf => sf.CreateDiffSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never());
    }

    [TestMethod]
    public async Task DeeplinkToDiffWithAllFourPathsOpensDiffSession()
    {
        var expectedBeforeBinaryPath = @"c:\dev\before\blah.dll";
        var expectedBeforePDBPath = @"c:\dev\before\other\blah.pdb";
        var expectedAfterBinaryPath = @"c:\dev\after\blah.dll";
        var expectedAfterPDBPath = @"c:\dev\after\other\blah.pdb";

        var mockDiffSession = new Mock<IDiffSession>();
        mockDiffSession.SetupGet(ds => ds.BeforeSession).Returns(new Mock<ISession>().Object);
        mockDiffSession.SetupGet(ds => ds.AfterSession).Returns(new Mock<ISession>().Object);

        this.MockSessionFactory.Setup(sf => sf.CreateDiffSession(expectedBeforeBinaryPath, expectedBeforePDBPath, expectedAfterBinaryPath, expectedAfterPDBPath, It.IsAny<ILogger>()))
                               .Returns(Task.FromResult(mockDiffSession.Object));

        using var container = new WindsorContainer();
        using var appLogger = new TestNoOpApplicationLogger();
        var vm = new MainWindowViewModel(container,
                                         appLogger,
                                         this.MockSessionFactory.Object);
        await vm.TryResolveDeeplink(new Uri($"sizebench://2.0/Test?BeforeBinaryPath={Uri.EscapeDataString(expectedBeforeBinaryPath)}" +
                                            $"&BeforePDBPath={Uri.EscapeDataString(expectedBeforePDBPath)}" +
                                            $"&AfterBinaryPath={Uri.EscapeDataString(expectedAfterBinaryPath)}" +
                                            $"&AfterPDBPath={Uri.EscapeDataString(expectedAfterPDBPath)}"));
        this.MockSessionFactory.Verify(sf => sf.CreateSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never());
        this.MockSessionFactory.Verify(sf => sf.CreateDiffSession(expectedBeforeBinaryPath, expectedBeforePDBPath, expectedAfterBinaryPath, expectedAfterPDBPath, It.IsAny<ILogger>()), Times.Exactly(1));
    }

    [TestMethod]
    public async Task DeeplinkToDiffWithAllFourPathsAndInAppPageGoesDeep()
    {
        var expectedBeforeBinaryPath = @"c:\dev\before\blah.dll";
        var expectedBeforePDBPath = @"c:\dev\before\other\blah.pdb";
        var expectedAfterBinaryPath = @"c:\dev\after\blah.dll";
        var expectedAfterPDBPath = @"c:\dev\after\other\blah.pdb";
        var expectedInAppPage = @"BinarySection/.text";

        var tcsCreateSession = new TaskCompletionSource<IDiffSession>();
        this.MockSessionFactory.Setup(sf => sf.CreateDiffSession(expectedBeforeBinaryPath, expectedBeforePDBPath, expectedAfterBinaryPath, expectedAfterPDBPath, It.IsAny<ILogger>()))
                               .Returns(tcsCreateSession.Task);

        var mockDiffSession = new Mock<IDiffSession>();
        mockDiffSession.Setup(ds => ds.BeforeSession).Returns(new Mock<ISession>().Object);
        mockDiffSession.Setup(ds => ds.AfterSession).Returns(new Mock<ISession>().Object);

        using var container = new WindsorContainer();
        using var appLogger = new TestNoOpApplicationLogger();
        var vm = new MainWindowViewModel(container,
                                         appLogger,
                                         this.MockSessionFactory.Object);

        var deeplinkResolutionTask = vm.TryResolveDeeplink(new Uri($"sizebench://2.0/{Uri.EscapeDataString(expectedInAppPage)}?" +
                                                                    $"BeforeBinaryPath={Uri.EscapeDataString(expectedBeforeBinaryPath)}" +
                                                                    $"&BeforePDBPath={Uri.EscapeDataString(expectedBeforePDBPath)}" +
                                                                    $"&AfterBinaryPath={Uri.EscapeDataString(expectedAfterBinaryPath)}" +
                                                                    $"&AfterPDBPath={Uri.EscapeDataString(expectedAfterPDBPath)}"));

        Assert.AreEqual(0, vm.OpenTabs.Count);
        Assert.IsNull(vm.SelectedTab);

        this.MockSessionFactory.Verify(sf => sf.CreateSession(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never());
        this.MockSessionFactory.Verify(sf => sf.CreateDiffSession(expectedBeforeBinaryPath, expectedBeforePDBPath, expectedAfterBinaryPath, expectedAfterPDBPath, It.IsAny<ILogger>()), Times.Exactly(1));
        tcsCreateSession.SetResult(mockDiffSession.Object);

        await deeplinkResolutionTask;

        Assert.AreEqual(1, vm.OpenTabs.Count);
        Assert.IsTrue(ReferenceEquals(vm.SelectedTab, vm.OpenTabs[0]));
        Assert.IsInstanceOfType(vm.OpenTabs[0], typeof(BinaryDiffTab));
        Assert.AreEqual(new Uri(expectedInAppPage, UriKind.Relative), vm.SelectedTab!.CurrentPage);
    }

    public void Dispose() => this.WindsorContainer.Dispose();
}
