using System.IO;

namespace SizeBench.SKUCrawler.CrawlFolder;

internal class MergeArguments : ApplicationArguments
{
    public List<string> FilesToMerge { get; } = new List<string>();

    internal override bool? TryParse(string[] args)
    {
        if (base.TryParse(args) != true)
        {
            return false;
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("/merge", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                this.FilesToMerge.Add(args[i + 1]);
                i++;
            }
            else if (args[i].Equals("/mergeFolder", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                CrawlFolder(new DirectoryInfo(args[i + 1]));
                i++;
            }
        }

        if (this.FilesToMerge.Count == 1)
        {
            Program.PrintArgumentErrorThenHelpAndExit("When merging, must specify at least two files to merge with \"/merge [fileName]\", or via folders with \"/mergeFolder [folderName]\"");
            return false;
        }
        else if (this.FilesToMerge.Count >= 2)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public override IEnumerable<FileInfo> GetDatabaseFilesToMerge()
    {
        foreach (var file in this.FilesToMerge)
        {
            yield return new FileInfo(file);
        }
    }

    private void CrawlFolder(DirectoryInfo directory)
    {
        try
        {
            foreach (var folder in directory.EnumerateDirectories())
            {
                CrawlFolder(folder);
            }

            foreach (var dbFile in directory.EnumerateFiles("*.db"))
            {
                this.FilesToMerge.Add(dbFile.FullName);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // swallow this, we'll just keep going
        }
    }
}
