using RigToggle.Core;

namespace RigToggle.App;

/// <summary>
/// The placeholder relaunch-helper entry point for the app's own internal
/// self-update relaunch (UPDATE-07). Reached only from the very first branch of
/// Program.Main(), before the single-instance guard is ever acquired and before any
/// other bootstrap step -- so the app's own relaunch of itself can never be blocked
/// by the very guard mechanism the currently-running instance holds (D-03,
/// ARCHITECTURE.md Pattern 1).
///
/// Phase 25 ships Run's body deliberately empty; Phase 26 replaces the body with the
/// real update-apply logic while leaving the flag token, this method's signature,
/// and its return contract untouched (D-04). In Phase 25 this method performs no
/// filesystem access, no external-process launch, no persistent-configuration
/// change, no outbound network call, and creates or touches no named
/// operating-system synchronization object of any kind -- that emptiness is a
/// load-bearing property, not an omission: it is what makes the bypass path
/// provably idempotent and provably free of interference with a concurrently
/// running normal instance.
///
/// The returned constant exists so an automated test can distinguish "the bypass
/// ran" from "a duplicate launch was blocked" -- both would otherwise look
/// identical from outside the process: one that started and then quickly exited.
/// </summary>
internal static class UpdateApplyEntryPoint
{
    /// <summary>
    /// Runs the placeholder relaunch-helper body and returns
    /// <see cref="StartupArgs.ApplyUpdateBypassExitCode"/>. <paramref name="applyUpdateArgs"/>
    /// is unused in Phase 25 -- its name is kept exactly as Phase 26 will need it,
    /// since Phase 26 replaces this method's body only, never its signature.
    /// </summary>
    internal static int Run(string[] applyUpdateArgs)
    {
        return StartupArgs.ApplyUpdateBypassExitCode;
    }
}
