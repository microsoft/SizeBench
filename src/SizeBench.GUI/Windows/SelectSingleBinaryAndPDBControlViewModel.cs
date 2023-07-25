using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using SizeBench.LocalBuild;
using SizeBench.PathLocators;

namespace SizeBench.GUI.Windows;

public sealed class SelectSingleBinaryAndPDBControlViewModel : INotifyPropertyChanged
{
    private readonly IBinaryLocator[] _allLocators;

    public SelectSingleBinaryAndPDBControlViewModel(IBinaryLocator[] allLocators)
    {
        if ((allLocators == null) || (allLocators.Length == 0))
        {
            allLocators = new[] { new LocalBuildPathLocator() };
        }

        this._allLocators = allLocators;
    }

    private string _pdbPath = String.Empty;
    public string PDBPath
    {
        get => this._pdbPath;
        set
        {
            this._pdbPath = value;
            RaiseOnPropertyChanged();

            if (String.IsNullOrEmpty(this._binaryPath))
            {
                InferBinaryPathFromPDBPathIfPossible();
            }
        }
    }

    private string _binaryPath = String.Empty;
    public string BinaryPath
    {
        get => this._binaryPath;
        set
        {
            this._binaryPath = value;
            RaiseOnPropertyChanged();

            if (String.IsNullOrEmpty(this._pdbPath))
            {
                InferPDBPathFromBinaryIfPossible();
            }
        }
    }

    private void InferBinaryPathFromPDBPathIfPossible()
    {
        foreach (var locator in this._allLocators)
        {
            if (locator.TryInferBinaryPathFromPDBPath(this.PDBPath, out var binaryPath) &&
                File.Exists(binaryPath))
            {
                this.BinaryPath = binaryPath;
            }
        }
    }

    private void InferPDBPathFromBinaryIfPossible()
    {
        foreach (var locator in  this._allLocators)
        {
            if (locator.TryInferPDBPathFromBinaryPath(this.BinaryPath, out var pdbPath) &&
                File.Exists(pdbPath))
            {
                this.PDBPath = pdbPath;
            }
        }
    }

    #region INPC

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaiseOnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

}
