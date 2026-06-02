using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DiffSessionTasks;

internal sealed class EnumerateTemplateFoldabilityDiffsSessionTask : DiffSessionTask<List<TemplateFoldabilityItemDiff>>
{
    private readonly Func<ILogger, Task<IReadOnlyList<TemplateFoldabilityItem>>> _beforeTFITaskFactory;
    private readonly Func<ILogger, Task<IReadOnlyList<TemplateFoldabilityItem>>> _afterTFITaskFactory;

    public EnumerateTemplateFoldabilityDiffsSessionTask(DiffSessionTaskParameters parameters,
                                                        Func<ILogger, Task<IReadOnlyList<TemplateFoldabilityItem>>> beforeTFITaskFactory,
                                                        Func<ILogger, Task<IReadOnlyList<TemplateFoldabilityItem>>> afterTFITaskFactory,
                                                        IProgress<SessionTaskProgress>? progress,
                                                        CancellationToken token)
                                                        : base(parameters, progress, token)
    {
        this.TaskName = $"Enumerate Template Foldability Diffs";
        this._beforeTFITaskFactory = beforeTFITaskFactory;
        this._afterTFITaskFactory = afterTFITaskFactory;
    }

    protected override async Task<List<TemplateFoldabilityItemDiff>> ExecuteCoreAsync(ILogger logger)
    {
        List<TemplateFoldabilityItem> beforeTFIs;
        List<TemplateFoldabilityItem> afterTFIs;

        using (var beforeAndAfterLog = logger.StartTaskLog("Enumerating template foldability data in 'before' and 'after'"))
        {
            var beforeTask = this._beforeTFITaskFactory(beforeAndAfterLog);
            var afterTask = this._afterTFITaskFactory(beforeAndAfterLog);

            var results = await Task.WhenAll(beforeTask, afterTask).WaitAsync(this.CancellationToken).ConfigureAwait(true);

            beforeTFIs = results[0]?.ToList() ?? new List<TemplateFoldabilityItem>();
            afterTFIs = results[1]?.ToList() ?? new List<TemplateFoldabilityItem>();
        }

        var TFIDiffs = new List<TemplateFoldabilityItemDiff>();

        uint beforeTFIsParsed = 0;
        uint afterTFIsParsed = 0;
        const int loggerOutputVelocity = 100;
        var nextLoggerOutput = loggerOutputVelocity;

        var beforeTFIsCount = (uint)beforeTFIs.Count;
        var afterTFIsCount = (uint)afterTFIs.Count;
        var totalTFIsToDiff = beforeTFIsCount + afterTFIsCount;

        using (var beforeTFILoopLog = logger.StartTaskLog("Looping over 'before' template foldability items"))
        {
            foreach (var beforeTFI in beforeTFIs)
            {
                this.CancellationToken.ThrowIfCancellationRequested();

                if (beforeTFIsParsed >= nextLoggerOutput)
                {
                    ReportProgress($"Parsed {beforeTFIsParsed:N0}/{beforeTFIsCount:N0} 'before' template foldability items and {afterTFIsParsed:N0}/{afterTFIsCount:N0} 'after' template foldability items into diffs.", beforeTFIsParsed + afterTFIsParsed, totalTFIsToDiff);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                beforeTFIsParsed++;

                var matchingAfterTFI = afterTFIs.FirstOrDefault(TFI => TFI.TemplateName == beforeTFI.TemplateName);
                if (matchingAfterTFI != null)
                {
                    afterTFIs.Remove(matchingAfterTFI); // Make the list smaller so the operation isn't a full two passes (one here, one in the loop below)
                    afterTFIsParsed++; // Because we removed one, let's keep the total marching towards "totalSymbolsToDiff"
                }

                TFIDiffs.Add(new TemplateFoldabilityItemDiff(beforeTFI, matchingAfterTFI));
            }
        }

        nextLoggerOutput = loggerOutputVelocity;

        using (var afterTFILoopLog = logger.StartTaskLog("Looping over 'after' template foldability items"))
        {
            // Now catch any symbols that are in 'after' but weren't in 'before'
            foreach (var afterTFI in afterTFIs)
            {
                this.CancellationToken.ThrowIfCancellationRequested();

                if (afterTFIsParsed >= nextLoggerOutput)
                {
                    ReportProgress($"Parsed {afterTFIsParsed:N0}/{afterTFIsCount:N0} 'after' template foldability items into diffs.", afterTFIsParsed + beforeTFIsCount, totalTFIsToDiff);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                afterTFIsParsed++;

                // This one wasn't found in 'before' so it's new in the 'after'
                TFIDiffs.Add(new TemplateFoldabilityItemDiff(null, afterTFI));
            }
        }

        // One final progress report so the log shows a nice summary at the end
        ReportProgress($"Parsed {beforeTFIsCount:N0} 'before' template foldability items and {afterTFIsCount:N0} 'after' template foldability items, generating  {TFIDiffs.Count:N0} diffs.", totalTFIsToDiff, totalTFIsToDiff);

        logger.Log($"Finished enumerating {TFIDiffs.Count:N0} template foldability item diffs.");

        return TFIDiffs;
    }
}
