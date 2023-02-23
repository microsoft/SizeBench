namespace SizeBench.AnalysisEngine.DIAInterop;

internal enum CompilandLanguage
{
    CV_CFL_C = 0x00,
    CV_CFL_CXX = 0x01,
    CV_CFL_FORTRAN = 0x02,
    CV_CFL_MASM = 0x03,
    CV_CFL_PASCAL = 0x04,
    CV_CFL_BASIC = 0x05,
    CV_CFL_COBOL = 0x06,
    CV_CFL_LINK = 0x07,
    CV_CFL_CVTRES = 0x08,
    CV_CFL_CVTPGD = 0x09,
    CV_CFL_CSHARP = 0x0A,  // C#
    CV_CFL_VB = 0x0B,  // Visual Basic
    CV_CFL_ILASM = 0x0C,  // IL (as in CLR) ASM
    CV_CFL_JAVA = 0x0D,
    CV_CFL_JSCRIPT = 0x0E,
    CV_CFL_MSIL = 0x0F,  // Unknown MSIL (LTCG of .NETMODULE)
    CV_CFL_HLSL = 0x10,  // High Level Shader Language
    CV_CFL_OBJC = 0x11,  // Objective-C
    CV_CFL_OBJCXX = 0x12,  // Objective-C++
    CV_CFL_SWIFT = 0x13,  // Swift
    CV_CFL_ALIASOBJ = 0x14,
    CV_CFL_RUST = 0x15,  // Rust
    Unknown = Int32.MaxValue
}
