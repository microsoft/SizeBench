// Turn this on if you need verbose logging to show up in the tests for DbgX failures.  It can be very noisy, though, so it's disabled by default.
//#define VERBOSE_DBGX_LOGGING

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DbgX;
using DbgX.Interfaces.Structs;

namespace SizeBench.AnalysisEngine.DebuggerInterop;

[ExcludeFromCodeCoverage]
internal class EngineStateController
{
    private readonly MessageQueue<DebugEvent> m_eventQueue = new MessageQueue<DebugEvent>();
    private readonly DebugEngine _engine;

    [Conditional("VERBOSE_DBGX_LOGGING")]
    private static void VerboseTrace(string trace, bool withNewline = true)
    {
        if (withNewline)
        {
            Trace.WriteLine(trace);
        }
        else
        {
            Trace.Write(trace);
        }
    }

    public EngineStateController(DebugEngine engine)
    {
        this._engine = engine;
        this._engine.DmlOutput += OnDmlOutput;
        this._engine.DebuggingState.PropertyChanged += DebuggingState_PropertyChanged;
    }

    public async Task CleanupAsync()
    {
        await StopControllerAsync().ConfigureAwait(true);
        EmptyMessageQueue();

        // We collect here so that all of our COM objects get released.
        // If we don't do this, and shut down the remote/dbgsrv we're connected to,
        // we will cause RPC_E_DISCONNECTED to occur, which .Net doesn't like.
        GC.Collect();
    }

    private Task StopControllerAsync()
    {
        if (this._engine != null)
        {
            return this._engine.ShutdownAsync(20000);
        }
        else
        {
            return Task.CompletedTask;
        }
    }

    private void DebuggingState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DebuggingState.RunningState))
        {
            VerboseTrace("RunningState changed to " + this._engine.DebuggingState.RunningState);
            this.m_eventQueue.AddItem(new ChangeRunningStateEvent { RunningState = this._engine.DebuggingState.RunningState });
        }
        else if (e.PropertyName == nameof(DebuggingState.EngineBusy))
        {
            VerboseTrace("EngineBusy changed");
        }
    }

    public Task WaitForBreakAsync(int timeout = -1)
        => WaitForAsync<ChangeRunningStateEvent>((x) => x.RunningState == RunningState.Stopped, timeout);

    public Task WaitForTerminationAsync(int timeout = -1)
        => WaitForAsync<ChangeRunningStateEvent>((x) => x.RunningState == RunningState.NoTarget, timeout);

    /// <summary>
    /// Waits for an event of the specified type. By default, if we don't see the event in 5 seconds, assume failure.
    /// </summary>
    /// <typeparam name="T">The type of event to look for</typeparam>
    /// <param name="whereClause">An additional predicate to match on, or null to match on any</param>
    /// <param name="timeout">Number of milliseconds to wait before assuming failure</param>
    /// <returns></returns>
    public async Task<T> WaitForAsync<T>(Predicate<T>? whereClause = null, int timeout = -1) where T : DebugEvent
    {
        var elapsedMillis = 0;

        while (true)
        {
            DebugEvent evt;
            while ((evt = this.m_eventQueue.WaitForItem(0)) is null)
            {
                await Task.Delay(10).ConfigureAwait(true);
                elapsedMillis += 10;
                if (timeout != -1 && elapsedMillis >= timeout)
                {
                    throw new TimeoutException("Specified wait timed out after " + timeout + " milliseconds.");
                }
            }

            ProcessEvent(evt);

            if (evt is T evtT)
            {
                if (whereClause is null || whereClause(evtT))
                {
                    return evtT;
                }
            }
        }
    }

    public void EmptyMessageQueue()
    {
        DebugEvent evt;
        while ((evt = this.m_eventQueue.WaitForItem(0)) != null)
        {
            try
            {
                ProcessEvent(evt);
            }
#pragma warning disable CA1031 // Do not catch general exception types - this is shutdown logic, it's best effort
            catch { }
#pragma warning restore CA1031
        }
    }

    private static void ProcessEvent(DebugEvent evt)
    {
        if (evt is OutputEvent outEvt)
        {
            VerboseTrace(outEvt.Text, withNewline: false);
        }
        else
        {
            VerboseTrace(evt.ToString()!);
        }
        if (evt is FailureMessage failure)
        {
            VerboseTrace(failure.ToString());
        }
    }

    public void OnDmlOutput(object? sender, OutputEventArgs e)
    {
        VerboseTrace("OnDmlOutput: " + e.Output);
        this.m_eventQueue.AddItem(new OutputEvent(e.Output));
    }
}

[ExcludeFromCodeCoverage] // Just holds data, no interesting code...
internal class DebugEvent
{

}

[ExcludeFromCodeCoverage] // Just holds data, no interesting code...
internal class OutputEvent : DebugEvent
{
    public string Text;

    public OutputEvent(string text)
    {
        this.Text = text;
    }
}

[ExcludeFromCodeCoverage] // Just holds data, no interesting code...
internal class ChangeRunningStateEvent : DebugEvent
{
    public RunningState RunningState;
    public override string ToString() => "ChangeRunningStateEvent: " + this.RunningState.ToString();
}

[ExcludeFromCodeCoverage] // Just holds data, no interesting code...
internal class FailureMessage : DebugEvent
{
    public string Message;

    public FailureMessage(string message)
    {
        this.Message = message;
    }

    public override string ToString() => "FailureMessage: " + this.Message;
}
