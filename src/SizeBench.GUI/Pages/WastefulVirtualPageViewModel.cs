using System.Web;
using SizeBench.AnalysisEngine;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class WastefulVirtualPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private WastefulVirtualItem? _wastefulVirtualItem;
    public WastefulVirtualItem? WastefulVirtualItem
    {
        get => this._wastefulVirtualItem;
        set { this._wastefulVirtualItem = value; RaisePropertyChanged(); }
    }

    public WastefulVirtualPageViewModel(IUITaskScheduler taskScheduler,
                                        ISession session) : base(session)
    {
        this._uiTaskScheduler = taskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var typeName = HttpUtility.UrlDecode(this.QueryString["TypeName"]);

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Wasteful Virtual Item",
            async (token) => this.WastefulVirtualItem = (await this.Session.EnumerateWastefulVirtuals(token)).FirstOrDefault(wvi => wvi.UserDefinedType.Name == typeName));
    }
}
