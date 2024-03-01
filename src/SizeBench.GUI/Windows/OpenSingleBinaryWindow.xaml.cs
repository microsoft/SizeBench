using System.Diagnostics.CodeAnalysis;
using System.Windows;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Windows;

// Purely view code, no good way to test
[ExcludeFromCodeCoverage]
internal partial class OpenSingleBinaryWindow : Window
{
    private readonly OpenSingleBinaryWindowViewModel _viewModel;

    public OpenSingleBinaryWindow(OpenSingleBinaryWindowViewModel viewModel)
    {
        this.DataContext = this._viewModel = viewModel;
        InitializeComponent();
    }

    public string PDBPath => this._viewModel.SelectSingleBinaryAndPDBControlViewModel.PDBPath;
    public string BinaryPath => this._viewModel.SelectSingleBinaryAndPDBControlViewModel.BinaryPath;
    public SessionOptions SessionOptions => this._viewModel.SelectSessionOptionsControlViewModel.SessionOptions;

    private void btnCancel_Click(object sender, RoutedEventArgs e)
        => this.DialogResult = false;

    private void btnOK_Click(object sender, RoutedEventArgs e)
        => this.DialogResult = true;
}
