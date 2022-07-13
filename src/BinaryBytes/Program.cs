using System.Globalization;
using System.IO;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace BinaryBytes;

public static class Program
{
    private static readonly string _outputfile = String.Format(CultureInfo.InvariantCulture, "BinaryBytes-Log-{0:yyyy-MM-dd}.log", DateTime.Now);

    public static async Task Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            if (CommandLineArgs.ProcessArgs(args))
            {
                using var appLogger = new ApplicationLogger("BinaryBytes", null);
                if (CommandLineArgs.IsMultiFileCommand)
                {
                    await ProcessMultiplePdbFiles(appLogger, CommandLineArgs.PdbPath);
                }
                else
                {
                    await ProcessSinglePdbFile(appLogger, CommandLineArgs.PdbPath, CommandLineArgs.BinaryPath);
                }
            }
        }
        catch (Exception ex)
        {
            LogErrorAndReportToConsoleOutput("Exception thrown somewhere in BinaryBytes!", ex, null);
            throw;
        }
    }

    public static void LogErrorAndReportToConsoleOutput(string error, Exception ex, ILogger? logger)
    {
        var originalForeground = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.GetFormattedTextForLogging(error, Environment.NewLine));
            logger?.LogException(error, ex);
        }
        finally
        {
            Console.ForegroundColor = originalForeground;
        }
    }

    private static async Task ProcessMultiplePdbFiles(ApplicationLogger appLogger, string pdbDirPath)
    {
        var pdbfiles = Directory.EnumerateFiles(pdbDirPath, "*.pdb", SearchOption.AllDirectories);
        foreach (var pdbfile in pdbfiles)
        {
            await ProcessSinglePdbFile(appLogger, pdbfile, null);
        }
    }

    private static async Task ProcessSinglePdbFile(ApplicationLogger appLogger, string pdbFilePath, string? binaryFile)
    {
        using var sessionLogger = appLogger.CreateSessionLog(pdbFilePath);
        sessionLogger.Log("Processing...");
        sessionLogger.Log($"PDB Path: {pdbFilePath}");

        var binaryFilePath = Utilities.InferBinaryPath(pdbFilePath, binaryFile);
        if (binaryFilePath != null && File.Exists(pdbFilePath) && File.Exists(binaryFilePath))
        {
            sessionLogger.Log($"Binary Path: {binaryFilePath}");
            await DoTheWork(pdbFilePath, binaryFilePath, sessionLogger);
        }
        else
        {
            sessionLogger.Log($"Binary Path: {binaryFilePath}");
            sessionLogger.Log("Either the PDB path or the Binary path invalid!", LogLevel.Error);
        }
        sessionLogger.Log($"End of processing: {pdbFilePath}\n");

        // Appends logs from different session to the same file (default WriteSessionLogToFile behavior)
        WriteSessionLogToFile(appLogger);
    }

    private static void WriteSessionLogToFile(ApplicationLogger appLogger)
    {
        using var file = File.AppendText(_outputfile);
        appLogger.WriteLog(file);
    }

    /// <summary>
    /// This routine is the enrty point for doing all of the work in getting bytes detail of a given binary.
    /// </summary>
    private static async Task DoTheWork(string pdbFilePath, string binaryFilePath, ILogger sessionLogger)
    {
        try
        {
            IEnumerable<SectionBytes> binaryBytes = Array.Empty<SectionBytes>();

            await using (var session = await Session.Create(binaryFilePath, pdbFilePath, sessionLogger))
            {
                var sections = await session.EnumerateBinarySectionsAndCOFFGroups(CancellationToken.None);
                var compilands = await session.EnumerateCompilands(CancellationToken.None);

                var rvaToContributorMap = Utilities.CreateRvaToContributorMap(compilands);
                binaryBytes = await ProcessSectionBytes(session, sections, rvaToContributorMap);
            }

            WriteBytesToDatabase(binaryFilePath, binaryBytes, sessionLogger);
        }
#pragma warning disable CA1031 // Do not catch general exception types - if we fail processing one binary it's desirable to keep going
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            LogErrorAndReportToConsoleOutput($"Exception thrown while processing {binaryFilePath}.  Will keep processing any additional binaries.", ex, sessionLogger);
        }
    }

    /// <summary>
    /// This routine calls into SQLite DB handler code to setup a DB (if one is not already setup)
    /// and then insert the bytes data into it.
    /// Note that for our purpose this DB is stored as a flat file database.
    /// </summary>
    private static void WriteBytesToDatabase(string binaryFilePath, IEnumerable<SectionBytes> binaryBytes, ILogger sessionLogger)
    {
        ApplicationDbHandler.SetupDb(CommandLineArgs.OutfilePath, sessionLogger);
        ApplicationDbHandler.AddData(binaryFilePath, binaryBytes, sessionLogger);
    }

    /// <summary>
    /// Given a list of PE sections, this routine enumerates all of the COFF groups and in turn the symbols 
    /// within those groups, for each of the section and mark the "bytes" as Symbols or Padding or SepcialCase 
    /// (i.e. Sections without any COFF groups OR COFF groups without any symbols, example .reloc section).  
    /// </summary>
    private static async Task<IEnumerable<SectionBytes>> ProcessSectionBytes(Session session, IReadOnlyList<BinarySection> sections, Dictionary<uint, SymbolContributor> rvaToContributorMap)
    {
        var binaryBytes = new List<SectionBytes>();
        foreach (var section in sections)
        {
            var adjustedSymbols = await ProcessCoffGroupBytes(session, section, rvaToContributorMap);
            MarkSpecialBytesInSection(section, rvaToContributorMap, ref adjustedSymbols);

            binaryBytes.Add(new SectionBytes()
            {
                SectionName = section.Name,
                Items = adjustedSymbols
            });
        }

        return binaryBytes;
    }

    private static void MarkSpecialBytesInSection(BinarySection section, Dictionary<uint, SymbolContributor> rvaToContributorMap, ref List<BytesItem> adjustedSymbols)
    {
        if (section.COFFGroups.Count > 0)
        {
            var startingByteOfFirstCoffgroup = section.COFFGroups[0].RVA;
            ulong endingByteOfLastCoffgroup = section.COFFGroups[section.COFFGroups.Count - 1].RVA +
                                            section.COFFGroups[section.COFFGroups.Count - 1].Size;

            // Is there padding at the start of the section?
            if (section.RVA < startingByteOfFirstCoffgroup)
            {
                adjustedSymbols.Insert(0,
                    Utilities.CreatePaddingBytesItem(Constants.SectionStartPadding, String.Empty,
                        section.RVA,
                        startingByteOfFirstCoffgroup - section.RVA));
            }

            // Is there padding at the end of the section?
            ulong endingByteOfTheSection = section.RVA + section.Size;
            if (endingByteOfTheSection > endingByteOfLastCoffgroup)
            {
                adjustedSymbols.Insert(adjustedSymbols.Count,
                    Utilities.CreatePaddingBytesItem(Constants.SectionEndPadding, String.Empty,
                        (uint)endingByteOfLastCoffgroup,
                        endingByteOfTheSection - endingByteOfLastCoffgroup));
            }
        }
        else
        {
            var rvaContributor = Utilities.GetContributorForRva(section.RVA, rvaToContributorMap);
            adjustedSymbols.Add(Utilities.CreateSpecialBytesItem(Constants.SpecialSection, String.Empty,
                section.RVA, section.Size, rvaContributor));
        }
    }

    /// <summary>
    /// Given a section, this routine enumerates all of the COFF groups in it and 
    /// processes the bytes in each of those groups.
    /// </summary>
    private static async Task<List<BytesItem>> ProcessCoffGroupBytes(Session session, BinarySection section, Dictionary<uint, SymbolContributor> rvaToContributorMap)
    {
        var adjustedSymbols = new List<BytesItem>();
        foreach (var coffgroup in section.COFFGroups)
        {
            var symbols = await session.EnumerateSymbolsInCOFFGroup(coffgroup, CancellationToken.None);

            if (symbols != null && symbols.Count > 0)
            {
                IdentifyPaddingAroundSymbols(symbols, coffgroup, rvaToContributorMap, ref adjustedSymbols);
            }
            else
            {
                var rvaContributor = Utilities.GetContributorForRva(coffgroup.RVA, rvaToContributorMap);
                adjustedSymbols.Add(Utilities.CreateSpecialBytesItem(Constants.SpecialCoffGroup,
                    coffgroup.Name, coffgroup.RVA, coffgroup.Size, rvaContributor));
            }
        }

        return adjustedSymbols;
    }

    /// <summary>
    /// Given a COFF group and a list of symbols in that group, this routine identifies all the Padding bytes and
    /// the actual symbols bytes in the COFF group and marks them appropriately.
    /// </summary>
    private static void IdentifyPaddingAroundSymbols(IReadOnlyList<ISymbol> symbols, COFFGroup coffgroup, Dictionary<uint, SymbolContributor> rvaToContributorMap, ref List<BytesItem> bytesItems)
    {
        // Is there gap at the start of this COFF group?
        var startingByteOfCoffgroup = coffgroup.RVA;
        var startingByteOfFirstSymbol = symbols[0].RVA;
        if (startingByteOfCoffgroup != startingByteOfFirstSymbol && startingByteOfCoffgroup < startingByteOfFirstSymbol)
        {
            bytesItems.Add(Utilities.CreatePaddingBytesItem(Constants.CoffgroupStartPadding, coffgroup.Name,
                startingByteOfCoffgroup, startingByteOfFirstSymbol - startingByteOfCoffgroup));
        }

        // Identify gaps between symbols
        uint endingByteOfPreviousSymbol = 0;
        foreach (var symbol in symbols)
        {
            // Look for padding
            var startingByteOfCurrentSymbol = symbol.RVA;
            if (endingByteOfPreviousSymbol != 0 && startingByteOfCurrentSymbol > endingByteOfPreviousSymbol)
            {
                bytesItems.Add(Utilities.CreatePaddingBytesItem(Constants.SymbolPadding, coffgroup.Name,
                    endingByteOfPreviousSymbol, startingByteOfCurrentSymbol - endingByteOfPreviousSymbol));
            }
            endingByteOfPreviousSymbol = symbol.RVA + symbol.Size;

            // Add the actual symbol itself to the items list
            var rvaContributor = Utilities.GetContributorForRva(symbol.RVA, rvaToContributorMap);
            bytesItems.Add(Utilities.CreateSymbolsBytesItem(symbol, coffgroup.Name, rvaContributor));
        }

        // Is there gap at the end of this COFF group?
        ulong endingByteOfCoffgroup = coffgroup.RVA + coffgroup.Size;
        var endingByteOfLastSymbol = symbols[symbols.Count - 1].RVA + symbols[symbols.Count - 1].Size;
        if (endingByteOfCoffgroup != endingByteOfLastSymbol && endingByteOfCoffgroup > endingByteOfLastSymbol)
        {
            bytesItems.Add(Utilities.CreatePaddingBytesItem(Constants.CoffgroupEndPadding, coffgroup.Name,
                    endingByteOfLastSymbol, endingByteOfCoffgroup - endingByteOfLastSymbol));
        }
    }
}
