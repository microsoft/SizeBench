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
        this.OKEnabled = File.Exists(this.SelectSingleBinaryAndPDBControlViewModel.PDBPath) &&
                         File.Exists(this.SelectSingleBinaryAndPDBControlViewModel.BinaryPath);
    }

}
