using SizeBench.AnalysisEngine;

namespace BinaryBytes;

// TODO: need to add more args for opt-in/out of symbol types
internal static class CommandLineArgs
{
    public static string PdbPath { get; private set; } = String.Empty;
    public static string BinaryPath { get; private set; } = String.Empty;
    public static string OutfilePath { get; private set; } = String.Empty;
    public static bool IsMultiFileCommand { get; private set; }

    // Default to code symbols, opt in to others
    public static SymbolSourcesSupported SymbolSourcesSupported { get; private set; } = SymbolSourcesSupported.Code;

    public static bool ProcessArgs(string[] args)
    {
        var goodCommand = true;

        if (args.Length < 1)
        {
            PrintUsage(isInvalidCommand: true);
            goodCommand = false;
        }
        else if (args[0] is "/?" or "?" or "-?")
        {
            PrintUsage();
            goodCommand = false;
        }
        else
        {
            foreach (var option in args)
            {
                var optionValues = option.ToLowerInvariant().Split('=');
                if (optionValues.Length == 1)
                {
                    switch (optionValues[0])
                    {
                        case "/include-all-symbols":
                            SymbolSourcesSupported = SymbolSourcesSupported.All;
                            break;
                        case "/include-data-symbols":
                            SymbolSourcesSupported |= SymbolSourcesSupported.DataSymbols;
                            break;
                        default:
                            PrintUsage(isInvalidCommand: true);
                            goodCommand = false;
                            break;
                    }
                }
                else if (optionValues.Length == 2 && !String.IsNullOrEmpty(optionValues[0]) && !String.IsNullOrEmpty(optionValues[1]))
                {
                    switch (optionValues[0])
                    {
                        case "/pdb-file":
                            IsMultiFileCommand = false;
                            PdbPath = optionValues[1];
                            break;
                        case "/pdb-dir":
                            IsMultiFileCommand = true;
                            PdbPath = optionValues[1];
                            break;
                        case "/binary-file":
                            BinaryPath = optionValues[1];
                            break;
                        case "/binary-dir":
                            BinaryPath = optionValues[1];
                            break;
                        case "/out-file":
                            OutfilePath = optionValues[1];
                            break;
                        default:
                            PrintUsage(isInvalidCommand: true);
                            goodCommand = false;
                            break;
                    }
                }
                else
                {
                    PrintUsage(isInvalidCommand: true);
                    break;
                }
            }
        }

        return goodCommand;
    }

    private static void PrintUsage(bool isInvalidCommand = false)
    {
        if (isInvalidCommand)
        {
            Console.WriteLine("Invalid command line...");
            Console.WriteLine();
        }

        Console.WriteLine("Usage:");
        Console.WriteLine();
        Console.WriteLine("BinaryBytes.exe /pdb-file=<PDB Path> [/binary-file=<Binary Path>] [/out-file=<Output SQLite DB path>]");
        Console.WriteLine("BinaryBytes.exe /pdb-dir=<PDB Directory Path> [/binary-dir=<Binary Directory Path>] [/out-file=<Output SQLite DB path>]");
        Console.WriteLine();
        Console.WriteLine("BinaryBytes defaults to only parsing out code symbols from a binary (functions, thunks, separated code blocks for PGO, etc.)");
        Console.WriteLine("You can customize this to parse more symbols at the cost of increased runtime, with the following switches:");
        Console.WriteLine("    /include-data-symbols    This will add data symbols like static/constexpr data.");
        Console.WriteLine("    /include-all-symbols     This will add all symbols, including data, PDATA, XDATA, RSRC, and PE symbols.");
        Console.WriteLine("                             /include-all-symbols implies /include-data-symbols.");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("(1) If binary path (via /binary-file or /binary-dir) is not specified, the tool tries to find it under the current directory tree.");
        Console.WriteLine("(2) If the pdb path specified is a Windows build share then the tool knows where to look for the binaries.");
        Console.WriteLine("(3) If output db path is not specified (via /out-file) then the tool will by default create a file named BinaryBytes-<Today's Date>.db");
    }
}
