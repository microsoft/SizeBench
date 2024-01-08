using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Web;
using System.Windows;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Castle.Windsor.Installer;
using SizeBench.AnalysisEngine;
using SizeBench.Logging;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Controls.Errors;
using SizeBench.GUI.Core;
using SizeBench.GUI.Windows;
using reg = Castle.MicroKernel.Registration;

namespace SizeBench.GUI.ViewModels;

internal sealed class MainWindowViewModel : INotifyPropertyChanged, IDialogService
{
    private readonly IApplicationLogger _applicationLogger;
    private readonly ISessionFactory _sessionFactory;
    private readonly IWindsorContainer _appWindsorContainer;

    public DelegateCommand<TabBase> CloseTabCommand { get; }

    public ObservableCollection<TabBase> OpenTabs { get; } = new ObservableCollection<TabBase>();

    private TabBase? _selectedTab;

    public TabBase? SelectedTab
    {
        get => this._selectedTab;
        set
        {
            this._selectedTab = value;
            RaisePropertyChanged();
        }
    }

    public bool AreTabsVisible => this.OpenTabs.Count > 0;

    public DelegateCommand OpenSingleBinaryCommand { get; }
    public DelegateCommand OpenBinaryDiffCommand { get; }
    public DelegateCommand ShowLogWindowCommand { get; }
    public DelegateCommand ShowHelpWindowCommand { get; }
    public DelegateCommand ShowAboutBoxCommand { get; }

    #region IDialogService

    private readonly Stack<AppWideDialogViewModel> _dialogs = new Stack<AppWideDialogViewModel>();

    private AppWideDialogViewModel? _currentlyOpenDialog;
    public AppWideDialogViewModel? CurrentlyOpenDialog
    {
        get => this._currentlyOpenDialog;
        private set
        {
            this._currentlyOpenDialog = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.IsAnyAppWideDialogOpen));
        }
    }

    public bool IsAnyAppWideDialogOpen => this.CurrentlyOpenDialog != null;

    private Task AddDialog(AppWideDialogViewModel dialogVM)
    {
        if (this.CurrentlyOpenDialog is null)
        {
            this.CurrentlyOpenDialog = dialogVM;
        }
        else
        {
            // A dialog is already open, so we need to push the current dialog into the background and replace it with this one
            this._dialogs.Push(this.CurrentlyOpenDialog);
            this.CurrentlyOpenDialog = null; // Play the hide animation for the previous dialog
            this.CurrentlyOpenDialog = dialogVM;
        }

        // This looks odd, but trust me, it's important - we need to explicitly rethrow
        // if we fault when doing a ContinueWith - otherwise we won't catch an exception
        // in a place like opening a binary/diff to call into ShowUserDialogWithException.
        return this.CurrentlyOpenDialog.AwaitableTask.ContinueWith(t =>
        {
            RemoveDialog(dialogVM);
            if (t.IsFaulted && t.Exception != null)
            {
                ExceptionDispatchInfo.Capture(t.Exception).Throw();
            }
        }, TaskScheduler.Current);
    }

    private void RemoveDialog(AppWideDialogViewModel dialogVM)
    {
        if (this.CurrentlyOpenDialog == dialogVM)
        {
            // We're closing the currently open dialog, so if there's more of them, open the next one in line.
            this.CurrentlyOpenDialog = null;

            if (this._dialogs.Count > 0)
            {
                this.CurrentlyOpenDialog = this._dialogs.Pop();
            }
        }
    }

    // This version is for when the dialog is cancelable
    public async Task OpenAppWideModalProgressOnlyDialog(string title, string message, bool isCancelable, Func<CancellationToken, Task> task)
    {
        using var newModalDialog = new AppWideModalProgressOnlyDialogViewModel(title, message, isCancelable, task);

        try
        {
            await AddDialog(newModalDialog);
        }
        catch (OperationCanceledException) { } // Cancellation isn't something we want to treat as exceptional, users can cancel freely by clicking 'x' in the dialog (if it supports one)
        catch (AggregateException aggEx) when (aggEx.InnerException is OperationCanceledException) { }
    }

    public Task OpenAppWideModalMessageDialog(string title, string message) =>
        OpenAppWideModalUserInteractionRequiredDialog(new AppWideModalMessageDialogViewModel(title, message));

    public Task OpenAppWideModalErrorDialog(string title, string leadingText, Exception exception) =>
        OpenAppWideModalUserInteractionRequiredDialog(new AppWideModalErrorDialogViewModel(title, new ErrorControlViewModel(exception, this._applicationLogger, this._sessionFactory, leadingText)));

    private async Task OpenAppWideModalUserInteractionRequiredDialog(AppWideModalUserInteractionRequiredDialogViewModel newModalDialog)
    {
        try
        {
            await AddDialog(newModalDialog);
        }
        catch (OperationCanceledException) { } // Cancellation isn't something we want to treat as exceptional, users can cancel freely by clicking 'x' in the dialog (if it supports one)
        catch (AggregateException aggEx) when (aggEx.InnerException is OperationCanceledException) { }
    }

    #endregion IDialogService

    #region Deeplinking

    public async Task TryResolveDeeplink(Uri deeplink)
    {
        using var deeplinkLog = this._applicationLogger.StartTaskLog("Trying to resolve deeplink");
        deeplinkLog.Log($"deeplink = {deeplink}");

        if (deeplink.Scheme.ToUpperInvariant() != "SIZEBENCH")
        {
            return;
        }

        var deeplinkVersion = new Version(deeplink.Host);

        // We can support versioning of the deeplink format here, but for now it's simple and just
        // one version is supported.
        if (deeplinkVersion.Major != 2 || deeplinkVersion.Minor != 0)
        {
            return;
        }

        if (String.IsNullOrEmpty(deeplink.AbsolutePath))
        {
            return;
        }

        var queryString = HttpUtility.ParseQueryString(deeplink.Query);
        var inAppPage = Uri.UnescapeDataString(deeplink.AbsolutePath.StartsWith('/') ? deeplink.AbsolutePath[1..] : deeplink.AbsolutePath);

        deeplinkLog.Log($"Query = {deeplink.Query}");
        deeplinkLog.Log($"inAppPage = {inAppPage}");

        var beforeBinaryPath = queryString["BeforeBinaryPath"];

        if (beforeBinaryPath != null)
        {
            await CheckForDeeplinkToDiff(inAppPage, queryString, deeplinkLog);
        }

        var binaryPath = queryString["BinaryPath"];

        if (binaryPath != null)
        {
            await CheckForDeeplinkToSingleBinary(inAppPage, queryString, deeplinkLog);
        }
    }

    private async Task CheckForDeeplinkToSingleBinary(string inAppPage, NameValueCollection queryString, ILogger deeplinkLog)
    {
        var binaryPath = queryString["BinaryPath"];
        var pdbPath = queryString["PDBPath"];

        deeplinkLog.Log($"binaryPath = {binaryPath ?? "null"}");
        deeplinkLog.Log($"pdbPath = {pdbPath ?? "null"}");

        if (binaryPath is null || pdbPath is null)
        {
            return;
        }

        var session = await OpenSessionFromBinaryPathAndPDBPath(deeplinkLog, binaryPath, pdbPath);

        // session can be null if the deeplink fails to open the binary/pdb (like, say, it's not present at the location anymore).
        if (session is null)
        {
            return;
        }

        CreateNewSingleBinaryTab(session);

        // We know that the first tab will be the deeplink because we only support checking for deeplinks at app launch (so there can't be any other tabs yet) and we don't support deeplinking
        // to multiple-open-tabs.  So OpenTabs[0] here is always the right one - and SelectedTab won't yet have updated, so though it looks tempting it's not.
        if (inAppPage != null)
        {
            deeplinkLog.Log($"Navigating to deeplink page {inAppPage}");
            this.OpenTabs[0].CurrentPage = new Uri(inAppPage, UriKind.Relative);
        }
    }

    private async Task CheckForDeeplinkToDiff(string inAppPage, NameValueCollection queryString, ILogger deeplinkLog)
    {
        var beforeBinaryPath = queryString["BeforeBinaryPath"];
        var beforePdbPath = queryString["BeforePDBPath"];
        var afterBinaryPath = queryString["AfterBinaryPath"];
        var afterPdbPath = queryString["AfterPDBPath"];

        deeplinkLog.Log($"beforeBinaryPath = {beforeBinaryPath ?? "null"}");
        deeplinkLog.Log($"beforePdbPath = {beforePdbPath ?? "null"}");
        deeplinkLog.Log($"afterBinaryPath = {afterBinaryPath ?? "null"}");
        deeplinkLog.Log($"afterPdbPath = {afterPdbPath ?? "null"}");

        if (beforeBinaryPath is null || beforePdbPath is null ||
            afterBinaryPath is null || afterPdbPath is null)
        {
            return;
        }

        var diffSession = await OpenDiffSessionFromBinaryPathsAndPDBPaths(deeplinkLog, beforeBinaryPath, beforePdbPath, afterBinaryPath, afterPdbPath);

        // diffSession can be null if the deeplink fails to open the binary/pdb (like, say, it's not present at the location anymore).
        if (diffSession is null)
        {
            return;
        }

        CreateNewDiffTab(diffSession);

        // We know that the first tab will be the deeplink because we only support checking for deeplinks at app launch (so there can't be any other tabs yet) and we don't support deeplinking
        // to multiple-open-tabs.  So OpenTabs[0] here is always the right one - and SelectedTab won't yet have updated, so though it looks tempting it's not.
        if (inAppPage != null)
        {
            deeplinkLog.Log($"Navigating to deeplink page {inAppPage}");
            this.OpenTabs[0].CurrentPage = new Uri(inAppPage, UriKind.Relative);
        }
    }

    #endregion Deeplinking

    public MainWindowViewModel(IWindsorContainer container,
                               IApplicationLogger applicationLogger,
                               ISessionFactory sessionFactory)
    {
        this._applicationLogger = applicationLogger;
        this._appWindsorContainer = container;
        this._sessionFactory = sessionFactory;
        this.CloseTabCommand = new DelegateCommand<TabBase>(CloseTab);
        this.OpenSingleBinaryCommand = new DelegateCommand(OpenSingleBinaryCommand_Executed);
        this.OpenBinaryDiffCommand = new DelegateCommand(OpenBinaryDiffCommand_Executed);
        this.ShowLogWindowCommand = new DelegateCommand(ShowLogWindow);
        this.ShowHelpWindowCommand = new DelegateCommand(ShowHelpWindow);
        this.ShowAboutBoxCommand = new DelegateCommand(ShowAboutBox);
    }

    private async void CloseTab(TabBase tabToClose)
    {
        //TODO: consider having an "are you sure you want to close?" dialog, since these tabs have *a lot* of state and can take a while to process data...would be dumb if they closed too easily and you had to redo a 20-minute-long analysis
        this.OpenTabs.Remove(tabToClose);
        await tabToClose.DisposeAsync();
        if (this.OpenTabs.Count == 0)
        {
            this.SelectedTab = null;
            RaisePropertyChanged(nameof(this.AreTabsVisible));
        }
    }

    public void CreateNewSingleBinaryTab(ISession session)
    {
        var container = new WindsorContainer();
        var newTab = new SingleBinaryTab(session, container);

        container.Install(FromAssembly.InDirectory(new AssemblyFilter(".", "SizeBench.*")));
        container.Register(reg.Component.For<ISession, ISessionWithProgress>()
                                        .Instance(session)
                                        .LifestyleSingleton());
        container.Register(reg.Component.For<SingleBinaryTab, TabBase, IUITaskScheduler>()
                                        .Instance(newTab)
                                        .LifestyleSingleton());

        this.OpenTabs.Add(newTab);

        // If this is the first tab, let's select it - the UI would probbly just bind and do this anyway, but this makes test code simpler.
        if (this.OpenTabs.Count == 1)
        {
            this.SelectedTab = this.OpenTabs[0];
        }

        RaisePropertyChanged(nameof(this.AreTabsVisible));
    }

    public void CreateNewDiffTab(IDiffSession diffSession)
    {
        var container = new WindsorContainer();
        var newTab = new BinaryDiffTab(diffSession, container);

        container.Install(FromAssembly.InDirectory(new AssemblyFilter(".", "SizeBench.*")));
        container.Register(reg.Component.For<IDiffSession, ISessionWithProgress>()
                                        .Instance(diffSession)
                                        .LifestyleSingleton());
        container.Register(reg.Component.For<BinaryDiffTab, TabBase, IUITaskScheduler>()
                                        .Instance(newTab)
                                        .LifestyleSingleton());

        this.OpenTabs.Add(newTab);

        // If this is the first tab, let's select it - the UI would probably just bind and do this anyway, but this makes test code simpler.
        if (this.OpenTabs.Count == 1)
        {
            this.SelectedTab = this.OpenTabs[0];
        }

        RaisePropertyChanged(nameof(this.AreTabsVisible));
    }

    // This is hard to test because it pops UI
    [ExcludeFromCodeCoverage]
    private async void OpenSingleBinaryCommand_Executed()
    {
        using var taskLog = this._applicationLogger.StartTaskLog("Open Single Binary Command Executed");
        taskLog.Log("OpenSingleBinary command execution started.");

        var window = this._appWindsorContainer.Resolve<OpenSingleBinaryWindow>(nameof(OpenSingleBinaryWindow));
        window.Owner = Application.Current.MainWindow;

        if (window.ShowDialog() == true)
        {
            var session = await OpenSessionFromBinaryPathAndPDBPath(taskLog, window.BinaryPath, window.PDBPath);

            if (session != null) // We can get null if the session fails to open, say due to a DIA error, corrupt PDB, etc.
            {
                CreateNewSingleBinaryTab(session);
            }
        }
        else
        {
            taskLog.Log("OpenSingleBinary command canceled.");
        }

        taskLog.Log("OpenSingleBinary command execution ended.");
    }

    private async Task<ISession?> OpenSessionFromBinaryPathAndPDBPath(ILogger taskLog, string binaryPath, string pdbPath)
    {
        taskLog.Log($"Creating Session for {binaryPath}, {pdbPath}");

        ISession? session = null;
        try
        {
            await OpenAppWideModalProgressOnlyDialog(
                                  "Opening binary",
                                  $"Opening {binaryPath}" + Environment.NewLine +
                                  Environment.NewLine +
                                  "This operation can take a little bit if the PDB or binary are large and over a network connection.",
                                  isCancelable: false,
                                  task: async (token) => session = await this._sessionFactory.CreateSession(binaryPath, pdbPath, this._applicationLogger.CreateSessionLog(binaryPath)));
        }
#pragma warning disable CA1031 // Do not catch general exception types - it's better to show an error to a user than crash the whole app if anything throws when opening a session
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            await ShowUserDialogWithException(taskLog, ex, "There was an error opening this binary or PDB.", "Error opening binary!");

            return null;
        }

        taskLog.Log($"Session creation complete for {binaryPath}, {pdbPath}");
        return session;
    }

    // This is hard to test because it pops UI
    [ExcludeFromCodeCoverage]
    private async void OpenBinaryDiffCommand_Executed()
    {
        using var taskLog = this._applicationLogger.StartTaskLog("Open Binary Diff Command Executed");
        taskLog.Log("OpenBinaryDiff command execution started.");

        var window = this._appWindsorContainer.Resolve<OpenBinaryDiffWindow>(nameof(OpenBinaryDiffWindow));
        window.Owner = Application.Current.MainWindow;

        if (window.ShowDialog() == true)
        {
            var diffSession = await OpenDiffSessionFromBinaryPathsAndPDBPaths(taskLog,
                                                                                        window.BeforeBinaryPath, window.BeforePDBPath,
                                                                                        window.AfterBinaryPath, window.AfterPDBPath);

            // We can get null if the diff session fails to open, say due to a DIA error, corrupt PDB, etc.
            if (diffSession != null)
            {
                CreateNewDiffTab(diffSession);
            }
        }
        else
        {
            taskLog.Log("OpenBinaryDiff command canceled.");
        }

        taskLog.Log("OpenBinaryDiff command execution ended.");
    }

    private async Task<IDiffSession?> OpenDiffSessionFromBinaryPathsAndPDBPaths(ILogger taskLog, string beforeBinaryPath, string beforePdbPath, string afterBinaryPath, string afterPdbPath)
    {
        taskLog.Log($"Creating Diff Session for Before=({beforeBinaryPath}, {beforePdbPath}), After=({afterBinaryPath}, {afterPdbPath})");
        IDiffSession? diffSession = null;
        try
        {
            await OpenAppWideModalProgressOnlyDialog(
                                  "Opening diff",
                                  "This operation can take a little bit if the PDBs are large and over a network connection.",
                                  isCancelable: false,
                                  task: async (token) => diffSession = await this._sessionFactory.CreateDiffSession(beforeBinaryPath, beforePdbPath, afterBinaryPath, afterPdbPath, this._applicationLogger.CreateSessionLog($"{beforeBinaryPath} vs. {afterBinaryPath}")));
        }
#pragma warning disable CA1031 // Do not catch general exception types - it's better to show the user an error than to crash, if anything should throw when opening a diff
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            await ShowUserDialogWithException(taskLog, ex, "There was an error opening this diff.", "Error opening diff!");

            return null;
        }

        taskLog.Log($"Diff Session creation complete for Before=({beforeBinaryPath}, {beforePdbPath}), After=({afterBinaryPath}, {afterPdbPath})");
        return diffSession;
    }

    private static Exception? ExtractExceptionNotWorthErrorReportingIfPossible(Exception ex)
    {
        if (ex is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                var possiblyMeaningful = ExtractExceptionNotWorthErrorReportingIfPossible(innerException);
                if (possiblyMeaningful != null)
                {
                    return possiblyMeaningful;
                }
            }
        }
        else if (ex is PDBNotSuitableForAnalysisException or
                       BinaryNotAnalyzableException or
                       BinaryAndPDBSignatureMismatchException)
        {
            return ex;
        }
        else if (ex.InnerException != null)
        {
            return ExtractExceptionNotWorthErrorReportingIfPossible(ex.InnerException);
        }

        return null;
    }

    private static Exception UnwrapDegenerateAggregateExceptions(Exception ex)
    {
        if (ex is AggregateException aggregateException &&
            aggregateException.InnerExceptions.Count == 1)
        {
            return UnwrapDegenerateAggregateExceptions(aggregateException.InnerExceptions[0]);
        }
        else
        {
            return ex;
        }
    }

    private Task ShowUserDialogWithException(ILogger taskLog, Exception ex, string messagePrefix, string dialogCaption)
    {
        taskLog.LogException(dialogCaption, ex);

        // When we rethrow the exception using ExceptionDispatchInfo to get it out of the ContinueWith call, it gets wrapped in an AggregateException that is
        // not helpful, so if we see an AggregateException with just one InnerException we'll unwrap that.
        var unwrappedException = UnwrapDegenerateAggregateExceptions(ex);
        var exceptionNotWorthErrorReporting = ExtractExceptionNotWorthErrorReportingIfPossible(unwrappedException);

        if (exceptionNotWorthErrorReporting != null)
        {
            var dialogContents = messagePrefix + Environment.NewLine +
                                 "If you need help please e-mail SizeBenchTeam@microsoft.com" + Environment.NewLine +
                                 Environment.NewLine +
                                 exceptionNotWorthErrorReporting!.GetType().Name + ": " + exceptionNotWorthErrorReporting.Message;

            return OpenAppWideModalMessageDialog(dialogCaption, dialogContents);
        }
        else
        {
            return OpenAppWideModalErrorDialog(dialogCaption, messagePrefix, unwrappedException);
        }
    }

    private void ShowLogWindow()
    {
        var window = new LogWindow(this._applicationLogger)
        {
            Owner = Application.Current.MainWindow
        };

        window.Show();
    }

    private void ShowHelpWindow()
    {
        var helpStartingPageUri = new Uri(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, @"Help\index.html"));
        Process.Start(new ProcessStartInfo()
        {
            FileName = helpStartingPageUri.AbsoluteUri,
            UseShellExecute = true
        });
    }

    private void ShowAboutBox() => new AboutBox(Application.Current.MainWindow).ShowDialog();

    #region INPC

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion INPC
}
