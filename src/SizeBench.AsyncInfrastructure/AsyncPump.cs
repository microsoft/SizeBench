using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AsyncInfrastructure;

// Snagged from Stephen Toub here: http://blogs.msdn.com/b/pfxteam/archive/2012/01/20/10259049.aspx

/// <summary>Provides a pump that supports running asynchronous methods on the current thread.</summary>
[ExcludeFromCodeCoverage]
public static class AsyncPump
{
    /// <summary>Runs the specified asynchronous function.</summary>
    /// <param name="func">The asynchronous function to execute.</param>
    public static void Run(Func<Task> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        var prevCtx = SynchronizationContext.Current;
        using var syncCtx = new SingleThreadSynchronizationContext();
        try
        {
            // Establish the new context
            SynchronizationContext.SetSynchronizationContext(syncCtx);

            // Invoke the function and alert the context to when it completes
            var t = func();
            if (t is null)
            {
                throw new InvalidOperationException("No task provided.");
            }

            t.ContinueWith(delegate { syncCtx.Complete(); }, TaskScheduler.Default);

            // Pump continuations and propagate any exceptions
            syncCtx.RunOnCurrentThread();
            t.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(prevCtx); }
    }

    /// <summary>Provides a SynchronizationContext that's single-threaded.</summary>
    private sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
    {
        /// <summary>The queue of work items.</summary>
        private readonly BlockingCollection<KeyValuePair<SendOrPostCallback, object?>> m_queue =
            new BlockingCollection<KeyValuePair<SendOrPostCallback, object?>>();

        /// <summary>Dispatches an asynchronous message to the synchronization context.</summary>
        /// <param name="d">The System.Threading.SendOrPostCallback delegate to call.</param>
        /// <param name="state">The object passed to the delegate.</param>
        public override void Post(SendOrPostCallback d, object? state)
        {
            ArgumentNullException.ThrowIfNull(d);

            this.m_queue.Add(new KeyValuePair<SendOrPostCallback, object?>(d, state));
        }

        /// <summary>Not supported.</summary>
        public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException("Synchronously sending is not supported.");

        /// <summary>Runs an loop to process all queued work items.</summary>
        public void RunOnCurrentThread()
        {
            foreach (var workItem in this.m_queue.GetConsumingEnumerable())
            {
                workItem.Key(workItem.Value);
            }
        }

        /// <summary>Notifies the context that no more work will arrive.</summary>
        public void Complete() => this.m_queue.CompleteAdding();

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.m_queue.Dispose();
                }

                this.disposedValue = true;
            }
        }

        // Override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~SingleThreadSynchronizationContext()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() =>
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);// Uncomment the following line if the finalizer is overridden above.// GC.SuppressFinalize(this);

        #endregion IDisposable Support
    }
}
