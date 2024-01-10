using System.IO;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace PEParser.Tests;

[DeploymentItem(@"Test PEs\PEParser.Tests.Dllx64.dll")]
[DeploymentItem(@"Test PEs\PEParser.Tests.Dllx64.pdb")]
[TestClass]
public class Dllx64Tests
{
    public TestContext? TestContext { get; set; }
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory!, filename);

    private string BinaryPath => MakePath("PEParser.Tests.Dllx64.dll");
    private string PDBPath => MakePath("PEParser.Tests.Dllx64.pdb");
    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;

    private static long RoundSizeUpTo4ByteAlignment(ISymbol sym) => RoundSizeUpToAlignment(sym.Size, 4);

    private static long RoundSizeUpTo8ByteAlignment(ISymbol sym) => RoundSizeUpToAlignment(sym.Size, 8);

    private static long RoundSizeUpToAlignment(uint size, uint alignment)
    {
        if (size % alignment == 0)
        {
            return size;
        }

        return size + (alignment - (size % alignment));
    }

    [TestMethod]
    public async Task Dllx64PDATAAndXDATACanBeParsed()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        // These are gotten from "link /dump /headers PEParser.Tests.Dllx64.dll" and looking at the "Exception" directory
        Assert.AreEqual(0x7000u, session.DataCache.PDataRVARange!.RVAStart);
        Assert.AreEqual(0x3C0u, session.DataCache.PDataRVARange.Size);

        // We should be discovering two RVA Ranges for xdata symbols for this binary.
        Assert.AreEqual(2, session.DataCache.XDataRVARanges!.Count);

        // The first range is for the cppxdata symbol that is in .rdata (still unclear why some xdata symbols
        // can live outside of .xdata, but they can...).
        var xdataRanges = session.DataCache.XDataRVARanges.OrderBy(range => range.RVAStart);
        Assert.AreEqual(0x4210u, xdataRanges.First().RVAStart);
        Assert.AreEqual(0x28u, xdataRanges.First().Size);

        // The second range is the .xdata COFF Group as found by "link /dump /headers /coffgroup"
        Assert.AreEqual(0x4878u, xdataRanges.Skip(1).First().RVAStart);
        Assert.AreEqual(0x3D8u, xdataRanges.Skip(1).First().Size);

        // These symbols are chosen from looking at the MAP file for symbols of each interesting
        // type, and the properties of those symbols as seen in the MAP file (assumed to be the truth).

        // [chainUnwind] for _RTC_Initialize
        var _RTC_InitializeChainUnwind = (ChainUnwindInfoSymbol)session.DataCache.XDataSymbolsByRVA![0x4ACC];
        Assert.AreEqual(0x28DDu, _RTC_InitializeChainUnwind.TargetStartRVA);
        Assert.AreEqual(20u, _RTC_InitializeChainUnwind.Size);
        Assert.IsTrue(_RTC_InitializeChainUnwind.Name.Contains("_RTC_Initialize", StringComparison.Ordinal));
        Assert.IsTrue(_RTC_InitializeChainUnwind.Name.Contains("chain-unwind", StringComparison.Ordinal));

        // [pdata] for Dllx64_CppxdataUsage::MaybeThrow
        var Dllx64_CppxdataUsage_MaybeThrowPdata = session.DataCache.PDataSymbolsByRVA![0x733C];
        Assert.AreEqual(0x30D0u, Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA);
        Assert.AreEqual(12u, Dllx64_CppxdataUsage_MaybeThrowPdata.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowPdata.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowPdata.Name.Contains("pdata", StringComparison.Ordinal));

        // [cppxdata] for Dllx64_CppxdataUsage::MaybeThrow
        var Dllx64_CppxdataUsage_MaybeThrowCppxdata = (CppXdataSymbol)session.DataCache.XDataSymbolsByRVA[0x4210];
        Assert.AreEqual(Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, Dllx64_CppxdataUsage_MaybeThrowCppxdata.TargetStartRVA);
        //Note that the MAP file suggests this is 48 bytes long, but DevDiv has confirmed that this symbol is
        // always 40 bytes (sizeof(FUNCINFO)).  The other 8 bytes shown in the MAP file is padding.
        // So in this specific case do not trust the MAP file, trust SizeBench.
        Assert.AreEqual(40u, Dllx64_CppxdataUsage_MaybeThrowCppxdata.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowCppxdata.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowCppxdata.Name.Contains("cppxdata", StringComparison.Ordinal));

        // [handlerMap] for Dllx64_CppxdataUsage::MaybeThrow
        var Dllx64_CppxdataUsage_MaybeThrowHandlerMap = (HandlerMapSymbol)session.DataCache.XDataSymbolsByRVA[0x4BE8];
        Assert.AreEqual(Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, Dllx64_CppxdataUsage_MaybeThrowHandlerMap.TargetStartRVA);
        Assert.AreEqual(20u, Dllx64_CppxdataUsage_MaybeThrowHandlerMap.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowHandlerMap.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowHandlerMap.Name.Contains("handlerMap", StringComparison.Ordinal));

        // [ip2State] for Dllx64_CppxdataUsage::MaybeThrow
        var Dllx64_CppxdataUsage_MaybeThrowIpToStateMap = (IpToStateMapSymbol)session.DataCache.XDataSymbolsByRVA[0x4BFC];
        Assert.AreEqual(Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, Dllx64_CppxdataUsage_MaybeThrowIpToStateMap.TargetStartRVA);
        Assert.AreEqual(48u, Dllx64_CppxdataUsage_MaybeThrowIpToStateMap.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowIpToStateMap.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowIpToStateMap.Name.Contains("ip2state", StringComparison.Ordinal));

        // [stateUnwindMap] for Dllx64_CppxdataUsage::MaybeThrow
        var Dllx64_CppxdataUsage_MaybeThrowStateUnwindMap = (StateUnwindMapSymbol)session.DataCache.XDataSymbolsByRVA[0x4BC4];
        Assert.AreEqual(Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, Dllx64_CppxdataUsage_MaybeThrowStateUnwindMap.TargetStartRVA);
        Assert.AreEqual(16u, Dllx64_CppxdataUsage_MaybeThrowStateUnwindMap.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowStateUnwindMap.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowStateUnwindMap.Name.Contains("stateUnwindMap", StringComparison.Ordinal));

        // [tryMap] for Dllx64_CppxdataUsage::MaybeThrow
        var Dllx64_CppxdataUsage_MaybeThrowTryMap = (TryMapSymbol)session.DataCache.XDataSymbolsByRVA[0x4BD4];
        Assert.AreEqual(Dllx64_CppxdataUsage_MaybeThrowPdata.TargetStartRVA, Dllx64_CppxdataUsage_MaybeThrowTryMap.TargetStartRVA);
        Assert.AreEqual(20u, Dllx64_CppxdataUsage_MaybeThrowTryMap.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowTryMap.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowTryMap.Name.Contains("tryMap", StringComparison.Ordinal));

        // [pdata] for DllMain
        var dllMainPdata = session.DataCache.PDataSymbolsByRVA[0x7324];
        Assert.AreEqual(0x2FF0u, dllMainPdata.TargetStartRVA);
        Assert.AreEqual(12u, dllMainPdata.Size);
        Assert.IsTrue(dllMainPdata.Name.Contains("DllMain", StringComparison.Ordinal));
        Assert.IsTrue(dllMainPdata.Name.Contains("pdata", StringComparison.Ordinal));

        // This tests an unwind symbol that uses UNW_FLAG_NHANDLER
        var is_potentially_valid_image_baseUnwind = (UnwindInfoSymbol)session.DataCache.XDataSymbolsByRVA[0x4A50];
        Assert.AreEqual(0x1DA0u, is_potentially_valid_image_baseUnwind.TargetStartRVA);
        Assert.AreEqual(8u, is_potentially_valid_image_baseUnwind.Size);
        Assert.IsTrue(is_potentially_valid_image_baseUnwind.Name.Contains("is_potentially_valid_image_base", StringComparison.Ordinal));
        Assert.IsTrue(is_potentially_valid_image_baseUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(is_potentially_valid_image_baseUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        // This tests an unwind symbol that uses __GSHandlerCheck
        var dllMainUnwind = (UnwindInfoSymbol)session.DataCache.XDataSymbolsByRVA[0x4B5C];
        Assert.AreEqual(0x2FF0u, dllMainUnwind.TargetStartRVA);
        Assert.AreEqual(16u, dllMainUnwind.Size);
        Assert.IsTrue(dllMainUnwind.Name.Contains("DllMain", StringComparison.Ordinal));
        Assert.IsFalse(dllMainUnwind.Name.Contains("dispatch", StringComparison.Ordinal)); // Don't get dllmain_dispatch by mistake here
        Assert.IsTrue(dllMainUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(dllMainUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not bea ChainUnwindInfo, just a regular unwind

        // This tests an unwind symbol that uses __GSHandlerCheck_EH
        var Dllx64_CppxdataUsage_MaybeThrowUnwind = (UnwindInfoSymbol)session.DataCache.XDataSymbolsByRVA[0x4BB0];
        Assert.AreEqual(0x30D0u, Dllx64_CppxdataUsage_MaybeThrowUnwind.TargetStartRVA);
        Assert.AreEqual(20u, Dllx64_CppxdataUsage_MaybeThrowUnwind.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowUnwind.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsFalse(Dllx64_CppxdataUsage_MaybeThrowUnwind.Name.Contains("SEH", StringComparison.Ordinal)); // Don't get MaybeThrowWithSEH by mistake here
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(Dllx64_CppxdataUsage_MaybeThrowUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        // This tests an unwind symbol that uses __GSHandlerCheck_SEH (for Structured Exception Handling with /GS data)
        var Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind = (UnwindInfoSymbol)session.DataCache.XDataSymbolsByRVA[0x4B6C];
        Assert.AreEqual(0x3060u, Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind.TargetStartRVA);
        Assert.AreEqual(52u, Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind.Name.Contains("Dllx64_CppxdataUsage::MaybeThrowWithSEH", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(Dllx64_CppxdataUsage_MaybeThrowWithSEHUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        // This tests an unwind symbol that uses __C_specific_handler
        var dllMainDispatchUnwind = (UnwindInfoSymbol)session.DataCache.XDataSymbolsByRVA[0x4934];
        Assert.AreEqual(0x15A0u, dllMainDispatchUnwind.TargetStartRVA);
        Assert.AreEqual(32u, dllMainDispatchUnwind.Size);
        Assert.IsTrue(dllMainDispatchUnwind.Name.Contains("dllmain_dispatch", StringComparison.Ordinal));
        Assert.IsTrue(dllMainDispatchUnwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(dllMainDispatchUnwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        // This tests an unwind symbol that uses __CxxFrameHandler3
        var Dllx64_CppxdataUsage_MaybeThrowCatch0Unwind = (UnwindInfoSymbol)session.DataCache.XDataSymbolsByRVA[0x4C2C];
        Assert.AreEqual(0x3404u, Dllx64_CppxdataUsage_MaybeThrowCatch0Unwind.TargetStartRVA);
        Assert.AreEqual(16u, Dllx64_CppxdataUsage_MaybeThrowCatch0Unwind.Size);
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowCatch0Unwind.Name.Contains("Dllx64_CppxdataUsage::MaybeThrow", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowCatch0Unwind.Name.Contains("catch$1", StringComparison.Ordinal));
        Assert.IsTrue(Dllx64_CppxdataUsage_MaybeThrowCatch0Unwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(Dllx64_CppxdataUsage_MaybeThrowCatch0Unwind.Name.Contains("chain", StringComparison.Ordinal)); // Should not be a ChainUnwindInfo, just a regular unwind

        // _RTC_Initialize is especially interesting because it has 3 pdata symbols within its single block of code, two of which are ChainUnwindInfo and one
        // of which is a regular UnwindInfo.  I don't know why it's weird like this, but it's a good test case.
        var _RTC_InitializePData = session.DataCache.PDataSymbolsByRVA[0x7294];
        Assert.AreEqual(0x28C0u, _RTC_InitializePData.TargetStartRVA);
        Assert.AreEqual(12u, _RTC_InitializePData.Size);
        Assert.AreEqual("[pdata] _RTC_Initialize()", _RTC_InitializePData.Name);
        var _RTC_Initialize_Unwind = session.DataCache.XDataSymbolsByRVA[0x4AC0];
        Assert.AreEqual(0x28C0u, _RTC_Initialize_Unwind.TargetStartRVA);
        Assert.AreEqual(12u, _RTC_Initialize_Unwind.Size);
        Assert.IsTrue(_RTC_Initialize_Unwind.Name.Contains("_RTC_Initialize", StringComparison.Ordinal));
        Assert.IsTrue(_RTC_Initialize_Unwind.Name.Contains("unwind", StringComparison.Ordinal));
        Assert.IsFalse(_RTC_Initialize_Unwind.Name.Contains("chain-unwind", StringComparison.Ordinal));

        var _RTC_InitializePData2 = session.DataCache.PDataSymbolsByRVA[0x72A0];
        Assert.AreEqual(0x28C0 + 0x1Du, _RTC_InitializePData2.TargetStartRVA); // This is in the middle of the function's block, how odd - but I'm sure it's accurate
        Assert.AreEqual(12u, _RTC_InitializePData2.Size);
        Assert.AreEqual("[pdata] _RTC_Initialize()", _RTC_InitializePData2.Name);
        var _RTC_Initialize_ChainUnwind0 = session.DataCache.XDataSymbolsByRVA[0x4ACC];
        Assert.AreEqual(0x28C0 + 0x1Du, _RTC_Initialize_ChainUnwind0.TargetStartRVA);
        Assert.AreEqual(20u, _RTC_Initialize_ChainUnwind0.Size);
        Assert.IsTrue(_RTC_Initialize_ChainUnwind0.Name.Contains("_RTC_Initialize", StringComparison.Ordinal));
        Assert.IsTrue(_RTC_Initialize_ChainUnwind0.Name.Contains("chain-unwind", StringComparison.Ordinal));

        var _RTC_InitializePData3 = session.DataCache.PDataSymbolsByRVA[0x72AC];
        Assert.AreEqual(0x28C0 + 0x42u, _RTC_InitializePData3.TargetStartRVA); // This is in the middle of the function's block, how odd - but I'm sure it's accurate
        Assert.AreEqual(12u, _RTC_InitializePData3.Size);
        Assert.AreEqual("[pdata] _RTC_Initialize()", _RTC_InitializePData3.Name);
        var _RTC_Initialize_ChainUnwind1 = session.DataCache.XDataSymbolsByRVA[0x4AE0];
        Assert.AreEqual(0x28C0u + 0x42u, _RTC_Initialize_ChainUnwind1.TargetStartRVA);
        Assert.AreEqual(16u, _RTC_Initialize_ChainUnwind1.Size);
        Assert.IsTrue(_RTC_Initialize_ChainUnwind1.Name.Contains("_RTC_Initialize", StringComparison.Ordinal));
        Assert.IsTrue(_RTC_Initialize_ChainUnwind1.Name.Contains("chain-unwind", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Dllx64RSRCCanBeParsed()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken);
        var rsrcSection = sections.Single(section => section.Name == ".rsrc");
        var rsrc01COFFGroup = rsrcSection.COFFGroups.Single(cg => cg.Name == ".rsrc$01");
        var rsrc02COFFGroup = rsrcSection.COFFGroups.Single(cg => cg.Name == ".rsrc$02");

        var rsrcSymbols = await session.EnumerateSymbolsInBinarySection(rsrcSection, this.CancellationToken);

        // The directories and other 'metadata' should consume all the bytes in .rsrc$01.
        var metadataSymbolsSizeSumWithPadding = (uint)rsrcSymbols.Where(sym => sym is not RsrcDataSymbol).Sum(RoundSizeUpTo4ByteAlignment);
        Assert.AreEqual(rsrc01COFFGroup.Size, metadataSymbolsSizeSumWithPadding);

        // The actual data should consume all the bytes in .rsrc$02, though everything is 8-byte-aligned so we round them up to
        // their 8-byte aligned size.
        var dataSymbolsSizeSumWithPadding = (uint)rsrcSymbols.Where(sym => sym is RsrcDataSymbol).Sum(RoundSizeUpTo8ByteAlignment);
        Assert.AreEqual(rsrc02COFFGroup.Size, dataSymbolsSizeSumWithPadding);

        // Assert that we accounted for every byte in the section.  We use VirtualSize for the section because section Size is
        // padded up to the FileAlignment.
        var allSymbolsSizeSumWithPadding = metadataSymbolsSizeSumWithPadding + dataSymbolsSizeSumWithPadding;
        Assert.AreEqual(rsrcSection.VirtualSize, allSymbolsSizeSumWithPadding);

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // RT_RIBBON_XML - tests loading a resource that has a user-named type
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        var ribbonXmlData = rsrcSymbols.OfType<RsrcDataSymbol>().Single(sym => sym.Name.Contains("RT_RIBBON_XML", StringComparison.Ordinal));
        Assert.AreEqual(SymbolComparisonClass.RsrcData, ribbonXmlData.SymbolComparisonClass);
        Assert.AreEqual("English (United States)", ribbonXmlData.Language);
        Assert.AreEqual("RT_RIBBON_XML", ribbonXmlData.ResourceTypeName);
        Assert.AreEqual<uint>(2088, ribbonXmlData.Size); // The size of the mfcribbon-ms file on disk
        Assert.AreEqual<uint>(2088, ribbonXmlData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.UserNamedResource, ribbonXmlData.Win32ResourceType);

        var ribbonXmlString = rsrcSymbols.OfType<RsrcStringSymbol>().Single(sym => sym.Name == "[rsrc string] \"RT_RIBBON_XML\"");
        Assert.AreEqual(SymbolComparisonClass.RsrcString, ribbonXmlString.SymbolComparisonClass);
        Assert.AreEqual<uint>((uint)(2 + (2 * "RT_RIBBON_XML".Length)), ribbonXmlString.Size);
        Assert.AreEqual<uint>((uint)(2 + (2 * "RT_RIBBON_XML".Length)), ribbonXmlString.VirtualSize);
        Assert.AreEqual("RT_RIBBON_XML", ribbonXmlString.String);

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // BITMAP - we test one each from a couple languages, including LANG_NEUTRAL that is special
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        var japaneseBitmapData = rsrcSymbols.OfType<RsrcDataSymbol>().Single(sym => sym.Name.Contains("#103", StringComparison.Ordinal));
        Assert.AreEqual(SymbolComparisonClass.RsrcData, japaneseBitmapData.SymbolComparisonClass);
        Assert.AreEqual("Japanese (Japan)", japaneseBitmapData.Language);
        Assert.AreEqual("BITMAP", japaneseBitmapData.ResourceTypeName);
        // We expect to find all the bytes in bitmap1.bmp, except the BITMAPINFOHEADER (14 bytes), as described in this blog post:
        // https://devblogs.microsoft.com/oldnewthing/20091211-00/?p=15693
        Assert.AreEqual<uint>(1270 - 14, japaneseBitmapData.Size);
        Assert.AreEqual<uint>(1270 - 14, japaneseBitmapData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.BITMAP, japaneseBitmapData.Win32ResourceType);

        var englishBitmapData = rsrcSymbols.OfType<RsrcDataSymbol>().Single(sym => sym.Name.Contains("#105", StringComparison.Ordinal));
        Assert.AreEqual(SymbolComparisonClass.RsrcData, englishBitmapData.SymbolComparisonClass);
        Assert.AreEqual("English (United States)", englishBitmapData.Language);
        Assert.AreEqual("BITMAP", englishBitmapData.ResourceTypeName);
        // We expect to find all the bytes in toolbar1.bmp, except the BITMAPINFOHEADER (14 bytes), as described in this blog post:
        // https://devblogs.microsoft.com/oldnewthing/20091211-00/?p=15693
        Assert.AreEqual<uint>(238 - 14, englishBitmapData.Size);
        Assert.AreEqual<uint>(238 - 14, englishBitmapData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.BITMAP, englishBitmapData.Win32ResourceType);

        var langNeutralBitmapData = rsrcSymbols.OfType<RsrcDataSymbol>().Single(sym => sym.Name.Contains("#108", StringComparison.Ordinal));
        Assert.AreEqual(SymbolComparisonClass.RsrcData, langNeutralBitmapData.SymbolComparisonClass);
        Assert.AreEqual("LANG_NEUTRAL", langNeutralBitmapData.Language);
        Assert.AreEqual("BITMAP", langNeutralBitmapData.ResourceTypeName);
        // We expect to find all the bytes in toolbar1.bmp, except the BITMAPINFOHEADER (14 bytes), as described in this blog post:
        // https://devblogs.microsoft.com/oldnewthing/20091211-00/?p=15693
        Assert.AreEqual<uint>(1270 - 14, langNeutralBitmapData.Size);
        Assert.AreEqual<uint>(1270 - 14, langNeutralBitmapData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.BITMAP, langNeutralBitmapData.Win32ResourceType);

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // ICON
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // We should not have directly gotten any Icon symbols back, they're all inside the GroupIcon
        Assert.AreEqual(0, rsrcSymbols.OfType<RsrcIconDataSymbol>().Count());

        var groupIconData = rsrcSymbols.OfType<RsrcGroupIconDataSymbol>().Single(sym => sym.Name.Contains("#101", StringComparison.Ordinal));
        Assert.AreEqual(SymbolComparisonClass.RsrcData, groupIconData.SymbolComparisonClass);
        Assert.AreEqual("English (United States)", groupIconData.Language);
        Assert.AreEqual("GROUP_ICON", groupIconData.ResourceTypeName);
        Assert.AreEqual<uint>(45444, groupIconData.Size); // I'm not sure why, but this is a few bytes smaller than the .ico file is on disk.  But I've checked in every way I can think, and it *seems* to be correct as SizeBench is parsing it.
        Assert.AreEqual<uint>(45444, groupIconData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.GROUP_ICON, groupIconData.Win32ResourceType);

        Assert.AreEqual(9, groupIconData.Icons.Count);

        // #1, 256x256, 8bpp
        var icon = groupIconData.Icons.Single(i => i.Width == 256 && i.Height == 256 && i.BitsPerPixel == 8);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, icon.SymbolComparisonClass);
        Assert.AreEqual("Resource '#1' (ICON, English (United States)) 256x256 8bpp", icon.Name);
        Assert.AreEqual("English (United States)", icon.Language);
        Assert.AreEqual("ICON", icon.ResourceTypeName);
        Assert.AreEqual<uint>(2835, icon.Size);
        Assert.AreEqual<uint>(2835, icon.VirtualSize);
        Assert.AreEqual(Win32ResourceType.ICON, icon.Win32ResourceType);

        // #2, 48x48, 8bpp
        icon = groupIconData.Icons.Single(i => i.Width == 48 && i.Height == 48 && i.BitsPerPixel == 8);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, icon.SymbolComparisonClass);
        Assert.AreEqual("Resource '#2' (ICON, English (United States)) 48x48 8bpp", icon.Name);
        Assert.AreEqual("English (United States)", icon.Language);
        Assert.AreEqual("ICON", icon.ResourceTypeName);
        Assert.AreEqual<uint>(3752, icon.Size);
        Assert.AreEqual<uint>(3752, icon.VirtualSize);
        Assert.AreEqual(Win32ResourceType.ICON, icon.Win32ResourceType);

        // #3, 32x32, 8bpp
        icon = groupIconData.Icons.Single(i => i.Width == 32 && i.Height == 32 && i.BitsPerPixel == 8);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, icon.SymbolComparisonClass);
        Assert.AreEqual("Resource '#3' (ICON, English (United States)) 32x32 8bpp", icon.Name);
        Assert.AreEqual("English (United States)", icon.Language);
        Assert.AreEqual("ICON", icon.ResourceTypeName);
        Assert.AreEqual<uint>(2216, icon.Size);
        Assert.AreEqual<uint>(2216, icon.VirtualSize);
        Assert.AreEqual(Win32ResourceType.ICON, icon.Win32ResourceType);

        // #4, 16x16, 8bpp
        icon = groupIconData.Icons.Single(i => i.Width == 16 && i.Height == 16 && i.BitsPerPixel == 8);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, icon.SymbolComparisonClass);
        Assert.AreEqual("Resource '#4' (ICON, English (United States)) 16x16 8bpp", icon.Name);
        Assert.AreEqual("English (United States)", icon.Language);
        Assert.AreEqual("ICON", icon.ResourceTypeName);
        Assert.AreEqual<uint>(1384, icon.Size);
        Assert.AreEqual<uint>(1384, icon.VirtualSize);
        Assert.AreEqual(Win32ResourceType.ICON, icon.Win32ResourceType);

        // #5, 256x256, 32bpp
        icon = groupIconData.Icons.Single(i => i.Width == 256 && i.Height == 256 && i.BitsPerPixel == 32);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, icon.SymbolComparisonClass);
        Assert.AreEqual("Resource '#5' (ICON, English (United States)) 256x256 32bpp", icon.Name);
        Assert.AreEqual("English (United States)", icon.Language);
        Assert.AreEqual("ICON", icon.ResourceTypeName);
        Assert.AreEqual<uint>(3146, icon.Size);
        Assert.AreEqual<uint>(3146, icon.VirtualSize);
        Assert.AreEqual(Win32ResourceType.ICON, icon.Win32ResourceType);

        // #6, 64x64, 32bpp
        icon = groupIconData.Icons.Single(i => i.Width == 64 && i.Height == 64 && i.BitsPerPixel == 32);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, icon.SymbolComparisonClass);
        Assert.AreEqual("Resource '#6' (ICON, English (United States)) 64x64 32bpp", icon.Name);
        Assert.AreEqual("English (United States)", icon.Language);
        Assert.AreEqual("ICON", icon.ResourceTypeName);
        Assert.AreEqual<uint>(16936, icon.Size);
        Assert.AreEqual<uint>(16936, icon.VirtualSize);
        Assert.AreEqual(Win32ResourceType.ICON, icon.Win32ResourceType);

        // #7, 48x48, 32bpp
        icon = groupIconData.Icons.Single(i => i.Width == 48 && i.Height == 48 && i.BitsPerPixel == 32);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, icon.SymbolComparisonClass);
        Assert.AreEqual("Resource '#7' (ICON, English (United States)) 48x48 32bpp", icon.Name);
        Assert.AreEqual("English (United States)", icon.Language);
        Assert.AreEqual("ICON", icon.ResourceTypeName);
        Assert.AreEqual<uint>(9640, icon.Size);
        Assert.AreEqual<uint>(9640, icon.VirtualSize);
        Assert.AreEqual(Win32ResourceType.ICON, icon.Win32ResourceType);

        // #8, 32x32, 32bpp
        icon = groupIconData.Icons.Single(i => i.Width == 32 && i.Height == 32 && i.BitsPerPixel == 32);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, icon.SymbolComparisonClass);
        Assert.AreEqual("Resource '#8' (ICON, English (United States)) 32x32 32bpp", icon.Name);
        Assert.AreEqual("English (United States)", icon.Language);
        Assert.AreEqual("ICON", icon.ResourceTypeName);
        Assert.AreEqual<uint>(4264, icon.Size);
        Assert.AreEqual<uint>(4264, icon.VirtualSize);
        Assert.AreEqual(Win32ResourceType.ICON, icon.Win32ResourceType);

        // #9, 64x64, 32bpp
        icon = groupIconData.Icons.Single(i => i.Width == 16 && i.Height == 16 && i.BitsPerPixel == 32);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, icon.SymbolComparisonClass);
        Assert.AreEqual("Resource '#9' (ICON, English (United States)) 16x16 32bpp", icon.Name);
        Assert.AreEqual("English (United States)", icon.Language);
        Assert.AreEqual("ICON", icon.ResourceTypeName);
        Assert.AreEqual<uint>(1128, icon.Size);
        Assert.AreEqual<uint>(1128, icon.VirtualSize);
        Assert.AreEqual(Win32ResourceType.ICON, icon.Win32ResourceType);

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // MENU
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        var menuData = rsrcSymbols.OfType<RsrcDataSymbol>().Single(sym => sym.Name.Contains("#104", StringComparison.Ordinal));
        Assert.AreEqual(SymbolComparisonClass.RsrcData, menuData.SymbolComparisonClass);
        Assert.AreEqual("English (United States)", menuData.Language);
        Assert.AreEqual("MENU", menuData.ResourceTypeName);
        Assert.AreEqual<uint>(78, menuData.Size);
        Assert.AreEqual<uint>(78, menuData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.MENU, menuData.Win32ResourceType);

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // STRINGTABLE
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        var groupStringTableData = rsrcSymbols.OfType<RsrcGroupStringTablesDataSymbol>().Single();
        Assert.AreEqual(SymbolComparisonClass.RsrcData, groupStringTableData.SymbolComparisonClass);
        Assert.AreEqual("English (United States)", groupStringTableData.Language);
        Assert.AreEqual("STRINGTABLE", groupStringTableData.ResourceTypeName);
        Assert.AreEqual<uint>(454 + 200 + 60, groupStringTableData.Size);
        Assert.AreEqual<uint>(454 + 200 + 60, groupStringTableData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.STRINGTABLE, groupStringTableData.Win32ResourceType);
        Assert.AreEqual(3, groupStringTableData.StringTables.Count);

        // These will throw if we fail to find 3 string tables with the right names that match up to what other tools show when walking
        // PE resources naively and showing each STRINGTABLE separately.
        var allStringsAcrossTables = new List<string>();
        var stringTable = groupStringTableData.StringTables.Single(st => st.Name.Contains("#7", StringComparison.Ordinal));
        allStringsAcrossTables.AddRange(stringTable.Strings);
        stringTable = groupStringTableData.StringTables.Single(st => st.Name.Contains("#8", StringComparison.Ordinal));
        allStringsAcrossTables.AddRange(stringTable.Strings);
        stringTable = groupStringTableData.StringTables.Single(st => st.Name.Contains("#313", StringComparison.Ordinal));
        allStringsAcrossTables.AddRange(stringTable.Strings);

        // I don't honestly care which table contains which string, so for testing we'll just check that all these strings are somewhere
        // in the group.
        Assert.AreEqual(18, allStringsAcrossTables.Count);
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 1");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 2");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 3");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 4");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 5");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 6");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 7");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 8");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 9");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 10");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 11");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 12");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 13");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 14");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 15");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 16");
        CollectionAssert.Contains(allStringsAcrossTables, "Test string 17");
        CollectionAssert.Contains(allStringsAcrossTables, "A really long string With newlines in it \nAnd more newlines \nAnd \thorizontal tab");

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CURSOR
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // We should not have directly gotten any Cursor symbols back, they're all inside the GroupCursor
        Assert.AreEqual(0, rsrcSymbols.OfType<RsrcCursorDataSymbol>().Count());

        // CURSOR2NAMEDRESOURCE
        var groupCursorData = rsrcSymbols.OfType<RsrcGroupCursorDataSymbol>().Single(sym => sym.Name.Contains("CURSOR2NAMEDRESOURCE", StringComparison.Ordinal));
        Assert.AreEqual(SymbolComparisonClass.RsrcData, groupCursorData.SymbolComparisonClass);
        Assert.AreEqual("English (United Kingdom)", groupCursorData.Language);
        Assert.AreEqual("GROUP_CURSOR", groupCursorData.ResourceTypeName);
        var expectedGroupCursorSize = (uint)(RoundSizeUpToAlignment(48, 8) + RoundSizeUpToAlignment(308, 8) + RoundSizeUpToAlignment(4148, 8) + RoundSizeUpToAlignment(19500, 8));
        Assert.AreEqual<uint>(expectedGroupCursorSize, groupCursorData.Size);
        Assert.AreEqual<uint>(expectedGroupCursorSize, groupCursorData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.GROUP_CURSOR, groupCursorData.Win32ResourceType);

        Assert.AreEqual(3, groupCursorData.Cursors.Count);

        // 32x32, 1bpp
        var cursor = groupCursorData.Cursors.Single(i => i.Width == 32 && i.Height == 32 && i.BitsPerPixel == 1);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, cursor.SymbolComparisonClass);
        Assert.AreEqual("Resource '#4' (CURSOR, English (United Kingdom)) 32x32 1bpp", cursor.Name);
        Assert.AreEqual("English (United Kingdom)", cursor.Language);
        Assert.AreEqual("CURSOR", cursor.ResourceTypeName);
        Assert.AreEqual<uint>(308, cursor.Size);
        Assert.AreEqual<uint>(308, cursor.VirtualSize);
        Assert.AreEqual(Win32ResourceType.CURSOR, cursor.Win32ResourceType);

        // 128x128, 1bpp
        cursor = groupCursorData.Cursors.Single(i => i.Width == 128 && i.Height == 128 && i.BitsPerPixel == 1);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, cursor.SymbolComparisonClass);
        Assert.AreEqual("Resource '#5' (CURSOR, English (United Kingdom)) 128x128 1bpp", cursor.Name);
        Assert.AreEqual("English (United Kingdom)", cursor.Language);
        Assert.AreEqual("CURSOR", cursor.ResourceTypeName);
        Assert.AreEqual<uint>(4148, cursor.Size);
        Assert.AreEqual<uint>(4148, cursor.VirtualSize);
        Assert.AreEqual(Win32ResourceType.CURSOR, cursor.Win32ResourceType);

        // 128x128, 8bpp
        cursor = groupCursorData.Cursors.Single(i => i.Width == 128 && i.Height == 128 && i.BitsPerPixel == 8);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, cursor.SymbolComparisonClass);
        Assert.AreEqual("Resource '#6' (CURSOR, English (United Kingdom)) 128x128 8bpp", cursor.Name);
        Assert.AreEqual("English (United Kingdom)", cursor.Language);
        Assert.AreEqual("CURSOR", cursor.ResourceTypeName);
        Assert.AreEqual<uint>(19500, cursor.Size);
        Assert.AreEqual<uint>(19500, cursor.VirtualSize);
        Assert.AreEqual(Win32ResourceType.CURSOR, cursor.Win32ResourceType);

        // Now we'll check IDC_CURSOR1 (cursor1.cur)
        groupCursorData = rsrcSymbols.OfType<RsrcGroupCursorDataSymbol>().Single(sym => sym.Name.Contains("#101", StringComparison.Ordinal));
        Assert.AreEqual(SymbolComparisonClass.RsrcData, groupCursorData.SymbolComparisonClass);
        Assert.AreEqual("English (United States)", groupCursorData.Language);
        Assert.AreEqual("GROUP_CURSOR", groupCursorData.ResourceTypeName);
        expectedGroupCursorSize = (uint)(20 + RoundSizeUpToAlignment(308, 8));
        Assert.AreEqual<uint>(expectedGroupCursorSize, groupCursorData.Size);
        Assert.AreEqual<uint>(expectedGroupCursorSize, groupCursorData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.GROUP_CURSOR, groupCursorData.Win32ResourceType);

        Assert.AreEqual(1, groupCursorData.Cursors.Count);

        // 32x32, 1bpp
        cursor = groupCursorData.Cursors.Single(i => i.Width == 32 && i.Height == 32 && i.BitsPerPixel == 1);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, cursor.SymbolComparisonClass);
        Assert.AreEqual("Resource '#1' (CURSOR, English (United States)) 32x32 1bpp", cursor.Name);
        Assert.AreEqual("English (United States)", cursor.Language);
        Assert.AreEqual("CURSOR", cursor.ResourceTypeName);
        Assert.AreEqual<uint>(308, cursor.Size);
        Assert.AreEqual<uint>(308, cursor.VirtualSize);
        Assert.AreEqual(Win32ResourceType.CURSOR, cursor.Win32ResourceType);

        // Now we'll check IDC_CURSOR2 (cur00001.cur)
        groupCursorData = rsrcSymbols.OfType<RsrcGroupCursorDataSymbol>().Single(sym => sym.Name.Contains("#102", StringComparison.Ordinal));
        Assert.AreEqual(SymbolComparisonClass.RsrcData, groupCursorData.SymbolComparisonClass);
        Assert.AreEqual("English (United States)", groupCursorData.Language);
        Assert.AreEqual("GROUP_CURSOR", groupCursorData.ResourceTypeName);
        expectedGroupCursorSize = (uint)(34 + RoundSizeUpToAlignment(308, 8) + RoundSizeUpToAlignment(204844, 8));
        Assert.AreEqual<uint>(expectedGroupCursorSize, groupCursorData.Size);
        Assert.AreEqual<uint>(expectedGroupCursorSize, groupCursorData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.GROUP_CURSOR, groupCursorData.Win32ResourceType);

        Assert.AreEqual(2, groupCursorData.Cursors.Count);

        // 32x32, 1bpp
        cursor = groupCursorData.Cursors.Single(i => i.Width == 32 && i.Height == 32 && i.BitsPerPixel == 1);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, cursor.SymbolComparisonClass);
        Assert.AreEqual("Resource '#2' (CURSOR, English (United States)) 32x32 1bpp", cursor.Name);
        Assert.AreEqual("English (United States)", cursor.Language);
        Assert.AreEqual("CURSOR", cursor.ResourceTypeName);
        Assert.AreEqual<uint>(308, cursor.Size);
        Assert.AreEqual<uint>(308, cursor.VirtualSize);
        Assert.AreEqual(Win32ResourceType.CURSOR, cursor.Win32ResourceType);

        // 256x256, 24bpp
        cursor = groupCursorData.Cursors.Single(i => i.Width == 256 && i.Height == 256 && i.BitsPerPixel == 24);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, cursor.SymbolComparisonClass);
        Assert.AreEqual("Resource '#3' (CURSOR, English (United States)) 256x256 24bpp", cursor.Name);
        Assert.AreEqual("English (United States)", cursor.Language);
        Assert.AreEqual("CURSOR", cursor.ResourceTypeName);
        Assert.AreEqual<uint>(204844, cursor.Size);
        Assert.AreEqual<uint>(204844, cursor.VirtualSize);
        Assert.AreEqual(Win32ResourceType.CURSOR, cursor.Win32ResourceType);

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // RCDATA
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        var rcdataData = rsrcSymbols.OfType<RsrcDataSymbol>().Single(sym => sym.Win32ResourceType == Win32ResourceType.RCDATA);
        Assert.IsTrue(rcdataData.Name.Contains("Resource '#5'", StringComparison.Ordinal));
        Assert.AreEqual(SymbolComparisonClass.RsrcData, rcdataData.SymbolComparisonClass);
        Assert.AreEqual("English (United States)", rcdataData.Language);
        Assert.AreEqual("RCDATA", rcdataData.ResourceTypeName);
        Assert.AreEqual<uint>(649748, rcdataData.Size); // The size of CascadiaCode.ttf on-disk
        Assert.AreEqual<uint>(649748, rcdataData.VirtualSize);

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // VERSION
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        var versionData = rsrcSymbols.OfType<RsrcDataSymbol>().Single(sym => sym.Win32ResourceType == Win32ResourceType.VERSION);
        Assert.IsTrue(versionData.Name.Contains("Resource '#1'", StringComparison.Ordinal));
        Assert.AreEqual(SymbolComparisonClass.RsrcData, versionData.SymbolComparisonClass);
        Assert.AreEqual("English (United States)", versionData.Language);
        Assert.AreEqual("VERSION", versionData.ResourceTypeName);
        Assert.AreEqual<uint>(780, versionData.Size);
        Assert.AreEqual<uint>(780, versionData.VirtualSize);


        // Now we'll test trying to lookup the location of an rsrc symbol to see if we find the right lib, compiland, section, and COFF Group.
        // Unfortunately we cannot find the right source file as that information is not persisted through CVTRES so the linker doesn't have
        // any source file info.
        var libs = await session.EnumerateLibs(this.CancellationToken);
        var resLib = libs.Single(l => l.Name.EndsWith(".res", StringComparison.Ordinal));
        var resCompiland = resLib.Compilands.Values.Single();

        var placement = await session.LookupSymbolPlacementInBinary(japaneseBitmapData, this.CancellationToken);
        Assert.AreEqual(rsrcSection, placement.BinarySection);
        Assert.AreEqual(rsrc02COFFGroup, placement.COFFGroup);
        Assert.AreEqual(resLib, placement.Lib);
        Assert.AreEqual(resCompiland, placement.Compiland);

        // We'll also try one of the 'group' symbols that we hand-roll-up
        placement = await session.LookupSymbolPlacementInBinary(groupCursorData, this.CancellationToken);
        Assert.AreEqual(rsrcSection, placement.BinarySection);
        Assert.AreEqual(rsrc02COFFGroup, placement.COFFGroup);
        Assert.AreEqual(resLib, placement.Lib);
        Assert.AreEqual(resCompiland, placement.Compiland);

        // And the group string table that we also hand-roll-up
        placement = await session.LookupSymbolPlacementInBinary(groupStringTableData, this.CancellationToken);
        Assert.AreEqual(rsrcSection, placement.BinarySection);
        Assert.AreEqual(rsrc02COFFGroup, placement.COFFGroup);
        Assert.AreEqual(resLib, placement.Lib);
        Assert.AreEqual(resCompiland, placement.Compiland);

        // Lastly we'll try one of the metadata symbols to confirm we find it in the right COFF Group (different than the data)
        placement = await session.LookupSymbolPlacementInBinary(rsrcSymbols.First(sym => sym is not RsrcDataSymbol), this.CancellationToken);
        Assert.AreEqual(rsrcSection, placement.BinarySection);
        Assert.AreEqual(rsrc01COFFGroup, placement.COFFGroup);
        Assert.AreEqual(resLib, placement.Lib);
        Assert.AreEqual(resCompiland, placement.Compiland);
    }

    [TestMethod]
    public async Task Dllx64ImportSymbolsCanBeParsed()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);

        var VCRuntime140DImportDescriptor = (ImportDescriptorSymbol)(await session.LoadSymbolByRVA(0x4CB8))!;
        var IsProcessorFeaturePresentImportThunk = (ImportThunkSymbol)(await session.LoadSymbolByRVA(0x4D70))!;
        var DebugBreakImportByName = (ImportByNameSymbol)(await session.LoadSymbolByRVA(0x4E38))!;

        var placement = await session.LookupSymbolPlacementInBinary(VCRuntime140DImportDescriptor, this.CancellationToken);

        Assert.AreEqual(".rdata", placement.BinarySection!.Name);
        Assert.AreEqual(".idata$2", placement.COFFGroup!.Name);
        Assert.AreEqual("VCRUNTIME140D.dll", placement.Compiland!.ShortName);
        Assert.AreEqual("vcruntimed", placement.Lib!.ShortName);
        Assert.IsNull(placement.SourceFile);

        placement = await session.LookupSymbolPlacementInBinary(IsProcessorFeaturePresentImportThunk, this.CancellationToken);

        Assert.AreEqual(".rdata", placement.BinarySection!.Name);
        Assert.AreEqual(".idata$4", placement.COFFGroup!.Name);
        Assert.AreEqual("Import:KERNEL32.dll", placement.Compiland!.ShortName);
        Assert.AreEqual("kernel32", placement.Lib!.ShortName);
        Assert.IsNull(placement.SourceFile);

        placement = await session.LookupSymbolPlacementInBinary(DebugBreakImportByName, this.CancellationToken);

        Assert.AreEqual(".rdata", placement.BinarySection!.Name);
        Assert.AreEqual(".idata$6", placement.COFFGroup!.Name);
        Assert.AreEqual("Import:KERNEL32.dll", placement.Compiland!.ShortName);
        Assert.AreEqual("kernel32", placement.Lib!.ShortName);
        Assert.IsNull(placement.SourceFile);
    }

    [TestMethod]
    public async Task Dllx64RelativeRVAsCanBeLoadedFromVTable()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        // The RVA passed in as a parameter here is that of Dllx64_DerivedClass::'vftable'
        // When updating this test, just get this value from the map file.
        const int derivedClassVftableRVA = 0x41D8;
        var firstEntry = await session.LoadSymbolForVTableSlotAsync(derivedClassVftableRVA, slotIndex: 0);
        Assert.IsNotNull(firstEntry);
        Assert.AreEqual(0x10C0u, firstEntry.RVA);
        Assert.AreEqual(firstEntry.Name, ((Symbol)firstEntry).CanonicalName);
        StringAssert.Contains(firstEntry.Name, "Dllx64_DerivedClass", StringComparison.Ordinal);
        StringAssert.Contains(firstEntry.Name, "AVirtualFunction", StringComparison.Ordinal);

        var secondEntry = await session.LoadSymbolForVTableSlotAsync(derivedClassVftableRVA, slotIndex: 1);
        Assert.IsNotNull(secondEntry);
        Assert.AreEqual(0x1060u, secondEntry.RVA);
        Assert.AreEqual(secondEntry.Name, ((Symbol)secondEntry).CanonicalName);
        StringAssert.Contains(secondEntry.Name, "Dllx64_BaseClass", StringComparison.Ordinal);
        StringAssert.Contains(secondEntry.Name, "ASecondVirtualFunction", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Dllx64LoadSymbolByRVAWorks()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);
        // RVAs here are from the MAP file

        // Try to load some from .text, basic code stuff that's easy
        var DllMainSymbol = await session.LoadSymbolByRVA(0x2FF0);
        Assert.IsNotNull(DllMainSymbol);
        var Dllx64_BaseClassCtorSymbol = await session.LoadSymbolByRVA(0x1020);
        Assert.IsNotNull(Dllx64_BaseClassCtorSymbol);

        Assert.AreEqual("DllMain", (DllMainSymbol as SimpleFunctionCodeSymbol)!.FunctionName);
        Assert.AreEqual(DllMainSymbol.Name, ((SimpleFunctionCodeSymbol)DllMainSymbol).CanonicalName);
        Assert.AreEqual("int DllMain(HINSTANCE__* hModule, unsigned long ul_reason_for_call, void* lpReserved)", (DllMainSymbol as SimpleFunctionCodeSymbol)!.FullName);
        Assert.AreEqual(112u, DllMainSymbol.Size);
        Assert.AreEqual(0x2FF0u, DllMainSymbol.RVA);

        StringAssert.Contains(Dllx64_BaseClassCtorSymbol.Name, "Dllx64_BaseClass::Dllx64_BaseClass", StringComparison.Ordinal);
        // Note the MAP file says this is 48 bytes, but as far as I can tell that's due to padding in the binary
        // between functions.  This function really is 42 bytes, so this Assert is correct despite the MAP file
        // saying otherwise.
        Assert.AreEqual(42u, Dllx64_BaseClassCtorSymbol.Size);
        Assert.AreEqual(0x1020u, Dllx64_BaseClassCtorSymbol.RVA);

        // Try to load some PE symbols since those are parsed out differently than DIA symbols.
        
        // PDATA
        var PDataForDllx64_DerivedClass_AVirtualFunctionSymbol = await session.LoadSymbolByRVA(0x700C);

        // XDATA
        var UnwindforDllx64_DerivedClass_AVirtualFunctionSymbol = await session.LoadSymbolByRVA(0x4880);

        // RSRC, including some we hand-roll-up, metadata and normal RSRC data
        var groupCursorData = (RsrcGroupCursorDataSymbol)(await session.LoadSymbolByRVA(0xE6E88))!;
        var groupStringTableData = (RsrcGroupStringTablesDataSymbol)(await session.LoadSymbolByRVA(0x15BB0))!;
        var japaneseBitmapData = (RsrcDataSymbol)(await session.LoadSymbolByRVA(0x9700))!;
        var rsrcDirectoryRoot = (RsrcDirectorySymbol)(await session.LoadSymbolByRVA(0x9000))!;

        // IDATA
        var VCRuntime140DImportDescriptor = (ImportDescriptorSymbol)(await session.LoadSymbolByRVA(0x4CB8))!;
        var IsProcessorFeaturePresentImportThunk = (ImportThunkSymbol)(await session.LoadSymbolByRVA(0x4D70))!;
        var DebugBreakImportByName = (ImportByNameSymbol)(await session.LoadSymbolByRVA(0x4E38))!;

        StringAssert.Contains(PDataForDllx64_DerivedClass_AVirtualFunctionSymbol!.Name, "[pdata]", StringComparison.Ordinal);
        StringAssert.Contains(PDataForDllx64_DerivedClass_AVirtualFunctionSymbol.Name, "Dllx64_DerivedClass::AVirtualFunction", StringComparison.Ordinal);
        Assert.AreEqual(12u, PDataForDllx64_DerivedClass_AVirtualFunctionSymbol.Size);
        Assert.AreEqual(0x700Cu, PDataForDllx64_DerivedClass_AVirtualFunctionSymbol.RVA);

        StringAssert.Contains(UnwindforDllx64_DerivedClass_AVirtualFunctionSymbol!.Name, "[unwind]", StringComparison.Ordinal);
        StringAssert.Contains(UnwindforDllx64_DerivedClass_AVirtualFunctionSymbol.Name, "Dllx64_DerivedClass::AVirtualFunction", StringComparison.Ordinal);
        Assert.AreEqual(8u, UnwindforDllx64_DerivedClass_AVirtualFunctionSymbol.Size);
        Assert.AreEqual(0x4880u, UnwindforDllx64_DerivedClass_AVirtualFunctionSymbol.RVA);

        StringAssert.Contains(groupCursorData.Name, "CURSOR2NAMEDRESOURCE", StringComparison.Ordinal);
        Assert.AreEqual(SymbolComparisonClass.RsrcData, groupCursorData.SymbolComparisonClass);
        Assert.AreEqual("English (United Kingdom)", groupCursorData.Language);
        Assert.AreEqual("GROUP_CURSOR", groupCursorData.ResourceTypeName);
        var expectedGroupCursorSize = (uint)(RoundSizeUpToAlignment(48, 8) + RoundSizeUpToAlignment(308, 8) + RoundSizeUpToAlignment(4148, 8) + RoundSizeUpToAlignment(19500, 8));
        Assert.AreEqual<uint>(expectedGroupCursorSize, groupCursorData.Size);
        Assert.AreEqual<uint>(expectedGroupCursorSize, groupCursorData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.GROUP_CURSOR, groupCursorData.Win32ResourceType);
        Assert.AreEqual(3, groupCursorData.Cursors.Count);

        Assert.AreEqual(SymbolComparisonClass.RsrcData, groupStringTableData.SymbolComparisonClass);
        Assert.AreEqual("English (United States)", groupStringTableData.Language);
        Assert.AreEqual("STRINGTABLE", groupStringTableData.ResourceTypeName);
        Assert.AreEqual<uint>(454 + 200 + 60, groupStringTableData.Size);
        Assert.AreEqual<uint>(454 + 200 + 60, groupStringTableData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.STRINGTABLE, groupStringTableData.Win32ResourceType);
        Assert.AreEqual(3, groupStringTableData.StringTables.Count);

        Assert.AreEqual(SymbolComparisonClass.RsrcData, japaneseBitmapData.SymbolComparisonClass);
        Assert.AreEqual("Japanese (Japan)", japaneseBitmapData.Language);
        Assert.AreEqual("BITMAP", japaneseBitmapData.ResourceTypeName);
        // We expect to find all the bytes in bitmap1.bmp, except the BITMAPINFOHEADER (14 bytes), as described in this blog post:
        // https://devblogs.microsoft.com/oldnewthing/20091211-00/?p=15693
        Assert.AreEqual<uint>(1270 - 14, japaneseBitmapData.Size);
        Assert.AreEqual<uint>(1270 - 14, japaneseBitmapData.VirtualSize);
        Assert.AreEqual(Win32ResourceType.BITMAP, japaneseBitmapData.Win32ResourceType);

        Assert.AreEqual(SymbolComparisonClass.RsrcDirectory, rsrcDirectoryRoot.SymbolComparisonClass);
        Assert.AreEqual("[rsrc directory] L0 (Root)", rsrcDirectoryRoot.Name);
        Assert.AreEqual<uint>(104, rsrcDirectoryRoot.Size);
        Assert.AreEqual<uint>(104, rsrcDirectoryRoot.VirtualSize);

        Assert.AreEqual(SymbolComparisonClass.ImportDescriptor, VCRuntime140DImportDescriptor.SymbolComparisonClass);
        Assert.AreEqual(20u, VCRuntime140DImportDescriptor.Size);
        Assert.AreEqual(20u, VCRuntime140DImportDescriptor.VirtualSize);
        Assert.IsFalse(VCRuntime140DImportDescriptor.IsCOMDATFolded);
        Assert.AreEqual("[import descriptor] VCRUNTIME140D.dll", VCRuntime140DImportDescriptor.Name);

        Assert.AreEqual(SymbolComparisonClass.ImportThunk, IsProcessorFeaturePresentImportThunk.SymbolComparisonClass);
        Assert.AreEqual(8u, IsProcessorFeaturePresentImportThunk.Size);
        Assert.AreEqual(8u, IsProcessorFeaturePresentImportThunk.VirtualSize);
        Assert.IsFalse(IsProcessorFeaturePresentImportThunk.IsCOMDATFolded);
        Assert.AreEqual("[import thunk] KERNEL32.dll IsProcessorFeaturePresent, ordinal 896", IsProcessorFeaturePresentImportThunk.Name);

        Assert.AreEqual(SymbolComparisonClass.ImportByName, DebugBreakImportByName.SymbolComparisonClass);
        Assert.AreEqual((uint)"DebugBreak".Length + 3, DebugBreakImportByName.Size); // +1 byte for the string null terminator and +2 bytes for the ordinal as a ushort
        Assert.AreEqual((uint)"DebugBreak".Length + 3, DebugBreakImportByName.VirtualSize);
        Assert.IsFalse(DebugBreakImportByName.IsCOMDATFolded);
        Assert.AreEqual("KERNEL32.dll", DebugBreakImportByName.ImportDescriptorName);
        Assert.AreEqual(260, DebugBreakImportByName.Ordinal);
        Assert.AreEqual("`string': \"DebugBreak\"", DebugBreakImportByName.Name);
    }
}
