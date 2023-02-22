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
    public void IncrementalLinkingDetectionWorks()
    {
        var noLTCGSpecified = MSVCLinkerFromArgs(" / ERRORREPORT:QUEUE");
        Assert.IsFalse(noLTCGSpecified.IncrementallyLinked);

        // /DEBUG implies LTCGIncremental
        var DebugCountsAsIncremental = MSVCLinkerFromArgs("/DEBUG");
        Assert.IsTrue(DebugCountsAsIncremental.IncrementallyLinked);

        // /DEBUG still gets overridden by /incremental:no
        var DebugGetsOverriddenByIncrementalNo = MSVCLinkerFromArgs("/incremental:no /debug");
        Assert.IsFalse(DebugGetsOverriddenByIncrementalNo.IncrementallyLinked);

        var plainIncrementalCountsAsIncremental = MSVCLinkerFromArgs("/ERRORREPORT:QUEUE /PDB:\"foo.pdb\" /ltcg:INCREMENTAL");
        Assert.IsTrue(plainIncrementalCountsAsIncremental.IncrementallyLinked);

        var ltcgOverridesLtcgIncrementalIfLast = MSVCLinkerFromArgs("/ERRORREPORT:QUEUE /PDB:\"foo.pdb\" /ltcg:INCREMENTAL /DEBUG /LTcG");
        Assert.IsFalse(ltcgOverridesLtcgIncrementalIfLast.IncrementallyLinked);

        var ltcgIncrementalOverridesIfLast = MSVCLinkerFromArgs("/ERRORREPORT:QUEUE /PDB:\"foo.pdb\" /ltcg:INCREMENTAL /DEBUG /LTcG /debugType:pdata /ltcg:InCREMEntal");
        Assert.IsTrue(ltcgIncrementalOverridesIfLast.IncrementallyLinked);

        var incrementalNoOverridesAll = MSVCLinkerFromArgs("/ERRORREPORT:QUEUE /PDB:\"foo.pdb\" /ltcg:INCREMENTAL /DEBUG /LTcG /debugType:pdata /ltcg:InCREMEntal /incremental:NO");
        Assert.IsFalse(incrementalNoOverridesAll.IncrementallyLinked);

        // /debug:none overrides /debug
        var debugNone = MSVCLinkerFromArgs("/debug /deBUG:NONE");
        Assert.IsFalse(debugNone.IncrementallyLinked);

        // Between opt:noX and opt:X, last one wins
        var optRef = MSVCLinkerFromArgs("/debug /opt:ref");
        Assert.IsFalse(optRef.IncrementallyLinked);
        var optNoRefOverrides = MSVCLinkerFromArgs("/debug /opt:ref /opt:noicf,noref");
        Assert.IsTrue(optNoRefOverrides.IncrementallyLinked);
        var optRefOverridesFurther = MSVCLinkerFromArgs("/debug /opt:ref -opt:noref /opt:ref");
        Assert.IsFalse(optRefOverridesFurther.IncrementallyLinked);

        var optIcf = MSVCLinkerFromArgs("/debug /opt:ICF");
        Assert.IsFalse(optIcf.IncrementallyLinked);
        var optNoIcfOverrides = MSVCLinkerFromArgs("/debug /opt:icf /opt:noref,NOicf");
        Assert.IsTrue(optNoIcfOverrides.IncrementallyLinked);
        var optIcfOverridesFurther = MSVCLinkerFromArgs("/debug /opt:icf /opt:noicf -opt:ICF");
        Assert.IsFalse(optIcfOverridesFurther.IncrementallyLinked);

        var optLbr = MSVCLinkerFromArgs("/debug /opt:lbR");
        Assert.IsFalse(optLbr.IncrementallyLinked);
        var optNoLbrOverrides = MSVCLinkerFromArgs("/debug /opt:lbr /opt:noicf,nolbr");
        Assert.IsTrue(optNoLbrOverrides.IncrementallyLinked);
        var optLbrOverridesFurther = MSVCLinkerFromArgs("/debug -opt:LBR /opt:nolbr /opt:lbr");
        Assert.IsFalse(optLbrOverridesFurther.IncrementallyLinked);

        // order:, stub:, force, profile, release, winmd:only and the various clrimagetype flags all disable incremental linking, even if specified early (ordering does not matter)
        var order = MSVCLinkerFromArgs("/orDER:@foo.ord /incremental");
        Assert.IsFalse(order.IncrementallyLinked);
        var stub = MSVCLinkerFromArgs("/stub:stub.exe /incremental");
        Assert.IsFalse(stub.IncrementallyLinked);
        var force = MSVCLinkerFromArgs("/FORCE /INCREMENTAL");
        Assert.IsFalse(force.IncrementallyLinked);
        var profile = MSVCLinkerFromArgs("-PROFILE /INCREMENTAL");
        Assert.IsFalse(profile.IncrementallyLinked);
        var release = MSVCLinkerFromArgs("/release /incremental");
        Assert.IsFalse(release.IncrementallyLinked);
        var winmdOnly = MSVCLinkerFromArgs("/winmd:only /debug /incremental");
        Assert.IsFalse(winmdOnly.IncrementallyLinked);
        var clrimageType = MSVCLinkerFromArgs("/clrimagetype:safe /incremental /debug");
        Assert.IsFalse(clrimageType.IncrementallyLinked);
        clrimageType = MSVCLinkerFromArgs("/clrimagetype:SAFE32BITPREFERRED /debug /incremental");
        Assert.IsFalse(clrimageType.IncrementallyLinked);
        clrimageType = MSVCLinkerFromArgs("-clrimagetype:PUre /debug /incremental");
        Assert.IsFalse(clrimageType.IncrementallyLinked);
    }

    private static LinkerCommandLine MSVCLinkerFromArgs(string args)
        => (LinkerCommandLine)CommandLine.FromLanguageAndToolName(CompilandLanguage.CV_CFL_LINK, "Microsoft (R) LINK", new Version(19, 28), new Version(19, 28), args);
}
