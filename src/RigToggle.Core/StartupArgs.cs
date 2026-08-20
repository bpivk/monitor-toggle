namespace RigToggle.Core;

/// <summary>
/// Parses the process command-line args for two independent, exact-token startup
/// flags: `--tray` (TRAY-02, hidden startup) and `--apply-update` (UPDATE-07, the
/// internal relaunch-helper bypass that lets Phase 26's update-apply relaunch skip
/// the single-instance guard). Lives in Core (not Program.cs) so both parsers are
/// unit-testable even though the App composition root has no test project of its
/// own. Neither helper may ever throw on null/empty/garbage args (Security Domain
/// V5): both run on every process launch before any UI exists, and an exception
/// here would crash the app silently on every login or every internal relaunch —
/// exactly the failure mode a startup-time parser must never have.
///
/// TryGetApplyUpdateArgs deliberately does not interpret or validate the payload it
/// extracts (D-04, Claude's Discretion "option-a" — opaque trailing payload): Phase
/// 26 owns the payload's positional meaning and can evolve it without ever touching
/// this parser or its tests. Note also that the payload appears verbatim in the
/// process command line, which is readable by any process on the machine — so by
/// contract it must carry only file paths and process ids, never a secret.
/// </summary>
public static class StartupArgs
{
    private const string TrayFlag = "--tray";

    /// <summary>
    /// The `--apply-update` startup-gate bypass flag (D-03/D-04, UPDATE-07). Checked
    /// as the very first branch in Program.cs's Main(), above the single-instance
    /// guard acquisition — public (unlike TrayFlag) because Phase 26 and this
    /// project's tests must both name the exact token rather than retyping the
    /// string literal.
    /// </summary>
    public const string ApplyUpdateFlag = "--apply-update";

    /// <summary>
    /// The process exit code the Phase 25 placeholder relaunch-helper entry point
    /// (UpdateApplyEntryPoint.Run) returns. Lives here in Core, not in the App
    /// layer, so RigToggle.Windows.Tests — which reaches Core transitively but can
    /// never reference RigToggle.App — can assert on it directly. Phase 26 replaces
    /// the entry point's body with real update-apply logic; if that change alters
    /// what "the bypass ran" looks like, it changes here first and the test follows.
    /// </summary>
    public const int ApplyUpdateBypassExitCode = 20;

    /// <summary>
    /// True iff `--tray` is present as an exact (case-insensitive) token in args.
    /// Never indexes into args and never throws — an empty array, a garbage array, or
    /// any combination of unrelated tokens simply yields false.
    /// </summary>
    public static bool ShouldStartHidden(string[]? args) =>
        args is not null && args.Contains(TrayFlag, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True iff `--apply-update` is present as an exact (case-insensitive) token in
    /// args; when true, <paramref name="applyUpdateArgs"/> holds every token that
    /// followed the first match, in original order, unvalidated and uninterpreted
    /// (D-04, option-a). When false — including for a null or empty args array —
    /// <paramref name="applyUpdateArgs"/> is always a non-null empty array, never
    /// null, so callers that skip the bool return still get a safe value. Never
    /// throws for any input.
    /// </summary>
    public static bool TryGetApplyUpdateArgs(string[]? args, out string[] applyUpdateArgs)
    {
        if (args is not null)
        {
            int index = Array.FindIndex(args, arg => StringComparer.OrdinalIgnoreCase.Equals(arg, ApplyUpdateFlag));
            if (index >= 0)
            {
                applyUpdateArgs = args[(index + 1)..];
                return true;
            }
        }

        applyUpdateArgs = Array.Empty<string>();
        return false;
    }
}
