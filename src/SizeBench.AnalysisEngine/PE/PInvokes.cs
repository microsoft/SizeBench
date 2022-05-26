using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security;

namespace SizeBench.AnalysisEngine.PE;

[ExcludeFromCodeCoverage]
[StructLayout(LayoutKind.Sequential)]
internal readonly struct IMAGE_DOS_HEADER
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public readonly char[] e_magic;       // Magic number
    public readonly ushort e_cblp;    // Bytes on last page of file
    public readonly ushort e_cp;      // Pages in file
    public readonly ushort e_crlc;    // Relocations
    public readonly ushort e_cparhdr;     // Size of header in paragraphs
    public readonly ushort e_minalloc;    // Minimum extra paragraphs needed
    public readonly ushort e_maxalloc;    // Maximum extra paragraphs needed
    public readonly ushort e_ss;      // Initial (relative) SS value
    public readonly ushort e_sp;      // Initial SP value
    public readonly ushort e_csum;    // Checksum
    public readonly ushort e_ip;      // Initial IP value
    public readonly ushort e_cs;      // Initial (relative) CS value
    public readonly ushort e_lfarlc;      // File address of relocation table
    public readonly ushort e_ovno;    // Overlay number
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public readonly ushort[] e_res1;    // Reserved words
    public readonly ushort e_oemid;       // OEM identifier (for e_oeminfo)
    public readonly ushort e_oeminfo;     // OEM information; e_oemid specific
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
    public readonly ushort[] e_res2;    // Reserved words
    public readonly int e_lfanew;      // File address of new exe header

    private string _e_magic => new string(this.e_magic);

    public bool isValid => this._e_magic == "MZ";
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct IMAGE_FILE_HEADER
{
    public readonly MachineType Machine;
    public readonly ushort NumberOfSections;
    public readonly uint TimeDateStamp;
    public readonly uint PointerToSymbolTable;
    public readonly uint NumberOfSymbols;
    public readonly ushort SizeOfOptionalHeader;
    public readonly ushort Characteristics;
}

internal enum MachineType : ushort
{
    Unknown = 0,
    I386 = 0x014c,
    Itanium = 0x0200,
    x64 = 0x8664,
    ARM = 0x01c4,
    ARM64 = 0xAA64
}
internal enum MagicType : ushort
{
    IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10b,
    IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20b
}
internal enum SubSystemType : ushort
{
    IMAGE_SUBSYSTEM_UNKNOWN = 0,
    IMAGE_SUBSYSTEM_NATIVE = 1,
    IMAGE_SUBSYSTEM_WINDOWS_GUI = 2,
    IMAGE_SUBSYSTEM_WINDOWS_CUI = 3,
    IMAGE_SUBSYSTEM_POSIX_CUI = 7,
    IMAGE_SUBSYSTEM_WINDOWS_CE_GUI = 9,
    IMAGE_SUBSYSTEM_EFI_APPLICATION = 10,
    IMAGE_SUBSYSTEM_EFI_BOOT_SERVICE_DRIVER = 11,
    IMAGE_SUBSYSTEM_EFI_RUNTIME_DRIVER = 12,
    IMAGE_SUBSYSTEM_EFI_ROM = 13,
    IMAGE_SUBSYSTEM_XBOX = 14

}

[Flags]
internal enum DllCharacteristicsType : ushort
{
    RES_0 = 0x0001,
    RES_1 = 0x0002,
    RES_2 = 0x0004,
    RES_3 = 0x0008,
    IMAGE_DLLCHARACTERISTICS_HIGH_ENTROPY_VA = 0x0020, // Image can handle a high entropy 64-bit virtual address space.
    IMAGE_DLLCHARACTERISTICS_DYNAMIC_BASE = 0x0040,
    IMAGE_DLLCHARACTERISTICS_FORCE_INTEGRITY = 0x0080,
    IMAGE_DLLCHARACTERISTICS_NX_COMPAT = 0x0100,
    IMAGE_DLLCHARACTERISTICS_NO_ISOLATION = 0x0200,
    IMAGE_DLLCHARACTERISTICS_NO_SEH = 0x0400,
    IMAGE_DLLCHARACTERISTICS_NO_BIND = 0x0800,
    IMAGE_DLLCHARACTERISTICS_APPCONTAINER = 0x1000, // Image should execute in an AppContainer
    IMAGE_DLLCHARACTERISTICS_WDM_DRIVER = 0x2000,
    IMAGE_DLLCHARACTERISTICS_GUARD_CF = 0x4000, // Image supports Control Flow Guard (CFG)
    IMAGE_DLLCHARACTERISTICS_TERMINAL_SERVER_AWARE = 0x8000
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct IMAGE_DATA_DIRECTORY
{
    public readonly uint VirtualAddress;
    public readonly uint Size;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct IMAGE_RESOURCE_DIRECTORY
{
    public readonly uint Characteristics;
    public readonly uint TimeDateStamp;
    public readonly ushort MajorVersion;
    public readonly ushort MinorVersion;
    public readonly ushort NumberOfNamedEntries;
    public readonly ushort NumberOfIdEntries;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct IMAGE_RESOURCE_DIRECTORY_ENTRY
{
    public readonly uint Name;
    public readonly uint OffsetToData;
    public bool DataIsDirectory => ((this.OffsetToData & 0x80000000) == 0x80000000);
    public uint OffsetToDirectory => this.OffsetToData & 0x7FFFFFFF;
    public bool IsNamedEntry => ((this.Name & 0x80000000) == 0x80000000);
    public bool IsIdEntry => !this.IsNamedEntry;
    public uint ID => this.Name & 0xFFFF;

    public uint NameOffset => this.Name & 0x7FFFFFFF;
    public unsafe string NameString(byte* rsrcSectionStart)
    {
        if (this.IsNamedEntry)
        {
            var nameAddress = rsrcSectionStart + this.NameOffset;
            // These strings are stores as 2 bytes of length, then the contents
            ushort length = *nameAddress;
            return Marshal.PtrToStringUni(new IntPtr(nameAddress + 2), length);
        }
        else
        {
            return $"#{this.ID}";
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct IMAGE_RESOURCE_DATA_ENTRY
{
    public readonly uint OffsetToData; // RVA
    public readonly uint Size;
    public readonly uint CodePage;
    public readonly uint Reserved;
}

// https://docs.microsoft.com/windows/win32/menurc/newheader
[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal readonly struct NEWHEADER
{
    public readonly ushort idReserved;
    public readonly ushort idType;
    public readonly ushort idCount;
    // The native structure has a variable-sized array of [ICON|CURSOR]RESDIR here, but we'll need to manually parse that as I can't figure out
    // how to get Marshal.PtrToStructure to realize a variable-sized array based on another member in the struct (idCount in this type).
};

// https://docs.microsoft.com/windows/win32/menurc/resdir
[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal readonly struct ICONRESDIR
{
    public readonly byte bWidth;
    public readonly byte bHeight;
    public readonly byte bColorCount;
    public readonly byte bReserved;
    public readonly ushort wPlanes;
    public readonly ushort wBitCount;
    public readonly uint dwBytesInRes;
    public readonly ushort nID;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal readonly struct CURSORRESDIR
{
    public readonly ushort wWidth;
    public readonly ushort wHeight;
    public readonly ushort wPlanes;
    public readonly ushort wBitCount;
    public readonly uint dwBytesInRes;
    public readonly ushort nID;
}

[StructLayout(LayoutKind.Explicit)]
internal readonly struct IMAGE_OPTIONAL_HEADER32
{
    [FieldOffset(0)]
    public readonly MagicType Magic;

    [FieldOffset(2)]
    public readonly byte MajorLinkerVersion;

    [FieldOffset(3)]
    public readonly byte MinorLinkerVersion;

    [FieldOffset(4)]
    public readonly uint SizeOfCode;

    [FieldOffset(8)]
    public readonly uint SizeOfInitializedData;

    [FieldOffset(12)]
    public readonly uint SizeOfUninitializedData;

    [FieldOffset(16)]
    public readonly uint AddressOfEntryPoint;

    [FieldOffset(20)]
    public readonly uint BaseOfCode;

    // PE32 contains this additional field
    [FieldOffset(24)]
    public readonly uint BaseOfData;

    [FieldOffset(28)]
    public readonly uint ImageBase;

    [FieldOffset(32)]
    public readonly uint SectionAlignment;

    [FieldOffset(36)]
    public readonly uint FileAlignment;

    [FieldOffset(40)]
    public readonly ushort MajorOperatingSystemVersion;

    [FieldOffset(42)]
    public readonly ushort MinorOperatingSystemVersion;

    [FieldOffset(44)]
    public readonly ushort MajorImageVersion;

    [FieldOffset(46)]
    public readonly ushort MinorImageVersion;

    [FieldOffset(48)]
    public readonly ushort MajorSubsystemVersion;

    [FieldOffset(50)]
    public readonly ushort MinorSubsystemVersion;

    [FieldOffset(52)]
    public readonly uint Win32VersionValue;

    [FieldOffset(56)]
    public readonly uint SizeOfImage;

    [FieldOffset(60)]
    public readonly uint SizeOfHeaders;

    [FieldOffset(64)]
    public readonly uint CheckSum;

    [FieldOffset(68)]
    public readonly SubSystemType Subsystem;

    [FieldOffset(70)]
    public readonly DllCharacteristicsType DllCharacteristics;

    [FieldOffset(72)]
    public readonly uint SizeOfStackReserve;

    [FieldOffset(76)]
    public readonly uint SizeOfStackCommit;

    [FieldOffset(80)]
    public readonly uint SizeOfHeapReserve;

    [FieldOffset(84)]
    public readonly uint SizeOfHeapCommit;

    [FieldOffset(88)]
    public readonly uint LoaderFlags;

    [FieldOffset(92)]
    public readonly uint NumberOfRvaAndSizes;

    [FieldOffset(96)]
    public readonly IMAGE_DATA_DIRECTORY ExportTable;

    [FieldOffset(104)]
    public readonly IMAGE_DATA_DIRECTORY ImportTable;

    [FieldOffset(112)]
    public readonly IMAGE_DATA_DIRECTORY ResourceTable;

    [FieldOffset(120)]
    public readonly IMAGE_DATA_DIRECTORY ExceptionTable;

    [FieldOffset(128)]
    public readonly IMAGE_DATA_DIRECTORY CertificateTable;

    [FieldOffset(136)]
    public readonly IMAGE_DATA_DIRECTORY BaseRelocationTable;

    [FieldOffset(144)]
    public readonly IMAGE_DATA_DIRECTORY Debug;

    [FieldOffset(152)]
    public readonly IMAGE_DATA_DIRECTORY Architecture;

    [FieldOffset(160)]
    public readonly IMAGE_DATA_DIRECTORY GlobalPtr;

    [FieldOffset(168)]
    public readonly IMAGE_DATA_DIRECTORY TLSTable;

    [FieldOffset(176)]
    public readonly IMAGE_DATA_DIRECTORY LoadConfigTable;

    [FieldOffset(184)]
    public readonly IMAGE_DATA_DIRECTORY BoundImport;

    [FieldOffset(192)]
    public readonly IMAGE_DATA_DIRECTORY IAT;

    [FieldOffset(200)]
    public readonly IMAGE_DATA_DIRECTORY DelayImportDescriptor;

    [FieldOffset(208)]
    public readonly IMAGE_DATA_DIRECTORY CLRRuntimeHeader;

    [FieldOffset(216)]
    public readonly IMAGE_DATA_DIRECTORY Reserved;
}
[StructLayout(LayoutKind.Explicit)]
internal readonly struct IMAGE_OPTIONAL_HEADER64
{
    [FieldOffset(0)]
    public readonly MagicType Magic;

    [FieldOffset(2)]
    public readonly byte MajorLinkerVersion;

    [FieldOffset(3)]
    public readonly byte MinorLinkerVersion;

    [FieldOffset(4)]
    public readonly uint SizeOfCode;

    [FieldOffset(8)]
    public readonly uint SizeOfInitializedData;

    [FieldOffset(12)]
    public readonly uint SizeOfUninitializedData;

    [FieldOffset(16)]
    public readonly uint AddressOfEntryPoint;

    [FieldOffset(20)]
    public readonly uint BaseOfCode;

    [FieldOffset(24)]
    public readonly ulong ImageBase;

    [FieldOffset(32)]
    public readonly uint SectionAlignment;

    [FieldOffset(36)]
    public readonly uint FileAlignment;

    [FieldOffset(40)]
    public readonly ushort MajorOperatingSystemVersion;

    [FieldOffset(42)]
    public readonly ushort MinorOperatingSystemVersion;

    [FieldOffset(44)]
    public readonly ushort MajorImageVersion;

    [FieldOffset(46)]
    public readonly ushort MinorImageVersion;

    [FieldOffset(48)]
    public readonly ushort MajorSubsystemVersion;

    [FieldOffset(50)]
    public readonly ushort MinorSubsystemVersion;

    [FieldOffset(52)]
    public readonly uint Win32VersionValue;

    [FieldOffset(56)]
    public readonly uint SizeOfImage;

    [FieldOffset(60)]
    public readonly uint SizeOfHeaders;

    [FieldOffset(64)]
    public readonly uint CheckSum;

    [FieldOffset(68)]
    public readonly SubSystemType Subsystem;

    [FieldOffset(70)]
    public readonly DllCharacteristicsType DllCharacteristics;

    [FieldOffset(72)]
    public readonly ulong SizeOfStackReserve;

    [FieldOffset(80)]
    public readonly ulong SizeOfStackCommit;

    [FieldOffset(88)]
    public readonly ulong SizeOfHeapReserve;

    [FieldOffset(96)]
    public readonly ulong SizeOfHeapCommit;

    [FieldOffset(104)]
    public readonly uint LoaderFlags;

    [FieldOffset(108)]
    public readonly uint NumberOfRvaAndSizes;

    [FieldOffset(112)]
    public readonly IMAGE_DATA_DIRECTORY ExportTable;

    [FieldOffset(120)]
    public readonly IMAGE_DATA_DIRECTORY ImportTable;

    [FieldOffset(128)]
    public readonly IMAGE_DATA_DIRECTORY ResourceTable;

    [FieldOffset(136)]
    public readonly IMAGE_DATA_DIRECTORY ExceptionTable;

    [FieldOffset(144)]
    public readonly IMAGE_DATA_DIRECTORY CertificateTable;

    [FieldOffset(152)]
    public readonly IMAGE_DATA_DIRECTORY BaseRelocationTable;

    [FieldOffset(160)]
    public readonly IMAGE_DATA_DIRECTORY Debug;

    [FieldOffset(168)]
    public readonly IMAGE_DATA_DIRECTORY Architecture;

    [FieldOffset(176)]
    public readonly IMAGE_DATA_DIRECTORY GlobalPtr;

    [FieldOffset(184)]
    public readonly IMAGE_DATA_DIRECTORY TLSTable;

    [FieldOffset(192)]
    public readonly IMAGE_DATA_DIRECTORY LoadConfigTable;

    [FieldOffset(200)]
    public readonly IMAGE_DATA_DIRECTORY BoundImport;

    [FieldOffset(208)]
    public readonly IMAGE_DATA_DIRECTORY IAT;

    [FieldOffset(216)]
    public readonly IMAGE_DATA_DIRECTORY DelayImportDescriptor;

    [FieldOffset(224)]
    public readonly IMAGE_DATA_DIRECTORY CLRRuntimeHeader;

    [FieldOffset(232)]
    public readonly IMAGE_DATA_DIRECTORY Reserved;
}

[ExcludeFromCodeCoverage]
[StructLayout(LayoutKind.Explicit)]
internal readonly struct IMAGE_NT_HEADERS32
{
    [FieldOffset(0)]
    public readonly uint Signature;

    [FieldOffset(4)]
    public readonly IMAGE_FILE_HEADER FileHeader;

    [FieldOffset(24)]
    public readonly IMAGE_OPTIONAL_HEADER32 OptionalHeader;

    private bool _SignatureIsValid => this.Signature == 0x00004550; /* this is "PE\0\0" as a UInt32 */

    public bool isValid => this._SignatureIsValid && (this.OptionalHeader.Magic == MagicType.IMAGE_NT_OPTIONAL_HDR32_MAGIC || this.OptionalHeader.Magic == MagicType.IMAGE_NT_OPTIONAL_HDR64_MAGIC);
}

[ExcludeFromCodeCoverage]
[StructLayout(LayoutKind.Explicit)]
internal readonly struct IMAGE_NT_HEADERS64
{
    [FieldOffset(0)]
    public readonly uint Signature;

    [FieldOffset(4)]
    public readonly IMAGE_FILE_HEADER FileHeader;

    [FieldOffset(24)]
    public readonly IMAGE_OPTIONAL_HEADER64 OptionalHeader;

    private bool _SignatureIsValid => this.Signature == 0x00004550; /* this is "PE\0\0" as a UInt32 */

    public bool isValid => this._SignatureIsValid && (this.OptionalHeader.Magic == MagicType.IMAGE_NT_OPTIONAL_HDR32_MAGIC || this.OptionalHeader.Magic == MagicType.IMAGE_NT_OPTIONAL_HDR64_MAGIC);
}

internal enum IMAGE_DEBUG_TYPE
{
    Unknown = 0,
    COFF = 1,
    CodeView = 2,
    FPO = 3,
    Misc = 4,
    Exception = 5,
    Fixup = 6,
    OmapToSrc = 7,
    OmapFromSrc = 8,
    Borland = 9,
    Reserved10 = 10,
    Clsid = 11,
    VCFeature = 12,
    POGO = 13,
    ILTCG = 14,
    MPX = 15,
    Repro = 16,
}

[StructLayout(LayoutKind.Explicit)]
internal readonly struct IMAGE_DEBUG_DIRECTORY
{
    [FieldOffset(0)]
    public readonly uint Characteristics;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly ushort MajorVersion;

    [FieldOffset(10)]
    public readonly ushort MinorVersion;

    [FieldOffset(12)]
    public readonly IMAGE_DEBUG_TYPE Type;

    [FieldOffset(16)]
    public readonly uint SizeOfData;

    [FieldOffset(20)]
    public readonly uint AddressOfRawData;

    [FieldOffset(24)]
    public readonly uint PointerToRawData;
}

internal enum IMAGE_DEBUG_DIRECTORY_POGO_MAGIC
{
    PGI = 0x50474900,
    PGO = 0x50474F00,
    PGU = 0x50475500
}

internal enum IMAGE_DEBUG_TYPE_MAGIC : int
{
    RSDS_SIGNATURE = 0x53445352
}

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]

internal readonly struct RSDS_DEBUG_FORMAT

{

    public readonly uint Signature; // RSDS

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]

    public readonly byte[] Guid;

    public readonly uint Age;

}

[StructLayout(LayoutKind.Explicit)]
internal readonly struct IMAGE_IMPORT_DESCRIPTOR
{
    [FieldOffset(0)]
    public readonly uint Characteristics;

    [FieldOffset(0)]
    public readonly uint OriginalFirstThunk;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly uint ForwarderChain;

    [FieldOffset(12)]
    public readonly uint Name;

    [FieldOffset(16)]
    public readonly uint FirstThunk;
}

[StructLayout(LayoutKind.Explicit)]
internal readonly struct IMAGE_THUNK_DATA32
{
    [FieldOffset(0)]
    public readonly uint ForwarderString;

    [FieldOffset(0)]
    public readonly uint Function;

    [FieldOffset(0)]
    public readonly uint Ordinal;

    [FieldOffset(0)]
    public readonly uint AddressOfData;
}

[StructLayout(LayoutKind.Explicit)]
internal readonly struct IMAGE_THUNK_DATA64
{
    [FieldOffset(0)]
    public readonly ulong ForwarderString;

    [FieldOffset(0)]
    public readonly ulong Function;

    [FieldOffset(0)]
    public readonly ulong Ordinal;

    [FieldOffset(0)]
    public readonly ulong AddressOfData;
}

[ExcludeFromCodeCoverage]
[DebuggerDisplay("Section={Section}, SizeOfRawData={SizeOfRawData}")]
[StructLayout(LayoutKind.Explicit)]
internal readonly struct IMAGE_SECTION_HEADER
{
    [FieldOffset(0)]
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public readonly char[] Name;

    [FieldOffset(8)]
    public readonly uint VirtualSize;

    [FieldOffset(12)]
    public readonly uint VirtualAddress;

    [FieldOffset(16)]
    public readonly uint SizeOfRawData;

    [FieldOffset(20)]
    public readonly uint PointerToRawData;

    [FieldOffset(24)]
    public readonly uint PointerToRelocations;

    [FieldOffset(28)]
    public readonly uint PointerToLinenumbers;

    [FieldOffset(32)]
    public readonly ushort NumberOfRelocations;

    [FieldOffset(34)]
    public readonly ushort NumberOfLinenumbers;

    [FieldOffset(36)]
    public readonly DataSectionFlags Characteristics;

    public string Section
    {
        get
        {
            var nameString = new string(this.Name);
            if (nameString.IndexOf('\0', StringComparison.Ordinal) != -1)
            {
                nameString = nameString[..nameString.IndexOf('\0', StringComparison.Ordinal)];
            }

            return nameString;
        }
    }

    public IMAGE_SECTION_HEADER(IMAGE_SECTION_HEADER original, string newName)
    {
        this.Name = newName.ToCharArray();
        this.VirtualSize = original.VirtualSize;
        this.VirtualAddress = original.VirtualAddress;
        this.SizeOfRawData = original.SizeOfRawData;
        this.PointerToRawData = original.PointerToRawData;
        this.PointerToRelocations = original.PointerToRelocations;
        this.PointerToLinenumbers = original.PointerToLinenumbers;
        this.NumberOfRelocations = original.NumberOfRelocations;
        this.NumberOfLinenumbers = original.NumberOfLinenumbers;
        this.Characteristics = original.Characteristics;
    }
}
[Flags]
#pragma warning disable CA1028 // Enum Storage should be Int32 - this is for interop purposes with DIA, and in DIA it is an unsigned int
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - "DataSectionFlags" is the name in the PE spec, so ignoring this rule
public enum DataSectionFlags : uint
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
#pragma warning restore CA1028 // Enum Storage should be Int32
{
    /// <summary>
    /// Reserved for future use.
    /// </summary>
#pragma warning disable CA1008 // Enums should have zero value - this is a well documented named part of the PE file format, the name should be exactly as it is in the PE spec.
    TypeReg = 0x00000000,
#pragma warning restore CA1008 // Enums should have zero value
    /// <summary>
    /// Reserved for future use.
    /// </summary>
    TypeDsect = 0x00000001,
    /// <summary>
    /// Reserved for future use.
    /// </summary>
    TypeNoLoad = 0x00000002,
    /// <summary>
    /// Reserved for future use.
    /// </summary>
    TypeGroup = 0x00000004,
    /// <summary>
    /// The section should not be padded to the next boundary. This flag is obsolete and is replaced by IMAGE_SCN_ALIGN_1BYTES. This is valid only for object files.
    /// </summary>
    TypeNoPadded = 0x00000008,
    /// <summary>
    /// Reserved for future use.
    /// </summary>
    TypeCopy = 0x00000010,
    /// <summary>
    /// The section contains executable code.
    /// </summary>
    ContentCode = 0x00000020,
    /// <summary>
    /// The section contains initialized data.
    /// </summary>
    ContentInitializedData = 0x00000040,
    /// <summary>
    /// The section contains uninitialized data.
    /// </summary>
    ContentUninitializedData = 0x00000080,
    /// <summary>
    /// Reserved for future use.
    /// </summary>
    LinkOther = 0x00000100,
    /// <summary>
    /// The section contains comments or other information. The .drectve section has this type. This is valid for object files only.
    /// </summary>
    LinkInfo = 0x00000200,
    /// <summary>
    /// Reserved for future use.
    /// </summary>
    TypeOver = 0x00000400,
    /// <summary>
    /// The section will not become part of the image. This is valid only for object files.
    /// </summary>
    LinkRemove = 0x00000800,
    /// <summary>
    /// The section contains COMDAT data. For more information, see section 5.5.6, COMDAT Sections (Object Only). This is valid only for object files.
    /// </summary>
    LinkComDat = 0x00001000,
    /// <summary>
    /// Reset speculative exceptions handling bits in the TLB entries for this section.
    /// </summary>
    NoDeferSpecExceptions = 0x00004000,
    /// <summary>
    /// The section contains data referenced through the global pointer (GP).
    /// </summary>
    RelativeGP = 0x00008000,
    /// <summary>
    /// Reserved for future use.
    /// </summary>
    MemPurgeable = 0x00020000,
    /// <summary>
    /// Reserved for future use.
    /// </summary>
#pragma warning disable CA1069 // Enums values should not be duplicated - this is an enum defined in the PE spec, not something I can control
    Memory16Bit = 0x00020000,
#pragma warning restore CA1069 // Enums values should not be duplicated
    /// <summary>
    /// Reserved for future use.
    /// </summary>
    MemoryLocked = 0x00040000,
    /// <summary>
    /// Reserved for future use.
    /// </summary>
    MemoryPreload = 0x00080000,
    /// <summary>
    /// Align data on a 1-byte boundary. Valid only for object files.
    /// </summary>
    Align1Bytes = 0x00100000,
    /// <summary>
    /// Align data on a 2-byte boundary. Valid only for object files.
    /// </summary>
    Align2Bytes = 0x00200000,
    /// <summary>
    /// Align data on a 4-byte boundary. Valid only for object files.
    /// </summary>
    Align4Bytes = 0x00300000,
    /// <summary>
    /// Align data on an 8-byte boundary. Valid only for object files.
    /// </summary>
    Align8Bytes = 0x00400000,
    /// <summary>
    /// Align data on a 16-byte boundary. Valid only for object files.
    /// </summary>
    Align16Bytes = 0x00500000,
    /// <summary>
    /// Align data on a 32-byte boundary. Valid only for object files.
    /// </summary>
    Align32Bytes = 0x00600000,
    /// <summary>
    /// Align data on a 64-byte boundary. Valid only for object files.
    /// </summary>
    Align64Bytes = 0x00700000,
    /// <summary>
    /// Align data on a 128-byte boundary. Valid only for object files.
    /// </summary>
    Align128Bytes = 0x00800000,
    /// <summary>
    /// Align data on a 256-byte boundary. Valid only for object files.
    /// </summary>
    Align256Bytes = 0x00900000,
    /// <summary>
    /// Align data on a 512-byte boundary. Valid only for object files.
    /// </summary>
    Align512Bytes = 0x00A00000,
    /// <summary>
    /// Align data on a 1024-byte boundary. Valid only for object files.
    /// </summary>
    Align1024Bytes = 0x00B00000,
    /// <summary>
    /// Align data on a 2048-byte boundary. Valid only for object files.
    /// </summary>
    Align2048Bytes = 0x00C00000,
    /// <summary>
    /// Align data on a 4096-byte boundary. Valid only for object files.
    /// </summary>
    Align4096Bytes = 0x00D00000,
    /// <summary>
    /// Align data on an 8192-byte boundary. Valid only for object files.
    /// </summary>
    Align8192Bytes = 0x00E00000,
    /// <summary>
    /// The section contains extended relocations.
    /// </summary>
    LinkExtendedRelocationOverflow = 0x01000000,
    /// <summary>
    /// The section can be discarded as needed.
    /// </summary>
    MemoryDiscardable = 0x02000000,
    /// <summary>
    /// The section cannot be cached.
    /// </summary>
    MemoryNotCached = 0x04000000,
    /// <summary>
    /// The section is not pageable.
    /// </summary>
    MemoryNotPaged = 0x08000000,
    /// <summary>
    /// The section can be shared in memory.
    /// </summary>
    MemoryShared = 0x10000000,
    /// <summary>
    /// The section can be executed as code.
    /// </summary>
    MemoryExecute = 0x20000000,
    /// <summary>
    /// The section can be read.
    /// </summary>
    MemoryRead = 0x40000000,
    /// <summary>
    /// The section can be written to.
    /// </summary>
    MemoryWrite = 0x80000000
}

// From https://msdn.microsoft.com/en-us/library/windows/desktop/ms680149(v=vs.85).aspx
internal enum IMAGE_DIRECTORY_ENTRY : ushort
{
    Architecture = 7,
    BaseReloc = 5,
    BoundImport = 11,
    COMDescriptor = 14,
    Debug = 6,
    DelayImport = 13,
    Exception = 3,
    Export = 0,
    GlobalPtr = 8,
    IAT = 12,
    Import = 1,
    LoadConfig = 10,
    Resource = 2,
    Security = 4,
    TLS = 9
}

internal static class PInvokes
{
    #region LoadLibraryEx / FreeLibrary

    [Flags]
    internal enum LoadLibraryFlags : uint
    {
        DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
        LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
        LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
        LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
        LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
        LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100,
        LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800,

    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetDllDirectory(string lpPathName);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("Kernelbase.dll", CallingConvention = CallingConvention.Winapi, PreserveSig = true, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadLibraryExW([In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName, IntPtr reserved, [In] LoadLibraryFlags flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern unsafe bool FreeLibrary(IntPtr hModule);

    #endregion

    #region DbgHelp - note we use the copy in SizeBench from DbgX, NOT the OS version

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport(@"amd64\dbghelp.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "ImageNtHeader")]
    [SuppressUnmanagedCodeSecurity]
    internal static extern unsafe void* ImageNtHeader(void* Base);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport(@"amd64\dbghelp.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "ImageDirectoryEntryToDataEx")]
    internal static extern unsafe void* ImageDirectoryEntryToDataEx(void* Base,
                                                                   [MarshalAs(UnmanagedType.Bool)] bool MappedAsImage,
                                                                   [MarshalAs(UnmanagedType.U2)] IMAGE_DIRECTORY_ENTRY DirectoryEntry,
                                                                   [MarshalAs(UnmanagedType.U2)] out ushort Size,
                                                                   out IntPtr FoundHeader);

    #endregion

    #region MSVCRT

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    public static extern IntPtr memcpy(IntPtr dest, IntPtr src, UIntPtr count);

    #endregion

    #region Imagehlp

    internal enum MapFileAndCheckSumWResult : uint
    {
        CHECKSUM_SUCCESS = 0,
        CHECKSUM_MAP_FAILURE = 2,
        CHECKSUM_MAPVIEW_FAILURE = 3,
        CHECKSUM_OPEN_FAILURE = 1,
        CHECKSUM_UNICODE_FAILURE = 4
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("imagehlp.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "MapFileAndCheckSumW")]
    internal static extern MapFileAndCheckSumWResult MapFileAndCheckSumW([MarshalAs(UnmanagedType.LPWStr)] string filename,
                                                                         [MarshalAs(UnmanagedType.U4)] out uint originalHeaderChecksum,
                                                                         [MarshalAs(UnmanagedType.U4)] out uint newChecksum);

    #endregion

}
