using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SizeBench.GUI.Windows;

// This is purely view code, there's no good way to test this currently
[ExcludeFromCodeCoverage]
public partial class SelectSingleBinaryAndPDBControl : UserControl
{
    private SelectSingleBinaryAndPDBControlViewModel? _viewModel;

    public SelectSingleBinaryAndPDBControl()
    {
        DataContextChanged += SelectSingleBinaryAndPDBControl_DataContextChanged;
        InitializeComponent();
    }

    private void SelectSingleBinaryAndPDBControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        => this._viewModel = this.DataContext as SelectSingleBinaryAndPDBControlViewModel;

    private void btnPDBPathBrowse_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog()
        {
            Title = "Select PDB",
            DefaultExt = ".pdb",
            Filter = "PDB files (.pdb)|*.pdb",
            CheckFileExists = true,
            CheckPathExists = true
        };

        var result = ofd.ShowDialog();

        if (result == true)
        {
            this._viewModel!.PDBPath = ofd.FileName;
        }
    }

    private void btnBinaryPathBrowse_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog()
        {
            Title = "Select Binary",
            Filter = "Binary files|*.dll;*.exe;*.efi;*.sys;*.pyd",
            CheckFileExists = true,
            CheckPathExists = true
        };

        var result = ofd.ShowDialog();

        if (result == true)
        {
            this._viewModel!.BinaryPath = ofd.FileName;
        }
    }
}
