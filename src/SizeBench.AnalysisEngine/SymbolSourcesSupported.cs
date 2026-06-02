namespace SizeBench.AnalysisEngine;

[Flags]
#pragma warning disable CA1028 // Enum Storage should be Int32 - I want all 32 bits for flags for future expansion
public enum SymbolSourcesSupported : uint
#pragma warning restore CA1028 // Enum Storage should be Int32
{
    None = 0,

    /// <summary>
    /// Code like functions, separated code blocks, thunks, etc.
    /// </summary>
    Code =  0x1,

    /// <summary>
    /// Things like strings (`string') or static data (arrays, constexpr things, etc.)
    /// </summary>
    DataSymbols =  0x2,

    /// <summary>
    /// Procedure data (PDATA) typically in the .pdata section
    /// </summary>
    PDATA = 0x4,

    /// <summary>
    /// Exception unwinding data (XDATA) typically in the .xdata COFF Group
    /// </summary>
    XDATA = 0x8,

    /// <summary>
    /// Win32 resources typically in the .rsrc section
    /// </summary>
    RSRC = 0x10,

    /// <summary>
    /// Other symbols parsed from the PE file directly like debug directories, load config table, image imports, delay load imports, etc.
    /// </summary>
    OtherPESymbols = 0x20,

    /// <summary>
    /// All types of symbols
    /// </summary>
    All = Code | DataSymbols | PDATA | XDATA | RSRC | OtherPESymbols
}
