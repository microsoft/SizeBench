using SizeBench.AnalysisEngine;
using SizeBench.GUI.Core;
using SizeBench.GUI.Models;

namespace SizeBench.GUI.Pages.Symbols;

internal sealed class InlineSiteGroupPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;

    private InlineSiteGroup? _inlineSiteGroup;
    public InlineSiteGroup? InlineSiteGroup
    {
        get => this._inlineSiteGroup;
        private set { this._inlineSiteGroup = value; RaisePropertyChanged(); }
    }

    private string _pageTitle = "Inlined Function";
    public string PageTitle
    {
        get => this._pageTitle;
        private set { this._pageTitle = value; RaisePropertyChanged(); }
    }

    public InlineSiteGroupPageViewModel(IUITaskScheduler uiTaskScheduler,
                                        ISession session) : base(session)
    {
        this._uiTaskScheduler = uiTaskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var inlineSiteGroupName = this.QueryString["Name"];

        await this._uiTaskScheduler.StartLongRunningUITask($"Looking up inline sites for '{inlineSiteGroupName}'", async (token) =>
        {
            var allInlineSites = await this.Session.EnumerateAllInlineSites(token);

            var matchingInlineSites = allInlineSites.Where(inlineSite => inlineSite.Name.Equals(inlineSiteGroupName, StringComparison.Ordinal)).ToList();

            this.InlineSiteGroup = new InlineSiteGroup(inlineSiteGroupName, matchingInlineSites);
        });

        this.PageTitle = $"Inlined Function: {inlineSiteGroupName}";
    }
}
