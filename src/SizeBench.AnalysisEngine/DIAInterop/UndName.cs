namespace SizeBench.AnalysisEngine.DIAInterop;

#pragma warning disable CA1712 // Do not prefix enum values with type name - these are the publicly documented names
// See here: https://learn.microsoft.com/en-us/visualstudio/debugger/debug-interface-access/idiasymbol-get-undecoratednameex?view=vs-2022
[Flags]
internal enum UndName : uint
{
    UNDNAME_COMPLETE = 0x0000,  // Enable full undecoration
    UNDNAME_NO_LEADING_UNDERSCORES = 0x0001,  // Remove leading underscores from MS extended keywords
    UNDNAME_NO_MS_KEYWORDS = 0x0002,  // Disable expansion of MS extended keywords
    UNDNAME_NO_FUNCTION_RETURNS = 0x0004,  // Disable expansion of return type for primary declaration
    UNDNAME_NO_ALLOCATION_MODEL = 0x0008,  // Disable expansion of the declaration model
    UNDNAME_NO_ALLOCATION_LANGUAGE = 0x0010,  // Disable expansion of the declaration language specifier
    UNDNAME_NO_THISTYPE = 0x0060,  // Disables all modifiers on the 'this' type
    UNDNAME_NO_ACCESS_SPECIFIERS = 0x0080,  // Disable expansion of access specifiers for members
    UNDNAME_NO_THROW_SIGNATURES = 0x0100,  // Disable expansion of 'throw-signatures' for functions and pointers to functions
    UNDNAME_NO_MEMBER_TYPE = 0x0200,  // Disable expansion of 'static' or 'virtual'ness of members
    UNDNAME_NO_RETURN_UDT_MODEL = 0x0400,  // Disable expansion of MS model for UDT returns
    UNDNAME_32_BIT_DECODE = 0x800,  // Undecorate 32-bit decorated names
    UNDNAME_NAME_ONLY = 0x1000,  // Gets only the name for primary declaration; returns just [scope::]name. Expands template params.
    UNDNAME_TYPE_ONLY = 0x2000,  // Input is just a type encoding; composes and abstract declarator
    UNDNAME_HAVE_PARAMETERS = 0x4000,  // The real template parameters are available
    UNDNAME_NO_ECSU = 0x08000,  // Suppresses enum/class/struct/union
    UNDNAME_NO_IDENT_CHAR_CHECK = 0x010000,   // Don't check for valid identifier characters
    UNDNAME_NO_PTR64 = 0x20000  // Does not include __ptr64 in output
}
#pragma warning restore CA1712 // Do not prefix enum values with type name
