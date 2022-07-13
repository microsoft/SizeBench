using System.ComponentModel;


namespace SizeBench.GUI.Core;

// This was in Nito.AsyncEx until v4, but was removed in v5.  SizeBench uses it a lot,
// so copying in the implementation from Nito.AsyncEx v4's source tree.
/// <summary>
/// A progress implementation that stores progress updates in a property. If this instance is created on a UI thread, its <see cref="Progress"/> property is suitable for data binding.
/// </summary>
/// <typeparam name="T">The type of progress value.</typeparam>
public sealed class PropertyProgress<T> : IProgress<T>, INotifyPropertyChanged
{
    /// <summary>
    /// The context of the thread that created this instance.
    /// </summary>
    private readonly SynchronizationContext _context;

    /// <summary>
    /// The last reported progress value.
    /// </summary>
    private T _progress;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyProgress&lt;T&gt;"/> class.
    /// </summary>
    /// <param name="initialProgress">The initial progress value.</param>
    public PropertyProgress(T initialProgress)
    {
        this._context = SynchronizationContext.Current ?? new SynchronizationContext();
        this._progress = initialProgress;
    }

    // Every time any progress occurs, we don't need to allocate a new args with the same
    // property name every time.  Just one will do.
    private readonly PropertyChangedEventArgs _progressPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(Progress));

    /// <summary>
    /// The last reported progress value.
    /// </summary>
    public T Progress
    {
        get => this._progress;

        private set
        {
            this._progress = value;
            PropertyChanged?.Invoke(this, this._progressPropertyChangedEventArgs);
        }
    }

    void IProgress<T>.Report(T value) => this._context.Post(_ => this.Progress = value, null);

    /// <summary>
    /// Occurs when the property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
}
