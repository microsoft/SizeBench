using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DiffSessionTasks;

internal sealed class EnumerateWastefulVirtualDiffsSessionTask : DiffSessionTask<List<WastefulVirtualItemDiff>>
{
    private readonly Func<ILogger, Task<IReadOnlyList<WastefulVirtualItem>>> _beforeWVITaskFactory;
    private readonly Func<ILogger, Task<IReadOnlyList<WastefulVirtualItem>>> _afterWVITaskFactory;

    public EnumerateWastefulVirtualDiffsSessionTask(DiffSessionTaskParameters parameters,
                                                    Func<ILogger, Task<IReadOnlyList<WastefulVirtualItem>>> beforeWVITaskFactory,
                                                    Func<ILogger, Task<IReadOnlyList<WastefulVirtualItem>>> afterWVITaskFactory,
                                                    IProgress<SessionTaskProgress>? progress,
                                                    CancellationToken token)
                                                    : base(parameters, progress, token)
    {
        this.TaskName = $"Enumerate Wasteful Virtual Diffs";
        this._beforeWVITaskFactory = beforeWVITaskFactory;
        this._afterWVITaskFactory = afterWVITaskFactory;
    }

    protected override async Task<List<WastefulVirtualItemDiff>> ExecuteCoreAsync(ILogger logger)
    {
        List<WastefulVirtualItem> beforeWVIs;
        List<WastefulVirtualItem> afterWVIs;

        using (var beforeAndAfterLog = logger.StartTaskLog("Enumerating wasteful virtuals in 'before' and 'after'"))
        {
            var beforeTask = this._beforeWVITaskFactory(beforeAndAfterLog);
            var afterTask = this._afterWVITaskFactory(beforeAndAfterLog);

            var results = await Task.WhenAll(beforeTask, afterTask).WaitAsync(this.CancellationToken).ConfigureAwait(true);

            beforeWVIs = results[0]?.ToList() ?? new List<WastefulVirtualItem>();
            afterWVIs = results[1]?.ToList() ?? new List<WastefulVirtualItem>();
        }

        var WVIDiffs = new List<WastefulVirtualItemDiff>();

        uint beforeWVIsParsed = 0;
        uint afterWVIsParsed = 0;
        const int loggerOutputVelocity = 100;
        var nextLoggerOutput = loggerOutputVelocity;

        var beforeWVIsCount = (uint)beforeWVIs.Count;
        var afterWVIsCount = (uint)afterWVIs.Count;
        var totalWVIsToDiff = beforeWVIsCount + afterWVIsCount;
        using (var beforeWVILoopLog = logger.StartTaskLog("Looping over 'before' wasteful virtual items"))
        {
            foreach (var beforeWVI in beforeWVIs)
            {
                this.CancellationToken.ThrowIfCancellationRequested();

                if (beforeWVIsParsed >= nextLoggerOutput)
                {
                    ReportProgress($"Parsed {beforeWVIsParsed:N0}/{beforeWVIsCount:N0} 'before' wasteful virtual items and {afterWVIsParsed:N0}/{afterWVIsCount:N0} 'after' wasteful virtual items into diffs.", beforeWVIsParsed + afterWVIsParsed, totalWVIsToDiff);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                beforeWVIsParsed++;

                var matchingAfterWVI = afterWVIs.FirstOrDefault(afterWVI => afterWVI.UserDefinedType.IsVeryLikelyTheSameAs(beforeWVI.UserDefinedType));
                if (matchingAfterWVI != null)
                {
                    afterWVIs.Remove(matchingAfterWVI); // Make the list smaller so the operation isn't a full two passes (one here, one in the loop below)
                    afterWVIsParsed++; // Because we removed one, let's keep the total marching towards "totalSymbolsToDiff"
                }

                var wviDiff = new WastefulVirtualItemDiff(beforeWVI, matchingAfterWVI, this.DataCache);

                //We'll only return ones that are interesting to look at - they meet one of these conditions:
                // 1. Size diff is not 0, so clearly something of interest happened.
                // 2. Type hierarchy changed in some way, it's possible size diff can be 0 but a type was added and a type was removed, still useful to show in UI
                // 3. Wasted overrides changed, again size diff may be 0 but there could be some of these added/removed (or in combination with types added/removed)
                if (wviDiff.WastedSizeDiff != 0 ||
                    wviDiff.TypeHierarchyChanges.Count != 0 ||
                    wviDiff.WastedOverrideChanges.Count != 0)
                {
                    WVIDiffs.Add(wviDiff);
                }
            }
        }

        nextLoggerOutput = loggerOutputVelocity;

        using (var afterWVILoopLog = logger.StartTaskLog("Looping over 'after' wasteful virtual items"))
        {
            // Now catch any symbols that are in 'after' but weren't in 'before'
            foreach (var afterWVI in afterWVIs)
            {
                this.CancellationToken.ThrowIfCancellationRequested();

                if (afterWVIsParsed >= nextLoggerOutput)
                {
                    ReportProgress($"Parsed {afterWVIsParsed:N0}/{afterWVIsCount:N0} 'after' wasteful virtual items into diffs.", afterWVIsParsed + beforeWVIsCount, totalWVIsToDiff);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                afterWVIsParsed++;

                // This entire type wasn't found in 'before' so it's new in the 'after'
                WVIDiffs.Add(new WastefulVirtualItemDiff(null, afterWVI, this.DataCache));
            }
        }

        // One final progress report so the log shows a nice summary at the end
        ReportProgress($"Parsed {beforeWVIsCount:N0} 'before' wasteful virtual items and {afterWVIsCount:N0} 'after' wasteful virtual items, generating  {WVIDiffs.Count:N0} diffs.", totalWVIsToDiff, totalWVIsToDiff);

        logger.Log($"Finished enumerating {WVIDiffs.Count:N0} wasteful virtual item diffs.");

        return WVIDiffs;
    }
}
