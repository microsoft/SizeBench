using System.Reflection.PortableExecutable;
using SizeBench.Logging;
using SizeBench.TestDataCommon;

namespace SizeBench.AnalysisEngine.SessionTasks.Tests;

[TestClass]
public sealed class EnumerateBinarySectionsAndCOFFGroupsSessionTaskTests : IDisposable
{
    private Mock<ISession>? MockSession;
    private SessionTaskParameters? SessionTaskParameters;
    private SessionDataCache? DataCache;
    private TestDIAAdapter TestDIAAdapter = new TestDIAAdapter();
    private List<BinarySection> Sections = new List<BinarySection>();
    private List<COFFGroup> COFFGroups = new List<COFFGroup>();

    [TestInitialize]
    public void TestInitialize()
    {
        this.MockSession = new Mock<ISession>();
        this.DataCache = new SessionDataCache();
        this.TestDIAAdapter = new TestDIAAdapter();

        this.SessionTaskParameters = new SessionTaskParameters(
            this.MockSession.Object,
            this.TestDIAAdapter,
            this.DataCache);

        this.Sections = new List<BinarySection>()
            {
                // RVA: 0-999
                new BinarySection(this.DataCache, ".text",  size: 1000, virtualSize: 1000, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute),
                // RVA: 1000-1499
                new BinarySection(this.DataCache, ".rdata", size: 500, virtualSize: 500, rva: 1000, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead),
                // RVA: 1500-1999 (note this is .bss so it's using VirtualSize)
                new BinarySection(this.DataCache, ".bss", size: 0, virtualSize: 500, rva: 1500, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead | SectionCharacteristics.ContainsInitializedData)
            };

        this.COFFGroups = new List<COFFGroup>()
            {
                // .text is 0-999, so these are 0-799 and 800-999
                new COFFGroup(this.DataCache, ".text$x", size: 800, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute),
                new COFFGroup(this.DataCache, ".text$mn", size: 200, rva: 800, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemExecute),
                // .rdata is 1000-1499, so these are 1000-1099 and 1100-1499
                new COFFGroup(this.DataCache, ".rdata$brc", size: 100, rva: 1000, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead | SectionCharacteristics.ContainsInitializedData),
                // Pick an intentionally weird name to verify we're not matching anything based on name like SizeBench V1 did (names are wrong, RVA ranges are right)
                new COFFGroup(this.DataCache, ".testing", size: 400, rva: 1100, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.MemRead | SectionCharacteristics.ContainsInitializedData),
                // This one is in virtualSize, from 1500-1999
                new COFFGroup(this.DataCache, ".bss-cg", size: 500, rva: 1500, fileAlignment: 0, sectionAlignment: 0, characteristics: SectionCharacteristics.ContainsUninitializedData)
            };
    }

    [TestMethod]
    public void BinarySectionsAndCOFFGroupsAreHookedUpBidirectionallyInTheOM()
    {
        this.TestDIAAdapter.BinarySectionsToFind = this.Sections;
        this.TestDIAAdapter.COFFGroupsToFind = this.COFFGroups;

        using var logger = new NoOpLogger();
        var output = new EnumerateBinarySectionsAndCOFFGroupsSessionTask(this.SessionTaskParameters!, token: CancellationToken.None).Execute(logger);

        var textSectionOutput = output.First(s => s.Name == ".text");
        var rdataSectionOutput = output.First(s => s.Name == ".rdata");
        var bssSectionOutput = output.First(s => s.Name == ".bss");

        // Check that the COFFGroups and Section properties got linked back and forth between these objects, based on their RVA/Size/VirtualSize
        Assert.AreEqual<ulong>(1000, textSectionOutput.Size);
        Assert.AreEqual(2, textSectionOutput.COFFGroups.Count);
        Assert.IsTrue(textSectionOutput.COFFGroups.Any(cg => cg.Name == ".text$x"));
        Assert.IsTrue(textSectionOutput.COFFGroups.Any(cg => cg.Name == ".text$mn"));
        Assert.AreEqual(textSectionOutput, textSectionOutput.COFFGroups[0].Section);
        Assert.AreEqual(textSectionOutput, textSectionOutput.COFFGroups[1].Section);

        Assert.AreEqual<ulong>(500, rdataSectionOutput.Size);
        Assert.AreEqual(2, rdataSectionOutput.COFFGroups.Count);
        Assert.IsTrue(rdataSectionOutput.COFFGroups.Any(cg => cg.Name == ".rdata$brc"));
        Assert.IsTrue(rdataSectionOutput.COFFGroups.Any(cg => cg.Name == ".testing"));
        Assert.AreEqual(rdataSectionOutput, rdataSectionOutput.COFFGroups[0].Section);
        Assert.AreEqual(rdataSectionOutput, rdataSectionOutput.COFFGroups[1].Section);

        Assert.AreEqual<ulong>(500, bssSectionOutput.VirtualSize);
        Assert.AreEqual(1, bssSectionOutput.COFFGroups.Count);
        Assert.AreEqual(".bss-cg", bssSectionOutput.COFFGroups[0].Name);
        Assert.AreEqual(bssSectionOutput, bssSectionOutput.COFFGroups[0].Section);
    }

    [TestMethod]
    public void CanCancelInTheMiddleOfEnumeratingSections()
    {
        using var cts = new CancellationTokenSource();
        this.TestDIAAdapter.BinarySectionsToFind = this.Sections.EnumerateListButCancelInTheMiddleOfEnumerating(cts, cancelAfter: 1);
        this.TestDIAAdapter.COFFGroupsToFind = this.COFFGroups;

        var task = new EnumerateBinarySectionsAndCOFFGroupsSessionTask(this.SessionTaskParameters!, token: cts.Token);

        List<BinarySection>? output = null;
        OperationCanceledException? exceptionCaught = null;

        try
        {
            using var logger = new NoOpLogger();
            output = task.Execute(logger);
        }
        catch (OperationCanceledException ex)
        {
            exceptionCaught = ex;
        }

        Assert.IsNull(output);
        Assert.IsNotNull(exceptionCaught);
    }

    [TestMethod]
    public void CanCancelInTheMiddleOfEnumeratingCOFFGroups()
    {
        using var cts = new CancellationTokenSource();
        this.TestDIAAdapter.BinarySectionsToFind = this.Sections;
        this.TestDIAAdapter.COFFGroupsToFind = this.COFFGroups.EnumerateListButCancelInTheMiddleOfEnumerating(cts, cancelAfter: 1);

        var task = new EnumerateBinarySectionsAndCOFFGroupsSessionTask(this.SessionTaskParameters!, token: cts.Token);

        List<BinarySection>? output = null;
        OperationCanceledException? exceptionCaught = null;

        try
        {
            using var logger = new NoOpLogger();
            output = task.Execute(logger);
        }
        catch (OperationCanceledException ex)
        {
            exceptionCaught = ex;
        }

        Assert.IsNull(output);
        Assert.IsNotNull(exceptionCaught);
    }

    [TestMethod]
    public void CacheIsFilledInAndReusedAfterFirstCall()
    {
        Assert.IsNull(this.DataCache!.AllBinarySections);

        this.TestDIAAdapter.BinarySectionsToFind = this.Sections;
        this.TestDIAAdapter.COFFGroupsToFind = this.COFFGroups;
        using var logger = new NoOpLogger();
        _ = new EnumerateBinarySectionsAndCOFFGroupsSessionTask(this.SessionTaskParameters!, token: CancellationToken.None).Execute(logger);

        Assert.IsNotNull(this.DataCache.AllBinarySections);
        Assert.AreEqual(this.Sections.Count, this.DataCache.AllBinarySections.Count);

        // Now the cache is filled in, so we shouldn't even try to enumerate out of the DIA Adapter
        this.TestDIAAdapter.BinarySectionsToFind = this.Sections.EnumerationThatThrowsIfEverCalled();
        this.TestDIAAdapter.COFFGroupsToFind = this.COFFGroups.EnumerationThatThrowsIfEverCalled();

        _ = new EnumerateBinarySectionsAndCOFFGroupsSessionTask(this.SessionTaskParameters!, token: CancellationToken.None).Execute(logger);

        Assert.IsNotNull(this.DataCache.AllBinarySections);
        Assert.AreEqual(this.Sections.Count, this.DataCache.AllBinarySections.Count);
    }

    public void Dispose() => this.DataCache?.Dispose();
}
