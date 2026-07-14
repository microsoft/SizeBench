using System.IO;
using System.Text.Json;
using SizeBench.AnalysisEngine;
using SizeBench.Logging;

namespace SizeBench.GUI;

internal interface IRecentSessionStore
{
    IReadOnlyList<RecentSession> GetRecentSessions();

    RecentSession RecordSession(RecentSession recentSession);
}

internal sealed class RecentSessionStore : IRecentSessionStore
{
    internal const int MaximumStoredSessions = 10;

    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions()
    {
        WriteIndented = true
    };

    private readonly IApplicationLogger _applicationLogger;
    private readonly string _storagePath;
    private readonly List<RecentSession> _recentSessions;

    public RecentSessionStore(IApplicationLogger applicationLogger)
        : this(applicationLogger, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SizeBench", "RecentSessions.json"))
    {
    }

    internal RecentSessionStore(IApplicationLogger applicationLogger, string storagePath)
    {
        this._applicationLogger = applicationLogger ?? throw new ArgumentNullException(nameof(applicationLogger));
        this._storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
        this._recentSessions = LoadRecentSessions();
    }

    public IReadOnlyList<RecentSession> GetRecentSessions() => this._recentSessions.AsReadOnly();

    public RecentSession RecordSession(RecentSession recentSession)
    {
        ArgumentNullException.ThrowIfNull(recentSession);

        var persistedSession = recentSession.WithUpdatedTimestamp(DateTimeOffset.UtcNow);
        var existingSessionIndex = this._recentSessions.FindIndex(candidate => candidate.Matches(persistedSession));
        if (existingSessionIndex >= 0)
        {
            this._recentSessions.RemoveAt(existingSessionIndex);
        }

        this._recentSessions.Insert(0, persistedSession);
        if (this._recentSessions.Count > MaximumStoredSessions)
        {
            this._recentSessions.RemoveRange(MaximumStoredSessions, this._recentSessions.Count - MaximumStoredSessions);
        }

        PersistRecentSessions();
        return persistedSession;
    }

    private List<RecentSession> LoadRecentSessions()
    {
        if (File.Exists(this._storagePath) == false)
        {
            return new List<RecentSession>();
        }

        try
        {
            using var stream = File.OpenRead(this._storagePath);
            var persistedSessions = JsonSerializer.Deserialize<List<PersistedRecentSession>>(stream, SerializerOptions);

            if (persistedSessions is null)
            {
                return new List<RecentSession>();
            }

            return persistedSessions.Select(static recentSession => recentSession.ToRecentSession())
                                    .Where(static recentSession => recentSession is not null)
                                    .Select(static recentSession => recentSession!)
                                    .OrderByDescending(static recentSession => recentSession.LastOpenedUtc)
                                    .Take(MaximumStoredSessions)
                                    .ToList();
        }
        catch (JsonException ex)
        {
            this._applicationLogger.LogException("Unable to deserialize recent session history.", ex);
            return new List<RecentSession>();
        }
        catch (IOException ex)
        {
            this._applicationLogger.LogException("Unable to read recent session history.", ex);
            return new List<RecentSession>();
        }
        catch (UnauthorizedAccessException ex)
        {
            this._applicationLogger.LogException("Unable to read recent session history.", ex);
            return new List<RecentSession>();
        }
    }

    private void PersistRecentSessions()
    {
        try
        {
            var directory = Path.GetDirectoryName(this._storagePath);
            if (String.IsNullOrEmpty(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = File.Create(this._storagePath);
            JsonSerializer.Serialize(stream,
                                     this._recentSessions.Select(PersistedRecentSession.FromRecentSession).ToList(),
                                     SerializerOptions);
        }
        catch (IOException ex)
        {
            this._applicationLogger.LogException("Unable to persist recent session history.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            this._applicationLogger.LogException("Unable to persist recent session history.", ex);
        }
    }

    private sealed class PersistedRecentSession
    {
        public RecentSessionKind Kind { get; set; }

        public string? BinaryPath { get; set; }

        public string? PDBPath { get; set; }

        public SymbolSourcesSupported SymbolSourcesSupported { get; set; }

        public string? BeforeBinaryPath { get; set; }

        public string? BeforePdbPath { get; set; }

        public string? AfterBinaryPath { get; set; }

        public string? AfterPdbPath { get; set; }

        public DateTimeOffset LastOpenedUtc { get; set; }

        public RecentSession? ToRecentSession() => this.Kind switch
        {
            RecentSessionKind.SingleBinary when this.BinaryPath is not null && this.PDBPath is not null
                => RecentSession.CreateSingle(this.BinaryPath,
                                              this.PDBPath,
                                              new SessionOptions() { SymbolSourcesSupported = this.SymbolSourcesSupported },
                                              this.LastOpenedUtc),
            RecentSessionKind.BinaryDiff when this.BeforeBinaryPath is not null &&
                                              this.BeforePdbPath is not null &&
                                              this.AfterBinaryPath is not null &&
                                              this.AfterPdbPath is not null
                => RecentSession.CreateDiff(this.BeforeBinaryPath,
                                            this.BeforePdbPath,
                                            this.AfterBinaryPath,
                                            this.AfterPdbPath,
                                            this.LastOpenedUtc),
            _ => null
        };

        public static PersistedRecentSession FromRecentSession(RecentSession recentSession)
        {
            ArgumentNullException.ThrowIfNull(recentSession);

            return new PersistedRecentSession()
            {
                Kind = recentSession.Kind,
                BinaryPath = recentSession.BinaryPath,
                PDBPath = recentSession.PDBPath,
                SymbolSourcesSupported = recentSession.SymbolSourcesSupported,
                BeforeBinaryPath = recentSession.BeforeBinaryPath,
                BeforePdbPath = recentSession.BeforePdbPath,
                AfterBinaryPath = recentSession.AfterBinaryPath,
                AfterPdbPath = recentSession.AfterPdbPath,
                LastOpenedUtc = recentSession.LastOpenedUtc
            };
        }
    }
}
