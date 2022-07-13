using Castle.Windsor;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Core.Tests;

[TestClass]
public sealed class BinaryDiffTabTests : IDisposable
{
    public IWindsorContainer WindsorContainer = new WindsorContainer();
    public Mock<IDiffSession> MockDiffSession = new Mock<IDiffSession>();
    public Mock<ISession> MockBeforeSession = new Mock<ISession>();
    public Mock<ISession> MockAfterSession = new Mock<ISession>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockDiffSession = new Mock<IDiffSession>();
        this.WindsorContainer = new WindsorContainer();
        this.MockBeforeSession = new Mock<ISession>();
        this.MockBeforeSession.SetupGet(s => s.BinaryPath).Returns(@"c:\foo\before\bar.dll");
        this.MockBeforeSession.SetupGet(s => s.PdbPath).Returns(@"c:\foo\before\bar.pdb");

        this.MockAfterSession = new Mock<ISession>();
        this.MockAfterSession.SetupGet(s => s.BinaryPath).Returns(@"c:\foo\after\bar.dll");
        this.MockAfterSession.SetupGet(s => s.PdbPath).Returns(@"c:\foo\after\other\bar.pdb");

        this.MockDiffSession.SetupGet(ds => ds.BeforeSession).Returns(this.MockBeforeSession.Object);
        this.MockDiffSession.SetupGet(ds => ds.AfterSession).Returns(this.MockAfterSession.Object);
    }

    [TestMethod]
    public async Task GoHomeTakesYouBackToDiffSessionPage()
    {
        await using var tab = new BinaryDiffTab(this.MockDiffSession.Object, this.WindsorContainer);

        Assert.AreEqual(new Uri(@"BinaryDiffOverview", UriKind.Relative), tab.CurrentPage);

        // Set the page to literally anywhere else for now, so we can validate that going back home works
        tab.CurrentPage = new Uri(@"Pages\SomewhereElse.xaml", UriKind.Relative);
        Assert.IsTrue(tab.GoHomeCommand.CanExecute());
        tab.GoHomeCommand.Execute();
        Assert.AreEqual(new Uri(@"BinaryDiffOverview", UriKind.Relative), tab.CurrentPage);
    }

    [TestMethod]
    public async Task DeeplinkCalculatedCorrectly()
    {
        await using var tab = new BinaryDiffTab(this.MockDiffSession.Object, this.WindsorContainer)
        {
            CurrentPage = new Uri(@"COFFGroup/.text$mn", UriKind.Relative)
        };

        var expectedDeeplink = $"sizebench://2.0/{Uri.EscapeDataString(tab.CurrentPage.OriginalString)}" +
                                  $"?BeforeBinaryPath={Uri.EscapeDataString(this.MockDiffSession.Object.BeforeSession.BinaryPath)}" +
                                  $"&BeforePDBPath={Uri.EscapeDataString(this.MockDiffSession.Object.BeforeSession.PdbPath)}" +
                                  $"&AfterBinaryPath={Uri.EscapeDataString(this.MockDiffSession.Object.AfterSession.BinaryPath)}" +
                                  $"&AfterPDBPath={Uri.EscapeDataString(this.MockDiffSession.Object.AfterSession.PdbPath)}";

        Assert.IsTrue(tab.CopyDeeplinkToClipboardCommand.CanExecute());
        Assert.AreEqual(expectedDeeplink, tab.CurrentDeeplink);

    }

    [TestMethod]
    public async Task UserCancellationOfProgressWindowCancelsToken()
    {
        await using var tab = new BinaryDiffTab(this.MockDiffSession.Object, this.WindsorContainer);

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
        await using var tab = new BinaryDiffTab(this.MockDiffSession.Object, this.WindsorContainer);

        Assert.AreEqual("bar", tab.Header); // The filename without the extension
        Assert.AreEqual($"c:\\foo\\before\\bar.dll{Environment.NewLine}vs.{Environment.NewLine}c:\\foo\\after\\bar.dll", tab.ToolTip);
        Assert.AreEqual(@"foo\before\bar.dll vs. foo\after\bar.dll", tab.BinaryPathForWindowTitle);

        this.MockBeforeSession.SetupGet(s => s.BinaryPath).Returns(@"c:\experiments\experiment1\before\bar.dll");
        this.MockAfterSession.SetupGet(s => s.BinaryPath).Returns(@"c:\experiments\experiment1\after\bar.dll");
        Assert.AreEqual(@"experiment1\before\bar.dll vs. experiment1\after\bar.dll", tab.BinaryPathForWindowTitle);

        this.MockBeforeSession.SetupGet(s => s.BinaryPath).Returns(@"c:\experiments\experiment1\before\bar.dll");
        this.MockAfterSession.SetupGet(s => s.BinaryPath).Returns(@"x:\experiments\experiment1\after\bar.dll");
        Assert.AreEqual(@"c:\experiments\experiment1\before\bar.dll vs. x:\experiments\experiment1\after\bar.dll", tab.BinaryPathForWindowTitle);
    }

    public void Dispose() => this.WindsorContainer.Dispose();
}
