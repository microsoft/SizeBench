using System.Globalization;
using Microsoft.Data.Sqlite;
using SizeBench.Logging;

namespace BinaryBytes;

internal static class ApplicationDbHandler
{
    private static SqliteConnection? _Connection;
    private static string? _DbFilename;
    private const string TableName = "BinaryBytes";

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

                    var createTableQuery = $"CREATE TABLE {TableName} (Binary VARCHAR(50), " +
                                              "PESection VARCHAR(15), " +
                                              "COFFGroup VARCHAR(20), " +
                                              "SymbolName VARCHAR(100), " +
                                              "RVA INT, " +
                                              "VirtualSize INT, " +
                                              "Libraryname VARCHAR(100), " +
                                              "CompilandName VARCHAR(100), " +
                                              "IsPadding BOOL NOT NULL DEFAULT 0, " +
                                              "IsPGO BOOL NOT NULL DEFAULT 0, " +
                                              "IsOptimizedForSpeed BOOL NOT NULL DEFAULT 0)";
                    using var command = new SqliteCommand(createTableQuery, _Connection);
                    command.ExecuteNonQuery();
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

    internal static void AddData(string binary, IEnumerable<SectionBytes> binaryBytes, ILogger logger)
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
                    _Connection.Open();
                    var transaction = _Connection.BeginTransaction();
                    var command = _Connection.CreateCommand();
                    command.Transaction = transaction;

                    command.CommandText =
                        $"INSERT INTO {TableName} " +
                        $"(Binary, PESection, COFFGroup, SymbolName, RVA, VirtualSize, Libraryname, CompilandName, IsPadding, IsPGO, IsOptimizedForSpeed) " +
                        $"VALUES " +
                        $"(@Binary, @SectionName, @CoffgroupName, @SymbolName, @RVA, @VirtualSize, @LibraryFilename, " +
                        $"@CompilandName, @IsPadding, @IsPGO, @IsOptimizedForSpeed)";

                    command.Parameters.AddWithValue("@Binary", "");
                    command.Parameters.AddWithValue("@SectionName", "");
                    command.Parameters.AddWithValue("@CoffgroupName", "");
                    command.Parameters.AddWithValue("@SymbolName", "");
                    command.Parameters.AddWithValue("@RVA", "");
                    command.Parameters.AddWithValue("@VirtualSize", "");
                    command.Parameters.AddWithValue("@LibraryFilename", "");
                    command.Parameters.AddWithValue("@CompilandName", "");
                    command.Parameters.AddWithValue("@IsPadding", "");
                    command.Parameters.AddWithValue("@IsPGO", "");
                    command.Parameters.AddWithValue("@IsOptimizedForSpeed", "");

                    foreach (var section in binaryBytes)
                    {
                        foreach (var item in section.Items)
                        {
                            var isPadding = item.IsPadding ? 1 : 0;
                            var isPGO = item.IsPGO ? 1 : 0;
                            var isOptimizedForSpeed = item.IsOptimizedForSpeed ? 1 : 0;
                            var escapedName = item.Name.Replace("'", "''", StringComparison.Ordinal);

                            InsertItem(binary, section.SectionName, item.CoffgroupName, escapedName, item.RVA,
                                item.VirtualSize, item.LibraryFilename, item.CompilandName, isPadding, isPGO,
                                isOptimizedForSpeed, command);
                        }
                    }

                    transaction.Commit();
                    command.Dispose();

                    _Connection.Close();
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

    private static int InsertItem(string binary, string secton, string coff, string symbolname,
        uint rva, ulong virtualSize, string lib, string compiland, int isPadding, int isPgo, int isOptimizedForSpeed, SqliteCommand command)
    {
        command.Parameters["@Binary"].Value = binary;
        command.Parameters["@SectionName"].Value = secton;
        command.Parameters["@CoffgroupName"].Value = coff;
        command.Parameters["@SymbolName"].Value = symbolname;
        command.Parameters["@RVA"].Value = rva;
        command.Parameters["@VirtualSize"].Value = virtualSize;
        command.Parameters["@LibraryFilename"].Value = lib;
        command.Parameters["@CompilandName"].Value = compiland;
        command.Parameters["@IsPadding"].Value = isPadding;
        command.Parameters["@IsPGO"].Value = isPgo;
        command.Parameters["@IsOptimizedForSpeed"].Value = isOptimizedForSpeed;

        return command.ExecuteNonQuery();
    }
}
