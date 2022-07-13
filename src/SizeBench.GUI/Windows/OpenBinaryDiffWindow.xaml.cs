using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace SizeBench.GUI.Windows;

// Purely view code, no good way to test
[ExcludeFromCodeCoverage]
internal partial class OpenBinaryDiffWindow : Window
{
    private readonly OpenBinaryDiffWindowViewModel _viewModel;

    public OpenBinaryDiffWindow(OpenBinaryDiffWindowViewModel viewModel)
    {
        this.DataContext = this._viewModel = viewModel;
        InitializeComponent();
    }

    public string BeforePDBPath => this._viewModel.Before.PDBPath;
    public string BeforeBinaryPath => this._viewModel.Before.BinaryPath;
    public string AfterPDBPath => this._viewModel.After.PDBPath;
    public string AfterBinaryPath => this._viewModel.After.BinaryPath;

    private void btnOK_Click(object sender, RoutedEventArgs e)
        => this.DialogResult = true;
}
