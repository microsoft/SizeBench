using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine;
using SizeBench.AnalysisEngine.SessionTasks;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.TestDataCommon;

// The relationship between BinarySection, COFFGroup, Lib, Compiland, SourceFile, and all their contributions
// is pretty complex, so let's just make one set of these for all the tests to share to avoid massive
// repetition between test classes.
//
// The full layout is listed below.
// Note that for testing purposes we try to cover every combination, so:
//   .text has COFF Groups that fill it up perfectly (no tail slop padding in size or virtual size)
//   .data contains .bss which is a COFF Group of VirtualSize - and it isn't full so it has tail slop for size and VirtualSize
//   .rdata has no VirtualSize COFF Groups, but it does have tail slop
//
// Note that SectionAlignment is set to 5000 because 0x1000 (4096) is the minimum amount to allow VirtualSize in the linker, and we want
// to test that.  In real life SectionAlignment can't be this number, since it needs to be in mutliples of pages sizes, but the tests are
// a lot easier to author and read when the numbers are in decimal (instaed of hex).
//
// ---------------------------------------------------
// |                       RVAs / Size / VirtualSize |
// |-------------------------------------------------|
// | .text             0 - 4999 / 5000 / 5000        |
// |   .text$mn        0 - 2999 / 3000 / 3000        |
// |   .text$zz     3000 - 4999 / 2000 / 2000        |
// | .data          5000 - 9999 / 1000 / 5000        |
// |   .data$xx     5000 - 5499 /  500 /  500        |
// |   .data$zz     5500 - 5949 /  450 /  450        |
// |   .bss         5950 - 6449 /    0 /  500        |
// |   <tail slop>  6449 - 9999 /   50 / 3550        |
// | .rdata        10000 -11799 / 1800 / 5000        |
// |   .rdata$xx   10000 -10499 /  500 /  500        |
// |   .rdata$zz   10500 -11699 / 1200 / 1200        |
// |   <tail slop> 11700 -11799 /  100 / 3300        |
// ---------------------------------------------------
//
// And within those, the compilands/libs/source files and how much they contribute to each is here.
//
// Again, we try to cover every combination, so:
//     a1.obj contributes to: multiple sections, but not all of them (no .rdata)
//                            multiple COFF Groups in the same section (.text)
//                            only-some of the COFF Groups in the same section (.data), none of which are VirtualSize
//                            none of the COFF Groups in some section (.rdata)
//     a2.obj contributes to: only one section (.data)
//                            only-some of the COFF Groups in the same section (.data), where all of them are VirtualSize
//     a3.obj contributes to: only one section (.data), so a.lib below can have multiple compilands contributing to the same COFF Group
//                            all of the COFF Groups in the same section (.data), with a mix of Size and VirtualSize
//     b1.obj contributes to: only one section (.data), contributing both Size and VirtualSize
//
//     a.lib contributes to: a section with only one compiland (.text with a1.obj)
//                           a section with multiple compilands (.data with a1.obj, a2.obj, and a3.obj)
//                           a section with none of its compilands (.rdata)
//                           a COFF Group that contains Size with multiple compilands (.data$xx with a1.obj and a3.obj)
//                           a COFF Group that contains VirtualSize with multiple compilands (.bss with a2.obj and a3.obj)
//                           a COFF Group with only one compiland (.text$zz with a1.obj)
//     b.lib contributes some stuff, just to verify that after filtering to a.lib, it gets filtered out
//
// --------------------------------------------------------------------------------------------------------------------------
// | Compiland  | Total | .text | .text$mn | .text$zz | .data | .data$xx | .data$zz | .bss | .rdata | .rdata$xx | .rdata$zz |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|
// | a1.obj     |  5025 |  5000 |     3000 |     2000 |    25 |        0 |       25 |    0 |      0 |         0 |         0 |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|
// | a2.obj     |   400 |     0 |        0 |        0 |   400 |        0 |        0 |  400 |      0 |         0 |         0 |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|
// | a3.obj     |   950 |     0 |        0 |        0 |   950 |      500 |      400 |   50 |      0 |         0 |         0 |
// |------------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|
// | b1.obj     |    75 |     0 |        0 |        0 |    75 |        0 |       25 |   50 |      0 |         0 |         0 |
// --------------------------------------------------------------------------------------------------------------------------
//
// Those Compilands (OBJs) are broken down by source file like so:
//
// Trying to cover every combination, so:
//     a1.cpp contributes to a1.obj in just .text, across multiple COFF groups
//     x.h contributes to a1.obj, a3.obj, and b1.obj in multiple sections and COFF groups, so that filtering by section may 
//                        select 1 or 2 compilands to test filtering
//
// -------------------------------------------------------------------------------------------
// | Source File         | Total | .text | .text$mn | .text$zz | .data | .data$xx | .data$zz |
// |---------------------|-------|-------|----------|----------|-------|----------|----------|
// | a1.cpp (via a1.obj) |   500 |   500 |      300 |      200 |     0 |        0 |        0 |
// |---------------------|-------|-------|----------|----------|-------|----------|----------|
// | x.h (total)         |   825 |   500 |      500 |        0 |   325 |      300 |       25 |
// | x.h (via a1.obj)    |   500 |   500 |      500 |        0 |     0 |        0 |        0 |
// | x.h (via a3.obj)    |   300 |     0 |        0 |        0 |   300 |      300 |        0 |
// | x.h (via b1.obj)    |    25 |     0 |        0 |        0 |    25 |        0 |       25 |
// -------------------------------------------------------------------------------------------
//
// And then the Libs:
// ---------------------------------------------------------------------------------------------------------------------
// | Lib   | Total | .text | .text$mn | .text$zz | .data | .data$xx | .data$zz | .bss | .rdata | .rdata$xx | .rdata$zz |
// |-------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|
// | a.lib |  6375 |  5000 |     3000 |     2000 |  1375 |      500 |      425 |  450 |      0 |         0 |         0 |
// |-------|-------|-------|----------|----------|-------|----------|----------|------|--------|-----------|-----------|
// | b.lib |    75 |     0 |        0 |        0 |    75 |        0 |       25 |   50 |      0 |         0 |         0 |
// ---------------------------------------------------------------------------------------------------------------------
//
public sealed class SingleBinaryDataGenerator : IDisposable
{
    #region Fields

    internal Mock<ISession> MockSession = new Mock<ISession>();
    internal SessionDataCache DataCache = new SessionDataCache()
    {
        AllCanonicalNames = new SortedList<uint, NameCanonicalization>()
    };
    internal uint _nextSymIndexId;

    //  objects
    internal SessionTaskParameters SessionTaskParameters;
    internal TestDIAAdapter DIAAdapter = new TestDIAAdapter();
    internal BinarySection TextSection;
    internal BinarySection DataSection;
    internal BinarySection RDataSection;
    internal List<BinarySection> Sections;
    internal COFFGroup TextMnCG;
    internal COFFGroup TextZzCG;
    internal COFFGroup DataXxCG;
    internal COFFGroup DataZzCG;
    internal COFFGroup BssCG;
    internal COFFGroup RDataXxCG;
    internal COFFGroup RDataZzCG;
    internal List<COFFGroup> COFFGroups;
    internal Compiland A1Compiland;
    internal Compiland A2Compiland;
    internal Compiland A3Compiland;
    internal Compiland B1Compiland;
    internal List<Compiland> Compilands;
    internal Library ALib;
    internal Library BLib;
    internal List<Library> Libs;
    internal SourceFile A1CppSourceFile;
    internal SourceFile XHSourceFile;
    internal List<SourceFile> SourceFiles;

    #endregion

    public SingleBinaryDataGenerator()
    {
        this.TextSection = new BinarySection(this.DataCache, ".text", size: 5000, virtualSize: 5000, rva: 0,
                                             fileAlignment: 200, sectionAlignment: 5000, characteristics: SectionCharacteristics.MemExecute);
        this.DataSection = new BinarySection(this.DataCache, ".data", size: 1000, virtualSize: 1450, rva: 5000,
                                             fileAlignment: 200, sectionAlignment: 5000, characteristics: SectionCharacteristics.MemWrite | SectionCharacteristics.MemRead);
        this.RDataSection = new BinarySection(this.DataCache, ".rdata", size: 1800, virtualSize: 1700, rva: 10000,
                                              fileAlignment: 200, sectionAlignment: 5000, characteristics: SectionCharacteristics.MemRead);

        this.TextMnCG = new COFFGroup(this.DataCache, ".text$mn", size: 3000, rva: 0,
                                      fileAlignment: 200, sectionAlignment: 5000,
                                      characteristics: SectionCharacteristics.MemExecute)
        {
            Section = this.TextSection
        };
        this.TextZzCG = new COFFGroup(this.DataCache, ".text$zz", size: 2000, rva: 3000,
                                      fileAlignment: 200, sectionAlignment: 5000,
                                      characteristics: SectionCharacteristics.MemExecute)
        {
            Section = this.TextSection
        };


        this.DataXxCG = new COFFGroup(this.DataCache, ".data$xx", size: 500, rva: 5000,
                                      fileAlignment: 200, sectionAlignment: 5000,
                                      characteristics: SectionCharacteristics.ContainsInitializedData |
                                                       SectionCharacteristics.MemRead |
                                                       SectionCharacteristics.MemWrite)
        {
            Section = this.DataSection
        };
        this.DataZzCG = new COFFGroup(this.DataCache, ".data$zz", size: 450, rva: 5500,
                                      fileAlignment: 200, sectionAlignment: 5000,
                                      characteristics: SectionCharacteristics.ContainsInitializedData |
                                                       SectionCharacteristics.MemRead |
                                                       SectionCharacteristics.MemWrite)
        {
            Section = this.DataSection
        };

        this.BssCG = new COFFGroup(this.DataCache, ".bss", size: 500, rva: 5950,
                                   fileAlignment: 200, sectionAlignment: 5000,
                                   characteristics: SectionCharacteristics.ContainsUninitializedData)
        {
            Section = this.DataSection
        };

        this.RDataXxCG = new COFFGroup(this.DataCache, ".rdata$xx", size: 500, rva: 10000,
                                       fileAlignment: 200, sectionAlignment: 5000,
                                       characteristics: SectionCharacteristics.MemRead)
        {
            Section = this.RDataSection
        };
        this.RDataZzCG = new COFFGroup(this.DataCache, ".rdata$zz", size: 1200, rva: 10500,
                                       fileAlignment: 200, sectionAlignment: 5000,
                                       characteristics: SectionCharacteristics.MemRead)
        {
            Section = this.RDataSection
        };

        this.TextMnCG.MarkFullyConstructed();
        this.TextZzCG.MarkFullyConstructed();
        this.TextSection.AddCOFFGroup(this.TextMnCG);
        this.TextSection.AddCOFFGroup(this.TextZzCG);
        this.TextSection.MarkFullyConstructed();

        this.DataXxCG.MarkFullyConstructed();
        this.DataZzCG.MarkFullyConstructed();
        this.BssCG.MarkFullyConstructed();
        this.DataSection.AddCOFFGroup(this.DataXxCG);
        this.DataSection.AddCOFFGroup(this.DataZzCG);
        this.DataSection.AddCOFFGroup(this.BssCG);
        this.DataSection.MarkFullyConstructed();

        this.RDataXxCG.MarkFullyConstructed();
        this.RDataZzCG.MarkFullyConstructed();
        this.RDataSection.AddCOFFGroup(this.RDataXxCG);
        this.RDataSection.AddCOFFGroup(this.RDataZzCG);
        this.RDataSection.MarkFullyConstructed();

        this.Sections = new List<BinarySection>()
            {
                this.TextSection,
                this.DataSection,
                this.RDataSection,
            };

        this.COFFGroups = new List<COFFGroup>()
            {
                this.TextMnCG,
                this.TextZzCG,
                this.DataXxCG,
                this.DataZzCG,
                this.BssCG,
                this.RDataXxCG,
                this.RDataZzCG,
            };

        this.ALib = new Library("a.lib");

        this._nextSymIndexId = 1234;
        this.A1Compiland = this.ALib.GetOrCreateCompiland(this.DataCache, @"c:\a\a1.obj", this._nextSymIndexId++ /* compilandSymIndex */, this.DIAAdapter);

        Contribution textMnContribution = this.A1Compiland.GetOrCreateCOFFGroupContribution(this.TextMnCG);
        textMnContribution.AddRVARange(RVARange.FromRVAAndSize(this.TextMnCG.RVA, 3000));
        textMnContribution.MarkFullyConstructed();
        Contribution textZzContribution = this.A1Compiland.GetOrCreateCOFFGroupContribution(this.TextZzCG);
        textZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.TextZzCG.RVA, 2000));
        textZzContribution.MarkFullyConstructed();
        Contribution textContribution = this.A1Compiland.GetOrCreateSectionContribution(this.TextSection);
        textContribution.AddRVARanges(textMnContribution.RVARanges);
        textContribution.AddRVARanges(textZzContribution.RVARanges);
        textContribution.MarkFullyConstructed();

        Contribution dataZzContribution = this.A1Compiland.GetOrCreateCOFFGroupContribution(this.DataZzCG);
        dataZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.DataZzCG.RVA, 25));
        dataZzContribution.MarkFullyConstructed();
        Contribution dataContribution = this.A1Compiland.GetOrCreateSectionContribution(this.DataSection);
        dataContribution.AddRVARanges(dataZzContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        this.A1Compiland.MarkFullyConstructed();



        this.A2Compiland = this.ALib.GetOrCreateCompiland(this.DataCache, "a2.obj", this._nextSymIndexId++ /* compilandSymIndex */, this.DIAAdapter);
        var bssContribution = this.A2Compiland.GetOrCreateCOFFGroupContribution(this.BssCG);
        bssContribution.AddRVARange(RVARange.FromRVAAndSize(this.BssCG.RVA, 400, isVirtualSize: true));
        bssContribution.MarkFullyConstructed();
        dataContribution = this.A2Compiland.GetOrCreateSectionContribution(this.DataSection);
        dataContribution.AddRVARanges(bssContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        this.A2Compiland.MarkFullyConstructed();


        this.A3Compiland = this.ALib.GetOrCreateCompiland(this.DataCache, "a3.obj", this._nextSymIndexId++ /* compilandSymIndex */, this.DIAAdapter);

        Contribution dataXxContribution = this.A3Compiland.GetOrCreateCOFFGroupContribution(this.DataXxCG);
        dataXxContribution.AddRVARange(RVARange.FromRVAAndSize(this.DataXxCG.RVA, 500));
        dataXxContribution.MarkFullyConstructed();
        dataZzContribution = this.A3Compiland.GetOrCreateCOFFGroupContribution(this.DataZzCG);
        dataZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.DataZzCG.RVA + this.A1Compiland.COFFGroupContributions[this.DataZzCG].Size, 400));
        dataZzContribution.MarkFullyConstructed();
        bssContribution = this.A3Compiland.GetOrCreateCOFFGroupContribution(this.BssCG);
        bssContribution.AddRVARange(RVARange.FromRVAAndSize(this.BssCG.RVA + this.A2Compiland.COFFGroupContributions[this.BssCG].VirtualSize, 50, isVirtualSize: true));
        bssContribution.MarkFullyConstructed();
        dataContribution = this.A3Compiland.GetOrCreateSectionContribution(this.DataSection);
        dataContribution.AddRVARanges(dataXxContribution.RVARanges);
        dataContribution.AddRVARanges(dataZzContribution.RVARanges);
        dataContribution.AddRVARanges(bssContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        this.A3Compiland.MarkFullyConstructed();



        this.ALib.MarkFullyConstructed();


        this.BLib = new Library("b.lib");
        this.B1Compiland = this.BLib.GetOrCreateCompiland(this.DataCache, "b1.obj", this._nextSymIndexId++ /* compilandSymIndex */, this.DIAAdapter);

        dataZzContribution = this.B1Compiland.GetOrCreateCOFFGroupContribution(this.DataZzCG);
        dataZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.DataZzCG.RVA
                                                               + this.A1Compiland.COFFGroupContributions[this.DataZzCG].Size
                                                               + this.A3Compiland.COFFGroupContributions[this.DataZzCG].Size, 25));
        dataZzContribution.MarkFullyConstructed();
        bssContribution = this.B1Compiland.GetOrCreateCOFFGroupContribution(this.BssCG);
        bssContribution.AddRVARange(RVARange.FromRVAAndSize(this.BssCG.RVA
                                                            + this.A2Compiland.COFFGroupContributions[this.BssCG].VirtualSize
                                                            + this.A3Compiland.COFFGroupContributions[this.BssCG].VirtualSize, 50, isVirtualSize: true));
        bssContribution.MarkFullyConstructed();
        dataContribution = this.B1Compiland.GetOrCreateSectionContribution(this.DataSection);
        dataContribution.AddRVARanges(dataZzContribution.RVARanges);
        dataContribution.AddRVARanges(bssContribution.RVARanges);
        dataContribution.MarkFullyConstructed();

        this.B1Compiland.MarkFullyConstructed();



        this.BLib.MarkFullyConstructed();



        this.Libs = new List<Library>()
            {
                this.ALib,
                this.BLib,
            };

        this.Compilands = new List<Compiland>()
            {
                this.A1Compiland,
                this.A2Compiland,
                this.A3Compiland,
                this.B1Compiland,
            };

        this.A1CppSourceFile = new SourceFile(this.DataCache, "a1.cpp", fileId: 0, new List<Compiland>() { this.A1Compiland });
        textMnContribution = this.A1CppSourceFile.GetOrCreateCOFFGroupContribution(this.TextMnCG);
        textMnContribution.AddRVARange(RVARange.FromRVAAndSize(this.TextMnCG.RVA, 300));
        textMnContribution.MarkFullyConstructed();
        textZzContribution = this.A1CppSourceFile.GetOrCreateCOFFGroupContribution(this.TextZzCG);
        textZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.TextZzCG.RVA, 200));
        textZzContribution.MarkFullyConstructed();
        textContribution = this.A1CppSourceFile.GetOrCreateSectionContribution(this.TextSection);
        textContribution.AddRVARanges(textMnContribution.RVARanges);
        textContribution.AddRVARanges(textZzContribution.RVARanges);
        textContribution.MarkFullyConstructed();
        var a1CompilandContribution = this.A1CppSourceFile.GetOrCreateCompilandContribution(this.A1Compiland);
        a1CompilandContribution.AddRVARanges(textContribution.RVARanges);
        a1CompilandContribution.MarkFullyConstructed();
        this.A1CppSourceFile.MarkFullyConstructed();

        this.XHSourceFile = new SourceFile(this.DataCache, "x.h", fileId: 1, new List<Compiland>() { this.A1Compiland, this.A3Compiland, this.B1Compiland });
        textMnContribution = this.XHSourceFile.GetOrCreateCOFFGroupContribution(this.TextMnCG);
        textMnContribution.AddRVARange(RVARange.FromRVAAndSize(this.TextMnCG.RVA + 300, 500));
        textMnContribution.MarkFullyConstructed();
        textContribution = this.XHSourceFile.GetOrCreateSectionContribution(this.TextSection);
        textContribution.AddRVARanges(textMnContribution.RVARanges);
        textContribution.MarkFullyConstructed();
        a1CompilandContribution = this.XHSourceFile.GetOrCreateCompilandContribution(this.A1Compiland);
        a1CompilandContribution.AddRVARanges(textContribution.RVARanges);
        a1CompilandContribution.MarkFullyConstructed();

        dataXxContribution = this.XHSourceFile.GetOrCreateCOFFGroupContribution(this.DataXxCG);
        dataXxContribution.AddRVARange(RVARange.FromRVAAndSize(this.A3Compiland.COFFGroupContributions[this.DataXxCG].RVARanges[0].RVAStart, 300));
        dataXxContribution.MarkFullyConstructed();
        dataContribution = this.XHSourceFile.GetOrCreateSectionContribution(this.DataSection);
        dataContribution.AddRVARanges(dataXxContribution.RVARanges);
        var a3CompilandContribution = this.XHSourceFile.GetOrCreateCompilandContribution(this.A3Compiland);
        a3CompilandContribution.AddRVARanges(dataXxContribution.RVARanges);
        a3CompilandContribution.MarkFullyConstructed();

        dataZzContribution = this.XHSourceFile.GetOrCreateCOFFGroupContribution(this.DataZzCG);
        dataZzContribution.AddRVARange(RVARange.FromRVAAndSize(this.B1Compiland.COFFGroupContributions[this.DataZzCG].RVARanges[0].RVAStart, 25));
        dataZzContribution.MarkFullyConstructed();
        dataContribution = this.XHSourceFile.GetOrCreateSectionContribution(this.DataSection);
        dataContribution.AddRVARanges(dataZzContribution.RVARanges);
        dataContribution.MarkFullyConstructed();
        var b1CompilandContribution = this.XHSourceFile.GetOrCreateCompilandContribution(this.B1Compiland);
        b1CompilandContribution.AddRVARanges(dataZzContribution.RVARanges);
        b1CompilandContribution.MarkFullyConstructed();

        this.XHSourceFile.MarkFullyConstructed();

        this.SourceFiles = new List<SourceFile>()
            {
                this.A1CppSourceFile,
                this.XHSourceFile
            };

        this.SessionTaskParameters = new SessionTaskParameters(
            this.MockSession.Object,
            this.DIAAdapter,
            this.DataCache);

        AssertSizesOnEverything();
    }

    public void AssertSizesOnEverything()
    {
        // Sections
        Assert.AreEqual(5000u, this.TextSection.Size);
        Assert.AreEqual(5000u, this.TextSection.VirtualSize);
        Assert.AreEqual(0u, this.TextSection.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(2, this.TextSection.COFFGroups.Count);
        Assert.AreEqual(5000, this.TextSection.COFFGroups.Sum(cg => cg.Size)); // No Byte Left Behind!
        Assert.AreEqual(0, this.TextSection.COFFGroups.Sum(cg => cg.TailSlopSizeAlignment));
        Assert.AreEqual(5000, this.TextSection.COFFGroups.Sum(cg => cg.VirtualSize));
        Assert.AreEqual(0, this.TextSection.COFFGroups.Sum(cg => cg.TailSlopVirtualSizeAlignment));
        Assert.AreEqual(".text", this.TextSection.Name);
        foreach (var cg in this.TextSection.COFFGroups)
        {
            Assert.IsTrue(ReferenceEquals(cg.Section, this.TextSection));
        }

        Assert.AreEqual(1000u, this.DataSection.Size);
        Assert.AreEqual(1450u, this.DataSection.VirtualSize);
        Assert.AreEqual(3550u, this.DataSection.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(3, this.DataSection.COFFGroups.Count);
        Assert.AreEqual(1000 - 50, this.DataSection.COFFGroups.Sum(cg => cg.Size)); // No Byte Left Behind!
        Assert.AreEqual(50, this.DataSection.COFFGroups.Sum(cg => cg.TailSlopSizeAlignment));
        Assert.AreEqual(5000 - 3550, this.DataSection.COFFGroups.Sum(cg => cg.VirtualSize));
        Assert.AreEqual(3550, this.DataSection.COFFGroups.Sum(cg => cg.TailSlopVirtualSizeAlignment));
        Assert.AreEqual(".data", this.DataSection.Name);
        foreach (var cg in this.DataSection.COFFGroups)
        {
            Assert.IsTrue(ReferenceEquals(cg.Section, this.DataSection));
        }

        Assert.AreEqual(1800u, this.RDataSection.Size);
        Assert.AreEqual(1700u, this.RDataSection.VirtualSize);
        Assert.AreEqual(3300u, this.RDataSection.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(2, this.RDataSection.COFFGroups.Count);
        Assert.AreEqual(1800 - 100, this.RDataSection.COFFGroups.Sum(cg => cg.Size)); // No Byte Left Behind!
        Assert.AreEqual(100, this.RDataSection.COFFGroups.Sum(cg => cg.TailSlopSizeAlignment));
        Assert.AreEqual(5000 - 3300, this.RDataSection.COFFGroups.Sum(cg => cg.VirtualSize));
        Assert.AreEqual(3300, this.RDataSection.COFFGroups.Sum(cg => cg.TailSlopVirtualSizeAlignment));
        Assert.AreEqual(".rdata", this.RDataSection.Name);
        foreach (var cg in this.RDataSection.COFFGroups)
        {
            Assert.IsTrue(ReferenceEquals(cg.Section, this.RDataSection));
        }


        // COFF Groups
        Assert.AreEqual(3000u, this.TextMnCG.Size);
        Assert.AreEqual(3000u, this.TextMnCG.VirtualSize);
        Assert.AreEqual(0u, this.TextMnCG.TailSlopSizeAlignment);
        Assert.AreEqual(0u, this.TextMnCG.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(".text$mn", this.TextMnCG.Name);
        Assert.IsTrue(ReferenceEquals(this.TextMnCG.Section, this.TextSection));

        Assert.AreEqual(2000u, this.TextZzCG.Size);
        Assert.AreEqual(2000u, this.TextZzCG.VirtualSize);
        Assert.AreEqual(0u, this.TextZzCG.TailSlopSizeAlignment);
        Assert.AreEqual(0u, this.TextZzCG.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(".text$zz", this.TextZzCG.Name);
        Assert.IsTrue(ReferenceEquals(this.TextZzCG.Section, this.TextSection));

        Assert.AreEqual(500u, this.DataXxCG.Size);
        Assert.AreEqual(500u, this.DataXxCG.VirtualSize);
        Assert.AreEqual(0u, this.DataXxCG.TailSlopSizeAlignment);
        Assert.AreEqual(0u, this.DataXxCG.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(".data$xx", this.DataXxCG.Name);
        Assert.IsTrue(ReferenceEquals(this.DataXxCG.Section, this.DataSection));

        Assert.AreEqual(450u, this.DataZzCG.Size);
        Assert.AreEqual(450u, this.DataZzCG.VirtualSize);
        Assert.AreEqual(0u, this.DataZzCG.TailSlopSizeAlignment);
        Assert.AreEqual(0u, this.DataZzCG.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(".data$zz", this.DataZzCG.Name);
        Assert.IsTrue(ReferenceEquals(this.DataZzCG.Section, this.DataSection));

        Assert.AreEqual(0u, this.BssCG.Size);
        Assert.AreEqual(500u, this.BssCG.VirtualSize);
        Assert.AreEqual(50u, this.BssCG.TailSlopSizeAlignment);
        Assert.AreEqual(3550u, this.BssCG.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(".bss", this.BssCG.Name);
        Assert.IsTrue(ReferenceEquals(this.BssCG.Section, this.DataSection));


        Assert.AreEqual(500u, this.RDataXxCG.Size);
        Assert.AreEqual(500u, this.RDataXxCG.VirtualSize);
        Assert.AreEqual(0u, this.RDataXxCG.TailSlopSizeAlignment);
        Assert.AreEqual(0u, this.RDataXxCG.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(".rdata$xx", this.RDataXxCG.Name);
        Assert.IsTrue(ReferenceEquals(this.RDataXxCG.Section, this.RDataSection));

        Assert.AreEqual(1200u, this.RDataZzCG.Size);
        Assert.AreEqual(1200u, this.RDataZzCG.VirtualSize);
        Assert.AreEqual(100u, this.RDataZzCG.TailSlopSizeAlignment);
        Assert.AreEqual(3300u, this.RDataZzCG.TailSlopVirtualSizeAlignment);
        Assert.AreEqual(".rdata$zz", this.RDataZzCG.Name);
        Assert.IsTrue(ReferenceEquals(this.RDataZzCG.Section, this.RDataSection));


        // Libs
        Assert.AreEqual(5925u, this.ALib.Size);
        Assert.AreEqual(5925, this.ALib.Compilands.Values.Sum(c => c.Size)); // No Byte Left Behind!
        Assert.AreEqual(5925, this.ALib.SectionContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(5925, this.ALib.SectionContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(5925, this.ALib.COFFGroupContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(5925, this.ALib.COFFGroupContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(6375u, this.ALib.VirtualSize);
        Assert.AreEqual(6375, this.ALib.Compilands.Values.Sum(c => c.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(6375, this.ALib.SectionContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(6375, this.ALib.SectionContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(6375, this.ALib.COFFGroupContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(6375, this.ALib.COFFGroupContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual("a.lib", this.ALib.Name);
        Assert.AreEqual("a", this.ALib.ShortName);
        Assert.AreEqual(2, this.ALib.SectionContributions.Count);
        Assert.AreEqual(2, this.ALib.SectionContributionsByName.Count);
        Assert.AreEqual(5, this.ALib.COFFGroupContributions.Count);
        Assert.AreEqual(5, this.ALib.COFFGroupContributionsByName.Count);

        Assert.AreEqual(25u, this.BLib.Size);
        Assert.AreEqual(25, this.BLib.Compilands.Values.Sum(c => c.Size)); // No Byte Left Behind!
        Assert.AreEqual(25, this.BLib.SectionContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(25, this.BLib.SectionContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(25, this.BLib.COFFGroupContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(25, this.BLib.COFFGroupContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(75u, this.BLib.VirtualSize);
        Assert.AreEqual(75, this.BLib.Compilands.Values.Sum(c => c.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(75, this.BLib.SectionContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(75, this.BLib.SectionContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(75, this.BLib.COFFGroupContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(75, this.BLib.COFFGroupContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual("b.lib", this.BLib.Name);
        Assert.AreEqual("b", this.BLib.ShortName);
        Assert.AreEqual(1, this.BLib.SectionContributions.Count);
        Assert.AreEqual(1, this.BLib.SectionContributionsByName.Count);
        Assert.AreEqual(2, this.BLib.COFFGroupContributions.Count);
        Assert.AreEqual(2, this.BLib.COFFGroupContributionsByName.Count);


        // Compilands
        Assert.AreEqual(5025u, this.A1Compiland.Size);
        Assert.AreEqual(5025, this.A1Compiland.SectionContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(5025, this.A1Compiland.SectionContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(5025, this.A1Compiland.COFFGroupContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(5025, this.A1Compiland.COFFGroupContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(5025u, this.A1Compiland.VirtualSize);
        Assert.AreEqual(5025, this.A1Compiland.SectionContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(5025, this.A1Compiland.SectionContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(5025, this.A1Compiland.COFFGroupContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(5025, this.A1Compiland.COFFGroupContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.IsTrue(ReferenceEquals(this.ALib, this.A1Compiland.Lib));
        Assert.AreEqual(@"c:\a\a1.obj", this.A1Compiland.Name);
        Assert.AreEqual("a1.obj", this.A1Compiland.ShortName);
        Assert.AreEqual(2, this.A1Compiland.SectionContributions.Count);
        Assert.AreEqual(2, this.A1Compiland.SectionContributionsByName.Count);
        Assert.AreEqual(3, this.A1Compiland.COFFGroupContributions.Count);
        Assert.AreEqual(3, this.A1Compiland.COFFGroupContributionsByName.Count);

        Assert.AreEqual(0u, this.A2Compiland.Size);
        Assert.AreEqual(0, this.A2Compiland.SectionContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(0, this.A2Compiland.SectionContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(0, this.A2Compiland.COFFGroupContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(0, this.A2Compiland.COFFGroupContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(400u, this.A2Compiland.VirtualSize);
        Assert.AreEqual(400, this.A2Compiland.SectionContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(400, this.A2Compiland.SectionContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(400, this.A2Compiland.COFFGroupContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(400, this.A2Compiland.COFFGroupContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.IsTrue(ReferenceEquals(this.ALib, this.A2Compiland.Lib));
        Assert.AreEqual(@"a2.obj", this.A2Compiland.Name);
        Assert.AreEqual("a2.obj", this.A2Compiland.ShortName);
        Assert.AreEqual(1, this.A2Compiland.SectionContributions.Count);
        Assert.AreEqual(1, this.A2Compiland.SectionContributionsByName.Count);
        Assert.AreEqual(1, this.A2Compiland.COFFGroupContributions.Count);
        Assert.AreEqual(1, this.A2Compiland.COFFGroupContributionsByName.Count);

        Assert.AreEqual(900u, this.A3Compiland.Size);
        Assert.AreEqual(900, this.A3Compiland.SectionContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(900, this.A3Compiland.SectionContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(900, this.A3Compiland.COFFGroupContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(900, this.A3Compiland.COFFGroupContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(950u, this.A3Compiland.VirtualSize);
        Assert.AreEqual(950, this.A3Compiland.SectionContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(950, this.A3Compiland.SectionContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(950, this.A3Compiland.COFFGroupContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(950, this.A3Compiland.COFFGroupContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.IsTrue(ReferenceEquals(this.ALib, this.A3Compiland.Lib));
        Assert.AreEqual(@"a3.obj", this.A3Compiland.Name);
        Assert.AreEqual("a3.obj", this.A3Compiland.ShortName);
        Assert.AreEqual(1, this.A3Compiland.SectionContributions.Count);
        Assert.AreEqual(1, this.A3Compiland.SectionContributionsByName.Count);
        Assert.AreEqual(3, this.A3Compiland.COFFGroupContributions.Count);
        Assert.AreEqual(3, this.A3Compiland.COFFGroupContributionsByName.Count);

        Assert.AreEqual(25u, this.B1Compiland.Size);
        Assert.AreEqual(25, this.B1Compiland.SectionContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(25, this.B1Compiland.SectionContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(25, this.B1Compiland.COFFGroupContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(25, this.B1Compiland.COFFGroupContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(75u, this.B1Compiland.VirtualSize);
        Assert.AreEqual(75, this.B1Compiland.SectionContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(75, this.B1Compiland.SectionContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(75, this.B1Compiland.COFFGroupContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(75, this.B1Compiland.COFFGroupContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.IsTrue(ReferenceEquals(this.BLib, this.B1Compiland.Lib));
        Assert.AreEqual(@"b1.obj", this.B1Compiland.Name);
        Assert.AreEqual("b1.obj", this.B1Compiland.ShortName);
        Assert.AreEqual(1, this.B1Compiland.SectionContributions.Count);
        Assert.AreEqual(1, this.B1Compiland.SectionContributionsByName.Count);
        Assert.AreEqual(2, this.B1Compiland.COFFGroupContributions.Count);
        Assert.AreEqual(2, this.B1Compiland.COFFGroupContributionsByName.Count);

        // Source Files
        Assert.AreEqual(500u, this.A1CppSourceFile.Size);
        Assert.AreEqual(500, this.A1CppSourceFile.SectionContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CppSourceFile.SectionContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CppSourceFile.COFFGroupContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CppSourceFile.COFFGroupContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CppSourceFile.CompilandContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(500u, this.A1CppSourceFile.VirtualSize);
        Assert.AreEqual(500, this.A1CppSourceFile.SectionContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CppSourceFile.SectionContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CppSourceFile.COFFGroupContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CppSourceFile.COFFGroupContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(500, this.A1CppSourceFile.CompilandContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(1, this.A1CppSourceFile.Compilands.Count);
        Assert.IsTrue(this.A1CppSourceFile.Compilands.Contains(this.A1Compiland));
        Assert.AreEqual("a1.cpp", this.A1CppSourceFile.Name);
        Assert.AreEqual("a1.cpp", this.A1CppSourceFile.ShortName);
        Assert.AreEqual(1, this.A1CppSourceFile.SectionContributions.Count);
        Assert.AreEqual(1, this.A1CppSourceFile.SectionContributionsByName.Count);
        Assert.AreEqual(2, this.A1CppSourceFile.COFFGroupContributions.Count);
        Assert.AreEqual(2, this.A1CppSourceFile.COFFGroupContributionsByName.Count);
        Assert.AreEqual(1, this.A1CppSourceFile.CompilandContributions.Count);

        Assert.AreEqual(825u, this.XHSourceFile.Size);
        Assert.AreEqual(825, this.XHSourceFile.SectionContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(825, this.XHSourceFile.SectionContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(825, this.XHSourceFile.COFFGroupContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(825, this.XHSourceFile.COFFGroupContributionsByName.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(825, this.XHSourceFile.CompilandContributions.Values.Sum(sc => sc.Size)); // No Byte Left Behind!
        Assert.AreEqual(825u, this.XHSourceFile.VirtualSize);
        Assert.AreEqual(825, this.XHSourceFile.SectionContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(825, this.XHSourceFile.SectionContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(825, this.XHSourceFile.COFFGroupContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(825, this.XHSourceFile.COFFGroupContributionsByName.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(825, this.XHSourceFile.CompilandContributions.Values.Sum(sc => sc.VirtualSize)); // No Byte Left Behind!
        Assert.AreEqual(3, this.XHSourceFile.Compilands.Count);
        Assert.IsTrue(this.XHSourceFile.Compilands.Contains(this.A1Compiland));
        Assert.IsTrue(this.XHSourceFile.Compilands.Contains(this.A3Compiland));
        Assert.IsTrue(this.XHSourceFile.Compilands.Contains(this.B1Compiland));
        Assert.AreEqual("x.h", this.XHSourceFile.Name);
        Assert.AreEqual("x.h", this.XHSourceFile.ShortName);
        Assert.AreEqual(2, this.XHSourceFile.SectionContributions.Count);
        Assert.AreEqual(2, this.XHSourceFile.SectionContributionsByName.Count);
        Assert.AreEqual(3, this.XHSourceFile.COFFGroupContributions.Count);
        Assert.AreEqual(3, this.XHSourceFile.COFFGroupContributionsByName.Count);
        Assert.AreEqual(3, this.XHSourceFile.CompilandContributions.Count);
    }

    public void Dispose() => this.DataCache.Dispose();
}
