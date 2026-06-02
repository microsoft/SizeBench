using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class RsrcGroupCursorDataSymbol : RsrcDataSymbol
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"{GetType().Name}: {this.Name} ({this.Size} bytes)";

    public IReadOnlyList<RsrcCursorDataSymbol> Cursors { get; }
    internal RsrcGroupCursorDataSymbol(uint size, string language, string dataName, List<RsrcCursorDataSymbol> cursors, SymbolSourcesSupported symbolSourcesSupported)
        : base(cursors[0].RVA, size, language, Win32ResourceType.GROUP_CURSOR, "GROUP_CURSOR", dataName, "", symbolSourcesSupported)
    {
        this.Cursors = cursors;
    }

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        return otherSymbol is RsrcGroupCursorDataSymbol &&
               base.IsVeryLikelyTheSameAs(otherSymbol);
    }
}
