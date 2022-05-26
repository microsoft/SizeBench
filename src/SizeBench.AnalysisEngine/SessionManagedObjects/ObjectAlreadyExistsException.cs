namespace SizeBench.AnalysisEngine;

internal sealed class ObjectAlreadyExistsException : InvalidOperationException
{
    public ObjectAlreadyExistsException()
        : base("This object has already been created before in this Session - something has gone wrong if we're creating it again, and all kinds of immutability guarantees will be invalidated.")
    { }
}
