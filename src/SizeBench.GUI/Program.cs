using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Web;
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

            // If we haven't gotten one so far, maybe the args list can be composed into one?
            Deeplink ??= ConvertArgumentsToDeepLink(args);
        }

        using var app = _windsorContainer.Resolve<App>();
        app.Run();
    }

    internal static Uri? ConvertArgumentsToDeepLink(string[] args)
    {
        // The command line patterns supported are:
        //
        // sizebench.exe <path to dll>
        // sizebench.exe <path to dll> <path to pdb>
        // sizebench.exe <path to previous dll> <path to dll>
        // sizebench.exe <path to previous dll> <path to previous pdb> <path to dll> <path to pdb>
        //

        var queryBuilder = HttpUtility.ParseQueryString(String.Empty);
        string path;
        
        if (args.Length == 4)
        {
            path = "BinaryDiffOverview";
            queryBuilder.Add("BeforeBinaryPath", args[0]);
            queryBuilder.Add("BeforePDBPath", args[1]);
            queryBuilder.Add("BinaryPath", args[2]);
            queryBuilder.Add("PDBPath", args[3]);
        }
        else if (args.Length == 2)
        {
            var arg1 = args[0];
            var arg2 = args[1];
            if (String.Equals(Path.GetExtension(arg2), ".pdb", StringComparison.OrdinalIgnoreCase))
            {
                path = "SingleBinaryOverview";
                queryBuilder.Add("BinaryPath", arg1);
                queryBuilder.Add("PDBPath", arg2);
            }
            else
            {
                path = "BinaryDiffOverview";
                queryBuilder.Add("BinaryPath", arg1);
                queryBuilder.Add("PDBPath", Path.ChangeExtension(arg1, "pdb"));
                queryBuilder.Add("BeforeBinaryPath", arg2);
                queryBuilder.Add("BeforePDBPath", Path.ChangeExtension(arg1, "pdb"));
            }
        }
        else if (args.Length == 1)
        {
            path = "SingleBinaryOverview";
            queryBuilder.Add("BinaryPath", args[0]);
            queryBuilder.Add("PDBPath", Path.ChangeExtension(args[0], "pdb"));
        }
        else
        {
            return null;
        }

        var builder = new UriBuilder
        {
            Host = "2.0",
            Scheme = "sizebench",
            Path = path,
            Query = queryBuilder.ToString()
        };

        return builder.Uri;
    }
}
