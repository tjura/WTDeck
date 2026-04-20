using System.Diagnostics;
using System.Text;

namespace WTDeck.Input.Windows;

/// <summary>
/// Locates the War Thunder game window so key input can be routed to it directly.
/// War Thunder's main window uses the Dagor engine, so we match both the known class
/// name "DagorWClass" and the process name "aces.exe" as a fallback.
/// </summary>
public static class WarThunderWindowFinder
{
    private const string WarThunderClassName = "DagorWClass";
    private const string WarThunderProcessName = "aces";

    public static IntPtr Find()
    {
        // Fast path: known class name
        var byClass = NativeMethods.FindWindow(WarThunderClassName, null);
        if (byClass != IntPtr.Zero)
            return byClass;

        // Fallback: locate by process name
        try
        {
            var processes = Process.GetProcessesByName(WarThunderProcessName);
            foreach (var process in processes)
            {
                try
                {
                    var handle = process.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                        return handle;
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            // Process enumeration can fail under restricted accounts - ignore
        }

        return IntPtr.Zero;
    }

    public static string DescribeWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return "<none>";

        var title = new StringBuilder(256);
        NativeMethods.GetWindowText(hWnd, title, title.Capacity);

        var className = new StringBuilder(256);
        NativeMethods.GetClassName(hWnd, className, className.Capacity);

        return $"hwnd=0x{hWnd.ToInt64():X} class=\"{className}\" title=\"{title}\"";
    }
}
