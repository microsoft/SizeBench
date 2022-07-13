using SizeBench.GUI.Commands;
using SizeBench.GUI.Controls.Errors;

namespace SizeBench.GUI;

internal abstract class AppWideDialogViewModel
{
    public string DialogTitle { get; } = String.Empty;

    public bool IsDialogClosable { get; protected set; }

    public DelegateCommand DialogClosedByUserCommand { get; protected set; }

    public Task AwaitableTask { get; protected set; } = Task.CompletedTask;

    public AppWideDialogViewModel(string dialogTitle)
    {
        this.DialogTitle = dialogTitle;
        this.DialogClosedByUserCommand = new DelegateCommand(() => { });
    }
}

internal sealed class AppWideModalProgressOnlyDialogViewModel : AppWideDialogViewModel, IDisposable
{
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    public string DialogMessage { get; } = String.Empty;

    public AppWideModalProgressOnlyDialogViewModel(string title, string message, bool isCancelable, Func<CancellationToken, Task> taskCreator) : base(title)
    {
        this.DialogMessage = message;
        this.IsDialogClosable = isCancelable;
        this.DialogClosedByUserCommand = new DelegateCommand(() => this._cts.Cancel());
        this.AwaitableTask = taskCreator(this._cts.Token);
    }

    #region IDisposable Support
    private bool disposedValue; // To detect redundant calls

    private void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this._cts.Dispose();
            }

            this.disposedValue = true;
        }
    }

    // override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    // ~ModalProgressOnlyDialogViewModel()
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

internal abstract class AppWideModalUserInteractionRequiredDialogViewModel : AppWideDialogViewModel
{
    private readonly TaskCompletionSource<object?> _tcs = new TaskCompletionSource<object?>();

    // The only user interaction dialog we have today just uses "OK" on the button - could make this a parameter for future dialogs, but good enough for now.
#pragma warning disable CA1822 // Member PrimaryButtonText does not access instance data and can be marked as static - No it can't be, Code Analysis...it's a property so we can data-bind to it!
    public string PrimaryButtonText => "OK";
#pragma warning restore CA1822

    public DelegateCommand PrimaryButtonCommand { get; }

    public AppWideModalUserInteractionRequiredDialogViewModel(string title) : base(title)
    {
        this.AwaitableTask = this._tcs.Task;
        this.PrimaryButtonCommand = new DelegateCommand(() => this._tcs.TrySetResult(new object()));
        this.DialogClosedByUserCommand = new DelegateCommand(() => this._tcs.TrySetResult(new object()));
    }
}

internal sealed class AppWideModalMessageDialogViewModel : AppWideModalUserInteractionRequiredDialogViewModel
{
    public string DialogMessage { get; } = String.Empty;

    public AppWideModalMessageDialogViewModel(string title, string message) : base(title)
    {
        this.DialogMessage = message;
    }
}

internal sealed class AppWideModalErrorDialogViewModel : AppWideModalUserInteractionRequiredDialogViewModel
{
    public ErrorControlViewModel ErrorControlViewModel { get; }

    public AppWideModalErrorDialogViewModel(string title, ErrorControlViewModel errorControlViewModel) : base(title)
    {
        this.ErrorControlViewModel = errorControlViewModel;
    }
}
