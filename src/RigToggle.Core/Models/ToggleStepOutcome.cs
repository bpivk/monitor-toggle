namespace RigToggle.Core.Models;

/// <summary>
/// Outcome of a single toggle step (Monitor / Audio / App). NotAttempted covers steps
/// skipped because an earlier step in a stop-on-first-failure sequence (ToggleToRigMode,
/// D-04) already failed. Skipped (added Phase 15/D-03) covers a step deliberately left
/// unconfigured by the user (optional App/Audio targets, APP-04/AUDIO-03/AUDIO-04) — these
/// are NOT the same state and must never render identically: NotAttempted means "blocked
/// by an earlier failure," Skipped means "nothing to do here by design." Do not reuse or
/// rename NotAttempted to represent the Skipped case.
/// </summary>
public enum ToggleStepOutcome
{
    Succeeded,
    Failed,
    NotAttempted,
    Skipped,
}
