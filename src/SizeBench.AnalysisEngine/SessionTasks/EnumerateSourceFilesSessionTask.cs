using System.Diagnostics;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class EnumerateSourceFilesSessionTask : SessionTask<List<SourceFile>>
{
    private readonly SessionTaskParameters _sessionTaskParameters;
    private uint _totalNumberOfItemsToReportProgressOn;

    public EnumerateSourceFilesSessionTask(SessionTaskParameters parameters,
                                           CancellationToken token,
                                           IProgress<SessionTaskProgress>? progress)
        : base(parameters, progress, token)
    {
        this._sessionTaskParameters = parameters;
        this.TaskName = "Enumerate Source Files";
    }

    protected override List<SourceFile> ExecuteCore(ILogger logger)
    {
        if (this.DataCache.AllSourceFiles != null)
        {
            logger.Log("Found source files in the cache, re-using them, hooray!");
            return this.DataCache.AllSourceFiles;
        }

        if (this.DataCache.PDataRVARange is null || this.DataCache.XDataRVARanges is null ||
            this.DataCache.PDataSymbolsByRVA is null || this.DataCache.XDataSymbolsByRVA is null)
        {
            throw new InvalidOperationException("It is not valid to attempt to enumerate source files before the PDATA and XDATA symbols have been parsed, as that data is necessary to properly attribute PDATA and XDATA contributions.  This is a bug in SizeBench's implementation, not your usage of it.");
        }

        var binarySections = new EnumerateBinarySectionsAndCOFFGroupsSessionTask(this._sessionTaskParameters, this.CancellationToken).Execute(logger);
        var libraries = new EnumerateLibsAndCompilandsSessionTask(this._sessionTaskParameters, this.CancellationToken, this.ProgressReporter).Execute(logger);

        var sourceFiles = new List<SourceFile>();
        uint sourceFilesParsed = 0;
        const int loggerOutputVelocity = 100;
        var nextLoggerOutput = loggerOutputVelocity;

        using (var parseSourceFilesFromDiaLogger = logger.StartTaskLog("Parsing source files"))
        {
            sourceFiles = this.DIAAdapter.FindSourceFiles(parseSourceFilesFromDiaLogger, this.CancellationToken).ToList();

            this._totalNumberOfItemsToReportProgressOn = (uint)(sourceFiles.Count + this.DataCache.PDataSymbolsByRVA.Count + this.DataCache.XDataSymbolsByRVA.Count);

            foreach (var sourceFile in sourceFiles)
            {
                this.CancellationToken.ThrowIfCancellationRequested();
                if (sourceFilesParsed >= nextLoggerOutput)
                {
                    ReportProgress($"Parsed {sourceFilesParsed}/{sourceFiles.Count} source files.", sourceFilesParsed, this._totalNumberOfItemsToReportProgressOn);
                    nextLoggerOutput += loggerOutputVelocity;
                }

                ParseSourceFile(sourceFile, this.DataCache.AllCOFFGroups!);
                sourceFilesParsed++;
            }

            // One final progress report so the log shows this as "120/120" instead of "100/120" due to the throttling of progress messages in the loop
            ReportProgress($"Parsed {sourceFilesParsed}/{sourceFiles.Count} source files.", sourceFilesParsed, this._totalNumberOfItemsToReportProgressOn);
        }

        // This process can be INCREDIBLY slow if we're naive about things, because large binaries can have tens of thousands of funclets, xdata and
        // pdata symbols and thousands of source files.  So we're going to be careful here - if you tweak this function, be sure to understand
        // the perf consequences by opening a very large binary (windows.ui.xaml.dll from Windows is a good one).
        //
        // We'll restrict ourselves to only looking at source files that contain any executable code as another way to filter how much we have
        // to look through.
        var sourceFilesWithExecutableCode = sourceFiles.Where(sf => sf.ContainsExecutableCode).ToList();

        // We need to do XDATA second, because XDATA symbols will in turn have PDATA - so for the PDATA attribution process below to catch everything, it'll need us to be done
        // with putting all the RVA ranges for code (above) and XDATA (here) in.
        var xdataCOFFGroup = binarySections.SelectMany(bs => bs.COFFGroups).FirstOrDefault(cg => cg.Name == ".xdata");
        if (xdataCOFFGroup != null)
        {
            AttributeXDataSymbols(sourceFilesWithExecutableCode, (uint)(sourceFiles.Count), xdataCOFFGroup, logger);
        }

        var pdataSection = binarySections.FirstOrDefault(bs => bs.Name == ".pdata");
        if (pdataSection != null)
        {
            // Not all compilers put in a .pdata COFF Group, this may be null (seems like Clang doesn't do this, for instance)
            var pdataCOFFGroup = pdataSection.COFFGroups.FirstOrDefault(cg => cg.Name == ".pdata");
            AttributePDataSymbols(sourceFilesWithExecutableCode, (uint)(sourceFiles.Count + this.DataCache.XDataSymbolsByRVA.Count), pdataSection, pdataCOFFGroup, logger);
        }

        logger.Log("Marking all source files as fully constructed.");
        foreach (var sourceFile in sourceFiles)
        {
            sourceFile.MarkFullyConstructed();
        }

        this.DataCache.AllSourceFiles = sourceFiles;

        logger.Log($"Finished parsing {sourceFiles.Count} source files");

        return this.DataCache.AllSourceFiles;
    }

    private void ParseSourceFile(SourceFile sourceFile,
                                 IReadOnlyList<COFFGroup> coffGroups)
    {
        foreach (var compilandUsingThisSourceFile in sourceFile._compilands)
        {
            foreach (var range in this.DIAAdapter.FindRVARangesForSourceFileAndCompiland(sourceFile, compilandUsingThisSourceFile, this.CancellationToken))
            {
                this.CancellationToken.ThrowIfCancellationRequested();
                COFFGroup? coffGroup = null;
                //TODO: PERF: We may want to enumerate all the ranges eagerly, then we could order them by RVAStart to cache the COFF Group lookup since many symbols
                //            will surely come from the same COFF Group in many cases, and right now we're doing this lookup logic per-range.
                for (var i = 0; i < coffGroups.Count; i++)
                {
                    var cg = coffGroups[i];
                    if (cg.IsVirtualSizeOnly)
                    {
                        if (range.RVAStart >= cg.RVA && range.RVAEnd <= (cg.RVA + cg.VirtualSize + cg.TailSlopVirtualSizeAlignment) - 1)
                        {
                            coffGroup = cg;
                            break;
                        }
                    }
                    else
                    {
                        if (range.RVAStart >= cg.RVA && range.RVAEnd <= (cg.RVA + cg.Size + cg.TailSlopSizeAlignment) - 1)
                        {
                            coffGroup = cg;
                            break;
                        }
                    }
                }

                if (coffGroup is null)
                {
                    return;
                }

                var section = coffGroup.Section;

                Debug.Assert(section != null);

                sourceFile.GetOrCreateSectionContribution(section).AddRVARange(range);
                sourceFile.GetOrCreateCOFFGroupContribution(coffGroup).AddRVARange(range);
                sourceFile.GetOrCreateCompilandContribution(compilandUsingThisSourceFile).AddRVARange(range);
            }
        }

        // Now that we've collected all the line number-based RVA ranges we can find, we need to 'compress' the RVA ranges to a minimal set - otherwise
        // finding the source files containing RVAs for XDATA and PDATA later in the process are dramatically slower, because of the sheer number of
        // line number-based RVA ranges that DIA produces.
        sourceFile.CompressRVARanges();
    }

    private void AttributeXDataSymbols(List<SourceFile> sourceFilesWithExecutableCode,
                                       uint itemsAlreadyProgressedThrough,
                                       COFFGroup xdataCOFFGroup,
                                       ILogger logger)
    {
        if (this.DataCache.XDataSymbolsByRVA!.Count == 0)
        {
            logger.Log("No XDATA symbols to attribute");
            return;
        }

        if (sourceFilesWithExecutableCode.Count == 0)
        {
            logger.Log("No source files discovered (was this binary built with clang?) - so cannot attribute XDATA symbols.");
            return;
        }

        // Now we need to check how XDATA records contribute to these source files, as the DIA line number information
        // only applies to code, it doesn't know about xdata since we parse that by hand in SizeBench.

#pragma warning disable IDE0063 // Use simple 'using' statement - this requires a careful scope, so I want to be explicit
        using (var xdataAttributionLog = logger.StartTaskLog("Attributing XDATA symbols to source files, based on TargetStartRVA"))
#pragma warning restore IDE0063 // Use simple 'using' statement
        {
            ReportProgress("Attributing XDATA symbols to source files.", itemsAlreadyProgressedThrough, this._totalNumberOfItemsToReportProgressOn);
            // We'll pre-fill this with empty lists to make the logic below simpler to read.
            var sourceFileXDataContributions = new Dictionary<SourceFile, List<RVARange>>(capacity: sourceFilesWithExecutableCode.Count);
            foreach (var sf in sourceFilesWithExecutableCode)
            {
                sourceFileXDataContributions.Add(sf, new List<RVARange>());
            }

            SourceFile? sourceFile = null;

            uint xdataSymbolsAttributed = 0;
            const int loggerOutputVelocity = 1000;
            var nextLoggerOutput = loggerOutputVelocity;

            foreach (var xdataSymbol in this.DataCache.XDataSymbolsByRVA)
            {
                if (xdataSymbolsAttributed >= nextLoggerOutput)
                {
                    ReportProgress($"Attributed {xdataSymbolsAttributed}/{this.DataCache.XDataSymbolsByRVA.Count} XDATA symbols to source files.", itemsAlreadyProgressedThrough + xdataSymbolsAttributed, this._totalNumberOfItemsToReportProgressOn);
                    nextLoggerOutput += loggerOutputVelocity;
                    this.CancellationToken.ThrowIfCancellationRequested();
                }

                // We know these are in order, so let's collect as many as we can that are contiguously related to
                // the same source file, to reduce the number of RVA ranges being created - otherwise we'd be dumb
                // and have one RVARange per XData symbol.

                var targetStartRVA = xdataSymbol.Value.TargetStartRVA;
                var xdataRange = RVARange.FromRVAAndSize(xdataSymbol.Value.RVA, xdataSymbol.Value.Size);

                // We also know that XDATA symbols come grouped together, and RVA ranges for source files are already
                // grouped so we can avoid finding the source file containing an RVA if the existing one we found already
                // contains that RVA...this will be the case most of the time so checking for that is a significant
                // perf win.
                if (sourceFile is null || !sourceFile.ContainsExecutableCodeAtRVA(targetStartRVA))
                {
                    sourceFile = FindSourceFileContainingRVA(sourceFilesWithExecutableCode, targetStartRVA);
                }

                // At this point, it can rarely be the case that sourceFile is still null.  To avoid a crash and let
                // the majority of other operations complete, if this is null we'll just live with that and not attribute
                // this symbol.
                if (sourceFile != null)
                {
                    var expandedExistingRVARange = false;
                    for (var i = 0; i < sourceFileXDataContributions[sourceFile].Count; i++)
                    {
                        // If we're contiguous with an existing range, just expand it to avoid explosion of RVA ranges.
                        if (sourceFileXDataContributions[sourceFile][i].IsAdjacentTo(xdataRange))
                        {
                            sourceFileXDataContributions[sourceFile][i] = sourceFileXDataContributions[sourceFile][i].CombineWith(xdataRange);
                            expandedExistingRVARange = true;
                            break;
                        }
                    }

                    if (!expandedExistingRVARange)
                    {
                        sourceFileXDataContributions[sourceFile].Add(xdataRange);
                    }
                }

                xdataSymbolsAttributed++;
            }

            // One final progress report to ensure it looks nice
            ReportProgress($"Attributed {xdataSymbolsAttributed}/{this.DataCache.XDataSymbolsByRVA.Count} XDATA symbols to source files.", itemsAlreadyProgressedThrough + xdataSymbolsAttributed, this._totalNumberOfItemsToReportProgressOn);

            // Some source files may not have any XDATA contributions - so we'll skip any that have an empty list.
            foreach (var sourceFileXDataContribution in sourceFileXDataContributions.Where(kvp => kvp.Value.Count > 0))
            {
                var compilandToAttributeTo = sourceFileXDataContribution.Key._compilands[0]; // TODO: SourceFile: this is wrong, how do we know what compiland to attribute the xdata to?

                var sectionContribution = sourceFileXDataContribution.Key.GetOrCreateSectionContribution(xdataCOFFGroup.Section);
                sectionContribution.AddRVARanges(sourceFileXDataContribution.Value);

                var coffGroupContribution = sourceFileXDataContribution.Key.GetOrCreateCOFFGroupContribution(xdataCOFFGroup);
                coffGroupContribution.AddRVARanges(sourceFileXDataContribution.Value);

                var compilandContribution = sourceFileXDataContribution.Key.GetOrCreateCompilandContribution(compilandToAttributeTo);
                compilandContribution.AddRVARanges(sourceFileXDataContribution.Value);
            }
        }
    }

    private void AttributePDataSymbols(List<SourceFile> sourceFilesWithExecutableCode,
                                       uint itemsAlreadyProgressedThrough,
                                       BinarySection pdataSection,
                                       COFFGroup? pdataCOFFGroup,
                                       ILogger logger)
    {
        if (this.DataCache.PDataSymbolsByRVA!.Count == 0)
        {
            logger.Log("No PDATA symbols to attribute");
            return;
        }

        if (sourceFilesWithExecutableCode.Count == 0)
        {
            logger.Log("No source files discovered (was this binary built with clang?) - so cannot attribute PDATA symbols.");
            return;
        }

        // Now we need to check how PDATA records contribute to these source files, as the DIA line number information
        // only applies to code.

#pragma warning disable IDE0063 // Use simple 'using' statement - this requires a careful scope, so I want to be explicit
        using (var pdataAttributionLog = logger.StartTaskLog("Attributing PDATA symbols to source files, based on TargetStartRVA"))
#pragma warning restore IDE0063 // Use simple 'using' statement
        {
            ReportProgress("Attributing PDATA symbols to source files.", itemsAlreadyProgressedThrough, this._totalNumberOfItemsToReportProgressOn);
            // We'll pre-fill this with empty lists to make the logic below simpler to read.
            var sourceFilePDataContributions = new Dictionary<SourceFile, List<RVARange>>(capacity: sourceFilesWithExecutableCode.Count);
            foreach (var sf in sourceFilesWithExecutableCode)
            {
                sourceFilePDataContributions.Add(sf, new List<RVARange>());
            }

            SourceFile? sourceFile = null;

            uint pdataSymbolsAttributed = 0;
            const int loggerOutputVelocity = 1000;
            var nextLoggerOutput = loggerOutputVelocity;

            foreach (var pdataSymbol in this.DataCache.PDataSymbolsByRVA)
            {
                if (pdataSymbolsAttributed >= nextLoggerOutput)
                {
                    ReportProgress($"Attributed {pdataSymbolsAttributed}/{this.DataCache.PDataSymbolsByRVA.Count} PDATA symbols to source files.", itemsAlreadyProgressedThrough + pdataSymbolsAttributed, this._totalNumberOfItemsToReportProgressOn);
                    nextLoggerOutput += loggerOutputVelocity;
                    this.CancellationToken.ThrowIfCancellationRequested();
                }

                // We know these are in order, so let's collect as many as we can that are contiguously related to
                // the same source file, to reduce the number of RVA ranges being created - otherwise we'd be dumb
                // and have one RVARange per PData symbol (12 bytes) - blech.

                var targetStartRVA = pdataSymbol.Value.TargetStartRVA;
                var pdataRange = RVARange.FromRVAAndSize(pdataSymbol.Value.RVA, pdataSymbol.Value.Size);

                // We also know that PDATA symbols come grouped together, and RVA ranges for source files are already
                // grouped so we can avoid finding the source file containing an RVA if the existing one we found already
                // contains that RVA...this will be the case most of the time so checking for that is a significant
                // perf win.
                if (sourceFile is null || !sourceFile.ContainsExecutableCodeAtRVA(targetStartRVA))
                {
                    sourceFile = FindSourceFileContainingRVA(sourceFilesWithExecutableCode, targetStartRVA);
                }

                // At this point, it can rarely be the case that sourceFile is still null.  To avoid a crash and let
                // the majority of other operations complete, if this is null we'll just live with that and not attribute
                // this symbol.
                if (sourceFile != null)
                {
                    var expandedExistingRVARange = false;
                    for (var i = 0; i < sourceFilePDataContributions[sourceFile].Count; i++)
                    {
                        // If we're contiguous with an existing range, just expand it to avoid explosion of RVA ranges.
                        if (sourceFilePDataContributions[sourceFile][i].IsAdjacentTo(pdataRange))
                        {
                            sourceFilePDataContributions[sourceFile][i] = sourceFilePDataContributions[sourceFile][i].CombineWith(pdataRange);
                            expandedExistingRVARange = true;
                            break;
                        }
                    }

                    if (!expandedExistingRVARange)
                    {
                        sourceFilePDataContributions[sourceFile].Add(pdataRange);
                    }
                }

                pdataSymbolsAttributed++;
            }

            // One final progress report to ensure it looks nice at 100%
            ReportProgress($"Attributed {pdataSymbolsAttributed}/{this.DataCache.PDataSymbolsByRVA.Count} PDATA symbols to source files.", itemsAlreadyProgressedThrough + pdataSymbolsAttributed, this._totalNumberOfItemsToReportProgressOn);

            // Some source files may not have any PDATA contributions - so we'll skip any that have an empty list.
            foreach (var sourceFilePDataContribution in sourceFilePDataContributions.Where(kvp => kvp.Value.Count > 0))
            {
                var compilandToAttributeTo = sourceFilePDataContribution.Key._compilands[0]; // TODO: SourceFile: this is wrong, how do we know what compiland to attribute the pdata to?

                var sectionContribution = sourceFilePDataContribution.Key.GetOrCreateSectionContribution(pdataSection);
                sectionContribution.AddRVARanges(sourceFilePDataContribution.Value);

                if (pdataCOFFGroup != null)
                {
                    var coffGroupContribution = sourceFilePDataContribution.Key.GetOrCreateCOFFGroupContribution(pdataCOFFGroup);
                    coffGroupContribution.AddRVARanges(sourceFilePDataContribution.Value);
                }

                var compilandContribution = sourceFilePDataContribution.Key.GetOrCreateCompilandContribution(compilandToAttributeTo);
                compilandContribution.AddRVARanges(sourceFilePDataContribution.Value);
            }
        }
    }

    private static SourceFile? FindSourceFileContainingRVA(List<SourceFile> sourceFiles, uint rva)
    {
        for (var i = 0; i < sourceFiles.Count; i++)
        {
            if (sourceFiles[i].ContainsExecutableCodeAtRVA(rva))
            {
                return sourceFiles[i];
            }
        }

        return null;
    }

}
