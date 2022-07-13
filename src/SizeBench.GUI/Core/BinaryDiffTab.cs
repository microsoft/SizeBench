using System.IO;
using Castle.Windsor;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI.Core;

internal sealed class BinaryDiffTab : TabBase
{
    public IDiffSession DiffSession { get; }

    protected override Uri HomePage => new Uri(@"BinaryDiffOverview", UriKind.Relative);

    public override string CurrentDeeplink
    {
        get
        {
            var beforeBinaryPath = Uri.EscapeDataString(this.DiffSession.BeforeSession.BinaryPath);
            var beforePdbPath = Uri.EscapeDataString(this.DiffSession.BeforeSession.PdbPath);
            var afterBinaryPath = Uri.EscapeDataString(this.DiffSession.AfterSession.BinaryPath);
            var afterPdbPath = Uri.EscapeDataString(this.DiffSession.AfterSession.PdbPath);
            var originalCurrentSource = this.CurrentPage.OriginalString;
            var inAppPage = Uri.EscapeDataString(originalCurrentSource);

            var deeplinkUrl = $"sizebench://2.0/{inAppPage}?" +
                              $"BeforeBinaryPath={beforeBinaryPath}&" +
                              $"BeforePDBPath={beforePdbPath}&" +
                              $"AfterBinaryPath={afterBinaryPath}&" +
                              $"AfterPDBPath={afterPdbPath}";

            return deeplinkUrl;
        }
    }

    public override string Header => Path.GetFileNameWithoutExtension(this.DiffSession.BeforeSession.BinaryPath);

    public override string ToolTip => $"{this.DiffSession.BeforeSession.BinaryPath}{Environment.NewLine}" +
               $"vs.{Environment.NewLine}" +
               $"{this.DiffSession.AfterSession.BinaryPath}";

    public override string BinaryPathForWindowTitle
    {
        get
        {
            var filePaths = new List<string>()
                {
                    this.DiffSession.BeforeSession.BinaryPath,
                    this.DiffSession.AfterSession.BinaryPath
                };

            var MatchingChars = from len in Enumerable.Range(0, filePaths.Min(s => s.Length)).Reverse()
                                let possibleMatch = filePaths.First()[..len]
                                where filePaths.All(f => f.StartsWith(possibleMatch, StringComparison.Ordinal))
                                select possibleMatch;

            var matchingCharsFirst = MatchingChars.First();
            var LongestSharedDir = (String.IsNullOrEmpty(matchingCharsFirst) ? "" : Path.GetDirectoryName(matchingCharsFirst)) ?? String.Empty;
            var startIndex = LongestSharedDir.Length;

            // Back up one folder so that
            //    \\share\path\that\is\long\experiment1\before\blah.dll
            //    vs.
            //    \\share\path\that\is\long\experiment1\after\blah.dll
            // Can have a window title like this:
            //    experiment1\before\blah.dll vs. experiment1\after\blah.dll
            //
            // The common use case is to have "before" and "after" folders so backing up one level keeps the name
            // of the experimental folder in there which is useful.
            if (LongestSharedDir.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                startIndex = LongestSharedDir.LastIndexOf(Path.DirectorySeparatorChar) + 1;
            }

            return $"{this.DiffSession.BeforeSession.BinaryPath[startIndex..]} vs. {this.DiffSession.AfterSession.BinaryPath[startIndex..]}";
        }
    }

    public BinaryDiffTab(IDiffSession session, IWindsorContainer container)
        : base(session, container)
    {
        this.DiffSession = session;
    }
}
