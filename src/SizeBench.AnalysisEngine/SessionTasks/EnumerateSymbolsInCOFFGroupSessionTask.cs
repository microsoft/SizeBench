namespace SizeBench.AnalysisEngine.SessionTasks;

internal class EnumerateSymbolsInCOFFGroupSessionTask : EnumerateSymbolsInRVARangeSessionTask
{
    public EnumerateSymbolsInCOFFGroupSessionTask(SessionTaskParameters parameters,
                                                  CancellationToken token,
                                                  IProgress<SessionTaskProgress>? progress,
                                                  COFFGroup coffGroup)
        : base(parameters, token, progress, RVARange.FromRVAAndSize(coffGroup.RVA, coffGroup.VirtualSize))
    {
        this.TaskName = $"Enumerate Symbols in COFF Group '{coffGroup.Name}'";
    }
}
