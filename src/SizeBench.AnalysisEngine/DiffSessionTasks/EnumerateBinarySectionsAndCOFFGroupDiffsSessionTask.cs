using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DiffSessionTasks;

internal sealed class EnumerateBinarySectionsAndCOFFGroupDiffsSessionTask : DiffSessionTask<List<BinarySectionDiff>>
{
    private readonly Func<ILogger, Task<IReadOnlyList<BinarySection>>> _beforeSectionsTaskFactory;
    private readonly Func<ILogger, Task<IReadOnlyList<BinarySection>>> _afterSectionsTaskFactory;

    public EnumerateBinarySectionsAndCOFFGroupDiffsSessionTask(DiffSessionTaskParameters parameters,
                                                               Func<ILogger, Task<IReadOnlyList<BinarySection>>> beforeSectionsTaskFactory,
                                                               Func<ILogger, Task<IReadOnlyList<BinarySection>>> afterSectionsTaskFactory,
                                                               CancellationToken token)
        : base(parameters, null, token)
    {
        this.TaskName = "Enumerate Binary Sections and COFF Group Diffs";
        this._beforeSectionsTaskFactory = beforeSectionsTaskFactory;
        this._afterSectionsTaskFactory = afterSectionsTaskFactory;

    }

    protected override async Task<List<BinarySectionDiff>> ExecuteCoreAsync(ILogger logger)
    {
        if (this.DataCache.AllBinarySectionDiffs != null)
        {
            logger.Log("Found section diffs in the cache, re-using them, hooray!");
            return this.DataCache.AllBinarySectionDiffs;
        }

        ReportProgress("Enumerating sections in 'before' and 'after'", 0, null);

        IReadOnlyList<BinarySection> beforeSections;
        IReadOnlyList<BinarySection> afterSections;

        using (var beforeAndAfterLog = logger.StartTaskLog("Enumerating sections in 'before' and 'after'"))
        {
            var beforeTask = this._beforeSectionsTaskFactory(beforeAndAfterLog);
            var afterTask = this._afterSectionsTaskFactory(beforeAndAfterLog);

            var results = await Task.WhenAll(beforeTask, afterTask).WaitAsync(this.CancellationToken).ConfigureAwait(true);

            beforeSections = results[0];
            afterSections = results[1];
        }

        var sectionDiffs = new List<BinarySectionDiff>(capacity: Math.Max(beforeSections.Count, afterSections.Count));

        foreach (var beforeSection in beforeSections)
        {
            this.CancellationToken.ThrowIfCancellationRequested();

            // Find the matching section in 'after' - the only way we know to match is by name but given how consistent everyone is
            // with section naming this should be ok.
            var matchingAfterSection = afterSections.FirstOrDefault(bs => bs.Name == beforeSection.Name);

#if DEBUG
            if (matchingAfterSection != null && afterSections.Where(bs => bs.Name == beforeSection.Name).Count() > 1)
            {
                throw new InvalidOperationException("This shouldn't be possible, and will throw off how diffing works.  Look into it...");
            }
#endif

            sectionDiffs.Add(new BinarySectionDiff(beforeSection, matchingAfterSection, this.DataCache));
        }

        // Now catch any sections that are in 'after' but weren't in 'before'
        foreach (var afterSection in afterSections)
        {
            // If we already matched this above, move on
            if (sectionDiffs.Any(bsd => bsd.Name == afterSection.Name))
            {
                continue;
            }

            // This one wasn't found in 'before' so it's new in the 'after'
            sectionDiffs.Add(new BinarySectionDiff(null, afterSection, this.DataCache));
        }

        this.DataCache.AllBinarySectionDiffs = sectionDiffs;

        logger.Log($"Finished enumerating {sectionDiffs.Count} binary section diffs.");

        return this.DataCache.AllBinarySectionDiffs;
    }
}
