using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal class LoadSymbolByRVASessionTask : SessionTask<ISymbol?>
{
    private readonly uint RVA;

    public LoadSymbolByRVASessionTask(SessionTaskParameters parameters,
                                      uint rva,
                                      IProgress<SessionTaskProgress>? progress,
                                      CancellationToken token)
                                      : base(parameters, progress, token)
    {
        this.TaskName = $"Load Symbol at RVA 0x{rva:X}";
        this.RVA = rva;
    }

    protected override ISymbol? ExecuteCore(ILogger logger)
    {
        this.CancellationToken.ThrowIfCancellationRequested();

        if (this.DataCache.PDataHasBeenInitialized == false ||
            this.DataCache.XDataHasBeenInitialized == false ||
            this.DataCache.RsrcHasBeenInitialized == false ||
            this.DataCache.OtherPESymbolsHaveBeenInitialized == false)
        {
            throw new InvalidOperationException("It is not valid to attempt to load a symbol by RVA before the PE symbols have been parsed, as that is necessary to ensure all types of symbols are found.  This is a bug in SizeBench's implementation, not your usage of it.");
        }

        // First get all the symbols out of the PDATA table that we can find, since DIA won't enumerate those.
        if (this.DataCache.PDataSymbolsByRVA.TryGetValue(this.RVA, out var pdataSymbol))
        {
            return pdataSymbol;
        }
        else if (this.DataCache.XDataSymbolsByRVA.TryGetValue(this.RVA, out var xdataSymbol))
        {
            return xdataSymbol;
        }
        else if (this.DataCache.RsrcSymbolsByRVA.TryGetValue(this.RVA, out var rsrcSymbol))
        {
            return rsrcSymbol;
        }
        else if (this.DataCache.OtherPESymbolsByRVA.TryGetValue(this.RVA, out var otherPESymbol))
        {
            return otherPESymbol;
        }

        var symbolFromDIA = this.DIAAdapter.FindSymbolByRVA(this.RVA, allowFindingNearest: false, this.CancellationToken);

        if (symbolFromDIA is null)
        {
            logger.Log("No symbol found at that RVA");
        }

        return symbolFromDIA;
    }
}
