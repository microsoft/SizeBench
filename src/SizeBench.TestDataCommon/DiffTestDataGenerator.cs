using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.DiffSessionTasks;
using SizeBench.AnalysisEngine.SessionTasks;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.TestDataCommon;

// The relationship between BinarySectionDiff, COFFGroupDiff, LibDiff, and CompilandDiff, and all their session caches
// is pretty complex, so let's just make one set of these for all the tests to share to avoid massive repetition between
// test classes.
// TODO: Diff: Add some cases where multiple RVA Ranges exist for a single contribution
// The full layout is listed below.
// Note that for testing purposes we try to cover every combination, so:
//   .text grows in size, across multiple COFF Groups
//   .data shrinks in size, and only in one COFF Group
//   .bss COFF Group tests VirtualSize instead of Size
//   .rdata stays the same size in every COFF Group, but is at a different RVA
//   .virt exists in "before" but not in "after" to test section being null in after case, and also tests a section that is entirely VirtualSize
//   .rsrc exists in "after" but not in "before" to test section being null in before case
//
// Note that SectionAlignment is set to 5000 because 0x1000 (4096) is the minimum amount to allow VirtualSize in the linker, and we want
// to test that.  In real life SectionAlignment can't be this number, since it needs to be in mutliples of pages sizes, but the tests are
// a lot easier to author and read when the numbers are in decimal (instaed of hex).
//
// ----------------------------------------------------------------------------------------------
// |                 Before RVAs/Size |   After RVAs/Size  | Diff In Size | Diff In VirtualSize |
// |----------------------------------|--------------------|--------------|---------------------|
// | .text             0 -  1499/1500 |     0 -  1999/2000 | +500         | +500                |
// |   .text$mn        0 -   899/ 900 |     0 -   999/1000 | +100         | +100                |
// |   .text$zz      900 -  1499/ 600 |  1000 -  1999/1000 | +400         | +400                |
// |   <tail slop>  1500 -  4999/3500 |  2000 -  4999/3000 | -500         | -500                |
// | .data          5000 -  7499/2500 |  5000 -  6699/1700 | -1000        | -800                |
// |   .data$xx     5000 -  5499/ 500 |  5000 -  5499/ 500 | 0            | 0                   |
// |   .data$zz     5500 -  6999/1500 |  5500 -  5999/ 500 | -1000        | -1000               |
// |   .bss         7000 -  7499/ 500 |  6000 -  6699/ 700 | 0            | +200                |
// |   <tail slop>  7500 -  9999/2500 |  6700 -  9999/3300 | +800         | +800                |
// | .rdata        10000 - 11799/1800 | 10000 - 11799/1800 | 0            | 0                   |
// |   .rdata$xx   10000 - 10799/ 800 | 10000 - 10799/ 800 | 0            | 0                   |
// |   .rdata$zz   10800 - 10999/ 200 | 10000 - 10999/ 200 | 0            | 0                   |
// |   .rdata$bef  11000 - 11299/ 300 |        Not Present | -300         | -300                |
// |   .rdata$aft         Not Present | 11000 - 11299/ 300 | 300          | 300                 |
// |   .rdata$foo  11300 - 11799/ 500 | 11300 - 11799/ 500 | 0            | 0                   |
// |   <tail slop> 11800 - 14999/3200 | 11800 - 14999/3200 | 0            | 0                   |
// | .virt         15000 - 15299/ 300 |        Not Present | 0            | -300                |
// | .rsrc                Not Present | 15000 - 15199/ 200 | +200         | +200                |
// |   <tail slop> 15300 - 19999/4700 | 15200 - 19999/4800 | +100         | +100                |
// ----------------------------------------------------------------------------------------------
//
// And within those, the compilands/libs and how much they contribute to each is here.  Note that because
// of strict debug sanity checks in the code, we must 'fill up' every section and COFF Group from above - which is how
// a real binary would be anyway so that's desirable to synthetic test close to what reality is (RealPETests will hit
// true reality, albeit much slower than these tests can run).
//
// Again, we try to cover every combination, so:
//     a1.obj contributes to: multiple sections, but not all of them (no .rdata) and it grows in size
//                            multiple COFF Groups in the same section (.text), where each one grows
//                            only-some of the COFF Groups in the same section (.data) where it grows
//                            none of the COFF Groups in some section (.rdata)
//     a2.obj contributes to: multiple sections, but not all of them (no .rdata) and it shrinks in size
//                            multiple COFF Groups in the same section (.text), where one shrinks and one grows but we net zero change
//                            only-some of the COFF Groups in the same section (.data) where it shrinks
//                            none of the COFF Groups in some section (.rdata)
//     a3.obj exists in "before" but not in "after" in a section that exists on both sides
//     a4.obj exists in "after" but not in "before" in a section that exists on both sides
//     b1.obj contributes to: only one section (.data)
//     b2.obj remains identical in all ways (0 diff, just RVAs change as it gets shuffled by the others)
//     c1.obj exists in "before" but not in "after" in a section that's also gone
//     d1.obj exists in "after" but not "before" in a section that's new as well
//
//     a.lib contributes to: a section with mulitple compilands (.text with a1.obj and a2.obj)
//                           a section with only one compiland (.data with a1.obj)
//                           a section with none of its compilands (.rdata)
//                           a COFF Group with multiple compilands (.text$mn with a1.obj and a2.obj)
//                           a COFF Group with only one compiland (.text$zz with a1.obj)
//                           a section (.data) without contributing anything to a COFF Group within (.data$xx)
//     b.lib contributes some stuff, just to verify that after filtering to a.lib, it gets filtered out
//     c.lib exists in "before" but not in "after"
//     d.lib exists in "after" but not in "before"

// Starting with Compilands, listed like this in the table (RawSize is either VirtualSize or real Size, depending on whether it's .bss or .virt (virtual) or anything else (real size)):
// | Compiland | Lib    | (for each section and COFF Group) |
// | <Name>    | <Name> | <Before RawSize>                  |
// |           |        | <After RawSize>                   |
// |           |        | <Diff RawSize>                    |
//
// --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
// | Compiland  | Total | .text | .text$mn | .text$zz | .data | .data$xx | .data$zz | .bss | .rdata | .rdata$xx | .rdata$zz | .rdata$bef | .rdata$aft | .rdata$foo | .virt | .rsrc |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | a1.obj     |  1225 |  1000 |      700 |      300 |   225 |        0 |       25 |  200 |      0 |         0 |         0 |          0 |        N/A |          0 |     0 |   N/A |
// |            |  1725 |  1400 |      900 |      500 |   325 |       25 |        0 |  300 |      0 |         0 |         0 |        N/A |          0 |          0 |   N/A |     0 |
// |            |   500 |   400 |      200 |      200 |   100 |       25 |      -25 |  100 |      0 |         0 |         0 |          0 |          0 |          0 |     0 |     0 |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | a2.obj     |   475 |   400 |      200 |      200 |    75 |       75 |        0 |    0 |      0 |         0 |         0 |          0 |        N/A |          0 |     0 |   N/A |
// |            |   475 |   400 |        0 |      400 |    75 |       75 |        0 |    0 |      0 |         0 |         0 |        N/A |          0 |          0 |   N/A |     0 |
// |            |     0 |     0 |     -200 |      200 |     0 |        0 |        0 |    0 |      0 |         0 |         0 |          0 |          0 |          0 |     0 |     0 |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | a3.obj     |  1100 |     0 |        0 |        0 |   300 |        0 |        0 |  300 |    800 |         0 |         0 |        300 |        N/A |        500 |     0 |   N/A |
// |            |   N/A |     0 |        0 |        0 |     0 |        0 |        0 |    0 |    N/A |         0 |         0 |        N/A |          0 |        N/A |   N/A |     0 |
// |            | -1100 |     0 |        0 |        0 |  -300 |        0 |        0 | -300 |   -800 |         0 |         0 |       -300 |          0 |       -500 |     0 |     0 |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | a4.obj     |   N/A |     0 |        0 |        0 |     0 |        0 |        0 |    0 |    N/A |         0 |         0 |          0 |        N/A |        N/A |     0 |   N/A |
// |            |  1200 |     0 |        0 |        0 |   400 |        0 |        0 |  400 |    800 |         0 |         0 |        N/A |        300 |        500 |   N/A |     0 |
// |            |  1200 |     0 |        0 |        0 |   400 |        0 |        0 |  400 |    800 |         0 |         0 |          0 |        300 |        500 |     0 |     0 |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | b1.obj     |  1900 |     0 |        0 |        0 |  1900 |      425 |     1475 |    0 |      0 |         0 |         0 |          0 |        N/A |          0 |     0 |   N/A |
// |            |  1000 |   100 |      100 |        0 |   900 |      400 |      500 |    0 |      0 |         0 |         0 |        N/A |          0 |          0 |   N/A |     0 |
// |            |  -900 |   100 |      100 |        0 | -1000 |      -25 |     -975 |    0 |      0 |         0 |         0 |          0 |          0 |          0 |     0 |     0 |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | b2.obj     |  1100 |   100 |        0 |      100 |     0 |        0 |        0 |    0 |   1000 |       800 |       200 |          0 |        N/A |          0 |     0 |   N/A |
// |            |  1100 |   100 |        0 |      100 |     0 |        0 |        0 |    0 |   1000 |       800 |       200 |        N/A |          0 |          0 |   N/A |     0 |
// |            |     0 |     0 |        0 |        0 |     0 |        0 |        0 |    0 |      0 |         0 |         0 |          0 |          0 |          0 |     0 |     0 |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | c1.obj     |   300 |     0 |        0 |        0 |     0 |        0 |        0 |    0 |      0 |         0 |         0 |          0 |        N/A |          0 |   300 |   N/A |
// |            |   N/A |   N/A |      N/A |      N/A |   N/A |      N/A |      N/A |    0 |    N/A |       N/A |       N/A |        N/A |          0 |        N/A |   N/A |   N/A |
// |            |  -300 |     0 |        0 |        0 |     0 |        0 |        0 |    0 |      0 |         0 |         0 |          0 |          0 |          0 |  -300 |     0 |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | d1.obj     |   N/A |   N/A |      N/A |      N/A |   N/A |      N/A |      N/A |    0 |    N/A |       N/A |       N/A |          0 |        N/A |        N/A |   N/A |   N/A |
// |            |   200 |     0 |        0 |        0 |     0 |        0 |        0 |    0 |      0 |         0 |         0 |        N/A |          0 |          0 |   N/A |   200 |
// |            |   200 |     0 |        0 |        0 |     0 |        0 |        0 |    0 |      0 |         0 |         0 |          0 |          0 |          0 |   N/A |   200 |
// ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
//
// And then summarizing the Libs:
// ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------
// | Lib   | Total | .text | .text$mn | .text$zz | .data | .data$xx | .data$zz | .bss | .rdata | .rdata$xx | .rdata$zz | .rdata$bef | .rdata$aft | .rdata$foo | .virt | .rsrc |
// |-------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | a.lib |  2800 |  1400 |      900 |      500 |   600 |       75 |       25 |  500 |    800 |         0 |         0 |        300 |        N/A |        500 |     0 |   N/A |
// |       |  3400 |  1800 |      900 |      900 |   800 |      100 |        0 |  700 |    800 |         0 |         0 |        N/A |        300 |        500 |   N/A |     0 |
// |       |   600 |   400 |        0 |      400 |   200 |       25 |      -25 |  200 |      0 |         0 |         0 |       -300 |        300 |          0 |     0 |     0 |
// |-------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | b.lib |  2900 |     0 |        0 |        0 |  1900 |      425 |     1475 |    0 |   1000 |       800 |       200 |          0 |        N/A |          0 |     0 |   N/A |
// |       |  2000 |   100 |      100 |        0 |   900 |      400 |      500 |    0 |   1000 |       800 |       200 |        N/A |          0 |          0 |   N/A |     0 |
// |       |  -900 |   100 |      100 |        0 | -1000 |      -25 |     -975 |    0 |      0 |         0 |         0 |          0 |          0 |          0 |     0 |     0 |
// |-------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | c.lib |   300 |     0 |        0 |        0 |     0 |        0 |        0 |    0 |      0 |         0 |         0 |          0 |        N/A |          0 |   300 |   N/A |
// |       |   N/A |   N/A |      N/A |      N/A |   N/A |      N/A |      N/A |    0 |    N/A |       N/A |       N/A |        N/A |        N/A |        N/A |   N/A |   N/A |
// |       |  -300 |     0 |        0 |        0 |     0 |        0 |        0 |    0 |      0 |         0 |         0 |          0 |          0 |          0 |  -300 |     0 |
// |-------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|------------|------------|------------|-------|-------|
// | d.lib |   N/A |   N/A |      N/A |      N/A |   N/A |      N/A |      N/A |    0 |    N/A |       N/A |       N/A |        N/A |        N/A |        N/A |   N/A |   N/A |
// |       |   200 |     0 |        0 |        0 |     0 |        0 |        0 |    0 |      0 |         0 |         0 |        N/A |          0 |          0 |   N/A |   200 |
// |       |   200 |     0 |        0 |        0 |     0 |        0 |        0 |    0 |      0 |         0 |         0 |        N/A |          0 |          0 |     0 |   200 |
// ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------
//
internal sealed class DiffTestDataGenerator : IDisposable
{
    #region Fields

    public Mock<ISession> MockBeforeSession = new Mock<ISession>();
    public Mock<ISession> MockAfterSession = new Mock<ISession>();
    public Mock<IDiffSession> MockDiffSession = new Mock<IDiffSession>();
    internal SessionDataCache BeforeDataCache;
    private uint _beforeNextSymIndexId;
    internal SessionDataCache AfterDataCache;
    private uint _afterNextSymIndexId;
    internal DiffSessionDataCache DiffDataCache;

    // Before objects
    internal SessionTaskParameters BeforeSessionTaskParameters;
    internal TestDIAAdapter BeforeDIAAdapter;
    public BinarySection BeforeTextSection;
    public BinarySection BeforeDataSection;
    public BinarySection BeforeRDataSection;
    public BinarySection BeforeVirtSection;
    public List<BinarySection> BeforeSections;
    public COFFGroup BeforeTextMnCG;
    public COFFGroup BeforeTextZzCG;
    public COFFGroup BeforeDataXxCG;
    public COFFGroup BeforeDataZzCG;
    public COFFGroup BeforeBssCG;
    public COFFGroup BeforeRDataXxCG;
    public COFFGroup BeforeRDataZzCG;
    public COFFGroup BeforeRDataBefCG;
    public COFFGroup BeforeRDataFooCG;
    public COFFGroup BeforeVirtCG;
    public List<COFFGroup> BeforeCOFFGroups;
    public Compiland BeforeA1Compiland;
    public Compiland BeforeA2Compiland;
    public Compiland BeforeA3Compiland;
    public Compiland BeforeB1Compiland;
    public Compiland BeforeB2Compiland;
    public Compiland BeforeC1Compiland;
    public HashSet<Compiland> BeforeCompilands;
    public Library BeforeALib;
    public Library BeforeBLib;
    public Library BeforeCLib;
    public HashSet<Library> BeforeLibs;
    public UserDefinedTypeSymbol BeforeIUnknownUDT;

    // After objects
    internal SessionTaskParameters AfterSessionTaskParameters;
    internal TestDIAAdapter AfterDIAAdapter;
    public BinarySection AfterTextSection;
    public BinarySection AfterDataSection;
    public BinarySection AfterRDataSection;
    public BinarySection AfterRsrcSection;
    public List<BinarySection> AfterSections;
    public COFFGroup AfterTextMnCG;
    public COFFGroup AfterTextZzCG;
    public COFFGroup AfterDataXxCG;
    public COFFGroup AfterDataZzCG;
    public COFFGroup AfterBssCG;
    public COFFGroup AfterRDataXxCG;
    public COFFGroup AfterRDataZzCG;
    public COFFGroup AfterRDataAftCG;
    public COFFGroup AfterRDataFooCG;
    public COFFGroup AfterRsrcCG;
    public List<COFFGroup> AfterCOFFGroups;
    public Compiland AfterA1Compiland;
    public Compiland AfterA2Compiland;
    public Compiland AfterA4Compiland;
    public Compiland AfterB1Compiland;
    public Compiland AfterB2Compiland;
    public Compiland AfterD1Compiland;
    public HashSet<Compiland> AfterCompilands;
    public Library AfterALib;
    public Library AfterBLib;
    public Library AfterDLib;
    public HashSet<Library> AfterLibs;
    public UserDefinedTypeSymbol AfterIUnknownUDT;

    // Diff objects
    internal DiffSessionTaskParameters DiffSessionTaskParameters;
    public BinarySectionDiff TextSectionDiff;
    public BinarySectionDiff DataSectionDiff;
    public BinarySectionDiff RDataSectionDiff;
    public BinarySectionDiff VirtSectionDiff;
    public BinarySectionDiff RsrcSectionDiff;
    public List<BinarySectionDiff> BinarySectionDiffs;
    public COFFGroupDiff TextMnCGDiff;
    public COFFGroupDiff TextZzCGDiff;
    public COFFGroupDiff DataXxCGDiff;
    public COFFGroupDiff DataZzCGDiff;
    public COFFGroupDiff BssCGDiff;
    public COFFGroupDiff RDataXxCGDiff;
    public COFFGroupDiff RDataZzCGDiff;
    public COFFGroupDiff RDataBefCGDiff;
    public COFFGroupDiff RDataAftCGDiff;
    public COFFGroupDiff RDataFooCGDiff;
    public COFFGroupDiff VirtCGDiff;
    public COFFGroupDiff RsrcCGDiff;
    public List<COFFGroupDiff> COFFGroupDiffs;
    public CompilandDiff A1CompilandDiff;
    public CompilandDiff A2CompilandDiff;
    public CompilandDiff A3CompilandDiff;
    public CompilandDiff A4CompilandDiff;
    public CompilandDiff B1CompilandDiff;
    public CompilandDiff B2CompilandDiff;
    public CompilandDiff C1CompilandDiff;
    public CompilandDiff D1CompilandDiff;
    public List<CompilandDiff> CompilandDiffs;
    public LibDiff ALibDiff;
    public LibDiff BLibDiff;
    public LibDiff CLibDiff;
    public LibDiff DLibDiff;
    public List<LibDiff> LibDiffs;

    #endregion

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.  This constructor calls a bunch of functions to make the code more readable, but code analysis can't see this - we'll trust that we initialized all the fields.
    public DiffTestDataGenerator(bool initializeDiffObjects = true, TestDIAAdapter? beforeDIAAdapter = null, TestDIAAdapter? afterDIAAdapter = null)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        this.BeforeDIAAdapter = beforeDIAAdapter ?? new TestDIAAdapter();
        this.AfterDIAAdapter = afterDIAAdapter ?? new TestDIAAdapter();
        this.BeforeDataCache = new SessionDataCache
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        this.AfterDataCache = new SessionDataCache
        {
            AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
        };
        this.DiffDataCache = new DiffSessionDataCache();
        this.MockBeforeSession.SetupGet(s => s.BytesPerWord).Returns(8);
        this.MockAfterSession.SetupGet(s => s.BytesPerWord).Returns(8);
        this.MockDiffSession.SetupGet(ds => ds.BeforeSession).Returns(this.MockBeforeSession.Object);
        this.MockDiffSession.SetupGet(ds => ds.AfterSession).Returns(this.MockAfterSession.Object);
        SetupBeforeSectionsAndCOFFGroups();
        SetupAfterSectionsAndCOFFGroups();
        if (initializeDiffObjects)
        {
            SetupDiffSectionsAndCOFFGroups();
        }

        SetupBeforeCompilandsAndLibs();
        SetupAfterCompilandsAndLibs();
        if (initializeDiffObjects)
        {
            SetupDiffCompilandsAndLibs();
        }

        SetupSessionTaskParameters();

        this.MockDiffSession.Setup(ds => ds.EnumerateLibDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<LibDiff>>(this.LibDiffs!));
        this.MockDiffSession.Setup(ds => ds.EnumerateCompilandDiffs(It.IsAny<CancellationToken>())).Returns(Task.FromResult<IReadOnlyList<CompilandDiff>>(this.CompilandDiffs!));
        this.MockDiffSession.Setup(ds => ds.GetBinarySectionDiffFromBinarySection(It.IsAny<BinarySection>()))
                            .Returns((BinarySection section) => this.BinarySectionDiffs!.SingleOrDefault(diff => diff.BeforeSection == section || diff.AfterSection == section));
        this.MockDiffSession.Setup(ds => ds.GetCOFFGroupDiffFromCOFFGroup(It.IsAny<COFFGroup>()))
                            .Returns((COFFGroup coffGroup) => this.COFFGroupDiffs!.SingleOrDefault(diff => diff.BeforeCOFFGroup == coffGroup || diff.AfterCOFFGroup == coffGroup));
        this.MockDiffSession.Setup(ds => ds.GetLibraryDiffFromLibrary(It.IsAny<Library>()))
                            .Returns((Library lib) => this.LibDiffs!.SingleOrDefault(diff => diff.BeforeLib == lib || diff.AfterLib == lib));
        this.MockDiffSession.Setup(ds => ds.GetCompilandDiffFromCompiland(It.IsAny<Compiland>()))
                            .Returns((Compiland compiland) => this.CompilandDiffs!.SingleOrDefault(diff => diff.BeforeCompiland == compiland || diff.AfterCompiland == compiland));
    }

    private void SetupBeforeSectionsAndCOFFGroups()
    {
        this.BeforeTextSection = new BinarySection(this.BeforeDataCache, ".text", size: 1500, virtualSize: 1500, rva: 0,
                                                  fileAlignment: 0, sectionAlignment: 5000, characteristics: SectionCharacteristics.MemExecute);
        this.BeforeDataSection = new BinarySection(this.BeforeDataCache, ".data", size: 2000, virtualSize: 2500, rva: 5000,
                                                  fileAlignment: 0, sectionAlignment: 5000, characteristics: SectionCharacteristics.MemWrite | SectionCharacteristics.MemRead);
        this.BeforeRDataSection = new BinarySection(this.BeforeDataCache, ".rdata", size: 1800, virtualSize: 1800, rva: 10000,
                                                   fileAlignment: 0, sectionAlignment: 5000, characteristics: SectionCharacteristics.MemRead);
        this.BeforeVirtSection = new BinarySection(this.BeforeDataCache, ".virt", size: 0, virtualSize: 300, rva: 15000,
                                                  fileAlignment: 0, sectionAlignment: 5000, characteristics: SectionCharacteristics.ContainsUninitializedData);

        this.BeforeTextMnCG = new COFFGroup(this.BeforeDataCache, ".text$mn", size: 900, rva: 0,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.MemExecute)
        {
            Section = this.BeforeTextSection
        };
        this.BeforeTextZzCG = new COFFGroup(this.BeforeDataCache, ".text$zz", size: 600, rva: 900,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.MemExecute)
        {
            Section = this.BeforeTextSection
        };


        this.BeforeDataXxCG = new COFFGroup(this.BeforeDataCache, ".data$xx", size: 500, rva: 5000,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.ContainsInitializedData |
                                                            SectionCharacteristics.MemRead |
                                                            SectionCharacteristics.MemWrite)
        {
            Section = this.BeforeDataSection
        };
        this.BeforeDataZzCG = new COFFGroup(this.BeforeDataCache, ".data$zz", size: 1500, rva: 5500,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.ContainsInitializedData |
                                                            SectionCharacteristics.MemRead |
                                                            SectionCharacteristics.MemWrite)
        {
            Section = this.BeforeDataSection
        };
        this.BeforeBssCG = new COFFGroup(this.BeforeDataCache, ".bss", size: 500, rva: 7000,
                                         fileAlignment: 0, sectionAlignment: 5000,
                                         characteristics: SectionCharacteristics.ContainsUninitializedData |
                                                          SectionCharacteristics.MemRead |
                                                          SectionCharacteristics.MemWrite)
        {
            Section = this.BeforeDataSection
        };


        this.BeforeRDataXxCG = new COFFGroup(this.BeforeDataCache, ".rdata$xx", size: 800, rva: 10000,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.MemRead)
        {
            Section = this.BeforeRDataSection
        };
        this.BeforeRDataZzCG = new COFFGroup(this.BeforeDataCache, ".rdata$zz", size: 200, rva: 10800,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.MemRead)
        {
            Section = this.BeforeRDataSection
        };
        this.BeforeRDataBefCG = new COFFGroup(this.BeforeDataCache, ".rdata$bef", size: 300, rva: 11000,
                                              fileAlignment: 0, sectionAlignment: 5000,
                                              characteristics: SectionCharacteristics.MemRead)
        {
            Section = this.BeforeRDataSection
        };
        this.BeforeRDataFooCG = new COFFGroup(this.BeforeDataCache, ".rdata$foo", size: 500, rva: 11300,
                                              fileAlignment: 0, sectionAlignment: 5000,
                                              characteristics: SectionCharacteristics.MemRead)
        {
            Section = this.BeforeRDataSection
        };

        this.BeforeVirtCG = new COFFGroup(this.BeforeDataCache, ".virt", size: 300, rva: 15000,
                                         fileAlignment: 0, sectionAlignment: 5000,
                                         characteristics: SectionCharacteristics.ContainsUninitializedData)
        {
            Section = this.BeforeVirtSection
        };

        this.BeforeTextMnCG.MarkFullyConstructed();
        this.BeforeTextZzCG.MarkFullyConstructed();
        this.BeforeTextSection.AddCOFFGroup(this.BeforeTextMnCG);
        this.BeforeTextSection.AddCOFFGroup(this.BeforeTextZzCG);
        this.BeforeTextSection.MarkFullyConstructed();

        this.BeforeDataXxCG.MarkFullyConstructed();
        this.BeforeDataZzCG.MarkFullyConstructed();
        this.BeforeBssCG.MarkFullyConstructed();
        this.BeforeDataSection.AddCOFFGroup(this.BeforeDataXxCG);
        this.BeforeDataSection.AddCOFFGroup(this.BeforeDataZzCG);
        this.BeforeDataSection.AddCOFFGroup(this.BeforeBssCG);
        this.BeforeDataSection.MarkFullyConstructed();

        this.BeforeRDataXxCG.MarkFullyConstructed();
        this.BeforeRDataZzCG.MarkFullyConstructed();
        this.BeforeRDataBefCG.MarkFullyConstructed();
        this.BeforeRDataFooCG.MarkFullyConstructed();
        this.BeforeRDataSection.AddCOFFGroup(this.BeforeRDataXxCG);
        this.BeforeRDataSection.AddCOFFGroup(this.BeforeRDataZzCG);
        this.BeforeRDataSection.AddCOFFGroup(this.BeforeRDataBefCG);
        this.BeforeRDataSection.AddCOFFGroup(this.BeforeRDataFooCG);
        this.BeforeRDataSection.MarkFullyConstructed();

        this.BeforeVirtCG.MarkFullyConstructed();
        this.BeforeVirtSection.AddCOFFGroup(this.BeforeVirtCG);
        this.BeforeVirtSection.MarkFullyConstructed();

        this.BeforeSections = new List<BinarySection>()
            {
                this.BeforeTextSection,
                this.BeforeDataSection,
                this.BeforeRDataSection,
                this.BeforeVirtSection
            };

        this.BeforeCOFFGroups = new List<COFFGroup>()
            {
                this.BeforeTextMnCG,
                this.BeforeTextZzCG,
                this.BeforeDataXxCG,
                this.BeforeDataZzCG,
                this.BeforeBssCG,
                this.BeforeRDataXxCG,
                this.BeforeRDataZzCG,
                this.BeforeRDataBefCG,
                this.BeforeRDataFooCG,
                this.BeforeVirtCG
            };
    }

    private void SetupAfterSectionsAndCOFFGroups()
    {
        this.AfterTextSection = new BinarySection(this.AfterDataCache, ".text", size: 2000, virtualSize: 2000, rva: 0,
                                                  fileAlignment: 0, sectionAlignment: 5000, characteristics: SectionCharacteristics.MemExecute);
        this.AfterDataSection = new BinarySection(this.AfterDataCache, ".data", size: 1000, virtualSize: 1700, rva: 5000,
                                                  fileAlignment: 0, sectionAlignment: 5000, characteristics: SectionCharacteristics.MemWrite | SectionCharacteristics.MemRead);
        this.AfterRDataSection = new BinarySection(this.AfterDataCache, ".rdata", size: 1800, virtualSize: 1800, rva: 10000,
                                                   fileAlignment: 0, sectionAlignment: 5000, characteristics: SectionCharacteristics.MemRead);
        this.AfterRsrcSection = new BinarySection(this.AfterDataCache, ".rsrc", size: 200, virtualSize: 200, rva: 15000,
                                                  fileAlignment: 0, sectionAlignment: 5000, characteristics: SectionCharacteristics.MemRead);

        this.AfterTextMnCG = new COFFGroup(this.AfterDataCache, ".text$mn", size: 1000, rva: 0,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.MemExecute)
        {
            Section = this.AfterTextSection
        };
        this.AfterTextZzCG = new COFFGroup(this.AfterDataCache, ".text$zz", size: 1000, rva: 1000,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.MemExecute)
        {
            Section = this.AfterTextSection
        };

        this.AfterDataXxCG = new COFFGroup(this.AfterDataCache, ".data$xx", size: 500, rva: 5000,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.ContainsInitializedData |
                                                            SectionCharacteristics.MemRead |
                                                            SectionCharacteristics.MemWrite)
        {
            Section = this.AfterDataSection
        };
        this.AfterDataZzCG = new COFFGroup(this.AfterDataCache, ".data$zz", size: 500, rva: 5500,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.ContainsInitializedData |
                                                            SectionCharacteristics.MemRead |
                                                            SectionCharacteristics.MemWrite)
        {
            Section = this.AfterDataSection
        };
        this.AfterBssCG = new COFFGroup(this.AfterDataCache, ".bss", size: 700, rva: 6000,
                                        fileAlignment: 0, sectionAlignment: 5000,
                                        characteristics: SectionCharacteristics.ContainsUninitializedData |
                                                         SectionCharacteristics.MemRead |
                                                         SectionCharacteristics.MemWrite)
        {
            Section = this.AfterDataSection
        };

        this.AfterRDataXxCG = new COFFGroup(this.AfterDataCache, ".rdata$xx", size: 800, rva: 10000,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.MemRead)
        {
            Section = this.AfterRDataSection
        };
        this.AfterRDataZzCG = new COFFGroup(this.AfterDataCache, ".rdata$zz", size: 200, rva: 10800,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.MemRead)
        {
            Section = this.AfterRDataSection
        };
        this.AfterRDataAftCG = new COFFGroup(this.AfterDataCache, ".rdata$aft", size: 300, rva: 11000,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.MemRead)
        {
            Section = this.AfterRDataSection
        };
        this.AfterRDataFooCG = new COFFGroup(this.AfterDataCache, ".rdata$foo", size: 500, rva: 11300,
                                           fileAlignment: 0, sectionAlignment: 5000,
                                           characteristics: SectionCharacteristics.MemRead)
        {
            Section = this.AfterRDataSection
        };
        this.AfterRsrcCG = new COFFGroup(this.AfterDataCache, ".rsrc", size: 200, rva: 15000,
                                         fileAlignment: 0, sectionAlignment: 5000,
                                         characteristics: SectionCharacteristics.MemRead)
        {
            Section = this.AfterRsrcSection
        };

        this.AfterTextMnCG.MarkFullyConstructed();
        this.AfterTextZzCG.MarkFullyConstructed();
        this.AfterTextSection.AddCOFFGroup(this.AfterTextMnCG);
        this.AfterTextSection.AddCOFFGroup(this.AfterTextZzCG);
        this.AfterTextSection.MarkFullyConstructed();

        this.AfterDataXxCG.MarkFullyConstructed();
        this.AfterDataZzCG.MarkFullyConstructed();
        this.AfterBssCG.MarkFullyConstructed();
        this.AfterDataSection.AddCOFFGroup(this.AfterDataXxCG);
        this.AfterDataSection.AddCOFFGroup(this.AfterDataZzCG);
        this.AfterDataSection.AddCOFFGroup(this.AfterBssCG);
        this.AfterDataSection.MarkFullyConstructed();

        this.AfterRDataXxCG.MarkFullyConstructed();
        this.AfterRDataZzCG.MarkFullyConstructed();
        this.AfterRDataAftCG.MarkFullyConstructed();
        this.AfterRDataFooCG.MarkFullyConstructed();
        this.AfterRDataSection.AddCOFFGroup(this.AfterRDataXxCG);
        this.AfterRDataSection.AddCOFFGroup(this.AfterRDataZzCG);
        this.AfterRDataSection.AddCOFFGroup(this.AfterRDataAftCG);
        this.AfterRDataSection.AddCOFFGroup(this.AfterRDataFooCG);
        this.AfterRDataSection.MarkFullyConstructed();

        this.AfterRsrcCG.MarkFullyConstructed();
        this.AfterRsrcSection.AddCOFFGroup(this.AfterRsrcCG);
        this.AfterRsrcSection.MarkFullyConstructed();

        this.AfterSections = new List<BinarySection>()
            {
                this.AfterTextSection,
                this.AfterDataSection,
                this.AfterRDataSection,
                this.AfterRsrcSection
            };

        this.AfterCOFFGroups = new List<COFFGroup>()
            {
                this.AfterTextMnCG,
                this.AfterTextZzCG,
                this.AfterDataXxCG,
                this.AfterDataZzCG,
                this.AfterBssCG,
                this.AfterRDataXxCG,
                this.AfterRDataZzCG,
                this.AfterRDataAftCG,
                this.AfterRDataFooCG,
                this.AfterRsrcCG,
            };
    }

    private void SetupDiffSectionsAndCOFFGroups()
    {
        this.TextSectionDiff = new BinarySectionDiff(this.BeforeTextSection, this.AfterTextSection, this.DiffDataCache);
        this.DataSectionDiff = new BinarySectionDiff(this.BeforeDataSection, this.AfterDataSection, this.DiffDataCache);
        this.RDataSectionDiff = new BinarySectionDiff(this.BeforeRDataSection, this.AfterRDataSection, this.DiffDataCache);
        this.VirtSectionDiff = new BinarySectionDiff(this.BeforeVirtSection, null, this.DiffDataCache);
        this.RsrcSectionDiff = new BinarySectionDiff(null, this.AfterRsrcSection, this.DiffDataCache);

        var allBinarySectionDiffs = new List<BinarySectionDiff>()
            {
                this.TextSectionDiff,
                this.DataSectionDiff,
                this.RDataSectionDiff,
                this.VirtSectionDiff,
                this.RsrcSectionDiff
            };

        this.BinarySectionDiffs = allBinarySectionDiffs;

        this.TextMnCGDiff = this.TextSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".text$mn");
        this.TextZzCGDiff = this.TextSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".text$zz");

        this.DataXxCGDiff = this.DataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".data$xx");
        this.DataZzCGDiff = this.DataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".data$zz");
        this.BssCGDiff = this.DataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".bss");

        this.RDataXxCGDiff = this.RDataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".rdata$xx");
        this.RDataZzCGDiff = this.RDataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".rdata$zz");
        this.RDataBefCGDiff = this.RDataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".rdata$bef");
        this.RDataAftCGDiff = this.RDataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".rdata$aft");
        this.RDataFooCGDiff = this.RDataSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".rdata$foo");

        this.VirtCGDiff = this.VirtSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".virt");

        this.RsrcCGDiff = this.RsrcSectionDiff.COFFGroupDiffs.First(cgd => cgd.Name == ".rsrc");

        this.COFFGroupDiffs = new List<COFFGroupDiff>()
            {
                this.TextMnCGDiff,
                this.TextZzCGDiff,
                this.DataXxCGDiff,
                this.DataZzCGDiff,
                this.BssCGDiff,
                this.RDataXxCGDiff,
                this.RDataZzCGDiff,
                this.RDataBefCGDiff,
                this.RDataAftCGDiff,
                this.RDataFooCGDiff,
                this.VirtCGDiff,
                this.RsrcCGDiff
            };
    }

    private void SetupBeforeCompilandsAndLibs()
    {
        this.BeforeALib = new Library("a.lib");

        this._beforeNextSymIndexId = 1234;
        this.BeforeA1Compiland = this.BeforeALib.GetOrCreateCompiland(this.BeforeDataCache, @"c:\a\a1.obj", this._beforeNextSymIndexId++ /* compilandSymIndex */, this.BeforeDIAAdapter);

        var textMnContribution = this.BeforeA1Compiland.GetOrCreateCOFFGroupContribution(this.BeforeTextMnCG);
        textMnContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeTextMnCG.RVA, 700));
        textMnContribution.MarkFullyConstructed();
        var textZzContribution = this.BeforeA1Compiland.GetOrCreateCOFFGroupContribution(this.BeforeTextZzCG);
        textZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeTextZzCG.RVA, 300));
        textZzContribution.MarkFullyConstructed();
        var textContribution = this.BeforeA1Compiland.GetOrCreateSectionContribution(this.BeforeTextSection);
        textContribution.AddRVARanges(textMnContribution.RVARanges);
        textContribution.AddRVARanges(textZzContribution.RVARanges);
        textContribution.MarkFullyConstructed();

        var dataZzContribution = this.BeforeA1Compiland.GetOrCreateCOFFGroupContribution(this.BeforeDataZzCG);
        dataZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeDataZzCG.RVA, 25));
        dataZzContribution.MarkFullyConstructed();
        var bssCGContribution = this.BeforeA1Compiland.GetOrCreateCOFFGroupContribution(this.BeforeBssCG);
        bssCGContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeBssCG.RVA, 200, isVirtualSize: true));
        bssCGContribution.MarkFullyConstructed();
        var dataContribution = this.BeforeA1Compiland.GetOrCreateSectionContribution(this.BeforeDataSection);
        dataContribution.AddRVARanges(dataZzContribution.RVARanges);
        dataContribution.AddRVARanges(bssCGContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        this.BeforeA1Compiland.MarkFullyConstructed();



        this.BeforeA2Compiland = this.BeforeALib.GetOrCreateCompiland(this.BeforeDataCache, "a2.obj", this._beforeNextSymIndexId++ /* compilandSymIndex */, this.BeforeDIAAdapter);

        textMnContribution = this.BeforeA2Compiland.GetOrCreateCOFFGroupContribution(this.BeforeTextMnCG);
        textMnContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeTextMnCG.RVA + this.BeforeA1Compiland.COFFGroupContributions[this.BeforeTextMnCG].Size, 200));
        textMnContribution.MarkFullyConstructed();
        textZzContribution = this.BeforeA2Compiland.GetOrCreateCOFFGroupContribution(this.BeforeTextZzCG);
        textZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeTextZzCG.RVA + this.BeforeA1Compiland.COFFGroupContributions[this.BeforeTextZzCG].Size, 200));
        textZzContribution.MarkFullyConstructed();
        textContribution = this.BeforeA2Compiland.GetOrCreateSectionContribution(this.BeforeTextSection);
        textContribution.AddRVARanges(textMnContribution.RVARanges);
        textContribution.AddRVARanges(textZzContribution.RVARanges);
        textContribution.MarkFullyConstructed();

        var dataXxContribution = this.BeforeA2Compiland.GetOrCreateCOFFGroupContribution(this.BeforeDataXxCG);
        dataXxContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeDataXxCG.RVA, 75));
        dataXxContribution.MarkFullyConstructed();
        dataContribution = this.BeforeA2Compiland.GetOrCreateSectionContribution(this.BeforeDataSection);
        dataContribution.AddRVARanges(dataXxContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        this.BeforeA2Compiland.MarkFullyConstructed();


        this.BeforeA3Compiland = this.BeforeALib.GetOrCreateCompiland(this.BeforeDataCache, "a3.obj", this._beforeNextSymIndexId++ /* compilandSymIndex */, this.BeforeDIAAdapter);

        bssCGContribution = this.BeforeA3Compiland.GetOrCreateCOFFGroupContribution(this.BeforeBssCG);
        bssCGContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeBssCG.RVA + this.BeforeA1Compiland.COFFGroupContributions[this.BeforeBssCG].VirtualSize, 300, isVirtualSize: true));
        bssCGContribution.MarkFullyConstructed();
        dataContribution = this.BeforeA3Compiland.GetOrCreateSectionContribution(this.BeforeDataSection);
        dataContribution.AddRVARanges(bssCGContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        var rdataFooContribution = this.BeforeA3Compiland.GetOrCreateCOFFGroupContribution(this.BeforeRDataFooCG);
        rdataFooContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeRDataFooCG.RVA, 500));
        rdataFooContribution.MarkFullyConstructed();
        var rdataBefContribution = this.BeforeA3Compiland.GetOrCreateCOFFGroupContribution(this.BeforeRDataBefCG);
        rdataBefContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeRDataBefCG.RVA, 300));
        rdataBefContribution.MarkFullyConstructed();
        var rdataContribution = this.BeforeA3Compiland.GetOrCreateSectionContribution(this.BeforeRDataSection);
        rdataContribution.AddRVARanges(rdataFooContribution.RVARanges);
        rdataContribution.AddRVARanges(rdataBefContribution.RVARanges);
        rdataContribution.MarkFullyConstructed();


        this.BeforeA3Compiland.MarkFullyConstructed();



        this.BeforeALib.MarkFullyConstructed();


        this.BeforeBLib = new Library("b.lib");
        this.BeforeB1Compiland = this.BeforeBLib.GetOrCreateCompiland(this.BeforeDataCache, "b1.obj", this._beforeNextSymIndexId++ /* compilandSymIndex */, this.BeforeDIAAdapter);

        dataXxContribution = this.BeforeB1Compiland.GetOrCreateCOFFGroupContribution(this.BeforeDataXxCG);
        dataXxContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeDataXxCG.RVA + this.BeforeA2Compiland.COFFGroupContributions[this.BeforeDataXxCG].Size, 425));
        dataXxContribution.MarkFullyConstructed();
        dataZzContribution = this.BeforeB1Compiland.GetOrCreateCOFFGroupContribution(this.BeforeDataZzCG);
        dataZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeDataZzCG.RVA + this.BeforeA1Compiland.COFFGroupContributions[this.BeforeDataZzCG].Size, 1475));
        dataZzContribution.MarkFullyConstructed();
        dataContribution = this.BeforeB1Compiland.GetOrCreateSectionContribution(this.BeforeDataSection);
        dataContribution.AddRVARanges(dataXxContribution.RVARanges);
        dataContribution.AddRVARanges(dataZzContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        this.BeforeB1Compiland.MarkFullyConstructed();



        this.BeforeB2Compiland = this.BeforeBLib.GetOrCreateCompiland(this.BeforeDataCache, "b2.obj", this._beforeNextSymIndexId++ /* compilandSymIndex */, this.BeforeDIAAdapter);

        textZzContribution = this.BeforeB2Compiland.GetOrCreateCOFFGroupContribution(this.BeforeTextZzCG);
        textZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeTextZzCG.RVA +
                                                               this.BeforeA1Compiland.COFFGroupContributions[this.BeforeTextZzCG].Size +
                                                               this.BeforeA2Compiland.COFFGroupContributions[this.BeforeTextZzCG].Size, 100));
        textZzContribution.MarkFullyConstructed();
        textContribution = this.BeforeB2Compiland.GetOrCreateSectionContribution(this.BeforeTextSection);
        textContribution.AddRVARanges(textZzContribution.RVARanges);
        textContribution.MarkFullyConstructed();

        var rdataXxContribution = this.BeforeB2Compiland.GetOrCreateCOFFGroupContribution(this.BeforeRDataXxCG);
        rdataXxContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeRDataXxCG.RVA, 800));
        rdataXxContribution.MarkFullyConstructed();
        var rdataZzContribution = this.BeforeB2Compiland.GetOrCreateCOFFGroupContribution(this.BeforeRDataZzCG);
        rdataZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeRDataZzCG.RVA, 200));
        rdataZzContribution.MarkFullyConstructed();
        rdataContribution = this.BeforeB2Compiland.GetOrCreateSectionContribution(this.BeforeRDataSection);
        rdataContribution.AddRVARanges(rdataXxContribution.RVARanges);
        rdataContribution.AddRVARanges(rdataZzContribution.RVARanges);
        rdataContribution.MarkFullyConstructed();

        this.BeforeB2Compiland.MarkFullyConstructed();
        this.BeforeBLib.MarkFullyConstructed();



        this.BeforeCLib = new Library("c.lib");
        this.BeforeC1Compiland = this.BeforeCLib.GetOrCreateCompiland(this.BeforeDataCache, "c1.obj", this._beforeNextSymIndexId++ /* compilandSymIndex */, this.BeforeDIAAdapter);

        var virtCGContribution = this.BeforeC1Compiland.GetOrCreateCOFFGroupContribution(this.BeforeVirtCG);
        virtCGContribution.AddRVARange(RVARange.FromRVAAndSize(this.BeforeVirtSection.RVA, 300, isVirtualSize: true));
        virtCGContribution.MarkFullyConstructed();
        var virtContribution = this.BeforeC1Compiland.GetOrCreateSectionContribution(this.BeforeVirtSection);
        virtContribution.AddRVARanges(virtCGContribution.RVARanges);
        virtContribution.MarkFullyConstructed();

        this.BeforeC1Compiland.MarkFullyConstructed();
        this.BeforeCLib.MarkFullyConstructed();

        this.BeforeLibs = new HashSet<Library>()
            {
                this.BeforeALib,
                this.BeforeBLib,
                this.BeforeCLib
            };

        this.BeforeCompilands = new HashSet<Compiland>()
            {
                this.BeforeA1Compiland,
                this.BeforeA2Compiland,
                this.BeforeA3Compiland,
                this.BeforeB1Compiland,
                this.BeforeB2Compiland,
                this.BeforeC1Compiland
            };
    }

    private void SetupAfterCompilandsAndLibs()
    {
        this.AfterALib = new Library("a.lib");
        this._afterNextSymIndexId = 123;
        this.AfterA1Compiland = this.AfterALib.GetOrCreateCompiland(this.AfterDataCache, @"c:\b\a1.obj", this._afterNextSymIndexId++ /* compilandSymIndex */, this.AfterDIAAdapter);

        var textMnContribution = this.AfterA1Compiland.GetOrCreateCOFFGroupContribution(this.AfterTextMnCG);
        textMnContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterTextMnCG.RVA, 900));
        textMnContribution.MarkFullyConstructed();
        var textZzContribution = this.AfterA1Compiland.GetOrCreateCOFFGroupContribution(this.AfterTextZzCG);
        textZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterTextZzCG.RVA, 500));
        textZzContribution.MarkFullyConstructed();
        var textContribution = this.AfterA1Compiland.GetOrCreateSectionContribution(this.AfterTextSection);
        textContribution.AddRVARanges(textMnContribution.RVARanges);
        textContribution.AddRVARanges(textZzContribution.RVARanges);
        textContribution.MarkFullyConstructed();

        var dataXxContribution = this.AfterA1Compiland.GetOrCreateCOFFGroupContribution(this.AfterDataXxCG);
        dataXxContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterDataXxCG.RVA, 25));
        dataXxContribution.MarkFullyConstructed();
        var bssCGContribution = this.AfterA1Compiland.GetOrCreateCOFFGroupContribution(this.AfterBssCG);
        bssCGContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterBssCG.RVA, 300, isVirtualSize: true));
        bssCGContribution.MarkFullyConstructed();
        var dataContribution = this.AfterA1Compiland.GetOrCreateSectionContribution(this.AfterDataSection);
        dataContribution.AddRVARanges(dataXxContribution.RVARanges);
        dataContribution.AddRVARanges(bssCGContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        this.AfterA1Compiland.MarkFullyConstructed();



        this.AfterA2Compiland = this.AfterALib.GetOrCreateCompiland(this.AfterDataCache, "a2.obj", this._afterNextSymIndexId++ /* compilandSymIndex */, this.AfterDIAAdapter);

        textZzContribution = this.AfterA2Compiland.GetOrCreateCOFFGroupContribution(this.AfterTextZzCG);
        textZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterTextZzCG.RVA + this.AfterA1Compiland.COFFGroupContributions[this.AfterTextZzCG].Size, 400));
        textZzContribution.MarkFullyConstructed();
        textContribution = this.AfterA2Compiland.GetOrCreateSectionContribution(this.AfterTextSection);
        textContribution.AddRVARanges(textZzContribution.RVARanges);
        textContribution.MarkFullyConstructed();

        dataXxContribution = this.AfterA2Compiland.GetOrCreateCOFFGroupContribution(this.AfterDataXxCG);
        dataXxContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterDataXxCG.RVA + this.AfterA1Compiland.COFFGroupContributions[this.AfterDataXxCG].Size, 75));
        dataXxContribution.MarkFullyConstructed();
        dataContribution = this.AfterA2Compiland.GetOrCreateSectionContribution(this.AfterDataSection);
        dataContribution.AddRVARanges(dataXxContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        this.AfterA2Compiland.MarkFullyConstructed();



        this.AfterA4Compiland = this.AfterALib.GetOrCreateCompiland(this.AfterDataCache, "a4.obj", this._afterNextSymIndexId++ /* compilandSymIndex */, this.AfterDIAAdapter);

        bssCGContribution = this.AfterA4Compiland.GetOrCreateCOFFGroupContribution(this.AfterBssCG);
        bssCGContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterBssCG.RVA + this.AfterA1Compiland.COFFGroupContributions[this.AfterBssCG].VirtualSize, 400, isVirtualSize: true));
        bssCGContribution.MarkFullyConstructed();
        dataContribution = this.AfterA4Compiland.GetOrCreateSectionContribution(this.AfterDataSection);
        dataContribution.AddRVARanges(bssCGContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        var rdataFooContribution = this.AfterA4Compiland.GetOrCreateCOFFGroupContribution(this.AfterRDataFooCG);
        rdataFooContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterRDataFooCG.RVA, 500));
        rdataFooContribution.MarkFullyConstructed();
        var rdataAftContribution = this.AfterA4Compiland.GetOrCreateCOFFGroupContribution(this.AfterRDataAftCG);
        rdataAftContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterRDataAftCG.RVA, 300));
        rdataAftContribution.MarkFullyConstructed();
        var rdataContribution = this.AfterA4Compiland.GetOrCreateSectionContribution(this.AfterRDataSection);
        rdataContribution.AddRVARanges(rdataFooContribution.RVARanges);
        rdataContribution.AddRVARanges(rdataAftContribution.RVARanges);
        rdataContribution.MarkFullyConstructed();

        this.AfterA4Compiland.MarkFullyConstructed();

        this.AfterALib.MarkFullyConstructed();



        this.AfterBLib = new Library("b.lib");
        this.AfterB1Compiland = this.AfterBLib.GetOrCreateCompiland(this.AfterDataCache, "b1.obj", this._afterNextSymIndexId++ /* compilandSymIndexId */, this.AfterDIAAdapter);

        textMnContribution = this.AfterB1Compiland.GetOrCreateCOFFGroupContribution(this.AfterTextMnCG);
        textMnContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterTextMnCG.RVA + this.AfterA1Compiland.COFFGroupContributions[this.AfterTextMnCG].Size, 100));
        textMnContribution.MarkFullyConstructed();
        textContribution = this.AfterB1Compiland.GetOrCreateSectionContribution(this.AfterTextSection);
        textContribution.AddRVARanges(textMnContribution.RVARanges);
        textContribution.MarkFullyConstructed();

        dataXxContribution = this.AfterB1Compiland.GetOrCreateCOFFGroupContribution(this.AfterDataXxCG);
        dataXxContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterDataXxCG.RVA +
                                                               this.AfterA1Compiland.COFFGroupContributions[this.AfterDataXxCG].Size +
                                                               this.AfterA2Compiland.COFFGroupContributions[this.AfterDataXxCG].Size, 400));
        dataXxContribution.MarkFullyConstructed();
        var dataZzContribution = this.AfterB1Compiland.GetOrCreateCOFFGroupContribution(this.AfterDataZzCG);
        dataZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterDataZzCG.RVA, 500));
        dataZzContribution.MarkFullyConstructed();
        dataContribution = this.AfterB1Compiland.GetOrCreateSectionContribution(this.AfterDataSection);
        dataContribution.AddRVARanges(dataXxContribution.RVARanges);
        dataContribution.AddRVARanges(dataZzContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        this.AfterB1Compiland.MarkFullyConstructed();



        this.AfterB2Compiland = this.AfterBLib.GetOrCreateCompiland(this.AfterDataCache, "b2.obj", this._afterNextSymIndexId++ /* compilandSymIndexId */, this.AfterDIAAdapter);

        textZzContribution = this.AfterB2Compiland.GetOrCreateCOFFGroupContribution(this.AfterTextZzCG);
        textZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterTextZzCG.RVA +
                                                               this.AfterA1Compiland.COFFGroupContributions[this.AfterTextZzCG].Size +
                                                               this.AfterA2Compiland.COFFGroupContributions[this.AfterTextZzCG].Size, 100));
        textZzContribution.MarkFullyConstructed();
        textContribution = this.AfterB2Compiland.GetOrCreateSectionContribution(this.AfterTextSection);
        textContribution.AddRVARanges(textZzContribution.RVARanges);
        textContribution.MarkFullyConstructed();

        var rdataXxContribution = this.AfterB2Compiland.GetOrCreateCOFFGroupContribution(this.AfterRDataXxCG);
        rdataXxContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterRDataXxCG.RVA, 800));
        rdataXxContribution.MarkFullyConstructed();
        var rdataZzContribution = this.AfterB2Compiland.GetOrCreateCOFFGroupContribution(this.AfterRDataZzCG);
        rdataZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterRDataZzCG.RVA, 200));
        rdataZzContribution.MarkFullyConstructed();
        rdataContribution = this.AfterB2Compiland.GetOrCreateSectionContribution(this.AfterRDataSection);
        rdataContribution.AddRVARanges(rdataXxContribution.RVARanges);
        rdataContribution.AddRVARanges(rdataZzContribution.RVARanges);
        rdataContribution.MarkFullyConstructed();

        this.AfterB2Compiland.MarkFullyConstructed();
        this.AfterBLib.MarkFullyConstructed();


        this.AfterDLib = new Library("d.lib");
        this.AfterD1Compiland = this.AfterDLib.GetOrCreateCompiland(this.AfterDataCache, "d1.obj", this._afterNextSymIndexId++ /* compilandSymIndexId */, this.AfterDIAAdapter);

        var rsrcCGContribution = this.AfterD1Compiland.GetOrCreateCOFFGroupContribution(this.AfterRsrcCG);
        rsrcCGContribution.AddRVARange(RVARange.FromRVAAndSize(this.AfterRsrcSection.RVA, 200));
        rsrcCGContribution.MarkFullyConstructed();
        var rsrcContribution = this.AfterD1Compiland.GetOrCreateSectionContribution(this.AfterRsrcSection);
        rsrcContribution.AddRVARanges(rsrcCGContribution.RVARanges);
        rsrcContribution.MarkFullyConstructed();

        this.AfterD1Compiland.MarkFullyConstructed();
        this.AfterDLib.MarkFullyConstructed();

        this.AfterLibs = new HashSet<Library>()
            {
                this.AfterALib,
                this.AfterBLib,
                this.AfterDLib
            };

        this.AfterCompilands = new HashSet<Compiland>()
            {
                this.AfterA1Compiland,
                this.AfterA2Compiland,
                this.AfterA4Compiland,
                this.AfterB1Compiland,
                this.AfterB2Compiland,
                this.AfterD1Compiland,
            };
    }

    private void SetupDiffCompilandsAndLibs()
    {
        this.ALibDiff = new LibDiff(this.BeforeALib, this.AfterALib, this.BinarySectionDiffs, this.DiffDataCache);
        this.BLibDiff = new LibDiff(this.BeforeBLib, this.AfterBLib, this.BinarySectionDiffs, this.DiffDataCache);
        this.CLibDiff = new LibDiff(this.BeforeCLib, null, this.BinarySectionDiffs, this.DiffDataCache);
        this.DLibDiff = new LibDiff(null, this.AfterDLib, this.BinarySectionDiffs, this.DiffDataCache);

        var allLibDiffs = new List<LibDiff>()
            {
                this.ALibDiff,
                this.BLibDiff,
                this.CLibDiff,
                this.DLibDiff
            };

        this.LibDiffs = allLibDiffs;

        this.A1CompilandDiff = this.ALibDiff.CompilandDiffs[@"c:\a\a1.obj"];
        this.A2CompilandDiff = this.ALibDiff.CompilandDiffs["a2.obj"];
        this.A3CompilandDiff = this.ALibDiff.CompilandDiffs["a3.obj"];
        this.A4CompilandDiff = this.ALibDiff.CompilandDiffs["a4.obj"];
        this.B1CompilandDiff = this.BLibDiff.CompilandDiffs["b1.obj"];
        this.B2CompilandDiff = this.BLibDiff.CompilandDiffs["b2.obj"];
        this.C1CompilandDiff = this.CLibDiff.CompilandDiffs["c1.obj"];
        this.D1CompilandDiff = this.DLibDiff.CompilandDiffs["d1.obj"];

        var allCompilandDiffs = new List<CompilandDiff>()
            {
                this.A1CompilandDiff,
                this.A2CompilandDiff,
                this.A3CompilandDiff,
                this.A4CompilandDiff,
                this.B1CompilandDiff,
                this.B2CompilandDiff,
                this.C1CompilandDiff,
                this.D1CompilandDiff,
            };

        this.CompilandDiffs = allCompilandDiffs;
    }

    private void SetupSessionTaskParameters()
    {
        this.BeforeSessionTaskParameters = new SessionTaskParameters(
            this.MockBeforeSession.Object,
            this.BeforeDIAAdapter,
            this.BeforeDataCache);

        this.AfterSessionTaskParameters = new SessionTaskParameters(
            this.MockAfterSession.Object,
            this.AfterDIAAdapter,
            this.AfterDataCache);

        this.DiffSessionTaskParameters = new DiffSessionTaskParameters(this.MockDiffSession.Object, this.DiffDataCache, this.BeforeDIAAdapter, this.AfterDIAAdapter);
    }

    internal List<SymbolDiff> GenerateSymbolDiffsInBinarySectionList(BinarySectionDiff diffSection)
    {
        var beforeOnlyRanges = new List<RVARange>()
            {
                new RVARange(diffSection.BeforeSection!.RVA + 0x100, diffSection.BeforeSection.RVA + diffSection.BeforeSection.Size, isVirtualSize: true)
            };
        var afterOnlyRanges = new List<RVARange>()
            {
                new RVARange(diffSection.AfterSection!.RVA + 0x100, diffSection.AfterSection.RVA + diffSection.AfterSection.Size, isVirtualSize: true)
            };
        return GenerateSymbolDiffsInBinarySectionList(new List<RVARange>() { RVARange.FromRVAAndSize(diffSection.BeforeSection.RVA, diffSection.BeforeSection.Size, isVirtualSize: true) },
                                                      new List<RVARange>() { RVARange.FromRVAAndSize(diffSection.AfterSection.RVA, diffSection.AfterSection.Size, isVirtualSize: true) },
                                                      beforeOnlyRanges,
                                                      afterOnlyRanges);
    }

    public List<Symbol> GenerateABunchOfBeforeSymbols(List<RVARange> rvaRangesToGenerateInto, string namePrefix = "")
    {
        var generatedSymbols = new List<Symbol>();

        const uint symbolSize = 0x5;
        const uint spreadBetweenSymbols = 0x8;

        for (var rangeIndex = 0; rangeIndex < rvaRangesToGenerateInto.Count; rangeIndex++)
        {
            for (uint i = 0;
                i < 5 && (rvaRangesToGenerateInto[rangeIndex].RVAStart + (i * spreadBetweenSymbols) + symbolSize) <= rvaRangesToGenerateInto[rangeIndex].RVAEnd;
                i++)
            {
                var rva = rvaRangesToGenerateInto[rangeIndex].RVAStart + (i * spreadBetweenSymbols);
                generatedSymbols.Add(new Symbol(this.BeforeDataCache, $"{namePrefix}symbol {i}", rva, symbolSize, false /* isVirtualSize */, this._beforeNextSymIndexId++));
            }
        }

        return generatedSymbols;
    }

    public List<Symbol> GenerateABunchOfAfterSymbols(List<RVARange> rvaRangesToGenerateInto, string namePrefix = "")
    {
        var generatedSymbols = new List<Symbol>();

        const uint symbolSize = 0x3;
        const uint spreadBetweenSymbols = 0x8;

        for (var rangeIndex = 0; rangeIndex < rvaRangesToGenerateInto.Count; rangeIndex++)
        {
            for (uint i = 0;
                i < 5 && (rvaRangesToGenerateInto[rangeIndex].RVAStart + (i * spreadBetweenSymbols) + symbolSize) <= rvaRangesToGenerateInto[rangeIndex].RVAEnd;
                i++)
            {
                var rva = rvaRangesToGenerateInto[rangeIndex].RVAStart + (i * spreadBetweenSymbols);
                generatedSymbols.Add(new Symbol(this.AfterDataCache, $"{namePrefix}symbol {i}", rva, symbolSize, false /* isVirtualSize */, this._afterNextSymIndexId++));
            }
        }

        return generatedSymbols;
    }

    public List<Symbol> GenerateSymbolsInBinarySection(BinarySection section)
    {
        var rvaRanges = new List<RVARange>() { RVARange.FromRVAAndSize(section.RVA, section.Size) };

        if (this.BeforeSections.Contains(section))
        {
            return GenerateABunchOfBeforeSymbols(rvaRanges);
        }
        else
        {
            return GenerateABunchOfAfterSymbols(rvaRanges);
        }
    }

    private List<SymbolDiff> GenerateABunchOfDiffSymbols(List<RVARange> beforeRVARangesToGenerateInto,
                                                         List<RVARange> afterRVARangesToGenerateInto,
                                                         List<RVARange> beforeOnlySymbolRanges,
                                                         List<RVARange> afterOnlySymbolRanges,
                                                         Type? typeOfSymbolsToGenerate = null)
    {
        List<SymbolDiff> generatedSymbolDiffs;
        if (beforeRVARangesToGenerateInto.Count != afterRVARangesToGenerateInto.Count)
        {
            throw new Exception("These need to match, since each diff will be created from a before/after matching pair");
        }

        generatedSymbolDiffs = new List<SymbolDiff>();

        const uint beforeSymbolSize = 0x5;
        const uint afterSymbolSize = 0x3;
        const uint spreadBetweenSymbols = 0x8;

        for (var rangeIndex = 0; rangeIndex < beforeRVARangesToGenerateInto.Count; rangeIndex++)
        {
            for (uint i = 0;
                i < 5 && (beforeRVARangesToGenerateInto[rangeIndex].RVAStart + (i * spreadBetweenSymbols) + beforeSymbolSize) <= beforeRVARangesToGenerateInto[rangeIndex].RVAEnd &&
                         (afterRVARangesToGenerateInto[rangeIndex].RVAStart + (i * spreadBetweenSymbols) + afterSymbolSize) <= afterRVARangesToGenerateInto[rangeIndex].RVAEnd;
                i++)
            {
                var beforeSymbol = GenerateTestSymbol(beforeRVARangesToGenerateInto[rangeIndex], beforeSymbolSize, spreadBetweenSymbols, i, "beforeAndAfter", typeOfSymbolsToGenerate, isForBeforeSession: true);
                var afterSymbol = GenerateTestSymbol(afterRVARangesToGenerateInto[rangeIndex], afterSymbolSize, spreadBetweenSymbols, i, "beforeAndAfter", typeOfSymbolsToGenerate, isForBeforeSession: false);
                generatedSymbolDiffs.Add(SymbolDiffFactory.CreateSymbolDiff(beforeSymbol, afterSymbol, this.DiffDataCache));
            }
        }

        foreach (var beforeOnlyRange in beforeOnlySymbolRanges)
        {
            for (uint i = 0;
                i < 5 && (beforeOnlyRange.RVAStart + (i * spreadBetweenSymbols) + beforeSymbolSize) <= beforeOnlyRange.RVAEnd;
                i++)
            {
                var beforeSymbol = GenerateTestSymbol(beforeOnlyRange, beforeSymbolSize, spreadBetweenSymbols, i, "beforeOnly", typeOfSymbolsToGenerate, isForBeforeSession: true);
                generatedSymbolDiffs.Add(SymbolDiffFactory.CreateSymbolDiff(beforeSymbol, null, this.DiffDataCache));
            }
        }

        foreach (var afterOnlyRange in afterOnlySymbolRanges)
        {
            for (uint i = 0;
                i < 5 && (afterOnlyRange.RVAStart + (i * spreadBetweenSymbols) + afterSymbolSize) <= afterOnlyRange.RVAEnd;
                i++)
            {
                var afterSymbol = GenerateTestSymbol(afterOnlyRange, afterSymbolSize, spreadBetweenSymbols, i, "afterOnly", typeOfSymbolsToGenerate, isForBeforeSession: false);
                generatedSymbolDiffs.Add(SymbolDiffFactory.CreateSymbolDiff(null, afterSymbol, this.DiffDataCache));
            }
        }

        return generatedSymbolDiffs;
    }

    private ISymbol GenerateTestSymbol(RVARange rangeToGenerateInto, uint symbolSize, uint spreadBetweenSymbols, uint i, string namePrefix, Type? typeOfSymbolToGenerate, bool isForBeforeSession)
    {
        var rva = rangeToGenerateInto.RVAStart + (i * spreadBetweenSymbols);
        var name = $"{namePrefix}{i}";
        if (typeOfSymbolToGenerate == typeof(StaticDataSymbol))
        {
            return new StaticDataSymbol(isForBeforeSession ? this.BeforeDataCache : this.AfterDataCache,
                                        name, rva,
                                        size: symbolSize,
                                        isVirtualSize: false,
                                        symIndexId: isForBeforeSession ? this._beforeNextSymIndexId++ : this._afterNextSymIndexId++,
                                        dataKind: DataKind.DataIsFileStatic,
                                        type: null, referencedIn: null, functionParent: null);
        }
        else
        {
            return new TestSymbol(name, rva,
                                  size: symbolSize,
                                  virtualSize: symbolSize);
        }
    }

    private int _nextBeforeUDTIndex = 1;
    private int _nextAfterUDTIndex = 1;

    private List<UserDefinedTypeSymbol> GenerateABunchOfBeforeUDTs(bool shouldBeCOMTypes, string namePrefix = "")
    {
        var udts = new List<UserDefinedTypeSymbol>();

        if (shouldBeCOMTypes && this.BeforeIUnknownUDT is null)
        {
            this.BeforeIUnknownUDT = new UserDefinedTypeSymbol(this.BeforeDataCache, this.BeforeDIAAdapter, this.MockBeforeSession.Object, "IUnknown", 8, this._beforeNextSymIndexId++, UserDefinedTypeKind.UdtClass);
            udts.Add(this.BeforeIUnknownUDT);
        }

        var iunknownBaseTypeIDs = new List<(uint, uint)>();

        if (shouldBeCOMTypes)
        {
            iunknownBaseTypeIDs.Add((this.BeforeIUnknownUDT.SymIndexId, 0));
        };

        var endIndex = this._nextBeforeUDTIndex + 3;

        for (; this._nextBeforeUDTIndex < endIndex; this._nextBeforeUDTIndex++)
        {
            uint size = 10;
            var udt = new UserDefinedTypeSymbol(this.BeforeDataCache, this.BeforeDIAAdapter, this.MockBeforeSession.Object,
                                                $"{namePrefix}UDT{(shouldBeCOMTypes ? "COMType" : "")}{this._nextBeforeUDTIndex}",
                                                size, this._beforeNextSymIndexId++, UserDefinedTypeKind.UdtClass);
            if (shouldBeCOMTypes)
            {
                this.BeforeDIAAdapter.BaseTypeIDsToFindByUDT.Add(udt, iunknownBaseTypeIDs);
            }
            udt.LoadBaseTypes(this.BeforeDataCache, this.BeforeDIAAdapter, CancellationToken.None);

            var baseTypeIDs = new List<(uint, uint)>()
                {
                    (udt.SymIndexId, 0)
                };

            var derivedUDT = new UserDefinedTypeSymbol(this.BeforeDataCache, this.BeforeDIAAdapter, this.MockBeforeSession.Object,
                                                       $"{namePrefix}UDT{(shouldBeCOMTypes ? "COMType" : "")}{this._nextBeforeUDTIndex}_Derived",
                                                       size, this._beforeNextSymIndexId++, UserDefinedTypeKind.UdtClass);
            this.BeforeDIAAdapter.BaseTypeIDsToFindByUDT.Add(derivedUDT, baseTypeIDs);
            derivedUDT.LoadBaseTypes(this.BeforeDataCache, this.BeforeDIAAdapter, CancellationToken.None);
            udt.AddDerivedType(derivedUDT);

            udt.MarkDerivedTypesLoaded();


            if (shouldBeCOMTypes)
            {
                this.BeforeIUnknownUDT.AddDerivedType(udt);
            }

            udts.Add(udt);
        }

        return udts;
    }

    private List<UserDefinedTypeSymbol> GenerateABunchOfAfterUDTs(bool shouldBeCOMTypes, string namePrefix = "")
    {
        var udts = new List<UserDefinedTypeSymbol>();

        if (shouldBeCOMTypes && this.AfterIUnknownUDT is null)
        {
            this.AfterIUnknownUDT = new UserDefinedTypeSymbol(this.AfterDataCache, this.AfterDIAAdapter, this.MockAfterSession.Object, "IUnknown", 8, this._afterNextSymIndexId++, UserDefinedTypeKind.UdtClass);
            udts.Add(this.AfterIUnknownUDT);
        }

        var iunknownBaseTypeIDs = new List<(uint, uint)>();

        if (shouldBeCOMTypes)
        {
            iunknownBaseTypeIDs.Add((this.AfterIUnknownUDT.SymIndexId, 0));
        };

        var endIndex = this._nextAfterUDTIndex + 3;

        for (; this._nextAfterUDTIndex < endIndex; this._nextAfterUDTIndex++)
        {
            uint size = 10;
            var udt = new UserDefinedTypeSymbol(this.AfterDataCache, this.AfterDIAAdapter, this.MockAfterSession.Object,
                                                $"{namePrefix}UDT{(shouldBeCOMTypes ? "COMType" : "")}{this._nextAfterUDTIndex}",
                                                size, this._afterNextSymIndexId++, UserDefinedTypeKind.UdtClass);
            if (shouldBeCOMTypes)
            {
                this.AfterDIAAdapter.BaseTypeIDsToFindByUDT.Add(udt, iunknownBaseTypeIDs);
            }
            udt.LoadBaseTypes(this.AfterDataCache, this.AfterDIAAdapter, CancellationToken.None);

            var baseTypeIDs = new List<(uint, uint)>()
                {
                    (udt.SymIndexId, 0)
                };

            var derivedUDT = new UserDefinedTypeSymbol(this.AfterDataCache, this.AfterDIAAdapter, this.MockAfterSession.Object,
                                                       $"{namePrefix}UDT{(shouldBeCOMTypes ? "COMType" : "")}{this._nextAfterUDTIndex}_Derived",
                                                       size, this._afterNextSymIndexId++, UserDefinedTypeKind.UdtClass);
            this.AfterDIAAdapter.BaseTypeIDsToFindByUDT.Add(derivedUDT, baseTypeIDs);
            derivedUDT.LoadBaseTypes(this.AfterDataCache, this.AfterDIAAdapter, CancellationToken.None);
            udt.AddDerivedType(derivedUDT);

            udt.MarkDerivedTypesLoaded();

            if (shouldBeCOMTypes)
            {
                this.AfterIUnknownUDT.AddDerivedType(udt);
            }

            udts.Add(udt);
        }

        return udts;
    }

    private List<TypeSymbolDiff> GenerateUDTSymbolDiffs()
    {
        var befores = GenerateABunchOfBeforeUDTs(shouldBeCOMTypes: true);
        befores.AddRange(GenerateABunchOfBeforeUDTs(shouldBeCOMTypes: false));
        befores.AddRange(GenerateABunchOfBeforeUDTs(shouldBeCOMTypes: true, namePrefix: "BeforeOnly"));
        befores.AddRange(GenerateABunchOfBeforeUDTs(shouldBeCOMTypes: false, namePrefix: "BeforeOnly"));
        var afters = GenerateABunchOfAfterUDTs(shouldBeCOMTypes: true);
        afters.AddRange(GenerateABunchOfAfterUDTs(shouldBeCOMTypes: false));
        afters.AddRange(GenerateABunchOfAfterUDTs(shouldBeCOMTypes: true, namePrefix: "AfterOnly"));
        afters.AddRange(GenerateABunchOfAfterUDTs(shouldBeCOMTypes: false, namePrefix: "AfterOnly"));

        this.BeforeIUnknownUDT.MarkDerivedTypesLoaded();
        this.AfterIUnknownUDT.MarkDerivedTypesLoaded();

        var udtDiffs = new List<TypeSymbolDiff>();
        foreach (var before in befores)
        {
            var matchingAfter = afters.FirstOrDefault(after => after.IsVeryLikelyTheSameAs(before));
            udtDiffs.Add(new TypeSymbolDiff(before, matchingAfter));

            if (matchingAfter != null)
            {
                afters.Remove(matchingAfter);
            }
        }
        foreach (var after in afters)
        {
            // By definition these had no matching before, since we would have caught that above
            udtDiffs.Add(new TypeSymbolDiff(null, after));
        }

        return udtDiffs;
    }

    internal List<SymbolDiff> GenerateSymbolDiffsInBinarySectionList(List<RVARange> beforeRVARangesToGenerateInto,
                                                                     List<RVARange> afterRVARangesToGenerateInto,
                                                                     List<RVARange> beforeOnlySymbolRanges,
                                                                     List<RVARange> afterOnlySymbolRanges)
        => GenerateABunchOfDiffSymbols(beforeRVARangesToGenerateInto, afterRVARangesToGenerateInto, beforeOnlySymbolRanges, afterOnlySymbolRanges);

    internal List<SymbolDiff> GenerateSymbolDiffsInCOFFGroupList(COFFGroupDiff diffCG)
    {
        var isDiffCGVirtualSizeOnly = diffCG.BeforeCOFFGroup?.IsVirtualSizeOnly == true && diffCG.AfterCOFFGroup?.IsVirtualSizeOnly == true;

        var beforeOnlyRanges = new List<RVARange>()
            {
                new RVARange(diffCG.BeforeCOFFGroup!.RVA + 0x100, diffCG.BeforeCOFFGroup.RVA + diffCG.BeforeCOFFGroup.Size, isVirtualSize: isDiffCGVirtualSizeOnly)
            };
        var afterOnlyRanges = new List<RVARange>()
            {
                new RVARange(diffCG.AfterCOFFGroup!.RVA + 0x100, diffCG.AfterCOFFGroup.RVA + diffCG.AfterCOFFGroup.Size, isVirtualSize: isDiffCGVirtualSizeOnly)
            };
        return GenerateSymbolDiffsInCOFFGroupList(new List<RVARange>() { RVARange.FromRVAAndSize(diffCG.BeforeCOFFGroup.RVA, diffCG.BeforeCOFFGroup.Size, isVirtualSize: isDiffCGVirtualSizeOnly) },
                                                  new List<RVARange>() { RVARange.FromRVAAndSize(diffCG.AfterCOFFGroup.RVA, diffCG.AfterCOFFGroup.Size, isVirtualSize: isDiffCGVirtualSizeOnly) },
                                                  beforeOnlyRanges,
                                                  afterOnlyRanges);
    }

    internal List<SymbolDiff> GenerateSymbolDiffsInCOFFGroupList(List<RVARange> beforeRVARangesToGenerateInto,
                                                                 List<RVARange> afterRVARangesToGenerateInto,
                                                                 List<RVARange> beforeOnlySymbolRanges,
                                                                 List<RVARange> afterOnlySymbolRanges)
        => GenerateABunchOfDiffSymbols(beforeRVARangesToGenerateInto, afterRVARangesToGenerateInto, beforeOnlySymbolRanges, afterOnlySymbolRanges);

    internal List<SymbolDiff> GenerateSymbolDiffsInCompilandList(CompilandDiff diffCompiland, Type? typeOfSymbolsToGenerate)
    {
        var beforeRVAStart = diffCompiland.BeforeCompiland!.SectionContributions.Values.First().RVARanges[0].RVAStart;
        var beforeSize = diffCompiland.BeforeCompiland.SectionContributions.Values.First().RVARanges[0].Size;

        var beforeOnlyRanges = new List<RVARange>()
            {
                new RVARange(beforeRVAStart + 0x100, beforeRVAStart + beforeSize, isVirtualSize: true)
            };

        var afterRVAStart = diffCompiland.AfterCompiland!.SectionContributions.Values.First().RVARanges[0].RVAStart;
        var afterSize = diffCompiland.AfterCompiland.SectionContributions.Values.First().RVARanges[0].Size;

        var afterOnlyRanges = new List<RVARange>()
            {
                new RVARange(afterRVAStart + 0x100, afterRVAStart + afterSize, isVirtualSize: true)
            };

        return GenerateSymbolDiffsInCompilandList(new List<RVARange>() { RVARange.FromRVAAndSize(beforeRVAStart, beforeSize, isVirtualSize: true) },
                                                  new List<RVARange>() { RVARange.FromRVAAndSize(afterRVAStart, afterSize, isVirtualSize: true) },
                                                  beforeOnlyRanges,
                                                  afterOnlyRanges,
                                                  typeOfSymbolsToGenerate);
    }

    internal List<SymbolDiff> GenerateSymbolDiffsInCompilandList(List<RVARange> beforeRVARangesToGenerateInto,
                                                                 List<RVARange> afterRVARangesToGenerateInto,
                                                                 List<RVARange> beforeOnlySymbolRanges,
                                                                 List<RVARange> afterOnlySymbolRanges,
                                                                 Type? typeOfsymbolsToGenerate)
        => GenerateABunchOfDiffSymbols(beforeRVARangesToGenerateInto, afterRVARangesToGenerateInto, beforeOnlySymbolRanges, afterOnlySymbolRanges, typeOfsymbolsToGenerate);

    internal List<SymbolDiff> GenerateSymbolDiffsInLibList(LibDiff diffLib)
    {
        var beforeRVAStart = diffLib.BeforeLib!.SectionContributions.Values.First().RVARanges[0].RVAStart;
        var beforeSize = diffLib.BeforeLib.SectionContributions.Values.First().RVARanges[0].Size;

        var beforeOnlyRanges = new List<RVARange>()
            {
                new RVARange(beforeRVAStart + 0x100, beforeRVAStart + beforeSize, isVirtualSize: true)
            };

        var afterRVAStart = diffLib.AfterLib!.SectionContributions.Values.First().RVARanges[0].RVAStart;
        var afterSize = diffLib.AfterLib.SectionContributions.Values.First().RVARanges[0].Size;

        var afterOnlyRanges = new List<RVARange>()
            {
                new RVARange(afterRVAStart + 0x100, afterRVAStart + afterSize, isVirtualSize: true)
            };

        return GenerateSymbolDiffsInLibList(new List<RVARange>() { RVARange.FromRVAAndSize(beforeRVAStart, beforeSize, isVirtualSize: true) },
                                            new List<RVARange>() { RVARange.FromRVAAndSize(afterRVAStart, afterSize, isVirtualSize: true) },
                                            beforeOnlyRanges,
                                            afterOnlyRanges);
    }

    internal List<SymbolDiff> GenerateSymbolDiffsInLibList(List<RVARange> beforeRVARangesToGenerateInto,
                                                           List<RVARange> afterRVARangesToGenerateInto,
                                                           List<RVARange> beforeOnlySymbolRanges,
                                                           List<RVARange> afterOnlySymbolRanges)
        => GenerateABunchOfDiffSymbols(beforeRVARangesToGenerateInto, afterRVARangesToGenerateInto, beforeOnlySymbolRanges, afterOnlySymbolRanges);

    internal List<DuplicateDataItemDiff> GenerateDuplicateDataItemDiffs(out List<DuplicateDataItem> beforeDDIList,
                                                                        out List<DuplicateDataItem> afterDDIList)
    {
        beforeDDIList = new List<DuplicateDataItem>();
        afterDDIList = new List<DuplicateDataItem>();
        var ddiDiffs = new List<DuplicateDataItemDiff>();

        var allSymbolDiffsInCompilandDiff = GenerateSymbolDiffsInCompilandList(this.A1CompilandDiff, typeof(StaticDataSymbol));

        // allSymbolDiffsInCompilandDiff will contain symbols in both before/after, as well as symbols only in before or only in after, so
        // we'll hit all 3 cases just by looping over that list.
        foreach (var symbolDiff in allSymbolDiffsInCompilandDiff)
        {
            var before = symbolDiff.BeforeSymbol != null ? new DuplicateDataItem((StaticDataSymbol)symbolDiff.BeforeSymbol, this.BeforeA1Compiland) : null;
            var after = symbolDiff.AfterSymbol != null ? new DuplicateDataItem((StaticDataSymbol)symbolDiff.AfterSymbol, this.AfterA1Compiland) : null;
            var diff = new DuplicateDataItemDiff(before, after, this.DiffDataCache);

            if (before != null)
            {
                beforeDDIList.Add(before);
            }
            if (after != null)
            {
                afterDDIList.Add(after);
            }
            ddiDiffs.Add(diff);
        }

        return ddiDiffs;
    }

    internal List<WastefulVirtualItemDiff> GenerateWastefulVirtualItemDiffs(out List<WastefulVirtualItem> beforeWVIList,
                                                                            out List<WastefulVirtualItem> afterWVIList)
    {
        beforeWVIList = new List<WastefulVirtualItem>();
        afterWVIList = new List<WastefulVirtualItem>();
        var wviDiffs = new List<WastefulVirtualItemDiff>();

        var udtDiffs = GenerateUDTSymbolDiffs();

        var beforeFunction1 = new SimpleFunctionCodeSymbol(this.BeforeDataCache, "SomeVirtual", 0, 15, this._beforeNextSymIndexId++, isVirtual: true);
        var beforeFunction2 = new SimpleFunctionCodeSymbol(this.BeforeDataCache, "AnotherVirtual", 0, 15, this._beforeNextSymIndexId++, isVirtual: true);
        var afterFunction1 = new SimpleFunctionCodeSymbol(this.AfterDataCache, "SomeVirtual", 0, 15, this._afterNextSymIndexId++, isVirtual: true);
        var afterFunction2 = new SimpleFunctionCodeSymbol(this.AfterDataCache, "AnotherVirtual", 0, 15, this._afterNextSymIndexId++, isVirtual: true);

        var i = 0;
        foreach (var symbolDiff in udtDiffs)
        {
            var beforeUDT = symbolDiff.BeforeSymbol as UserDefinedTypeSymbol;
            var afterUDT = symbolDiff.AfterSymbol as UserDefinedTypeSymbol;
            var isCOMType = IsCOMType(beforeUDT ?? afterUDT!);

            var before = beforeUDT != null ? new WastefulVirtualItem(beforeUDT, isCOMType, this.MockBeforeSession.Object.BytesPerWord) : null;
            if (before != null)
            {
                before.AddWastedOverrideThatIsNotPureWithNoOverrides(beforeFunction1);
                // Every 2nd one, which will sometimes have a matching 'after' (see below) and sometimes won't.
                if (i % 2 == 0)
                {
                    before.AddWastedOverrideThatIsNotPureWithNoOverrides(beforeFunction2);
                }
            }

            var after = afterUDT != null ? new WastefulVirtualItem(afterUDT, isCOMType, this.MockAfterSession.Object.BytesPerWord) : null;
            if (after != null)
            {
                after.AddWastedOverrideThatIsNotPureWithNoOverrides(afterFunction1);
                // Every 3rd one (which never lines up with 'beforeFunction2') and
                // every 4th one (which always lines up to a corresponding 'beforeFunction2')
                if (i % 3 == 0 || i % 4 == 0)
                {
                    after.AddWastedOverrideThatIsNotPureWithNoOverrides(afterFunction2);
                }
            }

            var diff = new WastefulVirtualItemDiff(before, after, this.DiffDataCache, this.BeforeDIAAdapter, this.AfterDIAAdapter);

            if (before != null)
            {
                beforeWVIList.Add(before);
            }
            if (after != null)
            {
                afterWVIList.Add(after);
            }

            if (diff.WastedSizeDiff != 0 ||
                diff.TypeHierarchyChanges.Count != 0 ||
                diff.WastedOverrideChanges.Count != 0)
            {
                wviDiffs.Add(diff);
            }

            i++;
        }

        return wviDiffs;
    }

    internal List<TemplateFoldabilityItemDiff> GenerateTemplateFoldabilityItemDiffs(out List<TemplateFoldabilityItem> beforeTFIList,
                                                                                    out List<TemplateFoldabilityItem> afterTFIList)
    {
        beforeTFIList = TestTemplateFoldabilityItems.GenerateSomeTemplateFoldabilityItems(this.MockBeforeSession, this.BeforeDataCache, this.BeforeDIAAdapter, ref this._beforeNextSymIndexId, CancellationToken.None);
        afterTFIList = TestTemplateFoldabilityItems.GenerateSomeTemplateFoldabilityItems(this.MockAfterSession, this.AfterDataCache, this.AfterDIAAdapter, ref this._afterNextSymIndexId, CancellationToken.None);
        var tfiDiffs = new List<TemplateFoldabilityItemDiff>
            {
                // Generate one with just a 'before'
                new TemplateFoldabilityItemDiff(beforeTFIList[0], null),

                // Generate one with just an 'after'
                new TemplateFoldabilityItemDiff(null, afterTFIList[1])
            };

        // Then some that have both a before and an after
        for (var i = 2; i < beforeTFIList.Count; i++)
        {
            tfiDiffs.Add(new TemplateFoldabilityItemDiff(beforeTFIList[i], afterTFIList[i]));
        }

        return tfiDiffs;
    }

    internal List<TypeLayoutItemDiff> GenerateTypeLayoutItemDiffs(out List<TypeLayoutItem> beforeTLIList,
                                                                  out List<TypeLayoutItem> afterTLIList)
    {
        beforeTLIList = new List<TypeLayoutItem>();
        afterTLIList = new List<TypeLayoutItem>();
        var tliDiffs = new List<TypeLayoutItemDiff>();

        var udtDiffs = GenerateUDTSymbolDiffs();

        var beforeIntTypeSymbol = new BasicTypeSymbol(this.BeforeDataCache, "int", 4, this._beforeNextSymIndexId++);
        var afterIntTypeSymbol = new BasicTypeSymbol(this.AfterDataCache, "int", 4, this._afterNextSymIndexId++);
        var beforeBoolTypeSymbol = new BasicTypeSymbol(this.BeforeDataCache, "bool", 1, this._beforeNextSymIndexId++);
        var afterBoolTypeSymbol = new BasicTypeSymbol(this.AfterDataCache, "bool", 1, this._afterNextSymIndexId++);

        for (var i = 0; i < udtDiffs.Count; i++)
        {
            TypeLayoutItem? beforeTLI = null, afterTLI = null;

            if (udtDiffs[i].BeforeSymbol != null)
            {
                var beforeMemberLayouts = new TypeLayoutItemMember[i % 2 == 0 ? 2 : 3];
                if (i % 2 == 0)
                {
                    // int, then int
                    var int1Data = new MemberDataSymbol(this.BeforeDataCache, "firstInt", size: 4, this._beforeNextSymIndexId++, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 0, type: beforeIntTypeSymbol);
                    beforeMemberLayouts[0] = TypeLayoutItemMember.FromDataSymbol(int1Data, 0);
                    var int2Data = new MemberDataSymbol(this.BeforeDataCache, "secondInt", size: 4, this._beforeNextSymIndexId++, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 4, type: beforeIntTypeSymbol);
                    beforeMemberLayouts[1] = TypeLayoutItemMember.FromDataSymbol(int2Data, 0);
                }
                else
                {
                    // bool (sometimes a bitfield), then padding, then int
                    var shouldBoolBeBitfield = i % 4 == 1;

                    var boolData = new MemberDataSymbol(this.BeforeDataCache, "myBool", size: shouldBoolBeBitfield ? 4u : 1u, this._beforeNextSymIndexId++, isStaticMember: false, isBitField: shouldBoolBeBitfield, bitStartPosition: 0, offset: 0, type: beforeBoolTypeSymbol);
                    beforeMemberLayouts[0] = TypeLayoutItemMember.FromDataSymbol(boolData, 0);
                    beforeMemberLayouts[1] = TypeLayoutItemMember.CreateAlignmentMember(3 + (shouldBoolBeBitfield ? 0.5M : 0M), 1 - (shouldBoolBeBitfield ? 0.5M : 0M), false, 0, false);
                    var intData = new MemberDataSymbol(this.BeforeDataCache, "myInt", size: 4, this._beforeNextSymIndexId++, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 4, type: beforeIntTypeSymbol);
                    beforeMemberLayouts[2] = TypeLayoutItemMember.FromDataSymbol(intData, 0);
                }
                beforeTLI = new TypeLayoutItem((UserDefinedTypeSymbol)udtDiffs[i].BeforeSymbol!, alignmentWasteExclusive: beforeMemberLayouts.Where(ml => ml.IsAlignmentMember).Sum(ml => ml.Size), usedForVFPtrsExclusive: 0, baseTypeLayouts: null, memberLayouts: beforeMemberLayouts);
                beforeTLIList.Add(beforeTLI);
            }

            if (udtDiffs[i].AfterSymbol != null)
            {
                var afterMemberLayouts = new TypeLayoutItemMember[i % 3 == 1 ? 2 : 3];
                if (i % 3 == 1)
                {
                    // int, then int
                    var int1Data = new MemberDataSymbol(this.AfterDataCache, "firstInt", size: 4, this._afterNextSymIndexId++, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 0, type: afterIntTypeSymbol);
                    afterMemberLayouts[0] = TypeLayoutItemMember.FromDataSymbol(int1Data, 0);
                    var int2Data = new MemberDataSymbol(this.AfterDataCache, "secondInt", size: 4, this._afterNextSymIndexId++, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 4, type: afterIntTypeSymbol);
                    afterMemberLayouts[1] = TypeLayoutItemMember.FromDataSymbol(int2Data, 0);
                }
                else
                {
                    // bool (sometimes a bitfield), then padding, then int
                    var shouldBoolBeBitfield = i % 4 == 1;

                    var boolData = new MemberDataSymbol(this.AfterDataCache, "myBool", size: shouldBoolBeBitfield ? 2u : 1u, this._afterNextSymIndexId++, isStaticMember: false, isBitField: shouldBoolBeBitfield, bitStartPosition: 0, offset: 0, type: afterBoolTypeSymbol);
                    afterMemberLayouts[0] = TypeLayoutItemMember.FromDataSymbol(boolData, 0);
                    afterMemberLayouts[1] = TypeLayoutItemMember.CreateAlignmentMember(3 + (shouldBoolBeBitfield ? 0.75M : 0M), 1 - (shouldBoolBeBitfield ? 0.75M : 0M), false, 0, false);
                    var intData = new MemberDataSymbol(this.AfterDataCache, "myInt", size: 4, this._afterNextSymIndexId++, isStaticMember: false, isBitField: false, bitStartPosition: 0, offset: 4, type: afterIntTypeSymbol);
                    afterMemberLayouts[2] = TypeLayoutItemMember.FromDataSymbol(intData, 0);
                }
                afterTLI = new TypeLayoutItem((UserDefinedTypeSymbol)udtDiffs[i].AfterSymbol!, alignmentWasteExclusive: afterMemberLayouts.Where(ml => ml.IsAlignmentMember).Sum(ml => ml.Size), usedForVFPtrsExclusive: 0, baseTypeLayouts: null, memberLayouts: afterMemberLayouts);
                afterTLIList.Add(afterTLI);
            }

            tliDiffs.Add(new TypeLayoutItemDiff(beforeTLI, afterTLI));
        }

        return tliDiffs;
    }

    internal List<FunctionCodeSymbolDiff> GenerateFunctionCodeSymbolDiffs(out List<IFunctionCodeSymbol> beforeFunctionsList,
                                                                          out List<IFunctionCodeSymbol> afterFunctionList)
    {
        beforeFunctionsList = GenerateBeforeFunctionCodeSymbols();
        afterFunctionList = GenerateAfterFunctionCodeSymbols();

        var functionCodeSymbolDiffs = new List<FunctionCodeSymbolDiff>();
        foreach (var before in beforeFunctionsList)
        {
            var matchingAfter = afterFunctionList.FirstOrDefault(before.IsVeryLikelyTheSameAs);

            var allBlockDiffsInFunction = new List<CodeBlockSymbolDiff>(capacity: 10);
            var beforeBlocks = new List<CodeBlockSymbol>(before?.Blocks ?? Enumerable.Empty<CodeBlockSymbol>());
            var afterBlocks = new List<CodeBlockSymbol>(matchingAfter?.Blocks ?? Enumerable.Empty<CodeBlockSymbol>());
            foreach (var beforeBlock in beforeBlocks)
            {
                var matchingAfterBlock = afterBlocks.FirstOrDefault(beforeBlock.IsVeryLikelyTheSameAs);
                if (matchingAfterBlock != null)
                {
                    afterBlocks.Remove(matchingAfterBlock); // Make the list smaller so the operation isn't a full two passes (one here, one in the loop below)
                }

                allBlockDiffsInFunction.Add(new CodeBlockSymbolDiff(beforeBlock, matchingAfterBlock));
            }
            foreach (var afterBlock in afterBlocks)
            {
                // This one wasn't found in 'before' so it's new in the 'after'
                allBlockDiffsInFunction.Add(new CodeBlockSymbolDiff(null, afterBlock));
            }

            functionCodeSymbolDiffs.Add(new FunctionCodeSymbolDiff(before, matchingAfter, allBlockDiffsInFunction));
        }
        foreach (var after in afterFunctionList)
        {
            // Find only the things that we didn't match above (those present only in the 'after' list)
            if (null == beforeFunctionsList.FirstOrDefault(after.IsVeryLikelyTheSameAs))
            {
                var allBlockDiffsInFunction = new List<CodeBlockSymbolDiff>(capacity: 10);
                foreach (var afterBlock in after.Blocks)
                {
                    // This one wasn't found in 'before' so it's new in the 'after'
                    allBlockDiffsInFunction.Add(new CodeBlockSymbolDiff(null, afterBlock));
                }
                functionCodeSymbolDiffs.Add(new FunctionCodeSymbolDiff(null, after, allBlockDiffsInFunction));
            }
        }

        return functionCodeSymbolDiffs;
    }

    private List<IFunctionCodeSymbol> GenerateBeforeFunctionCodeSymbols()
    {
        var boolType = new BasicTypeSymbol(this.BeforeDataCache, "bool", size: 1, symIndexId: this._beforeNextSymIndexId++);
        var intType = new BasicTypeSymbol(this.BeforeDataCache, "int", size: 1, symIndexId: this._beforeNextSymIndexId++);
        var voidType = new BasicTypeSymbol(this.BeforeDataCache, "void", size: 0, symIndexId: this._beforeNextSymIndexId++);
        var aComplexType = new UserDefinedTypeSymbol(this.BeforeDataCache, this.BeforeDIAAdapter, this.MockBeforeSession.Object, "AComplex::Type", instanceSize: 10, symIndexId: this._beforeNextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass);

        return new List<IFunctionCodeSymbol>()
            {
                // RVA 100: MyType::MyFunction(bool, int)
                new SimpleFunctionCodeSymbol(this.BeforeDataCache, "MyType::MyFunction", rva: 100, size: 10, symIndexId: this._beforeNextSymIndexId++,
                                             functionType: new FunctionTypeSymbol(this.BeforeDataCache, "type name", size: 0, symIndexId: this._beforeNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType, intType }, returnValueType: voidType)),

                // RVA 300: SomeNamespace::MyType::FoldableFunction<AComplex::Type,bool>(bool, AComplex::Type)
                new SimpleFunctionCodeSymbol(this.BeforeDataCache, "SomeNamespace::MyType::FoldableFunction<AComplex::Type,bool>", rva: 300, size: 10, symIndexId: this._beforeNextSymIndexId++,
                                            functionType: new FunctionTypeSymbol(this.BeforeDataCache, "type name", size: 0, symIndexId: this._beforeNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType, aComplexType }, returnValueType: voidType)),

                // RVA 400: int ComplexBeforeSimpleAfter(bool), with separated block at RVA 1000
                GenerateComplexFunctionCodeSymbol(this.BeforeDataCache, "ComplexBeforeSimpleAfter", ref this._beforeNextSymIndexId, primaryBlockRVA: 400, primaryBlockSize: 100, separatedBlockRVA: 1000, separatedBlockSize: 100,
                                                  functionType: new FunctionTypeSymbol(this.BeforeDataCache, "type name", size: 0, symIndexId: this._beforeNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType }, returnValueType: intType)),

                // RVA 500: int ComplexBeforeComplexAfter(bool), with separated block at same RVA 1000 (the two separated blocks here and in Complex1 are ICF folded)
                GenerateComplexFunctionCodeSymbol(this.BeforeDataCache, "ComplexBeforeComplexAfter", ref this._beforeNextSymIndexId, primaryBlockRVA: 500, primaryBlockSize: 100, separatedBlockRVA: 1000, separatedBlockSize: 100,
                                                  functionType: new FunctionTypeSymbol(this.BeforeDataCache, "type name", size: 0, symIndexId: this._beforeNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType }, returnValueType: intType)),

                // RVA 600: SimpleBeforeComplexAfter()
                new SimpleFunctionCodeSymbol(this.BeforeDataCache, "SimpleBeforeComplexAfter", rva: 600, size: 200, symIndexId: this._beforeNextSymIndexId++,
                                            functionType: new FunctionTypeSymbol(this.BeforeDataCache, "type name", size: 0, symIndexId: this._beforeNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: Array.Empty<TypeSymbol>(), returnValueType: voidType)),

                // RVA 800: OnlyInBefore
                new SimpleFunctionCodeSymbol(this.BeforeDataCache, "OnlyInBefore", rva: 800, size: 100, symIndexId: this._beforeNextSymIndexId++,
                                            functionType: new FunctionTypeSymbol(this.BeforeDataCache, "type name", size: 0, symIndexId: this._beforeNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: Array.Empty<TypeSymbol>(), returnValueType: voidType)),
            };
    }

    private List<IFunctionCodeSymbol> GenerateAfterFunctionCodeSymbols()
    {
        var boolType = new BasicTypeSymbol(this.AfterDataCache, "bool", size: 1, symIndexId: this._afterNextSymIndexId++);
        var intType = new BasicTypeSymbol(this.AfterDataCache, "int", size: 1, symIndexId: this._afterNextSymIndexId++);
        var voidType = new BasicTypeSymbol(this.AfterDataCache, "void", size: 0, symIndexId: this._afterNextSymIndexId++);
        var aComplexType = new UserDefinedTypeSymbol(this.AfterDataCache, this.AfterDIAAdapter, this.MockAfterSession.Object, "AComplex::Type", instanceSize: 10, symIndexId: this._afterNextSymIndexId++, udtKind: UserDefinedTypeKind.UdtClass);

        return new List<IFunctionCodeSymbol>()
            {
                // RVA 100: MyType::MyFunction(bool, int)
                new SimpleFunctionCodeSymbol(this.AfterDataCache, "MyType::MyFunction", rva: 100, size: 10, symIndexId: this._afterNextSymIndexId++,
                                             functionType: new FunctionTypeSymbol(this.AfterDataCache, "type name", size: 0, symIndexId: this._afterNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType, intType }, returnValueType: voidType)),

                // RVA 300: SomeNamespace::MyType::FoldableFunction<AComplex::Type,bool>(bool, AComplex::Type)
                new SimpleFunctionCodeSymbol(this.AfterDataCache, "SomeNamespace::MyType::FoldableFunction<AComplex::Type,bool>", rva: 300, size: 10, symIndexId: this._afterNextSymIndexId++,
                                            functionType: new FunctionTypeSymbol(this.AfterDataCache, "type name", size: 0, symIndexId: this._afterNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType, aComplexType }, returnValueType: voidType)),

                // RVA 400: int ComplexBeforeSimpleAfter(bool) (as the name suggests) became simple in the 'after' binary to allow us to test complex -> simple diffs
                new SimpleFunctionCodeSymbol(this.AfterDataCache, "ComplexBeforeSimpleAfter", rva: 400, size: 200, symIndexId: this._afterNextSymIndexId++,
                                            functionType: new FunctionTypeSymbol(this.AfterDataCache, "type name", size: 0, symIndexId: this._afterNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType }, returnValueType: intType)),

                // RVA 600: int ComplexBeforeComplexAfter(bool), with separated block at RVA 1000
                GenerateComplexFunctionCodeSymbol(this.AfterDataCache, "ComplexBeforeComplexAfter", ref this._afterNextSymIndexId, primaryBlockRVA: 600, primaryBlockSize: 100, separatedBlockRVA: 1000, separatedBlockSize: 100,
                                                  functionType: new FunctionTypeSymbol(this.AfterDataCache, "type name", size: 0, symIndexId: this._afterNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType }, returnValueType: intType)),

                // RVA 700: SimpleBeforeComplexAfter, with separated block at RVA 1000 (shared with the previous complex function)
                GenerateComplexFunctionCodeSymbol(this.AfterDataCache, "SimpleBeforeComplexAfter", ref this._afterNextSymIndexId, primaryBlockRVA: 700, primaryBlockSize: 100, separatedBlockRVA: 1000, separatedBlockSize: 100,
                                                  functionType: new FunctionTypeSymbol(this.AfterDataCache, "type name", size: 0, symIndexId: this._afterNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: Array.Empty<TypeSymbol>(), returnValueType: voidType)),

                // RVA 800: OnlyInAfter
                new SimpleFunctionCodeSymbol(this.AfterDataCache, "OnlyInAfter", rva: 800, size: 100, symIndexId: this._afterNextSymIndexId++,
                                             functionType: new FunctionTypeSymbol(this.AfterDataCache, "type name", size: 0, symIndexId: this._afterNextSymIndexId++, isConst: false, isVolatile: false, argumentTypes: new TypeSymbol[] { boolType }, returnValueType: intType)),
            };
    }

    public SimpleFunctionCodeSymbol GenerateSimpleFunctionCodeSymbolInBefore(string name, uint rva, uint size, FunctionTypeSymbol functionType)
        => new SimpleFunctionCodeSymbol(this.BeforeDataCache, name, rva, size, this._beforeNextSymIndexId++, functionType: functionType);

    public ComplexFunctionCodeSymbol GenerateComplexFunctionCodeSymbolInBefore(string name,
                                                                               uint primaryBlockRVA, uint primaryBlockSize,
                                                                               uint separatedBlockRVA, uint separatedBlockSize,
                                                                               FunctionTypeSymbol functionType)
        => GenerateComplexFunctionCodeSymbol(this.BeforeDataCache, name, ref this._beforeNextSymIndexId, primaryBlockRVA, primaryBlockSize, separatedBlockRVA, separatedBlockSize, functionType);

    public SimpleFunctionCodeSymbol GenerateSimpleFunctionCodeSymbolInAfter(string name, uint rva, uint size, FunctionTypeSymbol functionType)
        => new SimpleFunctionCodeSymbol(this.AfterDataCache, name, rva, size, this._afterNextSymIndexId++, functionType: functionType);

    public ComplexFunctionCodeSymbol GenerateComplexFunctionCodeSymbolInAfter(string name,
                                                                              uint primaryBlockRVA, uint primaryBlockSize,
                                                                              uint separatedBlockRVA, uint separatedBlockSize,
                                                                              FunctionTypeSymbol functionType)
        => GenerateComplexFunctionCodeSymbol(this.AfterDataCache, name, ref this._afterNextSymIndexId, primaryBlockRVA, primaryBlockSize, separatedBlockRVA, separatedBlockSize, functionType);

    private static ComplexFunctionCodeSymbol GenerateComplexFunctionCodeSymbol(SessionDataCache dataCache, string name,
                                                                               ref uint nextSymIndexId,
                                                                               uint primaryBlockRVA, uint primaryBlockSize,
                                                                               uint separatedBlockRVA, uint separatedBlockSize,
                                                                               FunctionTypeSymbol functionType)
    {
        var functionSymIndexId = nextSymIndexId++;

        var primaryBlock = new PrimaryCodeBlockSymbol(dataCache, primaryBlockRVA, primaryBlockSize, symIndexId: nextSymIndexId++);
        var sepBlock = new SeparatedCodeBlockSymbol(dataCache, separatedBlockRVA, separatedBlockSize, symIndexId: nextSymIndexId++, parentFunctionSymIndexId: functionSymIndexId);

        var complexFn = new ComplexFunctionCodeSymbol(dataCache, name, primaryBlock, new List<SeparatedCodeBlockSymbol>() { sepBlock }, functionType, isPGO: true, isOptimizedForSpeed: true);
        sepBlock.ParentFunction = complexFn;

        return complexFn;
    }

    private static bool IsCOMType(UserDefinedTypeSymbol udt)
    {
        if (udt.Name == "IUnknown")
        {
            return true;
        }

        if (udt.BaseTypes != null)
        {
            foreach (var baseType in udt.BaseTypes)
            {
                if (IsCOMType(baseType._baseTypeSymbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void AssertSizesOnEverything()
    {
        // Sections
        Assert.AreEqual(500, this.TextSectionDiff.SizeDiff);
        Assert.AreEqual(500, this.TextSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(2, this.TextSectionDiff.COFFGroupDiffs.Count);
        Assert.AreEqual(500, this.TextSectionDiff.COFFGroupDiffs.Sum(cd => cd.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(500, this.TextSectionDiff.COFFGroupDiffs.Sum(cd => cd.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.TextSectionDiff.BeforeSection);
        Assert.IsNotNull(this.TextSectionDiff.AfterSection);
        Assert.AreEqual(".text", this.TextSectionDiff.Name);
        foreach (var cg in this.TextSectionDiff.COFFGroupDiffs)
        {
            Assert.IsTrue(ReferenceEquals(cg.SectionDiff, this.TextSectionDiff));
        }

        Assert.AreEqual(-1000, this.DataSectionDiff.SizeDiff);
        Assert.AreEqual(-800, this.DataSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(3, this.DataSectionDiff.COFFGroupDiffs.Count);
        Assert.AreEqual(-1000, this.DataSectionDiff.COFFGroupDiffs.Sum(cd => cd.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-800, this.DataSectionDiff.COFFGroupDiffs.Sum(cd => cd.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.DataSectionDiff.BeforeSection);
        Assert.IsNotNull(this.DataSectionDiff.AfterSection);
        Assert.AreEqual(".data", this.DataSectionDiff.Name);
        foreach (var cg in this.DataSectionDiff.COFFGroupDiffs)
        {
            Assert.IsTrue(ReferenceEquals(cg.SectionDiff, this.DataSectionDiff));
        }

        Assert.AreEqual(0, this.RDataSectionDiff.SizeDiff);
        Assert.AreEqual(0, this.RDataSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(5, this.RDataSectionDiff.COFFGroupDiffs.Count);
        Assert.AreEqual(0, this.RDataSectionDiff.COFFGroupDiffs.Sum(cd => cd.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.RDataSectionDiff.COFFGroupDiffs.Sum(cd => cd.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.RDataSectionDiff.BeforeSection);
        Assert.IsNotNull(this.RDataSectionDiff.AfterSection);
        Assert.AreEqual(".rdata", this.RDataSectionDiff.Name);
        foreach (var cg in this.RDataSectionDiff.COFFGroupDiffs)
        {
            Assert.IsTrue(ReferenceEquals(cg.SectionDiff, this.RDataSectionDiff));
        }

        Assert.AreEqual(0, this.VirtSectionDiff.SizeDiff);
        Assert.AreEqual(-300, this.VirtSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(1, this.VirtSectionDiff.COFFGroupDiffs.Count);
        Assert.AreEqual(0, this.VirtSectionDiff.COFFGroupDiffs.Sum(cd => cd.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-300, this.VirtSectionDiff.COFFGroupDiffs.Sum(cd => cd.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.VirtSectionDiff.BeforeSection);
        Assert.IsNull(this.VirtSectionDiff.AfterSection);
        Assert.AreEqual(".virt", this.VirtSectionDiff.Name);
        foreach (var cg in this.VirtSectionDiff.COFFGroupDiffs)
        {
            Assert.IsTrue(ReferenceEquals(cg.SectionDiff, this.VirtSectionDiff));
        }

        Assert.AreEqual(200, this.RsrcSectionDiff.SizeDiff);
        Assert.AreEqual(200, this.RsrcSectionDiff.VirtualSizeDiff);
        Assert.AreEqual(1, this.RsrcSectionDiff.COFFGroupDiffs.Count);
        Assert.AreEqual(200, this.RsrcSectionDiff.COFFGroupDiffs.Sum(cd => cd.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.RsrcSectionDiff.COFFGroupDiffs.Sum(cd => cd.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNull(this.RsrcSectionDiff.BeforeSection);
        Assert.IsNotNull(this.RsrcSectionDiff.AfterSection);
        Assert.AreEqual(".rsrc", this.RsrcSectionDiff.Name);
        foreach (var cg in this.RsrcSectionDiff.COFFGroupDiffs)
        {
            Assert.IsTrue(ReferenceEquals(cg.SectionDiff, this.RsrcSectionDiff));
        }

        // COFF Groups
        Assert.AreEqual(100, this.TextMnCGDiff.SizeDiff);
        Assert.AreEqual(100, this.TextMnCGDiff.VirtualSizeDiff);
        Assert.IsNotNull(this.TextMnCGDiff.BeforeCOFFGroup);
        Assert.IsNotNull(this.TextMnCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".text$mn", this.TextMnCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.TextMnCGDiff.SectionDiff, this.TextSectionDiff));

        Assert.AreEqual(400, this.TextZzCGDiff.SizeDiff);
        Assert.AreEqual(400, this.TextZzCGDiff.VirtualSizeDiff);
        Assert.IsNotNull(this.TextZzCGDiff.BeforeCOFFGroup);
        Assert.IsNotNull(this.TextZzCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".text$zz", this.TextZzCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.TextZzCGDiff.SectionDiff, this.TextSectionDiff));

        Assert.AreEqual(0, this.DataXxCGDiff.SizeDiff);
        Assert.AreEqual(0, this.DataXxCGDiff.VirtualSizeDiff);
        Assert.IsNotNull(this.DataXxCGDiff.BeforeCOFFGroup);
        Assert.IsNotNull(this.DataXxCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".data$xx", this.DataXxCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.DataXxCGDiff.SectionDiff, this.DataSectionDiff));

        Assert.AreEqual(-1000, this.DataZzCGDiff.SizeDiff);
        Assert.AreEqual(-1000, this.DataZzCGDiff.VirtualSizeDiff);
        Assert.IsNotNull(this.DataZzCGDiff.BeforeCOFFGroup);
        Assert.IsNotNull(this.DataZzCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".data$zz", this.DataZzCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.DataZzCGDiff.SectionDiff, this.DataSectionDiff));

        Assert.AreEqual(0, this.BssCGDiff.SizeDiff);
        Assert.AreEqual(200, this.BssCGDiff.VirtualSizeDiff);
        Assert.IsNotNull(this.BssCGDiff.BeforeCOFFGroup);
        Assert.IsNotNull(this.BssCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".bss", this.BssCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.BssCGDiff.SectionDiff, this.DataSectionDiff));

        Assert.AreEqual(0, this.RDataXxCGDiff.SizeDiff);
        Assert.AreEqual(0, this.RDataXxCGDiff.VirtualSizeDiff);
        Assert.IsNotNull(this.RDataXxCGDiff.BeforeCOFFGroup);
        Assert.IsNotNull(this.RDataXxCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".rdata$xx", this.RDataXxCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.RDataXxCGDiff.SectionDiff, this.RDataSectionDiff));

        Assert.AreEqual(0, this.RDataZzCGDiff.SizeDiff);
        Assert.AreEqual(0, this.RDataZzCGDiff.VirtualSizeDiff);
        Assert.IsNotNull(this.RDataZzCGDiff.BeforeCOFFGroup);
        Assert.IsNotNull(this.RDataZzCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".rdata$zz", this.RDataZzCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.RDataZzCGDiff.SectionDiff, this.RDataSectionDiff));

        Assert.AreEqual(-300, this.RDataBefCGDiff.SizeDiff);
        Assert.AreEqual(-300, this.RDataBefCGDiff.VirtualSizeDiff);
        Assert.IsNotNull(this.RDataBefCGDiff.BeforeCOFFGroup);
        Assert.IsNull(this.RDataBefCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".rdata$bef", this.RDataBefCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.RDataBefCGDiff.SectionDiff, this.RDataSectionDiff));

        Assert.AreEqual(300, this.RDataAftCGDiff.SizeDiff);
        Assert.AreEqual(300, this.RDataAftCGDiff.VirtualSizeDiff);
        Assert.IsNull(this.RDataAftCGDiff.BeforeCOFFGroup);
        Assert.IsNotNull(this.RDataAftCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".rdata$aft", this.RDataAftCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.RDataAftCGDiff.SectionDiff, this.RDataSectionDiff));

        Assert.AreEqual(0, this.RDataFooCGDiff.SizeDiff);
        Assert.AreEqual(0, this.RDataFooCGDiff.VirtualSizeDiff);
        Assert.IsNotNull(this.RDataFooCGDiff.BeforeCOFFGroup);
        Assert.IsNotNull(this.RDataFooCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".rdata$foo", this.RDataFooCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.RDataFooCGDiff.SectionDiff, this.RDataSectionDiff));

        Assert.AreEqual(0, this.VirtCGDiff.SizeDiff);
        Assert.AreEqual(-300, this.VirtCGDiff.VirtualSizeDiff);
        Assert.IsNotNull(this.VirtCGDiff.BeforeCOFFGroup);
        Assert.IsNull(this.VirtCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".virt", this.VirtCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.VirtCGDiff.SectionDiff, this.VirtSectionDiff));

        Assert.AreEqual(200, this.RsrcCGDiff.SizeDiff);
        Assert.AreEqual(200, this.RsrcCGDiff.VirtualSizeDiff);
        Assert.IsNull(this.RsrcCGDiff.BeforeCOFFGroup);
        Assert.IsNotNull(this.RsrcCGDiff.AfterCOFFGroup);
        Assert.AreEqual(".rsrc", this.RsrcCGDiff.Name);
        Assert.IsTrue(ReferenceEquals(this.RsrcCGDiff.SectionDiff, this.RsrcSectionDiff));

        // Libs
        Assert.AreEqual(400, this.ALibDiff.SizeDiff);
        Assert.AreEqual(400, this.ALibDiff.CompilandDiffs.Values.Sum(cd => cd.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(400, this.ALibDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(400, this.ALibDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(400, this.ALibDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(400, this.ALibDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(600, this.ALibDiff.VirtualSizeDiff);
        Assert.AreEqual(600, this.ALibDiff.CompilandDiffs.Values.Sum(cd => cd.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(600, this.ALibDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(600, this.ALibDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(600, this.ALibDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(600, this.ALibDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.ALibDiff.BeforeLib);
        Assert.IsNotNull(this.ALibDiff.AfterLib);
        Assert.AreEqual("a.lib", this.ALibDiff.Name);
        Assert.AreEqual("a", this.ALibDiff.ShortName);
        Assert.AreEqual(3, this.ALibDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(3, this.ALibDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(8, this.ALibDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(8, this.ALibDiff.COFFGroupContributionDiffsByName.Count);

        Assert.AreEqual(-900, this.BLibDiff.SizeDiff);
        Assert.AreEqual(-900, this.BLibDiff.CompilandDiffs.Values.Sum(cd => cd.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.BLibDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.BLibDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.BLibDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.BLibDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.BLibDiff.VirtualSizeDiff);
        Assert.AreEqual(-900, this.BLibDiff.CompilandDiffs.Values.Sum(cd => cd.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.BLibDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.BLibDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.BLibDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.BLibDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.BLibDiff.BeforeLib);
        Assert.IsNotNull(this.BLibDiff.AfterLib);
        Assert.AreEqual("b.lib", this.BLibDiff.Name);
        Assert.AreEqual("b", this.BLibDiff.ShortName);
        Assert.AreEqual(3, this.BLibDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(3, this.BLibDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(6, this.BLibDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(6, this.BLibDiff.COFFGroupContributionDiffsByName.Count);

        Assert.AreEqual(0, this.CLibDiff.SizeDiff);
        Assert.AreEqual(0, this.CLibDiff.CompilandDiffs.Values.Sum(cd => cd.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.CLibDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.CLibDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.CLibDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.CLibDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-300, this.CLibDiff.VirtualSizeDiff);
        Assert.AreEqual(-300, this.CLibDiff.CompilandDiffs.Values.Sum(cd => cd.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-300, this.CLibDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-300, this.CLibDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-300, this.CLibDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-300, this.CLibDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No
        Assert.IsNotNull(this.CLibDiff.BeforeLib);
        Assert.IsNull(this.CLibDiff.AfterLib);
        Assert.AreEqual("c.lib", this.CLibDiff.Name);
        Assert.AreEqual("c", this.CLibDiff.ShortName);
        Assert.AreEqual(1, this.CLibDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(1, this.CLibDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(1, this.CLibDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(1, this.CLibDiff.COFFGroupContributionDiffsByName.Count);

        Assert.AreEqual(200, this.DLibDiff.SizeDiff);
        Assert.AreEqual(200, this.DLibDiff.CompilandDiffs.Values.Sum(cd => cd.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.DLibDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.DLibDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.DLibDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.DLibDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.DLibDiff.VirtualSizeDiff);
        Assert.AreEqual(200, this.DLibDiff.CompilandDiffs.Values.Sum(cd => cd.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.DLibDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.DLibDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.DLibDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.DLibDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNull(this.DLibDiff.BeforeLib);
        Assert.IsNotNull(this.DLibDiff.AfterLib);
        Assert.AreEqual("d.lib", this.DLibDiff.Name);
        Assert.AreEqual("d", this.DLibDiff.ShortName);
        Assert.AreEqual(1, this.DLibDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(1, this.DLibDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(1, this.DLibDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(1, this.DLibDiff.COFFGroupContributionDiffsByName.Count);

        // Compilands
        Assert.AreEqual(400, this.A1CompilandDiff.SizeDiff);
        Assert.AreEqual(400, this.A1CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(400, this.A1CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(400, this.A1CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(400, this.A1CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CompilandDiff.VirtualSizeDiff);
        Assert.AreEqual(500, this.A1CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.A1CompilandDiff.BeforeCompiland);
        Assert.IsNotNull(this.A1CompilandDiff.AfterCompiland);
        Assert.IsTrue(ReferenceEquals(this.ALibDiff, this.A1CompilandDiff.LibDiff));
        Assert.AreEqual(@"c:\a\a1.obj", this.A1CompilandDiff.Name);
        Assert.AreEqual("a1.obj", this.A1CompilandDiff.ShortName);
        Assert.AreEqual(2, this.A1CompilandDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(2, this.A1CompilandDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(5, this.A1CompilandDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(5, this.A1CompilandDiff.COFFGroupContributionDiffsByName.Count);

        Assert.AreEqual(0, this.A2CompilandDiff.SizeDiff);
        Assert.AreEqual(0, this.A2CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.A2CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.A2CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.A2CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.A2CompilandDiff.VirtualSizeDiff);
        Assert.AreEqual(0, this.A2CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.A2CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.A2CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.A2CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.A2CompilandDiff.BeforeCompiland);
        Assert.IsNotNull(this.A2CompilandDiff.AfterCompiland);
        Assert.IsTrue(ReferenceEquals(this.ALibDiff, this.A2CompilandDiff.LibDiff));
        Assert.AreEqual(@"a2.obj", this.A2CompilandDiff.Name);
        Assert.AreEqual("a2.obj", this.A2CompilandDiff.ShortName);
        Assert.AreEqual(2, this.A2CompilandDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(2, this.A2CompilandDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(3, this.A2CompilandDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(3, this.A2CompilandDiff.COFFGroupContributionDiffsByName.Count);

        Assert.AreEqual(-800, this.A3CompilandDiff.SizeDiff);
        Assert.AreEqual(-800, this.A3CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-800, this.A3CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-800, this.A3CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-800, this.A3CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-1100, this.A3CompilandDiff.VirtualSizeDiff);
        Assert.AreEqual(-1100, this.A3CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-1100, this.A3CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-1100, this.A3CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-1100, this.A3CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.A3CompilandDiff.BeforeCompiland);
        Assert.IsNull(this.A3CompilandDiff.AfterCompiland);
        Assert.IsTrue(ReferenceEquals(this.ALibDiff, this.A3CompilandDiff.LibDiff));
        Assert.AreEqual(@"a3.obj", this.A3CompilandDiff.Name);
        Assert.AreEqual("a3.obj", this.A3CompilandDiff.ShortName);
        Assert.AreEqual(2, this.A3CompilandDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(2, this.A3CompilandDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(3, this.A3CompilandDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(3, this.A3CompilandDiff.COFFGroupContributionDiffsByName.Count);

        Assert.AreEqual(800, this.A4CompilandDiff.SizeDiff);
        Assert.AreEqual(800, this.A4CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(800, this.A4CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(800, this.A4CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(800, this.A4CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(1200, this.A4CompilandDiff.VirtualSizeDiff);
        Assert.AreEqual(1200, this.A4CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(1200, this.A4CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(1200, this.A4CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(1200, this.A4CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNull(this.A4CompilandDiff.BeforeCompiland);
        Assert.IsNotNull(this.A4CompilandDiff.AfterCompiland);
        Assert.IsTrue(ReferenceEquals(this.ALibDiff, this.A4CompilandDiff.LibDiff));
        Assert.AreEqual(@"a4.obj", this.A4CompilandDiff.Name);
        Assert.AreEqual("a4.obj", this.A4CompilandDiff.ShortName);
        Assert.AreEqual(2, this.A4CompilandDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(2, this.A4CompilandDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(3, this.A4CompilandDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(3, this.A4CompilandDiff.COFFGroupContributionDiffsByName.Count);

        Assert.AreEqual(-900, this.B1CompilandDiff.SizeDiff);
        Assert.AreEqual(-900, this.B1CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.B1CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.B1CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.B1CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.B1CompilandDiff.VirtualSizeDiff);
        Assert.AreEqual(-900, this.B1CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.B1CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.B1CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-900, this.B1CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.B1CompilandDiff.BeforeCompiland);
        Assert.IsNotNull(this.B1CompilandDiff.AfterCompiland);
        Assert.IsTrue(ReferenceEquals(this.BLibDiff, this.B1CompilandDiff.LibDiff));
        Assert.AreEqual(@"b1.obj", this.B1CompilandDiff.Name);
        Assert.AreEqual("b1.obj", this.B1CompilandDiff.ShortName);
        Assert.AreEqual(2, this.B1CompilandDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(2, this.B1CompilandDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(3, this.B1CompilandDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(3, this.B1CompilandDiff.COFFGroupContributionDiffsByName.Count);

        Assert.AreEqual(0, this.B2CompilandDiff.SizeDiff);
        Assert.AreEqual(0, this.B2CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.B2CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.B2CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.B2CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.B2CompilandDiff.VirtualSizeDiff);
        Assert.AreEqual(0, this.B2CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.B2CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.B2CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.B2CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.B2CompilandDiff.BeforeCompiland);
        Assert.IsNotNull(this.B2CompilandDiff.AfterCompiland);
        Assert.IsTrue(ReferenceEquals(this.BLibDiff, this.B2CompilandDiff.LibDiff));
        Assert.AreEqual(@"b2.obj", this.B2CompilandDiff.Name);
        Assert.AreEqual("b2.obj", this.B2CompilandDiff.ShortName);
        Assert.AreEqual(2, this.B2CompilandDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(2, this.B2CompilandDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(3, this.B2CompilandDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(3, this.B2CompilandDiff.COFFGroupContributionDiffsByName.Count);

        Assert.AreEqual(0, this.C1CompilandDiff.SizeDiff);
        Assert.AreEqual(0, this.C1CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.C1CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.C1CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(0, this.C1CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-300, this.C1CompilandDiff.VirtualSizeDiff);
        Assert.AreEqual(-300, this.C1CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-300, this.C1CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-300, this.C1CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(-300, this.C1CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNotNull(this.C1CompilandDiff.BeforeCompiland);
        Assert.IsNull(this.C1CompilandDiff.AfterCompiland);
        Assert.IsTrue(ReferenceEquals(this.CLibDiff, this.C1CompilandDiff.LibDiff));
        Assert.AreEqual(@"c1.obj", this.C1CompilandDiff.Name);
        Assert.AreEqual("c1.obj", this.C1CompilandDiff.ShortName);
        Assert.AreEqual(1, this.C1CompilandDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(1, this.C1CompilandDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(1, this.C1CompilandDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(1, this.C1CompilandDiff.COFFGroupContributionDiffsByName.Count);

        Assert.AreEqual(200, this.D1CompilandDiff.SizeDiff);
        Assert.AreEqual(200, this.D1CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.D1CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.D1CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.D1CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.SizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.D1CompilandDiff.VirtualSizeDiff);
        Assert.AreEqual(200, this.D1CompilandDiff.SectionContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.D1CompilandDiff.SectionContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.D1CompilandDiff.COFFGroupContributionDiffs.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.AreEqual(200, this.D1CompilandDiff.COFFGroupContributionDiffsByName.Values.Sum(sc => sc.VirtualSizeDiff)); // No Byte Left Behind!
        Assert.IsNull(this.D1CompilandDiff.BeforeCompiland);
        Assert.IsNotNull(this.D1CompilandDiff.AfterCompiland);
        Assert.IsTrue(ReferenceEquals(this.DLibDiff, this.D1CompilandDiff.LibDiff));
        Assert.AreEqual(@"d1.obj", this.D1CompilandDiff.Name);
        Assert.AreEqual("d1.obj", this.D1CompilandDiff.ShortName);
        Assert.AreEqual(1, this.D1CompilandDiff.SectionContributionDiffs.Count);
        Assert.AreEqual(1, this.D1CompilandDiff.SectionContributionDiffsByName.Count);
        Assert.AreEqual(1, this.D1CompilandDiff.COFFGroupContributionDiffs.Count);
        Assert.AreEqual(1, this.D1CompilandDiff.COFFGroupContributionDiffsByName.Count);
    }

    public void Dispose()
    {
        this.BeforeDataCache.Dispose();
        this.AfterDataCache.Dispose();
        this.DiffDataCache.Dispose();
    }
}
