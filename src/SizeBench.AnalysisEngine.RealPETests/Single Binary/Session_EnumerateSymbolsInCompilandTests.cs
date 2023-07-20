using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[TestClass]
public sealed class Session_EnumerateSymbolsInCompilandTests
{
    public TestContext? TestContext { get; set; }
    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");

    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    [TestMethod]
    public async Task WideAndANSIStringSymbolsCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var compilands = await session.EnumerateCompilands(this.CancellationToken);
        Assert.IsNotNull(compilands);

        var sourceFile1Compiland = compilands.Single(c => c.Name.Contains("SourceFile1.obj", StringComparison.Ordinal));

        var symbolsInSourceFile1 = await session.EnumerateSymbolsInCompiland(sourceFile1Compiland, this.CancellationToken);
        Assert.IsNotNull(symbolsInSourceFile1);

        var stringSymbolsInSourceFile1 = symbolsInSourceFile1.Where(sym => sym is StringSymbol).Cast<StringSymbol>().ToList();

        // Verify that we can parse a simple ANSI string
        var simpleAnsiString = stringSymbolsInSourceFile1.Single(s => s.Name.Contains("`string'", StringComparison.Ordinal) &&
                                                                      s.Name.Contains("\"dummy print from source file 1, duplicatedPoint.x=%d, &duplicatedPointArray=%x\"", StringComparison.Ordinal));
        Assert.AreEqual(StringSymbolType.ANSI, simpleAnsiString.StringType);
        Assert.AreEqual("`string': \"dummy print from source file 1, duplicatedPoint.x=%d, &duplicatedPointArray=%x\"", simpleAnsiString.Name);
        Assert.AreEqual(simpleAnsiString.Name, simpleAnsiString.CanonicalName);
        Assert.AreEqual("dummy print from source file 1, duplicatedPoint.x=%d, &duplicatedPointArray=%x", simpleAnsiString.StringData);

        // Verify that we can parse a Unicode string
        var simpleUnicodeString = stringSymbolsInSourceFile1.Single(s => s.Name.Contains("`string'", StringComparison.Ordinal) &&
                                                                         s.Name.Contains("L\"a wide string in SourceFile1\"", StringComparison.Ordinal));
        Assert.AreEqual(StringSymbolType.Unicode, simpleUnicodeString.StringType);
        Assert.AreEqual("`string': L\"a wide string in SourceFile1\"", simpleUnicodeString.Name);
        Assert.AreEqual(simpleUnicodeString.Name, simpleUnicodeString.CanonicalName);
        Assert.AreEqual("a wide string in SourceFile1", simpleUnicodeString.StringData);

        // Verify that we can parse an ANSI string with an embedded horizontal tab
        var ansiWithEmbeddedHorizontalTab = stringSymbolsInSourceFile1.Single(s => s.Name.Contains("`string'", StringComparison.Ordinal) &&
                                                                                   s.Name.Contains("\"an ANSI string with an\\tembedded horizontal tab\"", StringComparison.Ordinal));
        Assert.AreEqual(StringSymbolType.ANSI, ansiWithEmbeddedHorizontalTab.StringType);
        Assert.AreEqual("`string': \"an ANSI string with an\\tembedded horizontal tab\"", ansiWithEmbeddedHorizontalTab.Name);
        Assert.AreEqual(ansiWithEmbeddedHorizontalTab.Name, ansiWithEmbeddedHorizontalTab.CanonicalName);
        Assert.AreEqual(@"an ANSI string with an\tembedded horizontal tab", ansiWithEmbeddedHorizontalTab.StringData);

        // Verify that we can parse a Unicode string with an embeddeded horizontal tab
        var unicodeWithEmbeddedHorizontalTab = stringSymbolsInSourceFile1.Single(s => s.Name.Contains("`string'", StringComparison.Ordinal) &&
                                                                                      s.Name.Contains("L\"a wide string with an\\tembedded horizontal tab\"", StringComparison.Ordinal));
        Assert.AreEqual(StringSymbolType.Unicode, unicodeWithEmbeddedHorizontalTab.StringType);
        Assert.AreEqual("`string': L\"a wide string with an\\tembedded horizontal tab\"", unicodeWithEmbeddedHorizontalTab.Name);
        Assert.AreEqual(unicodeWithEmbeddedHorizontalTab.Name, unicodeWithEmbeddedHorizontalTab.CanonicalName);
        Assert.AreEqual(@"a wide string with an\tembedded horizontal tab", unicodeWithEmbeddedHorizontalTab.StringData);

        // Verify that we can parse a Unicode string with an embeddeded vertical tab and another weird Unicode character (a star)
        var unicodeWithEmbeddedWeirdCharacters = stringSymbolsInSourceFile1.Single(s => s.Name.Contains("`string'", StringComparison.Ordinal) &&
                                                                                        s.Name.Contains("L\"a wide string with embedded vertical tab: \x2B7F, and a fancy star: \x2B51\"", StringComparison.Ordinal));
        Assert.AreEqual(StringSymbolType.Unicode, unicodeWithEmbeddedWeirdCharacters.StringType);
        Assert.AreEqual("`string': L\"a wide string with embedded vertical tab: \x2B7F, and a fancy star: \x2B51\"", unicodeWithEmbeddedWeirdCharacters.Name);
        Assert.AreEqual(unicodeWithEmbeddedWeirdCharacters.Name, unicodeWithEmbeddedWeirdCharacters.CanonicalName);
        Assert.AreEqual("a wide string with embedded vertical tab: \x2B7F, and a fancy star: \x2B51", unicodeWithEmbeddedWeirdCharacters.StringData);
    }

    [TestMethod]
    public async Task VirtualAndStaticAreCorrectlyIdentifiedForFunctionsEnumeratedFromCompiland()
    {
        // When we find symbols from functions in "TestIsStaticOrLocal" - we need to check that we detect their virtual/static-ness correctly since
        // the way DIA deals with 'static' and 'virtual' is complicated and based on how the symbols are enumerated - so this test makes sure we
        // correctly abstract that for consumers of SizeBench.
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var compilands = await session.EnumerateCompilands(this.CancellationToken);
        Assert.IsNotNull(compilands);

        var dllMainCompiland = compilands.Single(c => c.Name.Contains(@"\dllmain.obj", StringComparison.Ordinal));

        var symbolsInDllMainObj = await session.EnumerateSymbolsInCompiland(dllMainCompiland, this.CancellationToken);

        // This is essentially a regression test for this former DIA bug: https://developercommunity.visualstudio.com/t/types-declared-inside-functions-dont-report-idiasy/996975
        var virtualNonStaticFunctionInLocalTypeFromCompiland = symbolsInDllMainObj.Where(s => s is IFunctionCodeSymbol)
                                                                                  .Single(s => s.Name.Contains("TestIsStaticOrLocal::virtualNonStatic", StringComparison.Ordinal)) as IFunctionCodeSymbol;

        Assert.IsNotNull(virtualNonStaticFunctionInLocalTypeFromCompiland);
        Assert.IsTrue(virtualNonStaticFunctionInLocalTypeFromCompiland.IsVirtual);
        Assert.IsTrue(virtualNonStaticFunctionInLocalTypeFromCompiland.IsIntroVirtual);
        Assert.IsFalse(virtualNonStaticFunctionInLocalTypeFromCompiland.IsStatic);

        var staticFunctionInLocalTypeFromCompiland = symbolsInDllMainObj.Where(s => s is IFunctionCodeSymbol)
                                                                        .Single(s => s.Name.Contains("TestIsStaticOrLocal::staticFunction", StringComparison.Ordinal)) as IFunctionCodeSymbol;

        Assert.IsNotNull(staticFunctionInLocalTypeFromCompiland);
        Assert.IsFalse(staticFunctionInLocalTypeFromCompiland.IsVirtual);
        Assert.IsFalse(staticFunctionInLocalTypeFromCompiland.IsIntroVirtual);
        Assert.IsTrue(staticFunctionInLocalTypeFromCompiland.IsStatic);

        var types = await session.EnumerateAllUserDefinedTypes(this.CancellationToken);
        var testIsLocalOrStaticUDT = types.Single(udt => udt.Name.Contains("TestIsStaticOrLocal", StringComparison.Ordinal));

        var virtualNonStaticFunctionInLocalTypeFromUDT = (await testIsLocalOrStaticUDT.GetFunctionsAsync(this.CancellationToken)).Single(s => s.FunctionName.Contains("virtualNonStatic", StringComparison.Ordinal));
        Assert.IsTrue(virtualNonStaticFunctionInLocalTypeFromUDT.IsVirtual);
        Assert.IsTrue(virtualNonStaticFunctionInLocalTypeFromUDT.IsIntroVirtual);
        Assert.IsFalse(virtualNonStaticFunctionInLocalTypeFromUDT.IsStatic);

        var staticFunctionInLocalTypeFromUDT = (await testIsLocalOrStaticUDT.GetFunctionsAsync(this.CancellationToken)).Single(s => s.FunctionName.Contains("staticFunction", StringComparison.Ordinal));
        Assert.IsFalse(staticFunctionInLocalTypeFromUDT.IsVirtual);
        Assert.IsFalse(staticFunctionInLocalTypeFromUDT.IsIntroVirtual);
        Assert.IsTrue(staticFunctionInLocalTypeFromUDT.IsStatic);
    }
}
