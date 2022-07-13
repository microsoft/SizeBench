using System.IO;

namespace SizeBench.SKUCrawler;

internal abstract class ApplicationArguments
{
    public string OutputFolder { get; set; } = ".";

    public abstract IEnumerable<FileInfo> GetDatabaseFilesToMerge();

    internal virtual bool? TryParse(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("/outputFolder", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                this.OutputFolder = args[i + 1];
                i++; // skip the outputFolder's value
            }
        }

        return true;
    }
}
