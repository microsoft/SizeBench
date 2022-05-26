using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DebuggerInterop;

internal interface IDebuggerAdapter : IAsyncDisposable
{
    Task OpenAsync(string binaryPath, string pdbPath, PE.MachineType machineType, CancellationToken cancellationToken, ILogger taskLog);
    Task<string> DisassembleAsync(IFunctionCodeSymbol function, DisassembleFunctionOptions options, ILogger taskLog, CancellationToken token);
}
