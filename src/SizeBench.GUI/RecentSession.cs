using System.IO;
using System.Web;
using SizeBench.AnalysisEngine;

namespace SizeBench.GUI;

internal enum RecentSessionKind
{
    SingleBinary,
    BinaryDiff
}

internal sealed class RecentSession
{
    private RecentSession(RecentSessionKind kind,
                          string? binaryPath,
                          string? pdbPath,
                          SymbolSourcesSupported symbolSourcesSupported,
                          string? beforeBinaryPath,
                          string? beforePdbPath,
                          string? afterBinaryPath,
                          string? afterPdbPath,
                          DateTimeOffset lastOpenedUtc)
    {
        this.Kind = kind;
        this.BinaryPath = binaryPath;
        this.PDBPath = pdbPath;
        this.SymbolSourcesSupported = symbolSourcesSupported;
        this.BeforeBinaryPath = beforeBinaryPath;
        this.BeforePdbPath = beforePdbPath;
        this.AfterBinaryPath = afterBinaryPath;
        this.AfterPdbPath = afterPdbPath;
        this.LastOpenedUtc = lastOpenedUtc;
    }

    public RecentSessionKind Kind { get; }

    public string? BinaryPath { get; }

    public string? PDBPath { get; }

    public SymbolSourcesSupported SymbolSourcesSupported { get; }

    public string? BeforeBinaryPath { get; }

    public string? BeforePdbPath { get; }

    public string? AfterBinaryPath { get; }

    public string? AfterPdbPath { get; }

    public DateTimeOffset LastOpenedUtc { get; }

    public string DisplayName => this.Kind switch
    {
        RecentSessionKind.SingleBinary => GetFriendlyFileName(this.BinaryPath),
        RecentSessionKind.BinaryDiff => $"{GetFriendlyFileName(this.BeforeBinaryPath)} -> {GetFriendlyFileName(this.AfterBinaryPath)}",
        _ => throw new InvalidOperationException($"Unknown recent session kind '{this.Kind}'.")
    };

    public string SessionKindDisplayName => this.Kind switch
    {
        RecentSessionKind.SingleBinary => "Single binary",
        RecentSessionKind.BinaryDiff => "Diff",
        _ => throw new InvalidOperationException($"Unknown recent session kind '{this.Kind}'.")
    };

    public string DetailsText => this.Kind switch
    {
        RecentSessionKind.SingleBinary => $"Binary: {this.BinaryPath}{Environment.NewLine}PDB: {this.PDBPath}",
        RecentSessionKind.BinaryDiff => $"Before: {this.BeforeBinaryPath}{Environment.NewLine}After: {this.AfterBinaryPath}",
        _ => throw new InvalidOperationException($"Unknown recent session kind '{this.Kind}'.")
    };

    public bool IsLaunchable => GetMissingRequiredPaths().Count == 0;

    public string UnavailableReason
    {
        get
        {
            var missingPaths = GetMissingRequiredPaths();
            return missingPaths.Count == 0 ? String.Empty
                                           : $"Missing: {String.Join(", ", missingPaths.Select(Path.GetFileName))}";
        }
    }

    public static RecentSession CreateSingle(string binaryPath,
                                             string pdbPath,
                                             SessionOptions sessionOptions,
                                             DateTimeOffset? lastOpenedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(binaryPath);
        ArgumentNullException.ThrowIfNull(pdbPath);
        ArgumentNullException.ThrowIfNull(sessionOptions);

        return new RecentSession(RecentSessionKind.SingleBinary,
                                 binaryPath,
                                 pdbPath,
                                 sessionOptions.SymbolSourcesSupported,
                                 beforeBinaryPath: null,
                                 beforePdbPath: null,
                                 afterBinaryPath: null,
                                 afterPdbPath: null,
                                 lastOpenedUtc ?? DateTimeOffset.UtcNow);
    }

    public static RecentSession CreateDiff(string beforeBinaryPath,
                                           string beforePdbPath,
                                           string afterBinaryPath,
                                           string afterPdbPath,
                                           DateTimeOffset? lastOpenedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(beforeBinaryPath);
        ArgumentNullException.ThrowIfNull(beforePdbPath);
        ArgumentNullException.ThrowIfNull(afterBinaryPath);
        ArgumentNullException.ThrowIfNull(afterPdbPath);

        return new RecentSession(RecentSessionKind.BinaryDiff,
                                 binaryPath: null,
                                 pdbPath: null,
                                 SymbolSourcesSupported.All,
                                 beforeBinaryPath,
                                 beforePdbPath,
                                 afterBinaryPath,
                                 afterPdbPath,
                                 lastOpenedUtc ?? DateTimeOffset.UtcNow);
    }

    internal bool Matches(RecentSession other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return this.Kind == other.Kind &&
               String.Equals(this.BinaryPath, other.BinaryPath, StringComparison.OrdinalIgnoreCase) &&
               String.Equals(this.PDBPath, other.PDBPath, StringComparison.OrdinalIgnoreCase) &&
               this.SymbolSourcesSupported == other.SymbolSourcesSupported &&
               String.Equals(this.BeforeBinaryPath, other.BeforeBinaryPath, StringComparison.OrdinalIgnoreCase) &&
               String.Equals(this.BeforePdbPath, other.BeforePdbPath, StringComparison.OrdinalIgnoreCase) &&
               String.Equals(this.AfterBinaryPath, other.AfterBinaryPath, StringComparison.OrdinalIgnoreCase) &&
               String.Equals(this.AfterPdbPath, other.AfterPdbPath, StringComparison.OrdinalIgnoreCase);
    }

    internal RecentSession WithUpdatedTimestamp(DateTimeOffset lastOpenedUtc) =>
        new RecentSession(this.Kind,
                          this.BinaryPath,
                          this.PDBPath,
                          this.SymbolSourcesSupported,
                          this.BeforeBinaryPath,
                          this.BeforePdbPath,
                          this.AfterBinaryPath,
                          this.AfterPdbPath,
                          lastOpenedUtc);

    internal SessionOptions ToSessionOptions() => new SessionOptions() { SymbolSourcesSupported = this.SymbolSourcesSupported };

    internal Uri ToDeeplinkUri()
    {
        var queryBuilder = HttpUtility.ParseQueryString(String.Empty);
        string path;

        if (this.Kind == RecentSessionKind.SingleBinary)
        {
            path = "SingleBinaryOverview";
            queryBuilder.Add("BinaryPath", this.BinaryPath);
            queryBuilder.Add("PDBPath", this.PDBPath);
            queryBuilder.Add("SymbolSourcesSupported", this.SymbolSourcesSupported.ToString("D"));
        }
        else if (this.Kind == RecentSessionKind.BinaryDiff)
        {
            path = "BinaryDiffOverview";
            queryBuilder.Add("BeforeBinaryPath", this.BeforeBinaryPath);
            queryBuilder.Add("BeforePDBPath", this.BeforePdbPath);
            queryBuilder.Add("AfterBinaryPath", this.AfterBinaryPath);
            queryBuilder.Add("AfterPDBPath", this.AfterPdbPath);
        }
        else
        {
            throw new InvalidOperationException($"Unknown recent session kind '{this.Kind}'.");
        }

        return new UriBuilder()
        {
            Host = "2.0",
            Scheme = "sizebench",
            Path = path,
            Query = queryBuilder.ToString()
        }.Uri;
    }

    private List<string> GetMissingRequiredPaths() => GetRequiredPaths().Where(path => File.Exists(path) == false).ToList();

    private string[] GetRequiredPaths() => this.Kind switch
    {
        RecentSessionKind.SingleBinary => new[] { this.BinaryPath!, this.PDBPath! },
        RecentSessionKind.BinaryDiff => new[] { this.BeforeBinaryPath!, this.BeforePdbPath!, this.AfterBinaryPath!, this.AfterPdbPath! },
        _ => throw new InvalidOperationException($"Unknown recent session kind '{this.Kind}'.")
    };

    private static string GetFriendlyFileName(string? path)
    {
        if (String.IsNullOrEmpty(path))
        {
            return "(unknown)";
        }

        var fileName = Path.GetFileName(path);
        return String.IsNullOrEmpty(fileName) ? path : fileName;
    }
}
