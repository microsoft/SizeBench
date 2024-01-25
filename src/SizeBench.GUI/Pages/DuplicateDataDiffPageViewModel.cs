using System.Globalization;
using SizeBench.AnalysisEngine;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class DuplicateDataDiffPageViewModel : BinaryDiffViewModelBase
{
    private readonly IUITaskScheduler _uiTaskScheduler;
    private DuplicateDataItemDiff? _duplicateDataItemDiff;
    public DuplicateDataItemDiff? DuplicateDataItemDiff
    {
        get => this._duplicateDataItemDiff;
        private set { this._duplicateDataItemDiff = value; RaisePropertyChanged(); }
    }

    public DuplicateDataDiffPageViewModel(IUITaskScheduler taskScheduler,
                                          IDiffSession diffSession) : base(diffSession)
    {
        this._uiTaskScheduler = taskScheduler;
    }

    protected internal override async Task InitializeAsync()
    {
        uint? beforeDuplicateRVA = null;
        uint? afterDuplicateRVA = null;
        if (this.QueryString.TryGetValue("BeforeDuplicateRVA", out var value))
        {
            beforeDuplicateRVA = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }

        if (this.QueryString.TryGetValue("AfterDuplicateRVA", out value))
        {
            afterDuplicateRVA = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }

        await this._uiTaskScheduler.StartLongRunningUITask($"Finding Duplicate Data Item Diff",
            async (token) =>
            {
                this.DuplicateDataItemDiff = (await this.DiffSession.EnumerateDuplicateDataItemDiffs(token))
                                             .FirstOrDefault(ddi => (ddi.BeforeDuplicate != null && ddi.BeforeDuplicate.Symbol.RVA == beforeDuplicateRVA) ||
                                                                    (ddi.AfterDuplicate != null && ddi.AfterDuplicate.Symbol.RVA == afterDuplicateRVA));
            });
    }
}
