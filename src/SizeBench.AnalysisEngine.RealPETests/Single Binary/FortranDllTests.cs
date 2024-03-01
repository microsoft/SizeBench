using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests.Single_Binary;

[DeploymentItem(@"Test PEs\FortranDll.dll")]
[DeploymentItem(@"Test PEs\FortranDll.pdb")]
[TestClass]
public class FortranDllTests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);

    private string BinaryPath => MakePath("FortranDll.dll");
    private string PDBPath => MakePath("FortranDll.pdb");

    [TestMethod]
    public async Task FortranPDATAAndXDATACanBeParsed()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.TestContext!.CancellationTokenSource.Token);
        var pdataSection = sections.Single(s => s.Name == ".pdata");
        var rdataSection = sections.Single(s => s.Name == ".rdata");
        var xdataCOFFGroup = rdataSection.COFFGroups.Single(s => s.Name == ".xdata");

        var pdataSymbols = await session.EnumerateSymbolsInBinarySection(pdataSection, this.TestContext!.CancellationTokenSource.Token);

        Assert.IsNotNull(pdataSymbols.FirstOrDefault(sym => sym.Name.StartsWith("[pdata] SUBROUTINE1(", StringComparison.Ordinal)));
        Assert.IsNotNull(pdataSymbols.FirstOrDefault(sym => sym.Name.StartsWith("[pdata] SUBROUTINE2(", StringComparison.Ordinal)));
        Assert.IsNotNull(pdataSymbols.FirstOrDefault(sym => sym.Name.StartsWith("[pdata] SUBROUTINE3(", StringComparison.Ordinal)));

        var xdataSymbols = await session.EnumerateSymbolsInCOFFGroup(xdataCOFFGroup, this.TestContext!.CancellationTokenSource.Token);

        var xdataSUBSymbols = xdataSymbols.Where(xds => xds.Name.Contains("SUBROUTINE", StringComparison.Ordinal)).ToList();

        // FORTRAN, or at least Intel's ifort.exe compiler, only seems to generate simple [unwind] with no fancy language-specific data.
        Assert.AreEqual(3, xdataSUBSymbols.Count);
        Assert.IsNotNull(xdataSUBSymbols.OfType<UnwindInfoSymbol>().FirstOrDefault(sym => sym.Name.StartsWith("[unwind] SUBROUTINE1(", StringComparison.Ordinal)));
        Assert.IsNotNull(xdataSUBSymbols.OfType<UnwindInfoSymbol>().FirstOrDefault(sym => sym.Name.StartsWith("[unwind] SUBROUTINE2(", StringComparison.Ordinal)));
        Assert.IsNotNull(xdataSUBSymbols.OfType<UnwindInfoSymbol>().FirstOrDefault(sym => sym.Name.StartsWith("[unwind] SUBROUTINE3(", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task FortranDllSubroutinesCanBeParsed()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.TestContext!.CancellationTokenSource.Token);
        var textSection = sections.Single(s => s.Name == ".text");

        var symbols = await session.EnumerateSymbolsInBinarySection(textSection, this.TestContext!.CancellationTokenSource.Token);

        var subroutine1 = symbols.OfType<SimpleFunctionCodeSymbol>().Single(sym => sym.FunctionName == "SUBROUTINE1");

        Assert.AreEqual("void SUBROUTINE1(short* A_SHORT, long* AN_INT, int64[6]* ARR_OF_INT64, float[4][4]* SIMPLE_MATRIX, long[-3 .. 2][0 .. 4]* MATRIX_WITH_FANCY_BOUNDS, float[.tmp.0.MATRIX_WITH_PARAM_BOUNDS][.tmp.1.MATRIX_WITH_PARAM_BOUNDS]* MATRIX_WITH_PARAM_BOUNDS)", subroutine1.FullName);
        Assert.AreEqual(1, subroutine1.Blocks.Count);
        Assert.AreEqual(true, subroutine1.CanBeFolded);
        Assert.AreEqual(subroutine1.Name, subroutine1.CanonicalName);
        Assert.IsNotNull(subroutine1.FunctionType);
        Assert.IsNotNull(subroutine1.FunctionType.ArgumentTypes);
        Assert.AreEqual(6, subroutine1.FunctionType.ArgumentTypes.Count);
        Assert.AreEqual("short*", subroutine1.FunctionType.ArgumentTypes[0].Name);
        Assert.AreEqual("long*", subroutine1.FunctionType.ArgumentTypes[1].Name);
        Assert.AreEqual("int64[6]*", subroutine1.FunctionType.ArgumentTypes[2].Name); // Tests int64 and arrays (single-dimension arrays)
        Assert.AreEqual("float[4][4]*", subroutine1.FunctionType.ArgumentTypes[3].Name); // Tests a matrix with constant dimensions that are 1-based so we can simplify the name
        Assert.AreEqual("long[-3 .. 2][0 .. 4]*", subroutine1.FunctionType.ArgumentTypes[4].Name); // Tests having custom dimension lower and upper bounds
        Assert.AreEqual("float[.tmp.0.MATRIX_WITH_PARAM_BOUNDS][.tmp.1.MATRIX_WITH_PARAM_BOUNDS]*", subroutine1.FunctionType.ArgumentTypes[5].Name); // Tests a matrix with dimensions based on other parameters (I cannot figure out how to make the name "float[A_SHORT][AN_INT]*" which would be ideal to match the source)

        var subroutine1Placement = await session.LookupSymbolPlacementInBinary(subroutine1, this.TestContext!.CancellationTokenSource.Token);

        Assert.AreEqual(".text", subroutine1Placement.BinarySection!.Name);
        Assert.AreEqual(".text", subroutine1Placement.COFFGroup!.Name);
        Assert.AreEqual("FortranDll.obj", subroutine1Placement.Compiland!.ShortName);
        Assert.AreEqual("FortranDll", subroutine1Placement.Lib!.ShortName);
        Assert.AreEqual("FortranDll.f90", subroutine1Placement.SourceFile!.ShortName);

        var subroutine2 = symbols.OfType<SimpleFunctionCodeSymbol>().Single(sym => sym.FunctionName == "SUBROUTINE2");

        Assert.AreEqual("void SUBROUTINE2(double* SOME_DOUBLE, complex* SOME_COMPLEX, complex* SOME_DOUBLE_COMPLEX, bool* SOME_LOGICAL, bool* SOME_LOGICAL_KIND_4)", subroutine2.FullName);
        Assert.AreEqual(1, subroutine2.Blocks.Count);
        Assert.AreEqual(true, subroutine2.CanBeFolded);
        Assert.AreEqual(subroutine2.Name, subroutine2.CanonicalName);
        Assert.IsNotNull(subroutine2.FunctionType);
        Assert.IsNotNull(subroutine2.FunctionType.ArgumentTypes);
        Assert.AreEqual(5, subroutine2.FunctionType.ArgumentTypes.Count);
        Assert.AreEqual("double*", subroutine2.FunctionType.ArgumentTypes[0].Name);
        Assert.AreEqual("complex*", subroutine2.FunctionType.ArgumentTypes[1].Name);
        Assert.AreEqual("complex*", subroutine2.FunctionType.ArgumentTypes[2].Name);
        Assert.AreEqual("bool*", subroutine2.FunctionType.ArgumentTypes[3].Name);
        Assert.AreEqual("bool*", subroutine2.FunctionType.ArgumentTypes[4].Name);

        var subroutine2Placement = await session.LookupSymbolPlacementInBinary(subroutine2, this.TestContext!.CancellationTokenSource.Token);

        Assert.AreEqual(".text", subroutine2Placement.BinarySection!.Name);
        Assert.AreEqual(".text", subroutine2Placement.COFFGroup!.Name);
        Assert.AreEqual("File2.obj", subroutine2Placement.Compiland!.ShortName);
        Assert.AreEqual("File2", subroutine2Placement.Lib!.ShortName);
        Assert.AreEqual("File2.f90", subroutine2Placement.SourceFile!.ShortName);

        var subroutine3 = symbols.OfType<SimpleFunctionCodeSymbol>().Single(sym => sym.FunctionName == "SUBROUTINE3");

        // Not testing the full name because FORTRAN generates hidden parameters for string length at the end of the parameter list, and their name is unstable across builds
        // so the last parameter ends up with a name like ".tmp..T21__V$1b" which is too annoying to maintain in the tests.
        StringAssert.StartsWith(subroutine3.FullName, "void SUBROUTINE3(char[10]* SOME_STRING, [OEM_MS_FORTRAN90 defined type 0x5]* SOME_DEFERRED_LEN_STR, int64", StringComparison.Ordinal);
        Assert.AreEqual(1, subroutine3.Blocks.Count);
        Assert.AreEqual(true, subroutine3.CanBeFolded);
        Assert.AreEqual(subroutine3.Name, subroutine3.CanonicalName);
        Assert.IsNotNull(subroutine3.FunctionType);
        Assert.IsNotNull(subroutine3.FunctionType.ArgumentTypes);
        Assert.AreEqual(3, subroutine3.FunctionType.ArgumentTypes.Count);
        Assert.AreEqual("char[10]*", subroutine3.FunctionType.ArgumentTypes[0].Name);
        Assert.AreEqual("[OEM_MS_FORTRAN90 defined type 0x5]*", subroutine3.FunctionType.ArgumentTypes[1].Name);
        Assert.AreEqual("int64", subroutine3.FunctionType.ArgumentTypes[2].Name);

        var subroutine3Placement = await session.LookupSymbolPlacementInBinary(subroutine3, this.TestContext!.CancellationTokenSource.Token);

        Assert.AreEqual(".text", subroutine3Placement.BinarySection!.Name);
        Assert.AreEqual(".text", subroutine3Placement.COFFGroup!.Name);
        Assert.AreEqual("File2.obj", subroutine3Placement.Compiland!.ShortName);
        Assert.AreEqual("File2", subroutine3Placement.Lib!.ShortName);
        Assert.AreEqual("File2.f90", subroutine3Placement.SourceFile!.ShortName);
    }

    public static IEnumerable<object[]> DynamicDataSourceForSymbolSourcesSupportedTests => SymbolSourcesSupportedCommonTests.DynamicDataSourceForSymbolSourcesSupportedTests;

    [TestMethod]
    [DynamicData(nameof(DynamicDataSourceForSymbolSourcesSupportedTests))]
    public Task SymbolSourcesSupportedWorks(SymbolSourcesSupported symbolSources) =>
        SymbolSourcesSupportedCommonTests.VerifyNoUnexpectedSymbolTypesCanBeMaterialized(
            this.BinaryPath, this.PDBPath, symbolSources,
            this.TestContext!.CancellationTokenSource.Token);
}
