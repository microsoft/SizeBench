using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class ThunkSymbol : Symbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.Thunk;

    internal override bool CanBeFolded => true;

    public ThunkSymbol(SessionDataCache cache,
                       string name,
                       uint rva,
                       uint size,
                       uint symIndexId) : base(cache, FixupSymbolName(name), rva, size, isVirtualSize: false, symIndexId: symIndexId)
    {
        // Should this have a targetRVA?  Probably yes!
        // Also, consider having these symbols all start with "[thunk]" in their names (they don't now, in a lot of cases...)

        Debug.Assert(cache.SymbolSourcesSupported.HasFlag(SymbolSourcesSupported.Code));
    }

    private static string FixupSymbolName(string name)
        => String.IsNullOrEmpty(name) ? "[thunk]" : name;
}
