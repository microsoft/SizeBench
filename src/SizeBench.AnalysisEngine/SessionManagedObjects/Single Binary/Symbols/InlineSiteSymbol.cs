using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

[DebuggerDisplay("Inline Site Name={Name}, Inlined Into={BlockInlinedInto.Name} (canonical symbol: {CanonicalSymbolInlinedInto.Name}), Size={Size}")]
public sealed class InlineSiteSymbol
{
    public CodeBlockSymbol BlockInlinedInto { get; }
    public ISymbol CanonicalSymbolInlinedInto { get; }
    public string Name { get; }
    public uint Size { get; }
    public IEnumerable<RVARange> RVARanges { get; }

    internal uint SymIndexId { get; }

    internal InlineSiteSymbol(SessionDataCache cache,
                              string name,
                              uint symIndexId,
                              CodeBlockSymbol blockInlinedInto,
                              ISymbol canonicalSymbolInlinedInto,
                              RVARangeSet rvaRanges)
    {
#if DEBUG
        Debug.Assert(cache.SymbolSourcesSupported.HasFlag(SymbolSourcesSupported.Code));

        if (cache.AllInlineSiteSymbolsBySymIndexId.ContainsKey(symIndexId))
        {
            throw new ObjectAlreadyExistsException();
        }
#endif

        this.Name = name;
        this.RVARanges = rvaRanges;
        this.SymIndexId = symIndexId;
        this.BlockInlinedInto = blockInlinedInto;
        this.CanonicalSymbolInlinedInto = canonicalSymbolInlinedInto;
        this.Size = 0;
        foreach (var range in rvaRanges)
        {
            this.Size += range.Size;
        }

        cache.AllInlineSiteSymbolsBySymIndexId.Add(symIndexId, this);
    }
}
