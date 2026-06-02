using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[TestClass]
public sealed class EnumerateDuplicateDataSessionTaskTests : IDisposable
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private SessionTaskParameters? SessionTaskParameters;
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private SessionDataCache DataCache = new SessionDataCache();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();

        this.TestDIAAdapter = new TestDIAAdapter();
        this.DataCache = new SessionDataCache()
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };

        this.SessionTaskParameters = new SessionTaskParameters(
            this.MockSession.Object,
            this.TestDIAAdapter,
            this.DataCache);

        this.TestDIAAdapter.BinarySectionsToFind = new List<BinarySection>()
            {
                // .text  = 0x0000-0x1999
                new BinarySection(this.DataCache, ".text", size: 0x2000, virtualSize: 0x2000, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute),
                // .rdata = 0x2000-0x2999
                new BinarySection(this.DataCache, ".rdata", size: 0x1000, virtualSize: 0x1000, rva: 0x2000, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead),
                // .data  = 0x3000-0x3499
                new BinarySection(this.DataCache, ".data", size: 0x500, virtualSize: 0x500, rva: 0x3000, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemWrite | SectionCharacteristics.MemRead)
            };

        this.TestDIAAdapter.COFFGroupsToFind = new List<COFFGroup>()
            {
                // .text$mn = 0x0000-0x1499
                new COFFGroup(this.DataCache, ".text$mn", size: 0x1500, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute),
                // .CRT$XCA = 0x2100-0x2199
                new COFFGroup(this.DataCache, ".CRT$XCA", size: 0x100, rva: 0x2100, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead),
                // .text$zz = 0x1500-0x2099
                new COFFGroup(this.DataCache, ".text$zz", size: 0x600, rva: 0x1500, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute),
            };

        uint nextCompilandSymIndexId = 0;
        this.TestDIAAdapter.SectionContributionsToFind = new List<RawSectionContribution>()
            {
                // In .text$mn
                new RawSectionContribution(libName: @"c:\dummy\a.lib", compilandName: @"c:\dummy\a1.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x0000, length: 0x500),
                // In .text$zz
                new RawSectionContribution(libName: @"c:\dummy\a.lib", compilandName: @"c:\dummy\a2.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x1800, length: 0x300),
                // In .CRT$XCA
                new RawSectionContribution(libName: @"c:\dummy\b.lib", compilandName: @"c:\dummy\b1.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x2100, length: 0x50),
                // In .text$mn
                new RawSectionContribution(libName: @"c:\dummy\b.lib", compilandName: @"c:\dummy\b2.obj", compilandSymIndexId: nextCompilandSymIndexId, rva: 0x0500, length: 0x1000),
                // In .text$zz
                new RawSectionContribution(libName: @"c:\dummy\b.lib", compilandName: @"c:\dummy\b2.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x1700, length: 0x100),
                // In .text$zz
                new RawSectionContribution(libName: @"c:\dummy\c.lib", compilandName: @"c:\dummy\c.obj" , compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x1500, length: 0x200),
            };

        this.DataCache.PDataHasBeenInitialized = true;
        this.DataCache.XDataHasBeenInitialized = true;
        this.DataCache.RsrcHasBeenInitialized = true;
        this.DataCache.OtherPESymbolsHaveBeenInitialized = true;

        // Every test needs the compilands to be available (to construct the right DataSymbol objects) so run the EnumerateLibsAndCompilandsSessionTask to populate the cache of
        // compilands.
        using var logger = new NoOpLogger();
        new EnumerateLibsAndCompilandsSessionTask(this.SessionTaskParameters,
                                                  CancellationToken.None,
                                                  null).Execute(logger);
    }

    [TestMethod]
    public void CanCancelInTheMiddleOfEnumeratingSymbols()
    {
        using var cts = new CancellationTokenSource();
        SetupDuplicateData(out _, out _, out _, cancelInTheMiddle: true, cts: cts);

        var task = new EnumerateDuplicateDataSessionTask(this.SessionTaskParameters!,
                                                         cts.Token,
                                                         null /*progressReporter*/);

        List<DuplicateDataItem>? duplicates = null;
        OperationCanceledException? exceptionCaught = null;

        try
        {
            using var logger = new NoOpLogger();
            task.Execute(logger);
        }
        catch (OperationCanceledException ex)
        {
            exceptionCaught = ex;
        }

        Assert.IsNull(duplicates);
        Assert.IsNotNull(exceptionCaught);
    }

    [TestMethod]
    public void NoDuplicatesDetectedWhenThereShouldntBeAny()
    {
        SetupDataWithNoDuplicates();

        var task = new EnumerateDuplicateDataSessionTask(this.SessionTaskParameters!,
                                                         CancellationToken.None,
                                                         null /*progressReporter*/);

        Assert.IsFalse(String.IsNullOrEmpty(task.TaskName));
        using var logger = new NoOpLogger();
        var duplicates = task.Execute(logger);

        Assert.AreEqual(0, duplicates.Count);
    }

    [TestMethod]
    public void DuplicateDataIsFoundCorrectly()
    {
        SetupDuplicateData(out var a1Compiland, out var b2Compiland, out var cCompiland);

        var task = new EnumerateDuplicateDataSessionTask(this.SessionTaskParameters!,
                                                         CancellationToken.None,
                                                         null /*progressReporter*/);

        Assert.IsFalse(String.IsNullOrEmpty(task.TaskName));
        using var logger = new NoOpLogger();
        var duplicates = task.Execute(logger);

        Assert.AreEqual(2, duplicates.Count);

        var duplicate1 = duplicates.First(ddi => ddi.Symbol.Name == "test 1");
        var duplicate2 = duplicates.First(ddi => ddi.Symbol.Name == "test 2");

        Assert.AreEqual(1u, duplicate1.Symbol.Size);
        Assert.AreEqual(2u, duplicate1.WastedSize);
        Assert.AreEqual(3, duplicate1.ReferencedIn.Count);
        Assert.IsTrue(duplicate1.ReferencedIn.Contains(a1Compiland));
        Assert.IsTrue(duplicate1.ReferencedIn.Contains(b2Compiland));
        Assert.IsTrue(duplicate1.ReferencedIn.Contains(cCompiland));

        Assert.AreEqual(4u, duplicate2.Symbol.Size);
        Assert.AreEqual(4u, duplicate2.WastedSize);
        Assert.AreEqual(2, duplicate2.ReferencedIn.Count);
        Assert.IsTrue(duplicate2.ReferencedIn.Contains(b2Compiland));
        Assert.IsTrue(duplicate2.ReferencedIn.Contains(cCompiland));
    }


    private void SetupDataWithNoDuplicates()
    {
        var a1Compiland = this.DataCache.AllCompilands!.First(c => c.Name == @"c:\dummy\a1.obj");
        var b2Compiland = this.DataCache.AllCompilands!.First(c => c.Name == @"c:\dummy\b2.obj");

        uint nextDataSymIndex = 0;

        var typeSymbol = new BasicTypeSymbol(this.DataCache, "int", size: 4, symIndexId: nextDataSymIndex++);

        var a1Symbols = new List<StaticDataSymbol>()
            {
                new StaticDataSymbol(this.DataCache, "test 1", rva: 0, size: 1, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: a1Compiland, functionParent: null),
                // Should not be a duplicate because it's not File Static data
                new StaticDataSymbol(this.DataCache, "test 1", rva: 1, size: 1, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsGlobal,
                                     type: typeSymbol, referencedIn: a1Compiland, functionParent: null),
                new StaticDataSymbol(this.DataCache, "test 2", rva: 2, size: 4, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: a1Compiland, functionParent: null)
            };

        var b2Symbols = new List<StaticDataSymbol>()
            {
                // Should not be a duplicate because it has the same RVA as the other "test 2" in a1Symbols - sometimes DIA can find the same symbol multiple
                // ways (with different sym index IDs...*sigh*), so same RVA and same size and same name means it's not a real dupe.
                new StaticDataSymbol(this.DataCache, "test 2", rva: 6, size: 4, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: b2Compiland, functionParent: null),
                // Should not be a duplicate because it has the same name and size as "test 1" in a1Symbols[0], with a different RVA, but the data inside is different
                // (see the Setup on the MockSession CompareData below)
                new StaticDataSymbol(this.DataCache, "test 1", rva: 10, size: 1, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: b2Compiland, functionParent: null),
            };

        this.MockSession.Setup(s => s.CompareData(a1Symbols[0].RVA, b2Symbols[1].RVA, a1Symbols[0].Size)).Returns(false);

        this.TestDIAAdapter.StaticDataSymbolsToFindByCompiland.Add(a1Compiland, a1Symbols);
        this.TestDIAAdapter.StaticDataSymbolsToFindByCompiland.Add(b2Compiland, b2Symbols);
    }

    private void SetupDuplicateData(out Compiland a1Compiland, out Compiland b2Compiland, out Compiland cCompiland, bool cancelInTheMiddle = false, CancellationTokenSource? cts = null)
    {
        a1Compiland = this.DataCache.AllCompilands!.First(c => c.Name == @"c:\dummy\a1.obj");
        b2Compiland = this.DataCache.AllCompilands!.First(c => c.Name == @"c:\dummy\b2.obj");
        cCompiland = this.DataCache.AllCompilands!.First(c => c.Name == @"c:\dummy\c.obj");
        uint nextDataSymIndex = 0;

        var typeSymbol = new BasicTypeSymbol(this.DataCache, "int", size: 4, symIndexId: nextDataSymIndex++);

        var a1Symbols = new List<StaticDataSymbol>()
            {
                new StaticDataSymbol(this.DataCache, "test 1", rva: 0, size: 1, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: a1Compiland, functionParent: null),
                // Should not be a duplicate because it's not File Static data
                new StaticDataSymbol(this.DataCache, "test 1", rva: 1, size: 1, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsGlobal,
                                     type: typeSymbol, referencedIn: a1Compiland, functionParent: null),
                new StaticDataSymbol(this.DataCache, "test 2", rva: 2, size: 4, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: a1Compiland, functionParent: null),
            };

        var b2Symbols = new List<StaticDataSymbol>()
            {
                // Should not be a duplicate because it has the same RVA as the other "test 2" in a1Symbols - sometimes DIA can find the same symbol multiple
                // ways (with different sym index IDs...*sigh*), so same RVA and same size and same name means it's not a real dupe.
                new StaticDataSymbol(this.DataCache, "test 2", rva: 6, size: 4, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: b2Compiland, functionParent: null),
                // Should not be a duplicate because it has the same name and size as "test 1" in a1Symbols[0], with a different RVA, but the data inside is different
                // (see the Setup on the MockSession CompareData below)
                new StaticDataSymbol(this.DataCache, "test 1", rva: 10, size: 1, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: b2Compiland, functionParent: null),
                // SHOULD be a duplicate because it has the same name and size as "test 1" in a1Symbols[0], with a different RVA, and the data inside is the same
                // (see the Setup on the MockSession CompareData below)
                new StaticDataSymbol(this.DataCache, "test 1", rva: 11, size: 1, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: b2Compiland, functionParent: null),
            };

        var cSymbols = new List<StaticDataSymbol>()
            {
                // SHOULD be a duplicate because it has the same name and size as "test 1" in a1Symbols[0], with a different RVA, and the data inside is the same
                // (see the Setup on the MockSession CompareData below)
                new StaticDataSymbol(this.DataCache, "test 1", rva: 12, size: 1, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: cCompiland, functionParent: null),
                // SHOULD be a duplicate because it has the same name and size as "test 2" in b2Symbols[0], with a different RVA, and the data inside is the same
                new StaticDataSymbol(this.DataCache, "test 2", rva: 13, size: 4, isVirtualSize: false, symIndexId: nextDataSymIndex++, dataKind: DataKind.DataIsFileStatic,
                                     type: typeSymbol, referencedIn: cCompiland, functionParent: null),
            };

        this.MockSession.Setup(s => s.CompareData(a1Symbols[0].RVA, b2Symbols[1].RVA, a1Symbols[0].Size)).Returns(false);
        this.MockSession.Setup(s => s.CompareData(a1Symbols[0].RVA, b2Symbols[2].RVA, a1Symbols[0].Size)).Returns(true);
        this.MockSession.Setup(s => s.CompareData(a1Symbols[0].RVA, cSymbols[0].RVA, a1Symbols[0].Size)).Returns(true);
        this.MockSession.Setup(s => s.CompareData(b2Symbols[0].RVA, cSymbols[1].RVA, b2Symbols[0].Size)).Returns(true);

        this.TestDIAAdapter.StaticDataSymbolsToFindByCompiland.Add(a1Compiland, a1Symbols);
        this.TestDIAAdapter.StaticDataSymbolsToFindByCompiland.Add(b2Compiland, cancelInTheMiddle ? b2Symbols.EnumerateListButCancelInTheMiddleOfEnumerating(cts!, cancelAfter: 1) : b2Symbols);
        this.TestDIAAdapter.StaticDataSymbolsToFindByCompiland.Add(cCompiland, cSymbols);
    }

    [TestMethod]
    public void CacheIsReusedAfterOneRunWhenThereIsDuplicateData()
    {
        SetupDuplicateData(out _, out _, out _);

        var task = new EnumerateDuplicateDataSessionTask(this.SessionTaskParameters!,
                                                         CancellationToken.None,
                                                         null /*progressReporter*/);

        Assert.IsNull(this.DataCache.AllDuplicateDataItems);

        using var logger = new NoOpLogger();
        var duplicates = task.Execute(logger);

        Assert.IsNotNull(this.DataCache.AllDuplicateDataItems);

        var duplicates2 = new EnumerateDuplicateDataSessionTask(this.SessionTaskParameters!,
                                                                                    CancellationToken.None,
                                                                                    null /*progressReporter*/).Execute(logger);

        Assert.IsTrue(ReferenceEquals(duplicates, duplicates2));
        Assert.IsTrue(ReferenceEquals(duplicates2, this.DataCache.AllDuplicateDataItems));
    }

    [TestMethod]
    public void CacheIsReusedAfterOneRunWhenThereIsNoDuplicateData()
    {
        SetupDataWithNoDuplicates();

        var task = new EnumerateDuplicateDataSessionTask(this.SessionTaskParameters!,
                                                         CancellationToken.None,
                                                         null /*progressReporter*/);

        Assert.IsNull(this.DataCache.AllDuplicateDataItems);

        using var logger = new NoOpLogger();
        var duplicates = task.Execute(logger);

        Assert.IsNotNull(this.DataCache.AllDuplicateDataItems);

        var duplicates2 = new EnumerateDuplicateDataSessionTask(this.SessionTaskParameters!,
                                                                                    CancellationToken.None,
                                                                                    null /*progressReporter*/).Execute(logger);

        Assert.IsTrue(ReferenceEquals(duplicates, duplicates2));
        Assert.IsTrue(ReferenceEquals(duplicates2, this.DataCache.AllDuplicateDataItems));

    }

    public void Dispose() => this.DataCache.Dispose();
}
