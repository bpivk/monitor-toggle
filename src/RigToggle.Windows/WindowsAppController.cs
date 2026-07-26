using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using RigToggle.Core.Abstractions;

namespace RigToggle.Windows;

/// <summary>
/// Real companion-app running detection via Process.GetProcessesByName, matched on
/// the process name derived from the configured launch-target path (D-07).
/// LaunchOrFocus unconditionally relaunches the configured target via Process.Start
/// with UseShellExecute = true -- ShellExecute resolves a .lnk shortcut exactly like a
/// double-click (carrying its target/working-directory/arguments), and a well-behaved
/// single-instance app's own activation logic brings its existing instance to the
/// foreground when relaunched (rig-verified against Moza Companion). RigToggle never
/// enumerates or manipulates a window it does not own on this path -- this supersedes
/// the prior FindBestMainWindow + SetForegroundWindow + poll/fallback dance and is
/// believed to resolve the H9 "close button inert" limitation, since that symptom's
/// trigger (RigToggle foregrounding a window it didn't own) no longer exists
/// (.planning/debug/resolved/moza-foreground-focus.md).
///
/// MinimizeIfRunning is the only remaining window-control path (toggle-back still
/// needs real window control, per the redesign's scope). It resolves the existing
/// window via FindBestMainWindow's EnumWindows-by-PID scan (NOT title/class matching --
/// FindWindow/FindWindowEx is forbidden, CLAUDE.md "What NOT to Use") before
/// best-effort ShowWindow(SW_MINIMIZE)-ing it. FindBestMainWindow deliberately does NOT
/// trust Process.MainWindowHandle: (1) that heuristic only sees
/// IsWindowVisible==true windows, missing a tray-hidden (Visible=false) companion
/// window even though a live window exists; (2) when a process owns MULTIPLE
/// top-level windows at once (e.g. Moza Companion's main dashboard AND a separate
/// "Torque Curve" utility window open simultaneously), MainWindowHandle can pick the
/// wrong one entirely -- confirmed on rig test. Instead, FindBestMainWindow scores
/// every owner-less top-level window belonging to the PID by caption presence and
/// GetWindowPlacement rcNormalPosition area, picking the largest titled candidate (a
/// main dashboard is reliably larger than a small utility dialog). Deliberately does
/// NOT use AttachThreadInput/simulated-input focus bypass tricks (STACK.md "What NOT
/// to Use").
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
        // Unconditional relaunch, never a window-focus dance: a well-behaved
        // single-instance app (rig-verified: Moza Companion) self-activates its own
        // existing instance when relaunched, so RigToggle never needs to enumerate or
        // touch a window it does not own
        // (.planning/debug/resolved/moza-foreground-focus.md, H9). UseShellExecute =
        // true is REQUIRED so a .lnk shortcut resolves like a real double-click
        // (carrying the shortcut's working directory/arguments) instead of
        // Process.Start attempting to execute the .lnk file itself as a binary, and so
        // the target app's own single-instance activation logic runs at all. Let
        // Process.Start exceptions propagate — ToggleService's step wrapper handles
        // them; do NOT swallow them here.
        Log($"LaunchOrFocus: relaunch requested for '{companionAppPath}'.");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = companionAppPath,
            UseShellExecute = true,
        }) ?? throw new InvalidOperationException($"Failed to start '{companionAppPath}'.");
    }

    // Best-effort diagnostic logging (.planning/debug/resolved/moza-foreground-focus.md,
    // reasoning_checkpoint_3): still generically useful for the relaunch path. Routed
    // through Trace.WriteLine (the one logging convention already used elsewhere in this
    // codebase, e.g. ToggleService.cs) so RigToggle.App's TextWriterTraceListener (wired
    // in Program.cs) persists it to %LOCALAPPDATA%\RigToggle\debug.log for the user to
    // read back after a rig test. Never throws -- a logging failure must never affect the
    // best-effort toggle itself.
    private static void Log(string message)
    {
        try
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] WindowsAppController: {message}");
        }
        catch
        {
            // Logging is diagnostic-only; never let it affect toggle behavior.
        }
    }

    // Enumerates ALL top-level (owner-less) windows belonging to the given process ID,
    // regardless of visibility -- unlike Process.MainWindowHandle, which silently filters
    // to IsWindowVisible==true windows only AND stops at the first match it finds (an
    // undocumented internal heuristic, not something callers can rely on when a process
    // owns more than one top-level window). Matched by process ID via
    // GetWindowThreadProcessId, NOT by title/class (FindWindow/FindWindowEx is forbidden --
    // CLAUDE.md "What NOT to Use").
    //
    // IMPORTANT (.planning/debug/resolved/moza-foreground-focus.md, reasoning_checkpoint_3
    // and _5): this method must never stop at the first candidate it finds, for two
    // independent reasons discovered across that debug session's rig tests:
    //   (1) System.Windows.Forms.NotifyIcon creates its own top-level, owner-less,
    //       permanently-invisible native window purely to receive Shell_NotifyIcon
    //       callback messages -- it satisfies the same owner==0 filter as a real main
    //       form but has no caption and a zero/near-zero rect.
    //   (2) A single process can legitimately have MULTIPLE real, titled, non-trivially
    //       sized top-level windows open at once (confirmed on rig test: Moza Companion's
    //       main dashboard AND a separate "Torque Curve" utility window, same PID,
    //       Process.MainWindowHandle picked the smaller Torque Curve window). Taking the
    //       first titled+sized match (the old behavior) is therefore ALSO not safe.
    // The heuristic used here: among all owner-less, PID-matched candidates, prefer the
    // one with a real caption (excludes NotifyIcon's message window) AND the LARGEST
    // RESTORED window area, read via GetWindowPlacement's rcNormalPosition rather than
    // GetWindowRect (see NativeMethods.WindowPlacement remarks -- GetWindowRect reports a
    // degenerate/off-screen rect for a currently-minimized window, which would wrongly
    // score the real, but-currently-iconic, main dashboard as tiny/zero-area). A main
    // application dashboard is virtually always significantly larger than a small
    // utility/config dialog like a torque-curve editor, regardless of which one happens to
    // be minimized at the moment of comparison. If no candidate has a caption at all, fall
    // back to the largest-area candidate regardless of title, so this can never regress an
    // already-working case to "find nothing".
    private static IntPtr FindBestMainWindow(uint processId)
    {
        IntPtr bestTitledCandidate = IntPtr.Zero;
        long bestTitledArea = -1;
        IntPtr bestAnyCandidate = IntPtr.Zero;
        long bestAnyArea = -1;

        bool EnumCallback(IntPtr hWnd, IntPtr _)
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid != processId)
            {
                return true; // keep enumerating
            }

            if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
            {
                return true; // owned window (e.g. tooltip/popup) -- keep looking
            }

            int titleLength = NativeMethods.GetWindowTextLength(hWnd);
            if (titleLength > 0)
            {
                var sb = new StringBuilder(titleLength + 1);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
            }

            var placement = new NativeMethods.WindowPlacement
            {
                Length = Marshal.SizeOf<NativeMethods.WindowPlacement>(),
            };
            long width = 0;
            long height = 0;
            if (NativeMethods.GetWindowPlacement(hWnd, ref placement))
            {
                width = placement.NormalPosition.Right - placement.NormalPosition.Left;
                height = placement.NormalPosition.Bottom - placement.NormalPosition.Top;
            }
            long area = width > 0 && height > 0 ? width * height : 0;

            if (area > bestAnyArea)
            {
                bestAnyArea = area;
                bestAnyCandidate = hWnd;
            }

            if (titleLength > 0 && area > bestTitledArea)
            {
                bestTitledArea = area;
                bestTitledCandidate = hWnd;
            }

            // Never stop early -- every remaining candidate for this PID must be compared
            // by area, so the largest one is not missed regardless of enumeration order.
            return true;
        }

        NativeMethods.EnumWindows(EnumCallback, IntPtr.Zero); // best-effort

        return bestTitledCandidate != IntPtr.Zero ? bestTitledCandidate : bestAnyCandidate;
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

                // Trusting Process.MainWindowHandle alone risks minimizing the wrong
                // top-level window when the process owns more than one (e.g. a utility
                // window like "Torque Curve" instead of the real main dashboard),
                // leaving the real window on-screen after toggle-back. Use
                // FindBestMainWindow's largest-area candidate selection instead.
                IntPtr hWnd = FindBestMainWindow((uint)p.Id);
                if (hWnd != IntPtr.Zero)
                {
                    // Diagnostic-only instrumentation for the toggle-back regression
                    // investigation (quick task 260726-ixu, follow-up to 260726-idx): capture
                    // the target window's real visible/iconic state immediately before and
                    // after the unconditional ShowWindow(SW_MINIMIZE) call, and the call's own
                    // return value. No control flow changed -- same target, same unconditional
                    // minimize, same break.
                    bool preVisible = NativeMethods.IsWindowVisible(hWnd);
                    bool preIconic = NativeMethods.IsIconic(hWnd);
                    Log($"MinimizeIfRunning: pre-minimize hWnd=0x{hWnd:X}, IsWindowVisible={preVisible}, IsIconic={preIconic}");

                    bool showWindowReturned = NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE); // best-effort

                    bool postVisible = NativeMethods.IsWindowVisible(hWnd);
                    bool postIconic = NativeMethods.IsIconic(hWnd);
                    Log($"MinimizeIfRunning: post-minimize hWnd=0x{hWnd:X}, IsWindowVisible={postVisible}, IsIconic={postIconic}, ShowWindowReturned={showWindowReturned}");

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
