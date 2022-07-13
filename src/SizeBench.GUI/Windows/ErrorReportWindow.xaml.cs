using System.Diagnostics.CodeAnalysis;
using System.Windows;
using SizeBench.Logging;
using SizeBench.GUI.ViewModels;

namespace SizeBench.GUI.Windows;

// Purely view code, no good way to test
[ExcludeFromCodeCoverage]
public partial class ErrorReportWindow : Window
{
    public ErrorReportWindow(Exception fatalException, IApplicationLogger applicationLogger, ISessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(fatalException);
        ArgumentNullException.ThrowIfNull(applicationLogger);
        ArgumentNullException.ThrowIfNull(sessionFactory);

        this.DataContext = new ErrorReportWindowViewModel(fatalException, applicationLogger, sessionFactory);
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        this.HideMinimizeAndMaximizeFromTitleBar();
        base.OnSourceInitialized(e);
    }
}
