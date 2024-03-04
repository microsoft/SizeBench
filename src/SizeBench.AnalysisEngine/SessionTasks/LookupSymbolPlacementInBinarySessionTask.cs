using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class LookupSymbolPlacementInBinarySessionTask : SessionTask<SymbolPlacement>
{
    private readonly SessionTaskParameters _sessionTaskParameters;
    private readonly ISymbol _symbol;

    public LookupSymbolPlacementInBinarySessionTask(ISymbol symbol,
                                                    SessionTaskParameters parameters,
                                                    CancellationToken token,
                                                    IProgress<SessionTaskProgress>? progress)
        : base(parameters, progress, token)
    {
        this.TaskName = $"Lookup placement in the binary of symbol: {symbol.Name}";
        this._sessionTaskParameters = parameters;
        this._symbol = symbol;
    }

    protected override SymbolPlacement ExecuteCore(ILogger logger)
    {
        const int numSteps = 4;
        var lookupStepsCompleted = 0u;
        ReportProgress("Finding binary sections and COFF groups in the binary to find symbol's location", lookupStepsCompleted, numSteps);
        var binarySections = new EnumerateBinarySectionsAndCOFFGroupsSessionTask(this._sessionTaskParameters, this.CancellationToken).Execute(logger);
        var coffGroups = binarySections.SelectMany(bs => bs.COFFGroups).ToList();
        lookupStepsCompleted++;

        ReportProgress("Finding libs and compilands in the binary to find symbol's location", lookupStepsCompleted, numSteps);
        var libs = new EnumerateLibsAndCompilandsSessionTask(this._sessionTaskParameters, this.CancellationToken, this.ProgressReporter).Execute(logger);
        var compilands = libs.SelectMany(l => l.Compilands.Values).ToList();
        lookupStepsCompleted++;

        ReportProgress("Finding source files in the binary to find symbol's location", lookupStepsCompleted, numSteps);
        var sourceFiles = new EnumerateSourceFilesSessionTask(this._sessionTaskParameters, this.CancellationToken, this.ProgressReporter).Execute(logger);
        lookupStepsCompleted++;

        ReportProgress("Finding this symbol's precise location", lookupStepsCompleted, numSteps);
        var coffGroup = (from cg in coffGroups
                         where this._symbol.RVA >= cg.RVA &&
                               this._symbol.RVAEnd <= (cg.RVA + cg.VirtualSize)
                         select cg).FirstOrDefault();
        var section = coffGroup?.Section;

        var compiland = (from c in compilands
                         where c.Contains(this._symbol.RVA, this._symbol.VirtualSize)
                         select c).FirstOrDefault();
        var lib = compiland?.Lib;

        var sourceFile = (from sf in sourceFiles
                          where sf.Contains(this._symbol.RVA, this._symbol.VirtualSize)
                          select sf).FirstOrDefault();

        var placement = new SymbolPlacement(section, coffGroup, lib, compiland, sourceFile);
        lookupStepsCompleted++;

        System.Diagnostics.Debug.Assert(lookupStepsCompleted == numSteps);
        ReportProgress($"Found symbol's location", lookupStepsCompleted, numSteps);
        logger.Log($"Finished finding the location of '{this._symbol.Name}' at RVA range 0x{this._symbol.RVA:X}-0x{this._symbol.RVAEnd:X} in the binary.  " +
                   $"It is located in section {section?.Name ?? "null"}, COFF Group {coffGroup?.Name ?? "null"}, lib '{lib?.Name ?? "null"}', " +
                   $"compiland '{compiland?.Name ?? "null"}', and source file '{sourceFile?.Name ?? "null"}'");

        return placement;
    }
}
