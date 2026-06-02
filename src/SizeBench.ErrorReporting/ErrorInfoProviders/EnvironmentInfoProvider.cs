using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SizeBench.ErrorReporting.ErrorInfoProviders;

public sealed class EnvironmentInfoProvider : KeyValueInfoProvider
{
    public EnvironmentInfoProvider() : base("Environment Information")
    {
    }

    public override Dictionary<string, string> GetEntries()
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Processor Count", Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture.NumberFormat) },
                { "OS Version", Environment.OSVersion.ToString() },
                { "Runtime Version", Environment.Version.ToString() },
                { "Framework Description", RuntimeInformation.FrameworkDescription },
                { "CoreCLR Build", ((AssemblyInformationalVersionAttribute[])typeof(object).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute),false))[0].InformationalVersion.Split('+')[0] },
                { "CoreCLR Hash", ((AssemblyInformationalVersionAttribute[])typeof(object).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false))[0].InformationalVersion.Split('+')[1] },
                { "CoreFX Build", ((AssemblyInformationalVersionAttribute[])typeof(Uri).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute),false))[0].InformationalVersion.Split('+')[0] },
                { "CoreFX Hash", ((AssemblyInformationalVersionAttribute[])typeof(Uri).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute),false))[0].InformationalVersion.Split('+')[1] },
                { "OS Description", RuntimeInformation.OSDescription },
                { "OS Architecture", RuntimeInformation.OSArchitecture.ToString() },
                { "Process Architecture", RuntimeInformation.ProcessArchitecture.ToString() },
                { "Has Shutdown Started", Environment.HasShutdownStarted.ToString(CultureInfo.InvariantCulture) },
                { "Current Directory", Environment.CurrentDirectory },
                { "Locale", CultureInfo.CurrentCulture.ToString() },
                { "UI Locale", CultureInfo.CurrentUICulture.ToString() },
            };
        return entries;
    }
}
