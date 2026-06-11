using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace SizeBench.GUI.Windows;

internal sealed class OpenSingleBinaryWindowViewModel : INotifyPropertyChanged
{
    public OpenSingleBinaryWindowViewModel(SelectSingleBinaryAndPDBControlViewModel ssbandPDBViewModel,
                                           SelectSessionOptionsControlViewModel ssoViewModel)
    {
        this.SelectSingleBinaryAndPDBControlViewModel = ssbandPDBViewModel;
        this.SelectSingleBinaryAndPDBControlViewModel.PropertyChanged += (s, e) => EnableOKButtonIfReady();

        this.SelectSessionOptionsControlViewModel = ssoViewModel;
        this.SelectSessionOptionsControlViewModel.PropertyChanged += (s, e) => EnableOKButtonIfReady();
    }

    public SelectSingleBinaryAndPDBControlViewModel SelectSingleBinaryAndPDBControlViewModel { get; }

    public SelectSessionOptionsControlViewModel SelectSessionOptionsControlViewModel { get; }

    #region INPC

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaiseOnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

    private bool _okEnabled;
    public bool OKEnabled
    {
        get => this._okEnabled;
        set
        {
            if (this._okEnabled != value)
            {
                this._okEnabled = value;
                RaiseOnPropertyChanged();
            }
        }
    }

    private void EnableOKButtonIfReady()
    {
        if (!File.Exists(this.SelectSingleBinaryAndPDBControlViewModel.BinaryPath))
        {
            this.OKEnabled = false;
            return;
        }

        if (File.Exists(this.SelectSingleBinaryAndPDBControlViewModel.PDBPath))
        {
            this.OKEnabled = true;
            return;
        }

        // No explicit PDB on disk - allow proceeding only when the symbol server fallback is configured
        // with at least one path, so that DIA has somewhere to look for the matching PDB.
        this.OKEnabled = this.SelectSessionOptionsControlViewModel.UseSymbolServer &&
                         this.SelectSessionOptionsControlViewModel.HasAnySymbolServerPaths;
    }

}
