using System.Collections.Concurrent;
using System.Diagnostics;
using SizeBench.Logging;
using SizeBench.SKUCrawler.CrawlFolder;
using SizeBench.Threading.Tasks.Schedulers;

namespace SizeBench.SKUCrawler;

internal sealed class MasterControllerProcess : IDisposable
{
    private readonly ConcurrentBag<Process> BatchProcesses = new ConcurrentBag<Process>();
    //TODO: SKUCrawler: check if Parallel.ForEachAsync might be a simpler way of writing this code, once moved to .NET 6
    //TODO: SKUCrawler: 3 seems like a reasonable number of batch processes to run at once, since each one tries to soak all the CPU cores already, so this just
    //                  has a few running to interleave CPU work as they wait on disk I/O and such.
    private readonly QueuedTaskScheduler _taskScheduler = new QueuedTaskScheduler(threadCount: 3);
    private readonly TaskFactory _taskFactory;
    private readonly List<Task> _batchTasks = new List<Task>();
    private readonly object _outputSyncObject = new object();
    private readonly ConcurrentDictionary<int, ConcurrentBag<string>> _errorsFromBatchesByPID = new ConcurrentDictionary<int, ConcurrentBag<string>>();
    private readonly ConcurrentDictionary<int, string> _batchCommandLinesByPID = new ConcurrentDictionary<int, string>();
    private readonly ConcurrentDictionary<int, int> _batchNumbersByPID = new ConcurrentDictionary<int, int>();

    public MasterControllerProcess()
    {
        this._taskFactory = new TaskFactory(this._taskScheduler);
    }

    public Task KickOffAndWaitForBatches(List<ProductBinary> productBinaries, CrawlFolderArguments appArgs)
    {
        var numBatches = (int)Math.Ceiling(productBinaries.Count / (float)appArgs.BatchSize);
        Console.WriteLine($"Found {productBinaries.Count} binaries that SizeBench can try to parse.  Breaking this up into {numBatches} batches.");

        // Note that batch number starts at 1, not zero.  Why?  I forget, but that's what it is.
        for (var i = 1; i < numBatches + 1; i++)
        {
            var batchNumber = i; // Need to put this inside the loop so the lambda doesn't capture "i" as the final value over and over
            var commandLine = appArgs.CommandLineArgsForBatch(batchNumber);
            this._batchTasks.Add(this._taskFactory.StartNew(() => StartOneBatchTask(commandLine, batchNumber),
                                 CancellationToken.None,
                                 TaskCreationOptions.LongRunning,
                                 this._taskScheduler));
        }

        return Task.WhenAll(this._batchTasks);
    }

    private void StartOneBatchTask(string commandLineForBatch, int batchNumber)
    {
        var psi = new ProcessStartInfo("SizeBench.SKUCrawler.exe", commandLineForBatch)
        {
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        Console.WriteLine($"Starting batch process, command line: {commandLineForBatch}");

        var batchProcess = new Process()
        {
            StartInfo = psi,
            EnableRaisingEvents = true
        };
        batchProcess.OutputDataReceived += BatchProcess_OutputDataReceived;
        batchProcess.ErrorDataReceived += BatchProcess_ErrorDataReceived;
        this.BatchProcesses.Add(batchProcess);
        batchProcess.Start();
        batchProcess.BeginOutputReadLine();
        batchProcess.BeginErrorReadLine();
        this._batchCommandLinesByPID.TryAdd(batchProcess.Id, commandLineForBatch);
        this._batchNumbersByPID.TryAdd(batchProcess.Id, batchNumber);
        batchProcess.WaitForExit(); //TODO: is there a way to use WaitForExitAsync to make this code cleaner and more correctly async?
    }

    private void BatchProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!String.IsNullOrEmpty(e.Data))
        {
            lock (this._outputSyncObject)
            {
                var batchProcess = (Process)sender;
                var errorsFromThisBatch = this._errorsFromBatchesByPID.GetOrAdd(batchProcess.Id, (_) => new ConcurrentBag<string>());
                errorsFromThisBatch.Add(e.Data);
            }
        }
    }

    private void BatchProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!String.IsNullOrEmpty(e.Data))
        {
            lock (this._outputSyncObject)
            {
                Console.WriteLine(e.Data);
            }
        }
    }

    public void ProcessAllDeferredBatchErrors(ILogger log)
    {
        using (var colorScope = new ConsoleColorScope(ConsoleColor.Red))
        {
            foreach (var batchPID in this._errorsFromBatchesByPID.Keys)
            {
                foreach (var errorLineFromBatch in this._errorsFromBatchesByPID[batchPID])
                {
                    // We get the errors one line at a time, so we try to guess at where the interesting 'first line' of an exception is, by looking for
                    // "Exception:" - that way "System.InvalidOperationException: some message" gets this extra data before we then let it spew out the callstack.
                    if (errorLineFromBatch.Contains("Exception:", StringComparison.Ordinal))
                    {
                        WriteToLogAndStdErr(log, String.Empty);
                        WriteToLogAndStdErr(log, "----------------------------------------");
                        if (this._batchNumbersByPID.TryGetValue(batchPID, out var batchNumber))
                        {
                            WriteToLogAndStdErr(log, $"Process for batch {batchNumber} had an error:");
                            WriteToLogAndStdErr(log, $"To repro this error, use the command line: ");
                            WriteToLogAndStdErr(log, $"    {this._batchCommandLinesByPID[batchPID]}");
                        }
                        else
                        {
                            WriteToLogAndStdErr(log, $"Process with PID {batchPID} had an error, but we could not determine which batch this was from:");
                        }
                        WriteToLogAndStdErr(log, String.Empty);
                        WriteToLogAndStdErr(log, errorLineFromBatch);
                    }
                    else
                    {
                        WriteToLogAndStdErr(log, errorLineFromBatch);
                    }
                }
            }
        }

        if (!this._errorsFromBatchesByPID.IsEmpty)
        {
            throw new InvalidOperationException($"{this._errorsFromBatchesByPID.Count} batches (of {this.BatchProcesses.Count} batches total) had at least one error emitted.");
        }
    }

    private static void WriteToLogAndStdErr(ILogger log, string s)
    {
        log.Log(s, LogLevel.Error);
        Console.Error.WriteLine(s);
    }

    #region IDisposable Support
    private bool disposedValue; // To detect redundant calls

    private void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this._taskScheduler.Dispose();
            }

            this.disposedValue = true;
        }
    }

    // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    // ~MasterControllerProcess()
    // {
    //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
    //   Dispose(false);
    // }

    // This code added to correctly implement the disposable pattern.
    void IDisposable.Dispose() =>
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);// TODO: uncomment the following line if the finalizer is overridden above.// GC.SuppressFinalize(this);
    #endregion
}
