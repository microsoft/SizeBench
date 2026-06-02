using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[TestClass]
public sealed class EnumerateSourceFilesSessionTaskTests : IDisposable
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
                new BinarySection(this.DataCache, ".text", size: 0x2000, virtualSize: 0x2000, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute),
                // .rdata = 0x2000-0x2999
                new BinarySection(this.DataCache, ".rdata", size: 0x1000, virtualSize: 0x1000, rva: 0x2000, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead),
                // .data  = 0x3000-0x3499
                new BinarySection(this.DataCache, ".pdata", size: 0x500, virtualSize: 0x500, rva: 0x3000, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead)
            };

        this.TestDIAAdapter.COFFGroupsToFind = new List<COFFGroup>()
            {
                // .text$mn = 0x0000-0x1499
                new COFFGroup(this.DataCache, ".text$mn", size: 0x1500, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute),
                // .xdata = 0x2100-0x2199
                new COFFGroup(this.DataCache, ".xdata", size: 0x100, rva: 0x2100, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead),
                // .text$zz = 0x1500-0x1999
                new COFFGroup(this.DataCache, ".text$zz", size: 0x500, rva: 0x1500, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute),
                // .pdata = 0x3000-0x3499
                new COFFGroup(this.DataCache, ".pdata", size: 0x500, rva: 0x3000, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute),
            };

        uint nextCompilandSymIndexId = 0;
        this.SectionContributions = new List<RawSectionContribution>()
            {
                // In .text$mn
                new RawSectionContribution(libName: @"c:\dummy\a.lib", compilandName: @"c:\dummy\a1.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x0000, length: 0x500),
                // In .text$zz
                new RawSectionContribution(libName: @"c:\dummy\a.lib", compilandName: @"c:\dummy\a2.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x1700, length: 0x300),
                // In .xdata
                new RawSectionContribution(libName: @"c:\dummy\a.lib", compilandName: @"c:\dummy\a1.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x2100, length: 0x100),
                // In .text$mn
                new RawSectionContribution(libName: @"c:\dummy\a.lib", compilandName: @"c:\dummy\a2.obj", compilandSymIndexId: nextCompilandSymIndexId++, rva: 0x0500, length: 0x1000),
            };

        this.DataCache.PDataHasBeenInitialized = true;
        this.DataCache.XDataHasBeenInitialized = true;
        this.DataCache.RsrcHasBeenInitialized = true;
        this.DataCache.OtherPESymbolsHaveBeenInitialized = true;
    }

    [TestMethod]
    public void CanCancelInTheMiddleOfEnumeratingSourceFiles()
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        this.TestDIAAdapter.SectionContributionsToFind = this.SectionContributions;

        uint nextFileId = 0;
        var SourceFiles = new List<SourceFile>()
            {
                new SourceFile(this.DataCache, "a1.cpp", fileId: nextFileId++, new List<Compiland>() { }),
                new SourceFile(this.DataCache, "a2.cpp", fileId: nextFileId++, new List<Compiland>() { }),
            };

        this.TestDIAAdapter.SourceFilesToFind = SourceFiles.EnumerateListButCancelInTheMiddleOfEnumerating(cancellationTokenSource, cancelAfter: 1);

        var task = new EnumerateSourceFilesSessionTask(this.SessionTaskParameters!,
                                                       cancellationTokenSource.Token,
                                                       null);

        Assert.IsFalse(String.IsNullOrEmpty(task.TaskName));
        OperationCanceledException? capturedException = null;
        IList<SourceFile>? asyncResult = null;
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
    public void WorksInTheSimpleCase()
    {
        // In this test I won't bother trying to test pdata and xdata, they're tedious to set up in a synthetic test and the RealPE tests for
        // source files will cover that path just fine.
        using var cancellationTokenSource = new CancellationTokenSource();

        this.MockSession.SetupGet(s => s.BytesPerWord).Returns(8);
        this.TestDIAAdapter.SectionContributionsToFind = this.SectionContributions;

        // Need to do this here, so that the Compiland objects get created to be passed to the SourceFile constructors
        using var logger = new NoOpLogger();
        var libs = new EnumerateLibsAndCompilandsSessionTask(this.SessionTaskParameters!,
                                                             CancellationToken.None,
                                                             null).Execute(logger);

        var a1Compiland = libs.Single(lib => lib.ShortName == "a").Compilands.Values.Single(compiland => compiland.ShortName == "a1.obj");
        var a2Compiland = libs.Single(lib => lib.ShortName == "a").Compilands.Values.Single(compiland => compiland.ShortName == "a2.obj");

        uint nextFileId = 0;

        var a1SourceFile = new SourceFile(this.DataCache, "a1.cpp", fileId: nextFileId++, new List<Compiland>() { a1Compiland });
        var a2SourceFile = new SourceFile(this.DataCache, "a2.cpp", fileId: nextFileId++, new List<Compiland>() { a2Compiland });
        var aHeaderFile = new SourceFile(this.DataCache, "a.h", fileId: nextFileId++, new List<Compiland>() { a1Compiland, a2Compiland });

        var SourceFiles = new List<SourceFile>()
            {
                a1SourceFile,
                a2SourceFile,
                aHeaderFile
            };

        this.TestDIAAdapter.SourceFilesToFind = SourceFiles;

        this.TestDIAAdapter.RVARangesToFindForSourceFileCompilandCombinations
                      .Add(Tuple.Create(a1SourceFile, a1Compiland),
                           new List<RVARange>() {
                                   // .text$mn, two ranges that should be coalesced to one
                                   RVARange.FromRVAAndSize(0x0, 0x100),
                                   RVARange.FromRVAAndSize(0x100, 0x100),
                           });

        this.TestDIAAdapter.RVARangesToFindForSourceFileCompilandCombinations
                      .Add(Tuple.Create(a2SourceFile, a2Compiland),
                           new List<RVARange>() {
                                   // .text$zz, testing non-contiguous RVA ranges
                                   RVARange.FromRVAAndSize(0x1700, 0x50),
                                   RVARange.FromRVAAndSize(0x1800, 0x50),
                           });

        this.TestDIAAdapter.RVARangesToFindForSourceFileCompilandCombinations
                      .Add(Tuple.Create(aHeaderFile, a1Compiland),
                           new List<RVARange>() {
                                   // .text$mn
                                   RVARange.FromRVAAndSize(0x400, 0x100),
                           });

        this.TestDIAAdapter.RVARangesToFindForSourceFileCompilandCombinations
                      .Add(Tuple.Create(aHeaderFile, a2Compiland),
                           new List<RVARange>() {
                                   // .text$mn, testing one source file contributing to the same COFF group in two compilands (contiguously with a1.obj's contribution from this source file)
                                   RVARange.FromRVAAndSize(0x500, 0x100),
                                   // .text$zz, testing one source file contributing to two COFF groups
                                   RVARange.FromRVAAndSize(0x1850, 0x50),
                           });

        var task = new EnumerateSourceFilesSessionTask(this.SessionTaskParameters!,
                                                       cancellationTokenSource.Token,
                                                       null);

        Assert.IsFalse(String.IsNullOrEmpty(task.TaskName));
        var sourceFiles = task.Execute(logger);

        Assert.AreEqual(3, sourceFiles.Count);

        // Assertions about a1.cpp
        Assert.AreEqual(1, a1SourceFile.SectionContributions.Count);
        Assert.AreEqual(".text", a1SourceFile.SectionContributionsByName.Keys.First());
        var sectionContrib = a1SourceFile.SectionContributions.First();
        Assert.AreEqual(".text", sectionContrib.Key.Name);
        Assert.AreEqual<ulong>(0x200, sectionContrib.Value.Size);
        Assert.AreEqual(1, sectionContrib.Value.RVARanges.Count); // We should have coalesced this into one RVA range, to minimize later processing
        Assert.AreEqual<ulong>(0x0, sectionContrib.Value.RVARanges[0].RVAStart);
        Assert.AreEqual<ulong>(0x200, sectionContrib.Value.RVARanges[0].Size);

        Assert.AreEqual(1, a1SourceFile.COFFGroupContributions.Count);
        var cgContrib = a1SourceFile.COFFGroupContributions.First();
        Assert.AreEqual(".text$mn", cgContrib.Key.Name);
        Assert.AreEqual<ulong>(0x200, cgContrib.Value.Size);
        Assert.AreEqual(1, cgContrib.Value.RVARanges.Count); // We should have coalesced this into one RVA range, to minimize later processing
        Assert.AreEqual<ulong>(0x0, cgContrib.Value.RVARanges[0].RVAStart);
        Assert.AreEqual<ulong>(0x200, cgContrib.Value.RVARanges[0].Size);

        Assert.AreEqual(1, a1SourceFile.Compilands.Count);
        Assert.AreEqual(a1Compiland, a1SourceFile.Compilands.First());

        Assert.AreEqual(1, a1SourceFile.CompilandContributions.Count);
        var compilandContrib = a1SourceFile.CompilandContributions.First();
        Assert.AreEqual(@"c:\dummy\a1.obj", compilandContrib.Key.Name);
        Assert.AreEqual(0x200u, compilandContrib.Value.Size);
        Assert.AreEqual(1, compilandContrib.Value.RVARanges.Count); // We should have coalesced this into one RVA range, to minimize later processing
        Assert.AreEqual(0x0u, compilandContrib.Value.RVARanges[0].RVAStart);
        Assert.AreEqual(0x200u, compilandContrib.Value.RVARanges[0].Size);



        // Assertions about a2.cpp
        Assert.AreEqual(1, a2SourceFile.SectionContributions.Count);
        Assert.AreEqual(".text", a2SourceFile.SectionContributionsByName.Keys.First());
        sectionContrib = a2SourceFile.SectionContributions.First();
        Assert.AreEqual(".text", sectionContrib.Key.Name);
        Assert.AreEqual<ulong>(0x50 + 0x50, sectionContrib.Value.Size);
        Assert.AreEqual(2, sectionContrib.Value.RVARanges.Count);
        Assert.AreEqual<ulong>(0x1700, sectionContrib.Value.RVARanges[0].RVAStart);
        Assert.AreEqual<ulong>(0x50, sectionContrib.Value.RVARanges[0].Size);
        Assert.AreEqual<ulong>(0x1800, sectionContrib.Value.RVARanges[1].RVAStart);
        Assert.AreEqual<ulong>(0x50, sectionContrib.Value.RVARanges[1].Size);

        Assert.AreEqual(1, a2SourceFile.COFFGroupContributions.Count);
        cgContrib = a2SourceFile.COFFGroupContributions.First();
        Assert.AreEqual(".text$zz", cgContrib.Key.Name);
        Assert.AreEqual<ulong>(0x50 + 0x50, cgContrib.Value.Size);
        Assert.AreEqual(2, cgContrib.Value.RVARanges.Count);
        Assert.AreEqual<ulong>(0x1700, cgContrib.Value.RVARanges[0].RVAStart);
        Assert.AreEqual<ulong>(0x50, cgContrib.Value.RVARanges[0].Size);
        Assert.AreEqual<ulong>(0x1800, cgContrib.Value.RVARanges[1].RVAStart);
        Assert.AreEqual<ulong>(0x50, cgContrib.Value.RVARanges[1].Size);

        Assert.AreEqual(1, a2SourceFile.Compilands.Count);
        Assert.AreEqual(a2Compiland, a2SourceFile.Compilands.First());

        Assert.AreEqual(1, a2SourceFile.CompilandContributions.Count);
        compilandContrib = a2SourceFile.CompilandContributions.First();
        Assert.AreEqual(@"c:\dummy\a2.obj", compilandContrib.Key.Name);
        Assert.AreEqual(0x50u + 0x50u, compilandContrib.Value.Size);
        Assert.AreEqual(2, compilandContrib.Value.RVARanges.Count);
        Assert.AreEqual(0x1700u, compilandContrib.Value.RVARanges[0].RVAStart);
        Assert.AreEqual(0x50u, compilandContrib.Value.RVARanges[0].Size);
        Assert.AreEqual(0x1800u, compilandContrib.Value.RVARanges[1].RVAStart);
        Assert.AreEqual(0x50u, compilandContrib.Value.RVARanges[1].Size);



        // Assertions about a.h
        Assert.AreEqual(1, aHeaderFile.SectionContributions.Count);
        Assert.AreEqual(".text", aHeaderFile.SectionContributionsByName.Keys.First());
        sectionContrib = aHeaderFile.SectionContributions.First();
        Assert.AreEqual(".text", sectionContrib.Key.Name);
        Assert.AreEqual<ulong>(0x250, sectionContrib.Value.Size);
        Assert.AreEqual(2, sectionContrib.Value.RVARanges.Count);
        Assert.AreEqual<ulong>(0x400, sectionContrib.Value.RVARanges[0].RVAStart);
        Assert.AreEqual<ulong>(0x200, sectionContrib.Value.RVARanges[0].Size); // Coalesces the first two 0x100-sized ranges since they're right next to each other
        Assert.AreEqual<ulong>(0x1850, sectionContrib.Value.RVARanges[1].RVAStart);
        Assert.AreEqual<ulong>(0x50, sectionContrib.Value.RVARanges[1].Size);

        Assert.AreEqual(2, aHeaderFile.COFFGroupContributions.Count);
        cgContrib = aHeaderFile.COFFGroupContributions.Single(cg => cg.Key.Name == ".text$mn");
        Assert.AreEqual<ulong>(0x200, cgContrib.Value.Size);
        Assert.AreEqual(1, cgContrib.Value.RVARanges.Count);
        Assert.AreEqual<ulong>(0x400, sectionContrib.Value.RVARanges[0].RVAStart);
        Assert.AreEqual<ulong>(0x200, sectionContrib.Value.RVARanges[0].Size); // Coalesces the first two 0x100-sized ranges since they're right next to each other
        cgContrib = aHeaderFile.COFFGroupContributions.Single(cg => cg.Key.Name == ".text$zz");
        Assert.AreEqual<ulong>(0x50, cgContrib.Value.Size);
        Assert.AreEqual(1, cgContrib.Value.RVARanges.Count);
        Assert.AreEqual<ulong>(0x1850, cgContrib.Value.RVARanges[0].RVAStart);
        Assert.AreEqual<ulong>(0x50, cgContrib.Value.RVARanges[0].Size);

        Assert.AreEqual(2, aHeaderFile.Compilands.Count);
        Assert.IsTrue(aHeaderFile.Compilands.Contains(a1Compiland));
        Assert.IsTrue(aHeaderFile.Compilands.Contains(a2Compiland));

        Assert.AreEqual(2, aHeaderFile.CompilandContributions.Count);
        compilandContrib = aHeaderFile.CompilandContributions.Single(c => c.Key.Name == @"c:\dummy\a1.obj");
        Assert.AreEqual(0x100u, compilandContrib.Value.Size);
        Assert.AreEqual(1, compilandContrib.Value.RVARanges.Count);
        Assert.AreEqual(0x400u, compilandContrib.Value.RVARanges[0].RVAStart);
        Assert.AreEqual(0x100u, compilandContrib.Value.RVARanges[0].Size);
        compilandContrib = aHeaderFile.CompilandContributions.Single(c => c.Key.Name == @"c:\dummy\a2.obj");
        Assert.AreEqual(0x150u, compilandContrib.Value.Size);
        Assert.AreEqual(2, compilandContrib.Value.RVARanges.Count);
        Assert.AreEqual(0x500u, compilandContrib.Value.RVARanges[0].RVAStart);
        Assert.AreEqual(0x100u, compilandContrib.Value.RVARanges[0].Size);
        Assert.AreEqual(0x1850u, compilandContrib.Value.RVARanges[1].RVAStart);
        Assert.AreEqual(0x50u, compilandContrib.Value.RVARanges[1].Size);
    }

    public void Dispose() => this.DataCache.Dispose();
}
