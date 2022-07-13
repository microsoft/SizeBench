using System.Web;
using SizeBench.AnalysisEngine;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class TemplateFoldabilityDiffPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private TemplateFoldabilityItemDiff? _TemplateFoldabilityItemDiff;
    public TemplateFoldabilityItemDiff? TemplateFoldabilityItemDiff
    {
        get => this._TemplateFoldabilityItemDiff;
        private set { this._TemplateFoldabilityItemDiff = value; RaisePropertyChanged(); }
    }

    public TemplateFoldabilityDiffPageViewModel(IUITaskScheduler taskScheduler,
                                                IDiffSession diffSession) : base(diffSession)
    {
        this._uiTaskScheduler = taskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var templateName = HttpUtility.UrlDecode(this.QueryString["TemplateName"]);

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Template Foldability Item Diff",
            async (token) =>
            {
                this.TemplateFoldabilityItemDiff = (await this.DiffSession.EnumerateTemplateFoldabilityItemDiffs(token)).FirstOrDefault(tfi => tfi.TemplateName == templateName); ;
            });
    }
}
