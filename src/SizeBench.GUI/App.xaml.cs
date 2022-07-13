using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using Castle.Windsor;
using SizeBench.Logging;

namespace SizeBench.GUI;

[ExcludeFromCodeCoverage] // Testing the Application class is very difficult as it spins up WPF entirely, and we want the tests to be UI-less for speed and reliability if possible
public partial class App : Application, IDisposable
{
    private bool _isShuttingDown;
    private readonly IWindsorContainer _windsorContainer;
    private readonly IApplicationLogger _applicationLogger;

    // This parameterless constructor seems to be required by .NET Core 3.1's WPF implementation, but we don't want to use it because we want to
    // construct the App object via Windsor, so we'll just throw to satisfy WPF's generated code that it exists.
    private App()
    {
        throw new NotImplementedException();
    }

    public App(IWindsorContainer windsorContainer, IApplicationLogger applicationLogger)
    {
        this._windsorContainer = windsorContainer;
        this._applicationLogger = applicationLogger;
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        this.ShutdownMode = ShutdownMode.OnMainWindowClose;

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        this.Dispatcher.BeginInvoke(new Action(() =>
        {
            this.MainWindow = this._windsorContainer.Resolve<Window>("MainWindow");
            this.MainWindow.Show();
        }));
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e.Exception != null)
        {
            TheOneTrueUnhandledExceptionHandler("TaskScheduler.UnobservedTaskException!", e.Exception);
            e.SetObserved();
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TheOneTrueUnhandledExceptionHandler("App.DispatcherUnhandledException!", e.Exception);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception except)
        {
            TheOneTrueUnhandledExceptionHandler("AppDomain UnhandledException!", except);
        }
    }

    private void TheOneTrueUnhandledExceptionHandler(string source, Exception ex)
    {
        // Once we begin shutting down, we will ignore other sources of errors - we're already in a fatal place, no need
        // to report more than that.
        if (this._isShuttingDown)
        {
            return;
        }

        this._isShuttingDown = true;
        this._applicationLogger?.LogException(source, ex);

        if (this.Dispatcher.Thread == Thread.CurrentThread)
        {
            TheOneTrueUnhandledExceptionHandler_OnUIThread(ex);
        }
        else
        {
            this.Dispatcher.BeginInvoke(new Action(() => TheOneTrueUnhandledExceptionHandler_OnUIThread(ex)));
        }
    }

    private void TheOneTrueUnhandledExceptionHandler_OnUIThread(Exception ex)
    {
        var args = new Castle.MicroKernel.Arguments();
        args.AddNamed("fatalException", ex);
        var errorReportWindow = this._windsorContainer.Resolve<Window>("ErrorReportWindow", args);
        errorReportWindow.Owner = this.MainWindow;
        errorReportWindow.ShowDialog();

        Shutdown(-1);
    }

    #region IDisposable Support
    private bool disposedValue; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this._applicationLogger?.Dispose();
                this._windsorContainer.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override a finalizer below.
            // set large fields to null.

            this.disposedValue = true;
        }
    }

    // override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    // ~App()
    // {
    //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
    //   Dispose(false);
    // }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
