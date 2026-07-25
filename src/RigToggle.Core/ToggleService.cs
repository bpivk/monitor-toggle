using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core;

/// <summary>
/// Orchestrates the snapshot-before-mutate toggle sequence (D-08/ARCHITECTURE.md Pattern 2)
/// entirely through the ISettingsStore/ISnapshotStore/IMonitorController/IAudioController/
/// IAppController interfaces — zero Windows API references live here. Current mode is
/// derived from snapshot-file presence (D-14), not a separate flag. CORE-04 partial-failure
/// reporting is deliberately asymmetric between the two directions: ToggleToRigMode is
/// stop-on-first-failure (D-04) because the forward steps have real dependencies — there is
/// no point switching audio or launching the companion app if the monitor never actually
/// disabled — while ToggleToNormalMode is isolate-and-continue (D-05, unchanged since
/// gap-closure 03-04) because each restore step recovers independent, unrelated hardware
/// state and a failure in one should not block attempting the others. This asymmetry is
/// intentional and must not be "fixed" into false symmetry.
/// </summary>
public sealed class ToggleService
{
    private readonly ISettingsStore _settingsStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IMonitorController _monitorController;
    private readonly IAudioController _audioController;
    private readonly IAppController _appController;

    public ToggleService(
        ISettingsStore settingsStore,
        ISnapshotStore snapshotStore,
        IMonitorController monitorController,
        IAudioController audioController,
        IAppController appController)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
        _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
        _appController = appController ?? throw new ArgumentNullException(nameof(appController));
    }

    /// <summary>
    /// Loads settings, verifies the companion app path still exists (D-05 preflight —
    /// fails fast with nothing yet captured, persisted, or mutated), captures the
    /// current monitor + audio state, saves that snapshot (BEFORE any mutation —
    /// CORE-03), then disables the configured monitor, switches the default audio
    /// device, and launches/focuses the companion app — stop-on-first-failure (D-04):
    /// the first mutation step to throw is recorded as Failed and every step after it
    /// is recorded as NotAttempted, with no rollback and no further steps attempted.
    /// </summary>
    public ToggleResult ToggleToRigMode()
    {
        var settings = _settingsStore.Load();

        if (!IsFullyConfigured(settings))
        {
            // Guard against WR-01: without this check, an unconfigured (null-field)
            // AppSettings would still make it through to _snapshotStore.Save() below,
            // durably persisting a garbage snapshot and flipping IsInRigMode() to true
            // (D-14) even though nothing was actually captured or changed.
            throw new InvalidOperationException(
                "Rig Toggle settings are not fully configured. Open Settings and choose a monitor, both audio devices, and the companion app path before switching to Rig Mode.");
        }

        if (!File.Exists(settings.CompanionAppPath))
        {
            // D-05: a missing/moved companion-app path must fail before any state is
            // captured, persisted, or mutated — this is a fail-fast UX guard, not a
            // security control (T-03-09 accepts the TOCTOU window between this check
            // and the later Process.Start in WindowsAppController.LaunchOrFocus).
            throw new InvalidOperationException(
                $"The companion app could not be found at '{settings.CompanionAppPath}'. Open Settings and reselect the companion app path before switching to Rig Mode.");
        }

        var monitorState = _monitorController.CaptureState();
        var audioState = _audioController.CaptureState();

        // Snapshot MUST be persisted before any mutation call (D-08/CORE-03 guarantee).
        _snapshotStore.Save(new Models.StateSnapshot(monitorState, audioState));

        var steps = new List<ToggleStepResult>();

        if (!TryExecuteStep("Monitor", () => _monitorController.Disable(settings.MonitorDevicePath!), steps))
        {
            // D-04 stop-on-first-failure: a failed Disable means Audio/App never run —
            // no point switching audio or launching the companion app on a monitor that
            // never actually disabled.
            steps.Add(new ToggleStepResult("Audio", ToggleStepOutcome.NotAttempted, null));
            steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));

            // CR-01 (code review): WindowsMonitorController.Disable has pre-mutation
            // validation guards (target not active / target is the only active display)
            // that throw before any real CCD mutation is attempted — in that case
            // nothing on the machine actually changed, so the snapshot saved above must
            // not be left behind. Leaving it would flip IsInRigMode() to true (MainForm
            // would show "Mode: Rig") at the exact moment this same result reports
            // "Monitor: FAILED", even though the display was never touched. Re-capture
            // and compare against the state captured before Disable() ran; clear the
            // snapshot only if nothing changed. If re-capture throws, or the states
            // differ (a real, possibly partial mutation did happen), the snapshot is
            // kept — a retry or manual restore needs it, and silently discarding the
            // only copy of that state would be worse than leaving it.
            try
            {
                if (MonitorStateUnchanged(monitorState, _monitorController.CaptureState()))
                {
                    _snapshotStore.Clear();
                }
            }
            catch
            {
                // Re-capture failed — can't confirm nothing changed, so err toward
                // keeping the snapshot rather than risking silent loss of recoverable
                // state.
            }

            return new ToggleResult(steps);
        }

        if (!TryExecuteStep("Audio", () => _audioController.SetDefault(settings.RigAudioDeviceId!), steps))
        {
            steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));
            return new ToggleResult(steps);
        }

        TryExecuteStep("App", () => _appController.LaunchOrFocus(settings.CompanionAppPath!), steps);

        return new ToggleResult(steps);
    }

    /// <summary>
    /// Runs a single mutation step, appending a Succeeded/Failed ToggleStepResult to
    /// <paramref name="steps"/> and returning whether it succeeded — used by
    /// ToggleToRigMode's stop-on-first-failure sequence (D-04) to decide whether to
    /// continue to the next step or short-circuit with NotAttempted entries.
    /// </summary>
    private static bool TryExecuteStep(string stepName, Action action, List<ToggleStepResult> steps)
    {
        try
        {
            action();
            steps.Add(new ToggleStepResult(stepName, ToggleStepOutcome.Succeeded, null));
            return true;
        }
        catch (Exception ex)
        {
            steps.Add(new ToggleStepResult(stepName, ToggleStepOutcome.Failed, ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Structural equality for MonitorState, used only by the CR-01 fix above. MonitorState
    /// is a record, but its Paths member is typed IReadOnlyList&lt;T&gt; — record-generated
    /// equality falls back to reference equality for that member (interfaces/List&lt;T&gt;
    /// don't override Equals), so a plain `before == after` would always report "changed"
    /// even when nothing did. SequenceEqual against MonitorPathSnapshot's own (correct,
    /// record-generated) per-element equality avoids that trap.
    /// </summary>
    private static bool MonitorStateUnchanged(Models.MonitorState before, Models.MonitorState after) =>
        before.TargetDevicePath == after.TargetDevicePath && before.Paths.SequenceEqual(after.Paths);

    /// <summary>
    /// True when every field ToggleToRigMode/ToggleToNormalMode depend on has been saved
    /// at least once via Settings (mirrors the four fields SettingsForm.ValidateSettingsForm
    /// already requires before enabling Save). Exposed publicly so MainForm can pre-check
    /// before offering "Switch to Rig Mode" at all, rather than relying solely on the
    /// exception thrown by ToggleToRigMode above.
    /// </summary>
    public bool IsSettingsConfigured() => IsFullyConfigured(_settingsStore.Load());

    private static bool IsFullyConfigured(Models.AppSettings settings) =>
        !string.IsNullOrEmpty(settings.MonitorDevicePath)
        && !string.IsNullOrEmpty(settings.NormalAudioDeviceId)
        && !string.IsNullOrEmpty(settings.RigAudioDeviceId)
        && !string.IsNullOrEmpty(settings.CompanionAppPath);

    /// <summary>
    /// Loads the snapshot and restores the monitor and audio state it captured — the
    /// audio restore path always uses IAudioController.Restore, never the forward-mode
    /// device-switch call, which is reserved for the rig-mode path only. Minimizes the
    /// companion app if running, then clears the snapshot last so IsInRigMode() flips
    /// back to false. Isolate-and-continue (D-05): unlike ToggleToRigMode's stop-on-
    /// first-failure, every restore step here is attempted regardless of whether an
    /// earlier one failed, and no step throws — each outcome is recorded as a
    /// ToggleStepResult instead (D-02).
    ///
    /// Monitor restore failure is NOT swallowed (04-CONTEXT.md D-05): a failed monitor
    /// restore leaves the user's screen disabled, so it is recorded as a Failed step so
    /// MainForm's checklist can surface it, and the snapshot must survive the failure so
    /// a retry has the exact prior state to restore from. Clearing the snapshot on a
    /// failed monitor restore would silently and permanently discard the only copy of
    /// that state. Audio restore is still attempted even if monitor restore failed
    /// (independent subsystem — a monitor-driver hiccup shouldn't also block a
    /// potentially-successful audio restore); the monitor failure is recorded as the
    /// "App" step's NotAttempted afterward (not re-thrown), and MinimizeIfRunning/Clear
    /// below are skipped in that case since D-05 says no further recovery is attempted.
    ///
    /// Gap-closure 03-04 (AUDIO-02 recovery / APP-03 reachability, T-03-04-02): audio
    /// restore keeps its original swallow-and-continue behavior — a genuinely-gone
    /// audio device (unplugged) can never succeed on retry, so MinimizeIfRunning and
    /// Clear must still run afterward instead of getting permanently stuck. This does
    /// NOT apply to the monitor restore path above it.
    ///
    /// Snapshot-corruption handling: IsInRigMode()/Exists() reports true purely from file
    /// presence, but JsonSnapshotStore.Load() independently degrades to null on a
    /// corrupted/truncated state.json. Treating "corrupted" the same as "never existed"
    /// would silently skip both restores and still clear the file — permanently
    /// discarding the only recoverable state while reporting success. wasInRigMode
    /// distinguishes the two so a corrupted file fails loudly instead (still an exception
    /// — this preflight-style guard fires before any step runs, so it stays outside the
    /// ToggleResult contract, matching ToggleToRigMode's preflight guards).
    /// </summary>
    public ToggleResult ToggleToNormalMode()
    {
        var settings = _settingsStore.Load();

        bool wasInRigMode = _snapshotStore.Exists();
        var snapshot = _snapshotStore.Load();

        var steps = new List<ToggleStepResult>();

        if (snapshot is null)
        {
            if (wasInRigMode)
            {
                throw new InvalidOperationException(
                    "The saved rig-mode state file exists but could not be read (corrupted). " +
                    "Your monitor and audio device were NOT restored automatically. Fix or " +
                    "delete the corrupted state file before retrying.");
            }

            // WR-01 (code review): never was in rig mode (no snapshot ever existed) —
            // there is nothing to restore. Falling through to the companion-app block
            // below would minimize the app even though ToggleToRigMode never ran, and
            // Clear() would operate on a snapshot that never existed. ToggleToNormalMode
            // is public with no enforced "must be in rig mode" precondition (MainForm
            // happens to gate calls behind IsInRigMode(), but other/future callers are
            // not required to), so this must be a real no-op, not an implicit fallthrough.
            return new ToggleResult(new List<ToggleStepResult>());
        }
        else
        {
            Exception? monitorFailure = null;
            try
            {
                _monitorController.Restore(snapshot.Monitor);
            }
            catch (Exception ex)
            {
                monitorFailure = ex;
            }

            Exception? audioFailure = null;
            try
            {
                _audioController.Restore(snapshot.Audio);
            }
            catch (Exception ex)
            {
                // Intentionally swallowed (gap-closure 03-04): see class-level remarks.
                // Traced (not silent) since this codebase has no other logging and a
                // swallowed exception here would otherwise be forensically invisible.
                System.Diagnostics.Trace.WriteLine($"Audio restore failed, continuing: {ex}");
                audioFailure = ex;
            }

            steps.Add(new ToggleStepResult(
                "Monitor",
                monitorFailure is null ? ToggleStepOutcome.Succeeded : ToggleStepOutcome.Failed,
                monitorFailure?.Message));
            steps.Add(new ToggleStepResult(
                "Audio",
                audioFailure is null ? ToggleStepOutcome.Succeeded : ToggleStepOutcome.Failed,
                audioFailure?.Message));

            if (monitorFailure is not null)
            {
                // D-05: no further recovery is attempted once monitor restore fails —
                // MinimizeIfRunning/Clear below are skipped and the snapshot survives.
                steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));
                return new ToggleResult(steps);
            }
        }

        // CompanionAppPath can be null/empty if settings.json is corrupted
        // (JsonSettingsStore.Load() degrades to a blank AppSettings on any read
        // failure) — propagating null into MinimizeIfRunning would throw and prevent
        // Clear() below from ever running, permanently stranding the UI in "Rig" mode
        // even after monitor/audio were already restored successfully above.
        if (!string.IsNullOrEmpty(settings.CompanionAppPath))
        {
            _appController.MinimizeIfRunning(settings.CompanionAppPath);
            steps.Add(new ToggleStepResult("App", ToggleStepOutcome.Succeeded, null));
        }
        else
        {
            // No companion app path configured/available — minimize was never
            // attempted (not that it failed); represented as NotAttempted so the
            // checklist keeps a consistent up-to-3-entry shape.
            steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));
        }

        _snapshotStore.Clear();

        return new ToggleResult(steps);
    }

    /// <summary>
    /// Current mode is derived from snapshot-file presence (D-14) — no separate
    /// in-memory/persisted flag exists.
    /// </summary>
    public bool IsInRigMode() => _snapshotStore.Exists();
}
