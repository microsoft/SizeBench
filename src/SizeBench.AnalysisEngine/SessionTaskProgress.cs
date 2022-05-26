namespace SizeBench.AnalysisEngine;

public sealed class SessionTaskProgress
{
    public string Message { get; }
    public uint ItemsComplete { get; }
    public uint ItemsTotal { get; }
    public bool IsProgressIndeterminate { get; }

    public SessionTaskProgress(string message,
                               uint itemsComplete,
                               uint? itemsTotal)
    {
        this.Message = message;
        if (itemsTotal.HasValue)
        {
            this.ItemsTotal = itemsTotal.Value;
        }
        else
        {
            this.ItemsTotal = 0;
            this.IsProgressIndeterminate = true;
        }
        this.ItemsComplete = itemsComplete;
    }
}
