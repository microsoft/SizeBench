using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal class EnumerateSymbolsInRVARangeSessionTask : SessionTask<List<ISymbol>>
{
    private readonly RVARange _rvaRange;

    public EnumerateSymbolsInRVARangeSessionTask(SessionTaskParameters parameters,
                                                 CancellationToken token,
                                                 IProgress<SessionTaskProgress>? progress,
                                                 RVARange rvaRange)
        : base(parameters, progress, token)
    {
        this._rvaRange = rvaRange;
        this.TaskName = $"Enumerate Symbols in RVA Range {rvaRange}";
    }

    protected override List<ISymbol> ExecuteCore(ILogger logger)
    {
        this.CancellationToken.ThrowIfCancellationRequested();

        if (this.DataCache.PDataHasBeenInitialized == false ||
            this.DataCache.XDataHasBeenInitialized == false ||
            this.DataCache.RsrcHasBeenInitialized == false ||
            this.DataCache.OtherPESymbolsHaveBeenInitialized == false)
        {
            throw new InvalidOperationException("It is not valid to attempt to enumerate symbols in an RVA range before the PE symbols have been parsed, as that is necessary to ensure all types of symbols are found.  This is a bug in SizeBench's implementation, not your usage of it.");
        }

        var symbolsEnumerated = new List<ISymbol>(50);

        const int loggerOutputVelocity = 100;
        var nextLoggerOutput = loggerOutputVelocity;

        // DIA does not know how to enumerate some knds of symbols, such as PDATA, XDATA, RSRC
        // and imports, among others - basically things we hand parse out of the PE file.
        //
        // We check these hand parsed symbols first, and can entirely skip DIA enumeration if we're looking at
        // pdata since RVA Ranges for PDATA should never be coalesced with other RVA Ranges (they're in
        // a separate section/COFF Group so coalescing won't happen).
        // For XDATA/OtherPESymbols we must go ahead and look through DIA in some cases, since xdata symbols can appear
        // in the middle of non-XDATA RVARanges for example .
        // Checking for xdata in purely-xdata ranges is very expensive for perf (since we end up going
        // byte-by-byte and failing over and over again) so we skip DIA if we know we were entirely within
        // xdata.
        var canSkipDIAEnumeration = this.DataCache.XDataRVARanges.FullyContains(this._rvaRange) ||
                                    this.DataCache.OtherPESymbolsRVARanges.FullyContains(this._rvaRange);

        if (this.DataCache.PDataRVARange.Contains(this._rvaRange))
        {
            // Collect all PDATA symbols in the range
            foreach (var newSymbolRVA in this.DataCache.PDataSymbolsByRVA.Keys)
            {
                if (newSymbolRVA >= this._rvaRange.RVAStart)
                {
                    var newSymbol = this.DataCache.PDataSymbolsByRVA[newSymbolRVA];

                    if (newSymbol.RVAEnd > this._rvaRange.RVAEnd)
                    {
                        break; // We can't find anymore in this range since the list is sorted, just move on
                    }

                    // ok, at this point the RVA is within the range, and the size of the
                    // symbol does not overflow to beyond the range, so we're sure the full
                    // symbol fits in the RVA range.
                    symbolsEnumerated.Add(newSymbol);
                }
            }
            canSkipDIAEnumeration = true;
        }

        if (this.DataCache.RsrcRVARange.Contains(this._rvaRange))
        {
            // Collect all RSRC symbols in the range
            foreach (var newSymbolRVA in this.DataCache.RsrcSymbolsByRVA.Keys)
            {
                if (newSymbolRVA >= this._rvaRange.RVAStart)
                {
                    var newSymbol = this.DataCache.RsrcSymbolsByRVA[newSymbolRVA];

                    if (newSymbol.RVAEnd > this._rvaRange.RVAEnd)
                    {
                        break; // We can't find anymore in this range since the list is sorted, just move on
                    }

                    // ok, at this point the RVA is within the range, and the size of the
                    // symbol does not overflow to beyond the range, so we're sure the full
                    // symbol fits in the RVA range.
                    symbolsEnumerated.Add(newSymbol);
                }
            }
            canSkipDIAEnumeration = true;
        }

        // Note that as you read this, you might be thinking we should check for the RVAStart *and* RVAEnd to be
        // included, but that's too restrictive - it's possible to have this situation:
        // XDataRVARanges: (0, 100) and (200, 300)
        // Input to this function could ask for symbols from (150, 250) so we need to find the symbols in the
        // (200, 250) range of XData thus we only care if any of the XDataRVARanges contain the RVAStart
        // *or* the RVAEnd.
        if (this.DataCache.XDataRVARanges.AtLeastPartiallyOverlapsWith(this._rvaRange))
        {
            // Collect all XDATA symbols in the range
            foreach (var newSymbolRVA in this.DataCache.XDataSymbolsByRVA.Keys)
            {
                if (newSymbolRVA >= this._rvaRange.RVAStart)
                {
                    var newSymbol = this.DataCache.XDataSymbolsByRVA[newSymbolRVA];

                    if (newSymbol.RVAEnd > this._rvaRange.RVAEnd)
                    {
                        break; // We can't find anymore in this range since the list is sorted, just move on
                    }

                    // ok, at this point the RVA is within the range, and the size of the
                    // symbol does not overflow to beyond the range, so we're sure the full
                    // symbol fits in the RVA range.
                    symbolsEnumerated.Add(newSymbol);
                }
            }
        }

        if (this.DataCache.OtherPESymbolsRVARanges.AtLeastPartiallyOverlapsWith(this._rvaRange))
        {
            foreach (var newSymbolRVA in this.DataCache.OtherPESymbolsByRVA.Keys)
            {
                if (newSymbolRVA >= this._rvaRange.RVAStart)
                {
                    var newSymbol = this.DataCache.OtherPESymbolsByRVA[newSymbolRVA];

                    if (newSymbol.RVAEnd > this._rvaRange.RVAEnd)
                    {
                        break; // We can't find anymore in this range since the list is sorted, just move on
                    }

                    // ok, at this point the RVA is within the range, and the size of the
                    // symbol does not overflow to beyond the range, so we're sure the full
                    // symbol fits in the RVA range.
                    symbolsEnumerated.Add(newSymbol);
                }
            }
        }

        // If we know we have already processed RVA range that represents PDATA, XDATA or RSRC then there
        // is no point in going through that range from a DIA standpoint. 
        if (!canSkipDIAEnumeration)
        {
            EnumerateDIASymbols(logger, nextLoggerOutput, loggerOutputVelocity, symbolsEnumerated);
        }

        logger.Log($"Symbol enumeration completed, discovered {symbolsEnumerated.Count} symbols.");

#if DEBUG
        SanityCheckSymbolSizesFillTheRVARange(symbolsEnumerated);
#endif

        return symbolsEnumerated;
    }

    private void EnumerateDIASymbols(ILogger logger, int nextLoggerOutput, int loggerOutputVelocity, List<ISymbol> symbolsEnumerated)
    {
        var otherPESymbolsByRVA = this.DataCache.OtherPESymbolsByRVA;

        foreach ((var symbol, var amountOfRVARangeExplored) in this.DIAAdapter.FindSymbolsInRVARange(this._rvaRange, this.CancellationToken))
        {
            if (this.CancellationToken.IsCancellationRequested)
            {
                logger.Log($"Cancellation requested after enumerating {symbolsEnumerated.Count} symbols, stopping now.");
                this.CancellationToken.ThrowIfCancellationRequested();
            }

            // If we already found this symbol in OtherPESymbols, then that one will be preferred and this DIA version of the symbol will be
            // ignored, as we can better control those symbols to have useful names, ordinals for import thunks, and so on.
            if (false == otherPESymbolsByRVA.ContainsKey(symbol.RVA))
            {
                symbolsEnumerated.Add(symbol);
            }

            if (symbolsEnumerated.Count > nextLoggerOutput)
            {
                ReportProgress($"Enumerated {symbolsEnumerated.Count:N0} symbols.", amountOfRVARangeExplored, this._rvaRange.VirtualSize);
                nextLoggerOutput += loggerOutputVelocity;
            }
        }
    }

#if DEBUG
    private void SanityCheckSymbolSizesFillTheRVARange(List<ISymbol> symbolsEnumerated)
    {
        // When enumerating symbols in a range it's pretty important that we 'fill' the range entirely. If this fails, it means one of two things:
        //
        // 1) We didn't discover all the symbols in this range so we have gaps in our analysis. In the name of "no byte left behind!" this means we should do better.
        // 2) We discovered too much, which means we aren't de-dup'ing enough.  Sometimes symbols appear duplicated in PDBs and we're expected to compensate for that so
        //    callers don't have to know about that implementation detail.
        //
        // This gets tricky to ensure because there is some amount of padding between symbols, and that padding amount is not defined anywhere.  So we'll walk each symbol
        // and add up the padding between them.  At some point it would be ideal to quantify the maximum expected padding between two symbols to also add that to this sanity
        // checking, but that seems difficult to know now, perhaps impossible?

        long paddingFound = 0;
        long sumOfSymbolSizes = 0;
        ISymbol? previousSym = null;

        // Having the symbols in order in a separate variable is helpful when debugging failures
        var symbolsInRVAOrder = symbolsEnumerated.Where(s => s.IsCOMDATFolded == false && s.VirtualSize > 0).OrderBy(s => s.RVA).ToList();

        foreach (var sym in symbolsInRVAOrder)
        {
            if (previousSym is null)
            {
                sumOfSymbolSizes = sym.VirtualSize;
            }
            else
            {
                if (previousSym.RVA == sym.RVA && previousSym.Size > 0 && sym.Size > 0)
                {
                    throw new InvalidOperationException("We discovered two symbols at the same RVA, this should not happen unless one of them is zero-sized (like a label in assembly).  They are:\n" +
                                                        $"{previousSym.Name} ({previousSym.GetType().Name})\n" +
                                                        $"{sym.Name} ({sym.GetType().Name})\n");
                }

                // Some symbols can appear 'in the middle of' another symbol - such as MyTestEntry which is sort of a label in the middle of a procedure in assembly code.
                // So if we detect this, we continue on to the next loop (and specifically do not set previousSym == this one, because this one isn't the 'further' RVAEnd
                // we have seen for padding calculations).
                if (sym.RVA >= previousSym.RVA && sym.RVAEnd <= previousSym.RVAEnd)
                {
                    continue;
                }

                // We subtract one because the RVA is the beginning and RVAEnd is the address of the last byte, so the symbol occupies up to the "end" of the RVAEnd byte.
                // Thus if one symbol occupies (0x10, 0x20) and another occupies (0x21, <anything>), then there is no padding.
                var paddingBetweenSymbols = (long)sym.RVA - previousSym.RVAEnd - 1;

                // Note that it's ok for one symbol to end on RVA 123 and another to start on 123 - this happens for example in coreclr.dll with JIT_CheckedWriteBarrier (an
                // assembly procedure) and JIT_CheckedWriteBarrier_End (a public symbol stuck exactly at the end of the procedure).  So we don't fail if these are equal, only
                // if the RVA we found is less than the RVAEnd of the previous one.
                if (sym.RVA < previousSym.RVAEnd)
                {
                    throw new InvalidOperationException("We sorted the symbols by RVA, yet this one is somehow 'before' the end of the previous symbol?  This should be impossible.");
                }

                sumOfSymbolSizes += sym.VirtualSize;
                paddingFound += paddingBetweenSymbols;
            }
            previousSym = sym;
        }

        var sumOfSymbolSizesWithPadding = sumOfSymbolSizes + paddingFound;

        var sizeOfRangeBeingEnumerated = this._rvaRange.VirtualSize;

        // For now this "gap finding" code is disabled because we have at least one known gap that'll need to be addressed first:
        //   Product Backlog Item 3589: Properly and thoroughly parse idata (import data) from binaries, since DIA doesn't
        //
        // There could be up to maxPaddingExpected bytes of padding at the end of the list after the last symbol, so we'll allow that as the alignment of the entire list basically.
        // In the case of CFG, there is a special function called __guard_dispatch_icall_nop which is intentionally misdeclared as data as part of how it works.  That function seems to
        // have an alignment requirement and be the first thing in its COFF Group (.text$mn$00) so we can end up with 16 bytes of padding before it begins, resulting in up to 16 bytes 'leading'
        // and 16 bytes 'trailing' padding as the most seen in practice to tolerate with this sanity check.
        //const uint maxPaddingExpected = 16 + 16;
        //if (sizeOfRangeBeingEnumerated > (sumOfSymbolSizesWithPadding + maxPaddingExpected))
        //{
        //    throw new InvalidOperationException($"The symbols discovered do not 'fill up' this RVA range, so we have gaps in our analysis.  We discovered {sumOfSymbolSizesWithPadding} bytes, but the RVA range is {sizeOfRangeBeingEnumerated} bytes.");
        //}
        if (sizeOfRangeBeingEnumerated < sumOfSymbolSizesWithPadding)
        {
            throw new InvalidOperationException($"The symbols discovered are too big to fit in this RVA range, so we have some kind of duplication going on.  We discovered {sumOfSymbolSizesWithPadding} bytes, but the RVA range is {sizeOfRangeBeingEnumerated} bytes.");
        }
    }
#endif
}
