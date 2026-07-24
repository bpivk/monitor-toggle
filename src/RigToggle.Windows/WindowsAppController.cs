using System.Diagnostics;
using RigToggle.Core.Abstractions;

namespace RigToggle.Windows;

/// <summary>
/// Real companion-app running detection via Process.GetProcessesByName, matched on
/// the process name derived from the configured .exe path (D-07). LaunchOrFocus
/// launches the app if absent and polls (Process.Refresh()-aware, 250ms/10s) for its
/// main window to appear before best-effort SetForegroundWindow-ing it (D-06); if
/// already running, it re-enumerates and focuses the existing window instead of
/// launching a duplicate, and does NOT poll when that window handle is zero
/// (tray-only case — D-06). MinimizeIfRunning best-effort ShowWindow(SW_MINIMIZE)s the
/// existing window; a zero handle (or not running) is a silent no-op, never a failure
/// (D-07). Both use hand-rolled user32.dll P/Invoke via NativeMethods. Deliberately
/// does NOT use FindWindow/FindWindowEx title matching for detection, and does NOT use
/// AttachThreadInput/simulated-input focus bypass tricks (STACK.md "What NOT to Use").
/// </summary>
public sealed class WindowsAppController : IAppController
{
    private static readonly TimeSpan LaunchPollTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LaunchPollInterval = TimeSpan.FromMilliseconds(250);

    public bool IsRunning(string companionAppPath)
    {
        if (string.IsNullOrWhiteSpace(companionAppPath))
        {
            return false;
        }

        // GetProcessesByName excludes the ".exe" extension — never pass the raw path
        // or a hardcoded literal (02-RESEARCH.md Pattern 6 / Pitfall 5).
        string processName = Path.GetFileNameWithoutExtension(companionAppPath);
        if (string.IsNullOrEmpty(processName))
        {
            return false;
        }

        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            // Process.GetProcessesByName hands back IDisposable Process objects wrapping
            // native handles (WR-02) — dispose every one, not just the ones we happened
            // to read .Length from.
            foreach (var p in processes)
            {
                p.Dispose();
            }
        }
    }

    public void LaunchOrFocus(string companionAppPath)
    {
        if (!IsRunning(companionAppPath))
        {
            using var process = Process.Start(companionAppPath)
                ?? throw new InvalidOperationException($"Failed to start '{companionAppPath}'.");

            var deadline = DateTime.UtcNow + LaunchPollTimeout;
            while (DateTime.UtcNow < deadline)
            {
                // Process.MainWindowHandle is cached — Refresh() MUST run before every
                // read or a freshly-created window will never be observed (D-06,
                // 03-RESEARCH.md Pattern 4).
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(process.MainWindowHandle); // best-effort
                    return;
                }

                Thread.Sleep(LaunchPollInterval);
            }

            // Timed out — D-06 says don't fail the whole toggle; app is launched, just
            // not focused yet.
            return;
        }

        // Already running: focus the existing window (best-effort), never launch a
        // duplicate. If the tray-only case (no live window) applies, no-op — do NOT
        // poll, since nothing is expected to create a window on its own here (D-06).
        string processName = Path.GetFileNameWithoutExtension(companionAppPath);
        if (string.IsNullOrEmpty(processName))
        {
            return;
        }

        var processes = Process.GetProcessesByName(processName);
        try
        {
            foreach (var p in processes)
            {
                p.Refresh();
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(p.MainWindowHandle); // best-effort
                    break;
                }
            }
        }
        finally
        {
            foreach (var p in processes)
            {
                p.Dispose();
            }
        }
    }

    public void MinimizeIfRunning(string companionAppPath)
    {
        if (!IsRunning(companionAppPath))
        {
            return;
        }

        string processName = Path.GetFileNameWithoutExtension(companionAppPath);
        if (string.IsNullOrEmpty(processName))
        {
            return;
        }

        var processes = Process.GetProcessesByName(processName);
        try
        {
            foreach (var p in processes)
            {
                p.Refresh();
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(p.MainWindowHandle, NativeMethods.SW_MINIMIZE); // best-effort
                    break;
                }
            }
        }
        finally
        {
            foreach (var p in processes)
            {
                p.Dispose();
            }
        }
    }
}
