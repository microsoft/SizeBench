using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class EnumerateDuplicateDataSessionTask : SessionTask<List<DuplicateDataItem>>
{
    private readonly SessionTaskParameters _sessionTaskParameters;

    public EnumerateDuplicateDataSessionTask(SessionTaskParameters parameters,
                                             CancellationToken token,
                                             IProgress<SessionTaskProgress>? progressReporter)
        : base(parameters, progressReporter, token)
    {
        this.TaskName = "Enumerate Duplicate Data";
        this._sessionTaskParameters = parameters;
    }

    protected override List<DuplicateDataItem> ExecuteCore(ILogger logger)
    {
        if (this.DataCache.AllDuplicateDataItems != null)
        {
            logger.Log("Found duplicate data items in the cache, re-using them, hooray!");
            return this.DataCache.AllDuplicateDataItems;
        }

        if (this.DataCache.XDataHasBeenInitialized == false)
        {
            throw new InvalidOperationException("It is not valid to attempt to enumerate duplicate data before XDATA has been parsed, as that data is necessary to properly find all duplicated data.  This is a bug in SizeBench's implementation, not your usage of it.");
        }

        // We need access to all Compilands to iterate looking for duplicates, so just load those all now.
        new EnumerateLibsAndCompilandsSessionTask(this._sessionTaskParameters,
                                                  this.CancellationToken,
                                                  this.ProgressReporter).Execute(logger);

        var compilands = this.DataCache.AllCompilands!;

        var dataSymbols = FindDataSymbolsInCompilands(compilands);

        const int loggerOutputVelocity = 100;
        uint nextLoggerOutput = loggerOutputVelocity;
        var symbolsEnumerated = 0;
        var duplicates = new List<DuplicateDataItem>();
        var possibleDupes = new List<StaticDataSymbol>();
        var duplicatesInThisNameAndSizeGroup = new List<DuplicateDataItem>();
        var possibleDupesInXData = new List<ValueTuple<ISymbol, StaticDataSymbol>>();

        var sortedAndFilteredDataSymbols = dataSymbols.Where(s => s.DataKind == DataKind.DataIsFileStatic && (s.Size > 0 || s.VirtualSize > 0))
                                                      .GroupBy(s => (s.Name, s.Size, s.VirtualSize))
                                                      .ToList();

        foreach (var nameAndSizeGroup in sortedAndFilteredDataSymbols)
        {
            possibleDupes.Clear();
            duplicatesInThisNameAndSizeGroup.Clear();

            // The query above groups everything by the (name, size, virtualsize), so we know every nameAndSizeGroup here has the same name, 
            // the same Size, and the same VirtualSize, which are all pre-requisites for considering any of this data to be duplicative.
            // So now we can just look within this grouping to see if they have different RVAs and the same data bytes.
            foreach (var rawSymbol in nameAndSizeGroup)
            {
                symbolsEnumerated++;
                if (symbolsEnumerated >= nextLoggerOutput)
                {
                    ReportProgress($"Enumerated {symbolsEnumerated:N0}/{sortedAndFilteredDataSymbols.Count:N0} data symbols, found {duplicates.Count + duplicatesInThisNameAndSizeGroup.Count:N0} items with duplicates so far.", nextLoggerOutput, (uint)sortedAndFilteredDataSymbols.Count);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                this.CancellationToken.ThrowIfCancellationRequested();

                if (this.DataCache.XDataRVARanges.Contains(rawSymbol.RVA))
                {
                    // This symbol was found by the walk, but we know the name won't be right - for example every xdata symbol ends up named
                    // "$xdatasym" - so we'll just stash it off in a place to look at in a later loop and move on, it can't be a dupe here.
                    if (this.DataCache.XDataSymbolsByRVA.TryGetValue(rawSymbol.RVA, out var xdataSymbol))
                    {
                        possibleDupesInXData.Add((xdataSymbol, rawSymbol));
                    }
                    continue;
                }

                StaticDataSymbol? newDupe = null;
                var dupe = duplicatesInThisNameAndSizeGroup.FirstOrDefault(ddi => SymbolsAreTrulyDuplicates(ddi.Symbol, rawSymbol));
                if (dupe != null && rawSymbol.CompilandReferencedIn != null)
                {
                    dupe.AddReferencedCompilandIfNecessary(rawSymbol.CompilandReferencedIn, rawSymbol.RVA);
                }
                else if (null != (newDupe = possibleDupes.FirstOrDefault(s => SymbolsAreTrulyDuplicates(s, rawSymbol))) &&
                         newDupe.CompilandReferencedIn != null &&
                         rawSymbol.CompilandReferencedIn != null)
                {
                    possibleDupes.Remove(newDupe);
                    dupe = new DuplicateDataItem(newDupe, newDupe.CompilandReferencedIn);
                    dupe.AddReferencedCompilandIfNecessary(rawSymbol.CompilandReferencedIn, rawSymbol.RVA);
                    duplicatesInThisNameAndSizeGroup.Add(dupe);
                }
                else
                {
                    // We don't yet have a DuplicateDataItem for this (Name,Size) combo, but this may be found to be a dupe later so let's just keep track of it
                    // while we iterate through this nameAndSizeGroup.
                    possibleDupes.Add(rawSymbol);
                }
            }

            duplicates.AddRange(duplicatesInThisNameAndSizeGroup);
        }

        // Ok, we've finished looking at all the symbols in the normal fashion - but now we need to look at any we found in XDATA - these need to have some special logic to look up their
        // real name and location of the bytes in the binary, since DIA can't see them properly.
        foreach (var nameAndSizeGroup in possibleDupesInXData.GroupBy(s => (s.Item1.Name, s.Item1.Size)).ToList())
        {
            foreach (var rawSymbol in nameAndSizeGroup)
            {
                this.CancellationToken.ThrowIfCancellationRequested();

                var dupesOfThisXDataSymbol = nameAndSizeGroup.Where(xds => rawSymbol.Item1.RVA != xds.Item1.RVA &&
                                                                           (rawSymbol.Item1.Size == xds.Item1.Size || rawSymbol.Item1.VirtualSize == xds.Item1.VirtualSize) &&
                                                                           this.Session.CompareData(rawSymbol.Item1.RVA, xds.Item1.RVA, rawSymbol.Item1.Size)).ToList();

                if (dupesOfThisXDataSymbol.Count > 0)
                {
                    // The code below should probably be right for this, but I can't come up with any test case that can get to this kind of duplicate data so I'm just going
                    // to throw for now.  If a real dupe is found, then we can figure out how to write a test case and uncomment the code below this.
                    throw new InvalidOperationException("Duplicate xdata found for real - this is a surprise, and may be a bug in SizeBench.");
                    //var dupe = new DuplicateDataItem(rawSymbol.Item1, rawSymbol.Item2.CompilandReferencedIn);
                    //for (int i = 0; i < dupesOfThisXDataSymbol.Count; i++)
                    //    dupe.AddReferencedCompilandIfNecessary(dupesOfThisXDataSymbol[i].Item2.CompilandReferencedIn, dupesOfThisXDataSymbol[i].Item1.RVA);
                }
            }
        }

        // If we sort the ReferencedIn, it's nicer to display for UI or read in logs, and there's usually not that many of these, so just
        // paying the perf cost to sort them all isn't a big deal.
        foreach (var dupe in duplicates)
        {
            dupe.SortReferencedIn();
        }

        ReportProgress($"Enumerated {symbolsEnumerated:N0}/{sortedAndFilteredDataSymbols.Count:N0} data symbols, found {duplicates.Count:N0} items with duplicates so far.", nextLoggerOutput, (uint)sortedAndFilteredDataSymbols.Count);
        logger.Log($"Finished enumerating {duplicates.Count:N0} duplicate data items");
        this.DataCache.AllDuplicateDataItems = duplicates;

        return this.DataCache.AllDuplicateDataItems;
    }

    private List<StaticDataSymbol> FindDataSymbolsInCompilands(HashSet<Compiland> compilands)
    {
        const int loggerOutputVelocity = 10;
        uint nextLoggerOutput = loggerOutputVelocity;
        var compilandsEnumerated = 0;
        var dataSymbols = new List<StaticDataSymbol>(capacity: 500);

        foreach (var compiland in compilands)
        {
            this.CancellationToken.ThrowIfCancellationRequested();

            var allDataSymbolsInThisCompiland = this.DIAAdapter.FindAllStaticDataSymbolsWithinCompiland(compiland, this.CancellationToken);
            if (allDataSymbolsInThisCompiland != null)
            {
                dataSymbols.AddRange(allDataSymbolsInThisCompiland);
            }

            compilandsEnumerated++;
            if (compilandsEnumerated >= nextLoggerOutput)
            {
                ReportProgress($"Enumerated all data symbols in {compilandsEnumerated:N0}/{compilands.Count:N0} compilands.", nextLoggerOutput, (uint)compilands.Count);
                nextLoggerOutput += loggerOutputVelocity;
            }
        }

        return dataSymbols;
    }

    private bool SymbolsAreTrulyDuplicates(StaticDataSymbol lastSeenSymbol, StaticDataSymbol rawSymbol)
    {
        // ok, it has the same name and size - now let's see if they're really identical.
        // To be identical, the symbols must:
        //   - have different RVAs (same RVA means it got folded somewhere, perhaps by /Gw in the compiler)
        //   - point to the exact same bytes of data.  In some cases, like the "__midl_frag<X>" symbols from the MIDL compiler,
        //     we'll have the same name and size but different data - this is allowed and not a "duplicate"

        return lastSeenSymbol.RVA != rawSymbol.RVA &&
               this.Session.CompareData(lastSeenSymbol.RVA, rawSymbol.RVA, rawSymbol.Size);
    }
}
