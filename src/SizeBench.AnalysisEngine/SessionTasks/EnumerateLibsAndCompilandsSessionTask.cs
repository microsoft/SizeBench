using System.Diagnostics;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class EnumerateLibsAndCompilandsSessionTask : SessionTask<List<Library>>
{
    private readonly SessionTaskParameters _sessionTaskParameters;
    private uint _totalNumberOfItemsToReportProgressOn;

    public EnumerateLibsAndCompilandsSessionTask(SessionTaskParameters parameters,
                                                 CancellationToken token,
                                                 IProgress<SessionTaskProgress>? progress)
        : base(parameters, progress, token)
    {
        this._sessionTaskParameters = parameters;
        this.TaskName = "Enumerate LIBs and Compilands";
    }

    protected override List<Library> ExecuteCore(ILogger logger)
    {
        if (this.DataCache.AllLibs != null)
        {
            logger.Log("Found libs in the cache, re-using them, hooray!");
            return this.DataCache.AllLibs;
        }

        if (this.DataCache.PDataRVARange is null || this.DataCache.PDataSymbolsByRVA is null)
        {
            throw new InvalidOperationException("It is not valid to attempt to enumerate libs and compilands before the PDATA range and symbol RVAs has been established, as that data is necessary to properly attribute PDATA contributions.  This is a bug in SizeBench's implementation, not your usage of it.");
        }

        var binarySections = new EnumerateBinarySectionsAndCOFFGroupsSessionTask(this._sessionTaskParameters, this.CancellationToken).Execute(logger);

        var libs = new Dictionary<string, Library>();
        var compilands = new Dictionary<uint, Compiland>();

        uint contribsParsed = 0;
        const int loggerOutputVelocity = 100;
        var nextLoggerOutput = loggerOutputVelocity;

        using (var parseSectionContributionsLogger = logger.StartTaskLog("Parsing section contributions"))
        {
            var allSectionContributions = this.DIAAdapter.FindSectionContributions(parseSectionContributionsLogger, this.CancellationToken).ToList();

            this._totalNumberOfItemsToReportProgressOn = (uint)(allSectionContributions.Count + this.DataCache.PDataSymbolsByRVA.Count);

            foreach (var sectionContrib in allSectionContributions)
            {
                if (contribsParsed >= nextLoggerOutput)
                {
                    ReportProgress($"Parsed {contribsParsed}/{allSectionContributions.Count} section contributions.", contribsParsed, this._totalNumberOfItemsToReportProgressOn);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                ParseSectionContrib(sectionContrib, libs, compilands, this.DataCache.AllCOFFGroups!);
                contribsParsed++;
            }

            // One final progress report so the log shows this as "120/120" instead of "100/120" due to the throttling of progress messages in the loop
            ReportProgress($"Parsed {contribsParsed}/{allSectionContributions.Count} section contributions.", contribsParsed, this._totalNumberOfItemsToReportProgressOn);
        }

        var pdataSection = binarySections.FirstOrDefault(bs => bs.Name == ".pdata");
        if (pdataSection != null)
        {
            // Not all compilers put in a .pdata COFF Group, this may be null (seems like Clang doesn't do this, for instance)
            var pdataCOFFGroup = pdataSection.COFFGroups.FirstOrDefault(cg => cg.Name == ".pdata");
            AttributePDataSymbols(compilands, libs, pdataSection, pdataCOFFGroup, logger, contribsParsed);
        }

        logger.Log("Marking all compilands and libs as fully constructed.");
        foreach (var compiland in compilands.Values)
        {
            compiland.MarkFullyConstructed();
        }

        foreach (var lib in libs.Values)
        {
            lib.MarkFullyConstructed();
        }

        this.DataCache.AllLibs = libs.Values.ToList();
        this.DataCache.AllCompilands = compilands.Values.ToList();

        logger.Log($"Finished parsing {libs.Count} libs and {compilands.Count} compilands");

        return this.DataCache.AllLibs;
    }

    private void ParseSectionContrib(RawSectionContribution sectionContrib,
                                     Dictionary<string, Library> libs,
                                     Dictionary<uint, Compiland> compilands,
                                     IReadOnlyList<COFFGroup> coffGroups)
    {

        // If this RVA range is inside the PDATA region, it is not going to be correct anyway.  There was a bug
        // in the linker prior to VS 2017, where it would generate PDATA section contributions but could not
        // guarantee they were right - sometimes they would overlap or the same RVA would get attributed to two
        // different compilands. We compensate for this by ignoring these, and in more recent linkers they won't exist.
        //
        // To see how PDATA symbols get correctly attributed to the right compiland/lib, see the code above in
        // ExecuteCore where we do further processing on PDATA regions.
        if (this.DataCache.PDataRVARange!.Contains(sectionContrib.RVA, sectionContrib.Length))
        {
            return;
        }

        COFFGroup? coffGroup = null;
        for (var i = 0; i < coffGroups.Count; i++)
        {
            var cg = coffGroups[i];
            if (sectionContrib.RVA >= cg.RVA && sectionContrib.RVA <= (cg.RVA + Math.Max(cg.Size, cg.VirtualSize) - 1))
            {
                coffGroup = cg;
                break;
            }
        }

        if (coffGroup is null)
        {
            return;
        }

        var section = coffGroup.Section;

        Debug.Assert(section != null);

        if (!libs.TryGetValue(sectionContrib.LibName, out var lib))
        {
            lib = new Library(sectionContrib.LibName);
            libs.Add(sectionContrib.LibName, lib);
        }

        var contributingCompiland = lib.GetOrCreateCompiland(this.DataCache, sectionContrib.CompilandName, sectionContrib.CompilandSymIndexId, this._sessionTaskParameters.DIAAdapter);
        if (!compilands.ContainsKey(sectionContrib.CompilandSymIndexId))
        {
            compilands.Add(sectionContrib.CompilandSymIndexId, contributingCompiland);
        }

        var rvaRange = RVARange.FromRVAAndSize(sectionContrib.RVA, sectionContrib.Length, isVirtualSize: coffGroup.IsVirtualSizeOnly);

        contributingCompiland.GetOrCreateSectionContribution(section).AddRVARange(rvaRange);
        contributingCompiland.GetOrCreateCOFFGroupContribution(coffGroup).AddRVARange(rvaRange);
    }

    private void AttributePDataSymbols(Dictionary<uint, Compiland> compilands,
                                       Dictionary<string, Library> libs,
                                       BinarySection pdataSection,
                                       COFFGroup? pdataCOFFGroup,
                                       ILogger logger,
                                       uint contribsParsed)
    {
        if (this.DataCache.PDataSymbolsByRVA!.Count == 0)
        {
            logger.Log("No PDATA symbols to attribute");
            return;
        }

        if (libs.Count == 0 && compilands.Count == 0)
        {
            logger.Log("No compilands/libs discovered (was this binary built with clang?) - so cannot attribute PDATA symbols.");
            return;
        }

        // Now we need to check how PDATA records contribute to these compilands/libs, as they cannot be
        // parsed as part of section contributions (see comment in ParseSectionContrib).

#pragma warning disable IDE0063 // Use simple 'using' statement - this requires a careful scope, so I want to be explicit
        using (var pdataAttributionLog = logger.StartTaskLog("Attributing PDATA symbols to compilands and libs, based on TargetStartRVA"))
#pragma warning restore IDE0063 // Use simple 'using' statement
        {
            ReportProgress("Attributing PDATA symbols to compilands.", contribsParsed, this._totalNumberOfItemsToReportProgressOn);
            // We'll pre-fill this with empty lists to make the logic below simpler to read.
            var compilandPDataContributions = new Dictionary<Compiland, List<RVARange>>(capacity: compilands.Count);
            foreach (var compilandBySymIndexId in compilands)
            {
                compilandPDataContributions.Add(compilandBySymIndexId.Value, new List<RVARange>());
            }

            Compiland? compiland = null;

            // This process can be INCREDIBLY slow if we're naive about things, because large binaries can have hundreds of thousands of PDATA
            // entries and thousands of compilands.  So we're going to be careful here - if you tweak this function, be sure to understand
            // the perf consequences by opening a very large binary (windows.ui.xaml.dll from Windows is a good one).
            //
            // We'll restrict ourselves to only looking at compilands that contain any executable code as another way to filter how much we have
            // to look through.
            var compilandsWithExecutableCode = compilands.Values.Where(c => c.ContainsExecutableCode).ToList();

            uint pdataSymbolsAttributed = 0;
            const int loggerOutputVelocity = 1000;
            var nextLoggerOutput = loggerOutputVelocity;

            foreach (var pdataSymbol in this.DataCache.PDataSymbolsByRVA)
            {
                if (pdataSymbolsAttributed >= nextLoggerOutput)
                {
                    ReportProgress($"Attributed {pdataSymbolsAttributed}/{this.DataCache.PDataSymbolsByRVA.Count} PDATA symbols to compilands.", contribsParsed + pdataSymbolsAttributed, this._totalNumberOfItemsToReportProgressOn);
                    nextLoggerOutput += loggerOutputVelocity;
                    this.CancellationToken.ThrowIfCancellationRequested();
                }

                // We know these are in order, so let's collect as many as we can that are contiguously related to
                // the same compiland/lib, to reduce the number of RVA ranges being created - otherwise we'd be dumb
                // and have one RVARange per PData symbol (12 bytes) - blech.

                var targetStartRVA = pdataSymbol.Value.TargetStartRVA;
                var pdataRange = RVARange.FromRVAAndSize(pdataSymbol.Value.RVA, pdataSymbol.Value.Size);

                // We also know that PDATA symbols come grouped together, and RVA ranges for compilands are already
                // grouped so we can avoid finding the compiland containing an RVA if the existing one we found already
                // contains that RVA...this will be the case most of the time so checking for that is a significant
                // perf win.
                if (compiland is null || !compiland.Contains(targetStartRVA))
                {
                    compiland = FindCompilandContainingRVA(compilandsWithExecutableCode, targetStartRVA);
                }

                // At this point, it can rarely be the case that compiland is still null - some binaries in the Windows OS itself
                // hit this.  To avoid a crash and let the majority of other operations complete, if this is null we'll just live
                // with that and not attribute this symbol.
                if (compiland != null)
                {
                    var expandedExistingRVARange = false;
                    for (var i = 0; i < compilandPDataContributions[compiland].Count; i++)
                    {
                        // If we're contiguous with an existing range, just expand it to avoid explosion of RVA ranges.
                        if (compilandPDataContributions[compiland][i].IsAdjacentTo(pdataRange))
                        {
                            compilandPDataContributions[compiland][i] = compilandPDataContributions[compiland][i].CombineWith(pdataRange);
                            expandedExistingRVARange = true;
                            break;
                        }
                    }

                    if (!expandedExistingRVARange)
                    {
                        compilandPDataContributions[compiland].Add(pdataRange);
                    }
                }

                pdataSymbolsAttributed++;
            }

            // One final progress report to ensure it looks nice at 100%
            ReportProgress($"Attributed {pdataSymbolsAttributed}/{this.DataCache.PDataSymbolsByRVA.Count} PDATA symbols to compilands.", contribsParsed + pdataSymbolsAttributed, this._totalNumberOfItemsToReportProgressOn);

            // Some compilands may not have any PDATA contributions - so we'll skip any that have an empty list.
            foreach (var compilandPDataContribution in compilandPDataContributions.Where(kvp => kvp.Value.Count > 0))
            {
                var sectionContribution = compilandPDataContribution.Key.GetOrCreateSectionContribution(pdataSection);
                sectionContribution.AddRVARanges(compilandPDataContribution.Value);
                sectionContribution.MarkFullyConstructed();

                if (pdataCOFFGroup != null)
                {
                    var coffGroupContribution = compilandPDataContribution.Key.GetOrCreateCOFFGroupContribution(pdataCOFFGroup);
                    coffGroupContribution.AddRVARanges(compilandPDataContribution.Value);
                    coffGroupContribution.MarkFullyConstructed();
                }
            }
        }
    }

    private static Compiland? FindCompilandContainingRVA(List<Compiland> compilands, uint rva)
    {
        for (var i = 0; i < compilands.Count; i++)
        {
            if (compilands[i].ContainsExecutableCodeAtRVA(rva))
            {
                return compilands[i];
            }
        }

        return null;
    }

}
