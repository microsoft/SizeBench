using System.Diagnostics.CodeAnalysis;
using System.IO;
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

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedExeAndPdb(e.Data) is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private static (string, string)? GetDroppedExeAndPdb(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        var files = (data.GetData(DataFormats.FileDrop) as string[])!;

        if (files.Length is < 0 or > 2)
        {
            return null;
        }

        string? exePath = null;
        string? pdbPath = null;
        foreach (var file in files)
        {
            if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                exePath = file;
            }
            else if (file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                pdbPath = file;
            }
        }

        if (exePath is null && pdbPath is null)
        {
            return null;
        }

        if (exePath is not null && pdbPath is not null)
        {
            return (exePath, pdbPath);
        }

        if (files.Length != 1)
        {
            return null;
        }

        if (exePath is not null)
        {
            pdbPath = exePath[..^3] + "pdb";
            if (File.Exists(pdbPath))
            {
                return (exePath, pdbPath);
            }

            return null;
        }

        if (pdbPath is not null)
        {
            exePath = pdbPath[..^3] + "exe";
            if (File.Exists(exePath))
            {
                return (exePath, pdbPath);
            }
        }

        return null;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var exeAndPdb = GetDroppedExeAndPdb(e.Data);
        if (exeAndPdb is null)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var (exePath, pdbPath) = exeAndPdb.Value;
        this._viewModel!.PDBPath = pdbPath;
        this._viewModel!.BinaryPath = exePath;
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }
}
