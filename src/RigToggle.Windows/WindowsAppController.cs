using System.Diagnostics;
using RigToggle.Core.Abstractions;

namespace RigToggle.Windows;

/// <summary>
/// Real companion-app running detection via Process.GetProcessesByName, matched on
/// the process name derived from the configured .exe path (D-07). LaunchOrFocus and
/// MinimizeIfRunning are documented no-op stubs until Phase 3 fills in the real Win32
/// user32.dll ShowWindow/SetForegroundWindow mutation (02-RESEARCH.md Pattern 1,
/// Pattern 6). Deliberately does NOT use FindWindow/FindWindowEx title matching for
/// detection (STACK.md "What NOT to Use").
/// </summary>
public sealed class WindowsAppController : IAppController
{
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
        // FAKE in Phase 2 — no-op. Real Process.Start (if not running) or
        // SetForegroundWindow (if running) via user32.dll P/Invoke lands in Phase 3.
    }

    public void MinimizeIfRunning(string companionAppPath)
    {
        // FAKE in Phase 2 — no-op. Real ShowWindow(hWnd, SW_MINIMIZE) via user32.dll
        // P/Invoke lands in Phase 3.
    }
}
