using System.IO;
using SizeBench.ErrorReporting;
using SizeBench.ErrorReporting.ErrorInfoProviders;
using SizeBench.Logging;

namespace SizeBench.GUI.Controls.Errors;

internal sealed class ErrorControlViewModel
{
    public string LogFilePath { get; set; }
    private readonly List<string> _openFilePaths = new List<string>();
    public IEnumerable<string> OpenFilePaths => this._openFilePaths;
    public string ErrorDetails { get; }
    public string EmailLink { get; }
    public string LeadingText { get; }

    public ErrorControlViewModel(Exception exception, IApplicationLogger applicationLogger, ISessionFactory sessionFactory, string leadingText)
    {
        this.LeadingText = leadingText;

        this.LogFilePath = Path.GetTempFileName() + ".sizebenchlog.txt";
        using var file = File.CreateText(this.LogFilePath);
        applicationLogger.WriteLog(file);
        file.Flush();
        file.Close();

        foreach (var session in sessionFactory.OpenSessions)
        {
            this._openFilePaths.Add(session.BinaryPath);
            this._openFilePaths.Add(session.PdbPath);
        }
        foreach (var diffSession in sessionFactory.OpenDiffSessions)
        {
            this._openFilePaths.Add(diffSession.BeforeSession.BinaryPath);
            this._openFilePaths.Add(diffSession.BeforeSession.PdbPath);
            this._openFilePaths.Add(diffSession.AfterSession.BinaryPath);
            this._openFilePaths.Add(diffSession.AfterSession.PdbPath);
        }

        this.ErrorDetails = ErrorReport.GetErrorInfo(exception, new List<IErrorInfoProvider>()
            {
                new ExceptionInfoProvider(exception),
                new EnvironmentInfoProvider(),
                new ProcessInfoProvider()
            });

        this.ErrorDetails += "\n\nOpen files (which may be needed to repro the problem):\n";
        foreach (var openFilePath in this.OpenFilePaths)
        {
            this.ErrorDetails += openFilePath + "\n";
        }

        this.EmailLink = "mailto:sizebenchcrash@microsoft.com" +
                        $"?Subject={Uri.EscapeDataString("SizeBench error - " + exception.Hash())}" +
                        $"&Body={Uri.EscapeDataString(this.ErrorDetails)}";
    }
}
