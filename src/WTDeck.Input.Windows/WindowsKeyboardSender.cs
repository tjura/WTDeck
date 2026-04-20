using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WTDeck.Core.Interfaces;
using WTDeck.Core.Models;

namespace WTDeck.Input.Windows;

/// <summary>
/// Sends key chords to War Thunder via Win32 SendInput.
///
/// Games read input from the foreground window, so this sender first locates
/// the War Thunder game window and brings it to the foreground before emitting
/// the scan codes. If WT is not found it falls back to the current foreground
/// (useful for smoke tests without the game running).
/// </summary>
public sealed class WindowsKeyboardSender : IKeyboardSender
{
    private const int FocusSettleDelayMs = 120;
    private const int KeyHoldDelayMs = 60;
    private readonly ILogger<WindowsKeyboardSender> _logger;

    public WindowsKeyboardSender(ILogger<WindowsKeyboardSender> logger)
    {
        _logger = logger;
    }

    public void Send(KeyChord chord)
    {
        if (chord.ScanCodes.Count == 0)
            return;

        var wtHandle = WarThunderWindowFinder.Find();
        var fgBefore = NativeMethods.GetForegroundWindow();

        _logger.LogInformation(
            "Key chord {Chord}. WT window: {Wt}. Foreground before: {FgBefore}",
            string.Join("+", chord.ScanCodes),
            WarThunderWindowFinder.DescribeWindow(wtHandle),
            WarThunderWindowFinder.DescribeWindow(fgBefore));

        if (wtHandle == IntPtr.Zero)
        {
            _logger.LogWarning(
                "War Thunder window not found. Sending to current foreground - " +
                "input will not reach the game unless WT is focused.");
        }
        else if (wtHandle != fgBefore)
        {
            var brought = ForegroundActivator.BringToForeground(wtHandle);
            Thread.Sleep(FocusSettleDelayMs);
            var fgAfter = NativeMethods.GetForegroundWindow();
            _logger.LogInformation(
                "BringToForeground returned {Result}. Foreground after: {FgAfter}",
                brought,
                WarThunderWindowFinder.DescribeWindow(fgAfter));

            if (fgAfter != wtHandle)
            {
                _logger.LogWarning(
                    "War Thunder did not become foreground - input may be delivered to wrong window.");
            }
        }

        // Final verification: check foreground window RIGHT BEFORE SendInput.
        // Detects cases where another process steals focus back during the settle delay.
        var fgAtSend = NativeMethods.GetForegroundWindow();
        if (wtHandle != IntPtr.Zero && fgAtSend != wtHandle)
        {
            _logger.LogWarning(
                "Foreground was stolen back before send. Current: {Fg}. Retrying activation.",
                WarThunderWindowFinder.DescribeWindow(fgAtSend));
            ForegroundActivator.BringToForeground(wtHandle);
            Thread.Sleep(FocusSettleDelayMs);
        }

        SendScanCodes(chord);
    }

    private void SendScanCodes(KeyChord chord)
    {
        var inputSize = Marshal.SizeOf<NativeMethods.INPUT>();
        _logger.LogDebug("INPUT struct marshalled size: {Size} bytes (expected 40 on x64)", inputSize);

        // Press all keys (one SendInput call)
        var downInputs = new NativeMethods.INPUT[chord.ScanCodes.Count];
        for (var i = 0; i < chord.ScanCodes.Count; i++)
        {
            downInputs[i] = CreateKeyInput((ushort)chord.ScanCodes[i], keyUp: false);
        }
        var sentDown = NativeMethods.SendInput((uint)downInputs.Length, downInputs, inputSize);

        // Hold briefly so the game's input poller (typically running at frame rate)
        // actually sees the key as pressed. Without this, fast-repeating down+up
        // within the same millisecond can be missed by games that sample state
        // rather than queue raw events.
        Thread.Sleep(KeyHoldDelayMs);

        // Release all keys (reverse order)
        var upInputs = new NativeMethods.INPUT[chord.ScanCodes.Count];
        for (var i = 0; i < chord.ScanCodes.Count; i++)
        {
            var scanIndex = chord.ScanCodes.Count - 1 - i;
            upInputs[i] = CreateKeyInput((ushort)chord.ScanCodes[scanIndex], keyUp: true);
        }
        var sentUp = NativeMethods.SendInput((uint)upInputs.Length, upInputs, inputSize);

        if (sentDown != downInputs.Length || sentUp != upInputs.Length)
        {
            _logger.LogWarning(
                "SendInput partial: down {SentDown}/{ExpectedDown}, up {SentUp}/{ExpectedUp} (Win32 error {Error})",
                sentDown, downInputs.Length, sentUp, upInputs.Length, Marshal.GetLastWin32Error());
        }
        else
        {
            _logger.LogInformation("Sent key chord: [{ScanCodes}] (hold {Hold}ms)",
                string.Join(", ", chord.ScanCodes), KeyHoldDelayMs);
        }
    }

    private static NativeMethods.INPUT CreateKeyInput(ushort scanCode, bool keyUp)
    {
        // Populate both scan code and virtual key so games reading either
        // field see a complete event (Windows officially ignores wVk when
        // KEYEVENTF_SCANCODE is set, but some game input hooks still read it).
        var vk = (ushort)NativeMethods.MapVirtualKey(scanCode, NativeMethods.MAPVK_VSC_TO_VK);

        return new NativeMethods.INPUT
        {
            Type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scanCode,
                    dwFlags = NativeMethods.KEYEVENTF_SCANCODE | (keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0),
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }
}
