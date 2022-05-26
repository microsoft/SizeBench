using SizeBench.AnalysisEngine.DIAInterop;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.PE;

internal static class EHSymbolTable
{
    internal static unsafe void Parse(byte* libraryBaseAddress, uint sectionAlignment, SessionDataCache dataCache, IDIAAdapter diaAdapter, MachineType machine, RVARange? XDataRVARange, ILogger logger)
    {
        EHSymbolParser ehParser;
        switch (machine)
        {
            case MachineType.x64:
                ehParser = new AMD64_EHParser(diaAdapter,
                                              libraryBaseAddress,
                                              machine);
                break;
            case MachineType.ARM:
            case MachineType.ARM64:
                ehParser = new ARM_EHParser(diaAdapter,
                                            libraryBaseAddress,
                                            machine);
                break;
            case MachineType.I386:
                // x86 Exception Handling (EH) does not have pdata and xdata structures the way they exist in other architectures
                dataCache.PDataRVARange = new RVARange(0, 0);
                dataCache.PDataSymbolsByRVA = new SortedList<uint, PDataSymbol>();
                dataCache.XDataRVARanges = new RVARangeSet();
                dataCache.XDataSymbolsByRVA = new SortedList<uint, XDataSymbol>();
                return;
            default:
                throw new ArgumentException($"Unknown machine type to parse xdata for ({machine}).  This is a bug in SizeBench's implementation, not your use of it.", nameof(machine));
        }

        ehParser.Parse(sectionAlignment, XDataRVARange, dataCache, logger);
    }

    internal static uint GetAdjustedRva(uint rva, MachineType machine)
    {
        if (machine == MachineType.ARM)
        {
            // Mask the lowest bit off from the value because for ARM32 Thumb2 LSB set to 1 in the address. This means the target is in thumb code instead of ARM code.
            return rva & 0xFFFFFFFE;
        }
        else
        {
            return rva;
        }
    }
}
