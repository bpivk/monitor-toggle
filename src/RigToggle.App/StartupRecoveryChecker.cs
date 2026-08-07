using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.App
{
    /// <summary>
    /// Two blocking startup dialogs, extracted from Program.cs for testability
    /// (16-RESEARCH.md Pattern 3). Both are the deliberate exception to this app's
    /// best-effort-swallow startup idiom (Program.cs's trace-listener/settings-load
    /// try/catch blocks) — a mode-corruption or crash-recovery condition must fail
    /// loudly at startup (D-06/D-07), not be silently swallowed like every other
    /// startup side effect.
    ///
    /// Sequence (16-UI-SPEC.md Copywriting Contract "Dialog ordering"): if the mode
    /// file is missing/unreadable, Startup dialog 1 (mode-corruption) shows and the
    /// crash marker is deliberately NOT checked — the mode-unknown instruction
    /// ("check manually") already subsumes the crash-recovery guidance. Only when the
    /// mode is known does this checker look for a leftover crash-in-progress marker
    /// (DISPLAY-13); if found, the marker is cleared FIRST (so a repeat crash before
    /// the user acts doesn't re-show a stale dialog forever) and then Startup dialog 2
    /// (crash-recovery) shows, naming the interrupted target mode.
    ///
    /// Assumed to run before Application.Run/MainForm is shown (Program.cs wires this
    /// in, Task 2), on both the visible and --tray startup paths — so owner is always
    /// null (MainForm may not exist/be shown yet under --tray).
    /// </summary>
    internal static class StartupRecoveryChecker
    {
        public static void Run(IModeStore modeStore, IToggleInProgressStore markerStore)
        {
            ToggleMode? currentMode = modeStore.TryLoad();

            if (currentMode is null)
            {
                // D-06/D-07: fails loudly at startup rather than silently defaulting to
                // a mode, and rather than deferring the failure to the first toggle
                // attempt. The crash marker is deliberately NOT checked here — an
                // unknown mode already tells the user to verify everything manually,
                // which subsumes the crash-recovery dialog's own guidance.
                MessageBox.Show(
                    null,
                    "Rig Toggle can't tell whether you're currently in Rig Mode or Normal Mode — the saved mode file is missing or unreadable. Please check your monitors and audio device manually before using Toggle.",
                    "Rig Toggle",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var marker = markerStore.TryLoad();
            if (marker is not null)
            {
                // Clear FIRST (before showing the dialog) — DISPLAY-13: the marker must
                // never persist past this single surfacing, regardless of what happens
                // while the dialog is on screen.
                markerStore.Clear();

                string targetModeText = marker.TargetMode == ToggleMode.Rig ? "Rig" : "Normal";

                // D-02/D-03: inform-only, exactly one OK button — no inline retry action.
                MessageBox.Show(
                    null,
                    $"Rig Toggle didn't finish its last toggle to {targetModeText} Mode cleanly — it may have crashed or been closed mid-toggle. No automatic retry has been attempted; please check your monitors and audio device manually before using Toggle again.",
                    "Rig Toggle",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
