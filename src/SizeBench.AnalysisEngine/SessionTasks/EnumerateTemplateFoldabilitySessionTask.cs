using System.Diagnostics;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class EnumerateTemplateFoldabilitySessionTask : SessionTask<List<TemplateFoldabilityItem>>
{
    public EnumerateTemplateFoldabilitySessionTask(SessionTaskParameters parameters,
                                                   IProgress<SessionTaskProgress>? progressReporter,
                                                   CancellationToken token)
                                                   : base(parameters, progressReporter, token)
    {
        this.TaskName = "Explore Template Foldability";
    }

    protected override List<TemplateFoldabilityItem> ExecuteCore(ILogger logger)
    {
        if (this.DataCache.AllTemplateFoldabilityItems != null)
        {
            logger.Log("Found template foldability items in the cache, re-using them, hooray!");
            return this.DataCache.AllTemplateFoldabilityItems;
        }

        List<IFunctionCodeSymbol> allTemplatedFunctionSymbols;
        ReportProgress("Enumerating all templated functions in the binary...", 0, null);
        using (var taskLog = logger.StartTaskLog("Find all templated functions"))
        {
            allTemplatedFunctionSymbols = this.DIAAdapter.FindAllTemplatedFunctions(this.CancellationToken).ToList();
        }

        this.CancellationToken.ThrowIfCancellationRequested();

        IEnumerable<IGrouping<string, IFunctionCodeSymbol>> groupedSymbols;
        ReportProgress("Grouping templated functions by type/function/parameters...", 0, null);
        using (var taskLog = logger.StartTaskLog("Group templated functions"))
        {
            groupedSymbols = allTemplatedFunctionSymbols.GroupBy(SymbolNameHelper.FunctionToGenericTemplatedName).ToList();
        }

        const int loggerOutputVelocity = 100;
        uint nextLoggerOutput = loggerOutputVelocity;
        uint symbolsEnumerated = 0;
        var foldables = new List<TemplateFoldabilityItem>();

        foreach (var groupOfTemplatedFunctions in groupedSymbols)
        {
            symbolsEnumerated += (uint)groupOfTemplatedFunctions.Count();
            if (symbolsEnumerated >= nextLoggerOutput)
            {
                ReportProgress($"Enumerated {symbolsEnumerated}/{allTemplatedFunctionSymbols.Count} functions, found {foldables.Count} items with interesting foldability so far.", symbolsEnumerated, (uint)allTemplatedFunctionSymbols.Count);
                nextLoggerOutput += loggerOutputVelocity;
            }

            this.CancellationToken.ThrowIfCancellationRequested();

            // If we found only one templated function in this group, then it can't possibly fold with anything so we'll strip it from the results
            // to reduce the noisiness of the returned list of foldability items.
            if (groupOfTemplatedFunctions.Count() == 1)
            {
                continue;
            }

            var allUniqueRanges = new List<RVARange>();
            var uniqueFunctions = new List<IFunctionCodeSymbol>();

            // TODO: TemplateFoldability: These are already grouped by RVA in TemplateGrouper...could keep track of that somehow to avoid doing it again here
            // Calculate the combined size of the templated function (note that existing COMDAT folding may mean that the sum of the symbols' sizes
            // will overstate things - so we have to manually keep track of RVAs we've already counted.
            foreach (var sym in groupOfTemplatedFunctions)
            {
                foreach (var block in sym.Blocks)
                {
                    if (!allUniqueRanges.Any(range => range.Contains(block.RVA)))
                    {
                        var uniqueRange = RVARange.FromRVAAndSize(block.RVA, block.Size);
                        if (block.IsCOMDATFolded == false)
                        {
                            allUniqueRanges.Add(uniqueRange);
                            if (block is SimpleFunctionCodeSymbol or PrimaryCodeBlockSymbol)
                            {
                                uniqueFunctions.Add(sym);
                            }
                        }
                    }
                    else if (block.IsCOMDATFolded == false)
                    {
                        //TODO: TemplateFoldability: Debug.Assert isn't good for testing
                        Debug.Assert(allUniqueRanges.Any(range => range.Contains(block.RVAEnd)));
                    }
                }
            }

            foldables.Add(new TemplateFoldabilityItem(groupOfTemplatedFunctions.Key,
                                                      groupOfTemplatedFunctions.ToList(),
                                                      uniqueFunctions,
                                                      (uint)allUniqueRanges.Sum(range => range.Size),
                                                      CalculatePercentageSimilarity(groupOfTemplatedFunctions)));
        }

        ReportProgress($"Enumerated {symbolsEnumerated}/{allTemplatedFunctionSymbols.Count} functions, found {foldables.Count} items so far.", nextLoggerOutput, (uint)allTemplatedFunctionSymbols.Count);
        logger.Log($"Finished enumerating {foldables.Count} template foldability items");
        this.DataCache.AllTemplateFoldabilityItems = foldables;

        return this.DataCache.AllTemplateFoldabilityItems;
    }

    private float CalculatePercentageSimilarity(IGrouping<string, IFunctionCodeSymbol> allFunctionsToCompare)
    {
        //TODO: TemplateFoldability: need to think about a more robust way to compare similarity...this was done quickly.

        var allPercentageSimilarities = new List<float>();

        // We'll only look at one per unique RVA to basically skip over things that are already COMDAT-folded.  So if
        // a binary has 100 copies of a template that all folded into one, and 100 more copies that all folded into one
        // we'll end up with two "groups" to compare - which is all that's interesting, as moving a function from one of
        // these groups to the other wouldn't affect size - only structural changes that eliminate a group (or shrink all
        // groups) really save space.
        var functionsGroupedByRVA = allFunctionsToCompare.GroupBy(func => (func.PrimaryBlock.RVA, BlockCount: func.Blocks.Count));

        // If there's only one function group (after COMDAT-folding), then it's of course 100% the same as itself, so
        // filter out the degenerate case
        if (functionsGroupedByRVA.Count() < 2)
        {
            return 1.0f;
        }

        IFunctionCodeSymbol? previousFunction = null;
        foreach (var functionGroup in functionsGroupedByRVA.OrderBy(group => group.Key.BlockCount))
        {
            var thisFunction = functionGroup.First();
            if (previousFunction is null)
            {
                previousFunction = thisFunction;
                continue;
            }

            allPercentageSimilarities.Add(this.Session.CompareSimilarityOfCodeBytesInBinary(previousFunction, thisFunction));
            previousFunction = thisFunction;
        }

        return allPercentageSimilarities.Average();
    }
}
