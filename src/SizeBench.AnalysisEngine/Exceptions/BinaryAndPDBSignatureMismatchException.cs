namespace SizeBench.AnalysisEngine;

public sealed class BinaryAndPDBSignatureMismatchException : Exception
{
    public BinaryAndPDBSignatureMismatchException(string message) : base(message)
    { }

    public BinaryAndPDBSignatureMismatchException(string message, Exception innerException) : base(message, innerException)
    { }
}
