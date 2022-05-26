using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.PE;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[TestClass]
public sealed class EnumerateLibsAndCompilandsSessionTaskTests : IDisposable
{
    private Mock<ISession> MockSession = new Mock<ISession>();
    private SessionDataCache DataCache = new SessionDataCache();
    private SessionTaskParameters? SessionTaskParameters;
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private List<RawSectionContribution> SectionContributions = new List<RawSectionContribution>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.TestDIAAdapter = new TestDIAAdapter();
        this.DataCache = new SessionDataCache();

        this.SessionTaskParameters = new SessionTaskParameters(
            this.MockSession.Object,
            this.TestDIAAdapter,
            this.DataCache);

        this.TestDIAAdapter.BinarySectionsToFind = new List<BinarySection>()
            {
                // .text  = 0x0000-0x1999
                new BinarySection(this.DataCache, ".text", size: 0x2000, virtualSize: 0x2000, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: DataSectionFlags.MemoryExecute),
                // .rdata = 0x2000-0x2999
                new BinarySection(this.DataCache, ".rdata", size: 0x1000, virtualSize: 0x1000, rva: 0x2000, fileAlignment: 0, sectionAlignment: 0, characteristics: DataSectionFlags.MemoryRead),
                // .data  = 0x3000-0x3499
                new BinarySection(this.DataCache, ".data", size: 0x500, virtualSize: 0x500, rva: 0x3000, fileAlignment: 0, sectionAlignment: 0, characteristics: DataSectionFlags.MemoryWrite | DataSectionFlags.MemoryRead)
            };

        this.TestDIAAdapter.COFFGroupsToFind = new List<COFFGroup>()
            {
                // .text$mn = 0x0000-0x1499
                new COFFGroup(this.DataCache, ".text$mn", size: 0x1500, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: PE.DataSectionFlags.MemoryExecute),
                // .CRT$XCA = 0x2100-0x2199
                new COFFGroup(this.DataCache, ".CRT$XCA", size: 0x100, rva: 0x2100, fileAlignment: 0, sectionAlignment: 0, characteristics: PE.DataSectionFlags.MemoryRead),
                // .text$zz = 0x1500-0x2099
                new COFFGroup(this.DataCache, ".text$zz", size: 0x600, rva: 0x1500, fileAlignment: 0, sectionAlignment: 0, characteristics: PE.DataSectionFlags.MemoryExecute),
            };

        uint nextCompilandSymIndexId = 0;
        this.SectionContributions = new List<RawSectionContribution>()
            {
                new RawSectionContribution(libName: @"c:\dummy\a.lib", compilandName: @"c:\dummy\a1.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x0000, length: 0x500),
                // In .text$zz
                new RawSectionContribution(libName: @"c:\dummy\a.lib", compilandName: @"c:\dummy\a2.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x1800, length: 0x300),
                // In .CRT$XCA
                new RawSectionContribution(libName: @"c:\dummy\b.lib", compilandName: @"c:\dummy\b1.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x2100, length: 0x50),
                // In .text$mn
                new RawSectionContribution(libName: @"c:\dummy\b.lib", compilandName: @"c:\dummy\b2.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x0500, length: 0x1000),
                // In .text$zz
                new RawSectionContribution(libName: @"c:\dummy\b.lib", compilandName: @"c:\dummy\b2.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x1700, length: 0x100),
                // In .text$zz
                new RawSectionContribution(libName: @"c:\dummy\c.lib", compilandName: @"c:\dummy\c.obj" , compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x1500, length: 0x200),
            };

        this.DataCache.PDataRVARange = new RVARange(0, 0);
        this.DataCache.PDataSymbolsByRVA = new SortedList<uint, PDataSymbol>();
        this.DataCache.XDataRVARanges = new RVARangeSet();
        this.DataCache.XDataSymbolsByRVA = new SortedList<uint, XDataSymbol>();
        this.DataCache.RsrcRVARange = new RVARange(0, 0);
        this.DataCache.RsrcSymbolsByRVA = new SortedList<uint, RsrcSymbolBase>();
    }

    [TestMethod]
    public void CanCancelInTheMiddleOfEnumeratingLibs()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        this.TestDIAAdapter.SectionContributionsToFind = this.SectionContributions.EnumerateListButCancelInTheMiddleOfEnumerating(cancellationTokenSource, cancelAfter: 2);

        var task = new EnumerateLibsAndCompilandsSessionTask(this.SessionTaskParameters!,
                                                             cancellationTokenSource.Token,
                                                             null);

        Assert.IsFalse(String.IsNullOrEmpty(task.TaskName));
        OperationCanceledException? capturedException = null;
        IList<Library>? asyncResult = null;
        try
        {
            using var logger = new NoOpLogger();
            asyncResult = task.Execute(logger);
        }
        catch (OperationCanceledException ex)
        {
            capturedException = ex;
        }

        Assert.IsNull(asyncResult);
        Assert.IsNotNull(capturedException);
    }

    [TestMethod]
    public void CanEnumerateMultipleLibsWithCompilandsThatHaveTheSameNameIfItsAnImport()
    {
        TestNameCollisions("Import:ntdll.dll",
                           new string[] { @"c:\dev\a.lib", @"c:\dev\a.lib" });
    }

    [TestMethod]
    public void CanEnumerateMultipleLibsWithCompilandsThatHaveTheSameNameIfLibNamesDiffer()
    {
        TestNameCollisions(@"c:\dummy\a1.obj",
                           new string[] { @"c:\dev\a.lib", @"c:\dev\b.lib" });
    }

    private void TestNameCollisions(string nameToCollide, string[] libNames)
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        uint nextCompilandSymIndexId = 0;
        this.TestDIAAdapter.SectionContributionsToFind = new List<RawSectionContribution>()
            {
                // In .text$mn
                new RawSectionContribution(libName: libNames[0], compilandName: nameToCollide, compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x100, length: 0x500),
                // In .text$zz
                new RawSectionContribution(libName: @"c:\dummy\a.lib", compilandName: @"c:\dummy\a2.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x1550, length: 0x400),
                // In .CRT$XCA
                // Note this has the same objName as the first ContribMetadata in a.lib - this should be
                // allowed since they'll get unique symIndexId's in the generator and that's what's truly
                // unique.
                new RawSectionContribution(libName: libNames[1], compilandName: nameToCollide, compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x2100, length: 0x50),
                // In .text$mn
                new RawSectionContribution(libName: @"c:\dummy\b.lib", compilandName: @"c:\dummy\b2.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x0200, length: 0x1000),
                // In .text$zz
                new RawSectionContribution(libName: @"c:\dummy\c.lib", compilandName: @"c:\dummy\c.obj" , compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x1510, length: 0x200),
            };

        var task = new EnumerateLibsAndCompilandsSessionTask(this.SessionTaskParameters!,
                                                             cancellationTokenSource.Token,
                                                             null);

        Assert.IsFalse(String.IsNullOrEmpty(task.TaskName));
        using var logger = new NoOpLogger();
        Assert.IsNotNull(task.Execute(logger));
    }

    [TestMethod]
    public void WorksInTheSimpleCase()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        this.TestDIAAdapter.SectionContributionsToFind = this.SectionContributions;

        var task = new EnumerateLibsAndCompilandsSessionTask(this.SessionTaskParameters!,
                                                             cancellationTokenSource.Token,
                                                             null);

        Assert.IsFalse(String.IsNullOrEmpty(task.TaskName));
        using var logger = new NoOpLogger();
        var libs = task.Execute(logger);

        Assert.AreEqual(3, libs.Count);

        // Assertions about a.lib
        var aLib = libs.Where(l => l.Name == @"c:\dummy\a.lib").First();
        Assert.AreEqual(1, aLib.SectionContributions.Count);
        Assert.AreEqual(".text", aLib.SectionContributionsByName.Keys.First());
        var sectionContrib = aLib.SectionContributions.First();
        Assert.AreEqual(".text", sectionContrib.Key.Name);
        Assert.AreEqual<ulong>(0x800, sectionContrib.Value.Size); // 0x500 + 0x300 for a1.obj and a2.obj above

        Assert.AreEqual(2, aLib.COFFGroupContributions.Count);
        var cgContrib = aLib.COFFGroupContributions.First();
        Assert.AreEqual(".text$mn", cgContrib.Key.Name);
        Assert.AreEqual<ulong>(0x500, cgContrib.Value.Size);
        cgContrib = aLib.COFFGroupContributions.Skip(1).First();
        Assert.AreEqual(".text$zz", cgContrib.Key.Name);
        Assert.AreEqual<ulong>(0x300, cgContrib.Value.Size);

        Assert.AreEqual(2, aLib.Compilands.Count);
        var compiland = aLib.Compilands[@"c:\dummy\a1.obj"];
        Assert.AreEqual(@"c:\dummy\a1.obj", compiland.Name);
        Assert.AreEqual(0x500u, compiland.Size);
        Assert.AreEqual(1, compiland.SectionContributions.Count);
        Assert.AreEqual(1, compiland.SectionContributionsByName.Count);
        Assert.AreEqual(0x500u, compiland.SectionContributionsByName[".text"].Size);
        Assert.AreEqual(1, compiland.COFFGroupContributions.Count);
        Assert.AreEqual(1, compiland.COFFGroupContributionsByName.Count);
        Assert.AreEqual(0x500u, compiland.COFFGroupContributionsByName[".text$mn"].Size);
        compiland = aLib.Compilands[@"c:\dummy\a2.obj"];
        Assert.AreEqual(@"c:\dummy\a2.obj", compiland.Name);
        Assert.AreEqual(0x300u, compiland.Size);
        Assert.AreEqual(1, compiland.SectionContributions.Count);
        Assert.AreEqual(1, compiland.SectionContributionsByName.Count);
        Assert.AreEqual(0x300u, compiland.SectionContributionsByName[".text"].Size);
        Assert.AreEqual(1, compiland.COFFGroupContributions.Count);
        Assert.AreEqual(1, compiland.COFFGroupContributionsByName.Count);
        Assert.AreEqual(0x300u, compiland.COFFGroupContributionsByName[".text$zz"].Size);

        // Assertions about b.lib
        var bLib = libs.Where(l => l.Name == @"c:\dummy\b.lib").First();
        Assert.AreEqual(2, bLib.SectionContributions.Count);
        Assert.AreEqual(".rdata", bLib.SectionContributionsByName.Keys.First());
        Assert.AreEqual(".text", bLib.SectionContributionsByName.Keys.Skip(1).First());
        sectionContrib = bLib.SectionContributions.First();
        Assert.AreEqual(".rdata", sectionContrib.Key.Name);
        Assert.AreEqual<ulong>(0x50, sectionContrib.Value.Size); // 0x50 for b1.obj above
        sectionContrib = bLib.SectionContributions.Skip(1).First();
        Assert.AreEqual(".text", sectionContrib.Key.Name);
        Assert.AreEqual<ulong>(0x1100, sectionContrib.Value.Size); // 0x1000 + 0x100 for b2.obj (.text$mn) and b2.obj (.text$zz) above

        Assert.AreEqual(3, bLib.COFFGroupContributions.Count);
        //TODO: could assert more about b.lib's COFF Group contributions to strengthen this test

        compiland = bLib.Compilands[@"c:\dummy\b1.obj"];
        Assert.AreEqual(@"c:\dummy\b1.obj", compiland.Name);
        Assert.AreEqual(0x50u, compiland.Size);
        Assert.AreEqual(1, compiland.SectionContributions.Count);
        Assert.AreEqual(1, compiland.SectionContributionsByName.Count);
        Assert.AreEqual(0x50u, compiland.SectionContributionsByName[".rdata"].Size);
        Assert.AreEqual(1, compiland.COFFGroupContributions.Count);
        Assert.AreEqual(1, compiland.COFFGroupContributionsByName.Count);
        Assert.AreEqual(0x50u, compiland.COFFGroupContributionsByName[".CRT$XCA"].Size);
        compiland = bLib.Compilands[@"c:\dummy\b2.obj"];
        Assert.AreEqual(@"c:\dummy\b2.obj", compiland.Name);
        Assert.AreEqual(0x1100u, compiland.Size);
        Assert.AreEqual(1, compiland.SectionContributions.Count);
        Assert.AreEqual(1, compiland.SectionContributionsByName.Count);
        Assert.AreEqual(0x1100u, compiland.SectionContributionsByName[".text"].Size);
        Assert.AreEqual(2, compiland.COFFGroupContributions.Count);
        Assert.AreEqual(2, compiland.COFFGroupContributionsByName.Count);
        Assert.AreEqual(0x1000u, compiland.COFFGroupContributionsByName[".text$mn"].Size);
        Assert.AreEqual(0x100u, compiland.COFFGroupContributionsByName[".text$zz"].Size);

        // Assertions about c.lib
        //TODO: add more assertions about c.lib here, but they're pretty duplicative with a.lib above so this isn't
        //      super important coverage.
    }

    public void Dispose() => this.DataCache.Dispose();
}
