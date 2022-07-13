using System.Diagnostics.CodeAnalysis;

namespace SizeBench.GUI.Core;

// Implementation copied out of the Silverlight Toolkit
internal sealed class WeakEventListener<TInstance, TSource, TEventArgs> where TInstance : class
{
    /// <summary>
    /// WeakReference to the instance listening for the event.
    /// </summary>
    private readonly WeakReference _weakInstance;

    /// <summary>
    /// Gets or sets the method to call when the event fires.
    /// </summary>
    public Action<TInstance, TSource, TEventArgs>? OnEventAction { get; set; }

    /// <summary>
    /// Gets or sets the method to call when detaching from the event.
    /// </summary>
    public Action<WeakEventListener<TInstance, TSource, TEventArgs>>? OnDetachAction { get; set; }

    /// <summary>
    /// Initializes a new instances of the WeakEventListener class.
    /// </summary>
    /// <param name="instance">Instance subscribing to the event.</param>
    public WeakEventListener([DisallowNull] TInstance? instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        this._weakInstance = new WeakReference(instance);
    }

    /// <summary>
    /// Handler for the subscribed event calls OnEventAction to handle it.
    /// </summary>
    /// <param name="source">Event source.</param>
    /// <param name="eventArgs">Event arguments.</param>
    public void OnEvent(TSource source, TEventArgs eventArgs)
    {
        if (this._weakInstance.Target is TInstance target)
        {
            // Call registered action
            this.OnEventAction?.Invoke(target, source, eventArgs);
        }
        else
        {
            // Detach from event
            Detach();
        }
    }

    /// <summary>
    /// Detaches from the subscribed event.
    /// </summary>
    public void Detach()
    {
        if (null != this.OnDetachAction)
        {
            this.OnDetachAction(this);
            this.OnDetachAction = null;
        }
    }
}
