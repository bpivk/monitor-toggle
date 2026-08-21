using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace RigToggle.Windows;

/// <summary>
/// Public cross-assembly façade over the internal registered-window-message P/Invoke
/// pair in NativeMethods (INSTANCE-01/INSTANCE-02), mirroring GlobalHotkey.cs's exact
/// public-facade-over-internal-NativeMethods convention: NativeMethods is `internal` to
/// RigToggle.Windows with no InternalsVisibleTo grant to RigToggle.App, so the App layer
/// reaches this signal only through this wrapper, never NativeMethods directly.
///
/// The message name is embedded with this application's fixed instance GUID because a
/// registered-window-message id is a machine-wide reservation keyed off the exact string
/// passed in — embedding the GUID removes any chance of accidentally colliding with an
/// unrelated application that happens to register a similarly-generic name.
///
/// A zero <see cref="MessageId"/> means registration failed, and callers must never
/// compare a received message against an unguarded zero id, because WM_NULL is also
/// zero — treating every WM_NULL as an activation signal would restore the window on
/// essentially every idle message pump tick.
///
/// This signal is deliberately payload-free: <see cref="BroadcastActivation"/> always
/// posts with wParam and lParam both zero, and callers must never start passing data
/// through them. The whole effect on receipt is "make this app's own window visible and
/// focused" — nothing is delivered, read, or trusted from the broadcast itself.
///
/// INSTANCE-02 regression fix (25-03 Task 3 operator checkpoint, real Windows hardware):
/// posting the message alone is not sufficient for an ALREADY-VISIBLE, minimized primary
/// window to actually come to the front. Windows' anti-focus-stealing heuristic blocks
/// the primary's own <c>Activate()</c>/<c>SetForegroundWindow</c> call unless SOME
/// process currently holds "may set foreground window" permission at the moment it's
/// called — the primary itself does not hold it (it is a background process from the
/// OS's point of view at that moment), and merely receiving a posted message does not
/// grant it. The loser DOES hold it (inherited from Explorer launching it moments
/// earlier) and can explicitly forward it via <c>AllowSetForegroundWindow</c> before it
/// exits. <see cref="BroadcastActivation"/> grants it immediately before every retry
/// attempt below, matching the same repeated-robustness posture already used for the
/// message itself — a lone grant call could otherwise be consumed or expire before a
/// late-listening primary's <c>Activate()</c> call runs.
///
/// Follow-up hardening (25-03, second debug.log capture): the grant now targets the
/// specific peer process id (resolved via <see cref="System.Diagnostics.Process"/> by
/// process name, since exactly one other Rig Toggle process should exist while a
/// duplicate is running) instead of the wildcard <c>ASFW_ANY</c>. <c>ASFW_ANY</c> grants
/// the permission to whichever process next calls <c>SetForegroundWindow</c> —
/// literally any window on the desktop, not necessarily ours — so an unrelated
/// application happening to call it in the brief window between our grant and the
/// primary's own <c>Activate()</c> call could consume it first, wasting the grant. A
/// PID-targeted grant can only be consumed by the intended primary. Falls back to
/// <c>ASFW_ANY</c> only if the peer's process id could not be resolved.
/// </summary>
public static class ActivationSignal
{
    /// <summary>
    /// Machine-wide-unique message name, scoped by this application's fixed instance
    /// GUID (see the class doc comment for why the GUID is embedded here).
    /// </summary>
    public const string MessageName = "RigToggle_ShowExisting_8f3a1c42-7b5e-4d19-9a06-2e5c1f8b7d34";

    /// <summary>
    /// Number of times <see cref="BroadcastActivation"/> posts the activation message.
    /// Repeating is safe rather than merely tolerable: the receiving handler
    /// (MainForm.RestoreAndFocus — Show/restore/Activate) is idempotent, and
    /// PostMessage is fire-and-forget with no delivery confirmation available, so
    /// repeated posting is the only robustness lever available to the sending side
    /// against the Pitfall 8 startup race (a receiver that is not listening yet on the
    /// first attempt).
    /// </summary>
    public const int BroadcastAttempts = 3;

    private static readonly TimeSpan BroadcastRetryDelay = TimeSpan.FromMilliseconds(150);

    private static uint? _messageId;

    /// <summary>
    /// The registered message id for <see cref="MessageName"/>, resolved and cached
    /// (including a zero/failed result — a failed registration does not retry on every
    /// access) on first use.
    /// </summary>
    public static uint MessageId => _messageId ??= NativeMethods.RegisterWindowMessage(MessageName);

    /// <summary>
    /// Broadcasts the activation signal to every top-level window on the desktop,
    /// <see cref="BroadcastAttempts"/> times with a short delay between attempts.
    /// Returns immediately without posting when <see cref="MessageId"/> is zero
    /// (registration failed) — there is nothing meaningful to broadcast in that case.
    /// </summary>
    public static void BroadcastActivation()
    {
        uint messageId = MessageId;
        Log($"BroadcastActivation: resolved messageId={messageId}.");
        if (messageId == 0)
        {
            Log("BroadcastActivation: messageId is 0 (RegisterWindowMessage failed) -- nothing to broadcast.");
            return;
        }

        uint foregroundGrantTarget = ResolvePeerProcessId() ?? NativeMethods.ASFW_ANY;
        Log(foregroundGrantTarget == NativeMethods.ASFW_ANY
            ? "BroadcastActivation: could not resolve a specific peer process id -- falling back to ASFW_ANY."
            : $"BroadcastActivation: targeting foreground grant at peer process id {foregroundGrantTarget}.");

        for (int attempt = 0; attempt < BroadcastAttempts; attempt++)
        {
            if (attempt > 0)
            {
                Thread.Sleep(BroadcastRetryDelay);
            }

            // Best-effort, fire-and-forget, matching PostMessage's own posture on the
            // next line — a failed grant here (return value ignored) is not fatal, it
            // just means this particular attempt's Activate() call may not visibly
            // take effect; the retry loop is the tolerance mechanism for both calls.
            bool allowed = NativeMethods.AllowSetForegroundWindow(foregroundGrantTarget);
            bool posted = NativeMethods.PostMessage((IntPtr)NativeMethods.HWND_BROADCAST, messageId, IntPtr.Zero, IntPtr.Zero);
            Log($"BroadcastActivation: attempt {attempt}: AllowSetForegroundWindow={allowed}, PostMessage={posted}.");
        }
    }

    /// <summary>
    /// True iff <paramref name="handle"/> is currently the OS-level foreground window
    /// (<c>GetForegroundWindow() == handle</c>) -- the unambiguous ground-truth signal
    /// for "did this window actually come to the front," independent of
    /// <c>Control.Focused</c>/<c>ContainsFocus</c> (which reflect keyboard-focus
    /// assignment among a form's CHILD controls and can legitimately be false on the
    /// form itself even when the form genuinely is the foreground window).
    /// </summary>
    public static bool IsForegroundWindow(IntPtr handle) => NativeMethods.GetForegroundWindow() == handle;

    /// <summary>
    /// Resolves the process id of the one other Rig Toggle process expected to be
    /// running (the primary, from this loser's point of view) by matching on this
    /// process's own name -- best-effort, returns null on any failure or if zero/more
    /// than one other candidate is found (ambiguous; ASFW_ANY is the safer fallback in
    /// that case rather than guessing which candidate is the real primary).
    /// </summary>
    private static uint? ResolvePeerProcessId()
    {
        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            Process[] candidates = Process.GetProcessesByName(currentProcess.ProcessName);
            try
            {
                Process[] others = candidates.Where(p => p.Id != currentProcess.Id).ToArray();
                return others.Length == 1 ? (uint)others[0].Id : null;
            }
            finally
            {
                foreach (Process candidate in candidates)
                {
                    candidate.Dispose();
                }
            }
        }
        catch
        {
            return null;
        }
    }

    // Best-effort diagnostic logging (25-03 Task 3 operator checkpoint investigation),
    // routed through Trace.WriteLine (the established convention in this codebase --
    // WindowsAppController.cs, WindowsThemeProvider.cs, WindowsAutostartConfigurator.cs)
    // so RigToggle.App's TextWriterTraceListener persists it to
    // %LOCALAPPDATA%\RigToggle\debug.log. Never throws -- a logging failure must never
    // affect the activation broadcast itself.
    private static void Log(string message)
    {
        try
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ActivationSignal: {message}");
        }
        catch
        {
            // Logging is diagnostic-only; never let it affect broadcast behavior.
        }
    }
}
