using System.Reflection;
using System.Windows.Input;

namespace SizeBench.GUI.Commands;

public sealed class DelegateCommand<T> : ICommand
{
    private readonly Action<T> _executeMethod;
    private readonly Func<T, bool> _canExecuteMethod;
    private readonly SynchronizationContext? _synchronizationContext;

    public DelegateCommand(Action<T> executeMethod)
        : this(executeMethod, (o) => true)
    {
    }

    public DelegateCommand(Action<T> executeMethod, Func<T, bool> canExecuteMethod)
        : base()
    {
        var genericTypeInfo = typeof(T).GetTypeInfo();

        // DelegateCommand allows object or Nullable<>.  
        // note: Nullable<> is a struct so we cannot use a class constraint.
        if (genericTypeInfo.IsValueType)
        {
            if ((!genericTypeInfo.IsGenericType) || (!typeof(Nullable<>).GetTypeInfo().IsAssignableFrom(genericTypeInfo.GetGenericTypeDefinition().GetTypeInfo())))
            {
                throw new InvalidCastException("T for DelegateCommand<T> is not an object or Nullable<>");
            }
        }

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

    public void Execute(object? parameter) => this._executeMethod((T)parameter!);

    public bool CanExecute(object? parameter) => this._canExecuteMethod((T)parameter!);
}
