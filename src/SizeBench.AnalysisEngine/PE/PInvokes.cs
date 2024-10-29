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

#pragma warning disable CA1028 // Enum Storage should be Int32 - this is a 16 bit value for P/Invoke marshaling, per PE file format
public enum MachineType : ushort
#pragma warning restore CA1028 // Enum Storage should be Int32
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
    EmbeddedPortablePDB = 17,
    SPGO = 18,
    PDBChecksum = 19,
    ExtendedDllCharacteristics = 20,
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
    // Fun trivia: "RSDS" is for "Richard S, Dan S", presumably those two folks worked on defining this debugging information back in the day.
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
internal readonly struct IMAGE_DELAYLOAD_DESCRIPTOR
{
    [FieldOffset(0)]
    public readonly uint Attributes;

    public bool RvaBased => (this.Attributes & 0x1) == 0x1;

    [FieldOffset(4)]
    public readonly uint DllNameRVA;

    [FieldOffset(8)]
    public readonly uint ModuleHandleRVA;

    [FieldOffset(12)]
    public readonly uint ImportAddressTableRVA;

    [FieldOffset(16)]
    public readonly uint ImportNameTableRVA;

    [FieldOffset(20)]
    public readonly uint BoundImportAddressTableRVA;

    [FieldOffset(24)]
    public readonly uint UnloadInformationTableRVA;

    [FieldOffset(28)]
    public readonly uint TimeDateStamp;
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
            if (nameString.Contains('\0', StringComparison.Ordinal))
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

internal static class CFGConstants
{
    internal const uint IMAGE_GUARD_CF_FUNCTION_TABLE_SIZE_MASK = 0xF0000000;
    internal const uint IMAGE_GUARD_CF_FUNCTION_TABLE_SIZE_SHIFT = 28;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct IMAGE_LOAD_CONFIG_CODE_INTEGRITY
{
    public readonly ushort Flags;
    public readonly ushort Catalog;
    public readonly uint CatalogOffset;
    public readonly uint Reserved;
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal readonly struct IMAGE_LOAD_CONFIG_DIRECTORY64_V1
{
    [FieldOffset(0)]
    public readonly uint Size;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly ushort MajorVersion;

    [FieldOffset(10)]
    public readonly ushort MinorVersion;

    [FieldOffset(12)]
    public readonly uint GlobalFlagsClear;

    [FieldOffset(16)]
    public readonly uint GlobalFlagsSet;

    [FieldOffset(20)]
    public readonly uint CriticalSectionDefaultTimeout;

    [FieldOffset(24)]
    public readonly ulong DeCommitFreeBlockThreshold;

    [FieldOffset(32)]
    public readonly ulong DeCommitTotalFreeThreshold;

    [FieldOffset(40)]
    public readonly ulong LockPrefixTable;

    [FieldOffset(48)]
    public readonly ulong MaximumAllocationSize;

    [FieldOffset(56)]
    public readonly ulong VirtualMemoryThershold;

    [FieldOffset(64)]
    public readonly ulong ProcessAffinityMask;

    [FieldOffset(72)]
    public readonly uint ProcessHeapFlags;

    [FieldOffset(76)]
    public readonly ushort CSDVersion;

    [FieldOffset(78)]
    public readonly ushort DependentLoadFlags;

    [FieldOffset(80)]
    public readonly ulong EditList;

    [FieldOffset(88)]
    public readonly ulong SecurityCookie;

    [FieldOffset(96)]
    public readonly ulong SEHandlerTable;

    [FieldOffset(104)]
    public readonly ulong SEHandlerCount;
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal readonly struct IMAGE_LOAD_CONFIG_DIRECTORY32_V1
{
    [FieldOffset(0)]
    public readonly uint Size;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly ushort MajorVersion;

    [FieldOffset(10)]
    public readonly ushort MinorVersion;

    [FieldOffset(12)]
    public readonly uint GlobalFlagsClear;

    [FieldOffset(16)]
    public readonly uint GlobalFlagsSet;

    [FieldOffset(20)]
    public readonly uint CriticalSectionDefaultTimeout;

    [FieldOffset(24)]
    public readonly uint DeCommitFreeBlockThreshold;

    [FieldOffset(28)]
    public readonly uint DeCommitTotalFreeThreshold;

    [FieldOffset(32)]
    public readonly uint LockPrefixTable;

    [FieldOffset(36)]
    public readonly uint MaximumAllocationSize;

    [FieldOffset(40)]
    public readonly uint VirtualMemoryThershold;

    [FieldOffset(44)]
    public readonly uint ProcessAffinityMask;

    [FieldOffset(48)]
    public readonly uint ProcessHeapFlags;

    [FieldOffset(52)]
    public readonly ushort CSDVersion;

    [FieldOffset(54)]
    public readonly ushort DependentLoadFlags;

    [FieldOffset(56)]
    public readonly uint EditList;

    [FieldOffset(60)]
    public readonly uint SecurityCookie;

    [FieldOffset(64)]
    public readonly uint SEHandlerTable;

    [FieldOffset(68)]
    public readonly uint SEHandlerCount;
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal readonly struct IMAGE_LOAD_CONFIG_DIRECTORY64_V2
{
    [FieldOffset(0)]
    public readonly uint Size;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly ushort MajorVersion;

    [FieldOffset(10)]
    public readonly ushort MinorVersion;

    [FieldOffset(12)]
    public readonly uint GlobalFlagsClear;

    [FieldOffset(16)]
    public readonly uint GlobalFlagsSet;

    [FieldOffset(20)]
    public readonly uint CriticalSectionDefaultTimeout;

    [FieldOffset(24)]
    public readonly ulong DeCommitFreeBlockThreshold;

    [FieldOffset(32)]
    public readonly ulong DeCommitTotalFreeThreshold;

    [FieldOffset(40)]
    public readonly ulong LockPrefixTable;

    [FieldOffset(48)]
    public readonly ulong MaximumAllocationSize;

    [FieldOffset(56)]
    public readonly ulong VirtualMemoryThershold;

    [FieldOffset(64)]
    public readonly ulong ProcessAffinityMask;

    [FieldOffset(72)]
    public readonly uint ProcessHeapFlags;

    [FieldOffset(76)]
    public readonly ushort CSDVersion;

    [FieldOffset(78)]
    public readonly ushort DependentLoadFlags;

    [FieldOffset(80)]
    public readonly ulong EditList;

    [FieldOffset(88)]
    public readonly ulong SecurityCookie;

    [FieldOffset(96)]
    public readonly ulong SEHandlerTable;

    [FieldOffset(104)]
    public readonly ulong SEHandlerCount;

    [FieldOffset(112)]
    public readonly ulong GuardCFCheckFunctionPointer;

    [FieldOffset(120)]
    public readonly ulong GuardCFDispatchFunctionPointer;

    [FieldOffset(128)]
    public readonly ulong GuardCFFunctionTable;

    [FieldOffset(136)]
    public readonly ulong GuardCFFunctionCount;

    [FieldOffset(144)]
    public readonly uint GuardFlags;
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal readonly struct IMAGE_LOAD_CONFIG_DIRECTORY32_V2
{
    [FieldOffset(0)]
    public readonly uint Size;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly ushort MajorVersion;

    [FieldOffset(10)]
    public readonly ushort MinorVersion;

    [FieldOffset(12)]
    public readonly uint GlobalFlagsClear;

    [FieldOffset(16)]
    public readonly uint GlobalFlagsSet;

    [FieldOffset(20)]
    public readonly uint CriticalSectionDefaultTimeout;

    [FieldOffset(24)]
    public readonly uint DeCommitFreeBlockThreshold;

    [FieldOffset(28)]
    public readonly uint DeCommitTotalFreeThreshold;

    [FieldOffset(32)]
    public readonly uint LockPrefixTable;

    [FieldOffset(36)]
    public readonly uint MaximumAllocationSize;

    [FieldOffset(40)]
    public readonly uint VirtualMemoryThershold;

    [FieldOffset(44)]
    public readonly uint ProcessAffinityMask;

    [FieldOffset(48)]
    public readonly uint ProcessHeapFlags;

    [FieldOffset(52)]
    public readonly ushort CSDVersion;

    [FieldOffset(54)]
    public readonly ushort DependentLoadFlags;

    [FieldOffset(56)]
    public readonly uint EditList;

    [FieldOffset(60)]
    public readonly uint SecurityCookie;

    [FieldOffset(64)]
    public readonly uint SEHandlerTable;

    [FieldOffset(68)]
    public readonly uint SEHandlerCount;

    [FieldOffset(72)]
    public readonly uint GuardCFCheckFunctionPointer;

    [FieldOffset(76)]
    public readonly uint GuardCFDispatchFunctionPointer;

    [FieldOffset(80)]
    public readonly uint GuardCFFunctionTable;

    [FieldOffset(84)]
    public readonly uint GuardCFFunctionCount;

    [FieldOffset(88)]
    public readonly uint GuardFlags;
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal readonly struct IMAGE_LOAD_CONFIG_DIRECTORY64_V3
{
    [FieldOffset(0)]
    public readonly uint Size;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly ushort MajorVersion;

    [FieldOffset(10)]
    public readonly ushort MinorVersion;

    [FieldOffset(12)]
    public readonly uint GlobalFlagsClear;

    [FieldOffset(16)]
    public readonly uint GlobalFlagsSet;

    [FieldOffset(20)]
    public readonly uint CriticalSectionDefaultTimeout;

    [FieldOffset(24)]
    public readonly ulong DeCommitFreeBlockThreshold;

    [FieldOffset(32)]
    public readonly ulong DeCommitTotalFreeThreshold;

    [FieldOffset(40)]
    public readonly ulong LockPrefixTable;

    [FieldOffset(48)]
    public readonly ulong MaximumAllocationSize;

    [FieldOffset(56)]
    public readonly ulong VirtualMemoryThershold;

    [FieldOffset(64)]
    public readonly ulong ProcessAffinityMask;

    [FieldOffset(72)]
    public readonly uint ProcessHeapFlags;

    [FieldOffset(76)]
    public readonly ushort CSDVersion;

    [FieldOffset(78)]
    public readonly ushort DependentLoadFlags;

    [FieldOffset(80)]
    public readonly ulong EditList;

    [FieldOffset(88)]
    public readonly ulong SecurityCookie;

    [FieldOffset(96)]
    public readonly ulong SEHandlerTable;

    [FieldOffset(104)]
    public readonly ulong SEHandlerCount;

    [FieldOffset(112)]
    public readonly ulong GuardCFCheckFunctionPointer;

    [FieldOffset(120)]
    public readonly ulong GuardCFDispatchFunctionPointer;

    [FieldOffset(128)]
    public readonly ulong GuardCFFunctionTable;

    [FieldOffset(136)]
    public readonly ulong GuardCFFunctionCount;

    [FieldOffset(144)]
    public readonly uint GuardFlags;

    [FieldOffset(148)]
    public readonly IMAGE_LOAD_CONFIG_CODE_INTEGRITY CodeIntegrity;

    [FieldOffset(160)]
    public readonly ulong GuardAddressTakenIatEntryTable;

    [FieldOffset(168)]
    public readonly ulong GuardAddressTakenIatEntryCount;

    [FieldOffset(176)]
    public readonly ulong GuardLongJumpTargetTable;

    [FieldOffset(184)]
    public readonly ulong GuardLongJumpTargetCount;
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal readonly struct IMAGE_LOAD_CONFIG_DIRECTORY32_V3
{
    [FieldOffset(0)]
    public readonly uint Size;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly ushort MajorVersion;

    [FieldOffset(10)]
    public readonly ushort MinorVersion;

    [FieldOffset(12)]
    public readonly uint GlobalFlagsClear;

    [FieldOffset(16)]
    public readonly uint GlobalFlagsSet;

    [FieldOffset(20)]
    public readonly uint CriticalSectionDefaultTimeout;

    [FieldOffset(24)]
    public readonly uint DeCommitFreeBlockThreshold;

    [FieldOffset(28)]
    public readonly uint DeCommitTotalFreeThreshold;

    [FieldOffset(32)]
    public readonly uint LockPrefixTable;

    [FieldOffset(36)]
    public readonly uint MaximumAllocationSize;

    [FieldOffset(40)]
    public readonly uint VirtualMemoryThershold;

    [FieldOffset(44)]
    public readonly uint ProcessAffinityMask;

    [FieldOffset(48)]
    public readonly uint ProcessHeapFlags;

    [FieldOffset(52)]
    public readonly ushort CSDVersion;

    [FieldOffset(54)]
    public readonly ushort DependentLoadFlags;

    [FieldOffset(56)]
    public readonly uint EditList;

    [FieldOffset(60)]
    public readonly uint SecurityCookie;

    [FieldOffset(64)]
    public readonly uint SEHandlerTable;

    [FieldOffset(68)]
    public readonly uint SEHandlerCount;

    [FieldOffset(72)]
    public readonly uint GuardCFCheckFunctionPointer;

    [FieldOffset(76)]
    public readonly uint GuardCFDispatchFunctionPointer;

    [FieldOffset(80)]
    public readonly uint GuardCFFunctionTable;

    [FieldOffset(84)]
    public readonly uint GuardCFFunctionCount;

    [FieldOffset(88)]
    public readonly uint GuardFlags;

    [FieldOffset(92)]
    public readonly IMAGE_LOAD_CONFIG_CODE_INTEGRITY CodeIntegrity;

    [FieldOffset(104)]
    public readonly uint GuardAddressTakenIatEntryTable;

    [FieldOffset(108)]
    public readonly uint GuardAddressTakenIatEntryCount;

    [FieldOffset(112)]
    public readonly uint GuardLongJumpTargetTable;

    [FieldOffset(116)]
    public readonly uint GuardLongJumpTargetCount;
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal readonly struct IMAGE_LOAD_CONFIG_DIRECTORY64_V4
{
    [FieldOffset(0)]
    public readonly uint Size;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly ushort MajorVersion;

    [FieldOffset(10)]
    public readonly ushort MinorVersion;

    [FieldOffset(12)]
    public readonly uint GlobalFlagsClear;

    [FieldOffset(16)]
    public readonly uint GlobalFlagsSet;

    [FieldOffset(20)]
    public readonly uint CriticalSectionDefaultTimeout;

    [FieldOffset(24)]
    public readonly ulong DeCommitFreeBlockThreshold;

    [FieldOffset(32)]
    public readonly ulong DeCommitTotalFreeThreshold;

    [FieldOffset(40)]
    public readonly ulong LockPrefixTable;

    [FieldOffset(48)]
    public readonly ulong MaximumAllocationSize;

    [FieldOffset(56)]
    public readonly ulong VirtualMemoryThershold;

    [FieldOffset(64)]
    public readonly ulong ProcessAffinityMask;

    [FieldOffset(72)]
    public readonly uint ProcessHeapFlags;

    [FieldOffset(76)]
    public readonly ushort CSDVersion;

    [FieldOffset(78)]
    public readonly ushort DependentLoadFlags;

    [FieldOffset(80)]
    public readonly ulong EditList;

    [FieldOffset(88)]
    public readonly ulong SecurityCookie;

    [FieldOffset(96)]
    public readonly ulong SEHandlerTable;

    [FieldOffset(104)]
    public readonly ulong SEHandlerCount;

    [FieldOffset(112)]
    public readonly ulong GuardCFCheckFunctionPointer;

    [FieldOffset(120)]
    public readonly ulong GuardCFDispatchFunctionPointer;

    [FieldOffset(128)]
    public readonly ulong GuardCFFunctionTable;

    [FieldOffset(136)]
    public readonly ulong GuardCFFunctionCount;

    [FieldOffset(144)]
    public readonly uint GuardFlags;

    [FieldOffset(148)]
    public readonly IMAGE_LOAD_CONFIG_CODE_INTEGRITY CodeIntegrity;

    [FieldOffset(160)]
    public readonly ulong GuardAddressTakenIatEntryTable;

    [FieldOffset(168)]
    public readonly ulong GuardAddressTakenIatEntryCount;

    [FieldOffset(176)]
    public readonly ulong GuardLongJumpTargetTable;

    [FieldOffset(184)]
    public readonly ulong GuardLongJumpTargetCount;

    [FieldOffset(192)]
    public readonly ulong DynamicValueRelocTable;

    [FieldOffset(200)]
    public readonly ulong CHPEMetadataPointer;

    [FieldOffset(208)]
    public readonly ulong GuardRFFailureRoutine;

    [FieldOffset(216)]
    public readonly ulong GuardRFFailureRoutineFunctionPointer;

    [FieldOffset(224)]
    public readonly uint DynamicValueRelocTableOffset;

    [FieldOffset(228)]
    public readonly ushort DynamicValueRelocTableSection;

    [FieldOffset(230)]
    public readonly ushort Reserved2;

    [FieldOffset(232)]
    public readonly ulong GuardRFVerifyStackPointerFunctionPointer;

    [FieldOffset(240)]
    public readonly uint HotPatchTableOffset;

    [FieldOffset(244)]
    public readonly uint Reserved3;

    [FieldOffset(248)]
    public readonly ulong EnclaveConfigurationPointer;

    [FieldOffset(256)]
    public readonly ulong VolatileMetadataPointer;

    [FieldOffset(264)]
    public readonly ulong GuardEHContinuationTable;

    [FieldOffset(272)]
    public readonly ulong GuardEHContinuationCount;
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal readonly struct IMAGE_LOAD_CONFIG_DIRECTORY32_V4
{
    [FieldOffset(0)]
    public readonly uint Size;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly ushort MajorVersion;

    [FieldOffset(10)]
    public readonly ushort MinorVersion;

    [FieldOffset(12)]
    public readonly uint GlobalFlagsClear;

    [FieldOffset(16)]
    public readonly uint GlobalFlagsSet;

    [FieldOffset(20)]
    public readonly uint CriticalSectionDefaultTimeout;

    [FieldOffset(24)]
    public readonly uint DeCommitFreeBlockThreshold;

    [FieldOffset(28)]
    public readonly uint DeCommitTotalFreeThreshold;

    [FieldOffset(32)]
    public readonly uint LockPrefixTable;

    [FieldOffset(36)]
    public readonly uint MaximumAllocationSize;

    [FieldOffset(40)]
    public readonly uint VirtualMemoryThershold;

    [FieldOffset(44)]
    public readonly uint ProcessAffinityMask;

    [FieldOffset(48)]
    public readonly uint ProcessHeapFlags;

    [FieldOffset(52)]
    public readonly ushort CSDVersion;

    [FieldOffset(54)]
    public readonly ushort DependentLoadFlags;

    [FieldOffset(56)]
    public readonly uint EditList;

    [FieldOffset(60)]
    public readonly uint SecurityCookie;

    [FieldOffset(64)]
    public readonly uint SEHandlerTable;

    [FieldOffset(68)]
    public readonly uint SEHandlerCount;

    [FieldOffset(72)]
    public readonly uint GuardCFCheckFunctionPointer;

    [FieldOffset(76)]
    public readonly uint GuardCFDispatchFunctionPointer;

    [FieldOffset(80)]
    public readonly uint GuardCFFunctionTable;

    [FieldOffset(84)]
    public readonly uint GuardCFFunctionCount;

    [FieldOffset(88)]
    public readonly uint GuardFlags;

    [FieldOffset(92)]
    public readonly IMAGE_LOAD_CONFIG_CODE_INTEGRITY CodeIntegrity;

    [FieldOffset(104)]
    public readonly uint GuardAddressTakenIatEntryTable;

    [FieldOffset(108)]
    public readonly uint GuardAddressTakenIatEntryCount;

    [FieldOffset(112)]
    public readonly uint GuardLongJumpTargetTable;

    [FieldOffset(116)]
    public readonly uint GuardLongJumpTargetCount;

    [FieldOffset(120)]
    public readonly uint DynamicValueRelocTable;

    [FieldOffset(124)]
    public readonly uint CHPEMetadataPointer;

    [FieldOffset(128)]
    public readonly uint GuardRFFailureRoutine;

    [FieldOffset(132)]
    public readonly uint GuardRFFailureRoutineFunctionPointer;

    [FieldOffset(136)]
    public readonly uint DynamicValueRelocTableOffset;

    [FieldOffset(140)]
    public readonly ushort DynamicValueRelocTableSection;

    [FieldOffset(142)]
    public readonly ushort Reserved2;

    [FieldOffset(144)]
    public readonly uint GuardRFVerifyStackPointerFunctionPointer;

    [FieldOffset(148)]
    public readonly uint HotPatchTableOffset;

    [FieldOffset(152)]
    public readonly uint Reserved3;

    [FieldOffset(156)]
    public readonly uint EnclaveConfigurationPointer;

    [FieldOffset(160)]
    public readonly uint VolatileMetadataPointer;

    [FieldOffset(164)]
    public readonly uint GuardEHContinuationTable;

    [FieldOffset(168)]
    public readonly uint GuardEHContinuationCount;
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal readonly struct IMAGE_LOAD_CONFIG_DIRECTORY64_V5
{
    [FieldOffset(0)]
    public readonly uint Size;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly ushort MajorVersion;

    [FieldOffset(10)]
    public readonly ushort MinorVersion;

    [FieldOffset(12)]
    public readonly uint GlobalFlagsClear;

    [FieldOffset(16)]
    public readonly uint GlobalFlagsSet;

    [FieldOffset(20)]
    public readonly uint CriticalSectionDefaultTimeout;

    [FieldOffset(24)]
    public readonly ulong DeCommitFreeBlockThreshold;

    [FieldOffset(32)]
    public readonly ulong DeCommitTotalFreeThreshold;

    [FieldOffset(40)]
    public readonly ulong LockPrefixTable;

    [FieldOffset(48)]
    public readonly ulong MaximumAllocationSize;

    [FieldOffset(56)]
    public readonly ulong VirtualMemoryThershold;

    [FieldOffset(64)]
    public readonly ulong ProcessAffinityMask;

    [FieldOffset(72)]
    public readonly uint ProcessHeapFlags;

    [FieldOffset(76)]
    public readonly ushort CSDVersion;

    [FieldOffset(78)]
    public readonly ushort DependentLoadFlags;

    [FieldOffset(80)]
    public readonly ulong EditList;

    [FieldOffset(88)]
    public readonly ulong SecurityCookie;

    [FieldOffset(96)]
    public readonly ulong SEHandlerTable;

    [FieldOffset(104)]
    public readonly ulong SEHandlerCount;

    [FieldOffset(112)]
    public readonly ulong GuardCFCheckFunctionPointer;

    [FieldOffset(120)]
    public readonly ulong GuardCFDispatchFunctionPointer;

    [FieldOffset(128)]
    public readonly ulong GuardCFFunctionTable;

    [FieldOffset(136)]
    public readonly ulong GuardCFFunctionCount;

    [FieldOffset(144)]
    public readonly uint GuardFlags;

    [FieldOffset(148)]
    public readonly IMAGE_LOAD_CONFIG_CODE_INTEGRITY CodeIntegrity;

    [FieldOffset(160)]
    public readonly ulong GuardAddressTakenIatEntryTable;

    [FieldOffset(168)]
    public readonly ulong GuardAddressTakenIatEntryCount;

    [FieldOffset(176)]
    public readonly ulong GuardLongJumpTargetTable;

    [FieldOffset(184)]
    public readonly ulong GuardLongJumpTargetCount;

    [FieldOffset(192)]
    public readonly ulong DynamicValueRelocTable;

    [FieldOffset(200)]
    public readonly ulong CHPEMetadataPointer;

    [FieldOffset(208)]
    public readonly ulong GuardRFFailureRoutine;

    [FieldOffset(216)]
    public readonly ulong GuardRFFailureRoutineFunctionPointer;

    [FieldOffset(224)]
    public readonly uint DynamicValueRelocTableOffset;

    [FieldOffset(228)]
    public readonly ushort DynamicValueRelocTableSection;

    [FieldOffset(230)]
    public readonly ushort Reserved2;

    [FieldOffset(232)]
    public readonly ulong GuardRFVerifyStackPointerFunctionPointer;

    [FieldOffset(240)]
    public readonly uint HotPatchTableOffset;

    [FieldOffset(244)]
    public readonly uint Reserved3;

    [FieldOffset(248)]
    public readonly ulong EnclaveConfigurationPointer;

    [FieldOffset(256)]
    public readonly ulong VolatileMetadataPointer;

    [FieldOffset(264)]
    public readonly ulong GuardEHContinuationTable;

    [FieldOffset(272)]
    public readonly ulong GuardEHContinuationCount;

    [FieldOffset(280)]
    public readonly ulong GuardXFGCheckFunctionPointer;

    [FieldOffset(288)]
    public readonly ulong GuardXFGDispatchFunctionPointer;

    [FieldOffset(296)]
    public readonly ulong GuardXFGTableDispatchFunctionPointer;

    [FieldOffset(304)]
    public readonly ulong CastGuardOsDeterminedFailureMode;
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
internal readonly struct IMAGE_LOAD_CONFIG_DIRECTORY32_V5
{
    [FieldOffset(0)]
    public readonly uint Size;

    [FieldOffset(4)]
    public readonly uint TimeDateStamp;

    [FieldOffset(8)]
    public readonly ushort MajorVersion;

    [FieldOffset(10)]
    public readonly ushort MinorVersion;

    [FieldOffset(12)]
    public readonly uint GlobalFlagsClear;

    [FieldOffset(16)]
    public readonly uint GlobalFlagsSet;

    [FieldOffset(20)]
    public readonly uint CriticalSectionDefaultTimeout;

    [FieldOffset(24)]
    public readonly uint DeCommitFreeBlockThreshold;

    [FieldOffset(28)]
    public readonly uint DeCommitTotalFreeThreshold;

    [FieldOffset(32)]
    public readonly uint LockPrefixTable;

    [FieldOffset(36)]
    public readonly uint MaximumAllocationSize;

    [FieldOffset(40)]
    public readonly uint VirtualMemoryThershold;

    [FieldOffset(44)]
    public readonly uint ProcessAffinityMask;

    [FieldOffset(48)]
    public readonly uint ProcessHeapFlags;

    [FieldOffset(52)]
    public readonly ushort CSDVersion;

    [FieldOffset(54)]
    public readonly ushort DependentLoadFlags;

    [FieldOffset(56)]
    public readonly uint EditList;

    [FieldOffset(60)]
    public readonly uint SecurityCookie;

    [FieldOffset(64)]
    public readonly uint SEHandlerTable;

    [FieldOffset(68)]
    public readonly uint SEHandlerCount;

    [FieldOffset(72)]
    public readonly uint GuardCFCheckFunctionPointer;

    [FieldOffset(76)]
    public readonly uint GuardCFDispatchFunctionPointer;

    [FieldOffset(80)]
    public readonly uint GuardCFFunctionTable;

    [FieldOffset(84)]
    public readonly uint GuardCFFunctionCount;

    [FieldOffset(88)]
    public readonly uint GuardFlags;

    [FieldOffset(92)]
    public readonly IMAGE_LOAD_CONFIG_CODE_INTEGRITY CodeIntegrity;

    [FieldOffset(104)]
    public readonly uint GuardAddressTakenIatEntryTable;

    [FieldOffset(108)]
    public readonly uint GuardAddressTakenIatEntryCount;

    [FieldOffset(112)]
    public readonly uint GuardLongJumpTargetTable;

    [FieldOffset(116)]
    public readonly uint GuardLongJumpTargetCount;

    [FieldOffset(120)]
    public readonly uint DynamicValueRelocTable;

    [FieldOffset(124)]
    public readonly uint CHPEMetadataPointer;

    [FieldOffset(128)]
    public readonly uint GuardRFFailureRoutine;

    [FieldOffset(132)]
    public readonly uint GuardRFFailureRoutineFunctionPointer;

    [FieldOffset(136)]
    public readonly uint DynamicValueRelocTableOffset;

    [FieldOffset(140)]
    public readonly ushort DynamicValueRelocTableSection;

    [FieldOffset(142)]
    public readonly ushort Reserved2;

    [FieldOffset(144)]
    public readonly uint GuardRFVerifyStackPointerFunctionPointer;

    [FieldOffset(148)]
    public readonly uint HotPatchTableOffset;

    [FieldOffset(152)]
    public readonly uint Reserved3;

    [FieldOffset(156)]
    public readonly uint EnclaveConfigurationPointer;

    [FieldOffset(160)]
    public readonly uint VolatileMetadataPointer;

    [FieldOffset(164)]
    public readonly uint GuardEHContinuationTable;

    [FieldOffset(168)]
    public readonly uint GuardEHContinuationCount;

    [FieldOffset(172)]
    public readonly uint GuardXFGCheckFunctionPointer;

    [FieldOffset(176)]
    public readonly uint GuardXFGDispatchFunctionPointer;

    [FieldOffset(180)]
    public readonly uint GuardXFGTableDispatchFunctionPointer;

    [FieldOffset(184)]
    public readonly uint CastGuardOsDeterminedFailureMode;
}

internal static partial class PInvokes
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
    [LibraryImport("Kernelbase.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr LoadLibraryExW([MarshalAs(UnmanagedType.LPWStr)] string lpFileName, IntPtr reserved, LoadLibraryFlags flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool FreeLibrary(IntPtr hModule);

    #endregion

    #region DbgHelp - note we use the copy in SizeBench from DbgX, NOT the OS version

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(@"amd64\dbghelp.dll", EntryPoint = "ImageNtHeader")]
    [SuppressUnmanagedCodeSecurity]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    internal static unsafe partial void* ImageNtHeader(void* Base);

    #endregion

    #region MSVCRT

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("msvcrt.dll", EntryPoint = "memcpy", SetLastError = false)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial IntPtr memcpy(IntPtr dest, IntPtr src, UIntPtr count);

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
    [LibraryImport("imagehlp.dll", EntryPoint = "MapFileAndCheckSumW")]
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    internal static partial MapFileAndCheckSumWResult MapFileAndCheckSumW([MarshalAs(UnmanagedType.LPWStr)] string filename,
                                                                         [MarshalAs(UnmanagedType.U4)] out uint originalHeaderChecksum,
                                                                         [MarshalAs(UnmanagedType.U4)] out uint newChecksum);

    #endregion

}
