namespace SizeBench.AnalysisEngine;

public sealed record class SessionOptions
{
    public SymbolSourcesSupported SymbolSourcesSupported { get; init; } = SymbolSourcesSupported.All;

    /// <summary>
    /// Optional symsrv-style symbol search path used to locate (and download) PDBs when an explicit
    /// PDB path is not supplied. The string is passed verbatim to DIA's loadDataForExe, so it follows
    /// the standard format used by dbghelp / symsrv. Examples:
    /// <list type="bullet">
    /// <item><description><c>srv*https://msdl.microsoft.com/download/symbols</c></description></item>
    /// <item><description><c>srv*C:\Symbols*https://msdl.microsoft.com/download/symbols</c></description></item>
    /// <item><description><c>C:\local\symbols;srv*C:\cache*https://internal.symbol.server</c></description></item>
    /// </list>
    /// When this is non-empty and the PDB path passed to <see cref="Session.Create(string, string, SessionOptions, SizeBench.Logging.ILogger)"/>
    /// is empty, the symbol server will be used to locate the PDB for the binary.
    /// </summary>
    public string? SymbolServerSearchPath { get; init; }
}
