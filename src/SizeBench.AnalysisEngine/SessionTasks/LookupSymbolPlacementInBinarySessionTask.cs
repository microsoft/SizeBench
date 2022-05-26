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
        var binarySections = new EnumerateBinarySectionsAndCOFFGroupsSessionTask(this._sessionTaskParameters, this.CancellationToken).Execute(logger);
        var coffGroups = binarySections.SelectMany(bs => bs.COFFGroups).ToList();

        var libs = new EnumerateLibsAndCompilandsSessionTask(this._sessionTaskParameters, this.CancellationToken, this.ProgressReporter).Execute(logger);
        var compilands = libs.SelectMany(l => l.Compilands.Values).ToList();

        var sourceFiles = new EnumerateSourceFilesSessionTask(this._sessionTaskParameters, this.CancellationToken, this.ProgressReporter).Execute(logger);

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

        logger.Log($"Finished finding the location of '{this._symbol.Name}' at RVA range 0x{this._symbol.RVA:X}-0x{this._symbol.RVAEnd:X} in the binary.  " +
                   $"It is located in section {section?.Name ?? "null"}, COFF Group {coffGroup?.Name ?? "null"}, lib '{lib?.Name ?? "null"}', " +
                   $"compiland '{compiland?.Name ?? "null"}', and source file '{sourceFile?.Name ?? "null"}'");

        return placement;
    }
}
