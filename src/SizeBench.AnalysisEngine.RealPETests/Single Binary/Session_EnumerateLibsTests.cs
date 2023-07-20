using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppDll.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppDll.pdb")]
[DeploymentItem(@"Test PEs\PEParser.Tests.Dllx64.dll")]
[DeploymentItem(@"Test PEs\PEParser.Tests.Dllx64.pdb")]
[TestClass]
public sealed class Session_EnumerateLibsTests
{
    public TestContext? TestContext { get; set; }
    public CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);

    private string CppDllBinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppDll.dll");

    private string CppDllPDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppDll.pdb");

    private string Dllx64BinaryPath => MakePath("PEParser.Tests.Dllx64.dll");

    private string Dllx64PDBPath => MakePath("PEParser.Tests.Dllx64.pdb");

    [TestMethod]
    public async Task CppDllLibsCanBeEnumerated()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.CppDllBinaryPath, this.CppDllPDBPath, logger);
        var libs = await session.EnumerateLibs(CancellationToken.None);
        Assert.IsNotNull(libs);

        var dllMainLib = (from l in libs where l.Name.Contains("dllmain", StringComparison.Ordinal) select l).FirstOrDefault();
        Assert.IsNotNull(dllMainLib);
    }

    [TestMethod]
    public async Task Dllx64PDataXDataAndRsrcAttributedToCorrectLibAndCompiland()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.Dllx64BinaryPath, this.Dllx64PDBPath, logger);

        var libs = await session.EnumerateLibs(CancellationToken.None);
        Assert.IsNotNull(libs);

        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken);
        var pdataSection = sections.Single(s => s.Name == ".pdata");

        // We want to ensure we attributed every byte of pdata and xdata across the libs - there should be no gaps in our understanding ("no byte left behind")
        var totalLibPDataContributionsFound = libs.Sum(l => l.SectionContributions.ContainsKey(pdataSection) ? l.SectionContributions[pdataSection].Size : 0);
        Assert.AreEqual(pdataSection.VirtualSize, totalLibPDataContributionsFound);

        var lib = libs.Where(l => l.Name.Contains("PEParser.Tests.Dllx64.obj", StringComparison.Ordinal)).First();
        Assert.AreEqual(1, lib.Compilands.Count);

        var compiland = lib.Compilands.Values.First();

        Assert.IsTrue(lib.SectionContributionsByName.ContainsKey(".text"));
        Assert.IsTrue(compiland.SectionContributionsByName.ContainsKey(".text"));
        Assert.IsTrue(lib.SectionContributionsByName.ContainsKey(".rdata"));
        Assert.IsTrue(compiland.SectionContributionsByName.ContainsKey(".rdata"));

        Assert.IsTrue(lib.COFFGroupContributionsByName.ContainsKey(".text$mn"));
        Assert.IsTrue(compiland.COFFGroupContributionsByName.ContainsKey(".text$mn"));
        Assert.IsTrue(lib.COFFGroupContributionsByName.ContainsKey(".xdata"));
        Assert.IsTrue(compiland.COFFGroupContributionsByName.ContainsKey(".xdata"));

        // Both the lib and the compiland should be contributing pdata symbols to the binary, which go through a
        // special codepath because they're not in the PDB so we should be sure to check that pdata is accounted
        // for correctly, and thoroughly.
        Assert.IsTrue(lib.SectionContributionsByName.ContainsKey(".pdata"));
        Assert.IsTrue(compiland.SectionContributionsByName.ContainsKey(".pdata"));
        Assert.IsTrue(lib.COFFGroupContributionsByName.ContainsKey(".pdata"));
        Assert.IsTrue(compiland.COFFGroupContributionsByName.ContainsKey(".pdata"));

        // There are 10 [pdata] symbols in this compiland, each 12 bytes
        Assert.AreEqual(120u, lib.SectionContributionsByName[".pdata"].Size);
        Assert.AreEqual(120u, compiland.SectionContributionsByName[".pdata"].Size);
        Assert.AreEqual(120u, lib.COFFGroupContributionsByName[".pdata"].Size);
        Assert.AreEqual(120u, compiland.COFFGroupContributionsByName[".pdata"].Size);

        var libSymbolsInPData = await session.EnumerateSymbolsInContribution(lib.SectionContributionsByName[".pdata"], CancellationToken.None);
        var compilandSymbolsInPData = await session.EnumerateSymbolsInContribution(compiland.SectionContributionsByName[".pdata"], CancellationToken.None);

        Assert.AreEqual(10, libSymbolsInPData.Count);
        Assert.AreEqual(10, compilandSymbolsInPData.Count);

        // Let's find all 10 symbols - yes this is really tedious, but this parser was a real pain to get right, so being
        // thorough in the validation is super-important.
        Assert.IsNotNull(libSymbolsInPData.Where(s => s.Name == "[pdata] Dllx64_CppxdataUsage::MaybeThrow()").FirstOrDefault());
        Assert.IsNotNull(libSymbolsInPData.Where(s => s.Name == "[pdata] Dllx64_CppxdataUsage::MaybeThrowWithSEH()").FirstOrDefault());
        Assert.IsNotNull(libSymbolsInPData.Where(s => s.Name == "[pdata] std::exception::exception(const std::exception&)").FirstOrDefault());
        Assert.IsNotNull(libSymbolsInPData.Where(s => s.Name == "[pdata] std::exception::exception(const char* const)").FirstOrDefault());
        Assert.IsNotNull(libSymbolsInPData.Where(s => s.Name == "[pdata] std::exception::~exception()").FirstOrDefault());
        Assert.IsNotNull(libSymbolsInPData.Where(s => s.Name == "[pdata] std::exception::`scalar deleting destructor'(unsigned int)").FirstOrDefault());
        Assert.IsNotNull(libSymbolsInPData.Where(s => s.Name == "[pdata] std::exception::what() const").FirstOrDefault());
        Assert.IsNotNull(libSymbolsInPData.Where(s => s.Name == "[pdata] `Dllx64_CppxdataUsage::MaybeThrow'::`1'::catch$1()").FirstOrDefault());
        Assert.IsNotNull(libSymbolsInPData.Where(s => s.Name == "[pdata] `Dllx64_CppxdataUsage::MaybeThrowWithSEH'::`1'::fin$0()").FirstOrDefault());
        Assert.IsNotNull(libSymbolsInPData.Where(s => s.Name == "[pdata] `Dllx64_CppxdataUsage::MaybeThrowWithSEH'::`1'::filt$1()").FirstOrDefault());


        Assert.IsNotNull(compilandSymbolsInPData.Where(s => s.Name == "[pdata] Dllx64_CppxdataUsage::MaybeThrow()").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInPData.Where(s => s.Name == "[pdata] Dllx64_CppxdataUsage::MaybeThrowWithSEH()").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInPData.Where(s => s.Name == "[pdata] std::exception::exception(const std::exception&)").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInPData.Where(s => s.Name == "[pdata] std::exception::exception(const char* const)").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInPData.Where(s => s.Name == "[pdata] std::exception::~exception()").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInPData.Where(s => s.Name == "[pdata] std::exception::`scalar deleting destructor'(unsigned int)").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInPData.Where(s => s.Name == "[pdata] std::exception::what() const").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInPData.Where(s => s.Name == "[pdata] `Dllx64_CppxdataUsage::MaybeThrow'::`1'::catch$1()").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInPData.Where(s => s.Name == "[pdata] `Dllx64_CppxdataUsage::MaybeThrowWithSEH'::`1'::fin$0()").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInPData.Where(s => s.Name == "[pdata] `Dllx64_CppxdataUsage::MaybeThrowWithSEH'::`1'::filt$1()").FirstOrDefault());

        var libSymbolsInRData = await session.EnumerateSymbolsInContribution(lib.COFFGroupContributionsByName[".rdata"], CancellationToken.None);
        var compilandSymbolsInRData = await session.EnumerateSymbolsInContribution(compiland.COFFGroupContributionsByName[".rdata"], CancellationToken.None);

        // This binary ends up generating an xdata symbol into .rdata, so let's make sure we can find that - otherwise
        // we might only be able to find xdata symbols in .xdata
        var cilLib = libs.Single(l => l.Name == "...no name found...");
        var cilCompiland = cilLib.Compilands.Values.Single(c => c.Name == "* CIL *");

        var cilLibSymbolsInRData = await session.EnumerateSymbolsInContribution(cilLib.SectionContributionsByName[".rdata"], CancellationToken.None);
        var cilCompilandSymbolsInRData = await session.EnumerateSymbolsInContribution(cilCompiland.SectionContributionsByName[".rdata"], CancellationToken.None);

        Assert.IsNotNull(cilLibSymbolsInRData.Where(s => s.Name == "[cppxdata] Dllx64_CppxdataUsage::MaybeThrow()").FirstOrDefault());
        Assert.IsNotNull(cilCompilandSymbolsInRData.Where(s => s.Name == "[cppxdata] Dllx64_CppxdataUsage::MaybeThrow()").FirstOrDefault());

        // Let's spot-check one more .rdata symbol that's not xdata
        Assert.IsNotNull(libSymbolsInRData.Where(s => s.Name == "const std::exception::`vftable'").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInRData.Where(s => s.Name == "const std::exception::`vftable'").FirstOrDefault());

        Assert.AreEqual(2, libSymbolsInRData.Count);
        Assert.AreEqual(2, compilandSymbolsInRData.Count);

        // Let's check some in .xdata since that is contained in the pure-XDATA RVA ranges so it can skip DIA enumeration
        var libSymbolsInXData = await session.EnumerateSymbolsInContribution(lib.COFFGroupContributionsByName[".xdata"], CancellationToken.None);
        var compilandSymbolsInXData = await session.EnumerateSymbolsInContribution(compiland.COFFGroupContributionsByName[".xdata"], CancellationToken.None);

        Assert.IsNotNull(libSymbolsInXData.Where(s => s.Name == "[unwind] Dllx64_CppxdataUsage::MaybeThrow()").FirstOrDefault());
        Assert.IsNotNull(libSymbolsInXData.Where(s => s.Name == "[tryMap] Dllx64_CppxdataUsage::MaybeThrow()").FirstOrDefault());

        Assert.IsNotNull(compilandSymbolsInXData.Where(s => s.Name == "[unwind] Dllx64_CppxdataUsage::MaybeThrow()").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInXData.Where(s => s.Name == "[tryMap] Dllx64_CppxdataUsage::MaybeThrow()").FirstOrDefault());

        Assert.AreEqual(14, libSymbolsInXData.Count);
        Assert.AreEqual(14, compilandSymbolsInXData.Count);

        // And while we're at it, let's just check on the .text symbols for a couple functions that match these xdata/pdata symbols
        var libSymbolsInText = await session.EnumerateSymbolsInContribution(lib.SectionContributionsByName[".text"], CancellationToken.None);
        var compilandSymbolsInText = await session.EnumerateSymbolsInContribution(compiland.SectionContributionsByName[".text"], CancellationToken.None);

        Assert.IsNotNull(libSymbolsInText.OfType<IFunctionCodeSymbol>().Where(fn => fn.FullName == "bool Dllx64_CppxdataUsage::MaybeThrow()").FirstOrDefault());
        Assert.IsNotNull(libSymbolsInText.OfType<IFunctionCodeSymbol>().Where(fn => fn.FullName == "bool Dllx64_CppxdataUsage::MaybeThrowWithSEH()").FirstOrDefault());

        Assert.IsNotNull(compilandSymbolsInText.OfType<IFunctionCodeSymbol>().Where(fn => fn.FullName == "bool Dllx64_CppxdataUsage::MaybeThrow()").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInText.OfType<IFunctionCodeSymbol>().Where(fn => fn.FullName == "bool Dllx64_CppxdataUsage::MaybeThrowWithSEH()").FirstOrDefault());

        // These expected counts come from inspecting the map file
        // But note that in this case the map file misrepresents what's actually in the binary/PDB - it suggests
        // that there are these two symbols:
        // `Dllx64_CppxdataUsage::MaybeThrow'::`1'::catch$0
        // [catch] Dllx64_CppxdataUsage::MaybeThrow
        // When in fact these are one symbol.  Notice how the length is 13+37 for these two symbols, and the
        // catch symbol that we find with DIA is 50 bytes long - so we're attributing all the information and
        // not losing any bytes, but the count is off by one compared to the map file as a result.
        Assert.AreEqual(10, libSymbolsInText.Count);
        Assert.AreEqual(10, compilandSymbolsInText.Count);

        var catchSymbolInText = libSymbolsInText.Where(s => s.Name == "`Dllx64_CppxdataUsage::MaybeThrow'::`1'::catch$1()").FirstOrDefault();
        Assert.IsNotNull(catchSymbolInText);
        Assert.AreEqual(50u, catchSymbolInText.Size);

        catchSymbolInText = compilandSymbolsInText.Where(s => s.Name == "`Dllx64_CppxdataUsage::MaybeThrow'::`1'::catch$1()").FirstOrDefault();
        Assert.IsNotNull(catchSymbolInText);
        Assert.AreEqual(50u, catchSymbolInText.Size);

        var rsrcLib = libs.Single(lib => lib.Name.EndsWith(".res", StringComparison.Ordinal));
        var rsrcCompiland = rsrcLib.Compilands.Values.Single();

        var libSymbolsInRsrc = await session.EnumerateSymbolsInContribution(rsrcLib.SectionContributionsByName[".rsrc"], CancellationToken.None);
        var compilandSymbolsInRsrc = await session.EnumerateSymbolsInContribution(rsrcCompiland.SectionContributionsByName[".rsrc"], CancellationToken.None);

        Assert.IsNotNull(libSymbolsInRsrc.Where(s => s.Name == "[rsrc directory] L0 (Root)").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInRsrc.Where(s => s.Name == "[rsrc directory] L0 (Root)").FirstOrDefault());

        Assert.IsNotNull(libSymbolsInRsrc.OfType<RsrcGroupCursorDataSymbol>().Where(s => s.Name.Contains("CURSOR2NAMEDRESOURCE", StringComparison.Ordinal)).FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInRsrc.OfType<RsrcGroupCursorDataSymbol>().Where(s => s.Name.Contains("CURSOR2NAMEDRESOURCE", StringComparison.Ordinal)).FirstOrDefault());

        // Also check that we attributed to the right COFF Group Contribution
        libSymbolsInRsrc = await session.EnumerateSymbolsInContribution(rsrcLib.COFFGroupContributionsByName[".rsrc$01"], CancellationToken.None);
        compilandSymbolsInRsrc = await session.EnumerateSymbolsInContribution(rsrcCompiland.COFFGroupContributionsByName[".rsrc$01"], CancellationToken.None);

        Assert.IsNotNull(libSymbolsInRsrc.Where(s => s.Name == "[rsrc directory] L0 (Root)").FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInRsrc.Where(s => s.Name == "[rsrc directory] L0 (Root)").FirstOrDefault());

        Assert.IsNull(libSymbolsInRsrc.OfType<RsrcGroupCursorDataSymbol>().Where(s => s.Name.Contains("CURSOR2NAMEDRESOURCE", StringComparison.Ordinal)).FirstOrDefault());
        Assert.IsNull(compilandSymbolsInRsrc.OfType<RsrcGroupCursorDataSymbol>().Where(s => s.Name.Contains("CURSOR2NAMEDRESOURCE", StringComparison.Ordinal)).FirstOrDefault());

        libSymbolsInRsrc = await session.EnumerateSymbolsInContribution(rsrcLib.COFFGroupContributionsByName[".rsrc$02"], CancellationToken.None);
        compilandSymbolsInRsrc = await session.EnumerateSymbolsInContribution(rsrcCompiland.COFFGroupContributionsByName[".rsrc$02"], CancellationToken.None);

        Assert.IsNull(libSymbolsInRsrc.Where(s => s.Name == "[rsrc directory] L0 (Root)").FirstOrDefault());
        Assert.IsNull(compilandSymbolsInRsrc.Where(s => s.Name == "[rsrc directory] L0 (Root)").FirstOrDefault());

        Assert.IsNotNull(libSymbolsInRsrc.OfType<RsrcGroupCursorDataSymbol>().Where(s => s.Name.Contains("CURSOR2NAMEDRESOURCE", StringComparison.Ordinal)).FirstOrDefault());
        Assert.IsNotNull(compilandSymbolsInRsrc.OfType<RsrcGroupCursorDataSymbol>().Where(s => s.Name.Contains("CURSOR2NAMEDRESOURCE", StringComparison.Ordinal)).FirstOrDefault());
    }
}
