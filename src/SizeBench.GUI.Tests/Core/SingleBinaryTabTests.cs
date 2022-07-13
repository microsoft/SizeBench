using Castle.Windsor;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Core.Tests;

[TestClass]
public sealed class SingleBinaryTabTests : IDisposable
{
    public IWindsorContainer WindsorContainer = new WindsorContainer();
    public Mock<ISession> MockSession = new Mock<ISession>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.WindsorContainer = new WindsorContainer();
    }

    [TestMethod]
    public async Task GoHomeTakesYouBackToSessionPage()
    {
        await using var tab = new SingleBinaryTab(this.MockSession.Object, this.WindsorContainer);

        Assert.AreEqual(new Uri(@"SingleBinaryOverview", UriKind.Relative), tab.CurrentPage);

        // Set the page to literally anywhere else for now, so we can validate that going back home works
        tab.CurrentPage = new Uri(@"Pages\SomewhereElse.xaml", UriKind.Relative);
        Assert.IsTrue(tab.GoHomeCommand.CanExecute());
        tab.GoHomeCommand.Execute();
        Assert.AreEqual(new Uri(@"SingleBinaryOverview", UriKind.Relative), tab.CurrentPage);
    }

    [TestMethod]
    public async Task CurrentDeeplinkCalculatedCorrectly()
    {
        this.MockSession.SetupGet(s => s.BinaryPath).Returns(@"c:\foo\bar.dll");
        this.MockSession.SetupGet(s => s.PdbPath).Returns(@"c:\foo\bar.pdb");

        await using var tab = new SingleBinaryTab(this.MockSession.Object, this.WindsorContainer)
        {
            CurrentPage = new Uri(@"COFFGroup/.text$mn", UriKind.Relative)
        };

        var expectedDeeplink = $"sizebench://2.0/{Uri.EscapeDataString(tab.CurrentPage.OriginalString)}" +
                                  $"?BinaryPath={Uri.EscapeDataString(this.MockSession.Object.BinaryPath)}" +
                                  $"&PDBPath={Uri.EscapeDataString(this.MockSession.Object.PdbPath)}";

        Assert.IsTrue(tab.CopyDeeplinkToClipboardCommand.CanExecute());
        Assert.AreEqual(expectedDeeplink, tab.CurrentDeeplink);
    }

    [TestMethod]
    public async Task UserCancellationOfProgressWindowCancelsToken()
    {
        await using var tab = new SingleBinaryTab(this.MockSession.Object, this.WindsorContainer);

        Assert.IsNull(tab.CurrentlyOpenDialog);

        await tab.StartLongRunningUITask("test task", async (token) =>
        {
            await Task.Delay(1, token); // Need to force the continuation to process so the Task gets assigned
            Assert.IsNotNull(tab.CurrentlyOpenDialog);
            Assert.IsTrue(tab.CurrentlyOpenDialog.ProgressWindowClosedByUserCommand.CanExecute());
            tab.CurrentlyOpenDialog.ProgressWindowClosedByUserCommand.Execute();
            Assert.IsTrue(token.IsCancellationRequested);
        });
    }

    [TestMethod]
    public async Task HeaderToolTipAndWindowTitleAreUsefulAndSuccinct()
    {
        await using var tab = new SingleBinaryTab(this.MockSession.Object, this.WindsorContainer);

        this.MockSession.SetupGet(s => s.BinaryPath).Returns(@"c:\foo\bar.dll");

        Assert.AreEqual("bar", tab.Header); // The filename without the extension
        Assert.AreEqual(@"c:\foo\bar.dll", tab.ToolTip);
        Assert.AreEqual(@"c:\foo\bar.dll", tab.BinaryPathForWindowTitle);

        this.MockSession.SetupGet(s => s.BinaryPath).Returns(@"c:\experiments\experiment1\before\bar.dll");
        Assert.AreEqual(@"c:\experiments\experiment1\before\bar.dll", tab.BinaryPathForWindowTitle);

        this.MockSession.SetupGet(s => s.BinaryPath).Returns(@"\\share\with\long\path\bar.dll");
        Assert.AreEqual(@"\\share\with\long\path\bar.dll", tab.BinaryPathForWindowTitle);
    }

    [TestMethod]
    public async Task DisposingTabDisposesSessionAndIsIdempotent()
    {
        await using var tab = new SingleBinaryTab(this.MockSession.Object, this.WindsorContainer);
        this.MockSession.Verify((s) => s.DisposeAsync(), Times.Never);

        await tab.DisposeAsync();
        this.MockSession.Verify((s) => s.DisposeAsync(), Times.Once);

        await tab.DisposeAsync();
        this.MockSession.Verify((s) => s.DisposeAsync(), Times.Once); // still once, we can't dispose repeatedly
    }

    public void Dispose() => this.WindsorContainer.Dispose();
}
