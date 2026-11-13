using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace SizeBench.GUI.Windows;

internal sealed class OpenBinaryDiffWindowViewModel : INotifyPropertyChanged
{
    public OpenBinaryDiffWindowViewModel(SelectSingleBinaryAndPDBControlViewModel before,
                                         SelectSingleBinaryAndPDBControlViewModel after,
                                         SelectSessionOptionsControlViewModel sessionOptions)
    {
        this.Before = before;
        this.Before.PropertyChanged += (s, e) => EnableOKButtonIfReady();
        this.After = after;
        this.After.PropertyChanged += (s, e) => EnableOKButtonIfReady();
        this.SelectSessionOptionsControlViewModel = sessionOptions;
        this.SelectSessionOptionsControlViewModel.PropertyChanged += (s, e) => EnableOKButtonIfReady();
    }

    public SelectSingleBinaryAndPDBControlViewModel Before { get; }
    public SelectSingleBinaryAndPDBControlViewModel After { get; }
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
        var canUseSymbolServer = this.SelectSessionOptionsControlViewModel.UseSymbolServer &&
                                 this.SelectSessionOptionsControlViewModel.HasAnySymbolServerPaths;
        this.OKEnabled = CanOpenBinary(this.Before, canUseSymbolServer) &&
                         CanOpenBinary(this.After, canUseSymbolServer);
    }

    private static bool CanOpenBinary(SelectSingleBinaryAndPDBControlViewModel binary, bool canUseSymbolServer)
        => File.Exists(binary.BinaryPath) &&
           (File.Exists(binary.PDBPath) || (String.IsNullOrEmpty(binary.PDBPath) && canUseSymbolServer));

}
