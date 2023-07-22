using System.Diagnostics.CodeAnalysis;
using System.Windows;
using SizeBench.GUI.ViewModels;

namespace SizeBench.GUI;

[ExcludeFromCodeCoverage]
internal partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        this.DataContext = viewModel;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        // Cannot await this since it's in a constructor
        if (Program.Deeplink != null)
        {
            viewModel.TryResolveDeeplink(Program.Deeplink);
        }
        else if (Program.CommandLineArgs != null)
        {
            viewModel.TryResolveCommandLineArgs(Program.CommandLineArgs);
        }
#pragma warning restore CS4014
        InitializeComponent();
    }
}
