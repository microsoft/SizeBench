using System.ComponentModel.DataAnnotations;

namespace SizeBench.AnalysisEngine;

// It's important that these values match exactly with CompilandLanguage internal enum from DIA.  We cast between them.
public enum ToolLanguage
{
    C = 0x00,

    [Display(Name = "C++")]
    CPlusPlus = 0x01,

    Fortran = 0x02,
    MASM = 0x03,
    Pascal = 0x04,
    BASIC = 0x05,
    COBOL = 0x06,
    Linker = 0x07,
    CVTRES = 0x08,
    CVTPGD = 0x09,

    [Display(Name = "C#")]
    CSharp = 0x0A,  // C#

    VisualBasic = 0x0B,  // Visual Basic
    ILASM = 0x0C,
    Java = 0x0D,
    JScript = 0x0E,
    MSIL = 0x0F,  // Unknown MSIL (LTCG of .NETMODULE)
    HLSL = 0x10,  // High Level Shader Language

    [Display(Name = "Objective C")]
    ObjectiveC = 0x11,  // Objective-C

    [Display(Name = "Objective C++")]
    ObjectiveCPlusPlus = 0x12,  // Objective-C++

    Swift = 0x13,  // Swift

    ALIASOBJ = 0x14,

    Rust = 0x15, // Rust

    Zig = Int32.MaxValue - 1, // Zig, which does not yet have a CV_CFL_LANG value so SizeBench detects it using tool name
    Unknown = Int32.MaxValue
}
