﻿using System.Diagnostics;

namespace SizeBench.AnalysisEngine.Symbols;

internal class PDataSymbol : EHSymbolBase
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.PData;

    internal uint UnwindInfoStartRva { get; }

    internal PDataSymbol(uint targetStartRVA, uint unwindInfoStartRVA, uint rva, uint size, SymbolSourcesSupported symbolSourcesSupported)
        : base(targetStartRVA, rva, size)
    {
        Debug.Assert(symbolSourcesSupported.HasFlag(SymbolSourcesSupported.PDATA));
        this.UnwindInfoStartRva = unwindInfoStartRVA;
    }

    internal override string SymbolPrefix => "[pdata]";
}
