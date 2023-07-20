using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests.Single_Binary;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb")]
[TestClass]
public sealed class MASMTests
{
    public TestContext? TestContext { get; set; }
    public CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

    private string BinaryPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.dll");

    private string PDBPath => MakePath("SizeBenchV2.AnalysisEngine.Tests.CppTestCasesBefore.pdb");

    public string MakePath(string binary) => Path.Combine(this.TestContext!.DeploymentDirectory!, binary);

    [TestMethod]
    public async Task MASMProcPointersCanBeParsedAndAreNotInRVARangesWhenOverlappingOtherSymbols()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken);

        // When trying to enumerate the symbols in .text, we should find "asmProc" which is a procedure written in MASM, and 
        // we should also find a data symbol named "MyTestEntry" which is a weird MASM pointer type that ends up pointing to
        // SymTagNull - this used to cause us to fail, so this test ensures we correctly parse this unusual situation.

        var textSymbols = await session.EnumerateSymbolsInBinarySection(sections.Single(s => s.Name == ".text"), this.CancellationToken);

        var asmProc = (SimpleFunctionCodeSymbol)textSymbols.Single(sym => sym.Name == "asmProc()");
        Assert.AreEqual(asmProc.Name, asmProc.CanonicalName);

        var asmVeryLongBasicBlock = (SimpleFunctionCodeSymbol)textSymbols.Single(sym => sym.Name == "asmVeryLongBasicBlock()");
        Assert.AreEqual(asmVeryLongBasicBlock.Name, asmVeryLongBasicBlock.CanonicalName);

        // These two labels are 'within' other symbols so we don't return them when enumerating by RVA ranges because it would double-count bytes.
        Assert.IsNull(textSymbols.FirstOrDefault(sym => sym.Name == "MyTestEntry"));
        Assert.IsNull(textSymbols.FirstOrDefault(sym => sym.Name == "SomeAltEntry"));

        var MyTestEntry = await session.LoadSymbolByRVA(0x27E8) as StaticDataSymbol;
        Assert.AreEqual(0u, MyTestEntry!.Size); // It's a pointer, but it's a special pointer that doesn't occupy space, it just refers to an address
        Assert.AreEqual(0x27E8u, MyTestEntry.RVA);
        Assert.AreEqual(0x27E8u, MyTestEntry.RVAEnd);
        Assert.IsInstanceOfType(MyTestEntry.Type, typeof(FunctionTypeSymbol));
        Assert.AreEqual("void (*function)()", MyTestEntry.Type!.Name);
        // This data should be directly in the middle of asmProc's RVA range
        Assert.IsTrue(MyTestEntry.RVA > asmProc.RVA);
        Assert.IsTrue(MyTestEntry.RVAEnd < asmProc.RVAEnd);

        var SomeAltEntry = await session.LoadSymbolByRVA(0x45DF) as PublicSymbol;
        Assert.AreEqual(33u, SomeAltEntry!.Size); // This is an entry point into a procedure ("altentry") so it does occupy space from here to the end of the procedure
        Assert.AreEqual(0x45DFu, SomeAltEntry.RVA);
        Assert.AreEqual(0x45FFu, SomeAltEntry.RVAEnd);
        // This symbol should start in the middle of asmVeryLongBasicBlock, but it extends 'past the end' because the length is from a public symbol that is somewhat distrustworthy.
        // It's important that it extends past the end, as that's a good thing to test for the sanity check in EnumerateSymbolsInRVARangeSessionTask, as it simulates behavior seen
        // in OS Binaries in Windows when SizeBench is run in the Windows Engineering System.
        Assert.IsTrue(SomeAltEntry.RVA > asmVeryLongBasicBlock.RVA);
        Assert.IsTrue(SomeAltEntry.RVA < asmVeryLongBasicBlock.RVAEnd);
        Assert.IsTrue(SomeAltEntry.RVAEnd > asmVeryLongBasicBlock.RVAEnd);
    }
}
