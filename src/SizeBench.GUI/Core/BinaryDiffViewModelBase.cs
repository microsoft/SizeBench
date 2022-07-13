using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Core;

internal abstract class BinaryDiffViewModelBase : ViewModelBase
{
    public IDiffSession DiffSession { get; }

    public BinaryDiffViewModelBase(IDiffSession diffSession)
    {
        this.DiffSession = diffSession;
    }
}
