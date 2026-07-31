namespace RigToggle.Core;

/// <summary>
/// Parses the process command-line args for the `--tray` hidden-startup flag
/// (TRAY-02). Lives in Core (not Program.cs) so it is unit-testable even though the
/// App composition root has no test project of its own. ShouldStartHidden must never
/// throw on null/empty/garbage args (Security Domain V5): this predicate runs on
/// every autostart-launched process before any UI exists, and an exception here would
/// crash the app silently on every login — the exact failure mode an autostart entry
/// must never have.
/// </summary>
public static class StartupArgs
{
    private const string TrayFlag = "--tray";

    /// <summary>
    /// True iff `--tray` is present as an exact (case-insensitive) token in args.
    /// Never indexes into args and never throws — an empty array, a garbage array, or
    /// any combination of unrelated tokens simply yields false.
    /// </summary>
    public static bool ShouldStartHidden(string[]? args) =>
        args is not null && args.Contains(TrayFlag, StringComparer.OrdinalIgnoreCase);
}
