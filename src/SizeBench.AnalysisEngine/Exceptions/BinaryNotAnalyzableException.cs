namespace SizeBench.AnalysisEngine;

public sealed class BinaryNotAnalyzableException : Exception
{
    public BinaryNotAnalyzableException(string message) : base(message)
    { }

    public BinaryNotAnalyzableException(string message, Exception innerException) : base(message, innerException)
    { }
}
