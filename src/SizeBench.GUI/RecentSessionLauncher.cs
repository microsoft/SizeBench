using System.Diagnostics;

namespace SizeBench.GUI;

internal interface IRecentSessionLauncher
{
    void LaunchRecentSession(RecentSession recentSession);
}

internal sealed class RecentSessionLauncher : IRecentSessionLauncher
{
    public void LaunchRecentSession(RecentSession recentSession)
    {
        ArgumentNullException.ThrowIfNull(recentSession);

        var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to locate the current SizeBench executable.");
        var deeplink = recentSession.ToDeeplinkUri();

        Process.Start(new ProcessStartInfo()
        {
            FileName = exePath,
            Arguments = $"\"{deeplink.AbsoluteUri}\"",
            UseShellExecute = true
        });
    }
}
