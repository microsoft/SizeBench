using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SizeBench.GUI.Windows;

// This is all HWNDy code, very hard to test effectively
[ExcludeFromCodeCoverage]
internal static partial class WindowTitleBarExtensions
{
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial void SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;

    private const uint WS_MAXIMIZEBOX = 0x00010000;
    private const uint WS_MINIMIZEBOX = 0x00020000;

    private const uint WS_EX_DLGMODALFRAME = 0x00000001;

    public static void HideMinimizeAndMaximizeFromTitleBar(this Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;

        SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_MINIMIZEBOX);
        SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_MAXIMIZEBOX);
        SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_DLGMODALFRAME);
    }
}
