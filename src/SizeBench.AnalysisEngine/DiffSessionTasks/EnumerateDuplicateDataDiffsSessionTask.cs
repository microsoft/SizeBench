using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DiffSessionTasks;

internal sealed class EnumerateDuplicateDataDiffsSessionTask : DiffSessionTask<List<DuplicateDataItemDiff>>
{
    private readonly Func<ILogger, Task<IReadOnlyList<DuplicateDataItem>>> _beforeDDITaskFactory;
    private readonly Func<ILogger, Task<IReadOnlyList<DuplicateDataItem>>> _afterDDITaskFactory;

    public EnumerateDuplicateDataDiffsSessionTask(DiffSessionTaskParameters parameters,
                                                  Func<ILogger, Task<IReadOnlyList<DuplicateDataItem>>> beforeDDITaskFactory,
                                                  Func<ILogger, Task<IReadOnlyList<DuplicateDataItem>>> afterDDITaskFactory,
                                                  IProgress<SessionTaskProgress>? progress,
                                                  CancellationToken token)
                                                  : base(parameters, progress, token)
    {
        this.TaskName = $"Enumerate Duplicate Data Diffs";
        this._beforeDDITaskFactory = beforeDDITaskFactory;
        this._afterDDITaskFactory = afterDDITaskFactory;
    }

    protected override async Task<List<DuplicateDataItemDiff>> ExecuteCoreAsync(ILogger logger)
    {
        List<DuplicateDataItem> beforeDDIs;
        List<DuplicateDataItem> afterDDIs;

        using (var beforeAndAfterLog = logger.StartTaskLog("Enumerating duplicate data in 'before' and 'after'"))
        {
            var beforeTask = this._beforeDDITaskFactory(beforeAndAfterLog);
            var afterTask = this._afterDDITaskFactory(beforeAndAfterLog);

            var results = await Task.WhenAll(beforeTask, afterTask).WaitAsync(this.CancellationToken).ConfigureAwait(true);

            beforeDDIs = results[0].ToList();
            afterDDIs = results[1].ToList();
        }

        var DDIDiffs = new List<DuplicateDataItemDiff>();

        uint beforeDDIsParsed = 0;
        uint afterDDIsParsed = 0;
        const int loggerOutputVelocity = 100;
        var nextLoggerOutput = loggerOutputVelocity;

        var beforeDDIsCount = (uint)beforeDDIs.Count;
        var afterDDIsCount = (uint)afterDDIs.Count;
        var totalDDIsToDiff = beforeDDIsCount + afterDDIsCount;

        using (var beforeDDILoopLog = logger.StartTaskLog("Looping over 'before' duplicate data items"))
        {
            foreach (var beforeDDI in beforeDDIs)
            {
                this.CancellationToken.ThrowIfCancellationRequested();

                if (beforeDDIsParsed >= nextLoggerOutput)
                {
                    ReportProgress($"Parsed {beforeDDIsParsed:N0}/{beforeDDIsCount:N0} 'before' duplicate data items and {afterDDIsParsed:N0}/{afterDDIsCount:N0} 'after' duplicate data items into diffs.", beforeDDIsParsed + afterDDIsParsed, totalDDIsToDiff);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                beforeDDIsParsed++;

                var matchingAfterDDI = afterDDIs.FirstOrDefault(ddi => ddi.Symbol.IsVeryLikelyTheSameAs(beforeDDI.Symbol));
                if (matchingAfterDDI != null)
                {
                    afterDDIs.Remove(matchingAfterDDI); // Make the list smaller so the operation isn't a full two passes (one here, one in the loop below)
                    afterDDIsParsed++; // Because we removed one, let's keep the total marching towards "totalSymbolsToDiff"
                }

                DDIDiffs.Add(new DuplicateDataItemDiff(beforeDDI, matchingAfterDDI, this.DataCache));
            }
        }

        nextLoggerOutput = loggerOutputVelocity;

        using (var afterDDILoopLog = logger.StartTaskLog("Looping over 'after' duplicate data items"))
        {
            // Now catch any symbols that are in 'after' but weren't in 'before'
            foreach (var afterDDI in afterDDIs)
            {
                this.CancellationToken.ThrowIfCancellationRequested();

                if (afterDDIsParsed >= nextLoggerOutput)
                {
                    ReportProgress($"Parsed {afterDDIsParsed:N0}/{afterDDIsCount:N0} 'after' duplicate data items into diffs.", afterDDIsParsed + beforeDDIsCount, totalDDIsToDiff);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                afterDDIsParsed++;

                // This one wasn't found in 'before' so it's new in the 'after'
                DDIDiffs.Add(new DuplicateDataItemDiff(null, afterDDI, this.DataCache));
            }
        }

        // One final progress report so the log shows a nice summary at the end
        ReportProgress($"Parsed {beforeDDIsCount:N0} 'before' duplicate data items and {afterDDIsCount:N0} 'after' duplicate data items, generating  {DDIDiffs.Count:N0} diffs.", totalDDIsToDiff, totalDDIsToDiff);

        logger.Log($"Finished enumerating {DDIDiffs.Count:N0} duplicate data item diffs.");

        return DDIDiffs;
    }
}
