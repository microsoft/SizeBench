using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using Castle.Windsor;
using SizeBench.AnalysisEngine;
using SizeBench.ExcelExporter;
using SizeBench.GUI.Commands;
using SizeBench.GUI.Navigation;

namespace SizeBench.GUI.Core;

internal abstract class TabBase : INotifyPropertyChanged, IUITaskScheduler, IAsyncDisposable
{
    public ISessionWithProgress SessionBase { get; }

    public IWindsorContainer WindsorContainer { get; }

    public TabBase(ISessionWithProgress sessionBase, IWindsorContainer container)
    {
        this.SessionBase = sessionBase ?? throw new ArgumentNullException(nameof(sessionBase));
        this.WindsorContainer = container ?? throw new ArgumentNullException(nameof(container));
        this._currentPage = this.HomePage;
        this.GoHomeCommand = new DelegateCommand(() => this.CurrentPage = this.HomePage);
        this.InitiateNavigationToModelCommand = new DelegateCommand<object>(InitiateNavigationToModelCommand_Execute);
        this.CopyDeeplinkToClipboardCommand = new DelegateCommand(CopyDeeplinkToClipboard);
        this.SessionBase.ProgressReporter = new PropertyProgress<SessionTaskProgress>(new SessionTaskProgress("Starting up...", 0, null));
        if (this.SessionBase is IDiffSession diffSession)
        {
            diffSession.BeforeSession.ProgressReporter = new PropertyProgress<SessionTaskProgress>(new SessionTaskProgress("Starting up...", 0, null));
            diffSession.AfterSession.ProgressReporter = new PropertyProgress<SessionTaskProgress>(new SessionTaskProgress("Starting up...", 0, null));
        }
    }

    protected abstract Uri HomePage { get; }

    public DelegateCommand GoHomeCommand { get; }

    public DelegateCommand<object> InitiateNavigationToModelCommand { get; }

    private void InitiateNavigationToModelCommand_Execute(object model)
    {
        if (this.SessionBase is IDiffSession diffSession)
        {
            this.CurrentPage = DiffModelToUriConverter.ModelToUri(model, diffSession);
        }
        else
        {
            this.CurrentPage = SingleBinaryModelToUriConverter.ModelToUri(model);
        }
    }

    public abstract string CurrentDeeplink { get; }

    public abstract string Header { get; }

    public abstract string ToolTip { get; }

    public abstract string BinaryPathForWindowTitle { get; }

    private string _currentPageTitle = String.Empty;

    public string CurrentPageTitle
    {
        get => this._currentPageTitle;
        set { this._currentPageTitle = value; RaisePropertyChanged(); }
    }

    #region Copy Deeplink To Clipboard Command

    public DelegateCommand CopyDeeplinkToClipboardCommand { get; }

    private void CopyDeeplinkToClipboard()
    {
        try
        {
            // It seems tempting to use Clipboard.SetText, but that API is prone to throwing exceptions if
            // the clipboard is in use in another process - a more robust alternative seems to be using
            // Clipboard.SetDataObject, I think it might do some retry logic internally?
            Clipboard.SetDataObject(this.CurrentDeeplink);

            MessageBox.Show("Deeplink copied to clipboard!", "SizeBench", MessageBoxButton.OK);
        }
        catch (COMException comException)
        {
            if ((uint)comException.HResult == 0x800401D0 /* CLIPBRD_E_CANT_OPEN */)
            {
                MessageBox.Show("Unable to copy deeplink to clipboard.  Clipboard reported CLIPBRD_E_CANT_OPEN", "SizeBench", MessageBoxButton.OK);
            }
            else
            {
                MessageBox.Show($"Unable to copy deeplink to clipboard.  Exception type: {comException.GetType().Name}, HRESULT: 0x{comException.HResult.ToString("X", CultureInfo.InvariantCulture)}", "SizeBench", MessageBoxButton.OK);
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types - if we can't copy a deeplink, it's not worth crying about, we'll move on, and we'll inform the user.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            MessageBox.Show($"Unable to copy deeplink to clipboard.  Exception type: {ex.GetType().Name}, HRESULT: 0x{ex.HResult.ToString("X", CultureInfo.InvariantCulture)}", "SizeBench", MessageBoxButton.OK);
        }
    }

    #endregion Copy Deeplink To Clipboard Command

    private Uri _currentPage;

    public Uri CurrentPage
    {
        get => this._currentPage;
        set
        {
            this._currentPage = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.CurrentDeeplink));
        }
    }

    #region IUITaskScheduler (UI Task, progress reporting, etc...)

    internal abstract class TabWideDialogViewModel : IDisposable
    {
        protected readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public string DialogTitle { get; } = String.Empty;

        public IProgress<SessionTaskProgress>? ProgressReporter { get; protected set; }

        public Task AwaitableTask { get; protected set; } = Task.CompletedTask;

        public DelegateCommand ProgressWindowClosedByUserCommand { get; }

        public TabWideDialogViewModel(string dialogTitle)
        {
            this.DialogTitle = dialogTitle;
            this.ProgressWindowClosedByUserCommand = new DelegateCommand(() =>
            {
                if (!this.IsDisposed)
                {
                    this._cts.Cancel();
                }
            });
        }

        #region IDisposable Support

        public bool IsDisposed { get; private set; }

        private void Dispose(bool disposing)
        {
            if (!this.IsDisposed)
            {
                if (disposing)
                {
                    this._cts.Dispose();
                }

                // free unmanaged resources (unmanaged objects) and override a finalizer below.
                // set large fields to null.

                this.IsDisposed = true;
            }
        }

        // override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~LongRunningUITask()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() =>
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);// uncomment the following line if the finalizer is overridden above.// GC.SuppressFinalize(this);
        #endregion
    }

    internal sealed class SingleBinaryProgressDialogViewModel : TabWideDialogViewModel
    {
        public SingleBinaryProgressDialogViewModel(string taskName, ISession session, Func<CancellationToken, Task> taskCreator) : base(taskName)
        {
            ArgumentNullException.ThrowIfNull(session);

            this.ProgressReporter = session.ProgressReporter;
            this.AwaitableTask = taskCreator(this._cts.Token);
        }
    }

    internal sealed class BinaryDiffProgressDialogViewModel : TabWideDialogViewModel
    {
        public IProgress<SessionTaskProgress>? BeforeProgressReporter { get; }

        public IProgress<SessionTaskProgress>? AfterProgressReporter { get; }

        public BinaryDiffProgressDialogViewModel(string taskName, IDiffSession session, Func<CancellationToken, Task> taskCreator) : base(taskName)
        {
            ArgumentNullException.ThrowIfNull(session);

            this.ProgressReporter = session.ProgressReporter;
            this.BeforeProgressReporter = session.BeforeSession.ProgressReporter;
            this.AfterProgressReporter = session.AfterSession.ProgressReporter;

            this.AwaitableTask = taskCreator(this._cts.Token);
        }
    }

    internal sealed class GenericProgressDialogViewModel : TabWideDialogViewModel
    {
        public GenericProgressDialogViewModel(string taskName, IProgress<SessionTaskProgress> progressReporter, Func<CancellationToken, Task> taskCreator) : base(taskName)
        {
            this.ProgressReporter = progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));
            this.AwaitableTask = taskCreator(this._cts.Token);
        }

    }

    private readonly Stack<TabWideDialogViewModel> _dialogs = new Stack<TabWideDialogViewModel>();

    private TabWideDialogViewModel? _currentlyOpenDialog;
    public TabWideDialogViewModel? CurrentlyOpenDialog
    {
        get => this._currentlyOpenDialog;
        private set
        {
            this._currentlyOpenDialog = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(this.IsAnyTabWideDialogOpen));
        }
    }

    public bool IsAnyTabWideDialogOpen => this.CurrentlyOpenDialog != null;

    private async Task OpenDialog(TabWideDialogViewModel newUITask)
    {
        try
        {
            await AddDialog(newUITask);
        }
        catch (OperationCanceledException) { } // Cancellation isn't something we want to treat as exceptional, users can cancel freely by clicking 'x' in the dialog (if it supports one)
        catch (AggregateException aggEx) when (aggEx.InnerException is OperationCanceledException) { }
    }

    private Task AddDialog(TabWideDialogViewModel dialogVM)
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

        return dialogVM.AwaitableTask.ContinueWith(_ => RemoveDialog(dialogVM), TaskScheduler.Current);
    }

    private void RemoveDialog(TabWideDialogViewModel dialogVM)
    {
        if (this.CurrentlyOpenDialog == dialogVM)
        {
            // We're closing the currently open dialog, so if there's more of them, open the next one in line.
            this.CurrentlyOpenDialog = null;

            while (this._dialogs.Count > 0)
            {
                var nextDialog = this._dialogs.Pop();
                if (!nextDialog.IsDisposed)
                {
                    this.CurrentlyOpenDialog = nextDialog;
                    return;
                }
            }
        }
    }


    public async Task StartLongRunningUITask(string taskName, Func<CancellationToken, Task> taskCreator)
    {
        if (this.SessionBase is ISession singleBinarySession)
        {
            using var dialogViewModel = new SingleBinaryProgressDialogViewModel(taskName, singleBinarySession, taskCreator);
            await OpenDialog(dialogViewModel);
        }
        else if (this.SessionBase is IDiffSession diffSession)
        {
            using var dialogViewModel = new BinaryDiffProgressDialogViewModel(taskName, diffSession, taskCreator);
            await OpenDialog(dialogViewModel);
        }
    }

    public async Task StartExcelExport<T>(IExcelExporter excelExporter, IReadOnlyList<T>? items)
    {
        if (items is null)
        {
            return;
        }

        var progressReporter = new PropertyProgress<SessionTaskProgress>(new SessionTaskProgress("Starting up...", 0, null));
        using var dialogViewModel = new GenericProgressDialogViewModel("Exporting to Excel", progressReporter,
                                                                       (token) => Task.Run(() => excelExporter.ExportToExcel(items, this.SessionBase, this.CurrentDeeplink, this.CurrentPageTitle, progressReporter, token), token));
        await OpenDialog(dialogViewModel);
    }

    public async Task StartExcelExportWithPreformattedData(IExcelExporter excelExporter,
                                                           IList<string> columnHeaders,
                                                           IList<DictionaryThatDoesntThrowWhenKeyNotPresent<object>> preformattedData)
    {
        var progressReporter = new PropertyProgress<SessionTaskProgress>(new SessionTaskProgress("Starting up...", 0, null));
        using var dialogViewModel = new GenericProgressDialogViewModel("Exporting to Excel", progressReporter,
                                                                       (token) => Task.Run(() => excelExporter.ExportToExcelPreformatted(columnHeaders, preformattedData, this.SessionBase, this.CurrentDeeplink, this.CurrentPageTitle, progressReporter, token), token));

        await OpenDialog(dialogViewModel);
    }

    #endregion IUITaskScheduler (UI Task, progress reporting, etc...)

    #region INPC

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion INPC

    #region IAsyncDisposable Support

    private bool _isDisposed; // To detect redundant calls

    protected virtual async Task DisposeAsync(bool disposing)
    {
        if (!this._isDisposed)
        {
            if (disposing)
            {
                await this.SessionBase.DisposeAsync();
            }

            this._isDisposed = true;
        }
    }

    // Override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    // ~TabBase() {
    //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
    //   Dispose(false);
    // }

    // This code added to correctly implement the disposable pattern.
    public async ValueTask DisposeAsync() =>
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        await DisposeAsync(true);// Uncomment the following line if the finalizer is overridden above.// GC.SuppressFinalize(this);

    #endregion IAsyncDisposable Support
}
