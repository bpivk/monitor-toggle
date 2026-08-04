namespace RigToggle.Core.Models;

/// <summary>
/// Full outcome of a ToggleToRigMode/ToggleToNormalMode call — ordered per-step results,
/// consumed identically by MainForm regardless of toggle direction (D-03). Scoped strictly
/// to the 3 mutation steps (Monitor/Audio/App); preflight guards (unconfigured settings,
/// missing companion app path) remain exception-based and are NOT represented here.
/// </summary>
public sealed record ToggleResult(IReadOnlyList<ToggleStepResult> Steps)
{
    // Phase 15/D-03: a Skipped step (deliberately unconfigured target) is not a failure —
    // only Failed/NotAttempted should flip Success to false. Do not revert this to a
    // strict `== Succeeded` check; that would make every toggle with any optional target
    // left unset report as "did not fully complete" via MainForm's warning path.
    public bool Success => Steps.All(s => s.Outcome is ToggleStepOutcome.Succeeded or ToggleStepOutcome.Skipped);
}
