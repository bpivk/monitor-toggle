namespace RigToggle.Core.Models;

/// <summary>
/// Outcome of a single toggle step (Monitor / Audio / App). NotAttempted covers steps
/// skipped because an earlier step in a stop-on-first-failure sequence (ToggleToRigMode,
/// D-04) already failed.
/// </summary>
public enum ToggleStepOutcome
{
    Succeeded,
    Failed,
    NotAttempted,
}
