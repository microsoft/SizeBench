using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace SizeBench.GUI.Windows;

internal sealed class OpenBinaryDiffWindowViewModel : INotifyPropertyChanged
{
    public OpenBinaryDiffWindowViewModel(SelectSingleBinaryAndPDBControlViewModel before,
                                         SelectSingleBinaryAndPDBControlViewModel after)
    {
        this.Before = before;
        this.Before.PropertyChanged += (s, e) => EnableOKButtonIfReady();
        this.After = after;
        this.After.PropertyChanged += (s, e) => EnableOKButtonIfReady();
    }

    public SelectSingleBinaryAndPDBControlViewModel Before { get; }
    public SelectSingleBinaryAndPDBControlViewModel After { get; }

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
        this.OKEnabled = File.Exists(this.Before.PDBPath) &&
                         File.Exists(this.Before.BinaryPath) &&
                         File.Exists(this.After.PDBPath) &&
                         File.Exists(this.After.BinaryPath);
    }

}
