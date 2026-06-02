using System.Diagnostics.CodeAnalysis;
using SizeBench.AnalysisEngine;
using SizeBench.GUI.Core;

namespace SizeBench.GUI.Pages;

[ExcludeFromCodeCoverage] // This class does nothing, no need to test it
internal sealed class SingleBinaryOverviewPageViewModel : SingleBinaryViewModelBase
{
    public SingleBinaryOverviewPageViewModel(ISession session) : base(session)
    {
    }
}
