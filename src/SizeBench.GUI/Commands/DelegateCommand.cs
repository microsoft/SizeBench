using System.Windows.Input;

namespace SizeBench.GUI.Commands;

public sealed class DelegateCommand : ICommand
{
    private readonly Action _executeMethod;
    private readonly Func<bool> _canExecuteMethod;
    private readonly SynchronizationContext? _synchronizationContext;

    public DelegateCommand(Action executeMethod)
        : this(executeMethod, () => true)
    {
    }

    public DelegateCommand(Action executeMethod, Func<bool> canExecuteMethod)
        : base()
    {
        this._executeMethod = executeMethod ?? throw new ArgumentNullException(nameof(executeMethod));
        this._canExecuteMethod = canExecuteMethod ?? throw new ArgumentNullException(nameof(canExecuteMethod));
        this._synchronizationContext = SynchronizationContext.Current;
    }

    public event EventHandler? CanExecuteChanged;

    private void OnCanExecuteChanged()
    {
        var handler = CanExecuteChanged;
        if (handler != null)
        {
            if (this._synchronizationContext != null && this._synchronizationContext != SynchronizationContext.Current)
            {
                this._synchronizationContext.Post((o) => handler.Invoke(this, EventArgs.Empty), null);
            }
            else
            {
                handler.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void RaiseCanExecuteChanged() => OnCanExecuteChanged();


    public void Execute(object? parameter) => this._executeMethod();

    public bool CanExecute(object? parameter) => this._canExecuteMethod();

    public void Execute() => this._executeMethod();

    public bool CanExecute() => this._canExecuteMethod();
}
