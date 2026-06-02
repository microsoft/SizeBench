using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace SizeBench.ErrorReporting.ErrorInfoProviders;

public sealed class ProcessInfoProvider : KeyValueInfoProvider
{
    public ProcessInfoProvider() : base("Process Information")
    {
    }

    public override Dictionary<string, string> GetEntries()
    {
        var appVersion = String.Empty;
        var appHash = String.Empty;
        var entryAssembly = Assembly.GetEntryAssembly();
        if (null != entryAssembly)
        {
            var informationalVersionAttributes = (AssemblyInformationalVersionAttribute[])entryAssembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
            if (informationalVersionAttributes.Length > 0)
            {
                var informationalVersionParts = informationalVersionAttributes[0].InformationalVersion.Split('+');
                if (informationalVersionParts.Length > 0)
                {
                    appVersion = informationalVersionParts[0];
                    if (informationalVersionParts.Length > 1)
                    {
                        appHash = informationalVersionParts[1];
                    }
                }
            }
        }
        string processName;
        using (var currentProcess = Process.GetCurrentProcess())
        {
            processName = currentProcess.ProcessName;
        }

        var entries = new Dictionary<string, string>(StringComparer.Ordinal) {
                { "Process Name", processName },
                { "Application Version", appVersion },
                { "Application Hash", appHash },
                { "Working Set", Environment.WorkingSet.ToString("##,#", CultureInfo.InvariantCulture.NumberFormat) },
                { "Command Line", Environment.CommandLine },
                { "64-bit Process", Environment.Is64BitProcess.ToString(CultureInfo.InvariantCulture) },
            };

        return entries;
    }
}
