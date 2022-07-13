using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class WhoopsiePageViewModel : ViewModelBase
{
    private string _unknownTypeName = String.Empty;
    public string UnknownTypeName
    {
        get => this._unknownTypeName;
        set
        {
            this._unknownTypeName = value;
            RaisePropertyChanged();
        }
    }

    public WhoopsiePageViewModel()
    {
    }

    protected internal override Task InitializeAsync()
    {
        this.UnknownTypeName = this.QueryString["UnknownTypeName"];
        return Task.CompletedTask;
    }
}
