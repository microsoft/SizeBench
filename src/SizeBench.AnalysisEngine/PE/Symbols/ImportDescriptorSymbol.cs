using System.Runtime.InteropServices;
using SizeBench.AnalysisEngine.PE;

namespace SizeBench.AnalysisEngine.Symbols;

// Represents an IMAGE_IMPORT_DESCRIPTOR entry
internal sealed class ImportDescriptorSymbol : ImportSymbolBase
{
    public override SymbolComparisonClass SymbolComparisonClass => SymbolComparisonClass.ImportDescriptor;

    internal ImportDescriptorSymbol(uint rva, string importName)
        : base(rva, (uint)Marshal.SizeOf<IMAGE_IMPORT_DESCRIPTOR>(), $"[import descriptor] {importName}")
    { }
}
