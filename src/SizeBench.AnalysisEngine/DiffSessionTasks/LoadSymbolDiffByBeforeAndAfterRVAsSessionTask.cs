using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DiffSessionTasks;

internal sealed class LoadSymbolDiffByBeforeAndAfterRVAsSessionTask : DiffSessionTask<SymbolDiff?>
{
    private readonly Func<ILogger, Task<ISymbol?>> _beforeTaskFactory;
    private readonly Func<ILogger, Task<ISymbol?>> _afterTaskFactory;

    public LoadSymbolDiffByBeforeAndAfterRVAsSessionTask(DiffSessionTaskParameters parameters,
                                                         Func<ILogger, Task<ISymbol?>> beforeTaskFactory,
                                                         Func<ILogger, Task<ISymbol?>> afterTaskFactory,
                                                         IProgress<SessionTaskProgress>? progress,
                                                         CancellationToken token)
                                                         : base(parameters, progress, token)
    {
        this.TaskName = $"Load Symbol Diff";
        this._beforeTaskFactory = beforeTaskFactory;
        this._afterTaskFactory = afterTaskFactory;
    }

    protected override async Task<SymbolDiff?> ExecuteCoreAsync(ILogger logger)
    {
        this.CancellationToken.ThrowIfCancellationRequested();

        using var beforeAndAfterLog = logger.StartTaskLog("Loading symbol in 'before' and 'after'");
        var beforeTask = this._beforeTaskFactory(beforeAndAfterLog);
        var afterTask = this._afterTaskFactory(beforeAndAfterLog);

        var results = await Task.WhenAll(beforeTask, afterTask).WaitAsync(this.CancellationToken).ConfigureAwait(true);

        if (results[0] is null && results[1] is null)
        {
            return null;
        }

        return SymbolDiffFactory.CreateSymbolDiff(results[0], results[1], this.DataCache);
    }
}
