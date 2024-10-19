using System.IO;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.RealPETests.Single_Binary;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace PEParser.Tests;

[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.ClangClx64.dll")]
[DeploymentItem(@"Test PEs\SizeBenchV2.AnalysisEngine.Tests.ClangClx64.pdb")]
[TestClass]
public sealed class ClangClTests
{
    public TestContext? TestContext { get; set; }

    [TestMethod]
    public async Task ClangClx64PDATAAndXDATACanBeParsed()
    {
        using var SessionLogger = new NoOpLogger();
        await using var ClangClx64Session = await Session.Create(Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.ClangClx64.dll"),
                                                                 Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.ClangClx64.pdb"),
                                                                 SessionLogger);

        // These are gotten from "link /dump /headers SizeBenchV2.AnalysisEngine.Tests.ClangClx64.dll" and looking at the "Exception" directory
        Assert.AreEqual(0x5000u, ClangClx64Session.DataCache.PDataRVARange.RVAStart);
        Assert.AreEqual(0x348u, ClangClx64Session.DataCache.PDataRVARange.Size);

        // We discover the .xdata COFF Group in the PDB, since Clang doesn't record it in the PE file.  This can be verified with
        // Dia2Dump.exe -compiland "* Linker *" SizeBenchV2.AnalysisEngine.Tests.ClangClx64.pdb
        Assert.AreEqual(1, ClangClx64Session.DataCache.XDataRVARanges.Count);
        Assert.AreEqual(0x3A18u, ClangClx64Session.DataCache.XDataRVARanges.First().RVAStart);
        Assert.AreEqual(0x2E4u, ClangClx64Session.DataCache.XDataRVARanges.First().Size);

        // [unwind] for _RTC_Initialize, Clang does not seem to generate chain-unwind the way MSVC does
        var _RTC_InitializeUnwind = (UnwindInfoSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3CC4];
        Assert.AreEqual(0x25C0u, _RTC_InitializeUnwind.TargetStartRVA);
        Assert.AreEqual(12u, _RTC_InitializeUnwind.Size);
        Assert.IsTrue(_RTC_InitializeUnwind.Name.Contains("_RTC_Initialize", StringComparison.Ordinal));
        Assert.IsTrue(_RTC_InitializeUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(_RTC_InitializeUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        // [pdata] for Dllx64_CppxdataUsage::MaybeThrow.  Clang generates two copies of this - one at 0x1000 for the function itself, then
        // it seems to generate a second one with the same CppXdata at 0x1070 for some kind of catch handler funclet-esque thing.
        // There is no symbol name recorded at 0x1070 in the PDB, instead the "Dllx64_CppxdataUsage::MaybeThrow" function is recorded as having
        // a length of 0x98 so it extends all the way to the end of the funclet.  Less expressive than MSVC's symbols, but it'll do, and at least
        // it's not losing/hiding bytes to attribute to this function.
        // Each [pdata] should have a corresponding [unwind] too.
        var Dllx64_CppxdataUsage_MaybeThrowPdata = ClangClx64Session.DataCache.PDataSymbolsByRVA[0x5000];
        Assert.AreEqual(0x1000u, Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA);
        Assert.AreEqual(12u, Dllx64_CppxdataUsage_MaybeThrowPdata.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowPdata.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowPdata.Name.Contains("pdata", StringComparison.Ordinal));

        var Dllx64_CppxdataUsage_MaybeThrowUnwind = (UnwindInfoSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3A18];
        Assert.AreEqual(0x1000u, Dllx64_CppxdataUsage_MaybeThrowUnwind.TargetStartRVA);
        Assert.AreEqual(20u, Dllx64_CppxdataUsage_MaybeThrowUnwind.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowUnwind.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsFalse(Dllx64_CppxdataUsage_MaybeThrowUnwind.Name.Contains("SEH", StringComparison.Ordinal)); // Don't get MaybeThrowWithSEH by mistake here
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(Dllx64_CppxdataUsage_MaybeThrowUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        var Dllx64_CppxdataUsage_MaybeThrow_catchFuncletPdata = ClangClx64Session.DataCache.PDataSymbolsByRVA[0x5000 + 12];
        Assert.AreEqual(0x1070u, Dllx64_CppxdataUsage_MaybeThrow_catchFuncletPdata.TargetStartRVA);
        Assert.AreEqual(12u, Dllx64_CppxdataUsage_MaybeThrow_catchFuncletPdata.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrow_catchFuncletPdata.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrow_catchFuncletPdata.Name.Contains("pdata", StringComparison.Ordinal));

        var Dllx64_CppxdataUsage_MaybeThrow_catchFuncletUnwind = (UnwindInfoSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3A2C];
        Assert.AreEqual(0x1070u, Dllx64_CppxdataUsage_MaybeThrow_catchFuncletUnwind.TargetStartRVA);
        Assert.AreEqual(16u, Dllx64_CppxdataUsage_MaybeThrow_catchFuncletUnwind.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrow_catchFuncletUnwind.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsFalse(Dllx64_CppxdataUsage_MaybeThrow_catchFuncletUnwind.Name.Contains("SEH", StringComparison.Ordinal)); // Don't get MaybeThrowWithSEH by mistake here
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrow_catchFuncletUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(Dllx64_CppxdataUsage_MaybeThrow_catchFuncletUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        // [cppxdata] for Dllx64_CppxdataUsage::MaybeThrow
        var Dllx64_CppxdataUsage_MaybeThrowCppxdata = (CppXdataSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3A3C];
        Assert.AreEqual(Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, Dllx64_CppxdataUsage_MaybeThrowCppxdata.TargetStartRVA);
        Assert.AreEqual(40u, Dllx64_CppxdataUsage_MaybeThrowCppxdata.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowCppxdata.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowCppxdata.Name.Contains("cppxdata", StringComparison.Ordinal));

        // [handlerMap] for Dllx64_CppxdataUsage::MaybeThrow
        var Dllx64_CppxdataUsage_MaybeThrowHandlerMap = (HandlerMapSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3A88];
        Assert.AreEqual(Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, Dllx64_CppxdataUsage_MaybeThrowHandlerMap.TargetStartRVA);
        Assert.AreEqual(20u, Dllx64_CppxdataUsage_MaybeThrowHandlerMap.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowHandlerMap.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowHandlerMap.Name.Contains("handlerMap", StringComparison.Ordinal));

        // [ip2State] for Dllx64_CppxdataUsage::MaybeThrow
        var Dllx64_CppxdataUsage_MaybeThrowIpToStateMap = (IpToStateMapSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3A9C];
        Assert.AreEqual(Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, Dllx64_CppxdataUsage_MaybeThrowIpToStateMap.TargetStartRVA);
        Assert.AreEqual(32u, Dllx64_CppxdataUsage_MaybeThrowIpToStateMap.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowIpToStateMap.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowIpToStateMap.Name.Contains("ip2state", StringComparison.Ordinal));

        // [stateUnwindMap] for Dllx64_CppxdataUsage::MaybeThrow
        var Dllx64_CppxdataUsage_MaybeThrowStateUnwindMap = (StateUnwindMapSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3A64];
        Assert.AreEqual(Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, Dllx64_CppxdataUsage_MaybeThrowStateUnwindMap.TargetStartRVA);
        Assert.AreEqual(16u, Dllx64_CppxdataUsage_MaybeThrowStateUnwindMap.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowStateUnwindMap.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowStateUnwindMap.Name.Contains("stateUnwindMap", StringComparison.Ordinal));

        // [tryMap] for Dllx64_CppxdataUsage::MaybeThrow
        var Dllx64_CppxdataUsage_MaybeThrowTryMap = (TryMapSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3A74];
        Assert.AreEqual(Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, Dllx64_CppxdataUsage_MaybeThrowTryMap.TargetStartRVA);
        Assert.AreEqual(20u, Dllx64_CppxdataUsage_MaybeThrowTryMap.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowTryMap.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowTryMap.Name.Contains("tryMap", StringComparison.Ordinal));

        // This tests an unwind symbol that uses UNW_FLAG_NHANDLER.  As noted above, Clang seems to generate unwind info for both the function and for
        // its funclet, but only gives a symbol for the function, so we expect to find *two* unwind info symbols for this function, with different
        // target start RVAs (one at the beginning of the function, one "in the middle" of the symbol where the funclet is.
        var Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind = (UnwindInfoSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3ABC];
        Assert.AreEqual(0x10A0u, Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind.TargetStartRVA);
        Assert.AreEqual(68u, Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind.Name.Contains("Dllx64_CppxdataUsage::MaybeThrowWithSEH", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        var Dllx64_CppxdataUsage_MaybeThrowWithSEH_catchFuncletUnwind = (UnwindInfoSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3B00];
        Assert.AreEqual(0x1130u, Dllx64_CppxdataUsage_MaybeThrowWithSEH_catchFuncletUnwind.TargetStartRVA);
        Assert.AreEqual(8u, Dllx64_CppxdataUsage_MaybeThrowWithSEH_catchFuncletUnwind.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowWithSEH_catchFuncletUnwind.Name.Contains("Dllx64_CppxdataUsage::MaybeThrowWithSEH", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowWithSEH_catchFuncletUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(Dllx64_CppxdataUsage_MaybeThrowWithSEH_catchFuncletUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        var xdataForDebugging = ClangClx64Session.DataCache.XDataSymbolsByRVA.Values.Where(x => x.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal)).ToList();

        // This tests an unwind symbol that uses __GSHandlerCheck
        var isaAvailableInitUnwind = (UnwindInfoSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3CD0];
        Assert.AreEqual(0x2670u, isaAvailableInitUnwind.TargetStartRVA);
        Assert.AreEqual(16u, isaAvailableInitUnwind.Size);
        Assert.IsTrue(isaAvailableInitUnwind.Name.Contains("__isa_available_init", StringComparison.Ordinal));
        Assert.IsTrue(isaAvailableInitUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(isaAvailableInitUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not bea ChainUnwindInfo, just a regular unwind

        // This tests an unwind symbol that uses __C_specific_handler
        var dllMainDispatchUnwind = (UnwindInfoSymbol)ClangClx64Session.DataCache.XDataSymbolsByRVA[0x3C34];
        Assert.AreEqual(0x1AC0u, dllMainDispatchUnwind.TargetStartRVA);
        Assert.AreEqual(32u, dllMainDispatchUnwind.Size);
        Assert.IsTrue(dllMainDispatchUnwind.Name.Contains("dllmain_dispatch", StringComparison.Ordinal));
        Assert.IsTrue(dllMainDispatchUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(dllMainDispatchUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind
    }

    [TestMethod]
    public async Task ClangClx64RelativeRVAsCanBeLoadedFromVTable()
    {
        using var SessionLogger = new NoOpLogger();
        await using var ClangClx64Session = await Session.Create(Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.ClangClx64.dll"),
                                                                 Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.ClangClx64.pdb"),
                                                                 SessionLogger);

        // The RVA passed in as a parameter here is that of Dllx64_DerivedClass::'vftable'
        const int derivedClassVftableRVA = 0x30F8;
        var firstEntry = await ClangClx64Session!.LoadSymbolForVTableSlotAsync(derivedClassVftableRVA, slotIndex: 0);
        Assert.IsNotNull(firstEntry);
        Assert.AreEqual(0x1510u, firstEntry.RVA);
        Assert.AreEqual(firstEntry.Name, ((Symbol)firstEntry).CanonicalName);
        StringAssert.Contains(firstEntry.Name, "Dllx64_DerivedClass", StringComparison.Ordinal);
        StringAssert.Contains(firstEntry.Name, "AVirtualFunction", StringComparison.Ordinal);

        // Get an entry that is from the base class (not overridden in the derived class)
        var secondEntry = await ClangClx64Session.LoadSymbolForVTableSlotAsync(derivedClassVftableRVA, slotIndex: 1);
        Assert.IsNotNull(secondEntry);
        Assert.AreEqual(0x14F0u, secondEntry.RVA);
        Assert.AreEqual(secondEntry.Name, ((Symbol)secondEntry).CanonicalName);
        StringAssert.Contains(secondEntry.Name, "Dllx64_BaseClass", StringComparison.Ordinal);
        StringAssert.Contains(secondEntry.Name, "ASecondVirtualFunction", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Float16Works()
    {
        using var SessionLogger = new NoOpLogger();
        await using var ClangClx64Session = await Session.Create(Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.ClangClx64.dll"),
                                                                 Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.ClangClx64.pdb"),
                                                                 SessionLogger);

        var typeWithClangExtensionsLayouts = await ClangClx64Session!.LoadTypeLayoutsByName("TypeWithClangExtensions", CancellationToken.None);

        Assert.AreEqual(1, typeWithClangExtensionsLayouts.Count);

        // We should see a "_Float16" member, then 2 bytes of padding, then a 4 byte standard float
        var typeWithClangExtensionsLayout = typeWithClangExtensionsLayouts[0];

        Assert.AreEqual(2u, typeWithClangExtensionsLayout.AlignmentWasteExclusive);
        Assert.AreEqual(2u, typeWithClangExtensionsLayout.AlignmentWasteIncludingBaseTypes);
        Assert.AreEqual(0u, typeWithClangExtensionsLayout.UsedForVFPtrsExclusive);
        Assert.IsNull(typeWithClangExtensionsLayout.BaseTypeLayouts);
        Assert.IsNotNull(typeWithClangExtensionsLayout.MemberLayouts);
        Assert.AreEqual(3, typeWithClangExtensionsLayout.MemberLayouts.Count);

        Assert.AreEqual(0u, typeWithClangExtensionsLayout.MemberLayouts[0].Offset);
        Assert.AreEqual(2u, typeWithClangExtensionsLayout.MemberLayouts[0].Size);
        Assert.AreEqual("_Float16", typeWithClangExtensionsLayout.MemberLayouts[0].Type!.Name);
        Assert.AreEqual(2u, typeWithClangExtensionsLayout.MemberLayouts[0].Type!.InstanceSize);
        Assert.IsFalse(typeWithClangExtensionsLayout.MemberLayouts[0].IsAlignmentMember);
        Assert.IsFalse(typeWithClangExtensionsLayout.MemberLayouts[0].IsBitField);
        Assert.IsFalse(typeWithClangExtensionsLayout.MemberLayouts[0].IsTailSlopAlignmentMember);
        Assert.AreEqual("m_Float16", typeWithClangExtensionsLayout.MemberLayouts[0].Name);

        Assert.IsTrue(typeWithClangExtensionsLayout.MemberLayouts[1].IsAlignmentMember);
        Assert.IsFalse(typeWithClangExtensionsLayout.MemberLayouts[1].IsTailSlopAlignmentMember);
        Assert.AreEqual("<alignment padding>", typeWithClangExtensionsLayout.MemberLayouts[1].Name);
        Assert.AreEqual(2u, typeWithClangExtensionsLayout.MemberLayouts[1].Offset);
        Assert.AreEqual(2u, typeWithClangExtensionsLayout.MemberLayouts[0].Size);

        Assert.AreEqual(4u, typeWithClangExtensionsLayout.MemberLayouts[2].Offset);
        Assert.AreEqual(4u, typeWithClangExtensionsLayout.MemberLayouts[2].Size);
        Assert.AreEqual("float", typeWithClangExtensionsLayout.MemberLayouts[2].Type!.Name);
        Assert.AreEqual(4u, typeWithClangExtensionsLayout.MemberLayouts[2].Type!.InstanceSize);
        Assert.AreEqual("m_float", typeWithClangExtensionsLayout.MemberLayouts[2].Name);

        // Also test that _Float16 is used in method names, like the return type and parameter types
        var functions = await ClangClx64Session.EnumerateFunctionsFromUserDefinedType(typeWithClangExtensionsLayout.UserDefinedType, CancellationToken.None);

        Assert.AreEqual(3, functions.Count);

        var getFloat16 = functions.Single(x => x.FunctionName == "GetFloat16");
        Assert.AreEqual("_Float16", getFloat16.FunctionType!.ReturnValueType.Name);
        Assert.AreEqual("_Float16 TypeWithClangExtensions::GetFloat16() const", getFloat16.FormattedName.All);

        var setFloat16 = functions.Single(x => x.FunctionName == "SetFloat16");
        Assert.AreEqual(1, setFloat16.FunctionType!.ArgumentTypes!.Count);
        Assert.AreEqual("_Float16", setFloat16.FunctionType!.ArgumentTypes![0].Name);
        Assert.AreEqual("void TypeWithClangExtensions::SetFloat16(_Float16)", setFloat16.FormattedName.All);
    }

    public static IEnumerable<object[]> DynamicDataSourceForSymbolSourcesSupportedTests => SymbolSourcesSupportedCommonTests.DynamicDataSourceForSymbolSourcesSupportedTests;

    [TestMethod]
    [DynamicData(nameof(DynamicDataSourceForSymbolSourcesSupportedTests))]
    public Task SymbolSourcesSupportedWorks(SymbolSourcesSupported symbolSources) =>
        SymbolSourcesSupportedCommonTests.VerifyNoUnexpectedSymbolTypesCanBeMaterialized(
            Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.ClangClx64.dll"),
            Path.Combine(this.TestContext!.DeploymentDirectory!, "SizeBenchV2.AnalysisEngine.Tests.ClangClx64.pdb"),
            symbolSources,
            this.TestContext!.CancellationTokenSource.Token);
}
