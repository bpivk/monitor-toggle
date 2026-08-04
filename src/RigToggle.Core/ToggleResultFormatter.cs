using RigToggle.Core.Models;

namespace RigToggle.Core;

/// <summary>
/// Shared ToggleResult -> display-string formatting, relocated out of
/// RigToggle.App.MainForm (D-09) to unlock reuse by this phase's tray/toast triggers
/// and to make the wording unit-testable. FormatChecklist reproduces MainForm's
/// existing private FormatChecklist verbatim (same switch arms, same
/// Environment.NewLine join) -- MainForm.cs itself is left untouched by this plan;
/// its own caller is cleaned up in Plan 08-02, which owns that file. TruncateForBalloon
/// exists because NotifyIcon.ShowBalloonTip has an undocumented ~255-char hard limit
/// that silently truncates mid-word with no ellipsis of its own (08-RESEARCH.md
/// Pitfall 4) -- without this guard, a verbose failure Reason could vanish from the
/// toast with no indication anything was cut.
/// </summary>
public static class ToggleResultFormatter
{
    /// <summary>
    /// Maps a ToggleResult's per-step outcomes to a human-readable checklist, one line
    /// per step in step order. Byte-identical to MainForm's current wording (relocated
    /// verbatim, not reworded).
    /// </summary>
    public static string FormatChecklist(ToggleResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Steps.Select(step => step.Outcome switch
            {
                ToggleStepOutcome.Succeeded => $"{step.StepName}: OK",
                ToggleStepOutcome.Failed => $"{step.StepName}: FAILED ({step.Reason})",
                ToggleStepOutcome.NotAttempted => $"{step.StepName}: not attempted",
                ToggleStepOutcome.Skipped => $"{step.StepName}: Skipped (not configured)",
                _ => $"{step.StepName}: unknown",
            }));
    }

    /// <summary>
    /// Mode-title wording for the toast/notification header (D-09), matching the GUI's
    /// own "Switch to Rig/Normal Mode" phrasing convention.
    /// </summary>
    public static string FormatModeTitle(bool isInRigMode) =>
        isInRigMode ? "Switched to Rig Mode" : "Switched to Normal Mode";

    /// <summary>
    /// Truncates text to at most maxLength characters, appending an ellipsis marker
    /// when truncation occurs, so a verbose failure reason cannot silently vanish mid-
    /// word inside a balloon notification's hard character limit. Never throws on
    /// empty input or a degenerate maxLength.
    /// </summary>
    public static string TruncateForBalloon(string text, int maxLength = 250)
    {
        if (string.IsNullOrEmpty(text) || maxLength < 1 || text.Length <= maxLength)
        {
            return text;
        }

        int cutoff = Math.Max(0, maxLength - 1);
        return text[..cutoff] + "…";
    }
}
