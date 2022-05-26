using System.IO;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.RealPETests.Single_Binary;

[DeploymentItem(@"Test PEs\PEParser.Tests.Dllx86.dll")]
[DeploymentItem(@"Test PEs\PEParser.Tests.Dllx86.pdb")]
[TestClass]
public class Dllx86Tests
{
    public TestContext? TestContext { get; set; }
    private CancellationToken CancellationToken => this.TestContext!.CancellationTokenSource.Token;
    private string MakePath(string filename) => Path.Combine(this.TestContext!.DeploymentDirectory, filename);

    private string BinaryPath => MakePath("PEParser.Tests.Dllx86.dll");
    private string PDBPath => MakePath("PEParser.Tests.Dllx86.pdb");

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
    public async Task Dllx86CanEnumerateAllSymbolsInEveryBinarySectionCOFFGroupCompilandLibAndSourceFile()
    {
        // This ensures coverage of the sanity check in EnumerateSymbolsInRVARangeSessionTask, for every type of enumeration that can happen in a 32-bit DLL.
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);

        var sections = await session.EnumerateBinarySectionsAndCOFFGroups(this.CancellationToken);
        foreach(var section in sections)
        {
            // Don't care about the result, just that we didn't break trying to do it.
            // At some point once all gaps are filled, it'd be nice to assert that the count of symbols is > 0, as we should always discover at least one.
            _ = await session.EnumerateSymbolsInBinarySection(section, this.CancellationToken);

            foreach(var coffGroup in section.COFFGroups)
            {
                if (coffGroup.Name != ".xdata$x") // .xdata$x for 32-bit binaries requires some hand parsing that we don't do yet.
                {
                    _ = await session.EnumerateSymbolsInCOFFGroup(coffGroup, this.CancellationToken);
                }
            }
        }

        var libs = await session.EnumerateLibs(this.CancellationToken);
        foreach(var lib in libs)
        {
            _ = await session.EnumerateSymbolsInLib(lib, this.CancellationToken);
        }

        var compilands = await session.EnumerateCompilands(this.CancellationToken);
        foreach(var compiland in compilands)
        {
            _ = await session.EnumerateSymbolsInCompiland(compiland, this.CancellationToken);
        }

        var sourceFiles = await session.EnumerateSourceFiles(this.CancellationToken);
        foreach(var sourceFile in sourceFiles)
        {
            _ = await session.EnumerateSymbolsInSourceFile(sourceFile, this.CancellationToken);
        }
    }

    [TestMethod]
    public async Task Dllx86RSRCCanBeParsed()
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
        // BITMAP - we test one each from a couple languages
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
    public async Task Dllx86ImportSymbolsCanBeParsed()
    {
        using var logger = new NoOpLogger();
        await using var session = await Session.Create(this.BinaryPath, this.PDBPath, logger);

        // Test an IMAGE_IMPORT_DESCRIPTOR
        var VCRuntime140DImportDescriptor = (ImportDescriptorSymbol)(await session.LoadSymbolByRVA(0x34A4))!;
        Assert.AreEqual(SymbolComparisonClass.ImportDescriptor, VCRuntime140DImportDescriptor.SymbolComparisonClass);
        Assert.AreEqual(20u, VCRuntime140DImportDescriptor.Size);
        Assert.AreEqual(20u, VCRuntime140DImportDescriptor.VirtualSize);
        Assert.IsFalse(VCRuntime140DImportDescriptor.IsCOMDATFolded);
        Assert.AreEqual("[import descriptor] VCRUNTIME140D.dll", VCRuntime140DImportDescriptor.Name);

        var placement = await session.LookupSymbolPlacementInBinary(VCRuntime140DImportDescriptor, this.CancellationToken);

        Assert.AreEqual(".rdata", placement.BinarySection!.Name);
        Assert.AreEqual(".idata$2", placement.COFFGroup!.Name);
        Assert.AreEqual("VCRUNTIME140D.dll", placement.Compiland!.ShortName);
        Assert.AreEqual("vcruntimed", placement.Lib!.ShortName);
        Assert.IsNull(placement.SourceFile);

        // Test an import thunk by name - ideally we'd test an ordinal-only import too, but don't have a convenient test...
        var IsProcessorFeaturePresentImportThunk = (ImportThunkSymbol)(await session.LoadSymbolByRVA(0x3518))!;
        Assert.AreEqual(SymbolComparisonClass.ImportThunk, IsProcessorFeaturePresentImportThunk.SymbolComparisonClass);
        Assert.AreEqual(4u, IsProcessorFeaturePresentImportThunk.Size);
        Assert.AreEqual(4u, IsProcessorFeaturePresentImportThunk.VirtualSize);
        Assert.IsFalse(IsProcessorFeaturePresentImportThunk.IsCOMDATFolded);
        Assert.AreEqual("[import thunk] KERNEL32.dll IsProcessorFeaturePresent, ordinal 893", IsProcessorFeaturePresentImportThunk.Name);

        placement = await session.LookupSymbolPlacementInBinary(IsProcessorFeaturePresentImportThunk, this.CancellationToken);

        Assert.AreEqual(".rdata", placement.BinarySection!.Name);
        Assert.AreEqual(".idata$4", placement.COFFGroup!.Name);
        Assert.AreEqual("Import:KERNEL32.dll", placement.Compiland!.ShortName);
        Assert.AreEqual("kernel32", placement.Lib!.ShortName);
        Assert.IsNull(placement.SourceFile);

        // Test an IMAGE_IMPORT_BY_NAME
        var IsDebuggerPresentImportByName = (ImportByNameSymbol)(await session.LoadSymbolByRVA(0x3788))!;
        Assert.AreEqual(SymbolComparisonClass.ImportByName, IsDebuggerPresentImportByName.SymbolComparisonClass);
        Assert.AreEqual((uint)"IsDebuggerPresent".Length + 3, IsDebuggerPresentImportByName.Size); // +1 byte for the string null terminator and +2 bytes for the ordinal as a ushort
        Assert.AreEqual((uint)"IsDebuggerPresent".Length + 3, IsDebuggerPresentImportByName.VirtualSize);
        Assert.IsFalse(IsDebuggerPresentImportByName.IsCOMDATFolded);
        Assert.AreEqual("KERNEL32.dll", IsDebuggerPresentImportByName.ImportDescriptorName);
        Assert.AreEqual(0x376, IsDebuggerPresentImportByName.Ordinal);
        Assert.AreEqual("`string': \"IsDebuggerPresent\"", IsDebuggerPresentImportByName.Name);

        placement = await session.LookupSymbolPlacementInBinary(IsDebuggerPresentImportByName, this.CancellationToken);

        Assert.AreEqual(".rdata", placement.BinarySection!.Name);
        Assert.AreEqual(".idata$6", placement.COFFGroup!.Name);
        Assert.AreEqual("Import:KERNEL32.dll", placement.Compiland!.ShortName);
        Assert.AreEqual("kernel32", placement.Lib!.ShortName);
        Assert.IsNull(placement.SourceFile);
    }
}
