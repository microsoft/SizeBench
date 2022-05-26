using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.TestDataCommon;

public class TestSymbol : ISymbol
{
    public string Name { get; }

    public SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.Unknown;

    public uint RVA { get; }

    public uint RVAEnd { get; }

    public uint Size { get; }

    public uint VirtualSize { get; }

    public bool IsCOMDATFolded => false;

    public TestSymbol(string name, uint rvaStart, uint size, uint virtualSize)
    {
        this.Name = name;
        this.RVA = rvaStart;
        this.RVAEnd = rvaStart + virtualSize - 1;
        this.Size = size;
        this.VirtualSize = virtualSize;
    }

    public bool IsVeryLikelyTheSameAs(ISymbol otherSymbol)
    {
        throw new NotImplementedException();
    }
}
