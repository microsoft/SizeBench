using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using SizeBench.AnalysisEngine.PE;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class COFFGroup
{
    private string DebuggerDisplay
    {
        get
        {
            if (this._fullyConstructed)
            {
                return $"COFF Group: {this.Name}, Section={this.Section?.Name ?? "<none set>"} Size={this.Size}, VirtualSize={this.VirtualSize}, RVA={this.RVA}";
            }
            else
            {
                return $"[Not Yet Fully Constructed] COFF Group: {this.Name}, _rawSize={this._rawSize}, RVA={this.RVA}";
            }
        }
    }

    private bool _fullyConstructed;

    public string Name { get; }

    [DisplayFormat(DataFormatString = "0x{0:X}")]
    public uint RVA { get; }

    [Display(AutoGenerateField = false)]
    public DataSectionFlags Characteristics { get; }

    // When parsing a COFF Group it is not possible to tell if the size found is real
    // or virtual, so we must look it up based on properties of its containing section.
    private uint _rawSize;
    internal uint RawSize
    {
        get
        {
            // You should not use RawSize past initial construction time - it's a parking
            // ground for the size until we can determine if it's real or virtual.
            // Post-construction-time you should always use Size or VirtualSize.
            if (this._fullyConstructed)
            {
                throw new ObjectFullyConstructedAlreadyException();
            }

            return this._rawSize;
        }
        private set => this._rawSize = value;
    }

    #region Size (on-disk, not VirtualSize)

    // From the PE docs on MSDN:
    // The alignment factor (in bytes) that is used to align the raw data of sections in the image file. The value should 
    // be a power of 2 between 512 and 64 K, inclusive. The default is 512. If the SectionAlignment is less than the 
    // architecture's page size, then FileAlignment must match SectionAlignment.
    internal uint FileAlignment { get; }

    internal uint? _tailSlopSizeAlignment;
    [Display(AutoGenerateField = false)]
    public uint TailSlopSizeAlignment
    {
        get
        {
            if (!this._fullyConstructed || !this._tailSlopSizeAlignment.HasValue)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._tailSlopSizeAlignment.Value;
        }
        internal set
        {
            if (this._tailSlopSizeAlignment.HasValue)
            {
                throw new InvalidOperationException("This value is only expected to be set once, how did this happen?");
            }

            this._tailSlopSizeAlignment = value;
        }
    }

    private uint _size;
    [Display(Name = "Size on disk")]
    public uint Size
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._size;
        }
    }

    #endregion

    #region VirtualSize

    internal bool IsVirtualSizeOnly => this.Size == 0 && this.VirtualSize > 0;

    // From the PE docs on MSDN:
    // The alignment (in bytes) of sections when they are loaded into memory. It must be greater than or equal to FileAlignment.
    // The default is the page size for the architecture.
    internal uint SectionAlignment { get; }

    internal uint _tailSlopVirtualSizeAlignment;
    [Display(AutoGenerateField = false)]
    public uint TailSlopVirtualSizeAlignment
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._tailSlopVirtualSizeAlignment;
        }
        internal set => this._tailSlopVirtualSizeAlignment = value;
    }

    private uint _virtualSize;
    [Display(Name = "Size in memory")]
    public uint VirtualSize
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._virtualSize;
        }
    }

    [Display(Name = "Size in memory (including padding)")]
    public uint VirtualSizeIncludingPadding
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._virtualSize + this._tailSlopVirtualSizeAlignment;
        }
    }

    #endregion

    // _section isn't nullable because it'll be set very shortly after the constructor in reality.
    private BinarySection _section = BinarySection.NoSectionsSentinel;
    [Display(AutoGenerateField = false)]
    public BinarySection Section
    {
        get
        {
            if (!this._fullyConstructed)
            {
                throw new ObjectNotYetFullyConstructedException();
            }

            return this._section;
        }
        internal set
        {
            if (this._fullyConstructed)
            {
                throw new ObjectFullyConstructedAlreadyException();
            }

            this._section = value;
        }
    }

    //TODO: remove FileAlignment and SectionAlignment parameters here, they don't do much...but need to check how the size vs. virtualsize is calculated in
    //      MarkFullyConstructed.  It seems like I could base this off VA vs. RVA or something instead?
    internal COFFGroup(SessionDataCache cache, string name, uint size, uint rva, uint fileAlignment, uint sectionAlignment, DataSectionFlags characteristics)
    {
#if DEBUG
        var conflict = cache.COFFGroupsConstrutedEver.Where(cg => cg.RVA == rva || cg.Name == name).FirstOrDefault();
        if (conflict != null)
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.RVA = rva;
        this.Characteristics = characteristics;
        this.RawSize = size;
        this.Name = name;
        this.FileAlignment = fileAlignment;
        this.SectionAlignment = sectionAlignment;

        cache.RecordCOFFGroupConstructed(this);
    }

    internal void MarkFullyConstructed()
    {
        // If the section alignment is specified to be below 4K (0x1000) then the linker will lay out the binary to include
        // some uninitialized data (like .bss) on-disk, because it needs to be able to be mapped into memory.
        // If, however, the file alignment is >= 4K (the default), then we can use the characteristics to
        // determine if this is representing 'real' size (on-disk size) or 'virtual' size (size in memory, but not on-disk).
        if (this.SectionAlignment >= 0x1000 &&
             this.Characteristics.HasFlag(DataSectionFlags.ContentUninitializedData) &&
            !this.Characteristics.HasFlag(DataSectionFlags.ContentInitializedData))
        {
            this._virtualSize = this.RawSize;
            this._size = 0;
        }
        else
        {
            this._virtualSize = this.RawSize;
            this._size = this.RawSize;
        }

        this._fullyConstructed = true;
    }
}
