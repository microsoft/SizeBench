using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

internal static class SymbolDiffFactory
{
    internal static SymbolDiff CreateSymbolDiff(ISymbol? beforeSymbol, ISymbol? afterSymbol, DiffSessionDataCache cache)
        => CreateSymbolDiffHelper(beforeSymbol, afterSymbol, cache, createBlockSymbolsDirectly: false);

    private static SymbolDiff CreateSymbolDiffHelper(ISymbol? beforeSymbol, ISymbol? afterSymbol, DiffSessionDataCache cache, bool createBlockSymbolsDirectly)
    {
        if (beforeSymbol is null && afterSymbol is null)
        {
            throw new ArgumentException($"Both {nameof(beforeSymbol)} and {nameof(afterSymbol)} cannot be null.", nameof(beforeSymbol));
        }

        if (TryGetSymbolDiffFromCaches(beforeSymbol, afterSymbol, cache, out var cachedSymbolDiff))
        {
            return cachedSymbolDiff;
        }

        SymbolDiff? newlyCreatedSymbolDiff;

        if ((beforeSymbol is null || beforeSymbol is CodeBlockSymbol) &&
            (afterSymbol is null || afterSymbol is CodeBlockSymbol))
        {
            if (createBlockSymbolsDirectly)
            {
                newlyCreatedSymbolDiff = new CodeBlockSymbolDiff(beforeSymbol as CodeBlockSymbol, afterSymbol as CodeBlockSymbol);
            }
            else
            {
                var parentFunctionDiff = CreateFunctionCodeSymbolDiff((beforeSymbol as CodeBlockSymbol)?.ParentFunction, (afterSymbol as CodeBlockSymbol)?.ParentFunction, cache);
                // Now just find the appropriate block diff from the function we just created and return that.  No need to bother with the caching code below, we'll already have
                // cached all the blocks as necessary when creating the function diff.
                foreach (var blockDiff in parentFunctionDiff.CodeBlockDiffs)
                {
                    if (blockDiff.BeforeSymbol == beforeSymbol && blockDiff.AfterSymbol == afterSymbol)
                    {
                        return blockDiff;
                    }
                }

                throw new InvalidOperationException($"After creating a function diff for {parentFunctionDiff.FullName}, we could not find the appropriate block diff within it.  This is a bug in SizeBench's implementation, not your usage of it.");
            }
        }
        else
        {
            newlyCreatedSymbolDiff = new SymbolDiff(beforeSymbol, afterSymbol);
        }

        InsertNewlyCreatedSymbolDiffIntoCaches(beforeSymbol, afterSymbol, cache, newlyCreatedSymbolDiff);

        return newlyCreatedSymbolDiff;
    }

    internal static FunctionCodeSymbolDiff CreateFunctionCodeSymbolDiff(IFunctionCodeSymbol? before, IFunctionCodeSymbol? after, DiffSessionDataCache cache)
    {
        if (before is null && after is null)
        {
            throw new ArgumentException($"Both {nameof(before)} and {nameof(after)} cannot be null.", nameof(before));
        }

        if (TryGetSymbolDiffFromCaches(before?.PrimaryBlock, after?.PrimaryBlock, cache, out var cachedSymbolDiff))
        {
            return ((CodeBlockSymbolDiff)cachedSymbolDiff).ParentFunctionDiff;
        }

        // When creating the diff, we should also diff all the blocks of both halves while we have them
        var allBlockDiffsInFunction = new List<CodeBlockSymbolDiff>(capacity: 10);
        var beforeBlocks = new List<CodeBlockSymbol>(before?.Blocks ?? Enumerable.Empty<CodeBlockSymbol>());
        var afterBlocks = new List<CodeBlockSymbol>(after?.Blocks ?? Enumerable.Empty<CodeBlockSymbol>());
        foreach (var beforeBlock in beforeBlocks)
        {
            var matchingAfterBlock = afterBlocks.FirstOrDefault(beforeBlock.IsVeryLikelyTheSameAs);
            if (matchingAfterBlock != null)
            {
                afterBlocks.Remove(matchingAfterBlock); // Make the list smaller so the operation isn't a full two passes (one here, one in the loop below)
            }

            allBlockDiffsInFunction.Add((CodeBlockSymbolDiff)CreateSymbolDiffHelper(beforeBlock, matchingAfterBlock, cache, createBlockSymbolsDirectly: true));
        }
        foreach (var afterBlock in afterBlocks)
        {
            // This one wasn't found in 'before' so it's new in the 'after'
            allBlockDiffsInFunction.Add((CodeBlockSymbolDiff)CreateSymbolDiffHelper(null, afterBlock, cache, createBlockSymbolsDirectly: true));
        }

        return new FunctionCodeSymbolDiff(before, after, allBlockDiffsInFunction);
    }

    private static bool TryGetSymbolDiffFromCaches(ISymbol? beforeSymbol, ISymbol? afterSymbol, DiffSessionDataCache cache, [NotNullWhen(true)] out SymbolDiff? cachedSymbolDiff)
    {
        // The RVA of 0 is special and used for some sentinel things - like pure functions or functions that were fully optimized out of the binary (which can still be important
        // when crawling wasteful virtuals, for example).  So if the RVA is zero we won't ever try to look it up in the cache - nor will we insert it into the cache.

        if (beforeSymbol is null &&
            afterSymbol!.RVA != 0 &&
            cache.AllSymbolDiffsAlreadyConstructedWithNullBeforeSymbolByAfterRVA.TryGetValue(afterSymbol.RVA, out cachedSymbolDiff))
        {
            return true;
        }
        else if (afterSymbol is null &&
                 beforeSymbol!.RVA != 0 &&
                 cache.AllSymbolDiffsAlreadyConstructedWithNullAfterSymbolByBeforeRVA.TryGetValue(beforeSymbol.RVA, out cachedSymbolDiff))
        {
            return true;
        }
        else if (beforeSymbol != null && afterSymbol != null &&
                 beforeSymbol.RVA != 0 && afterSymbol.RVA != 0 &&
                 cache.AllSymbolDiffsAlreadyConstructedByBeforeRVAThenByAfterRVA.TryGetValue(beforeSymbol.RVA, out var matchingAfterSymbols) &&
                 matchingAfterSymbols.TryGetValue(afterSymbol.RVA, out cachedSymbolDiff))
        {
            return true;
        }
        else if (beforeSymbol != null && cache.AllSymbolDiffsBySymbolFromEitherBeforeOrAfter.TryGetValue(beforeSymbol, out cachedSymbolDiff))
        {
            return true;
        }
        else if (afterSymbol != null && cache.AllSymbolDiffsBySymbolFromEitherBeforeOrAfter.TryGetValue(afterSymbol, out cachedSymbolDiff))
        {
            return true;
        }

        cachedSymbolDiff = null;
        return false;
    }

    private static void InsertNewlyCreatedSymbolDiffIntoCaches(ISymbol? beforeSymbol, ISymbol? afterSymbol, DiffSessionDataCache cache, SymbolDiff newlyCreatedSymbolDiff)
    {
        // The RVA of 0 is special and used for some sentinel things - like pure functions or functions that were fully optimized out of the binary (which can still be important
        // when crawling wasteful virtuals, for example).  So if the RVA is zero we won't ever try to look it up in the cache - nor will we insert it into the cache.

        if (beforeSymbol is null && afterSymbol!.RVA != 0)
        {
            cache.AllSymbolDiffsAlreadyConstructedWithNullBeforeSymbolByAfterRVA.Add(afterSymbol.RVA, newlyCreatedSymbolDiff);
            cache.AllSymbolDiffsBySymbolFromEitherBeforeOrAfter.Add(afterSymbol, newlyCreatedSymbolDiff);
        }
        else if (afterSymbol is null && beforeSymbol!.RVA != 0)
        {
            cache.AllSymbolDiffsAlreadyConstructedWithNullAfterSymbolByBeforeRVA.Add(beforeSymbol.RVA, newlyCreatedSymbolDiff);
            cache.AllSymbolDiffsBySymbolFromEitherBeforeOrAfter.Add(beforeSymbol, newlyCreatedSymbolDiff);
        }
        else if (beforeSymbol != null && afterSymbol != null &&
                 beforeSymbol.RVA != 0 && afterSymbol.RVA != 0)
        {
            if (!cache.AllSymbolDiffsAlreadyConstructedByBeforeRVAThenByAfterRVA.TryGetValue(beforeSymbol.RVA, out var toInsertInto))
            {
                toInsertInto = new Dictionary<uint, SymbolDiff>();
                cache.AllSymbolDiffsAlreadyConstructedByBeforeRVAThenByAfterRVA.Add(beforeSymbol.RVA, toInsertInto);
            }

            toInsertInto.Add(afterSymbol.RVA, newlyCreatedSymbolDiff);
            cache.AllSymbolDiffsBySymbolFromEitherBeforeOrAfter.Add(beforeSymbol, newlyCreatedSymbolDiff);
            cache.AllSymbolDiffsBySymbolFromEitherBeforeOrAfter.Add(afterSymbol, newlyCreatedSymbolDiff);
        }
    }
}
