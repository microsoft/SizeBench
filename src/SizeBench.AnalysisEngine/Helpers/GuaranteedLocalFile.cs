using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.Helpers;

internal sealed partial class GuaranteedLocalFile : IDisposable
{
    private readonly string _guaranteedLocalPath;
    private FileStream? _deleteOnCloseStream;

    /// <summary>
    /// Makes sure a file is local - not hosted on UNC, network drive, etc.  That way we can be sure to control latency
    /// on chatty filesystem operations, which DIA does in some cases.
    /// </summary>
    /// <param name="originalPath">The path that the user provided - may be local or remote</param>
    /// <param name="log">The log to write to.</param>
    /// <param name="forceLocalCopy">Always copy, even if the path is already local.  If you need to modify the file, 
    /// for example, to not change somebody's original.</param>
    public GuaranteedLocalFile(string originalPath, ILogger log, bool forceLocalCopy = false, bool openDeleteOnCloseStreamImmediately = true)
    {
        this.OriginalPath = originalPath;

        //TODO: SKUCrawler: this copying locally is a big perf hit - consider having an option on the Session to disable this, which SKUCrawler can turn on, but the GUI can leave off.
        //                  SKUCrawler must of course leave it on for "forceLocalCopy" which is used by the 'strip force integrity bit' code.
        if (!forceLocalCopy && IsLocalPath(originalPath))
        {
            this._guaranteedLocalPath = this.OriginalPath;
            this.CopiedLocally = false;
        }
        else
        {
            using var copyLog = log.StartTaskLog("Copying file locally");
            this._guaranteedLocalPath = Path.GetTempFileName();
            // We'll let the filesystem know this file is temporary, and that the indexer need not look at it
            // to avoid extra system load with no benefit.
            var fileInfo = new FileInfo(this._guaranteedLocalPath);
            fileInfo.Attributes |= FileAttributes.Temporary & FileAttributes.NotContentIndexed;

            copyLog.Log($"Copying from {originalPath} to {this._guaranteedLocalPath} (forceLocalCopy={forceLocalCopy})");
            File.Copy(this.OriginalPath, this._guaranteedLocalPath, overwrite: true);

            this.CopiedLocally = true;
            if (openDeleteOnCloseStreamImmediately)
            {
                OpenDeleteOnCloseStreamIfCopiedLocally();
            }
        }
    }

    public void OpenDeleteOnCloseStreamIfCopiedLocally()
    {
        if (this.CopiedLocally)
        {
            const int bufferSize = 1024 * 1024;
            this._deleteOnCloseStream = new FileStream(this._guaranteedLocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize, FileOptions.RandomAccess | FileOptions.DeleteOnClose);
        }
    }

    public bool CopiedLocally { get; }

    public string OriginalPath { get; }

    public string GuaranteedLocalPath
    {
        get
        {
            if (this._guaranteedLocalPath is null)
            {
                throw new ObjectDisposedException(GetType().Name, $"GuarateedLocalFile for {this.OriginalPath} has already been disposed.");
            }

            return this._guaranteedLocalPath;
        }
    }

    private static bool IsLocalPath(string path)
    {
        if (!PathIsUNC(path))
        {
            return !PathIsNetworkPath(path);
        }

        var uri = new Uri(path);
        return IsLocalHost(uri.Host);
    }

    private static bool IsLocalHost(string input)
    {
        IPAddress[] host;

        //get host addresses
        try { host = Dns.GetHostAddresses(input); }
#pragma warning disable CA1031 // Do not catch general exception types - if we throw for some reason that's not very important to the intent of this code, error handling here isn't worth much effort.
        catch { return false; }

        //get local adresses
        IPAddress[] local;
        try { local = Dns.GetHostAddresses(Dns.GetHostName()); }
        catch { return false; }
#pragma warning restore CA1031

        //check if local
        return host.Any(hostAddress => IPAddress.IsLoopback(hostAddress) || local.Contains(hostAddress));
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("Shlwapi.dll", EntryPoint = "PathIsNetworkPathW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PathIsNetworkPath([MarshalAs(UnmanagedType.LPWStr)] string pszPath);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("Shlwapi.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PathIsUNC([MarshalAs(UnmanagedType.LPWStr)] string pszPath);

    private bool _isDisposed;
    ~GuaranteedLocalFile() { Dispose(false); }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    private void Dispose(bool _)
    {
        if (this._isDisposed)
        {
            return;
        }

        this._isDisposed = true;

        if (this.CopiedLocally)
        {
            try
            {
                this._deleteOnCloseStream?.Dispose();
                this._deleteOnCloseStream = null;
            }
#pragma warning disable CA1031 // Do not catch general exception types - this is a best effort attempt to delete a temp file.  It if tails, there's nothing anyone can do.
            catch { } // best effort
#pragma warning restore CA1031
        }
    }
}
