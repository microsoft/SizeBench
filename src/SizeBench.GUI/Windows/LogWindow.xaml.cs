using System.Diagnostics.CodeAnalysis;
using System.Windows;
using SizeBench.Logging;
using SizeBench.GUI.ViewModels;

namespace SizeBench.GUI.Windows;

// Purely view code, no good way to test
[ExcludeFromCodeCoverage]
public partial class LogWindow : Window
{
    public LogWindow(IApplicationLogger applicationLogger)
    {
        this.DataContext = new LogWindowViewModel(applicationLogger);
        InitializeComponent();
    }
}
