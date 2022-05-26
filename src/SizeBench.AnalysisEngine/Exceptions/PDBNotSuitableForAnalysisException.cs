namespace SizeBench.AnalysisEngine;

public sealed class PDBNotSuitableForAnalysisException : Exception
{
    public PDBNotSuitableForAnalysisException(string message) : base(message)
    { }

    public PDBNotSuitableForAnalysisException(string message, Exception innerException) : base(message, innerException)
    { }
}
