using SizeBench.AnalysisEngine.PE;

namespace SizeBench.AnalysisEngine.Symbols;

internal readonly struct SeparatedIpToStateMapEntry
{
    internal readonly uint RVAOfBlock;
    internal readonly uint RVAOfIpToStateMap;

    internal SeparatedIpToStateMapEntry(uint rvaOfBlock, uint rvaOfIpToStateMap)
    {
        this.RVAOfBlock = rvaOfBlock;
        this.RVAOfIpToStateMap = rvaOfIpToStateMap;
    }
}

internal sealed class SeparatedIpToStateMapSymbol : XDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.SeparatedIpToStateMap;

    internal SeparatedIpToStateMapEntry[] Entries { get; }

    internal SeparatedIpToStateMapSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, EHSymbolParser.SepIPToStateMap4 sepMap4, SymbolSourcesSupported symbolSourcesSupported)
        : base(targetSymbol, targetStartRVA, rva, sepMap4.Size, symbolSourcesSupported)
    {
        this.Entries = new SeparatedIpToStateMapEntry[sepMap4.Entries.Length];
        for (var i = 0; i < sepMap4.Entries.Length; i++)
        {
            this.Entries[i] = new SeparatedIpToStateMapEntry((uint)sepMap4.Entries[i].addrStartRVA, (uint)sepMap4.Entries[i].dispOfIPMap);
        }
    }

    internal override string SymbolPrefix => "[seg2ip2state]";
}
