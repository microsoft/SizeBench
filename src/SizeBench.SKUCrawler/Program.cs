using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Nito.AsyncEx;
using SizeBench.Logging;
using SizeBench.SKUCrawler.CrawlFolder;

namespace SizeBench.SKUCrawler;

public class ProductBinary
{
    public string BinaryPath { get; }
    public string PdbPath { get; }

    public ProductBinary(string binaryPath, string pdbPath)
    {
        this.BinaryPath = binaryPath;
        this.PdbPath = pdbPath;
    }
}

internal static class Program
{
    private static string _logFilenameBase = String.Empty;
    private static int _processExitCode;

    private static int Main(string[] args)
    {
        Process.GetCurrentProcess().PriorityBoostEnabled = true;
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

        AsyncContext.Run(async () =>
        {
            try
            {
                await MainAsync(args);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Catching more generic stuff is good for console app exit codes
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                using (var scope = new ConsoleColorScope(ConsoleColor.Red))
                {
                    await Console.Error.WriteLineAsync("Exception thrown during execution!");
                    await Console.Error.WriteLineAsync(ex.GetFormattedTextForLogging(String.Empty, Environment.NewLine));
                }
                _processExitCode = 1;
            }
        });

        return _processExitCode;
    }

    private static async Task MainAsync(string[] args)
    {
        using var appLogger = new ApplicationLogger("SKUCrawler", null);

        try
        {
            var parsedArgs = ParseCommandLineArgs(args);

            if (!Directory.Exists(parsedArgs.OutputFolder))
            {
                Directory.CreateDirectory(parsedArgs.OutputFolder);
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var finalOutputMessage = String.Empty;
            if (parsedArgs is CrawlFolderArguments crawlArgs)
            {
                finalOutputMessage = await CrawlFolderAsync(crawlArgs, appLogger);
            }
            else if (parsedArgs is MergeArguments mergeArgs)
            {
                finalOutputMessage = await MergeAsync(mergeArgs);
            }
            else
            {
                throw new InvalidOperationException("Command-line arguments malformed, we should not get here.");
            }

            stopwatch.Stop();
            var output = $"{finalOutputMessage} - total runtime is {stopwatch.Elapsed}";
            await Console.Out.WriteLineAsync(output);
            appLogger.Log(output);
        }
        catch (Exception ex)
        {
            appLogger.LogException("Exception thrown during SKUCrawler execution!", ex);
            throw;
        }
        finally
        {
            WriteLogsToFiles(appLogger);
        }
    }

    public static async Task<string> CrawlFolderAsync(CrawlFolderArguments crawlArgs, IApplicationLogger appLogger)
    {
        if (crawlArgs.IsBatch)
        {
            _logFilenameBase = Path.Combine(crawlArgs.OutputFolder, $"SizeBench.SKUCrawler-{crawlArgs.TimestampOfMaster}-batch{crawlArgs.BatchNumber}");
            await Console.Out.WriteLineAsync($"Batch process started!  Batch number={crawlArgs.BatchNumber}, outputting to {_logFilenameBase}");
        }
        else if (crawlArgs.IsMasterController)
        {
            _logFilenameBase = Path.Combine(crawlArgs.OutputFolder, $"SizeBench.SKUCrawler-{crawlArgs.TimestampOfMaster}-master");
            await Console.Out.WriteLineAsync($"Master process started!  Outputting to {_logFilenameBase}");
        }

        var productBinaries = CrawlFolderBinaryCollector.FindAllBinariesForTheseArgs(crawlArgs, appLogger);

        if (productBinaries.Count > 0)
        {
            if (crawlArgs.IsMasterController)
            {
                using var masterController = new MasterControllerProcess();
                await masterController.KickOffAndWaitForBatches(productBinaries, crawlArgs);
                await Console.Out.WriteLineAsync($"Merging all the databases in {crawlArgs.OutputFolder}");

                using (appLogger.StartTaskLog("Merging batch databases"))
                {
                    MergeDatabases(crawlArgs);
                }

                using var deferredErrorsLog = appLogger.StartTaskLog("Processing all deferred errors from batches");
                masterController.ProcessAllDeferredBatchErrors(deferredErrorsLog);
            }
            else if (crawlArgs.IsBatch)
            {
                var productBinariesInThisBatch = new List<ProductBinary>(crawlArgs.BatchSize);
                productBinariesInThisBatch.AddRange(productBinaries.Skip(crawlArgs.BatchSize * (crawlArgs.BatchNumber - 1)).Take(crawlArgs.BatchSize));

                var batchProcess = new BatchProcess(crawlArgs.BatchNumber, productBinariesInThisBatch, _logFilenameBase)
                {
                    BinaryRoot = crawlArgs.CrawlRoot ?? String.Empty,
                    IncludeWastefulVirtuals = crawlArgs.IncludeWastefulVirtuals,
                    IncludeCodeSymbols = crawlArgs.IncludeCodeSymbols,
                    IncludeDuplicateDataItems = crawlArgs.IncludeDuplicateDataItems,
                };
                await batchProcess.AnalyzeBatchAsync(appLogger);
            }
        }
        else
        {
            // No binaries were found, but customers want to still get an empty schema-only database so we'll just make the database and be done.
            CreateMergedDb(crawlArgs);
        }

        return $"Done executing {(crawlArgs.IsMasterController ? "all batches" : $"batch {crawlArgs.BatchNumber}")}";
    }

    public static Task<string> MergeAsync(MergeArguments mergeArgs)
    {
        MergeDatabases(mergeArgs);

        return Task.FromResult($"Merged {mergeArgs.FilesToMerge.Count} databases");
    }

    public static void LogExceptionAndReportToStdErr(string error, Exception ex, ILogger logger)
    {
        using var scope = new ConsoleColorScope(ConsoleColor.Red);
        Console.Error.WriteLine(ex.GetFormattedTextForLogging(error, Environment.NewLine));
        logger.LogException(error, ex);
    }

    private static void WriteLogsToFiles(ApplicationLogger appLogger)
    {
        using var file = File.CreateText($"{_logFilenameBase}.log");
        appLogger.WriteLog(file);
    }

    // Yes, the code in here takes many passes but...who cares, it's such a short list, and the code is more readable to me this way.
    private static ApplicationArguments ParseCommandLineArgs(string[] args)
    {
        // First see if we should print out help:
        if (args.Length == 0)
        {
            PrintHelpAndExit();
        }

        foreach (var arg in args)
        {
            if (arg is "--?" or "/?")
            {
                PrintHelpAndExit();
                break;
            }
        }

        var crawlFolderArgs = new CrawlFolderArguments();
        var mergeArgs = new MergeArguments();
        var crawlSuccessfullyParsed = crawlFolderArgs.TryParse(args);
        var mergeSuccessfullyParsed = mergeArgs.TryParse(args);

        if (crawlSuccessfullyParsed == true && mergeSuccessfullyParsed == true)
        {
            PrintArgumentErrorThenHelpAndExit("Don't specify /folderRoot (used for crawling) and [/merge|/mergeFolder] (used for merging), only one of those modes should be used at once.");
            throw new InvalidOperationException("Invalid set of args.");
        }
        else if (crawlSuccessfullyParsed == true)
        {
            return crawlFolderArgs;
        }
        else if (mergeSuccessfullyParsed == true)
        {
            return mergeArgs;
        }
        else
        {
            PrintHelpAndExit();
            throw new InvalidOperationException("Invalid set of args.");
        }
    }

    [DoesNotReturn]
    public static void PrintHelpAndExit()
    {
        Console.WriteLine("SizeBench.SKUCrawler will use the SizeBench analysis engine to parse a bunch of binaries into a SQLite database." + Environment.NewLine +
                          Environment.NewLine +
                          "Options you can specify: " + Environment.NewLine +
                          Environment.NewLine +
                          "/outputFolder [path]     This is where all the output files will go - SQLite databases and log files." + Environment.NewLine +
                          "                         outputFolder defaults to your current working directory." + Environment.NewLine +
                          "                         The final result will be in a file called merged.db" + Environment.NewLine +
                          Environment.NewLine +
                          "/folderRoot [path]       Specifies the root to start recursively crawling.  Every PDB is expected to be" + Environment.NewLine +
                          "                         is side-by-side with the binary." + Environment.NewLine +
                          Environment.NewLine +
                          "/includeWastefulVirtuals Include the Wasteful Virtual information in the output database - this is omitted by" + Environment.NewLine +
                          "                         default because it can be quite slow." + Environment.NewLine +
                          "/includeCodeSymbols      Include symbol information for all the code symbols in all compilands - this is also " + Environment.NewLine +
                          "                         omitted by default because it's potentially slow." + Environment.NewLine +
                          "/includeDuplicateData    Include Duplicate Data information in the output database - this is omitted by " + Environment.NewLine +
                          "                         default because it's potentially slow." + Environment.NewLine +
                          Environment.NewLine +
                          "/merge [fileName]        The fileName database will be merged into the final database." + Environment.NewLine +
                          Environment.NewLine +
                          "/mergeFolder [path]      The path will be recursively searched for *.db files, and all of them will be merged" + Environment.NewLine +
                          "                         into the final database." + Environment.NewLine +
                          Environment.NewLine +
                          "You must specify either /folderRoot to crawl things, or some combination of /merge and /mergeFolder to merge " + Environment.NewLine +
                          "existing databases.  You can't specify both." + Environment.NewLine +
                          Environment.NewLine +
                          Environment.NewLine +
                          Environment.NewLine +
                          "So, putting all that together, an example command line might look like this:" + Environment.NewLine +
                          @"SizeBench.SKUCrawler.exe /folderRoot \\some\unc\path /outputFolder c:\SKUCrawlerOutput /includeCodeSymbols" + Environment.NewLine +
                          Environment.NewLine +
                          "Or this:" + Environment.NewLine +
                          @"SizeBench.SKUCrawler.exe /outputFolder c:\SKUCrawlerOutput /merge 1.db /mergeFolder folderOfDatabasese /merge 2.db" + Environment.NewLine
                          );

        Environment.Exit(1);
    }

    [DoesNotReturn]
    public static void PrintArgumentErrorThenHelpAndExit(string error)
    {
        using (new ConsoleColorScope(ConsoleColor.Red))
        {
            Console.Error.WriteLine(error);
        }
        PrintHelpAndExit();
    }

    #region MergeDatabases - to merge all the SQLite databases from the batches into one final result

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

    private static void CreateMergedDb(ApplicationArguments appArgs)
    {
        try
        {
            var dbFileName = Path.Combine(appArgs.OutputFolder, "merged.db");
            File.Delete(dbFileName);
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder()
            {
                DataSource = dbFileName
            }.ToString());
            connection.Open();

            var createTableQuery = $"CREATE TABLE {_BinariesTable} (" +
                                       "BinaryID INTEGER PRIMARY KEY, " +
                                       "Name TEXT, " +
                                       "Size INT " +
                                       ")";
            using (var command = new SqliteCommand(createTableQuery, connection))
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

            using (var command = new SqliteCommand(createTableQuery, connection))
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
            using (var command = new SqliteCommand(createTableQuery, connection))
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

            using (var command = new SqliteCommand(createTableQuery, connection))
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

            using (var command = new SqliteCommand(createTableQuery, connection))
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

            using (var command = new SqliteCommand(createTableQuery, connection))
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

            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            createTableQuery = $"CREATE TABLE {_DuplicateDataTableName} (" +
                                "DuplicateDataID INTEGER PRIMARY KEY, " +
                                "BinaryID INT NOT NULL, " +
                                "SymbolID NOT NULL, " +
                                "WastedSize INT," +
                                "CONSTRAINT fk_binaries " +
                                "  FOREIGN KEY (BinaryID) " +
                               $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                "CONSTRAINT fk_symbols " +
                                "  FOREIGN KEY (SymbolID) " +
                               $"  REFERENCES {_SymbolsTableName}(SymbolID) " +
                                ")";

            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

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

            using (var command = new SqliteCommand(createTableQuery, connection))
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

            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
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

            using (var command = new SqliteCommand(createTableQuery, connection))
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

            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

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

            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            createTableQuery = $"CREATE TABLE {_ErrorsTableName} (" +
                                "ErrorID INTEGER PRIMARY KEY, " +
                                "BinaryID INT NOT NULL, " +
                                "ExceptionType TEXT, " +
                                "ExceptionMessage TEXT, " +
                                "ExceptionDetails TEXT, " +
                                "CONSTRAINT fk_binaries " +
                                "  FOREIGN KEY (BinaryID) " +
                               $"  REFERENCES {_BinariesTable}(BinaryID) " +
                                ")";

            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Console.Out.WriteLine(ex.GetFormattedTextForLogging("Failed to setup the DB!", Environment.NewLine));
            throw;
        }
    }

    private static void MergeDatabases(ApplicationArguments appArgs)
    {
        CreateMergedDb(appArgs);

        using (var connectionToMerged = new SqliteConnection(new SqliteConnectionStringBuilder()
        {
            DataSource = Path.Combine(appArgs.OutputFolder, "merged.db")
        }.ToString()))
        {
            var mergeStartTime = DateTime.Now;
            connectionToMerged.Open();

            {
                // Disable on-disk journaling for perf
                var pragmaCommand = connectionToMerged.CreateCommand();
                pragmaCommand.CommandText = "PRAGMA journal_mode = MEMORY;";
                pragmaCommand.ExecuteNonQuery();
            }

            using var transaction = connectionToMerged.BeginTransaction();

            var symbolSizeAndNameToMergedDatabaseIDs = new SortedList<int, SortedList<string, int>>(capacity: 10000);
            foreach (var file in appArgs.GetDatabaseFilesToMerge())
            {
                Console.Out.WriteLine($"Merging in {file.Name}");
                using var connectionToOneBatch = new SqliteConnection(new SqliteConnectionStringBuilder()
                {
                    DataSource = file.FullName
                }.ToString());
                connectionToOneBatch.Open();

                {
                    // Disable on-disk journaling for perf
                    var pragmaCommand = connectionToOneBatch.CreateCommand();
                    pragmaCommand.CommandText = "PRAGMA journal_mode = MEMORY;";
                    pragmaCommand.ExecuteNonQuery();
                }

                var mergedCommand = connectionToMerged.CreateCommand();
                mergedCommand.Transaction = transaction;

                var mergedSelect_last_rowidCommand = connectionToMerged.CreateCommand();
                mergedSelect_last_rowidCommand.CommandText = "select last_insert_rowid()";
                mergedSelect_last_rowidCommand.Transaction = transaction;

                var binaryIDMappings = MergeInBinariesTable(mergedCommand, mergedSelect_last_rowidCommand, connectionToOneBatch);
                var sectionIDMappingns = MergeInSectionsTable(mergedCommand, mergedSelect_last_rowidCommand, connectionToOneBatch, binaryIDMappings);
                var coffGroupIDMappings = MergeInCOFFGroupsTable(mergedCommand, mergedSelect_last_rowidCommand, connectionToOneBatch, binaryIDMappings, sectionIDMappingns);
                var libIDMappings = MergeInLibsTable(mergedCommand, mergedSelect_last_rowidCommand, connectionToOneBatch, binaryIDMappings);
                var compilandIDMappings = MergeInCompilandsTable(mergedCommand, mergedSelect_last_rowidCommand, connectionToOneBatch, binaryIDMappings, libIDMappings);
                var sourceFileIDMappings = MergeInSourceFilesTable(mergedCommand, mergedSelect_last_rowidCommand, connectionToOneBatch, binaryIDMappings);
                var symbolIDMappings = MergeInSymbolsTable(mergedCommand, mergedSelect_last_rowidCommand, connectionToOneBatch, symbolSizeAndNameToMergedDatabaseIDs);

                if (BatchHasDuplicateDataTable(connectionToOneBatch))
                {
                    MergeInDuplicateDataTable(mergedCommand, connectionToOneBatch, binaryIDMappings, symbolIDMappings);
                }

                if (BatchHasWastefulVirtualsTable(connectionToOneBatch))
                {
                    var wvTypeIDMappings = MergeInWastefulVirtualTypesTable(mergedCommand, mergedSelect_last_rowidCommand, connectionToOneBatch, binaryIDMappings);
                    MergInWastefulVirtualFunctionsTable(mergedCommand, connectionToOneBatch, wvTypeIDMappings);
                }

                MergeInAnnotationsTable(mergedCommand, connectionToOneBatch, binaryIDMappings, sourceFileIDMappings);

                if (BatchHasCodeSymbolsTable(connectionToOneBatch))
                {
                    MergeInCompilandSymbolsTable(mergedCommand, connectionToOneBatch, symbolIDMappings, compilandIDMappings, sourceFileIDMappings);
                }

                MergeInPerfStatsTable(mergedCommand, connectionToOneBatch, binaryIDMappings);
                MergeInErrorsTable(mergedCommand, connectionToOneBatch, binaryIDMappings);
            }

            // Add indexes to merged db
            const string indexesToCreate = @"
                        CREATE INDEX[IX_Binaries_Name] ON[Binaries](Name);
                        CREATE INDEX[IX_COFFGroups_BinaryIDBinarySectionID] ON[COFFGroups](BinaryID, BinarySectionID);
                        CREATE INDEX[IX_COFFGroups_COFFGroupNameBinaryID] ON[COFFGroups](COFFGroupName, BinaryID);
                        CREATE INDEX[IX_COFFGroups_BinaryID] ON[COFFGroups](BinaryID);
                        CREATE INDEX[IX_Compilands_CompilandNameBinaryID] ON[Compilands](CompilandName, BinaryID);
                        CREATE INDEX[IX_Compilands_BinaryLibIDCompilandName] ON[Compilands](BinaryLibID, CompilandName);
                        CREATE INDEX[IX_Libs_BinaryIDLibName] ON[Libs](BinaryID, LibName);
                        CREATE INDEX[IX_Libs_LibName] ON[Libs](LibName);
                        CREATE INDEX[IX_Sections_BinaryIDSectionName] ON[Sections](BinaryID, SectionName);
                        CREATE INDEX[IX_Sections_SectionName] ON[Sections](SectionName);";

            using (var commandToAddIndexes = new SqliteCommand(indexesToCreate, connectionToMerged, transaction))
            {
                commandToAddIndexes.ExecuteNonQuery();
            }

            transaction.Commit();
            var mergeEndTime = DateTime.Now;
            Console.Out.WriteLine($"Merging process itself took {mergeEndTime - mergeStartTime}");
        }

        using (_ = new ConsoleColorScope(ConsoleColor.Green))
        {
            Console.Out.WriteLine($"Finished processing - full SQLite database output is in {Path.Combine(appArgs.OutputFolder, "merged.db")}");
        }
    }

    private static bool BatchHasDuplicateDataTable(SqliteConnection connectionToOneBatch)
        => DoesTableExist(connectionToOneBatch, _DuplicateDataTableName);

    private static bool BatchHasWastefulVirtualsTable(SqliteConnection connectionToOneBatch)
        => DoesTableExist(connectionToOneBatch, _WastefulVirtualsTypeTableName);

    private static bool BatchHasCodeSymbolsTable(SqliteConnection connectionToOneBatch)
        => DoesTableExist(connectionToOneBatch, _SymbolLocationsTableName);

    private static bool DoesTableExist(SqliteConnection connectionToOneBatch, string tableName)
    {
        using var query = connectionToOneBatch.CreateCommand();
        query.CommandText = "SELECT * FROM sqlite_master WHERE type = 'table' AND tbl_name=@TableName";
        query.Parameters.AddWithValue("@TableName", tableName);
        var reader = query.ExecuteReader();
        return reader.HasRows;
    }

    private static SortedList<int, int> MergeInBinariesTable(SqliteCommand mergedCommand, SqliteCommand mergedSelect_last_rowidCommand,
                                                             SqliteConnection connectionToOneBatch)
    {
        var binaryIDMappings = new SortedList<int, int>(capacity: 1000);
        using (var queryBinaries = connectionToOneBatch.CreateCommand())
        {
            queryBinaries.CommandText = "SELECT * FROM Binaries ORDER BY BinaryID";
            var reader = queryBinaries.ExecuteReader();

            mergedCommand.CommandText = "INSERT INTO Binaries " +
                                        "(Name, Size) " +
                                        "VALUES " +
                                        "(@Name, @Size)";

            mergedCommand.Parameters.Clear();
            mergedCommand.Parameters.AddWithValue("@Name", String.Empty);
            mergedCommand.Parameters.AddWithValue("@Size", 0);

            while (reader.Read())
            {
                mergedCommand.Parameters["@Name"].Value = reader["Name"];
                mergedCommand.Parameters["@Size"].Value = reader["Size"];
                mergedCommand.ExecuteNonQuery();

                binaryIDMappings.Add(Convert.ToInt32(reader["BinaryID"], CultureInfo.InvariantCulture),
                                     Convert.ToInt32(mergedSelect_last_rowidCommand.ExecuteScalar(), CultureInfo.InvariantCulture));
            }
        }

        return binaryIDMappings;
    }

    private static SortedList<int, int> MergeInSectionsTable(SqliteCommand mergedCommand, SqliteCommand mergedSelect_last_rowidCommand,
                                                             SqliteConnection connectionToOneBatch, SortedList<int, int> binaryIDMappings)
    {
        var sectionIDMappings = new SortedList<int, int>(capacity: 1000);
        using (var querySections = connectionToOneBatch.CreateCommand())
        {
            querySections.CommandText = "SELECT * FROM Sections ORDER BY BinarySectionID";
            var reader = querySections.ExecuteReader();

            mergedCommand.CommandText = "INSERT INTO Sections " +
                                        "(BinaryID, SectionName, Size, VirtualSize) " +
                                        "VALUES " +
                                        "(@BinaryID, @SectionName, @Size, @VirtualSize)";

            mergedCommand.Parameters.Clear();
            mergedCommand.Parameters.AddWithValue("@BinaryID", 0);
            mergedCommand.Parameters.AddWithValue("@SectionName", String.Empty);
            mergedCommand.Parameters.AddWithValue("@Size", 0);
            mergedCommand.Parameters.AddWithValue("@VirtualSize", 0);

            while (reader.Read())
            {
                mergedCommand.Parameters["@BinaryID"].Value = binaryIDMappings[Convert.ToInt32(reader["BinaryID"], CultureInfo.InvariantCulture)];
                mergedCommand.Parameters["@SectionName"].Value = reader["SectionName"];
                mergedCommand.Parameters["@Size"].Value = reader["Size"];
                mergedCommand.Parameters["@VirtualSize"].Value = reader["VirtualSize"];
                mergedCommand.ExecuteNonQuery();

                sectionIDMappings.Add(Convert.ToInt32(reader["BinarySectionID"], CultureInfo.InvariantCulture),
                                      Convert.ToInt32(mergedSelect_last_rowidCommand.ExecuteScalar(), CultureInfo.InvariantCulture));
            }
        }

        return sectionIDMappings;
    }

    private static SortedList<int, int> MergeInCOFFGroupsTable(SqliteCommand mergedCommand, SqliteCommand mergedSelect_last_rowidCommand,
                                                               SqliteConnection connectionToOneBatch,
                                                               SortedList<int, int> binaryIDMappings, SortedList<int, int> sectionIDMappings)
    {
        var coffGroupIDMappings = new SortedList<int, int>(capacity: 1000);
        using (var queryCOFFGroups = connectionToOneBatch.CreateCommand())
        {
            queryCOFFGroups.CommandText = "SELECT * FROM COFFGroups ORDER BY BinaryCOFFGroupID";
            var reader = queryCOFFGroups.ExecuteReader();

            mergedCommand.CommandText = "INSERT INTO COFFGroups " +
                                        "(BinaryID, BinarySectionID, COFFGroupName, Size, VirtualSize) " +
                                        "VALUES " +
                                        "(@BinaryID, @BinarySectionID, @COFFGroupName, @Size, @VirtualSize)";

            mergedCommand.Parameters.Clear();
            mergedCommand.Parameters.AddWithValue("@BinaryID", 0);
            mergedCommand.Parameters.AddWithValue("@BinarySectionID", 0);
            mergedCommand.Parameters.AddWithValue("@COFFGroupName", String.Empty);
            mergedCommand.Parameters.AddWithValue("@Size", 0);
            mergedCommand.Parameters.AddWithValue("@VirtualSize", 0);

            while (reader.Read())
            {
                mergedCommand.Parameters["@BinaryID"].Value = binaryIDMappings[Convert.ToInt32(reader["BinaryID"], CultureInfo.InvariantCulture)];
                mergedCommand.Parameters["@BinarySectionID"].Value = sectionIDMappings[Convert.ToInt32(reader["BinarySectionID"], CultureInfo.InvariantCulture)];
                mergedCommand.Parameters["@COFFGroupName"].Value = reader["COFFGroupName"];
                mergedCommand.Parameters["@Size"].Value = reader["Size"];
                mergedCommand.Parameters["@VirtualSize"].Value = reader["VirtualSize"];
                mergedCommand.ExecuteNonQuery();

                coffGroupIDMappings.Add(Convert.ToInt32(reader["BinaryCOFFGroupID"], CultureInfo.InvariantCulture),
                                        Convert.ToInt32(mergedSelect_last_rowidCommand.ExecuteScalar(), CultureInfo.InvariantCulture));
            }
        }

        return coffGroupIDMappings;
    }

    private static SortedList<int, int> MergeInLibsTable(SqliteCommand mergedCommand, SqliteCommand mergedSelect_last_rowidCommand,
                                                         SqliteConnection connectionToOneBatch, SortedList<int, int> binaryIDMappings)
    {
        var libIDMappings = new SortedList<int, int>(capacity: 1000);
        using (var queryLibs = connectionToOneBatch.CreateCommand())
        {
            queryLibs.CommandText = "SELECT * FROM Libs ORDER BY BinaryLibID";
            var reader = queryLibs.ExecuteReader();

            mergedCommand.CommandText = "INSERT INTO Libs " +
                                        "(BinaryID, LibName, Size) " +
                                        "VALUES " +
                                        "(@BinaryID, @LibName, @Size)";

            mergedCommand.Parameters.Clear();
            mergedCommand.Parameters.AddWithValue("@BinaryID", 0);
            mergedCommand.Parameters.AddWithValue("@LibName", String.Empty);
            mergedCommand.Parameters.AddWithValue("@Size", 0);

            while (reader.Read())
            {
                mergedCommand.Parameters["@BinaryID"].Value = binaryIDMappings[Convert.ToInt32(reader["BinaryID"], CultureInfo.InvariantCulture)];
                mergedCommand.Parameters["@LibName"].Value = reader["LibName"];
                mergedCommand.Parameters["@Size"].Value = reader["Size"];
                mergedCommand.ExecuteNonQuery();

                libIDMappings.Add(Convert.ToInt32(reader["BinaryLibID"], CultureInfo.InvariantCulture),
                                  Convert.ToInt32(mergedSelect_last_rowidCommand.ExecuteScalar(), CultureInfo.InvariantCulture));
            }
        }

        return libIDMappings;
    }

    private static SortedList<int, int> MergeInCompilandsTable(SqliteCommand mergedCommand, SqliteCommand mergedSelect_last_rowidCommand,
                                                               SqliteConnection connectionToOneBatch,
                                                               SortedList<int, int> binaryIDMappings, SortedList<int, int> libIDMappings)
    {
        var compilandIDMappings = new SortedList<int, int>(capacity: 1000);
        using (var queryCompilands = connectionToOneBatch.CreateCommand())
        {
            queryCompilands.CommandText = "SELECT * FROM Compilands ORDER BY BinaryCompilandID";
            var reader = queryCompilands.ExecuteReader();

            mergedCommand.CommandText = "INSERT INTO Compilands " +
                                        "(BinaryID, BinaryLibID, CompilandName, Size, CommandLine, RTTIEnabled, Language, FrontEndVersion, BackEndVersion) " +
                                        "VALUES " +
                                        "(@BinaryID, @BinaryLibID, @CompilandName, @Size, @CommandLine, @RTTIEnabled, @Language, @FrontEndVersion, @BackEndVersion)";

            mergedCommand.Parameters.Clear();
            mergedCommand.Parameters.AddWithValue("@BinaryID", 0);
            mergedCommand.Parameters.AddWithValue("@BinaryLibID", 0);
            mergedCommand.Parameters.AddWithValue("@CompilandName", String.Empty);
            mergedCommand.Parameters.AddWithValue("@Size", 0);
            mergedCommand.Parameters.AddWithValue("@CommandLine", String.Empty);
            mergedCommand.Parameters.AddWithValue("@RTTIEnabled", 0);
            mergedCommand.Parameters.AddWithValue("@Language", String.Empty);
            mergedCommand.Parameters.AddWithValue("@FrontEndVersion", 0);
            mergedCommand.Parameters.AddWithValue("@BackEndVersion", 0);

            while (reader.Read())
            {
                mergedCommand.Parameters["@BinaryID"].Value = binaryIDMappings[Convert.ToInt32(reader["BinaryID"], CultureInfo.InvariantCulture)];
                mergedCommand.Parameters["@BinaryLibID"].Value = libIDMappings[Convert.ToInt32(reader["BinaryLibID"], CultureInfo.InvariantCulture)];
                mergedCommand.Parameters["@CompilandName"].Value = reader["CompilandName"];
                mergedCommand.Parameters["@Size"].Value = reader["Size"];
                mergedCommand.Parameters["@CommandLine"].Value = reader["CommandLine"];
                mergedCommand.Parameters["@RTTIEnabled"].Value = reader["RTTIEnabled"];
                mergedCommand.Parameters["@Language"].Value = reader["Language"];
                mergedCommand.Parameters["@FrontEndVersion"].Value = reader["FrontEndVersion"];
                mergedCommand.Parameters["@BackEndVersion"].Value = reader["BackEndVersion"];
                mergedCommand.ExecuteNonQuery();

                compilandIDMappings.Add(Convert.ToInt32(reader["BinaryCompilandID"], CultureInfo.InvariantCulture),
                                        Convert.ToInt32(mergedSelect_last_rowidCommand.ExecuteScalar(), CultureInfo.InvariantCulture));
            }
        }

        return compilandIDMappings;
    }

    private static void MergeInDuplicateDataTable(SqliteCommand mergedCommand, SqliteConnection connectionToOneBatch,
                                                  SortedList<int, int> binaryIDMappings, SortedList<int, int> symbolIDMappings)
    {
        using var queryDDI = connectionToOneBatch.CreateCommand();
        queryDDI.CommandText = "SELECT * FROM DuplicateData";
        var reader = queryDDI.ExecuteReader();

        mergedCommand.CommandText = "INSERT INTO DuplicateData " +
                                    "(BinaryID, SymbolID, WastedSize) " +
                                    "VALUES " +
                                    "(@BinaryID, @SymbolID, @WastedSize)";

        mergedCommand.Parameters.Clear();
        mergedCommand.Parameters.AddWithValue("@BinaryID", 0);
        mergedCommand.Parameters.AddWithValue("@SymbolID", 0);
        mergedCommand.Parameters.AddWithValue("@WastedSize", 0);

        while (reader.Read())
        {
            mergedCommand.Parameters["@BinaryID"].Value = binaryIDMappings[Convert.ToInt32(reader["BinaryID"], CultureInfo.InvariantCulture)];
            mergedCommand.Parameters["@SymbolID"].Value = symbolIDMappings[Convert.ToInt32(reader["SymbolID"], CultureInfo.InvariantCulture)];
            mergedCommand.Parameters["@WastedSize"].Value = reader["WastedSize"];
            mergedCommand.ExecuteNonQuery();
        }
    }

    private static SortedList<int, int> MergeInWastefulVirtualTypesTable(SqliteCommand mergedCommand, SqliteCommand mergedSelect_last_rowidCommand,
                                                                         SqliteConnection connectionToOneBatch, SortedList<int, int> binaryIDMappings)
    {
        var wvTypeIDMappings = new SortedList<int, int>(capacity: 1000);
        using (var queryWVTypes = connectionToOneBatch.CreateCommand())
        {
            queryWVTypes.CommandText = "SELECT * FROM WastefulVirtualTypes ORDER BY WastefulVirtualTypeID";
            var reader = queryWVTypes.ExecuteReader();

            mergedCommand.CommandText = "INSERT INTO WastefulVirtualTypes " +
                                        "(BinaryID, TypeName, IsCOMType, WastePerSlot, WastedSize) " +
                                        "VALUES " +
                                        "(@BinaryID, @TypeName, @IsCOMType, @WastePerSlot, @WastedSize)";

            mergedCommand.Parameters.Clear();
            mergedCommand.Parameters.AddWithValue("@BinaryID", 0);
            mergedCommand.Parameters.AddWithValue("@TypeName", String.Empty);
            mergedCommand.Parameters.AddWithValue("@IsCOMType", 0);
            mergedCommand.Parameters.AddWithValue("@WastePerSlot", 0);
            mergedCommand.Parameters.AddWithValue("@WastedSize", 0);

            while (reader.Read())
            {
                mergedCommand.Parameters["@BinaryID"].Value = binaryIDMappings[Convert.ToInt32(reader["BinaryID"], CultureInfo.InvariantCulture)];
                mergedCommand.Parameters["@TypeName"].Value = reader["TypeName"];
                mergedCommand.Parameters["@IsCOMType"].Value = reader["IsCOMType"];
                mergedCommand.Parameters["@WastePerSlot"].Value = reader["WastePerSlot"];
                mergedCommand.Parameters["@WastedSize"].Value = reader["WastedSize"];
                mergedCommand.ExecuteNonQuery();

                wvTypeIDMappings.Add(Convert.ToInt32(reader["WastefulVirtualTypeID"], CultureInfo.InvariantCulture),
                                     Convert.ToInt32(mergedSelect_last_rowidCommand.ExecuteScalar(), CultureInfo.InvariantCulture));
            }
        }

        return wvTypeIDMappings;
    }

    private static void MergInWastefulVirtualFunctionsTable(SqliteCommand mergedCommand, SqliteConnection connectionToOneBatch, SortedList<int, int> wvTypeIDMappings)
    {
        using var queryWVFunctions = connectionToOneBatch.CreateCommand();
        queryWVFunctions.CommandText = "SELECT * FROM WastefulVirtualFunctions";
        var reader = queryWVFunctions.ExecuteReader();

        mergedCommand.CommandText = "INSERT INTO WastefulVirtualFunctions " +
                                    "(WastefulVirtualTypeID, FunctionName, WastedSize) " +
                                    "VALUES " +
                                    "(@WastefulVirtualTypeID, @FunctionName, @WastedSize)";

        mergedCommand.Parameters.Clear();
        mergedCommand.Parameters.AddWithValue("@WastefulVirtualTypeID", 0);
        mergedCommand.Parameters.AddWithValue("@FunctionName", String.Empty);
        mergedCommand.Parameters.AddWithValue("@WastedSize", 0);

        while (reader.Read())
        {
            mergedCommand.Parameters["@WastefulVirtualTypeID"].Value = wvTypeIDMappings[Convert.ToInt32(reader["WastefulVirtualTypeID"], CultureInfo.InvariantCulture)];
            mergedCommand.Parameters["@FunctionName"].Value = reader["FunctionName"];
            mergedCommand.Parameters["@WastedSize"].Value = reader["WastedSize"];
            mergedCommand.ExecuteNonQuery();
        }
    }

    private static SortedList<int, int> MergeInSourceFilesTable(SqliteCommand mergedCommand, SqliteCommand mergedSelect_last_rowidCommand,
                                                                SqliteConnection connectionToOneBatch,
                                                                SortedList<int, int> binaryIDMappings)
    {
        var sourceFileIDMappings = new SortedList<int, int>(capacity: 1000);

        using (var querySourceFiles = connectionToOneBatch.CreateCommand())
        {
            querySourceFiles.CommandText = $"SELECT * FROM {_SourceFilesTableName} ORDER BY SourceFileID";
            var reader = querySourceFiles.ExecuteReader();

            mergedCommand.CommandText = $"INSERT INTO {_SourceFilesTableName} " +
                                         "(BinaryID, SourceFileName, Size) " +
                                         "VALUES " +
                                         "(@BinaryID, @SourceFileName, @Size)";

            mergedCommand.Parameters.Clear();
            mergedCommand.Parameters.AddWithValue("@BinaryID", 0);
            mergedCommand.Parameters.AddWithValue("@SourceFileName", String.Empty);
            mergedCommand.Parameters.AddWithValue("@Size", 0);

            while (reader.Read())
            {
                mergedCommand.Parameters["@BinaryID"].Value = binaryIDMappings[Convert.ToInt32(reader["BinaryID"], CultureInfo.InvariantCulture)];
                mergedCommand.Parameters["@SourceFileName"].Value = reader["SourceFileName"];
                mergedCommand.Parameters["@Size"].Value = reader["Size"];
                mergedCommand.ExecuteNonQuery();

                sourceFileIDMappings.Add(Convert.ToInt32(reader["SourceFileID"], CultureInfo.InvariantCulture),
                                         Convert.ToInt32(mergedSelect_last_rowidCommand.ExecuteScalar(), CultureInfo.InvariantCulture));
            }
        }

        return sourceFileIDMappings;
    }

    private static void MergeInAnnotationsTable(SqliteCommand mergedCommand, SqliteConnection connectionToOneBatch,
                                                SortedList<int, int> binaryIDMappings, SortedList<int, int> sourceFileIDMappings)
    {
        using var queryAnnotations = connectionToOneBatch.CreateCommand();
        queryAnnotations.CommandText = $"SELECT * FROM {_AnnotationsTableName}";
        var reader = queryAnnotations.ExecuteReader();

        mergedCommand.CommandText = $"INSERT INTO {_AnnotationsTableName} " +
                                     "(BinaryID, SourceFileID, LineNumber, IsInlinedOrAnnotatingInlineSite, AnnotationText) " +
                                     "VALUES " +
                                     "(@BinaryID, @SourceFileID, @LineNumber, @IsInlinedOrAnnotatingInlineSite, @AnnotationText) ";

        mergedCommand.Parameters.Clear();
        mergedCommand.Parameters.AddWithValue("@BinaryID", 0);
        mergedCommand.Parameters.AddWithValue("@SourceFileID", 0);
        mergedCommand.Parameters.AddWithValue("@LineNumber", 0);
        mergedCommand.Parameters.AddWithValue("@IsInlinedOrAnnotatingInlineSite", 0);
        mergedCommand.Parameters.AddWithValue("@AnnotationText", String.Empty);

        while (reader.Read())
        {
            mergedCommand.Parameters["@BinaryID"].Value = binaryIDMappings[Convert.ToInt32(reader["BinaryID"], CultureInfo.InvariantCulture)];
            mergedCommand.Parameters["@SourceFileID"].Value = sourceFileIDMappings[Convert.ToInt32(reader["SourceFileID"], CultureInfo.InvariantCulture)];
            mergedCommand.Parameters["@LineNumber"].Value = reader["LineNumber"];
            mergedCommand.Parameters["@IsInlinedOrAnnotatingInlineSite"].Value = reader["IsInlinedOrAnnotatingInlineSite"];
            mergedCommand.Parameters["@AnnotationText"].Value = reader["AnnotationText"];
            mergedCommand.ExecuteNonQuery();
        }
    }

    private static SortedList<int, int> MergeInSymbolsTable(SqliteCommand mergedCommand, SqliteCommand mergedSelect_last_rowidCommand,
                                                            SqliteConnection connectionToOneBatch,
                                                            SortedList<int, SortedList<string, int>> symbolSizeAndNameToMergedDatabaseIDs)
    {
        var symbolIDMappingsFromBatchToMerged = new SortedList<int, int>(capacity: 10000);

        using var querySymbols = connectionToOneBatch.CreateCommand();
        querySymbols.CommandText = $"SELECT * FROM {_SymbolsTableName} ORDER BY SymbolID";
        var reader = querySymbols.ExecuteReader();

        mergedCommand.CommandText = $"INSERT INTO {_SymbolsTableName} " +
                                     "(SymbolName, SymbolDetemplatedName, Size) " +
                                     "VALUES " +
                                     "(@SymbolName, @SymbolDetemplatedName, @Size)";

        mergedCommand.Parameters.Clear();
        mergedCommand.Parameters.AddWithValue("@SymbolName", String.Empty);
        mergedCommand.Parameters.AddWithValue("@SymbolDetemplatedName", String.Empty);
        mergedCommand.Parameters.AddWithValue("@Size", 0);

        while (reader.Read())
        {
            var symbolName = (string)reader["SymbolName"];
            var symbolSize = Convert.ToInt32(reader["Size"], CultureInfo.InvariantCulture);
            var symbolIDInBatchDB = Convert.ToInt32(reader["SymbolID"], CultureInfo.InvariantCulture);

            // First we need to find the symbol's name and size from the batch, to see if a previous binary in the merged DB may have already written this, to re-use its ID as we merge.
            if (symbolSizeAndNameToMergedDatabaseIDs.TryGetValue(symbolSize, out var symbolsOfThatSize))
            {
                if (symbolsOfThatSize.TryGetValue(symbolName, out var symbolIDInMergedDB))
                {
                    symbolIDMappingsFromBatchToMerged.Add(symbolIDInBatchDB, symbolIDInMergedDB);
                    continue;
                }
            }

            // No symbol with this name and size exists, so we'll insert it now.  This keeps the database from having tons of copies of the SymbolName string, and makes it easier
            // to query across binaries for like symbols, when looking for SKU-wide opportunities across binaries and files.
            mergedCommand.Parameters["@SymbolName"].Value = symbolName;
            mergedCommand.Parameters["@SymbolDetemplatedName"].Value = reader["SymbolDetemplatedName"];
            mergedCommand.Parameters["@Size"].Value = symbolSize;
            mergedCommand.ExecuteNonQuery();

            symbolIDMappingsFromBatchToMerged.Add(Convert.ToInt32(reader["SymbolID"], CultureInfo.InvariantCulture),
                                                  Convert.ToInt32(mergedSelect_last_rowidCommand.ExecuteScalar(), CultureInfo.InvariantCulture));
        }

        return symbolIDMappingsFromBatchToMerged;
    }

    private static void MergeInCompilandSymbolsTable(SqliteCommand mergedCommand, SqliteConnection connectionToOneBatch,
                                                     SortedList<int, int> symbolIDMappings, SortedList<int, int> compilandIDMappings,
                                                     SortedList<int, int> sourceFileIDMappings)
    {
        using var queryCompilandSymbols = connectionToOneBatch.CreateCommand();
        queryCompilandSymbols.CommandText = $"SELECT * FROM {_SymbolLocationsTableName}";
        var reader = queryCompilandSymbols.ExecuteReader();

        mergedCommand.CommandText = $"INSERT INTO {_SymbolLocationsTableName} " +
                                     "(BinaryCompilandID, SourceFileID, SymbolID) " +
                                     "VALUES " +
                                     "(@BinaryCompilandID, @SourceFileID, @SymbolID)";

        mergedCommand.Parameters.Clear();
        mergedCommand.Parameters.AddWithValue("@BinaryCompilandID", 0);
        mergedCommand.Parameters.AddWithValue("@SourceFileID", 0);
        mergedCommand.Parameters.AddWithValue("@SymbolID", 0);

        while (reader.Read())
        {
            mergedCommand.Parameters["@BinaryCompilandID"].Value = compilandIDMappings[Convert.ToInt32(reader["BinaryCompilandID"], CultureInfo.InvariantCulture)];
            mergedCommand.Parameters["@SourceFileID"].Value = sourceFileIDMappings[Convert.ToInt32(reader["SourceFileID"], CultureInfo.InvariantCulture)];
            mergedCommand.Parameters["@SymbolID"].Value = symbolIDMappings[Convert.ToInt32(reader["SymbolID"], CultureInfo.InvariantCulture)];

            mergedCommand.ExecuteNonQuery();
        }
    }

    private static void MergeInPerfStatsTable(SqliteCommand mergedCommand, SqliteConnection connectionToOneBatch, SortedList<int, int> binaryIDMappings)
    {
        using var queryPerfStats = connectionToOneBatch.CreateCommand();
        queryPerfStats.CommandText = "SELECT * FROM PerfStats";
        var reader = queryPerfStats.ExecuteReader();

        mergedCommand.CommandText = "INSERT INTO PerfStats " +
                                    "(BinaryID, OpeningTookMs, SectionsTookMs, LibsTookMs, SourceFilesTookMs, DDITookMs, WVITookMs, AnnotationsTookMs, SymbolsInCompilandsTookMs) " +
                                    "VALUES " +
                                    "(@BinaryID, @OpeningTookMs, @SectionsTookMs, @LibsTookMs, @SourceFilesTookMs, @DDITookMs, @WVITookMs, @AnnotationsTookMs, @SymbolsInCompilandsTookMs)";

        mergedCommand.Parameters.Clear();
        mergedCommand.Parameters.AddWithValue("@BinaryID", 0);
        mergedCommand.Parameters.AddWithValue("@OpeningTookMs", 0);
        mergedCommand.Parameters.AddWithValue("@SectionsTookMs", 0);
        mergedCommand.Parameters.AddWithValue("@LibsTookMs", 0);
        mergedCommand.Parameters.AddWithValue("@SourceFilesTookMs", 0);
        mergedCommand.Parameters.AddWithValue("@DDITookMs", 0);
        mergedCommand.Parameters.AddWithValue("@WVITookMs", 0);
        mergedCommand.Parameters.AddWithValue("@AnnotationsTookMs", 0);
        mergedCommand.Parameters.AddWithValue("@SymbolsInCompilandsTookMs", 0);

        while (reader.Read())
        {
            mergedCommand.Parameters["@BinaryID"].Value = binaryIDMappings[Convert.ToInt32(reader["BinaryID"], CultureInfo.InvariantCulture)];
            mergedCommand.Parameters["@OpeningTookMs"].Value = reader["OpeningTookMs"];
            mergedCommand.Parameters["@SectionsTookMs"].Value = reader["SectionsTookMs"];
            mergedCommand.Parameters["@LibsTookMs"].Value = reader["LibsTookMs"];
            mergedCommand.Parameters["@SourceFilesTookMs"].Value = reader["SourceFilesTookMs"];
            mergedCommand.Parameters["@DDITookMs"].Value = reader["DDITookMs"];
            mergedCommand.Parameters["@WVITookMs"].Value = reader["WVITookMs"];
            mergedCommand.Parameters["@AnnotationsTookMs"].Value = reader["AnnotationsTookMs"];
            mergedCommand.Parameters["@SymbolsInCompilandsTookMs"].Value = reader["SymbolsInCompilandsTookMs"];
            mergedCommand.ExecuteNonQuery();
        }
    }

    private static void MergeInErrorsTable(SqliteCommand mergedCommand, SqliteConnection connectionToOneBatch, SortedList<int, int> binaryIDMappings)
    {
        using var queryPerfStats = connectionToOneBatch.CreateCommand();
        queryPerfStats.CommandText = $"SELECT * FROM {_ErrorsTableName}";
        var reader = queryPerfStats.ExecuteReader();

        mergedCommand.CommandText = $"INSERT INTO {_ErrorsTableName} " +
                                     "(BinaryID, ExceptionType, ExceptionMessage, ExceptionDetails) " +
                                     "VALUES " +
                                     "(@BinaryID, @ExceptionType, @ExceptionMessage, @ExceptionDetails)";

        mergedCommand.Parameters.Clear();
        mergedCommand.Parameters.AddWithValue("@BinaryID", 0);
        mergedCommand.Parameters.AddWithValue("@ExceptionType", String.Empty);
        mergedCommand.Parameters.AddWithValue("@ExceptionMessage", String.Empty);
        mergedCommand.Parameters.AddWithValue("@ExceptionDetails", String.Empty);

        while (reader.Read())
        {
            mergedCommand.Parameters["@BinaryID"].Value = binaryIDMappings[Convert.ToInt32(reader["BinaryID"], CultureInfo.InvariantCulture)];
            mergedCommand.Parameters["@ExceptionType"].Value = reader["ExceptionType"];
            mergedCommand.Parameters["@ExceptionMessage"].Value = reader["ExceptionMessage"];
            mergedCommand.Parameters["@ExceptionDetails"].Value = reader["ExceptionDetails"];
            mergedCommand.ExecuteNonQuery();
        }
    }

    #endregion
}
