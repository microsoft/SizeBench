namespace SizeBench.SKUCrawler;

public sealed class ConsoleColorScope : IDisposable
{
    private readonly ConsoleColor originalForegroundColor = ConsoleColor.White;

    public ConsoleColorScope(ConsoleColor newForegroundColor)
    {
        this.originalForegroundColor = Console.ForegroundColor;
        Console.ForegroundColor = newForegroundColor;
    }

    #region IDisposable Support
    private bool disposedValue; // To detect redundant calls

    private void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                // Dispose managed state (managed objects).
            }

            Console.ForegroundColor = this.originalForegroundColor;

            this.disposedValue = true;
        }
    }

    ~ConsoleColorScope()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(false);
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
