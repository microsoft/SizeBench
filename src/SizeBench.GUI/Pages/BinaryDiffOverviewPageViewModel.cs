using SizeBench.AnalysisEngine;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

internal sealed class BinaryDiffOverviewPageViewModel : BinaryDiffViewModelBase
{
    public BinaryDiffOverviewPageViewModel(IDiffSession diffSession) : base(diffSession)
    {
    }
}
