using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class RsrcGroupIconDataSymbol : RsrcDataSymbol
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"{GetType().Name}: {this.Name} ({this.Size} bytes)";

    public IReadOnlyList<RsrcIconDataSymbol> Icons { get; }
    internal RsrcGroupIconDataSymbol(uint size, string language, string dataName, List<RsrcIconDataSymbol> icons, SymbolSourcesSupported symbolSourcesSupported)
        : base(icons[0].RVA, size, language, Win32ResourceType.GROUP_ICON, "GROUP_ICON", dataName, "", symbolSourcesSupported)
    {
        this.Icons = icons;
    }

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        return otherSymbol is RsrcGroupIconDataSymbol &&
               base.IsVeryLikelyTheSameAs(otherSymbol);
    }
}
