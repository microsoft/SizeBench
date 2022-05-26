using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine;

public sealed class DisassembleFunctionOptions
{
    /// <summary>
    /// If you're disassembling Foo::Bar, and this string is set to "---" then this will turn strings like this:
    ///    Foo::Bar+0x23
    /// Into this form:
    ///    ---+0x23
    /// This can be useful to remove noise when diffing disassembly, especially when "re-templatizing" a name
    /// like the Template Foldability analysis does.
    /// </summary>
    public string? ReplaceFunctionNameWith { get; set; }

    /// <summary>
    /// When using the ReplaceFunctionNameWith option, this is the list of names to replace.  Because multiple
    /// functions can be COMDAT folded together, and the debugger might end up picking any one of them for its
    /// disassembly output, this should be all the functions that share an RVA.
    /// </summary>
    public IList<IFunctionCodeSymbol> FunctionsThatShareAnRVAWithDisassembledFunction { get; } = new List<IFunctionCodeSymbol>();

    /// <summary>
    /// If you're disassembling Foo::Bar, then this will turn strings like this:
    ///    Foo::Bar+0x23 (12345)
    /// Into this form:
    ///    Foo::Bar+0x23
    /// This can be useful if you don't care about absolute addresses (such as when diffing), and you
    /// just want to know "it jumped within this function by X bytes".
    /// </summary>
    public bool StripAbsoluteAddressForFunctionLocalReferences { get; set; }
}
