using System.ComponentModel;
using System.Runtime.CompilerServices;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Windows;

public sealed class SelectSessionOptionsControlViewModel : INotifyPropertyChanged
{
    public SymbolSourcesSupported SymbolSourcesSupported { get; private set; } = SymbolSourcesSupported.All;

    public SessionOptions SessionOptions => new SessionOptions() { SymbolSourcesSupported = this.SymbolSourcesSupported };

    public bool CodeSymbolsSupported
    {
        get => this.SymbolSourcesSupported.HasFlag(SymbolSourcesSupported.Code);
        set => SetFlagOnSymbolsSupported(SymbolSourcesSupported.Code, value);
    }

    public bool DataSymbolsSupported
    {
        get => this.SymbolSourcesSupported.HasFlag(SymbolSourcesSupported.DataSymbols);
        set => SetFlagOnSymbolsSupported(SymbolSourcesSupported.DataSymbols, value);
    }

    public bool PDATASymbolsSupported
    {
        get => this.SymbolSourcesSupported.HasFlag(SymbolSourcesSupported.PDATA);
        set => SetFlagOnSymbolsSupported(SymbolSourcesSupported.PDATA, value);
    }

    public bool XDATASymbolsSupported
    {
        get => this.SymbolSourcesSupported.HasFlag(SymbolSourcesSupported.XDATA);
        set => SetFlagOnSymbolsSupported(SymbolSourcesSupported.XDATA, value);
    }

    public bool RSRCSymbolsSupported
    {
        get => this.SymbolSourcesSupported.HasFlag(SymbolSourcesSupported.RSRC);
        set => SetFlagOnSymbolsSupported(SymbolSourcesSupported.RSRC, value);
    }

    public bool OtherPESymbolsSupported
    {
        get => this.SymbolSourcesSupported.HasFlag(SymbolSourcesSupported.OtherPESymbols);
        set => SetFlagOnSymbolsSupported(SymbolSourcesSupported.OtherPESymbols, value);
    }

    private void SetFlagOnSymbolsSupported(SymbolSourcesSupported flag, bool flagValue)
    {
        if (flagValue)
        {
            this.SymbolSourcesSupported |= flag;
        }
        else
        {
            this.SymbolSourcesSupported &= ~flag;
        }

        RaiseOnPropertyChanged(String.Empty);
    }

    #region INPC

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaiseOnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion

}
