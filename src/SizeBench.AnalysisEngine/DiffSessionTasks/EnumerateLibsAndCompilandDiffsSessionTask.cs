using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DiffSessionTasks;

internal sealed class EnumerateLibsAndCompilandDiffsSessionTask : DiffSessionTask<List<LibDiff>>
{
    private readonly Func<ILogger, Task<IReadOnlyList<Library>>> _beforeLibTaskFactory;
    private readonly Func<ILogger, Task<IReadOnlyList<Library>>> _afterLibTaskFactory;

    public EnumerateLibsAndCompilandDiffsSessionTask(DiffSessionTaskParameters parameters,
                                                     Func<ILogger, Task<IReadOnlyList<Library>>> beforeLibTaskFactory,
                                                     Func<ILogger, Task<IReadOnlyList<Library>>> afterLibTaskFactory,
                                                     CancellationToken token,
                                                     IProgress<SessionTaskProgress>? progress)
        : base(parameters, progress, token)
    {
        this.TaskName = "Enumerate Lib and Compiland Diffs";
        this._beforeLibTaskFactory = beforeLibTaskFactory;
        this._afterLibTaskFactory = afterLibTaskFactory;

    }

    protected override async Task<List<LibDiff>> ExecuteCoreAsync(ILogger logger)
    {
        if ((this.DataCache.AllLibDiffs != null && this.DataCache._allLibDiffsCreationInProgress) ||
            this.DataCache.AllLibDiffsInList != null)
        {
            if (this.DataCache._allLibDiffsCreationInProgress &&
                this.DataCache.AllLibDiffs != null)
            {
                await this.DataCache.AllLibDiffs.ConfigureAwait(true);
            }

            logger.Log("Found lib diffs in the cache, re-using them, hooray!");
            return this.DataCache.AllLibDiffsInList!;
        }

        this.DataCache._allLibDiffsCreationInProgress = true;

        ReportProgress("Enumerating diffs of binary sections", 0, null);

        await new EnumerateBinarySectionsAndCOFFGroupDiffsSessionTask(this._diffSessionTaskParameters,
                                                                      (l) => this.DiffSession.BeforeSession.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken, l),
                                                                      (l) => this.DiffSession.AfterSession.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken, l),
                                                                      this.CancellationToken)
                                                                      .ExecuteAsync(logger)
                                                                      .ConfigureAwait(true);

        ReportProgress("Enumerating libs in 'before' and 'after'", 0, null);

        IReadOnlyList<Library> beforeLibs;
        IReadOnlyList<Library> afterLibs;

        using (var beforeAndAfterLog = logger.StartTaskLog("Enumerating libs in 'before' and 'after'"))
        {
            var beforeTask = this._beforeLibTaskFactory(beforeAndAfterLog);
            var afterTask = this._afterLibTaskFactory(beforeAndAfterLog);

            var results = await Task.WhenAll(beforeTask, afterTask).WaitAsync(this.CancellationToken).ConfigureAwait(true);

            beforeLibs = results[0];
            afterLibs = results[1];
        }

        var libDiffs = new List<LibDiff>(capacity: Math.Max(beforeLibs.Count, afterLibs.Count));

        uint beforeLibsParsed = 0;
        const int loggerOutputVelocity = 5;
        var nextLoggerOutput = loggerOutputVelocity;

        var afterLibsToProcess = new List<Library>(afterLibs);

        foreach (var beforeLib in beforeLibs)
        {
            this.CancellationToken.ThrowIfCancellationRequested();

            if (beforeLibsParsed >= nextLoggerOutput)
            {
                ReportProgress($"Parsed {beforeLibsParsed}/{beforeLibs.Count} libs into diffs.", beforeLibsParsed, (uint)beforeLibs.Count);
                nextLoggerOutput += loggerOutputVelocity;
            }

            beforeLibsParsed++;

            // First try matching on the name exactly
            var matchingAfterLib = afterLibsToProcess.FirstOrDefault(afterLib => String.Equals(beforeLib.Name, afterLib.Name, StringComparison.OrdinalIgnoreCase));

            if (matchingAfterLib is null)
            {
                // If that failed, try matching on IsVeryLikelyTheSameAs, which is more generous.  This two-phase attempt is unfortunately
                // necessary since some binaries have "very similar paths" internally, yet different enough that we find more than one
                // matching lib.  This way we always allow exact matches to be perfect, and only fallback to the heuristic comparer when
                // we must.  Hopefully this is ok for perf.
                matchingAfterLib = afterLibsToProcess.FirstOrDefault(beforeLib.IsVeryLikelyTheSameAs);

#if DEBUG
                if (matchingAfterLib != null && afterLibsToProcess.Where(beforeLib.IsVeryLikelyTheSameAs).Count() > 1)
                {
                    // Some binaries discovered in the wild just can't pass this heuristic - I've tried and tried to make the "similar enough" metric
                    // constrained enough without breaking it down totally to a strict-match (which is too constrained for many customers), so at this
                    // point I don't have any other ideas than ignoring this debug-sanity check for certain binaries SizeBench developers frequently look
                    // at, when we know they will not pass this check.
                    var binariesToIgnoreThisCheckFor = new (string, string)[]
                    {
                            ("mso30win32client.dll", "precomp_win32")
                    };
                    var binaryFilename = System.IO.Path.GetFileName(this.DiffSession.BeforeSession.BinaryPath);
                    if (!binariesToIgnoreThisCheckFor.Contains((binaryFilename, beforeLib.ShortName)))
                    {
                        var localEnumerationForDebugging = afterLibsToProcess.Where(beforeLib.IsVeryLikelyTheSameAs).OrderBy(lib => lib.Name).ToList();
                        throw new InvalidOperationException("This shouldn't be possible, and will throw off how diffing works.  Look into it...");
                    }
                }
#endif
            }

            libDiffs.Add(new LibDiff(beforeLib, matchingAfterLib, this.DataCache.AllBinarySectionDiffs!, this.DataCache));

            if (matchingAfterLib != null)
            {
                afterLibsToProcess.Remove(matchingAfterLib);
            }
        }

        // Now catch any libs that are in 'after' but weren't in 'before'
        foreach (var afterLib in afterLibsToProcess)
        {
            // This one wasn't found in 'before' so it's new in the 'after'
            libDiffs.Add(new LibDiff(null, afterLib, this.DataCache.AllBinarySectionDiffs!, this.DataCache));
        }

        // One final progress report so the log shows a nice summary at the end
        ReportProgress($"Parsed {beforeLibs.Count} 'before' libs and {afterLibs.Count} 'after' libs, generating  {libDiffs.Count} diffs.", (uint)libDiffs.Count, (uint)libDiffs.Count);

        this.DataCache.AllLibDiffsInList = libDiffs;

        logger.Log($"Finished enumerating {libDiffs.Count} lib diffs.");
        this.DataCache._allLibDiffsCreationInProgress = false;

        return libDiffs;
    }
}
