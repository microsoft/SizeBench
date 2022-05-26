namespace SizeBench.AnalysisEngine.Symbols;

internal sealed class StateUnwindMapSymbol : XDataSymbol
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.StateUnwindMap;

    internal StateUnwindMapSymbol(Symbol? targetSymbol, uint targetStartRVA, uint rva, uint size)
        : base(targetSymbol, targetStartRVA, rva, size)
    {
    }

    internal override string SymbolPrefix => "[stateUnwindMap]";
}
