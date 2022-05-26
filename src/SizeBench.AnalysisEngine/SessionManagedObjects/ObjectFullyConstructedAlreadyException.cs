namespace SizeBench.AnalysisEngine;

internal sealed class ObjectFullyConstructedAlreadyException : InvalidOperationException
{
    public ObjectFullyConstructedAlreadyException()
        : base("This object is fully constructed already, this operation doesn't make sense now.")
    { }
}
