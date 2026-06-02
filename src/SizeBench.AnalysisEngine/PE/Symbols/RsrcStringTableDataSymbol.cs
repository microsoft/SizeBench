using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class RsrcStringTableDataSymbol : RsrcDataSymbol
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"{GetType().Name}: {this.Name} ({this.Size} bytes)";

    public IReadOnlyList<string> Strings { get; }
    internal RsrcStringTableDataSymbol(uint rva, uint size, string language, string dataName, List<string> strings, SymbolSourcesSupported symbolSourcesSupported)
        : base(rva, size, language, Win32ResourceType.STRINGTABLE, "STRINGTABLE", dataName, "", symbolSourcesSupported)
    {
        this.Strings = strings;
    }

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        return otherSymbol is RsrcStringTableDataSymbol &&
               base.IsVeryLikelyTheSameAs(otherSymbol);
    }
}
