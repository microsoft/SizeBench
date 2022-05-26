using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests.Single_Binary;

[DeploymentItem(@"Test PEs\PEParser.Tests.DllCxxFrameHandler4.dll")]
[DeploymentItem(@"Test PEs\PEParser.Tests.DllCxxFrameHandler4.pdb")]
[TestClass]
public class DllCxxFrameHandler4Tests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory, filename);

    private string BinaryPath => MakePath("PEParser.Tests.DllCxxFrameHandler4.dll");
    private string PDBPath => MakePath("PEParser.Tests.DllCxxFrameHandler4.pdb");

    [TestMethod]
    public async Task DllCxxFrameHandler4PDATAAndXDATACanBeParsed()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        // These are gotten from "link /dump /headers PEParser.Tests.DllCxxFrameHandler4.dll" and looking at the .pdata section-
        // "virtual address" and "virtual size"
        Assert.AreEqual(0x5000u, session.DataCache.PDataRVARange!.RVAStart);

        // Note that the size will be rounded up to the nearest section alignment, so it will be 0x1000
        // because the next section (.rsrc) doesn't begin until 0x6000
        // To make this test easier to update as the binary may change, we calculate this as
        // (beginning of .rsrc - beginning of .pdata)
        Assert.AreEqual(0x6000u - 0x5000u, session.DataCache.PDataRVARange.Size);

        Assert.AreEqual(1, session.DataCache.XDataRVARanges!.Count);

        Assert.AreEqual(0x3788u, session.DataCache.XDataRVARanges.First().RVAStart);
        Assert.AreEqual(0x348u, session.DataCache.XDataRVARanges.First().Size);

        // These symbols are chosen from looking at the MAP file for symbols of each interesting
        // type, and the properties of those symbols as seen in the MAP file (assumed to be the truth).

        // [pdata] for DllCxxFrameHandler4_CppxdataUsage::MaybeThrow
        var DllCxxFrameHandler4_CppxdataUsage_MaybeThrowPdata = session.DataCache.PDataSymbolsByRVA![0x524C];
        Assert.AreEqual(0x2940u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowPdata.TargetStartRVA);
        Assert.AreEqual(12u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowPdata.Size);
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowPdata.Name.Contains("DllCxxFrameHandler4_CppxdataUsage::MaybeThrow(", StringComparison.Ordinal));
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowPdata.Name.Contains("pdata", StringComparison.Ordinal));

        // This tests an unwind symbol that uses __CxxFrameHandler4 (MaybeThrow catch$1)
        var DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCatch1Unwind = (UnwindInfoSymbol)session.DataCache.XDataSymbolsByRVA![0x3ABC];
        Assert.AreEqual(0x2BFBu, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCatch1Unwind.TargetStartRVA);
        Assert.AreEqual(8u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCatch1Unwind.Size);
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCatch1Unwind.Name.Contains("DllCxxFrameHandler4_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCatch1Unwind.Name.Contains("catch$1", StringComparison.Ordinal));
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCatch1Unwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCatch1Unwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        // This tests an unwind symbol that uses __GSHandlerCheck_EH4 (MaybeThrow function itself)
        var DllCxxFrameHandler4_CppxdataUsage_MaybeThrowUnwind = (UnwindInfoSymbol)session.DataCache.XDataSymbolsByRVA[0x3A88];
        Assert.AreEqual(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowUnwind.TargetStartRVA);
        Assert.AreEqual(20u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowUnwind.Size);
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowUnwind.Name.Contains("DllCxxFrameHandler4_CppxdataUsage::MaybeThrow(", StringComparison.Ordinal));
        Assert.IsFalse(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowUnwind.Name.Contains("catch", StringComparison.Ordinal)); // This should be for the function itself, not any specific catch$x block
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        // [cppxdata] for DllCxxFrameHandler4_CppxdataUsage::MaybeThrow
        var DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCppxdata = (CppXdataSymbol)session.DataCache.XDataSymbolsByRVA[0x3A71];
        Assert.AreEqual(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCppxdata.TargetStartRVA);
        Assert.AreEqual(13u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCppxdata.Size);
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCppxdata.Name.Contains("DllCxxFrameHandler4_CppxdataUsage::MaybeThrow(", StringComparison.Ordinal));
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowCppxdata.Name.Contains("cppxdata", StringComparison.Ordinal));

        // [handlerMap] for DllCxxFrameHandler4_CppxdataUsage::MaybeThrow
        var DllCxxFrameHandler4_CppxdataUsage_MaybeThrowHandlerMap = (HandlerMapSymbol)session.DataCache.XDataSymbolsByRVA[0x3AA7];
        Assert.AreEqual(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowHandlerMap.TargetStartRVA);
        Assert.AreEqual(12u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowHandlerMap.Size);
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowHandlerMap.Name.Contains("DllCxxFrameHandler4_CppxdataUsage::MaybeThrow(", StringComparison.Ordinal));
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowHandlerMap.Name.Contains("handlerMap", StringComparison.Ordinal));

        // [ip2State] for DllCxxFrameHandler4_CppxdataUsage::MaybeThrow
        var DllCxxFrameHandler4_CppxdataUsage_MaybeThrowIpToStateMap = (IpToStateMapSymbol)session.DataCache.XDataSymbolsByRVA[0x3AB3];
        Assert.AreEqual(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowIpToStateMap.TargetStartRVA);
        // The map file suggests this would be 9 bytes long, but it's not - I confirmed with DevDiv the parser in SizeBench is correct
        // and this should be 7 bytes.  The other 2 bytes are some padding needed by the following symbol.
        Assert.AreEqual(7u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowIpToStateMap.Size);
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowIpToStateMap.Name.Contains("DllCxxFrameHandler4_CppxdataUsage::MaybeThrow(", StringComparison.Ordinal));
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowIpToStateMap.Name.Contains("ip2state", StringComparison.Ordinal));

        // [stateUnwindMap] for DllCxxFrameHandler4_CppxdataUsage::MaybeThrow
        var DllCxxFrameHandler4_CppxdataUsage_MaybeThrowStateUnwindMap = (StateUnwindMapSymbol)session.DataCache.XDataSymbolsByRVA[0x3A9C];
        Assert.AreEqual(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowStateUnwindMap.TargetStartRVA);
        Assert.AreEqual(3u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowStateUnwindMap.Size);
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowStateUnwindMap.Name.Contains("DllCxxFrameHandler4_CppxdataUsage::MaybeThrow(", StringComparison.Ordinal));
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowStateUnwindMap.Name.Contains("stateUnwindMap", StringComparison.Ordinal));

        // [tryMap] for DllCxxFrameHandler4_CppxdataUsage::MaybeThrow
        var DllCxxFrameHandler4_CppxdataUsage_MaybeThrowTryMap = (TryMapSymbol)session.DataCache.XDataSymbolsByRVA[0x3A9F];
        Assert.AreEqual(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowTryMap.TargetStartRVA);
        Assert.AreEqual(8u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowTryMap.Size);
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowTryMap.Name.Contains("DllCxxFrameHandler4_CppxdataUsage::MaybeThrow(", StringComparison.Ordinal));
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowTryMap.Name.Contains("tryMap", StringComparison.Ordinal));

        // [handlerMap] for DllCxxFrameHandler4_CppxdataUsage::MaybeThrowWithContTypeNONE to test that continuation type
        var DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeNONEHandlerMap = (HandlerMapSymbol)session.DataCache.XDataSymbolsByRVA[0x3A63];
        Assert.AreEqual(0x28F0u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeNONEHandlerMap.TargetStartRVA);
        Assert.AreEqual(11u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeNONEHandlerMap.Size);
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeNONEHandlerMap.Name.Contains("DllCxxFrameHandler4_CppxdataUsage::MaybeThrowWithContTypeNONE", StringComparison.Ordinal));
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeNONEHandlerMap.Name.Contains("handlerMap", StringComparison.Ordinal));

        // [handlerMap] for DllCxxFrameHandler4_CppxdataUsage::MaybeThrowWithContTypeONE to test that continuation type
        var DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeONEHandlerMap = (HandlerMapSymbol)session.DataCache.XDataSymbolsByRVA[0x3A28];
        Assert.AreEqual(0x28C0u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeONEHandlerMap.TargetStartRVA);
        Assert.AreEqual(8u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeONEHandlerMap.Size);
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeONEHandlerMap.Name.Contains("DllCxxFrameHandler4_CppxdataUsage::MaybeThrowWithContTypeONE", StringComparison.Ordinal));
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeONEHandlerMap.Name.Contains("handlerMap", StringComparison.Ordinal));

        // [handlerMap] for DllCxxFrameHandler4_CppxdataUsage::MaybeThrowWithContTypeTWO to test that continuation type
        var DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeTWOHandlerMap = (HandlerMapSymbol)session.DataCache.XDataSymbolsByRVA[0x39EC];
        Assert.AreEqual(0x2880u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeTWOHandlerMap.TargetStartRVA);
        Assert.AreEqual(9u, DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeTWOHandlerMap.Size);
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeTWOHandlerMap.Name.Contains("DllCxxFrameHandler4_CppxdataUsage::MaybeThrowWithContTypeTWO", StringComparison.Ordinal));
        Assert.IsTrue(DllCxxFrameHandler4_CppxdataUsage_MaybeThrowWithContTypeTWOHandlerMap.Name.Contains("handlerMap", StringComparison.Ordinal));
    }
}
