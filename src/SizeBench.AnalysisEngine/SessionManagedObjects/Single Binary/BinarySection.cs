using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.PortableExecutable;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("Binary Section: {Name}, Size={Size}, RVA={RVA}")]
public sealed class BinarySection
{
    internal static readonly BinarySection NoSectionsSentinel = new BinarySection("No sections"); // To be used only for diffing, really

    private bool _fullyConstructed;

    public string Name { get; }

    [Display(AutoGenerateField = false)]
    public SectionCharacteristics Characteristics { get; }

    [DisplayFormat(DataFormatString = "0x{0:X}")]
    public uint RVA { get; }

    #region Size (on disk - not VirtualSize)

    // From the PE docs on MSDN:
    // The alignment factor (in bytes) that is used to align the raw data of sections in the image file. The value should 
    // be a power of 2 between 512 and 64 K, inclusive. The default is 512. If the SectionAlignment is less than the 
    // architecture's page size, then FileAlignment must match SectionAlignment.
    internal uint FileAlignment { get; }

    private readonly uint _size;

    [Display(Name = "Size on disk")]
    public uint Size
    {
        get
        {
            DebugValidateSize();

            return this._size;
        }
    }

    // Code coverage on sanity checks isn't necessary, the goal is that they never fail.
    [ExcludeFromCodeCoverage]
    [Conditional("DEBUG")]
    private void DebugValidateSize()
    {
        // Sections in a binary have a "file alignment" (in link /dump /headers) which means they'll
        // often have some slop in them that's not in a COFF Group's Size, only in its TailSlop.
        // This is ok, but if there's more missing mysterious space than that, we did something wrong
        // when constructing this object and that should be sorted out.
        if (this._fullyConstructed && this.COFFGroups.Count > 0)
        {
            var coffGroupSizeSum = (uint)this.COFFGroups.Sum(cg => cg.Size + (long)cg.TailSlopSizeAlignment);
            if (this._size != coffGroupSizeSum)
            {
                Trace.WriteLine("COFF Group sizes don't match binary section size, this is going to throw.");
                Trace.WriteLine("Here's some additional data to try to help with debugging:");
                Trace.WriteLine($"coffGroupSizeSum != _size (0x{coffGroupSizeSum:X} != 0x{this._size:X})");
                Trace.WriteLine($"COFFGroups.Sum(cg => (long)cg.Size)=0x{this.COFFGroups.Sum(cg => cg.Size):X}");
                Trace.WriteLine($"COFFGroups.Sum(cg => (long)cg.TailSlopSizeAlignment)=0x{this.COFFGroups.Sum(cg => cg.TailSlopSizeAlignment):X}");
                PrintDebuggingInfoForGapAndAlignmentAnalysis();

                throw new InvalidOperationException("Something has gone terribly wrong!");
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private void PrintDebuggingInfoForGapAndAlignmentAnalysis()
    {
        Trace.WriteLine($"Section: {this.Name}, RVAStart=0x{this.RVA:X}, RVAEnd=0x{this.RVA + this._virtualSize:X}, Size=0x{this._size:X}, VirtualSize=0x{this._virtualSize:X}");
        foreach (var coffGroup in this._coffGroups.OrderBy(cg => cg.RVA))
        {
            Trace.WriteLine($"{coffGroup.Name}: RVAStart=0x{coffGroup.RVA:X}, RVAEnd=0x{coffGroup.RVA + coffGroup.VirtualSize:X}, Size=0x{coffGroup.Size:X}, VirtualSize=0x{coffGroup.VirtualSize:X}, TailSlopSizeAlignment={coffGroup._tailSlopSizeAlignment:X}, TailSlopVirtualSizeAlignment={coffGroup._tailSlopVirtualSizeAlignment:X}");
        }
    }

    #endregion

    #region VirtualSize

    // From the PE docs on MSDN:
    // The alignment (in bytes) of sections when they are loaded into memory. It must be greater than or equal to FileAlignment.
    // The default is the page size for the architecture.
    internal uint SectionAlignment { get; }

    // The bytes between the end of the VirtualSize in memory and the end of the section - in other words, these bytes may or
    // may not use up disk space, but they will take up space in memory when this binary is loaded.  They are all padding.
    internal uint TailSlopVirtualSizeAlignment { get; }

    private readonly uint _virtualSize;
    [Display(Name = "Size in memory")]
    public uint VirtualSize
    {
        get
        {
            DebugValidateVirtualSize();
            return this._virtualSize;
        }
    }

    [Conditional("DEBUG")]
    private void DebugValidateVirtualSize()
    {
        // Sections in a binary have a "file alignment" (in link /dump /headers) which means they'll
        // often have some slop in them that's not in a COFF Group's VirtualSize, only in its TailSlop.
        // This is ok, but if there's more missing mysterious space than that, we did something wrong
        // when constructing this object and that should be sorted out.
        if (this._fullyConstructed && this.COFFGroups.Count > 0)
        {
            var coffGroupSizeSum = (uint)this.COFFGroups.Sum(cg => cg.VirtualSize + (long)cg.TailSlopVirtualSizeAlignment);
            if (this._virtualSize + this.TailSlopVirtualSizeAlignment != coffGroupSizeSum)
            {
                throw new InvalidOperationException("Something has gone terribly wrong!");
            }
        }
    }

    [Display(Name = "Size in memory (including section padding)")]
    public uint VirtualSizeIncludingPadding => this.VirtualSize + this.TailSlopVirtualSizeAlignment;

    #endregion

    #region COFF Groups

    private readonly List<COFFGroup> _coffGroups = new List<COFFGroup>();

    public IReadOnlyList<COFFGroup> COFFGroups
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._coffGroups;
        }
    }

    public void AddCOFFGroup(COFFGroup cg)
    {
        if (this._fullyConstructed)
        {
            throw new ObjectFullyConstructedAlreadyException();
        }

        this._coffGroups.Add(cg);
    }

    #endregion

    private BinarySection(string name) :
        this(null /* cache */, name, size: 0, virtualSize: 0, rva: 0, fileAlignment: 0, sectionAlignment: 0, characteristics: 0)
    {
        MarkFullyConstructed();
    }

    internal BinarySection(SessionDataCache? cache, string name, uint size, uint virtualSize, uint rva, uint fileAlignment, uint sectionAlignment, SectionCharacteristics characteristics)
    {
#if DEBUG
        if (cache?.BinarySectionsConstructedEver.Any(bs => bs.RVA == rva || bs.Name == name) == true)
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.RVA = rva;
        this._size = size;
        this._virtualSize = virtualSize;
        this.Name = name;
        this.FileAlignment = fileAlignment;
        this.SectionAlignment = sectionAlignment;
        this.Characteristics = characteristics;

        // We assume that every section's Size is a multiple of FileAlignment - this seems to be true.  If it's not, it wouldn't be especially hard to add
        // TailSlopSizeAlignment like TailSlopVirtualSizeAlignment.
        // But for now, rather than plumb that through everywhere, I'll just throw if I see a section whose size is not a multiple of FileAlignment, to help
        // me learn when that may not be true.
        if (fileAlignment != 0)
        {
            ulong tailSlopSizeAlignment = (size % fileAlignment) == 0 ? 0 : fileAlignment - (size % fileAlignment);
            if (tailSlopSizeAlignment != 0)
            {
                throw new InvalidOperationException($"BinarySection '{name}' has a Size of 0x{size:X}, which is not a multiple of its FileAlignment (0x{fileAlignment:X})!  This may indicate a bug in SizeBench.");
            }
        }
        if (sectionAlignment != 0)
        {
            this.TailSlopVirtualSizeAlignment = (virtualSize % sectionAlignment) == 0 ? 0 : sectionAlignment - (virtualSize % sectionAlignment);
        }

        cache?.RecordBinarySectionConstructed(this);
    }

    internal void MarkFullyConstructed()
    {
        // When a binary section is finally 'ready' we can calculate how much padding is being used between the COFF Groups
        // in here.  Note that the alignment requirements of a COFF Group are not available in the PDB anywhere, nor are they
        // calculate-able from the binary - so we have to just guess at what alignment could be.  I've asked for a
        // DIA API to query alignment requirements to someday not need this hacky guess.
        // In the meantime, to stick to what has been seen in the wild, limit alignment requirement guesses to 64-byte-alignment.
        // If we calculate we have more padding than that, this is possibly a bug in the tool (a hole in understanding and
        // analysis) or it's just that in practice padding can be greater.  Investigate before changing this code.
        var coffGroupsSortedByRVA = this._coffGroups.OrderBy(cg => cg.RVA).ToList();
        var biggestRVASeenBySize = this.RVA;
        for (var i = 1; i < coffGroupsSortedByRVA.Count; i++)
        {
            // Calculate the 'gap' between the previous COFF Group and this one
            var previousRVAEndVirtualSize = coffGroupsSortedByRVA[i - 1].RVA + coffGroupsSortedByRVA[i - 1].VirtualSize;
            var previousRVAEndSize = coffGroupsSortedByRVA[i - 1].RVA + coffGroupsSortedByRVA[i - 1].Size;
            var gapVirtualSize = coffGroupsSortedByRVA[i].RVA - previousRVAEndVirtualSize;
            var gapSize = coffGroupsSortedByRVA[i].RVA - previousRVAEndSize;
            if (gapVirtualSize > this.FileAlignment)
            {
                Trace.WriteLine($"COFF Groups {coffGroupsSortedByRVA[i - 1].Name} and {coffGroupsSortedByRVA[i].Name} contain a large gap between them ({gapVirtualSize} bytes), which is larger than the FileAlignment ({this.FileAlignment}).  This has not been seen before in practice, and may indicate a bug in SizeBench.");
                PrintDebuggingInfoForGapAndAlignmentAnalysis();

                throw new InvalidOperationException($"The gap between COFF Groups '{coffGroupsSortedByRVA[i].Name}' and '{coffGroupsSortedByRVA[i - 1].Name}' in binary section '{this.Name}' is {gapVirtualSize} bytes - a gap this large has not been observed before so it may indicate a bug in SizeBench");
            }
            coffGroupsSortedByRVA[i - 1].TailSlopVirtualSizeAlignment = gapVirtualSize;
            coffGroupsSortedByRVA[i - 1].TailSlopSizeAlignment = gapSize;

            var totalSizeAttributed = coffGroupsSortedByRVA[i - 1].Size + gapSize;

            if (totalSizeAttributed > 0 &&
                (coffGroupsSortedByRVA[i - 1].RVA + totalSizeAttributed > biggestRVASeenBySize))
            {
                biggestRVASeenBySize = coffGroupsSortedByRVA[i - 1].RVA + totalSizeAttributed;
            }
        }

        if (coffGroupsSortedByRVA.Count > 0)
        {
            // The last COFF Group also has tail slop padding (potentially), from the end of itself to the end of the section (due to section alignment)
            var lastCG = coffGroupsSortedByRVA[^1];
            lastCG.TailSlopVirtualSizeAlignment = (this.RVA + this.VirtualSize + this.TailSlopVirtualSizeAlignment) - (lastCG.RVA + lastCG.VirtualSize);
            lastCG.TailSlopSizeAlignment = (this.RVA + this.Size) - biggestRVASeenBySize - lastCG.Size;
        }

        this._fullyConstructed = true;
    }
}
