using System.Web;
using SizeBench.AnalysisEngine;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class WastefulVirtualDiffPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private WastefulVirtualItemDiff? _wastefulVirtualItemDiff;
    public WastefulVirtualItemDiff? WastefulVirtualItemDiff
    {
        get => this._wastefulVirtualItemDiff;
        private set { this._wastefulVirtualItemDiff = value; RaisePropertyChanged(); }
    }

    public WastefulVirtualDiffPageViewModel(IUITaskScheduler taskScheduler,
                                            IDiffSession diffSession) : base(diffSession)
    {
        this._uiTaskScheduler = taskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var typeName = HttpUtility.UrlDecode(this.QueryString["TypeName"]);

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Wasteful Virtual Item Diff",
            async (token) =>
            {
                this.WastefulVirtualItemDiff = (await this.DiffSession.EnumerateWastefulVirtualItemDiffs(token)).FirstOrDefault(wvi => wvi.TypeName == typeName); ;
            });
    }
}
