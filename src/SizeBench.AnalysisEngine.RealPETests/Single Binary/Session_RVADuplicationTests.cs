using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestInfrastructure;

namespace SizeBench.AnalysisEngine.RealPETests;

[DeploymentItem(@"Test PEs\External\x64\ReactNativeXaml.dll")]
[DeploymentItem(@"Test PEs\External\x64\ReactNativeXaml.pdb")]
[TestCategory(CommonTestCategories.SlowTests)]
[TestClass]
public sealed class Session_RVADuplicationTests
{
    public TestContext? TestContext { get; set; }

    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

    // PGO'd binaries are complicated and slow to open, so we don't want to re-open this for each test method.
    // But we can't afford to make this a class-level piece of state because MSTest may not clean it up while it's
    // running test methods in another class and that could cause us to have two huge sessions active in memory
    // at once (or more) which we can't afford on the Azure DevOps agenst that have only ~7GB of memory.
    // So we have just one TestMethod which just calls a bunch of "test methodlets" that all roll up to a single
    // result of pass/fail.  If it fails, unfortunately the callstack will be the way to tell which "test methodlet"
    // failed.  But this seems to be the only option to get the tests to fit into the memory constraints of the ADO agents.

    [TestMethod]
    public async Task RunAllRVADuplicationTests()
    {
        {
            using var SessionLogger = new NoOpLogger();
            await using var ReactNativeXamlSession = await Session.Create(Path.Combine(this.TestContext!.DeploymentDirectory!, "ReactNativeXaml.dll"),
                                                                          Path.Combine(this.TestContext!.DeploymentDirectory!, "ReactNativeXaml.pdb"),
                                                                          SessionLogger);

            await VerifySingleSymbolEntry(ReactNativeXamlSession);
            await VerifyNoDuplicationPerRVA(ReactNativeXamlSession);
        }

        // Force GC since these big binaries create so much memory pressure in the ADO pipelines
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
    }


    private async Task VerifySingleSymbolEntry(Session ReactNativeXamlSession)
    {
        var compilands = await ReactNativeXamlSession!.EnumerateCompilands(this.CancellationToken);
        var xamlMetadata = compilands.Single(c => c.Name.Contains("XamlMetadata.obj", StringComparison.Ordinal));
        var symbols = await ReactNativeXamlSession.EnumerateSymbolsInCompiland(xamlMetadata, this.CancellationToken);
        var propertyMap = symbols.Where(c => c.Name.Contains("xamlPropertyMap", StringComparison.Ordinal));
        Assert.AreEqual(1, propertyMap.Count(), "Verifying that xamlPropertyMap symbol occurs exactly once in XamlMetadata.obj symbols list.");
    }

    private async Task VerifyNoDuplicationPerRVA(Session ReactNativeXamlSession)
    {
        var compilands = await ReactNativeXamlSession!.EnumerateCompilands(this.CancellationToken);
        var xamlMetadata = compilands.Single(c => c.Name.Contains("XamlMetadata.obj", StringComparison.Ordinal));
        var symbols = await ReactNativeXamlSession.EnumerateSymbolsInCompiland(xamlMetadata, this.CancellationToken);

        // Not only do we want to check that we didn't find any duplicates, we want to make sure that what we found 'fills up' the entire RVA range, so we
        // didn't over-trim somewhere.  This would in theory be covered by EnumerateSymbolsInRVARangeSessionTask.SanityCheckSymbolSizesFillTheRVARange, but
        // for now that portion of the check has been commented out, until this is fixed:
        //    Product Backlog Item 3589: Properly and thoroughly parse idata (import data) from binaries, since DIA doesn't
        // When 3589 is fixed, this assert and the sanity check code below duplicated from the session task could be removed to clean things up.
        SanityCheckSymbolSizesFillTheRVARange(symbols, xamlMetadata);

        var groupsWithDuplication = symbols.GroupBy(s => new { s.Name, s.RVA }).Where(g => g.Count() > 1);
        Assert.AreEqual(0, groupsWithDuplication.Select(k => k.Key).Count(),
            "Verifying that all Name-RVA pairs in XamlMetadata.obj are unique, i.e. no duplication.");
    }

    private static void SanityCheckSymbolSizesFillTheRVARange(IReadOnlyList<ISymbol> symbolsEnumerated, Compiland compiland)
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

        foreach (var sectionContrib in compiland.SectionContributions.Values)
        {
            foreach (var rvaRange in sectionContrib.RVARanges)
            {
                long paddingFound = 0;
                long sumOfSymbolSizes = 0;
                ISymbol? previousSym = null;

                // Having the symbols in order in a separate variable is helpful when debugging failures
                var symbolsInRVAOrderInThisRVARange = symbolsEnumerated.Where(s => rvaRange.Contains(s.RVA) && s.IsCOMDATFolded == false && s.VirtualSize > 0)
                                                                       .OrderBy(s => s.RVA)
                                                                       .ToList();

                foreach (var sym in symbolsInRVAOrderInThisRVARange)
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

                // There could be up to maxPaddingExpected bytes of padding at the end of the list after the last symbol, so we'll allow that as the alignment of the entire list basically.
                const uint maxPaddingExpected = 16;
                if (rvaRange.VirtualSize > (sumOfSymbolSizesWithPadding + maxPaddingExpected))
                {
                    throw new InvalidOperationException($"The symbols discovered do not 'fill up' the expected size, so we have gaps in our analysis.  We discovered {sumOfSymbolSizesWithPadding} bytes, but we expected to find {rvaRange.VirtualSize} bytes.");
                }
                if (rvaRange.VirtualSize < sumOfSymbolSizesWithPadding)
                {
                    throw new InvalidOperationException($"The symbols discovered are too big to fit in the expected size, so we have some kind of duplication going on.  We discovered {sumOfSymbolSizesWithPadding} bytes, but we expected to find {rvaRange.VirtualSize} bytes.");
                }
            }
        }
    }
}
