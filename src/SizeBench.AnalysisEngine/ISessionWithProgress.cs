using System.ComponentModel;

namespace SizeBench.AnalysisEngine;

public interface ISessionWithProgress : INotifyPropertyChanged, IAsyncDisposable
{
    #region Progress Reporting

    bool IsBusy { get; }

    IProgress<SessionTaskProgress>? ProgressReporter { get; set; }

    #endregion
}
