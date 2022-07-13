using SizeBench.AnalysisEngine;
using SizeBench.Logging;

namespace SizeBench.GUI;

public interface ISessionFactory
{
    Task<ISession> CreateSession(string binaryPath, string pdbPath, ILogger logger);
    Task<IDiffSession> CreateDiffSession(string beforeBinaryPath, string beforePdbPath,
                                         string afterBinaryPath, string afterPdbPath,
                                         ILogger logger);
    IEnumerable<ISession> OpenSessions { get; }
    IEnumerable<IDiffSession> OpenDiffSessions { get; }
}

internal sealed class SessionFactory : ISessionFactory
{
    private readonly List<ISession> _openSessions = new List<ISession>();
    private readonly List<IDiffSession> _openDiffSessions = new List<IDiffSession>();

    public async Task<ISession> CreateSession(string binaryPath, string pdbPath, ILogger logger)
    {
        var newSession = await Session.Create(binaryPath, pdbPath, logger);
        this._openSessions.Add(newSession);
        return newSession;
    }

    public async Task<IDiffSession> CreateDiffSession(string beforeBinaryPath, string beforePdbPath,
                                                      string afterBinaryPath, string afterPdbPath,
                                                      ILogger logger)
    {
        var newDiffSession = await DiffSession.Create(beforeBinaryPath, beforePdbPath, afterBinaryPath, afterPdbPath, logger);
        this._openDiffSessions.Add(newDiffSession);
        return newDiffSession;
    }

    public IEnumerable<ISession> OpenSessions => this._openSessions;
    public IEnumerable<IDiffSession> OpenDiffSessions => this._openDiffSessions;
}
