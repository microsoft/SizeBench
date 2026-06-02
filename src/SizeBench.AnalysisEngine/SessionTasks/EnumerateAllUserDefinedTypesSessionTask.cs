using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal sealed class EnumerateAllUserDefinedTypesSessionTask : SessionTask<List<UserDefinedTypeSymbol>>
{
    public EnumerateAllUserDefinedTypesSessionTask(SessionTaskParameters parameters,
                                                   CancellationToken token,
                                                   IProgress<SessionTaskProgress>? progressReporter)
        : base(parameters, progressReporter, token)
    {
        this.TaskName = "Enumerate All User-Defined Types";
    }

    protected override List<UserDefinedTypeSymbol> ExecuteCore(ILogger logger)
    {
        ReportProgress("Discovering all user-defined types in the binary", 0, null);

        var udts = this.DIAAdapter.FindAllUserDefinedTypes(logger, this.CancellationToken).ToList();

        this.CancellationToken.ThrowIfCancellationRequested();

        // We need to have all base types loaded before we can determine derived types (since we used the base type info to calculate derivation).
        // So load all the base type information first, then go hookup derived types.
        using (logger.StartTaskLog("Loading all base types"))
        {
            udts.LoadAllBaseTypes(this.DataCache, this.DIAAdapter, this.CancellationToken, ReportProgress);
        }

        this.CancellationToken.ThrowIfCancellationRequested();

        using (logger.StartTaskLog("Loading all derived types"))
        {
            udts.LoadAllDerivedTypes(this.CancellationToken, ReportProgress);
        }

        const int loggerOutputVelocity = 100;
        uint nextLoggerOutput = loggerOutputVelocity;
        var udtsEnumerated = 0;

        foreach (var udt in udts)
        {
            udtsEnumerated++;
            if (udtsEnumerated >= nextLoggerOutput)
            {
                ReportProgress($"Enumerated {udtsEnumerated:N0}/{udts.Count:N0} user-defined types so far.", nextLoggerOutput, (uint)udts.Count);
                nextLoggerOutput += loggerOutputVelocity;
            }

            this.CancellationToken.ThrowIfCancellationRequested();

            udt.EnsureFunctionsLoaded(this.CancellationToken);
            udt.EnsureVTableCountLoaded();
            udt.EnsureDataMembersLoaded(this.CancellationToken);
        }

        return udts;
    }
}
