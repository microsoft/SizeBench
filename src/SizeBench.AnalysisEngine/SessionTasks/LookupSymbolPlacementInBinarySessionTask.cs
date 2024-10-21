using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class LookupSymbolPlacementInBinarySessionTask : SessionTask<SymbolPlacement>
{
    private readonly SessionTaskParameters _sessionTaskParameters;
    private readonly ISymbol _symbol;
    private readonly bool _shouldLookupSectionAndCOFFGroup;
    private readonly bool _shouldLookupLibAndCompiland;
    private readonly bool _shouldLookupSourceFile;

    public LookupSymbolPlacementInBinarySessionTask(ISymbol symbol,
                                                    LookupSymbolPlacementOptions? options,
                                                    SessionTaskParameters parameters,
                                                    CancellationToken token,
                                                    IProgress<SessionTaskProgress>? progress)
        : base(parameters, progress, token)
    {
        this.TaskName = $"Lookup placement in the binary of symbol: {symbol.Name}";
        this._sessionTaskParameters = parameters;
        this._symbol = symbol;
        this._shouldLookupSectionAndCOFFGroup = options?.IncludeBinarySectionAndCOFFGroup ?? true;
        this._shouldLookupLibAndCompiland = options?.IncludeLibAndCompiland ?? true;
        this._shouldLookupSourceFile = options?.IncludeSourceFile ?? true;
    }

    protected override SymbolPlacement ExecuteCore(ILogger logger)
    {
        const int numSteps = 4;
        var lookupStepsCompleted = 0u;
        ReportProgress("Finding binary sections and COFF groups in the binary to find symbol's location", lookupStepsCompleted, numSteps);
        List<BinarySection>? binarySections = null;
        List<COFFGroup>? coffGroups = null;
        if (this._shouldLookupSectionAndCOFFGroup)
        {
            binarySections = new EnumerateBinarySectionsAndCOFFGroupsSessionTask(this._sessionTaskParameters, this.CancellationToken).Execute(logger);
            coffGroups = binarySections.SelectMany(bs => bs.COFFGroups).ToList();
        }
        lookupStepsCompleted++;

        ReportProgress("Finding libs and compilands in the binary to find symbol's location", lookupStepsCompleted, numSteps);
        HashSet<Library>? libs = null;
        List<Compiland>? compilands = null;
        if (this._shouldLookupLibAndCompiland)
        {
            libs = new EnumerateLibsAndCompilandsSessionTask(this._sessionTaskParameters, this.CancellationToken, this.ProgressReporter).Execute(logger);
            compilands = libs.SelectMany(l => l.Compilands.Values).ToList();
        }
        lookupStepsCompleted++;

        ReportProgress("Finding source files in the binary to find symbol's location", lookupStepsCompleted, numSteps);
        List<SourceFile>? sourceFiles = null;
        if (this._shouldLookupSourceFile)
        {
            sourceFiles = new EnumerateSourceFilesSessionTask(this._sessionTaskParameters, this.CancellationToken, this.ProgressReporter).Execute(logger);
        }
        lookupStepsCompleted++;

        ReportProgress("Finding this symbol's precise location", lookupStepsCompleted, numSteps);
        var coffGroup = this._shouldLookupSectionAndCOFFGroup ? (from cg in coffGroups
                         where this._symbol.RVA >= cg.RVA &&
                               this._symbol.RVAEnd <= (cg.RVA + cg.VirtualSize)
                         select cg).FirstOrDefault() : null;
        var section = coffGroup?.Section;

        var compiland = this._shouldLookupLibAndCompiland ? (from c in compilands
                         where c.Contains(this._symbol.RVA, this._symbol.VirtualSize)
                         select c).FirstOrDefault() : null;
        var lib = compiland?.Lib;

        var sourceFile = this._shouldLookupSourceFile ? (from sf in sourceFiles
                          where sf.Contains(this._symbol.RVA, this._symbol.VirtualSize)
                          select sf).FirstOrDefault() : null;

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
