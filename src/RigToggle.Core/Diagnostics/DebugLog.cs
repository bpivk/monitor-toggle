using System.Diagnostics;

namespace RigToggle.Core.Diagnostics;

/// <summary>
/// Debug session monitor-enable-reactivates-others-again, round 4: this class replaces
/// Program.cs's former inline, startup-only trace-listener wiring after round 3's rig
/// trial reported a COMPLETELY empty debug.log despite the bug still reproducing --
/// investigated before proposing yet another mechanism fix, per the round-4 checkpoint
/// objective's explicit instruction.
///
/// Root cause traced (by static reading, not rig access -- this sandbox has no Windows
/// runtime) to a real gap in the OLD design: Program.Main() read
/// AppSettings.EnableDebugLogging exactly ONCE, at process startup, and wired (or did
/// not wire) a TextWriterTraceListener based on that single read -- SettingsForm.cs's
/// Save handler wrote the new value to settings.json but never re-checked or re-wired
/// anything. This app is designed to run tray-resident for long stretches (TILE-06/
/// TRAY-01 doc comments elsewhere in this codebase); the natural operator workflow this
/// round -- open the already-running app, flip "Enable debug logging" on in Settings,
/// then immediately reproduce the bug WITHOUT fully quitting and relaunching the
/// process -- would silently produce exactly a zero-byte-of-new-content debug.log,
/// because the already-running process's Trace.Listeners collection was permanently
/// decided (empty) back when IT started, before the checkbox was ever touched. This
/// class fixes that permanently by making the listener configurable LIVE (see
/// Configure below), called both at startup (Program.cs) and on every Settings-Save
/// (SettingsForm.cs) -- so the checkbox takes effect immediately, no restart required,
/// for good.
///
/// This class also centralizes debug.log's path resolution in exactly one place
/// (ResolveLogFilePath) so Program.cs and SettingsForm.cs can never disagree about
/// where the file lives -- directly closes off candidate (e) ("the debug.log file
/// path/location being different than expected") from ever being possible, rather than
/// just diagnosing it once.
/// </summary>
public static class DebugLog
{
    private static readonly object Gate = new();
    private static TextWriterTraceListener? s_listener;
    private static bool s_wired;

    /// <summary>
    /// The one true debug.log path, computed the same way everywhere it is needed.
    /// </summary>
    public static string ResolveLogFilePath()
    {
        string basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RigToggle");
        return Path.Combine(basePath, "debug.log");
    }

    /// <summary>
    /// Idempotent, thread-safe, live (re)configuration of the file trace listener.
    /// Safe to call every time AppSettings.EnableDebugLogging is loaded (Program.cs
    /// startup) or saved (SettingsForm.cs), regardless of whether the value actually
    /// changed -- a call that requests the state already in effect is a deliberate,
    /// silent no-op (not logged), so calling this unconditionally on every Settings-Save
    /// is always safe and never spams the log with "nothing changed" noise.
    /// </summary>
    public static void Configure(bool enabled)
    {
        lock (Gate)
        {
            try
            {
                if (enabled && !s_wired)
                {
                    string logFilePath = ResolveLogFilePath();
                    Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

                    // FileShare.ReadWrite (not the StreamWriter(path) default of
                    // FileShare.Read only) -- 25-03 Task 3 precedent: a second process
                    // (or, here, a second Configure(true) after a prior
                    // Configure(false)/Configure(true) cycle within the SAME process)
                    // must be able to hold its own writable handle without a sharing-
                    // violation IOException silently swallowed by the catch below.
                    var logStream = new FileStream(
                        logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    var traceWriter = new StreamWriter(logStream) { AutoFlush = true };
                    s_listener = new TextWriterTraceListener(traceWriter);
                    Trace.Listeners.Add(s_listener);
                    s_wired = true;

                    Trace.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss.fff}] DebugLog.Configure: logging ENABLED -- writing to {logFilePath}.");
                }
                else if (!enabled && s_wired)
                {
                    Trace.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss.fff}] DebugLog.Configure: logging DISABLED -- this is the last line until re-enabled.");

                    if (s_listener is not null)
                    {
                        Trace.Listeners.Remove(s_listener);
                        s_listener.Flush();
                        s_listener.Dispose();
                        s_listener = null;
                    }

                    s_wired = false;
                }
            }
            catch
            {
                // Diagnostic logging is best-effort only -- never let it affect the caller
                // (matches every other Log()/TryLog() convention already in this codebase).
            }
        }
    }

    /// <summary>
    /// Always-on preflight marker: written directly via File.AppendAllText, bypassing
    /// Trace/Configure entirely, so it lands in debug.log REGARDLESS of whether
    /// EnableDebugLogging is on. Call once, as early as possible in Program.Main() after
    /// settings load. Round-4 fix for exactly the "log was completely empty and we can't
    /// tell why" report: this one line records the running exe's own path and on-disk
    /// last-write time (a stale, un-rebuilt binary is directly visible by comparing
    /// builtAt to "now"), the resolved debug.log path (rules out candidate (e) by
    /// showing exactly where this process is looking), and the EnableDebugLogging value
    /// as loaded from settings (rules out/in candidate (a) directly) -- all before any
    /// crash later in startup could prevent further logging (candidate (d): if this line
    /// is present but nothing else follows, the crash happened AFTER this point; if this
    /// line is entirely absent, the process never reached here at all). Cheap enough to
    /// always write unconditionally -- this is a personal single-user tool (CLAUDE.md),
    /// not a high-volume server log.
    /// </summary>
    public static void WriteStartupBanner(bool enableDebugLoggingAsLoaded)
    {
        try
        {
            string logFilePath = ResolveLogFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

            string exePath = Environment.ProcessPath ?? "(unknown)";
            string exeBuildStamp = exePath != "(unknown)" && File.Exists(exePath)
                ? File.GetLastWriteTime(exePath).ToString("yyyy-MM-dd HH:mm:ss")
                : "(unknown)";

            File.AppendAllText(
                logFilePath,
                $"[{DateTime.Now:HH:mm:ss.fff}] Program.Main: STARTUP pid={Environment.ProcessId} exe={exePath} " +
                $"builtAt={exeBuildStamp} EnableDebugLogging={enableDebugLoggingAsLoaded} logFile={logFilePath}" +
                Environment.NewLine);
        }
        catch
        {
            // Diagnostic logging is best-effort only -- never let it prevent startup.
        }
    }
}
