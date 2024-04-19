using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace BinaryBytes;

internal static class ApplicationDbHandler
{
    private static SqliteConnection? _Connection;
    private static string? _DbFilename;
    private const string BinariesTableName = "Binaries";
    private const string SymbolInfoTableName = "SymbolDetails";
    private const string StringTableName = "Strings";
    private const string InlineTableName = "InlineSiteDetails";

    internal static void SetupDb(string dbFilename, ILogger logger)
    {
        using var setupDbLog = logger.StartTaskLog("Setting up SQLite Db...");
        if (_DbFilename is null || _Connection is null)
        {
            var defaultFilename = String.Format(CultureInfo.InvariantCulture, "BinaryBytes-{0:yyyy-MM-dd}.db", DateTime.Now);
            _DbFilename = String.IsNullOrEmpty(dbFilename) ? defaultFilename : $"{dbFilename}.db";
            setupDbLog.Log($"Creating new DB file {_DbFilename}");
            try
            {
                using (_Connection = new SqliteConnection(new SqliteConnectionStringBuilder()
                {
                    DataSource = _DbFilename
                }.ToString()))
                {
                    _Connection.Open();

                    var createTableQuery = $"""
                                            CREATE TABLE {StringTableName} (
                                            StringID INTEGER PRIMARY KEY AUTOINCREMENT,
                                            String VARCHAR(1000) NOT NULL
                                            )
                                            """;
                    {
                        using var command = new SqliteCommand(createTableQuery, _Connection);
                        command.ExecuteNonQuery();
                    }

                    createTableQuery = $"""
                                        CREATE TABLE {BinariesTableName} (
                                        BinaryID INTEGER PRIMARY KEY AUTOINCREMENT,
                                        BinaryNameStringID INT NOT NULL,
                                        CONSTRAINT fk_BinaryNameStringID
                                          FOREIGN KEY (BinaryNameStringID)
                                          REFERENCES {StringTableName}(StringID)
                                        )
                                        """;

                    {
                        using var command = new SqliteCommand(createTableQuery, _Connection);
                        command.ExecuteNonQuery();
                    }

                    // TODO: convert PESection, COFFGroup, LibrayrName, CompilandName to StringID in the string table
                    createTableQuery = $"""
                                        CREATE TABLE {SymbolInfoTableName} (
                                        SymbolID INTEGER PRIMARY KEY AUTOINCREMENT,
                                        BinaryID INT NOT NULL,
                                        PESection VARCHAR(50),
                                        COFFGroup VARCHAR(100),
                                        SymbolNameStringID INT NOT NULL,
                                        RVA INT,
                                        VirtualSize INT,
                                        LibraryName VARCHAR(100),
                                        CompilandName VARCHAR(100),
                                        IsPadding BOOL NOT NULL DEFAULT 0,
                                        IsPGO BOOL NOT NULL DEFAULT 0,
                                        IsOptimizedForSpeed BOOL NOT NULL DEFAULT 0,
                                        DynamicInstructionCount ULONG,
                                        CONSTRAINT fk_BinaryID
                                          FOREIGN KEY (BinaryID)
                                          REFERENCES {BinariesTableName}(BinaryID),
                                        CONSTRAINT fk_SymbolNameStringID
                                          FOREIGN KEY (SymbolNameStringID)
                                          REFERENCES {StringTableName}(StringID)
                                        )
                                        """;
                    {
                        using var command = new SqliteCommand(createTableQuery, _Connection);
                        command.ExecuteNonQuery();
                    }

                    createTableQuery = $"""
                                        CREATE TABLE {InlineTableName} (
                                        BinaryID INTEGER NOT NULL,
                                        InlinedIntoSymbolID INTEGER NOT NULL,
                                        InlinedSymbolNameStringID INTEGER NOT NULL,
                                        CONSTRAINT fk_binaryID
                                          FOREIGN KEY (BinaryID)
                                          REFERENCES {BinariesTableName}(BinaryID),
                                        CONSTRAINT fk_inlinedIntoSymbolID
                                          FOREIGN KEY (InlinedIntoSymbolID)
                                          REFERENCES {SymbolInfoTableName}(SymbolID),
                                        CONSTRAINT fk_inlinedSymbolNameStringID
                                          FOREIGN KEY (InlinedSymbolNameStringID)
                                          REFERENCES {StringTableName}(StringID)
                                        )
                                        """;
                    {
                        using var command = new SqliteCommand(createTableQuery, _Connection);
                        command.ExecuteNonQuery();
                    }

                    // Now some views that make interacting with this denormalized data more user-friendly in a tool like DB Browser for SQLite
                    createTableQuery = $"""
                                        CREATE VIEW Symbols AS
                                        SELECT BinaryNameStrings.String AS BinaryName, 
                                               {SymbolInfoTableName}.PESection, {SymbolInfoTableName}.COFFGroup, SymbolNameStrings.String AS SymbolName,
                                               {SymbolInfoTableName}.RVA, {SymbolInfoTableName}.VirtualSize, {SymbolInfoTableName}.LibraryName,
                                               {SymbolInfoTableName}.CompilandName, {SymbolInfoTableName}.IsPadding, {SymbolInfoTableName}.IsPGO, 
                                               {SymbolInfoTableName}.IsOptimizedForSpeed, {SymbolInfoTableName}.DynamicInstructionCount
                                        FROM {BinariesTableName}
                                        INNER JOIN {StringTableName} AS BinaryNameStrings ON {BinariesTableName}.BinaryNameStringID = BinaryNameStrings.StringID
                                        INNER JOIN {SymbolInfoTableName} ON {SymbolInfoTableName}.BinaryID = {BinariesTableName}.BinaryID
                                        INNER JOIN {StringTableName} AS SymbolNameStrings ON {SymbolInfoTableName}.SymbolNameStringID = SymbolNameStrings.StringID
                                        """;
                    {
                        using var command = new SqliteCommand(createTableQuery, _Connection);
                        command.ExecuteNonQuery();
                    }

                    createTableQuery = $"""
                                        CREATE VIEW Inlines AS
                                        SELECT BinaryNameStrings.String AS BinaryName,
                                               InlineNameStrings.String AS InlinedFunctionName,
                                               {SymbolInfoTableName}.PESection AS InlinedIntoPESection, 
                                               {SymbolInfoTableName}.COFFGroup AS InlinedIntoCOFFGroup,
                                               SymbolNameStrings.String AS InlinedIntoSymbolName,
                                               {SymbolInfoTableName}.LibraryName AS InlinedIntoLibraryName,
                                               {SymbolInfoTableName}.CompilandName AS InlinedIntoCompilandName,
                                               {SymbolInfoTableName}.IsPadding AS InlinedIntoIsPadding, 
                                               {SymbolInfoTableName}.IsPGO AS InlinedIntoIsPGO, 
                                               {SymbolInfoTableName}.IsOptimizedForSpeed AS InlinedIntoIsOptimizedForSpeed,
                                               {SymbolInfoTableName}.DynamicInstructionCount AS InlinedIntoDynamicInstructionCount
                                        FROM {BinariesTableName}
                                        INNER JOIN {StringTableName} AS BinaryNameStrings ON {BinariesTableName}.BinaryNameStringID = BinaryNameStrings.StringID
                                        INNER JOIN {InlineTableName} ON {InlineTableName}.BinaryID = {BinariesTableName}.BinaryID
                                        INNER JOIN {SymbolInfoTableName} ON {SymbolInfoTableName}.SymbolID = {InlineTableName}.InlinedIntoSymbolID
                                        INNER JOIN {StringTableName} AS SymbolNameStrings ON {SymbolInfoTableName}.SymbolNameStringID = SymbolNameStrings.StringID
                                        INNER JOIN {StringTableName} AS InlineNameStrings ON {InlineTableName}.InlinedSymbolNameStringID = InlineNameStrings.StringID
                                        """;
                    {
                        using var command = new SqliteCommand(createTableQuery, _Connection);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogErrorAndReportToConsoleOutput("Failed to set up the DB!", ex, setupDbLog);
                throw;
            }
        }
        else
        {
            setupDbLog.Log($"DB file {_DbFilename} already exists. Same will be used to add more data.");
        }
    }

    internal static async Task AddData(string binary, IEnumerable<SectionBytes> binaryBytes, IEnumerable<InlineSiteSymbol> inlineSites, Session session, ILogger logger)
    {
        using var addDataLog = logger.StartTaskLog("Insert data into SQLite Db...");
        if (_Connection != null)
        {
            try
            {
                using (_Connection = new SqliteConnection(new SqliteConnectionStringBuilder()
                {
                    DataSource = _DbFilename
                }.ToString()))
                {
                    var stringToID = new Dictionary<string, int>(StringComparer.Ordinal);
                    var symbolRVAToID = new Dictionary<uint, int>();

                    await _Connection.OpenAsync();
                    {
                        // Disable on-disk journaling for perf
                        var pragmaCommand = _Connection.CreateCommand();
                        pragmaCommand.CommandText = "PRAGMA journal_mode = MEMORY;";
                        await pragmaCommand.ExecuteNonQueryAsync();
                    }

                    {
                        // Transactions massively increase bulk insert performance, see here: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/bulk-insert
#pragma warning disable CA1849 // Call async methods when in an async method - the async version returns DbTransaction, we need a SqliteTransaction.
                        using var transaction = _Connection.BeginTransaction();
#pragma warning restore CA1849 // Call async methods when in an async method
                        using var insertStringCommand = _Connection.CreateCommand();
                        insertStringCommand.Transaction = transaction;
                        insertStringCommand.CommandText =
                            $"""
                            INSERT INTO {StringTableName}
                            (String)
                            VALUES
                            (@String)
                            ; SELECT last_insert_rowid();
                            """;
                        insertStringCommand.Parameters.AddWithValue("@String", "");

                        var binaryNameID = InsertString(binary, stringToID, insertStringCommand);
                        int binaryID;

                        {
                            using var insertBinaryCommand = _Connection.CreateCommand();
                            insertBinaryCommand.Transaction = transaction;
                            insertBinaryCommand.CommandText =
                                $"""
                                INSERT INTO {BinariesTableName}
                                (BinaryNameStringID)
                                VALUES
                                (@BinaryNameStringID)
                                ; SELECT last_insert_rowid();
                                """;

                            insertBinaryCommand.Parameters.AddWithValue("@BinaryNameStringID", binaryNameID);
#pragma warning disable CA1849 // Call async methods when in an async method - this method should be extremely fast
                            binaryID = Convert.ToInt32(insertBinaryCommand.ExecuteScalar()!, CultureInfo.InvariantCulture);
#pragma warning restore CA1849 // Call async methods when in an async method
                        }

                        using var insertSymbolInfoCommand = _Connection.CreateCommand();
                        insertSymbolInfoCommand.Transaction = transaction;
                        insertSymbolInfoCommand.CommandText =
                            $"""
                             INSERT INTO {SymbolInfoTableName}
                             (BinaryID, PESection, COFFGroup, SymbolNameStringID, RVA, VirtualSize, LibraryName, 
                              CompilandName, IsPadding, IsPGO, IsOptimizedForSpeed, DynamicInstructionCount)
                             VALUES
                             (@BinaryID, @SectionName, @CoffgroupName, @SymbolNameStringID, @RVA, @VirtualSize, @LibraryFilename,
                              @CompilandName, @IsPadding, @IsPGO, @IsOptimizedForSpeed, @DynamicInstructionCount)
                             ; SELECT last_insert_rowid();
                            """;

                        insertSymbolInfoCommand.Parameters.AddWithValue("@BinaryID", binaryID); // This is set once here and does not need to be calculated again per symbol
                        insertSymbolInfoCommand.Parameters.AddWithValue("@SectionName", "");
                        insertSymbolInfoCommand.Parameters.AddWithValue("@CoffgroupName", "");
                        insertSymbolInfoCommand.Parameters.AddWithValue("@SymbolNameStringID", "");
                        insertSymbolInfoCommand.Parameters.AddWithValue("@RVA", "");
                        insertSymbolInfoCommand.Parameters.AddWithValue("@VirtualSize", "");
                        insertSymbolInfoCommand.Parameters.AddWithValue("@LibraryFilename", "");
                        insertSymbolInfoCommand.Parameters.AddWithValue("@CompilandName", "");
                        insertSymbolInfoCommand.Parameters.AddWithValue("@IsPadding", "");
                        insertSymbolInfoCommand.Parameters.AddWithValue("@IsPGO", "");
                        insertSymbolInfoCommand.Parameters.AddWithValue("@IsOptimizedForSpeed", "");
                        insertSymbolInfoCommand.Parameters.AddWithValue("@DynamicInstructionCount", "");

                        using var insertInlineInfoCommand = _Connection.CreateCommand();
                        insertInlineInfoCommand.Transaction = transaction;
                        insertInlineInfoCommand.CommandText =
                            $"""
                             INSERT INTO {InlineTableName}
                             (BinaryID, InlinedIntoSymbolID, InlinedSymbolNameStringID)
                             VALUES
                             (@BinaryID, @InlinedIntoSymbolID, @InlinedSymbolNameStringID)
                             """;

                        insertInlineInfoCommand.Parameters.AddWithValue("@BinaryID", binaryID); // Similarly, set once here and reused for each inline site
                        insertInlineInfoCommand.Parameters.AddWithValue("@InlinedIntoSymbolID", "");
                        insertInlineInfoCommand.Parameters.AddWithValue("@InlinedSymbolNameStringID", "");

                        foreach (var section in binaryBytes)
                        {
                            Program.LogIt($"Persisting data to database for section {section.SectionName}...");
                            foreach (var item in section.Items)
                            {
                                var isPadding = item.IsPadding ? 1 : 0;
                                var isPGO = item.IsPGO ? 1 : 0;
                                var isOptimizedForSpeed = item.IsOptimizedForSpeed ? 1 : 0;
                                var escapedName = item.Name.Replace("'", "''", StringComparison.Ordinal);

                                var symbolID = InsertItem(section.SectionName, item.CoffGroupName, escapedName, item.RVA,
                                    item.VirtualSize, item.LibraryFilename, item.CompilandName, isPadding, isPGO,
                                    isOptimizedForSpeed, item.DynamicInstructionCount,
                                    stringToID,
                                    insertStringCommand, insertSymbolInfoCommand);

                                if(!symbolRVAToID.TryAdd(item.RVA, symbolID))
                                {
                                    // We somehow got multiple symbols at the same RVA, which we don't expect because
                                    // of how we filter out COMDAT folded symbols and 0-byte symbols.
                                    // So, we'll add additional logging here to help diagnose the issue, then fail.

                                    var errorTextBuilder = new StringBuilder();
                                    errorTextBuilder.AppendLine(CultureInfo.InvariantCulture, $"Multiple symbols at the same RVA {item.RVA:X} detected!  This should not happen.");
                                    
                                    foreach (var symbolAtRVA in section.Items.Where(x => x.RVA == item.RVA).OrderBy(x => x.Name))
                                    {
                                        errorTextBuilder.AppendLine(CultureInfo.InvariantCulture, $"  Symbol at {symbolAtRVA.RVA:X}, Length {symbolAtRVA.VirtualSize:N0}: {symbolAtRVA.Name}");
                                    }

                                    var errorText = errorTextBuilder.ToString();
                                    var exToThrow = new InvalidOperationException($"Unable to establish RVA -> Symbol ID mapping for RVA 0x{item.RVA:X}, see logs for details.");
                                    Program.LogErrorAndReportToConsoleOutput(errorText, exToThrow, addDataLog);

                                    throw exToThrow;
                                }
                            }
                        }

                        Program.LogIt($"Persisting inline sites to database...");
                        foreach (var inlineSite in inlineSites)
                        {
                            await InsertInlineSite(inlineSite, session, stringToID, symbolRVAToID, insertStringCommand, insertInlineInfoCommand);
                        }

                        await transaction.CommitAsync();
                    }

                    await _Connection.CloseAsync();
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types - If we fail to add data for one row, keep going to see if we can get most of them
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Program.LogErrorAndReportToConsoleOutput($"Failed to write data for {binary} to database!  Will keep going to try to write data for any other binaries to database", ex, addDataLog);
            }
        }
    }

    private static int InsertItem(string secton, string coff, string symbolname,
        uint rva, ulong virtualSize, string lib, string compiland, int isPadding, int isPgo, int isOptimizedForSpeed, ulong dynamicInstructionCount,
        Dictionary<string, int> symbolNameToID,
        SqliteCommand insertStringCommand, SqliteCommand insertSymbolInfoCommand)
    {
        var symbolNameStringID = InsertString(symbolname, symbolNameToID, insertStringCommand);

        insertSymbolInfoCommand.Parameters["@SectionName"].Value = secton;
        insertSymbolInfoCommand.Parameters["@CoffgroupName"].Value = coff;
        insertSymbolInfoCommand.Parameters["@SymbolNameStringID"].Value = symbolNameStringID;
        insertSymbolInfoCommand.Parameters["@RVA"].Value = rva;
        insertSymbolInfoCommand.Parameters["@VirtualSize"].Value = virtualSize;
        insertSymbolInfoCommand.Parameters["@LibraryFilename"].Value = lib;
        insertSymbolInfoCommand.Parameters["@CompilandName"].Value = compiland;
        insertSymbolInfoCommand.Parameters["@IsPadding"].Value = isPadding;
        insertSymbolInfoCommand.Parameters["@IsPGO"].Value = isPgo;
        insertSymbolInfoCommand.Parameters["@IsOptimizedForSpeed"].Value = isOptimizedForSpeed;
        insertSymbolInfoCommand.Parameters["@DynamicInstructionCount"].Value = dynamicInstructionCount;

        return Convert.ToInt32(insertSymbolInfoCommand.ExecuteScalar()!, CultureInfo.InvariantCulture);
    }

    private static async ValueTask InsertInlineSite(InlineSiteSymbol inlineSite, Session session, Dictionary<string, int> stringToID,
        Dictionary<uint, int> symbolRVAToID,
        SqliteCommand insertStringCommand, SqliteCommand insertInlineSiteCommand)
    {
        var inlinedFunctionStringID = InsertString(inlineSite.Name, stringToID, insertStringCommand);

        // We use the RVA, not the Function/Block Symbol itself, because the inline site may be COMDAT folded into another function,
        // and we exclude all functions with IsCOMDATFolded from being in the database for space reasons.
        if (symbolRVAToID.TryGetValue(inlineSite.CanonicalSymbolInlinedInto.RVA, out var inlinedIntoSymbolID))
        {
            insertInlineSiteCommand.Parameters["@InlinedIntoSymbolID"].Value = inlinedIntoSymbolID;
        }
        else
        {
            var placement = await session.LookupSymbolPlacementInBinary(inlineSite.BlockInlinedInto, CancellationToken.None);

            Program.LogIt($"""
                           Unable to locate FunctionInlinedInto in symbolToID map!
                                 InlineSite: {inlineSite.Name}, inlined into {inlineSite.BlockInlinedInto.Name}
                                 Canonical Inlined Into: {inlineSite.CanonicalSymbolInlinedInto.Name}
                                 BlockInlinedInto RVA: 0x{inlineSite.BlockInlinedInto.RVA:X}
                                 BlockInlinedInto IsCOMDATFolded: {inlineSite.BlockInlinedInto.IsCOMDATFolded}
                                 BlockInlinedInto Placement: {placement.BinarySection?.Name ?? "no section"}, {placement.COFFGroup?.Name ?? "no COFF group"}
                           """);
            return;
        }

        insertInlineSiteCommand.Parameters["@InlinedSymbolNameStringID"].Value = inlinedFunctionStringID;

#pragma warning disable CA1849 // Call async methods when in an async method - this method will be sync except in the rare case of a failure to find a FunctionInlinedInto, let's not pay the cost of a Task<T>
        insertInlineSiteCommand.ExecuteNonQuery();
#pragma warning restore CA1849 // Call async methods when in an async method
    }

    private static int InsertString(string str, Dictionary<string, int> stringToID, SqliteCommand insertStringCommand)
    {
        if (!stringToID.TryGetValue(str, out var stringID))
        {
            insertStringCommand.Parameters["@String"].Value = str;
            stringID = Convert.ToInt32(insertStringCommand.ExecuteScalar()!, CultureInfo.InvariantCulture);
            stringToID.Add(str, stringID);
        }

        return stringID;
    }
}
