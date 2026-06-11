using System.ComponentModel;
using System.Runtime.CompilerServices;
using SizeBench.AnalysisEngine;
using SizeBench.GUI.Settings;

namespace SizeBench.GUI.Windows;

public sealed class SelectSessionOptionsControlViewModel : INotifyPropertyChanged
{
    private readonly IAppSettings? _appSettings;

    public SelectSessionOptionsControlViewModel() : this(null) { }

    public SelectSessionOptionsControlViewModel(IAppSettings? appSettings)
    {
        this._appSettings = appSettings;

        if (appSettings != null)
        {
            this._useSymbolServer = appSettings.UseSymbolServer;
            this._symbolServerPathsText = String.Join(Environment.NewLine, appSettings.SymbolServerPaths);
        }
    }

    public SymbolSourcesSupported SymbolSourcesSupported { get; private set; } = SymbolSourcesSupported.All;

    public SessionOptions SessionOptions => new SessionOptions()
    {
        SymbolSourcesSupported = this.SymbolSourcesSupported,
        SymbolServerSearchPath = this.UseSymbolServer ? BuildSymbolSearchPath() : null,
    };

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

    private bool _useSymbolServer;
    public bool UseSymbolServer
    {
        get => this._useSymbolServer;
        set
        {
            if (this._useSymbolServer != value)
            {
                this._useSymbolServer = value;
                if (this._appSettings != null)
                {
                    this._appSettings.UseSymbolServer = value;
                }
                RaiseOnPropertyChanged(String.Empty);
            }
        }
    }

    private string _symbolServerPathsText = String.Empty;
    public string SymbolServerPathsText
    {
        get => this._symbolServerPathsText;
        set
        {
            value ??= String.Empty;
            if (this._symbolServerPathsText != value)
            {
                this._symbolServerPathsText = value;
                this._appSettings?.SetSymbolServerPaths(ParseLines(value));
                RaiseOnPropertyChanged(String.Empty);
            }
        }
    }

    public bool HasAnySymbolServerPaths => ParseLines(this._symbolServerPathsText).Any();

    private string BuildSymbolSearchPath()
        => String.Join(";", ParseLines(this._symbolServerPathsText));

    private static IEnumerable<string> ParseLines(string text)
        => (text ?? String.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

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
