using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class EnumerateInlineSitesInFunctionSessionTask : SessionTask<IReadOnlyList<InlineSiteSymbol>>
{
    private readonly SessionTaskParameters _sessionTaskParameters;
    private readonly IFunctionCodeSymbol _functionSymbol;

    public EnumerateInlineSitesInFunctionSessionTask(SessionTaskParameters parameters,
                                                     IFunctionCodeSymbol functionSymbol,
                                                     IProgress<SessionTaskProgress>? progressReporter,
                                                     CancellationToken token)
        : base(parameters, progressReporter, token)
    {
        this.TaskName = $"Enumerate All Inline Sites within {functionSymbol.FunctionName}";
        this._functionSymbol = functionSymbol;
        this._sessionTaskParameters = parameters;
    }

    protected override IReadOnlyList<InlineSiteSymbol> ExecuteCore(ILogger logger)
    {
        ReportProgress($"Discovering all inline sites within {this._functionSymbol.FunctionName}", 0, null);

        List<InlineSiteSymbol>? allInlineSites = null;

        foreach (var block in this._functionSymbol.Blocks)
        {
            var inlinesForBlock = this._sessionTaskParameters.DIAAdapter.FindAllInlineSitesForBlock(block, this.CancellationToken);
            if (inlinesForBlock is not null)
            {
                allInlineSites ??= new List<InlineSiteSymbol>();
                allInlineSites.AddRange(inlinesForBlock);
            }
        }

        return allInlineSites ?? (IReadOnlyList<InlineSiteSymbol>)Array.Empty<InlineSiteSymbol>();
    }
}
