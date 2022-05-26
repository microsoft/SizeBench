using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.DIAInterop;

namespace SizeBench.TestDataCommon;

internal static class CommonCommandLines
{
    public static CommandLine NullCommandLine { get; } = CommandLine.FromLanguageAndToolName(CompilandLanguage.Unknown, "unknown.exe", new Version(0, 0), new Version(0, 0), String.Empty);
}
