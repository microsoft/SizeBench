using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[TestClass]
public sealed class Session_EnumerateSymbolsInSourceFileTests
{
    public TestContext? TestContext { get; set; }

    private string BinaryPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");

    private string PDBPath => Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

    [Timeout(60 * 1000)] // 1 minute
    [TestMethod]
    public async Task AllVectorSymbolsCanBeFoundFromSourceFile()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var vectorSymbolsDiscoveredBySection = new List<ISymbol>();

        // First we discover all the symbols in every section, we'll need these to see if we truly find all the <vector> symbols from the
        // source file.
        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken);

        foreach (var section in sections)
        {
            var symbolsInThisSection = await session.EnumerateSymbolsInBinarySection(section, this.CancellationToken);
            // We don't want to look at strings, since the test binary contains some strings with "std::vector" in them that aren't from the vector header.
            vectorSymbolsDiscoveredBySection.AddRange(symbolsInThisSection.Where(s => s.Name.Contains("std::vector", StringComparison.Ordinal) && !s.Name.Contains("`string'", StringComparison.Ordinal)));
        }

        // Ensure we found at least one pdata and at least one xdata symbol when enumerating by section, so that below we'll verify we found all those
        // by source file too - pdata and xdata need to be done manually so ensure source file symbol enumeration took that into account.
        Assert.IsTrue(vectorSymbolsDiscoveredBySection.Any(sym => sym.Name.StartsWith("[pdata]", StringComparison.Ordinal)));
        Assert.IsTrue(vectorSymbolsDiscoveredBySection.Any(sym => sym.Name.StartsWith("[cppxdata]", StringComparison.Ordinal)));

        var sourceFiles = await session.EnumerateSourceFiles(this.CancellationToken);
        var vectorSF = sourceFiles.Single(sf => sf.Name.Contains(@"\vector", StringComparison.OrdinalIgnoreCase));

        var symbolsDiscoveredInVectorSourceFile = new List<ISymbol>(await session.EnumerateSymbolsInSourceFile(vectorSF, this.CancellationToken));

        // Check that the lists of symbols are of equal Sum(Size) - that way we know we also didn't discover *extra* symbols in the source file
        Assert.AreEqual(vectorSymbolsDiscoveredBySection.Sum(sym => sym.Size), symbolsDiscoveredInVectorSourceFile.Sum(sym => sym.Size));

        foreach (var symbol in vectorSymbolsDiscoveredBySection)
        {
            Assert.IsTrue(symbolsDiscoveredInVectorSourceFile.Contains(symbol), $"When enumerating symbols by source file, this std::vector symbol could not be found: {symbol.Name}");
        }
    }
}
