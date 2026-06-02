using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class RsrcStringSymbol : RsrcSymbolBase
{
    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay => $"{GetType().Name}: \"{this.String}\", ({this.Size} bytes)";

    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.RsrcString;

    public string String { get; }

    internal RsrcStringSymbol(uint rva, uint size, string str, SymbolSourcesSupported symbolSourcesSupported)
        : base(rva, size, $"[rsrc string] \"{str}\"", symbolSourcesSupported)
    {
        this.String = str;
    }

    public override bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
        => otherSymbol is RsrcStringSymbol &&
           base.IsVeryLikelyTheSameAs(otherSymbol);
}
