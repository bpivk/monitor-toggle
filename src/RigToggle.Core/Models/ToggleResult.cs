namespace RigToggle.Core.Models;

/// <summary>
/// Full outcome of a ToggleToRigMode/ToggleToNormalMode call — ordered per-step results,
/// consumed identically by MainForm regardless of toggle direction (D-03). Scoped strictly
/// to the 3 mutation steps (Monitor/Audio/App); preflight guards (unconfigured settings,
/// missing companion app path) remain exception-based and are NOT represented here.
/// </summary>
public sealed record ToggleResult(IReadOnlyList<ToggleStepResult> Steps)
{
    public bool Success => Steps.All(s => s.Outcome == ToggleStepOutcome.Succeeded);
}
