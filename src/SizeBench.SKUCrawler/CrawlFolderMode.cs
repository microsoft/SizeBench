using System.Diagnostics;
using System.Globalization;
using System.IO;
using SizeBench.Logging;

namespace SizeBench.SKUCrawler.CrawlFolder;

internal sealed class CrawlFolderArguments : ApplicationArguments
{
    public string? CrawlRoot { get; set; }

    public bool IsMasterController => !this.IsBatch;
    public bool IsBatch { get; set; }

    private int _batchNumber;
    public int BatchNumber
    {
        get => this.IsBatch ? this._batchNumber : throw new InvalidOperationException("BatchNumber should not be used unless IsBatch == true");
        set => this._batchNumber = value;
    }

    public bool IncludeWastefulVirtuals { get; set; }
    public bool IncludeCodeSymbols { get; set; }
    public bool IncludeDuplicateDataItems { get; set; }

    public int BatchSize { get; } = 25; // Make this customizable later if we need to

    public string TimestampOfMaster { get; set; }


    public CrawlFolderArguments()
    {
        this.TimestampOfMaster = $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss}";
    }

    internal override bool? TryParse(string[] args)
    {
        if (base.TryParse(args) != true)
        {
            return false;
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("/folderRoot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                this.CrawlRoot = args[i + 1];
                i++;
            }
            else if (args[i].Equals("/batch", StringComparison.OrdinalIgnoreCase))
            {
                this.IsBatch = true;
            }
            else if (args[i].Equals("/batchNumber", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                this.BatchNumber = Convert.ToInt32(args[i + 1], CultureInfo.InvariantCulture);
                i++; // Skip the batchNumber value
            }
            else if (args[i].Equals("/timestampOfMaster", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                this.TimestampOfMaster = args[i + 1];
                i++; // Skip the timestampOfMaster value
            }
            else if (args[i].Equals("/includeWastefulVirtuals", StringComparison.OrdinalIgnoreCase))
            {
                this.IncludeWastefulVirtuals = true;
            }
            else if (args[i].Equals("/includeCodeSymbols", StringComparison.OrdinalIgnoreCase))
            {
                this.IncludeCodeSymbols = true;
            }
            else if (args[i].Equals("/includeDuplicateData", StringComparison.OrdinalIgnoreCase))
            {
                this.IncludeDuplicateDataItems = true;
            }
        }

        return !String.IsNullOrEmpty(this.CrawlRoot);
    }

    public override IEnumerable<FileInfo> GetDatabaseFilesToMerge()
        => new DirectoryInfo(this.OutputFolder).EnumerateFileSystemInfos($"SizeBench.SKUCrawler-{this.TimestampOfMaster}-batch*.db").OrderBy(fsi => fsi.Name).Cast<FileInfo>();

    public string CommandLineArgsForBatch(int batchNumber)
    {
        return $"/batch /batchNumber {batchNumber} /timestampOfMaster \"{this.TimestampOfMaster}\" /outputFolder \"{this.OutputFolder}\"" +
               $" {(this.IncludeWastefulVirtuals ? "/includeWastefulVirtuals" : "")}" +
               $" {(this.IncludeCodeSymbols ? "/includeCodeSymbols" : "")}" +
               $" {(this.IncludeDuplicateDataItems ? "/includeDuplicateData" : "")}" +
               $" /folderRoot \"{this.CrawlRoot}\"";
    }
}

internal static class CrawlFolderBinaryCollector
{
    public static List<ProductBinary> FindAllBinariesForTheseArgs(CrawlFolderArguments crawlArgs, IApplicationLogger appLogger)
    {
        var productBinaries = new List<ProductBinary>();

        using (var taskLog = appLogger.StartTaskLog($"Getting list of binaries in {crawlArgs.CrawlRoot}"))
        {
            if (crawlArgs.IsMasterController)
            {
                Console.Out.WriteLine("Folder mode!  Discovering binaries in the folder...");
            }

            var binaryDiscoveryWatch = Stopwatch.StartNew();
            productBinaries = GetListOfBinariesFromFolderRecursively(crawlArgs.CrawlRoot!);
            binaryDiscoveryWatch.Stop();

            if (crawlArgs.IsMasterController)
            {
                var output = $"Enumerating binaries from the folder found {productBinaries.Count} binaries in {binaryDiscoveryWatch.Elapsed}";

                Console.Out.WriteLine(output);
                taskLog.Log(output);
            }
        }

        return productBinaries;
    }

    private static List<ProductBinary> GetListOfBinariesFromFolderRecursively(string folderRoot)
    {
        var productBinaries = new List<ProductBinary>();
        CrawlFolder(new DirectoryInfo(folderRoot), productBinaries);

        return productBinaries;
    }

    private static void CrawlFolder(DirectoryInfo directory, List<ProductBinary> binaries)
    {
        try
        {
            foreach (var folder in directory.EnumerateDirectories())
            {
                CrawlFolder(folder, binaries);
            }

            string[] potentialBinaryExtensions = { "dll", "exe", "sys", "pyd", "efi" };

            foreach (var pdbFile in directory.EnumerateFiles("*.pdb"))
            {
                // Check for clang-style "foo.dll.pdb" first
                if (File.Exists(pdbFile.FullName[0..^4]))
                {
                    binaries.Add(new ProductBinary(pdbPath: pdbFile.FullName, binaryPath: pdbFile.FullName[0..^4]));
                    continue;
                }

                foreach (var binaryExtension in potentialBinaryExtensions)
                {
                    if (File.Exists(Path.ChangeExtension(pdbFile.FullName, binaryExtension)))
                    {
                        binaries.Add(new ProductBinary(pdbPath: pdbFile.FullName, binaryPath: Path.ChangeExtension(pdbFile.FullName, binaryExtension)));
                        break;
                    }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // swallow this, we'll just keep going
        }
    }
}
