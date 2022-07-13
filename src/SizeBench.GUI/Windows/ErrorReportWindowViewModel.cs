using SizeBench.Logging;
using SizeBench.GUI.Controls.Errors;

namespace SizeBench.GUI.ViewModels;

internal class ErrorReportWindowViewModel
{
    public ErrorControlViewModel ErrorControlViewModel { get; }

    public ErrorReportWindowViewModel(Exception fatalException, IApplicationLogger applicationLogger, ISessionFactory sessionFactory)
    {
        this.ErrorControlViewModel = new ErrorControlViewModel(fatalException, applicationLogger, sessionFactory,
                                                               "SizeBench has encountered a fatal error and needs to shut down.  It would help a lot if you're willing to send an e-mail with more details on the error you've encountered.");
    }
}
