using System.Text;
using SizeBench.AnalysisEngine.DIAInterop;

namespace SizeBench.AnalysisEngine;

internal enum CommandLineSwitchState
{
    SwitchNotFound,
    SwitchEnabled,
    SwitchDisabled
}

internal enum CommandLineOrderOfPrecedence
{
    FirstWins,
    LastWins
}

// Liberally inspired by the command-line parsing code from BinSkim, found here:
// https://github.com/microsoft/binskim/blob/main/src/BinaryParsers/PEBinary/ProgramDatabase/CompilerCommandLine.cs
//
// In the future it may make sense to have derived classes for the LINK.exe command-line, LLD-LINK, MSVC, Clang, etc. - that way
// we can abstract out concepts like "Is RTTI enabled" which have different switch names for different compilers.
// For now, this is enough to get started.
internal class CommandLine
{
    private static class ArgumentSplitter
    {
        private enum WhitespaceMode
        {
            Ignore,
            PartOfArgument,
            EndArgument
        }

        /// <summary>
        /// Mimics CommandLineToArgvW's argument splitting behavior, plus bug fixes.
        /// </summary>
        /// <param name="input">The command line to split into arguments.</param>
        /// <returns>The values of the arguments supplied in the input.</returns>
        public static List<string> CommandLineToArgvW(string input)
        {
            // This function mimics CommandLineToArgvW's escaping behavior, documented here:
            // http://msdn.microsoft.com/en-us/library/windows/desktop/bb776391.aspx

            //
            // We used to P/Invoke to the real CommandLineToArgvW, but re-implement it here
            // as a workaround for the following:
            // 
            // * CommandLineToArgvW does not treat newlines as whitespace (twcsec-tfs01 bug # 17291)
            // * CommandLineToArgvW returns the executable name for the empty string, not the empty set
            // * CommandLineToArgvW chokes on leading whitespace (twcsec-tfs01 bug# 17378)
            //
            // and as a result of the above we expect to find more nasty edge cases in the future.
            //

            ArgumentNullException.ThrowIfNull(input);

            var whitespaceMode = WhitespaceMode.Ignore;
            var slashCount = 0;

            var result = new List<string>();
            var sb = new StringBuilder();

            foreach (var c in input)
            {
                if (whitespaceMode == WhitespaceMode.Ignore && Char.IsWhiteSpace(c))
                {
                    // Purposely do nothing
                }
                else if (whitespaceMode == WhitespaceMode.EndArgument && Char.IsWhiteSpace(c))
                {
                    AddSlashes(sb, ref slashCount);
                    EmitArgument(result, sb);
                    whitespaceMode = WhitespaceMode.Ignore;
                }
                else if (c == '\\')
                {
                    ++slashCount;
                    if (whitespaceMode == WhitespaceMode.Ignore)
                    {
                        whitespaceMode = WhitespaceMode.EndArgument;
                    }
                }
                else if (c == '\"')
                {
                    var quoteIsEscaped = (slashCount & 1) == 1;
                    slashCount >>= 1; // Using >> to avoid C# bankers rounding
                                      // 2n backslashes followed by a quotation mark produce n slashes followed by a quotation mark
                    AddSlashes(sb, ref slashCount);

                    if (quoteIsEscaped)
                    {
                        sb.Append(c);
                    }
                    else if (whitespaceMode == WhitespaceMode.PartOfArgument)
                    {
                        whitespaceMode = WhitespaceMode.EndArgument;
                    }
                    else
                    {
                        whitespaceMode = WhitespaceMode.PartOfArgument;
                    }
                }
                else
                {
                    AddSlashes(sb, ref slashCount);
                    sb.Append(c);
                    if (whitespaceMode == WhitespaceMode.Ignore)
                    {
                        whitespaceMode = WhitespaceMode.EndArgument;
                    }
                }
            }

            AddSlashes(sb, ref slashCount);
            if (sb.Length != 0)
            {
                EmitArgument(result, sb);
            }

            return result;
        }

        private static void EmitArgument(List<string> result, StringBuilder sb)
        {
            result.Add(sb.ToString());
            sb.Clear();
        }

        private static void AddSlashes(StringBuilder sb, ref int slashCount)
        {
            sb.Append('\\', slashCount);
            slashCount = 0;
        }
    }

    protected static readonly char[] switchPrefix = new char[] { '-', '/' };

    public string Raw { get; }

    private List<string>? _splitArgs;
    internal List<string> SplitArguments
    {
        get
        {
            this._splitArgs ??= ArgumentSplitter.CommandLineToArgvW(this.Raw);

            return this._splitArgs;
        }
    }

    internal CompilandLanguage Language { get; }

    internal string ToolName { get; }

    internal Version FrontEndVersion { get; }
    internal Version BackEndVersion { get; }

    protected CommandLine(string rawCommandLine, CompilandLanguage language, string toolName, Version frontEndVersion, Version backEndVersion)
    {
        this.Raw = rawCommandLine;
        this.Language = language;
        this.ToolName = toolName;
        this.FrontEndVersion = frontEndVersion;
        this.BackEndVersion = backEndVersion;
    }

    internal static CommandLine FromLanguageAndToolName(CompilandLanguage language, string toolName, Version frontEndVersion, Version backEndVersion, string rawCommandLine)
    {
        return language switch
        {
            CompilandLanguage.CV_CFL_CXX => CompilerCommandLine.FromLanguageAndCompilerName(language, toolName, frontEndVersion, backEndVersion, rawCommandLine),
            CompilandLanguage.CV_CFL_LINK => LinkerCommandLine.FromLanguageAndLinkerName(language, toolName, frontEndVersion, backEndVersion, rawCommandLine),
            _ => new CommandLine(rawCommandLine, language, toolName, frontEndVersion, backEndVersion),
        };
    }

    protected static bool IsCommandLineOption(string candidate)
    {
        if (candidate.Length < 2)
        {
            return false;
        }

        var c = candidate[0];
        return c is '/' or '-';
    }

    internal CommandLineSwitchState GetSwitchState(string[] switchNames, CommandLineSwitchState defaultState, CommandLineOrderOfPrecedence precedence, StringComparison stringComparison = StringComparison.Ordinal)
    {
        // TODO-paddymcd-MSFT - This is an OK first pass.
        // Unfortunately composite switches get tricky and not all switches support the '-' semantics 
        // e.g. /O1- gets translated to /O1 /O-, the second of which is not supported.
        // Additionally, currently /d2guardspecload is translated into /guardspecload, which may be a bug for ENC
        var namedswitchesState = CommandLineSwitchState.SwitchNotFound;

        if (switchNames != null && switchNames.Length > 0)
        {
            // array of strings for the switch name without the preceding switchPrefix to make comparison easier
            var switchArray = new string[switchNames.Length];

            for (var index = 0; index < switchNames.Length; index++)
            {
                // if present remove the slash or minus
                switchArray[index] = switchNames[index].TrimStart(switchPrefix);
            }

            foreach (var arg in this.SplitArguments)
            {
                if (IsCommandLineOption(arg))
                {
                    var realArg = arg.TrimStart(switchPrefix);

                    // Check if this matches one of the names switches
                    for (var index = 0; index < switchArray.Length; index++)
                    {
                        if (realArg.StartsWith(switchArray[index], stringComparison))
                        {
                            // partial stem match - now check if this is a full match or a match with a "-" on the end
                            if (realArg.Equals(switchArray[index], stringComparison))
                            {
                                namedswitchesState = CommandLineSwitchState.SwitchEnabled;
                            }
                            else if (realArg[switchArray[index].Length] == '-')
                            {
                                namedswitchesState = CommandLineSwitchState.SwitchDisabled;
                            }
                            // Else we have a stem match - do nothing
                        }
                    }

                    if (namedswitchesState != CommandLineSwitchState.SwitchNotFound &&
                        precedence == CommandLineOrderOfPrecedence.FirstWins)
                    {
                        // we found a switch that impacts the desired state and FirstWins is set
                        break;
                    }
                }
            }

            if (namedswitchesState == CommandLineSwitchState.SwitchNotFound)
            {
                namedswitchesState = defaultState;
            }
        }

        return namedswitchesState;
    }
}

internal class CompilerCommandLine : CommandLine
{
    internal virtual bool RTTIEnabled { get; }

    protected CompilerCommandLine(string rawCommandLine, CompilandLanguage language, string toolName, Version frontEndVersion, Version backEndVersion)
        : base(rawCommandLine, language, toolName, frontEndVersion, backEndVersion) { }

    internal static CompilerCommandLine FromLanguageAndCompilerName(CompilandLanguage language, string toolName, Version frontEndVersion, Version backEndVersion, string rawCommandLine)
    {
        if (language == CompilandLanguage.CV_CFL_CXX && toolName == "Microsoft (R) Optimizing Compiler")
        {
            return new MSVC_CXX_CommandLine(rawCommandLine, language, toolName, frontEndVersion, backEndVersion);
        }
        else
        {
            return new CompilerCommandLine(rawCommandLine, language, toolName, frontEndVersion, backEndVersion);
        }
    }
}

internal sealed class MSVC_CXX_CommandLine : CompilerCommandLine
{
    // RTTI is on by default for C++ code, and the last copy of the switch is the final override.
    internal override bool RTTIEnabled => GetSwitchState(new string[] { "GR" }, CommandLineSwitchState.SwitchEnabled, CommandLineOrderOfPrecedence.LastWins) == CommandLineSwitchState.SwitchEnabled;

    public MSVC_CXX_CommandLine(string rawCommandLine, CompilandLanguage language, string toolName, Version frontEndVersion, Version backEndVersion)
        : base(rawCommandLine, language, toolName, frontEndVersion, backEndVersion) { }
}

internal class LinkerCommandLine : CommandLine
{
    internal virtual bool IncrementallyLinked { get; }
    internal virtual bool IsPGInstrumented { get; }

    internal LinkerCommandLine(string rawCommandLine, CompilandLanguage language, string toolName, Version frontEndVersion, Version backEndVersion)
        : base(rawCommandLine, language, toolName, frontEndVersion, backEndVersion) { }

    internal static LinkerCommandLine FromLanguageAndLinkerName(CompilandLanguage language, string toolName, Version frontEndVersion, Version backEndVersion, string rawCommandLine)
    {
        if (language == CompilandLanguage.CV_CFL_LINK && toolName == "Microsoft (R) LINK")
        {
            return new MSVC_LINK_CommandLine(rawCommandLine, language, toolName, frontEndVersion, backEndVersion);
        }
        else
        {
            return new LinkerCommandLine(rawCommandLine, language, toolName, frontEndVersion, backEndVersion);
        }
    }
}

internal sealed class MSVC_LINK_CommandLine : LinkerCommandLine
{
    private enum LTCGStatus
    {
        LTCG,
        LTCGIncremental
    }

    // See very similar code in BinSkim here: https://github.com/microsoft/binskim/pull/667/files#diff-355f6f7e5a5a3381ffa2c1d2bd33a41003ce1b09f036c818b913f64f8d235da1
    internal override bool IncrementallyLinked
    {
        get
        {
            var debugSet = false;
            var optRef = false;
            var optIcf = false;
            var optLbr = false;
            var order = false;
            var explicitlyEnabled = false;
            var explicitlyDisabled = false;
            var ltcg = false;
            bool? ltcgIncremental = false;
            var winmdOnly = false;
            var guardXfg = false;
            var profile = false;
            var stub = false;
            var force = false;
            var release = false;
            var clrImageType = false;

            foreach (var argumentWithPrefix in this.SplitArguments)
            {
                if (IsCommandLineOption(argumentWithPrefix))
                {
                    var argument = argumentWithPrefix.TrimStart(switchPrefix);

                    // There are multiple /debug options so use StartsWith
                    // Also according to MSDN, "/DEBUG" implies incremental link unless /INCREMENTAL:NO overrides it later, unless
                    // it is debug:none
                    if (argument.StartsWith("debug", StringComparison.OrdinalIgnoreCase))
                    {
                        debugSet = !argument.Contains(":none", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (argument.StartsWith("opt:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Assume that if specified multiple times the last wins.  It is possible to specify arguments like
                        // "/opt:icf <other args> /opt:ref,noicf" and end up with REF on and ICF off.
                        if (argument.Contains("noref", StringComparison.OrdinalIgnoreCase))
                        {
                            optRef = false;
                        }
                        else if (argument.Contains("ref", StringComparison.OrdinalIgnoreCase))
                        {
                            optRef = true;
                        }

                        if (argument.Contains("noicf", StringComparison.OrdinalIgnoreCase))
                        {
                            optIcf = false;
                        }
                        else if (argument.Contains("icf", StringComparison.OrdinalIgnoreCase))
                        {
                            optIcf = true;
                        }

                        if (argument.Contains("nolbr", StringComparison.OrdinalIgnoreCase))
                        {
                            optLbr = false;
                        }
                        else if (argument.Contains("lbr", StringComparison.OrdinalIgnoreCase))
                        {
                            optLbr = true;
                        }
                    }
                    else if (argument.StartsWith("order:", StringComparison.OrdinalIgnoreCase))
                    {
                        order = true;
                    }
                    else if (String.Equals(argument, "incremental", StringComparison.OrdinalIgnoreCase) ||
                             String.Equals(argument, "incremental:yes", StringComparison.OrdinalIgnoreCase))
                    {
                        explicitlyEnabled = true;
                        explicitlyDisabled = false; // Assume that if specified multiple times the last wins
                    }
                    else if (String.Equals(argument, "incremental:no", StringComparison.OrdinalIgnoreCase))
                    {
                        explicitlyDisabled = true;
                        explicitlyEnabled = false; // Assume that if specified multiple times the last wins
                    }
                    else if (String.Equals(argument, "ltcg", StringComparison.OrdinalIgnoreCase))
                    {
                        ltcg = true;
                        ltcgIncremental = false;
                    }
                    else if (String.Equals(argument, "ltcg:off", StringComparison.OrdinalIgnoreCase))
                    {
                        ltcg = false;
                        ltcgIncremental = false;
                    }
                    else if (String.Equals(argument, "ltcg:incremental", StringComparison.OrdinalIgnoreCase))
                    {
                        ltcgIncremental = true;
                    }
                    else if (argument.StartsWith("guard:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (argument.Contains("noxfg", StringComparison.OrdinalIgnoreCase))
                        {
                            guardXfg = false;
                        }
                        else if (argument.Contains("xfg", StringComparison.OrdinalIgnoreCase))
                        {
                            guardXfg = true;
                        }
                    }
                    else if (String.Equals(argument, "winmd:only", StringComparison.OrdinalIgnoreCase))
                    {
                        winmdOnly = true;
                    }
                    else if (String.Equals(argument, "profile", StringComparison.OrdinalIgnoreCase))
                    {
                        profile = true;
                    }
                    else if (argument.StartsWith("stub:", StringComparison.OrdinalIgnoreCase))
                    {
                        stub = true;
                    }
                    else if (String.Equals(argument, "force", StringComparison.OrdinalIgnoreCase))
                    {
                        force = true;
                    }
                    else if (String.Equals(argument, "release", StringComparison.OrdinalIgnoreCase))
                    {
                        release = true;
                    }
                    else if (argument.StartsWith("clrimagetype:", StringComparison.OrdinalIgnoreCase))
                    {
                        clrImageType = true;
                    }
                }
            }

            if (!ltcgIncremental.HasValue && debugSet)
            {
                ltcgIncremental = true;
            }

            // If any of these flags are set, they explicitly disable incremental linking even if /incremental was also explicitly specified (with a linker warning)
            if (winmdOnly || guardXfg || profile || stub || force || optIcf || optRef || optLbr || order || release || clrImageType)
            {
                return false;
            }
            // If /debug is set then incremental is implied unless certain other flags convert it back to false
            // If nothing is specified then it is disabled.
            else if (explicitlyEnabled)
            {
                return true;
            }
            else if (explicitlyDisabled)
            {
                return false;
            }
            else if (debugSet)
            {
                return (!ltcg || ltcgIncremental.Value);
            }
            else
            {
                return ltcgIncremental.Value;
            }
        }
    }

    internal override bool IsPGInstrumented =>
        GetSwitchState(new string[] { "/ltcg:pgi", "/genprofile", "/fastgenprofile" }, CommandLineSwitchState.SwitchNotFound, CommandLineOrderOfPrecedence.FirstWins, StringComparison.OrdinalIgnoreCase) == CommandLineSwitchState.SwitchEnabled;

    public MSVC_LINK_CommandLine(string rawCommandLine, CompilandLanguage language, string toolName, Version frontEndVersion, Version backEndVersion)
        : base(rawCommandLine, language, toolName, frontEndVersion, backEndVersion) { }
}
