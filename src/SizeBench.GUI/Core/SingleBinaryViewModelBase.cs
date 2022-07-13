using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Core;

internal abstract class SingleBinaryViewModelBase : ViewModelBase
{
    public ISession Session { get; }
    public SingleBinaryViewModelBase(ISession session)
    {
        this.Session = session;
    }
}
