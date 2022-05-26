using SizeBench.AnalysisEngine.DIAInterop;

namespace SizeBench.AnalysisEngine.Tests;

[TestClass]
public sealed class CommandLineTests
{
    private static CommandLine DummyCommandLineFromArguments(string arguments) =>
        CommandLine.FromLanguageAndToolName(CompilandLanguage.Unknown, "Clang", new Version(12, 0), new Version(12, 0), arguments);

    [TestMethod]
    public void ArgumentsSplitCorrectly()
    {
        var commandLine = DummyCommandLineFromArguments("/GR /GR- -GR -DFOO");
        Assert.AreEqual(typeof(CommandLine), commandLine.GetType());
        Assert.AreEqual("/GR /GR- -GR -DFOO", commandLine.Raw);
        Assert.AreEqual(4, commandLine.SplitArguments.Count);
        Assert.AreEqual("/GR", commandLine.SplitArguments[0]);
        Assert.AreEqual("/GR-", commandLine.SplitArguments[1]);
        Assert.AreEqual("-GR", commandLine.SplitArguments[2]);
        Assert.AreEqual("-DFOO", commandLine.SplitArguments[3]);
    }

    [TestMethod]
    public void OrderOfPrecedenceFirstWinsWorks()
    {
        var commandLine = DummyCommandLineFromArguments("-GR /GR- -DFOO");
        Assert.AreEqual(CommandLineSwitchState.SwitchEnabled, commandLine.GetSwitchState(new string[] { "GR" }, CommandLineSwitchState.SwitchEnabled, CommandLineOrderOfPrecedence.FirstWins));
        commandLine = DummyCommandLineFromArguments("/GR- -DFOO");
        Assert.AreEqual(CommandLineSwitchState.SwitchDisabled, commandLine.GetSwitchState(new string[] { "GR" }, CommandLineSwitchState.SwitchNotFound, CommandLineOrderOfPrecedence.FirstWins));

        // Now try it when the option isn't present, do we get the default
        commandLine = DummyCommandLineFromArguments("-DFOO");
        Assert.AreEqual(CommandLineSwitchState.SwitchEnabled, commandLine.GetSwitchState(new string[] { "GR" }, CommandLineSwitchState.SwitchEnabled, CommandLineOrderOfPrecedence.FirstWins));
        commandLine = DummyCommandLineFromArguments("-DFOO");
        Assert.AreEqual(CommandLineSwitchState.SwitchNotFound, commandLine.GetSwitchState(new string[] { "GR" }, CommandLineSwitchState.SwitchNotFound, CommandLineOrderOfPrecedence.FirstWins));

        // Now test alternate names
        commandLine = DummyCommandLineFromArguments("-DFOO /GR- /AltName");
        Assert.AreEqual(CommandLineSwitchState.SwitchDisabled, commandLine.GetSwitchState(new string[] { "GR", "AltName" }, CommandLineSwitchState.SwitchNotFound, CommandLineOrderOfPrecedence.FirstWins));
        commandLine = DummyCommandLineFromArguments("-DFOO /GR- /AltName -GR-");
        Assert.AreEqual(CommandLineSwitchState.SwitchDisabled, commandLine.GetSwitchState(new string[] { "GR", "AltName" }, CommandLineSwitchState.SwitchNotFound, CommandLineOrderOfPrecedence.FirstWins));
    }

    [TestMethod]
    public void OrderOfPrecedenceLastWinsWorks()
    {
        var commandLine = DummyCommandLineFromArguments("/GR /GR- -GR -DFOO");
        Assert.AreEqual(CommandLineSwitchState.SwitchEnabled, commandLine.GetSwitchState(new string[] { "GR" }, CommandLineSwitchState.SwitchEnabled, CommandLineOrderOfPrecedence.LastWins));
        commandLine = DummyCommandLineFromArguments("/GR- -DFOO");
        Assert.AreEqual(CommandLineSwitchState.SwitchDisabled, commandLine.GetSwitchState(new string[] { "GR" }, CommandLineSwitchState.SwitchNotFound, CommandLineOrderOfPrecedence.LastWins));

        // Now try it when the option isn't present, do we get the default
        commandLine = DummyCommandLineFromArguments("-DFOO");
        Assert.AreEqual(CommandLineSwitchState.SwitchEnabled, commandLine.GetSwitchState(new string[] { "GR" }, CommandLineSwitchState.SwitchEnabled, CommandLineOrderOfPrecedence.LastWins));
        commandLine = DummyCommandLineFromArguments("-DFOO");
        Assert.AreEqual(CommandLineSwitchState.SwitchNotFound, commandLine.GetSwitchState(new string[] { "GR" }, CommandLineSwitchState.SwitchNotFound, CommandLineOrderOfPrecedence.LastWins));

        // Now test alternate names
        commandLine = DummyCommandLineFromArguments("-DFOO /GR- /AltName");
        Assert.AreEqual(CommandLineSwitchState.SwitchEnabled, commandLine.GetSwitchState(new string[] { "GR", "AltName" }, CommandLineSwitchState.SwitchNotFound, CommandLineOrderOfPrecedence.LastWins));
        commandLine = DummyCommandLineFromArguments("-DFOO /GR- /AltName -GR-");
        Assert.AreEqual(CommandLineSwitchState.SwitchDisabled, commandLine.GetSwitchState(new string[] { "GR", "AltName" }, CommandLineSwitchState.SwitchNotFound, CommandLineOrderOfPrecedence.LastWins));
    }

    [TestMethod]
    public void FactoryProducesAppropriateCommandLineType()
    {
        var baseCommandLine = CommandLine.FromLanguageAndToolName(CompilandLanguage.Unknown, "unknown.exe", new Version(0, 0), new Version(0, 0), "foo");
        Assert.AreEqual(typeof(CommandLine), baseCommandLine.GetType());

        var cxxCommandLineFromClang = CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_CXX, "clang", new Version(12, 0), new Version(0, 0), "-DFOO");
        Assert.AreEqual(typeof(CompilerCommandLine), cxxCommandLineFromClang.GetType());

        var cxxCommandLineFromMSVC = CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_CXX, "Microsoft (R) Optimizing Compiler", new Version(19, 28), new Version(19, 28), "/DFOO");
        Assert.AreEqual(typeof(MSVC_CXX_CommandLine), cxxCommandLineFromMSVC.GetType());
    }

    [TestMethod]
    public void RTTIEnabledWorks()
    {
        var cxxCommandLineFromClang = (CompilerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_CXX, "clang", new Version(12, 0), new Version(0, 0), "/GR");
        Assert.IsFalse(cxxCommandLineFromClang.RTTIEnabled);

        // When the switch is not specified, this means RTTI is enabled by default
        var cxxCommandLineFromMSVC = (CompilerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_CXX, "Microsoft (R) Optimizing Compiler", new Version(19, 28), new Version(19, 28), "/DFOO");
        Assert.IsTrue(cxxCommandLineFromMSVC.RTTIEnabled);

        cxxCommandLineFromMSVC = (CompilerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_CXX, "Microsoft (R) Optimizing Compiler", new Version(19, 28), new Version(19, 28), "/GR");
        Assert.IsTrue(cxxCommandLineFromMSVC.RTTIEnabled);

        cxxCommandLineFromMSVC = (CompilerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_CXX, "Microsoft (R) Optimizing Compiler", new Version(19, 28), new Version(19, 28), "/GR-");
        Assert.IsFalse(cxxCommandLineFromMSVC.RTTIEnabled);

        // If specified twice, last wins
        cxxCommandLineFromMSVC = (CompilerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_CXX, "Microsoft (R) Optimizing Compiler", new Version(19, 28), new Version(19, 28), "/GR- -GR");
        Assert.IsTrue(cxxCommandLineFromMSVC.RTTIEnabled);

        // An enable can be overridden with a further disable later
        cxxCommandLineFromMSVC = (CompilerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_CXX, "Microsoft (R) Optimizing Compiler", new Version(19, 28), new Version(19, 28), "/GR /DFOO -DBAR -GR-");
        Assert.IsFalse(cxxCommandLineFromMSVC.RTTIEnabled);
    }

    [TestMethod]
    public void LTCGIncrementalDetectionWorks()
    {
        var noLTCGSpecified = (LinkerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_LINK, "Microsoft (R) LINK", new Version(19, 28), new Version(19, 28), " / ERRORREPORT:QUEUE");
        Assert.IsFalse(noLTCGSpecified.LTCGIsIncremental);

        // LTCG is implied by /GL
        var GLCountsAsLTCG = (LinkerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_LINK, "Microsoft (R) LINK", new Version(19, 28), new Version(19, 28), "/ltcg:INCREMENTAL /GL");
        Assert.IsFalse(GLCountsAsLTCG.LTCGIsIncremental);

        // /DEBUG implies LTCGIncremental
        var DebugCountsAsIncremental = (LinkerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_LINK, "Microsoft (R) LINK", new Version(19, 28), new Version(19, 28), "/DEBUG");
        Assert.IsTrue(DebugCountsAsIncremental.LTCGIsIncremental);

        // /DEBUG still gets overridden by /incremental:no
        var DebugGetsOverriddenByIncrementalNo = (LinkerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_LINK, "Microsoft (R) LINK", new Version(19, 28), new Version(19, 28), "/incremental:no /debug");
        Assert.IsFalse(DebugGetsOverriddenByIncrementalNo.LTCGIsIncremental);

        var plainIncrementalCountsAsIncremental = (LinkerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_LINK, "Microsoft (R) LINK", new Version(19, 28), new Version(19, 28), "/ERRORREPORT:QUEUE /PDB:\"foo.pdb\" /ltcg:INCREMENTAL");
        Assert.IsTrue(plainIncrementalCountsAsIncremental.LTCGIsIncremental);

        var ltcgOverridesLtcgIncrementalIfLast = (LinkerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_LINK, "Microsoft (R) LINK", new Version(19, 28), new Version(19, 28), "/ERRORREPORT:QUEUE /PDB:\"foo.pdb\" /ltcg:INCREMENTAL /DEBUG /LTcG");
        Assert.IsFalse(ltcgOverridesLtcgIncrementalIfLast.LTCGIsIncremental);

        var ltcgIncrementalOverridesIfLast = (LinkerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_LINK, "Microsoft (R) LINK", new Version(19, 28), new Version(19, 28), "/ERRORREPORT:QUEUE /PDB:\"foo.pdb\" /ltcg:INCREMENTAL /DEBUG /LTcG /debugType:pdata /ltcg:InCREMEntal");
        Assert.IsTrue(ltcgIncrementalOverridesIfLast.LTCGIsIncremental);

        var incrementalNoOverridesAll = (LinkerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_LINK, "Microsoft (R) LINK", new Version(19, 28), new Version(19, 28), "/ERRORREPORT:QUEUE /PDB:\"foo.pdb\" /ltcg:INCREMENTAL /DEBUG /LTcG /debugType:pdata /ltcg:InCREMEntal /incremental:NO");
        Assert.IsFalse(incrementalNoOverridesAll.LTCGIsIncremental);
    }
}
