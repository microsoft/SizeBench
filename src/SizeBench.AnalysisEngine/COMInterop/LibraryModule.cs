using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SizeBench.AnalysisEngine.COMInterop;

[ExcludeFromCodeCoverage]
[DebuggerDisplay("LibraryModule for {FilePath}")]
internal class LibraryModule : IDisposable
{
    private IntPtr _handle;

    private static class Win32
    {
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - cannot use this here as it's got a false positive going on, as tracked in this GitHub issue: https://github.com/dotnet/roslyn-analyzers/issues/5479
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);
#pragma warning restore CA2101
    }


    public static LibraryModule LoadModule(string filePath)
    {
        var libraryModule = new LibraryModule(Win32.LoadLibrary(filePath), filePath);
        if (libraryModule._handle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, "Cannot load library: " + filePath);
        }
        return libraryModule;
    }

    private LibraryModule(IntPtr handle, string filePath)
    {
        this.FilePath = filePath;
        this._handle = handle;
    }

    #region IDisposable

    private void Dispose(bool _)
    {
        if (this._handle != IntPtr.Zero)
        {
            Win32.FreeLibrary(this._handle);
            this._handle = IntPtr.Zero;
        }
    }

    ~LibraryModule()
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

    public IntPtr GetProcAddress(string procName)
    {
        var ptr = Win32.GetProcAddress(this._handle, "DllGetClassObject");
        if (ptr == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"Cannot find proc {procName} in {this.FilePath}");
        }
        return ptr;
    }

    public string FilePath { get; }
}
