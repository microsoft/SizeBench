using System.IO;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.RealPETests.Single_Binary;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace PEParser.Tests;

[DeploymentItem(@"Test PEs\PEParser.Tests.Dllarm32.dll")]
[DeploymentItem(@"Test PEs\PEParser.Tests.Dllarm32.pdb")]
[TestClass]
public sealed class DllArmTests
{
    public TestContext? TestContext { get; set; }

    private static Session? DllArm32Session;

    private static NoOpLogger? SessionLogger;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);

        SessionLogger = new NoOpLogger();
        DllArm32Session = await Session.Create(Path.Combine(testContext.DeploymentDirectory!, "PEParser.Tests.Dllarm32.dll"),
                                               Path.Combine(testContext.DeploymentDirectory!, "PEParser.Tests.Dllarm32.pdb"),
                                               SessionLogger);
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (DllArm32Session != null)
        {
            await DllArm32Session.DisposeAsync();
            SessionLogger?.Dispose();
        }
    }

    [TestMethod]
    public void DllArmPDATAAndXDATACanBeParsed()
    {
        Assert.IsNotNull(DllArm32Session);

        // These are gotten from "link /dump /headers PEParser.Tests.Dllarm32.dll" and looking at the "Exception" directory
        Assert.AreEqual(0x7000u, DllArm32Session.DataCache.PDataRVARange.RVAStart);
        Assert.AreEqual(0x318u, DllArm32Session.DataCache.PDataRVARange.Size);

        // We should be discovering a bunch of RVA Ranges that we then coalesce down to just two: cppxdata in .rdata and the .xdata COFF Group
        Assert.AreEqual(2, DllArm32Session.DataCache.XDataRVARanges.Count);

        // The first range is for the cppxdata symbol that is in .rdata
        var xdataRanges = DllArm32Session.DataCache.XDataRVARanges.OrderBy(range => range.RVAStart);
        Assert.AreEqual(0x40D8u, xdataRanges.First().RVAStart);
        Assert.AreEqual(0xA0u, xdataRanges.First().Size);

        // The second range is the .xdata COFF Group, the size should match the size of .xdata in "link /dump /headers /coffgroup"
        Assert.AreEqual(0x46A8u, xdataRanges.Skip(1).First().RVAStart);
        Assert.AreEqual(0x6B8u, xdataRanges.Skip(1).Sum(x => x.Size));

        // These symbols are chosen from looking at the MAP file for symbols of each interesting
        // type, and the properties of those symbols as seen in the MAP file (assumed to be the truth).

        // [packedUnwindData-pdata] for _RTC_Initialize
        var _RTC_InitializePackedUnwind = (PackedUnwindDataPDataSymbol)DllArm32Session.DataCache.PDataSymbolsByRVA[0x7258];
        Assert.AreEqual(0x27C4u, _RTC_InitializePackedUnwind.TargetStartRVA);
        Assert.AreEqual(8u, _RTC_InitializePackedUnwind.Size);
        Assert.IsTrue(_RTC_InitializePackedUnwind.Name.Contains("_RTC_Initialize", StringComparison.Ordinal));
        Assert.IsTrue(_RTC_InitializePackedUnwind.Name.Contains("packedUnwindData-pdata", StringComparison.Ordinal));

        // [pdata] for DllArm_CppxdataUsage::MaybeThrow
        var DllArm_CppxdataUsage_MaybeThrowPdata = DllArm32Session.DataCache.PDataSymbolsByRVA[0x7080];
        Assert.AreEqual(0x1618u, DllArm_CppxdataUsage_MaybeThrowPdata.TargetStartRVA);
        Assert.AreEqual(8u, DllArm_CppxdataUsage_MaybeThrowPdata.Size);
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowPdata.Name.Contains("DllArm_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowPdata.Name.Contains("pdata", StringComparison.Ordinal));

        // [cppxdata] for DllArm_CppxdataUsage::MaybeThrow
        var DllArm_CppxdataUsage_MaybeThrowCppxdata = (CppXdataSymbol)DllArm32Session.DataCache.XDataSymbolsByRVA[0x40D8];
        Assert.AreEqual(DllArm_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, DllArm_CppxdataUsage_MaybeThrowCppxdata.TargetStartRVA);
        Assert.AreEqual(40u, DllArm_CppxdataUsage_MaybeThrowCppxdata.Size);
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowCppxdata.Name.Contains("DllArm_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowCppxdata.Name.Contains("cppxdata", StringComparison.Ordinal));

        // [handlerMap] for DllArm_CppxdataUsage::MaybeThrow
        var DllArm_CppxdataUsage_MaybeThrowHandlerMap = (HandlerMapSymbol)DllArm32Session.DataCache.XDataSymbolsByRVA[0x4124];
        Assert.AreEqual(DllArm_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, DllArm_CppxdataUsage_MaybeThrowHandlerMap.TargetStartRVA);
        Assert.AreEqual(20u, DllArm_CppxdataUsage_MaybeThrowHandlerMap.Size);
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowHandlerMap.Name.Contains("DllArm_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowHandlerMap.Name.Contains("handlerMap", StringComparison.Ordinal));

        // [ip2State] for DllArm_CppxdataUsage::MaybeThrow
        var DllArm_CppxdataUsage_MaybeThrowIpToStateMap = (IpToStateMapSymbol)DllArm32Session.DataCache.XDataSymbolsByRVA[0x4138];
        Assert.AreEqual(DllArm_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, DllArm_CppxdataUsage_MaybeThrowIpToStateMap.TargetStartRVA);
        Assert.AreEqual(64u, DllArm_CppxdataUsage_MaybeThrowIpToStateMap.Size);
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowIpToStateMap.Name.Contains("DllArm_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowIpToStateMap.Name.Contains("ip2state", StringComparison.Ordinal));

        // [stateUnwindMap] for DllArm_CppxdataUsage::MaybeThrow
        var DllArm_CppxdataUsage_MaybeThrowStateUnwindMap = (StateUnwindMapSymbol)DllArm32Session.DataCache.XDataSymbolsByRVA[0x4100];
        Assert.AreEqual(DllArm_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, DllArm_CppxdataUsage_MaybeThrowStateUnwindMap.TargetStartRVA);
        Assert.AreEqual(16u, DllArm_CppxdataUsage_MaybeThrowStateUnwindMap.Size);
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowStateUnwindMap.Name.Contains("DllArm_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowStateUnwindMap.Name.Contains("stateUnwindMap", StringComparison.Ordinal));

        // [tryMap] for DllArm_CppxdataUsage::MaybeThrow
        var DllArm_CppxdataUsage_MaybeThrowTryMap = (TryMapSymbol)DllArm32Session.DataCache.XDataSymbolsByRVA[0x4110];
        Assert.AreEqual(DllArm_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, DllArm_CppxdataUsage_MaybeThrowTryMap.TargetStartRVA);
        Assert.AreEqual(20u, DllArm_CppxdataUsage_MaybeThrowTryMap.Size);
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowTryMap.Name.Contains("DllArm_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowTryMap.Name.Contains("tryMap", StringComparison.Ordinal));

        // [pdata] for dllMain
        var dllMainPdata = DllArm32Session.DataCache.PDataSymbolsByRVA[0x7048];
        Assert.AreEqual(0x14E4u, dllMainPdata.TargetStartRVA);
        Assert.AreEqual(8u, dllMainPdata.Size);
        Assert.IsTrue(dllMainPdata.Name.Contains("DllMain", StringComparison.Ordinal));
        Assert.IsTrue(dllMainPdata.Name.Contains("pdata", StringComparison.Ordinal));

        // This tests an unwind symbol that does not have an exception handler
        var is_potentially_valid_image_baseUnwind = (UnwindInfoSymbol)DllArm32Session.DataCache.XDataSymbolsByRVA[0x4B10];
        Assert.AreEqual(0x1EB4u, is_potentially_valid_image_baseUnwind.TargetStartRVA);
        Assert.AreEqual(16u, is_potentially_valid_image_baseUnwind.Size);
        Assert.IsTrue(is_potentially_valid_image_baseUnwind.Name.Contains("is_potentially_valid_image_base", StringComparison.Ordinal));
        Assert.IsTrue(is_potentially_valid_image_baseUnwind.Name.Contains("unwind", StringComparison.Ordinal));

        // This tests an unwind symbol that uses __GSHandlecrCheck
        var dllMainUnwind = (UnwindInfoSymbol)DllArm32Session.DataCache.XDataSymbolsByRVA[0x4708];
        Assert.AreEqual(0x14E4u, dllMainUnwind.TargetStartRVA);
        Assert.AreEqual(28u, dllMainUnwind.Size);
        Assert.IsTrue(dllMainUnwind.Name.Contains("DllMain", StringComparison.Ordinal));
        Assert.IsFalse(dllMainUnwind.Name.Contains("dispatch", StringComparison.Ordinal)); // Don't get dllmain_dispatch by mistake here
        Assert.IsTrue(dllMainUnwind.Name.Contains("unwind", StringComparison.Ordinal));

        // This tests an unwind symbol that uses __GSHandlerCheck_EH
        var DllArm_CppxdataUsage_MaybeThrowUnwind = (UnwindInfoSymbol)DllArm32Session.DataCache.XDataSymbolsByRVA[0x4728];
        Assert.AreEqual(0x1618u, DllArm_CppxdataUsage_MaybeThrowUnwind.TargetStartRVA);
        Assert.AreEqual(36u, DllArm_CppxdataUsage_MaybeThrowUnwind.Size);
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowUnwind.Name.Contains("DllArm_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsFalse(DllArm_CppxdataUsage_MaybeThrowUnwind.Name.Contains("SEH", StringComparison.Ordinal)); // Don't get MaybeThrowWithSEH by mistake here
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowUnwind.Name.Contains("unwind", StringComparison.Ordinal));

        // This tests an unwind symbol that uses __GSHandlerCheck_SEH (for Structured Exception Handling with /GS data)
        var DllArm_CppxdataUsage_MaybeThrowWithSEHUnwind = (UnwindInfoSymbol)DllArm32Session.DataCache.XDataSymbolsByRVA[0x4768];
        Assert.AreEqual(0x1674u, DllArm_CppxdataUsage_MaybeThrowWithSEHUnwind.TargetStartRVA);
        Assert.AreEqual(68u, DllArm_CppxdataUsage_MaybeThrowWithSEHUnwind.Size);
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowWithSEHUnwind.Name.Contains("DllArm_CppxdataUsage::MaybeThrowWithSEH", StringComparison.Ordinal));
        Assert.IsTrue(DllArm_CppxdataUsage_MaybeThrowWithSEHUnwind.Name.Contains("unwind", StringComparison.Ordinal));

        // This tests an unwind symbol that uses __C_specific_handler
        var dllMainDispatchUnwind = (UnwindInfoSymbol)DllArm32Session.DataCache.XDataSymbolsByRVA[0x4958];
        Assert.AreEqual(0x1BB8u, dllMainDispatchUnwind.TargetStartRVA);
        Assert.AreEqual(44u, dllMainDispatchUnwind.Size);
        Assert.IsTrue(dllMainDispatchUnwind.Name.Contains("dllmain_dispatch", StringComparison.Ordinal));
        Assert.IsTrue(dllMainDispatchUnwind.Name.Contains("unwind", StringComparison.Ordinal));

        // This tests an unwind symbol that uses __CxxFrameHandler3
        // TODO: Add a unit test to cover this case.
        // In the test ARM test PE we added, there is only one funclet (for function DllArm_CppxdataUsage::MaybeThrow) that makes use of this handler.
        // But given that this funclet falls under .text$x and we don't know how to parse the content of this group yet (in ARM funclets are not COFF symbols, hence!),
        // SizeBench RVA to symbol name mapping is not working for these.
    }

    [TestMethod]
    public async Task DllArmRelativeRVAsCanBeLoadedFromVTable()
    {
        Assert.IsNotNull(DllArm32Session);

        // The RVA passed in as a parameter here is that of DllArm_DerivedClass::'vftable'
        // When updating this test, just get this value from the map file.
        const int derivedClassVftableRVA = 0x40C0;
        var firstEntry = await DllArm32Session.LoadSymbolForVTableSlotAsync(derivedClassVftableRVA, slotIndex: 0);
        Assert.IsNotNull(firstEntry);
        // Note that this entry is offset by 1 byte from what DIA fetches, because we later compensate for Thumb code RVAs in GetAdjustedRva - so this test
        // is really important that it tests the RVA values explicitly for ARM32.
        Assert.AreEqual(0x15ECu, firstEntry.RVA);
        StringAssert.Contains(firstEntry.Name, "DllArm_DerivedClass", StringComparison.Ordinal);
        StringAssert.Contains(firstEntry.Name, "AVirtualFunction", StringComparison.Ordinal);

        var secondEntry = await DllArm32Session.LoadSymbolForVTableSlotAsync(derivedClassVftableRVA, slotIndex: 1);
        Assert.IsNotNull(secondEntry);
        Assert.AreEqual(0x15A8u, secondEntry.RVA);
        StringAssert.Contains(secondEntry.Name, "DllArm_BaseClass", StringComparison.Ordinal);
        StringAssert.Contains(secondEntry.Name, "ASecondVirtualFunction", StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> DynamicDataSourceForSymbolSourcesSupportedTests => SymbolSourcesSupportedCommonTests.DynamicDataSourceForSymbolSourcesSupportedTests;

    [TestMethod]
    [DynamicData(nameof(DynamicDataSourceForSymbolSourcesSupportedTests))]
    public Task SymbolSourcesSupportedWorks(SymbolSourcesSupported symbolSources) =>
        SymbolSourcesSupportedCommonTests.VerifyNoUnexpectedSymbolTypesCanBeMaterialized(
            Path.Combine(this.TestContext!.DeploymentDirectory!, "PEParser.Tests.Dllarm32.dll"),
            Path.Combine(this.TestContext!.DeploymentDirectory!, "PEParser.Tests.Dllarm32.pdb"),
            symbolSources,
            this.TestContext.CancellationTokenSource.Token);
}
