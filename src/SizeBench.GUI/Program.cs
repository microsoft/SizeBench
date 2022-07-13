using System.Diagnostics.CodeAnalysis;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Castle.Windsor.Installer;
using SizeBench.Logging;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace SizeBench.GUI;

[ExcludeFromCodeCoverage] // This isn't really testable
public static class Program
{
    internal static Uri? Deeplink { get; private set; }
    private static readonly WindsorContainer _windsorContainer = new WindsorContainer();
    private static IApplicationLogger? _logger;


    [STAThread]
    public static void Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!UriParser.IsKnownScheme("sizebench"))
        {
            UriParser.Register(new GenericUriParser(GenericUriParserOptions.AllowEmptyAuthority |
                                                    GenericUriParserOptions.NoUserInfo |
                                                    GenericUriParserOptions.NoPort |
                                                    GenericUriParserOptions.DontConvertPathBackslashes |
                                                    GenericUriParserOptions.DontCompressPath), "sizebench", -1);
        }

        _windsorContainer.Install(FromAssembly.InDirectory(new AssemblyFilter(".", "SizeBench.*")));
        _logger = _windsorContainer.Resolve<IApplicationLogger>();

        using (var startupInfoLogs = _logger.StartTaskLog("Launch info"))
        {

            foreach (var arg in args)
            {
                startupInfoLogs.Log($"Command-line arg: {arg}");
            }

            try
            {
                var activatedEventArgs = AppInstance.GetActivatedEventArgs();
                if (activatedEventArgs != null)
                {
                    startupInfoLogs.Log($"Activation Kind: {activatedEventArgs.Kind}");

                    if (activatedEventArgs.Kind == ActivationKind.Protocol)
                    {
                        Deeplink = ((ProtocolActivatedEventArgs)activatedEventArgs).Uri;
                        startupInfoLogs.Log($"Deeplink: {Deeplink}");
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types - if the deeplink is malformed or we're debugging unpackaged in VS this and we fail to parse it, just launch anyway
            catch (Exception) { }
#pragma warning restore CA1031 // Do not catch general exception types

            // If we didn't find one from protocol activation, we can check the command-line parameters to see if any of those looks like a deeplink Uri.
            if (Deeplink is null)
            {
                try
                {
                    foreach (var param in args)
                    {
                        if (param.Length > 0 && param.StartsWith("sizebench:", StringComparison.OrdinalIgnoreCase))
                        {
                            Deeplink = new Uri(param, UriKind.Absolute);
                            startupInfoLogs.Log($"Deeplink from command-line: {Deeplink}");
                            break;
                        }
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types - if the deeplink is malformed and we fail to parse it, just launch anyway
                catch (Exception) { }
#pragma warning restore CA1031 // Do not catch general exception types
            }
        }

        using var app = _windsorContainer.Resolve<App>();
        app.Run();
    }
}
