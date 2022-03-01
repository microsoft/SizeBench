using System.IO;

namespace SizeBench.Logging;

public interface IApplicationLogger : ILogger
{
    IEnumerable<ILogger> SessionLogs { get; }

    ILogger CreateSessionLog(string sessionName);

    void WriteLog(TextWriter writer);
}
