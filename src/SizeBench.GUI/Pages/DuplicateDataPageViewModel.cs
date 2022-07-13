using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class DuplicateDataPageViewModel : SingleBinaryViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private DuplicateDataItem? _duplicateDataItem;
    public DuplicateDataItem? DuplicateDataItem
    {
        get => this._duplicateDataItem;
        private set { this._duplicateDataItem = value; RaisePropertyChanged(); }
    }

    public DuplicateDataPageViewModel(IUITaskScheduler taskScheduler,
                                      ISession session) : base(session)
    {
        this._uiTaskScheduler = taskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        var duplicateRVA = Convert.ToUInt32(this.QueryString["DuplicateRVA"], CultureInfo.InvariantCulture);

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Duplicate Data Item",
            async (token) => this.DuplicateDataItem = (await this.Session.EnumerateDuplicateDataItems(token)).FirstOrDefault(ddi => ddi.Symbol.RVA == duplicateRVA));
    }
}
