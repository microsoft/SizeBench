namespace SizeBench.AnalysisEngine.SessionTasks;

internal class EnumerateSymbolsInBinarySectionSessionTask : EnumerateSymbolsInRVARangeSessionTask
{
    public EnumerateSymbolsInBinarySectionSessionTask(SessionTaskParameters parameters,
                                                      CancellationToken token,
                                                      IProgress<SessionTaskProgress>? progress,
                                                      BinarySection section)
        : base(parameters, token, progress, RVARange.FromRVAAndSize(section.RVA, section.VirtualSize))
    {
        this.TaskName = $"Enumerate Symbols in Binary Section '{section.Name}'";
    }
}
