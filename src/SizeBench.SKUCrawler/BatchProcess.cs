using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.AsyncInfrastructure;
using SizeBench.Logging;
using SizeBench.Threading.Tasks.Schedulers;

namespace SizeBench.SKUCrawler;

internal class BatchProcess : IDisposable
{
    // At least in SKUCrawler we want to 'fold up' all the BlockSymbols from a function up into the Function.
    // At some point it'd be good to have a way to do this in the Analysis Engine for other customers, as BlockSymbols suck
    // for diffing in the GUI too - but that needs a lot more thought because it breaks something important by allowing a
    // single symbol to contribute to multiple COFF Groups and how to visualize that in the UI and represent that in the
    // object model is hard.  So for now, to unblock SKUCrawler progress, this 'folding up blocks' functionality is here.
    private sealed class SKUCrawlerSymbol
    {
        public string Name = String.Empty;
        public string? DetemplatedName;
        public uint Size;
        public SKUCrawlerSymbol() { }
        public SKUCrawlerSymbol(ISymbol sym)
        {
            if (sym is CodeBlockSymbol)
            {
                throw new ArgumentException("Don't try to construct a SKUCrawlerSymbol from a block", nameof(sym));
            }

            this.Name = sym.Name;
            this.DetemplatedName = null;
            this.Size = sym.Size;
        }
    }

    private readonly string _logFilenameBase;

    public string BinaryRoot { get; set; } = String.Empty;
    public bool IncludeWastefulVirtuals { get; set; }
    public bool IncludeCodeSymbols { get; set; }

    private string DbFilename => $"{this._logFilenameBase}.db";

    private class ProductBinaryAnalysisResults
    {
        public ProductBinaryAnalysisResults(string binaryPath)
        {
            this.binaryPath = binaryPath;
        }

        public readonly string binaryPath;
        public long fullBinarySize;
        public long openingTookMs;
        public IReadOnlyList<BinarySection>? sections;
        public long sectionEnumerationTookMs;
        public IReadOnlyList<Library>? libs;
        public long libEnumerationTookMs;
        public IReadOnlyList<SourceFile>? sourceFiles;
        public long sourceFileEnumerationTookMs;
        public IReadOnlyList<DuplicateDataItem>? duplicateDataItems;
        public long ddiEnumerationTookMs;
        public IReadOnlyList<WastefulVirtualItem>? wastefulVirtualItems;
        public long wviEnumerationTookMs;
        public IReadOnlyList<AnnotationSymbol>? annotations;
        public long annotationEnumerationTookMs;
        public Dictionary<Tuple<Compiland, SourceFile>, List<SKUCrawlerSymbol>>? codeSymbolsInAllSourceFiles;
        public long codeSymbolsInSourceFilesEnumerationTookMs;
        public Exception? errorDuringProcessing;
    }
    private readonly BlockingCollection<ProductBinaryAnalysisResults> _databaseWriteQueue = new BlockingCollection<ProductBinaryAnalysisResults>();

    private readonly int _batchNumber;
    private readonly List<ProductBinary> _productBinariesInThisBatch;

    private static readonly Dictionary<ToolLanguage, string> ToolLanguageFriendlyNames = new Dictionary<ToolLanguage, string>();

    public BatchProcess(int batchNumber, List<ProductBinary> productBinariesInThisBatch, string logFilenameBase)
    {
        this._batchNumber = batchNumber;
        this._productBinariesInThisBatch = productBinariesInThisBatch;
        this._logFilenameBase = logFilenameBase;

        ToolLanguageFriendlyNames.Clear();
        foreach (var toolLanguage in EnumHelper<ToolLanguage>.GetValues())
        {
            ToolLanguageFriendlyNames.Add(toolLanguage, EnumHelper<ToolLanguage>.GetDisplayValue(toolLanguage));
        }

    }

    public async Task AnalyzeBatch(IApplicationLogger appLogger)
    {
        //TODO: SKUCrawler: check if Parallel.ForEachAsync might be a simpler way of writing this code, once on .NET 6
        using var taskScheduler = new QueuedTaskScheduler(threadCount: Environment.ProcessorCount * 10);
        var taskFactory = new TaskFactory(taskScheduler);
        var tasks = new List<Task>(100);

        using (var taskLog = appLogger.StartTaskLog("Ensure SQLite database exists and conenction is open"))
        {
            EnsureDatabaseCreated(taskLog);
        }

        var databaseWriterTask = Task.Factory.StartNew(() => WriteToDatabase(appLogger), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        for (var i = 0; i < this._productBinariesInThisBatch.Count; i++)
        {
            var binary = this._productBinariesInThisBatch[i];
            var log = appLogger.CreateSessionLog(binary.BinaryPath);
            var binaryNumber = i + 1;
            tasks.Add(taskFactory.StartNew(async () =>
            {
                await Console.Out.WriteLineAsync($"{DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} Batch={this._batchNumber:000}, TID={Environment.CurrentManagedThreadId:000}: {binaryNumber:00}/{this._productBinariesInThisBatch.Count}: Analyzing {binary.BinaryPath}");
                AnalyzeOneBinaryOnThreadPool(log, binary.BinaryPath, binary.PdbPath);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, taskScheduler));
        }

        await taskFactory.ContinueWhenAll(tasks.ToArray(),
                                            _ => this._databaseWriteQueue.CompleteAdding());

        await databaseWriterTask;
    }

    private void AnalyzeOneBinaryOnThreadPool(ILogger log, string binaryPath, string pdbPath)
    {
        AsyncPump.Run(
            async delegate
            {
                var results = new ProductBinaryAnalysisResults(binaryPath);

                try
                {
                    if (!File.Exists(binaryPath))
                    {
                        results.errorDuringProcessing = new IOException("Binary file not found");
                        return;
                    }
                    else
                    {
                        results.fullBinarySize = new FileInfo(binaryPath).Length;
                    }

                    if (!File.Exists(pdbPath))
                    {
                        results.errorDuringProcessing = new IOException("PDB file not found");
                        return;
                    }

                    var openingWatch = Stopwatch.StartNew();
                    var session = await Session.Create(binaryPath, pdbPath, log);
                    openingWatch.Stop();
                    results.openingTookMs = openingWatch.ElapsedMilliseconds;
                    await AnalyzeOneBinary(results, session, log);
                }
                catch (BinaryNotAnalyzableException bnae)
                {
                    // This is just because it's managed code, we don't need to bother logging this now, it's a very well-understood
                    // limitation.
                    results.errorDuringProcessing = bnae;
                }
#pragma warning disable CA1031 // Do not catch general exception types - if we can't analyze this one, maybe we can do others, so keep going
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    log.LogException($"Failed to analyze {binaryPath}", ex);
                    results.errorDuringProcessing = ex;
                }
                finally
                {
                    this._databaseWriteQueue.Add(results);
                }
            });
    }

    private async Task AnalyzeOneBinary(ProductBinaryAnalysisResults results, Session session, ILogger log)
    {
        using (log.StartTaskLog("Enumerating sections and COFF Groups"))
        {
            var enumSectionsWatch = Stopwatch.StartNew();
            results.sections = await session.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None);
            enumSectionsWatch.Stop();
            results.sectionEnumerationTookMs = enumSectionsWatch.ElapsedMilliseconds;
        }

        using (log.StartTaskLog("Enumerating libs and compilands"))
        {
            var enumLibsWatch = Stopwatch.StartNew();
            results.libs = await session.EnumerateLibs(CancellationToken.None);
            enumLibsWatch.Stop();
            results.libEnumerationTookMs = enumLibsWatch.ElapsedMilliseconds;
        }

        using (log.StartTaskLog("Enumerating source files"))
        {
            var enumSourceFilesWatch = Stopwatch.StartNew();
            results.sourceFiles = await session.EnumerateSourceFiles(CancellationToken.None);
            enumSourceFilesWatch.Stop();
            results.sourceFileEnumerationTookMs = enumSourceFilesWatch.ElapsedMilliseconds;
        }

        using (log.StartTaskLog("Enumerating duplicate data"))
        {
            var ddiWatch = Stopwatch.StartNew();
            results.duplicateDataItems = await session.EnumerateDuplicateDataItems(CancellationToken.None);
            ddiWatch.Stop();
            results.ddiEnumerationTookMs = ddiWatch.ElapsedMilliseconds;
        }

        if (this.IncludeWastefulVirtuals)
        {
            using (log.StartTaskLog("Enumerating wasteful virtuals"))
            {
                var wviWatch = Stopwatch.StartNew();
                results.wastefulVirtualItems = await session.EnumerateWastefulVirtuals(CancellationToken.None);
                wviWatch.Stop();
                results.wviEnumerationTookMs = wviWatch.ElapsedMilliseconds;
            }
        }

        using (log.StartTaskLog("Enumerating Annotations"))
        {
            var annotationWatch = Stopwatch.StartNew();
            results.annotations = await session.EnumerateAnnotations(CancellationToken.None);
            annotationWatch.Stop();
            results.annotationEnumerationTookMs = annotationWatch.ElapsedMilliseconds;
        }

        if (this.IncludeCodeSymbols)
        {
            using (log.StartTaskLog("Enumerating Code Symbols In All Source Files"))
            {
                await CrawlCodeSymbols(results, session);
            }
        }
    }

    private static async Task CrawlCodeSymbols(ProductBinaryAnalysisResults results, Session session)
    {
        if (results.libs is null)
        {
            return;
        }

        var symbolsInSourceFilesWatch = Stopwatch.StartNew();
        results.codeSymbolsInAllSourceFiles = new Dictionary<Tuple<Compiland, SourceFile>, List<SKUCrawlerSymbol>>();

        foreach (var lib in results.libs)
        {
            foreach (var compiland in lib.Compilands.Values)
            {
                if (!compiland.ContainsExecutableCode)
                {
                    continue;
                }

                var allCodeSymbolsInCompiland = await session.EnumerateSymbolsInCompiland(compiland,
                                                                                          new SymbolEnumerationOptions() { OnlyCodeSymbols = true },
                                                                                          CancellationToken.None);

                // TODO: need to deal with BlockSymbol somehow, do we always detect the function correctly and we can somehow 'throw away' or coalesce blocks to add to the size of their parent
                //       function?
                if (allCodeSymbolsInCompiland.Count > 0 && results.sourceFiles != null)
                {
                    // We want to group the symbols by what source file they're from, so the database can have a SourceFileID too.  It's much faster to do this here
                    // than on the Session/AnalysisEngine side, otherwise the AnalysisEngine code needs to know about this very specific use case to avoid some really
                    // large numbers of RVARanges to enumerate.
                    // An an example, the windows.ui.xaml.dll binary in Windows has 5000+ source files, so if we enumerated each sourcefile/compiland contribution it'd
                    // mean thousands upon thousands of EnumerateRVARangeSessionTasks objects get spun up and do their work.  That's very slow.
                    // Instead, we enumerate by compiland (only a few hundred of those contain any code), and then here we can fairly quickly group it into source files.

                    var sourceFilesThatThisCompilandContributesTo = results.sourceFiles.Where(sf => sf.Size > 0 && sf.CompilandContributions.ContainsKey(compiland));
                    foreach (var sourceFile in sourceFilesThatThisCompilandContributesTo)
                    {
                        var contributionToThisCompiland = sourceFile.CompilandContributions[compiland];
                        var codeSymbolsInThisSourceFileAndCompiland = allCodeSymbolsInCompiland.Where(sym => contributionToThisCompiland.Contains(sym.RVA, sym.Size)).ToList();
                        if (codeSymbolsInThisSourceFileAndCompiland.Count > 0)
                        {
                            var symbolsWithBlocksRolledUp = new Dictionary<ISymbol, SKUCrawlerSymbol>();
                            foreach (var symbol in codeSymbolsInThisSourceFileAndCompiland)
                            {
                                if (symbol is SeparatedCodeBlockSymbol separatedBlock)
                                {
                                    var parentFunction = separatedBlock.ParentFunction;
                                    var primaryBlock = parentFunction.PrimaryBlock;

                                    if (symbolsWithBlocksRolledUp.TryGetValue(primaryBlock, out var skuSymbolForFunction) &&
                                        skuSymbolForFunction != null)
                                    {
                                        // If the parent function was already found, just increment its size.
                                        skuSymbolForFunction.Size += separatedBlock.Size;
                                    }
                                    else
                                    {
                                        // If we found the block before the parent function, we need to insert the parent function and then add the block's size
                                        // And then we remove that function from the list of things we're enumerating, to not duplicately insert the function.
                                        var skuSymbol = new SKUCrawlerSymbol()
                                        {
                                            Name = parentFunction.FormattedName.IncludeParentType,
                                            DetemplatedName = parentFunction.FormattedName.IncludeParentType.Contains('<', StringComparison.Ordinal) ? SymbolNameHelper.FunctionToGenericTemplatedName(parentFunction) : null,
                                            Size = primaryBlock.Size + separatedBlock.Size
                                        };
                                        symbolsWithBlocksRolledUp.Add(primaryBlock, skuSymbol);
                                        codeSymbolsInThisSourceFileAndCompiland.Remove(primaryBlock);
                                    }
                                }
                                else if (symbol is PrimaryCodeBlockSymbol primaryBlock)
                                {
                                    var skuSymbol = new SKUCrawlerSymbol()
                                    {
                                        Name = primaryBlock.ParentFunction.FormattedName.IncludeParentType,
                                        DetemplatedName = primaryBlock.ParentFunction.FormattedName.IncludeParentType.Contains('<', StringComparison.Ordinal) ? SymbolNameHelper.FunctionToGenericTemplatedName(primaryBlock.ParentFunction) : null,
                                        Size = primaryBlock.Size
                                    };
                                    symbolsWithBlocksRolledUp.Add(primaryBlock, skuSymbol);
                                }
                                else
                                {
                                    var skuSymbol = new SKUCrawlerSymbol()
                                    {
                                        Name = symbol.Name,
                                        DetemplatedName = null,
                                        Size = symbol.Size
                                    };
                                    symbolsWithBlocksRolledUp.Add(symbol, skuSymbol);
                                }
                            }
                            results.codeSymbolsInAllSourceFiles.Add(Tuple.Create(compiland, sourceFile), symbolsWithBlocksRolledUp.Values.ToList());
                        }
                    }
                }
            }
        }
        symbolsInSourceFilesWatch.Stop();
        results.codeSymbolsInSourceFilesEnumerationTookMs = symbolsInSourceFilesWatch.ElapsedMilliseconds;
    }

    private void WriteToDatabase(IApplicationLogger logger)
    {
        using var taskLog = logger.StartTaskLog("Write binary information to database");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder()
        {
            DataSource = this.DbFilename
        }.ToString());
        connection.Open();

        foreach (var databaseWrite in this._databaseWriteQueue.GetConsumingEnumerable())
        {
            try
            {
                var compilandsToDatabaseIDs = new Dictionary<Compiland, int>();
                var symbolsToDatabaseIDs = new Dictionary<uint, Dictionary<string, int>>();

                using var transaction = connection.BeginTransaction();
                var binaryID = InsertBinary(connection, transaction, databaseWrite);

                InsertPerfStats(connection, transaction, binaryID, databaseWrite);

                if (databaseWrite.sections != null)
                {
                    foreach (var section in databaseWrite.sections)
                    {
                        InsertSectionAndCOFFGroupsInThatSection(connection, transaction, binaryID, section);
                    }
                }

                if (databaseWrite.libs != null)
                {
                    foreach (var lib in databaseWrite.libs)
                    {
                        InsertLibAndCompilands(connection, transaction, binaryID, lib, compilandsToDatabaseIDs);
                    }
                }

                var sourceFilesToDatabaseIDs = new Dictionary<SourceFile, int>();

                // We need to establish an ID for the 'null' source file since some annotations do not have a source file but still need to
                // get their foreign key set up.
                var sourceFileNullID = -1;

                if (databaseWrite.sourceFiles != null)
                {
                    sourceFileNullID = InsertSourceFile(connection, transaction, binaryID, null, sourceFilesToDatabaseIDs);

                    foreach (var sf in databaseWrite.sourceFiles)
                    {
                        InsertSourceFile(connection, transaction, binaryID, sf, sourceFilesToDatabaseIDs);
                    }
                }

                if (databaseWrite.duplicateDataItems != null)
                {
                    foreach (var ddi in databaseWrite.duplicateDataItems)
                    {
                        InsertDuplicateDataItem(connection, transaction, binaryID, symbolsToDatabaseIDs, ddi);
                    }
                }

                if (this.IncludeWastefulVirtuals && databaseWrite.wastefulVirtualItems != null)
                {
                    foreach (var wvi in databaseWrite.wastefulVirtualItems)
                    {
                        InsertWastefulVirtualItems(connection, transaction, binaryID, wvi);
                    }
                }

                if (databaseWrite.annotations != null)
                {
                    foreach (var annotation in databaseWrite.annotations)
                    {
                        InsertAnnotation(connection, transaction, binaryID, sourceFilesToDatabaseIDs, sourceFileNullID, annotation);
                    }
                }

                if (this.IncludeCodeSymbols && databaseWrite.codeSymbolsInAllSourceFiles != null)
                {
                    foreach (var symbolsInCompilandAndSourceFile in databaseWrite.codeSymbolsInAllSourceFiles)
                    {
                        InsertSymbolsInSourceFileAndCompiland(connection, transaction,
                                                              compilandsToDatabaseIDs[symbolsInCompilandAndSourceFile.Key.Item1],
                                                              sourceFilesToDatabaseIDs[symbolsInCompilandAndSourceFile.Key.Item2],
                                                              symbolsToDatabaseIDs, symbolsInCompilandAndSourceFile.Value);
                    }
                }

                if (databaseWrite.errorDuringProcessing != null)
                {
                    InsertError(connection, transaction, binaryID, databaseWrite.errorDuringProcessing);
                }

                transaction.Commit();
            }
#pragma warning disable CA1031 // Do not catch general exception types - if we throw trying to write one binary, maybe we can still write most of them, let's keep going
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Program.LogExceptionAndReportToStdErr("Database write threw an exception!", ex, logger);
            }
        }
    }

    private const string _BinariesTable = "Binaries";
    private const string _PerfStatsTable = "PerfStats";
    private const string _SectionTableName = "Sections";
    private const string _COFFGroupTableName = "COFFGroups";
    private const string _LibsTableName = "Libs";
    private const string _CompilandsTableName = "Compilands";
    private const string _DuplicateDataTableName = "DuplicateData";
    private const string _WastefulVirtualsTypeTableName = "WastefulVirtualTypes";
    private const string _WastefulVirtualsFunctionTableName = "WastefulVirtualFunctions";
    private const string _SourceFilesTableName = "SourceFiles";
    private const string _AnnotationsTableName = "Annotations";
    private const string _SymbolsTableName = "Symbols";
    private const string _SymbolLocationsTableName = "SymbolLocations";
    private const string _ErrorsTableName = "Errors";

    private int InsertBinary(SqliteConnection connection, SqliteTransaction transaction, ProductBinaryAnalysisResults results)
    {
        var binaryName = !String.IsNullOrEmpty(this.BinaryRoot)
                       ? Regex.Replace(results.binaryPath, "^" + this.BinaryRoot.Replace(@"\", @"\\", StringComparison.Ordinal), String.Empty, RegexOptions.IgnoreCase).TrimStart('\\')
                       : Path.GetFileName(results.binaryPath);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO {_BinariesTable} " +
                              $"(Name, Size) " +
                              $"VALUES " +
                              $"(@Name, @Size)";

        command.Parameters.AddWithValue("@Name", binaryName);
        command.Parameters.AddWithValue("@Size", results.fullBinarySize);
        command.ExecuteNonQuery();

        command.CommandText = "select last_insert_rowid()";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void InsertPerfStats(SqliteConnection connection, SqliteTransaction transaction, int binaryID, ProductBinaryAnalysisResults results)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
                            $"INSERT INTO {_PerfStatsTable} " +
                            $"(BinaryID, OpeningTookMs, SectionsTookMs, LibsTookMs, SourceFilesTookMs, DDITookMs, WVITookMs, AnnotationsTookMs, SymbolsInCompilandsTookMs) " +
                            $"VALUES " +
                            $"(@BinaryID, @OpeningTookMs, @SectionsTookMs, @LibsTookMs, @SourceFilesTookMs, @DDITookMs, @WVITookMs, @AnnotationsTookMs, @SymbolsInCompilandsTookMs)";

        command.Parameters.AddWithValue("@BinaryID", binaryID);
        command.Parameters.AddWithValue("@OpeningTookMs", results.openingTookMs);
        command.Parameters.AddWithValue("@SectionsTookMs", results.sectionEnumerationTookMs);
        command.Parameters.AddWithValue("@LibsTookMs", results.libEnumerationTookMs);
        command.Parameters.AddWithValue("@SourceFilesTookMs", results.sourceFileEnumerationTookMs);
        command.Parameters.AddWithValue("@DDITookMs", results.ddiEnumerationTookMs);
        command.Parameters.AddWithValue("@WVITookMs", results.wviEnumerationTookMs);
        command.Parameters.AddWithValue("@AnnotationsTookMs", results.annotationEnumerationTookMs);
        command.Parameters.AddWithValue("@SymbolsInCompilandsTookMs", results.codeSymbolsInSourceFilesEnumerationTookMs);
        command.ExecuteNonQuery();
    }

    private static void InsertSectionAndCOFFGroupsInThatSection(SqliteConnection connection, SqliteTransaction transaction, int binaryID, BinarySection section)
    {
        var sectionID = 0;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                            $"INSERT INTO {_SectionTableName} " +
                            $"(BinaryID, SectionName, Size, VirtualSize) " +
                            $"VALUES " +
                            $"(@BinaryID, @SectionName, @Size, @VirtualSize)";

            command.Parameters.AddWithValue("@BinaryID", binaryID);
            command.Parameters.AddWithValue("@SectionName", section.Name);
            command.Parameters.AddWithValue("@Size", section.Size);
            command.Parameters.AddWithValue("@VirtualSize", section.VirtualSize);
            command.ExecuteNonQuery();

            command.CommandText = "select last_insert_rowid()";
            sectionID = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                            $"INSERT INTO {_COFFGroupTableName} " +
                            $"(BinaryID, BinarySectionID, COFFGroupName, Size, VirtualSize) " +
                            $"VALUES " +
                            $"(@BinaryID, @BinarySectionID, @COFFGroupName, @Size, @VirtualSize)";

            command.Parameters.AddWithValue("@BinaryID", binaryID);
            command.Parameters.AddWithValue("@BinarySectionID", sectionID);
            command.Parameters.AddWithValue("@COFFGroupName", String.Empty);
            command.Parameters.AddWithValue("@Size", 0);
            command.Parameters.AddWithValue("@VirtualSize", 0);

            foreach (var cg in section.COFFGroups)
            {
                command.Parameters["@COFFGroupName"].Value = cg.Name;
                command.Parameters["@Size"].Value = cg.Size;
                command.Parameters["@VirtualSize"].Value = cg.VirtualSize;
                command.ExecuteNonQuery();
            }
        }
    }

    private static void InsertLibAndCompilands(SqliteConnection connection, SqliteTransaction transaction, int binaryID, Library lib, Dictionary<Compiland, int> compilandsToDatabaseIDs)
    {
        var libID = 0;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                            $"INSERT INTO {_LibsTableName} " +
                            $"(BinaryID, LibName, Size) " +
                            $"VALUES " +
                            $"(@BinaryID, @LibName, @Size)";

            command.Parameters.AddWithValue("@BinaryID", binaryID);
            command.Parameters.AddWithValue("@LibName", lib.Name);
            command.Parameters.AddWithValue("@Size", lib.Size);
            command.ExecuteNonQuery();

            command.CommandText = "select last_insert_rowid()";
            libID = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                            $"INSERT INTO {_CompilandsTableName} " +
                            $"(BinaryID, BinaryLibID, CompilandName, Size, CommandLine, RTTIEnabled, Language, FrontEndVersion, BackEndVersion) " +
                            $"VALUES " +
                            $"(@BinaryID, @BinaryLibID, @CompilandName, @Size, @CommandLine, @RTTIEnabled, @Language, @FrontEndVersion, @BackEndVersion)";

            command.Parameters.AddWithValue("@BinaryID", binaryID);
            command.Parameters.AddWithValue("@BinaryLibID", libID);
            command.Parameters.AddWithValue("@CompilandName", String.Empty);
            command.Parameters.AddWithValue("@Size", 0);
            command.Parameters.AddWithValue("@CommandLine", String.Empty);
            command.Parameters.AddWithValue("@RTTIEnabled", 0);
            command.Parameters.AddWithValue("@Language", String.Empty);
            command.Parameters.AddWithValue("@FrontEndVersion", String.Empty);
            command.Parameters.AddWithValue("@BackEndVersion", String.Empty);


            foreach (var compiland in lib.Compilands.Values)
            {
                command.CommandText =
                            $"INSERT INTO {_CompilandsTableName} " +
                            $"(BinaryID, BinaryLibID, CompilandName, Size, CommandLine, RTTIEnabled, Language, FrontEndVersion, BackEndVersion) " +
                            $"VALUES " +
                            $"(@BinaryID, @BinaryLibID, @CompilandName, @Size, @CommandLine, @RTTIEnabled, @Language, @FrontEndVersion, @BackEndVersion)";

                command.Parameters["@CompilandName"].Value = compiland.Name;
                command.Parameters["@Size"].Value = compiland.Size;
                command.Parameters["@CommandLine"].Value = compiland.CommandLine;
                command.Parameters["@RTTIEnabled"].Value = compiland.RTTIEnabled ? 1 : 0;
                command.Parameters["@Language"].Value = ToolLanguageFriendlyNames[compiland.ToolLanguage];
                command.Parameters["@FrontEndVersion"].Value = compiland.ToolFrontEndVersion.ToString();
                command.Parameters["@BackEndVersion"].Value = compiland.ToolBackEndVersion.ToString();
                command.ExecuteNonQuery();

                command.CommandText = "select last_insert_rowid()";
                compilandsToDatabaseIDs.Add(compiland, Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture));
            }
        }
    }

    private static int InsertSourceFile(SqliteConnection connection, SqliteTransaction transaction, int binaryID, SourceFile? sourceFile, Dictionary<SourceFile, int> sourceFilesToDatabaseIDs)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
                        $"INSERT INTO {_SourceFilesTableName} " +
                        $"(BinaryID, SourceFileName, Size) " +
                        $"VALUES " +
                        $"(@BinaryID, @SourceFileName, @Size)";

        command.Parameters.AddWithValue("@BinaryID", binaryID);
        command.Parameters.AddWithValue("@SourceFileName", sourceFile?.Name ?? "unknown source file");
        command.Parameters.AddWithValue("@Size", sourceFile?.Size ?? 0);
        command.ExecuteNonQuery();

        command.CommandText = "select last_insert_rowid()";
        var newID = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);

        if (sourceFile != null)
        {
            sourceFilesToDatabaseIDs.Add(sourceFile, newID);
        }

        return newID;
    }

    private static int InsertSymbol(SqliteConnection connection, SqliteTransaction transaction, Dictionary<uint, Dictionary<string, int>> symbolsToDatabaseIDs, SKUCrawlerSymbol skuSymbol)
    {
        if (symbolsToDatabaseIDs.TryGetValue(skuSymbol.Size, out var symbolsOfCorrectSize))
        {
            if (symbolsOfCorrectSize != null && symbolsOfCorrectSize.TryGetValue(skuSymbol.Name, out var existingSymbolID))
            {
                return existingSymbolID;
            }
        }

        // No symbol with this name and size exists, so we'll insert it now.  This keeps the database from having tons of copies of the SymbolName string, and makes it easier
        // to query across binaries for like symbols, when looking for SKU-wide opportunities across binaries and files.
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
                        $"INSERT INTO {_SymbolsTableName} " +
                        $"(SymbolName, SymbolDetemplatedName, Size) " +
                        $"VALUES " +
                        $"(@SymbolName, @SymbolDetemplatedName, @Size)";

        command.Parameters.AddWithValue("@SymbolName", skuSymbol.Name);
        if (skuSymbol.DetemplatedName != null)
        {
            command.Parameters.AddWithValue("@SymbolDetemplatedName", skuSymbol.DetemplatedName);
        }
        else
        {
            command.Parameters.AddWithValue("@SymbolDetemplatedName", DBNull.Value);
        }

        command.Parameters.AddWithValue("@Size", skuSymbol.Size);
        command.ExecuteNonQuery();

        command.CommandText = "select last_insert_rowid()";
        var symbolIDJustInserted = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);

        if (symbolsOfCorrectSize != null)
        {
            symbolsOfCorrectSize.Add(skuSymbol.Name, symbolIDJustInserted);
        }
        else
        {
            var newSymbolsOfCorrectSize = new Dictionary<string, int>()
                {
                    { skuSymbol.Name, symbolIDJustInserted }
                };
            symbolsToDatabaseIDs.Add(skuSymbol.Size, newSymbolsOfCorrectSize);
        }

        return symbolIDJustInserted;
    }

    private static void InsertDuplicateDataItem(SqliteConnection connection, SqliteTransaction transaction, int binaryID, Dictionary<uint, Dictionary<string, int>> symbolsToDatabaseIDs, DuplicateDataItem ddi)
    {
        var symbolID = InsertSymbol(connection, transaction, symbolsToDatabaseIDs, new SKUCrawlerSymbol(ddi.Symbol));

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
                        $"INSERT INTO {_DuplicateDataTableName} " +
                        $"(BinaryID, SymbolID, WastedSize) " +
                        $"VALUES " +
                        $"(@BinaryID, @SymbolID, @WastedSize)";

        command.Parameters.AddWithValue("@BinaryID", binaryID);
        command.Parameters.AddWithValue("@SymbolID", symbolID);
        command.Parameters.AddWithValue("@WastedSize", ddi.WastedSize);
        command.ExecuteNonQuery();
    }

    private static void InsertWastefulVirtualItems(SqliteConnection connection, SqliteTransaction transaction, int binaryID, WastefulVirtualItem wvi)
    {
        var wastefulVirtualTypeID = 0;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                            $"INSERT INTO {_WastefulVirtualsTypeTableName} " +
                            $"(BinaryID, TypeName, IsCOMType, WastePerSlot, WastedSize) " +
                            $"VALUES " +
                            $"(@BinaryID, @TypeName, @IsCOMType, @WastePerSlot, @WastedSize)";

            command.Parameters.AddWithValue("@BinaryID", binaryID);
            command.Parameters.AddWithValue("@TypeName", wvi.UserDefinedType.Name);
            command.Parameters.AddWithValue("@IsCOMType", wvi.IsCOMType ? 0 : 1);
            command.Parameters.AddWithValue("@WastePerSlot", wvi.WastePerSlot);
            command.Parameters.AddWithValue("@WastedSize", wvi.WastedSize);
            command.ExecuteNonQuery();

            command.CommandText = "select last_insert_rowid()";
            wastefulVirtualTypeID = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                            $"INSERT INTO {_WastefulVirtualsFunctionTableName} " +
                            $"(WastefulVirtualTypeID, FunctionName, WastedSize) " +
                            $"VALUES " +
                            $"(@WastefulVirtualTypeID, @FunctionName, @WastedSize)";

            command.Parameters.AddWithValue("@WastefulVirtualTypeID", wastefulVirtualTypeID);
            command.Parameters.AddWithValue("@FunctionName", String.Empty);
            command.Parameters.AddWithValue("@WastedSize", wvi.WastePerSlot);

            foreach (var func in wvi.WastedOverridesNonPureWithNoOverrides.Concat(wvi.WastedOverridesPureWithExactlyOneOverride))
            {
                command.Parameters["@FunctionName"].Value = func.FormattedName.IncludeParentType;
                command.ExecuteNonQuery();
            }
        }
    }

    private static void InsertAnnotation(SqliteConnection connection, SqliteTransaction transaction, int binaryID, Dictionary<SourceFile, int> sourceFileToIDMapping,
                                         int sourceFileNullID, AnnotationSymbol annotation)
    {
        var sourceFileID = annotation.SourceFile is null ? sourceFileNullID : sourceFileToIDMapping[annotation.SourceFile];

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
                        $"INSERT INTO {_AnnotationsTableName} " +
                        $"(BinaryID, SourceFileID, LineNumber, IsInlinedOrAnnotatingInlineSite, AnnotationText) " +
                        $"VALUES " +
                        $"(@BinaryID, @SourceFileID, @LineNumber, @IsInlinedOrAnnotatingInlineSite, @AnnotationText)";

        command.Parameters.AddWithValue("@BinaryID", binaryID);
        command.Parameters.AddWithValue("@SourceFileID", sourceFileID);
        command.Parameters.AddWithValue("@LineNumber", annotation.LineNumber);
        command.Parameters.AddWithValue("@IsInlinedOrAnnotatingInlineSite", annotation.IsInlinedOrAnnotatingInlineSite);
        command.Parameters.AddWithValue("@AnnotationText", annotation.Text);
        command.ExecuteNonQuery();
    }

    private static void InsertSymbolsInSourceFileAndCompiland(SqliteConnection connection, SqliteTransaction transaction, int binaryCompilandID, int sourceFileID,
                                                              Dictionary<uint, Dictionary<string, int>> symbolsToIDMapping, List<SKUCrawlerSymbol> symbols)
    {
        foreach (var symbol in symbols)
        {
            var symbolID = InsertSymbol(connection, transaction, symbolsToIDMapping, symbol);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                            $"INSERT INTO {_SymbolLocationsTableName} " +
                            $"(BinaryCompilandID, SourceFileID, SymbolID) " +
                            $"VALUES " +
                            $"(@BinaryCompilandID, @SourceFileID, @SymbolID)";

            command.Parameters.AddWithValue("@BinaryCompilandID", binaryCompilandID);
            command.Parameters.AddWithValue("@SourceFileID", sourceFileID);
            command.Parameters.AddWithValue("@SymbolID", symbolID);
            command.ExecuteNonQuery();
        }
    }

    private static void InsertError(SqliteConnection connection, SqliteTransaction transaction, int binaryID, Exception error)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
                            $"INSERT INTO {_ErrorsTableName} " +
                            $"(BinaryID, ExceptionType, ExceptionMessage) " +
                            $"VALUES " +
                            $"(@BinaryID, @ExceptionType, @ExceptionMessage)";

        command.Parameters.AddWithValue("@BinaryID", binaryID);
        command.Parameters.AddWithValue("@ExceptionType", error.GetType().Name);
        command.Parameters.AddWithValue("@ExceptionMessage", error.Message);
        command.ExecuteNonQuery();
    }

    private void EnsureDatabaseCreated(ILogger logger)
    {
        logger.Log($"Creating new DB file {this.DbFilename}");
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder()
            {
                DataSource = this.DbFilename
            }.ToString());
            connection.Open();

            var createTableQuery = $"CREATE TABLE {_BinariesTable} (" +
                                       "BinaryID INTEGER PRIMARY KEY, " +
                                       "Name TEXT, " +
                                       "Size INT " +
                                       ")";

            SqliteCommand command;
            using (command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            createTableQuery = $"CREATE TABLE {_PerfStatsTable} (" +
                                "BinaryID INT NOT NULL, " +
                                "OpeningTookMs INT NOT NULL, " +
                                "SectionsTookMs INT NOT NULL, " +
                                "LibsTookMs INT NOT NULL, " +
                                "SourceFilesTookMs INT NOT NULL, " +
                                "DDITookMs INT NOT NULL, " +
                                "WVITookMs INT NOT NULL, " +
                                "AnnotationsTookMs INT NOT NULL, " +
                                "SymbolsInCompilandsTookMs INT NOT NULL, " +
                                "CONSTRAINT fk_binaries " +
                                "  FOREIGN KEY (BinaryID) " +
                               $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                ")";

            using (command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            createTableQuery = $"CREATE TABLE {_SectionTableName} (" +
                                "BinarySectionID INTEGER PRIMARY KEY, " +
                                "BinaryID INT NOT NULL, " +
                                "SectionName TEXT, " +
                                "Size INT," +
                                "VirtualSize INT, " +
                                "CONSTRAINT fk_binaries " +
                                "  FOREIGN KEY (BinaryID) " +
                               $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                ")";
            using (command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            createTableQuery = $"CREATE TABLE {_COFFGroupTableName} (" +
                                "BinaryCOFFGroupID INTEGER PRIMARY KEY, " +
                                "BinaryID INT NOT NULL, " +
                                "BinarySectionID INT NOT NULL, " +
                                "COFFGroupName TEXT, " +
                                "Size INT," +
                                "VirtualSize INT, " +
                                "CONSTRAINT fk_binaries " +
                                "  FOREIGN KEY (BinaryID) " +
                               $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                "CONSTRAINT fk_binarySections " +
                                "  FOREIGN KEY (BinarySectionID) " +
                               $"  REFERENCES {_SectionTableName}(BinarySectionID) " +
                                ")";

            using (command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            createTableQuery = $"CREATE TABLE {_LibsTableName} (" +
                                "BinaryLibID INTEGER PRIMARY KEY, " +
                                "BinaryID INT NOT NULL, " +
                                "LibName TEXT, " +
                                "Size INT," +
                                "CONSTRAINT fk_binaries " +
                                "  FOREIGN KEY (BinaryID) " +
                               $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                ")";

            using (command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            createTableQuery = $"CREATE TABLE {_CompilandsTableName} (" +
                                "BinaryCompilandID INTEGER PRIMARY KEY, " +
                                "BinaryID INT NOT NULL, " +
                                "BinaryLibID INT NOT NULL, " +
                                "CompilandName TEXT, " +
                                "Size INT," +
                                "CommandLine TEXT, " +
                                "RTTIEnabled INT, " +
                                "Language TEXT, " +
                                "FrontEndVersion TEXT, " +
                                "BackEndVersion TEXT, " +
                                "CONSTRAINT fk_binaries " +
                                "  FOREIGN KEY (BinaryID) " +
                               $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                "CONSTRAINT fk_binaryLibs " +
                                "  FOREIGN KEY (BinaryLibID) " +
                               $"  REFERENCES {_LibsTableName}(BinaryLibID) " +
                                ")";

            using (command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            //TODO: SKUCrawler: consider recording section/COFFGroup contributions of each lib/compiland too

            createTableQuery = $"CREATE TABLE {_SymbolsTableName} (" +
                                    "SymbolID INTEGER PRIMARY KEY, " +
                                    "SymbolName TEXT NOT NULL, " +
                                    "SymbolDetemplatedName TEXT, " +
                                    "Size INT NOT NULL " +
                                    ")";

            using (command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            createTableQuery = $"CREATE TABLE {_DuplicateDataTableName} (" +
                                "DuplicateDataID INTEGER PRIMARY KEY, " +
                                "BinaryID INT NOT NULL, " +
                                "SymbolID INT NOT NULL, " +
                                "WastedSize INT," +
                                "CONSTRAINT fk_binaries " +
                                "  FOREIGN KEY (BinaryID) " +
                               $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                "CONSTRAINT fk_symbols " +
                                "  FOREIGN KEY (SymbolID) " +
                               $"  REFERENCES {_SymbolsTableName}(SymbolID) " +
                                ")";

            using (command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            if (this.IncludeWastefulVirtuals)
            {
                createTableQuery = $"CREATE TABLE {_WastefulVirtualsTypeTableName} (" +
                                    "WastefulVirtualTypeID INTEGER PRIMARY KEY, " +
                                    "BinaryID INT NOT NULL, " +
                                    "TypeName TEXT, " +
                                    "IsCOMType INT, " +
                                    "WastePerSlot INT," +
                                    "WastedSize INT," +
                                    "CONSTRAINT fk_binaries " +
                                    "  FOREIGN KEY (BinaryID) " +
                                   $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                    ")";

                using (command = new SqliteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }

                createTableQuery = $"CREATE TABLE {_WastefulVirtualsFunctionTableName} (" +
                                    "WastefulVirtualFunctionID INTEGER PRIMARY KEY, " +
                                    "WastefulVirtualTypeID INT NOT NULL, " +
                                    "FunctionName TEXT, " +
                                    "WastedSize INT, " +
                                    "CONSTRAINT fk_wastefulVirtualTypes " +
                                    "  FOREIGN KEY (WastefulVirtualTypeID) " +
                                   $"  REFERENCES {_WastefulVirtualsTypeTableName}(WastefulVirtualTypeID) " +
                                    ")";

                using (command = new SqliteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }

            createTableQuery = $"CREATE TABLE {_SourceFilesTableName} (" +
                                "SourceFileID INTEGER PRIMARY KEY, " +
                                "BinaryID INT NOT NULL, " +
                                "SourceFileName TEXT, " +
                                "Size INT, " +
                                "CONSTRAINT fk_binaries " +
                                "  FOREIGN KEY (BinaryID) " +
                               $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                ")";

            using (command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            createTableQuery = $"CREATE TABLE {_AnnotationsTableName} (" +
                                "AnnotationID INTEGER PRIMARY KEY, " +
                                "BinaryID INT NOT NULL, " +
                                "SourceFileID INT NOT NULL, " +
                                "LineNumber INT NOT NULL, " +
                                "IsInlinedOrAnnotatingInlineSite INT NOT NULL, " +
                                "AnnotationText TEXT, " +
                                "CONSTRAINT fk_binaries " +
                                "  FOREIGN KEY (BinaryID) " +
                               $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                "CONSTRAINT fk_sourceFiles " +
                                "  FOREIGN KEY (SourceFileID) " +
                               $"  REFERENCES {_SourceFilesTableName}(SourceFileID) " +
                                ")";

            using (command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            if (this.IncludeCodeSymbols)
            {
                createTableQuery = $"CREATE TABLE {_SymbolLocationsTableName} (" +
                                    "SymbolLocationID INTEGER PRIMARY KEY, " +
                                    "BinaryCompilandID INT NOT NULL, " +
                                    "SourceFileID INT NOT NULL, " +
                                    "SymbolID INT NOT NULL, " +
                                    "CONSTRAINT fk_binaryCompilands " +
                                    "  FOREIGN KEY (BinaryCompilandID) " +
                                   $"  REFERENCES {_CompilandsTableName}(BinaryCompilandID) " +
                                    "CONSTRAINT fk_sourceFiles " +
                                    "  FOREIGN KEY (SourceFileID) " +
                                   $"  REFERENCES {_SourceFilesTableName}(SourceFileID) " +
                                    "CONSTRAINT fk_symbols " +
                                    "  FOREIGN KEY (SymbolID) " +
                                   $"  REFERENCES {_SymbolsTableName}(SymbolID) " +
                                    ")";

                using (command = new SqliteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }

            createTableQuery = $"CREATE TABLE {_ErrorsTableName} (" +
                                "ErrorID INTEGER PRIMARY KEY, " +
                                "BinaryID INT NOT NULL, " +
                                "ExceptionType TEXT, " +
                                "ExceptionMessage TEXT, " +
                                "CONSTRAINT fk_binaries " +
                                "  FOREIGN KEY (BinaryID) " +
                               $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                ")";

            using (command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            logger.LogException("Failed to setup the DB!", ex);
            throw;
        }
    }

    #region IDisposable Support
    private bool disposedValue; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this._databaseWriteQueue.Dispose();
            }

            this.disposedValue = true;
        }
    }

    // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    // ~BatchProcess()
    // {
    //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
    //   Dispose(false);
    // }

    // This code added to correctly implement the disposable pattern.
    public void Dispose() =>
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);// TODO: uncomment the following line if the finalizer is overridden above.// GC.SuppressFinalize(this);
    #endregion
}
