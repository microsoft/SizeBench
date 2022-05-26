namespace SizeBench.AnalysisEngine;

internal sealed class ObjectNotYetFullyConstructedException : InvalidOperationException
{
    public ObjectNotYetFullyConstructedException()
        : base("This object is not yet fully constructed, this operation doesn't make sense now.")
    { }
}
