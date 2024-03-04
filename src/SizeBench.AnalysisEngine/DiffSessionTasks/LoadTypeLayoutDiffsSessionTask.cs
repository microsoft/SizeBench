using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DiffSessionTasks;

internal sealed class LoadTypeLayoutDiffsSessionTask : DiffSessionTask<List<TypeLayoutItemDiff>>
{
    private readonly Func<ILogger, Task<IReadOnlyList<TypeLayoutItem>>> _beforeTLITaskFactory;
    private readonly Func<ILogger, Task<IReadOnlyList<TypeLayoutItem>>> _afterTLITaskFactory;

    public LoadTypeLayoutDiffsSessionTask(DiffSessionTaskParameters parameters,
                                          Func<ILogger, Task<IReadOnlyList<TypeLayoutItem>>> beforeTLITaskFactory,
                                          Func<ILogger, Task<IReadOnlyList<TypeLayoutItem>>> afterTLITaskFactory,
                                          IProgress<SessionTaskProgress>? progress,
                                          CancellationToken token)
                                          : base(parameters, progress, token)
    {
        this.TaskName = $"Load Type Layout Diffs";
        this._beforeTLITaskFactory = beforeTLITaskFactory;
        this._afterTLITaskFactory = afterTLITaskFactory;
    }

    protected override async Task<List<TypeLayoutItemDiff>> ExecuteCoreAsync(ILogger logger)
    {
        List<TypeLayoutItem> beforeTLIs;
        List<TypeLayoutItem> afterTLIs;

        using (var beforeAndAfterLog = logger.StartTaskLog("Loading type layouts in 'before' and 'after'"))
        {
            var beforeTask = this._beforeTLITaskFactory(beforeAndAfterLog);
            var afterTask = this._afterTLITaskFactory(beforeAndAfterLog);

            var results = await Task.WhenAll(beforeTask, afterTask).WaitAsync(this.CancellationToken).ConfigureAwait(true);

            beforeTLIs = results[0]?.ToList() ?? new List<TypeLayoutItem>();
            afterTLIs = results[1]?.ToList() ?? new List<TypeLayoutItem>();
        }

        var TLIDiffs = new List<TypeLayoutItemDiff>();

        uint beforeTLIsParsed = 0;
        uint afterTLIsParsed = 0;
        const int loggerOutputVelocity = 100;
        var nextLoggerOutput = loggerOutputVelocity;

        var beforeTLIsCount = (uint)beforeTLIs.Count;
        var afterTLIsCount = (uint)afterTLIs.Count;
        var totalTLIsToDiff = beforeTLIsCount + afterTLIsCount;
        using (var beforeTLILoopLog = logger.StartTaskLog("Looping over 'before' type layout items"))
        {
            var allPossibleMatchingAfters = new List<TypeLayoutItem>(10); // Allocate once up here so it doesn't continually allocate in the loop below
            foreach (var beforeTLI in beforeTLIs)
            {
                this.CancellationToken.ThrowIfCancellationRequested();

                if (beforeTLIsParsed >= nextLoggerOutput)
                {
                    ReportProgress($"Parsed {beforeTLIsParsed:N0}/{beforeTLIsCount:N0} 'before' type layout items and {afterTLIsParsed:N0}/{afterTLIsCount:N0} 'after' type layout items into diffs.", beforeTLIsParsed + afterTLIsParsed, totalTLIsToDiff);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                beforeTLIsParsed++;

                // This code may look pretty complex, but it's very expensive if written more simply (by, say, using LINQ).
                // On a large binary with tens of thousands of types (like windows.ui.xaml.dll) this code can take 10+ minutes to
                // run with naive LINQ implementation.  This more manual way can get it done in < 15 seconds.
                TypeLayoutItem? matchingAfterTLI = null;
                var beforeUDT = beforeTLI.UserDefinedType;
                allPossibleMatchingAfters.Clear();
                for (var i = 0; i < afterTLIs.Count; i++)
                {
                    if (afterTLIs[i].UserDefinedType.IsVeryLikelyTheSameAs(beforeUDT))
                    {
                        allPossibleMatchingAfters.Add(afterTLIs[i]);
                        if (matchingAfterTLI is null)
                        {
                            matchingAfterTLI = afterTLIs[i];
                        }
                    }

                    // We know the type layouts come out sorted by their name - so once the name of the after is greater than the before name, we
                    // can stop enumerating.
                    if (String.CompareOrdinal(afterTLIs[i].UserDefinedType.Name, beforeUDT.Name) > 0)
                    {
                        break;
                    }
                }

                // There's multiple possible matches, likely due to ODR violations.  Let's try another way to reduce noise, by favoring
                // the first one we find that has the same size.  Most binaries I've seen don't end up modifying the things they contain
                // that violate ODR - those come from headers from other teams.  So, in practice this does reduce noise quite a bit.
                //
                // If none of them are the same size, we'll live with the one we found above (the value in matchingAfterTLI). At least we tried.
                if (allPossibleMatchingAfters.Count > 1)
                {
                    foreach (var possibleMatch in allPossibleMatchingAfters)
                    {
                        if (possibleMatch.UserDefinedType.InstanceSize == beforeUDT.InstanceSize)
                        {
                            matchingAfterTLI = possibleMatch;
                            break;
                        }
                    }
                }

                if (matchingAfterTLI != null)
                {
                    afterTLIs.Remove(matchingAfterTLI); // Make the list smaller so the operation isn't a full two passes (one here, one in the loop below)
                    afterTLIsParsed++; // Because we removed one, let's keep the total marching towards "totalSymbolsToDiff"
                }

                TLIDiffs.Add(new TypeLayoutItemDiff(beforeTLI, matchingAfterTLI));
            }
        }

        nextLoggerOutput = loggerOutputVelocity;

        using (var afterTLILoopLog = logger.StartTaskLog("Looping over 'after' type layout items"))
        {
            // Now catch any symbols that are in 'after' but weren't in 'before'
            foreach (var afterTLI in afterTLIs)
            {
                this.CancellationToken.ThrowIfCancellationRequested();

                if (afterTLIsParsed >= nextLoggerOutput)
                {
                    ReportProgress($"Parsed {afterTLIsParsed:N0}/{afterTLIsCount:N0} 'after' type layout items into diffs.", afterTLIsParsed + beforeTLIsCount, totalTLIsToDiff);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                afterTLIsParsed++;

                // This entire type wasn't found in 'before' so it's new in the 'after'
                TLIDiffs.Add(new TypeLayoutItemDiff(null, afterTLI));
            }
        }

        // One final progress report so the log shows a nice summary at the end
        ReportProgress($"Parsed {beforeTLIsCount:N0} 'before' type layout items and {afterTLIsCount:N0} 'after' type layout items, generating  {TLIDiffs.Count:N0} diffs.", totalTLIsToDiff, totalTLIsToDiff);

        logger.Log($"Finished enumerating {TLIDiffs.Count:N0} type layout item diffs.");

        return TLIDiffs;
    }
}
