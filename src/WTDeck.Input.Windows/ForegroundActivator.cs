namespace WTDeck.Input.Windows;

/// <summary>
/// Brings a target window to the foreground reliably.
///
/// Windows restricts <c>SetForegroundWindow</c> when the calling process is not
/// already in the foreground. The standard workaround is to briefly attach the
/// calling thread's input queue to the target thread's input queue; while attached,
/// <c>SetForegroundWindow</c> behaves as if the caller was allowed to steal focus.
/// </summary>
public static class ForegroundActivator
{
    public static bool BringToForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return false;

        // Restore the window if it is minimized.
        if (NativeMethods.IsIconic(hWnd))
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);

        var currentThread = NativeMethods.GetCurrentThreadId();
        var targetThread = NativeMethods.GetWindowThreadProcessId(hWnd, out _);

        if (currentThread == targetThread)
            return NativeMethods.SetForegroundWindow(hWnd);

        var attached = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
        try
        {
            return NativeMethods.SetForegroundWindow(hWnd);
        }
        finally
        {
            if (attached)
                NativeMethods.AttachThreadInput(currentThread, targetThread, false);
        }
    }
}
