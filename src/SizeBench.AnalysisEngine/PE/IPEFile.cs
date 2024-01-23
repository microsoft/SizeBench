using System.Reflection.PortableExecutable;
using SizeBench.AnalysisEngine.Symbols;

namespace SizeBench.AnalysisEngine.PE;

public interface IPEFile : IDisposable
{
    PEFileDebugSignature DebugSignature { get; }
    MachineType MachineType { get; }
    IReadOnlyList<PEDirectorySymbol> PEDirectorySymbols { get; }
    PEReader PEReader { get; }
    IEnumerable<RVARange> DelayLoadImportThunksRVARanges { get; }
    IEnumerable<RVARange> DelayLoadImportStringsRVARanges { get; }
    IEnumerable<RVARange> DelayLoadModuleHandlesRVARanges { get; }
    ISymbol? GFIDSTable { get; }
    ISymbol? GIATSTable { get; }
}
