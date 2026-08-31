using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core;

/// <summary>
/// Orchestrates the toggle sequence entirely through the ISettingsStore/IModeStore/
/// IMonitorController/IAudioController/IAppController interfaces — zero Windows API
/// references live here. Current mode is tracked via an explicit IModeStore flag
/// (DISPLAY-11), not derived from snapshot-file presence — the mode flag is written
/// only AFTER the Monitor step of the relevant direction has confirmed success, in
/// both directions. Both toggle directions apply their own explicit, symmetric
/// monitor set (Rig: MonitorsToDisable/MonitorsToEnable; Normal:
/// NormalMonitorsToDisable/NormalMonitorsToEnable) via the same
/// ActivateMonitors/DeactivateMonitors primitives — Normal mode no longer restores
/// from a pre-toggle snapshot (DISPLAY-10). CORE-04 partial-failure reporting is
/// deliberately asymmetric between the two directions: ToggleToRigMode is
/// stop-on-first-failure (D-04) because the forward steps have real dependencies — there is
/// no point switching audio or launching the companion app if the monitor never actually
/// disabled — while ToggleToNormalMode is isolate-and-continue (D-05, unchanged since
/// gap-closure 03-04) because each restore step recovers independent, unrelated hardware
/// state and a failure in one should not block attempting the others. This asymmetry is
/// intentional and must not be "fixed" into false symmetry.
///
/// The shared <see cref="ReconcileModeAfterMonitorFailure"/> helper preserves the CR-01
/// "never let the mode flag misrepresent whether the display was really touched" safety
/// net — reachable from BOTH toggle directions now that both call the same guarded
/// DeactivateMonitors.
/// </summary>
public sealed class ToggleService
{
    private readonly ISettingsStore _settingsStore;
    private readonly IModeStore _modeStore;
    private readonly IMonitorController _monitorController;
    private readonly IAudioController _audioController;
    private readonly IAppController _appController;

    public ToggleService(
        ISettingsStore settingsStore,
        IModeStore modeStore,
        IMonitorController monitorController,
        IAudioController audioController,
        IAppController appController)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _modeStore = modeStore ?? throw new ArgumentNullException(nameof(modeStore));
        _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
        _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
        _appController = appController ?? throw new ArgumentNullException(nameof(appController));
    }

    /// <summary>
    /// Loads settings, captures the current monitor + audio state, saves that snapshot
    /// (BEFORE any mutation — CORE-03), then disables the configured monitor, switches
    /// the default audio device (if configured), and launches/focuses the companion app
    /// (if configured) — stop-on-first-failure (D-04): the first mutation step to throw
    /// is recorded as Failed and every step after it is recorded as NotAttempted, with no
    /// rollback and no further steps attempted. Phase 15/AUDIO-03/APP-04: Audio and App
    /// are optional — a null/empty RigAudioDeviceId or CompanionAppPath records a Skipped
    /// step instead of running (and does not block the next step); the companion-app
    /// existence check now runs inside the App step body rather than as a top-level
    /// preflight, so D-04's "always 3 steps" holds even for a configured-but-missing path.
    /// </summary>
    public ToggleResult ToggleToRigMode()
    {
        var settings = _settingsStore.Load();

        if (!IsFullyConfigured(settings))
        {
            // Guard against WR-01: without this check, an unconfigured (null-field)
            // AppSettings would proceed straight into a real mutation attempt with
            // nothing meaningful configured.
            throw new InvalidOperationException(
                "Rig Toggle settings are not fully configured. Open Settings and choose at least one monitor to disable or enable before switching to Rig Mode.");
        }

        // Phase 15/D-04: the companion-app-path existence check used to be a top-level
        // preflight throw here — that made a broken app path block the entire toggle
        // (including monitor disable) and produced zero ToggleResult steps, contradicting
        // D-04's "the result always has all 3 steps." The check now lives inside the App
        // step body below (TryExecuteOptionalStep), running only when a path is configured.

        // Pattern 2 (16-RESEARCH.md): capture a pre-mutation baseline for the shared
        // CR-01 reconcile helper, not to persist a restore payload — the mode flag is
        // now written only AFTER the Monitor step's real outcome is known, never before.
        var monitorState = _monitorController.CaptureState();

        var steps = new List<ToggleStepResult>();

        var disableSet = (settings.MonitorsToDisable ?? new List<string>()).ToHashSet();
        var enableSet = (settings.MonitorsToEnable ?? new List<string>()).ToHashSet();

        // Debug session monitor-position-regre, round 18 (item 1a): live-filter BEFORE
        // handing either set to ActivateMonitors/DeactivateMonitors — see
        // LiveFilterMonitorSets' own remarks for the full round-17/18 rationale.
        (disableSet, enableSet, IReadOnlyList<string> staleDevicePaths) = LiveFilterMonitorSets(disableSet, enableSet);

        // 06-RESEARCH.md Pitfall 2: ActivateMonitors MUST run BEFORE DeactivateMonitors.
        // ApplyTopology(Extend) (used internally by ActivateMonitors) restores the CCD
        // persistence database's last-known extend layout, which still contains the
        // disable-set monitors as active because DeactivateMonitors uses
        // saveToDatabase:false — running Activate after Deactivate would silently undo
        // the disable. Kept as a single "Monitor" step closure below (not two separate
        // steps) so the ToggleResult checklist still reports one Monitor step, not two
        // (Phase 5 per-step, not per-sub-action, granularity).
        if (!TryExecuteStep("Monitor", () =>
            {
                // Debug session monitor-position-resets-to-de (Symptom 2, round 3):
                // monitorSwapDisableSet is passed through as-is whenever this ActivateMonitors
                // call is immediately followed, right here, by a DeactivateMonitors call
                // against a non-empty (largely-disjoint) set — the ordinary Rig-mode full-swap
                // shape. See IMonitorController's doc comment for the full rig-log-confirmed
                // rationale (the implementation now excludes this set from the survivors it
                // preserves, so one scoped ApplyPathInfos call both activates the enable-set
                // and implicitly deactivates the disable-set).
                _monitorController.ActivateMonitors(enableSet, monitorSwapDisableSet: disableSet);
                _monitorController.DeactivateMonitors(disableSet);
            }, steps))
        {
            // D-04 stop-on-first-failure: a failed Disable means Audio/App never run —
            // no point switching audio or launching the companion app on a monitor that
            // never actually disabled.
            steps.Add(new ToggleStepResult("Audio", ToggleStepOutcome.NotAttempted, null));
            steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));

            // CR-01/Pattern 2 (16-RESEARCH.md): the shared reconcile helper recaptures
            // MonitorState and refrains from writing a new mode when the topology
            // changed but doesn't cleanly match either target — the mode flag never
            // claims a mode the display didn't actually reach.
            ReconcileModeAfterMonitorFailure(monitorState);

            return new ToggleResult(steps) { StaleMonitorsSkipped = staleDevicePaths };
        }

        // Mode is written only after a confirmed successful Monitor step (Pattern 2),
        // mirrored identically in ToggleToNormalMode below.
        TrySaveMode(Models.ToggleMode.Rig);

        // Phase 15/AUDIO-03/APP-04: Audio and App are now optional in both directions.
        // TryExecuteOptionalStep records a Skipped step (not blocking) when the field is
        // unset, otherwise delegates to TryExecuteStep — preserving the exact same
        // stop-on-first-failure short-circuit shape as before: a Failed Audio step still
        // blocks App (NotAttempted), a Skipped Audio step does not.
        if (!TryExecuteOptionalStep("Audio", settings.RigAudioDeviceId, deviceId =>
            {
                if (_audioController.TryResolveDevice(deviceId) is null)
                {
                    throw new InvalidOperationException(
                        "The configured Rig-mode audio device could not be found. Open Settings and reselect it.");
                }

                _audioController.SetDefault(deviceId);
            }, steps))
        {
            steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));
            return new ToggleResult(steps) { StaleMonitorsSkipped = staleDevicePaths };
        }

        TryExecuteOptionalStep("App", settings.CompanionAppPath, path =>
            {
                if (!File.Exists(path))
                {
                    // D-05 (relocated from the old top-level preflight, Phase 15/D-04):
                    // a missing/moved companion-app path fails as a real step, not before
                    // any state is captured — this is a fail-fast UX guard, not a security
                    // control (T-03-09 accepts the TOCTOU window between this check and
                    // the later Process.Start in WindowsAppController.LaunchOrFocus).
                    throw new InvalidOperationException(
                        $"The companion app could not be found at '{path}'. Open Settings and reselect the companion app path before switching to Rig Mode.");
                }

                _appController.LaunchOrFocus(path);
            }, steps);

        return new ToggleResult(steps) { StaleMonitorsSkipped = staleDevicePaths };
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
            // IN-02 (code review): traced for the same reason as the restore-path
            // catch blocks in ToggleToNormalMode below — no other logging exists in
            // this codebase, and a Trace breadcrumb outlives the single
            // ToggleStepResult/MessageBox prompt this failure is also surfaced through.
            System.Diagnostics.Trace.WriteLine($"{stepName} step failed: {ex}");
            steps.Add(new ToggleStepResult(stepName, ToggleStepOutcome.Failed, ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Phase 15/D-03: extends TryExecuteStep with a "configured at all?" guard, rather
    /// than duplicating its try/catch/trace logic. When <paramref name="configuredValue"/>
    /// is null/empty, records a distinct Skipped step (the user deliberately left this
    /// target unconfigured — never NotAttempted, which means "blocked by an earlier
    /// failure") and returns true, since Skipped does not block the chain, the same as
    /// Succeeded. Otherwise delegates to TryExecuteStep so a configured-but-broken target
    /// still surfaces as a real Failed step (AUDIO-05/APP-05).
    /// </summary>
    private static bool TryExecuteOptionalStep(
        string stepName,
        string? configuredValue,
        Action<string> action,
        List<ToggleStepResult> steps)
    {
        if (string.IsNullOrEmpty(configuredValue))
        {
            steps.Add(new ToggleStepResult(stepName, ToggleStepOutcome.Skipped, null));
            return true;
        }

        return TryExecuteStep(stepName, () => action(configuredValue), steps);
    }

    /// <summary>
    /// Debug session monitor-position-regre, round 18 (item 1a): live-filters
    /// <paramref name="disableSet"/>/<paramref name="enableSet"/> against a fresh
    /// IMonitorController.GetAllMonitors() enumeration BEFORE either set is ever handed to
    /// ActivateMonitors/DeactivateMonitors. Round 17 confirmed, by direct code read, that
    /// both ToggleToRigMode and ToggleToNormalMode previously passed settings.json's raw
    /// MonitorsToDisable/MonitorsToEnable (or NormalMonitorsToDisable/NormalMonitorsToEnable)
    /// straight through with no live check of any kind — WindowsMonitorController's own
    /// early-availability guard then throws IMMEDIATELY the instant ANY requested path
    /// isn't currently detected, which — combined with the Monitor step being a single
    /// atomic unit — blocked the ENTIRE toggle (including every still-live monitor in the
    /// same batch) over one stale entry (round 17: SAM748A/DELA0B8, superseded identities
    /// SettingsForm's own deliberate union-merge design preserves forever with no in-app
    /// removal control — see SettingsForm.GetStaleSavedDevicePaths/ShowStaleMonitorWarning).
    ///
    /// Design decision (round 18, documented in the debug file): filter out the stale
    /// path(s) and PROCEED with whatever remains live, rather than failing the whole
    /// action — this matches SettingsForm's own already-established, non-blocking
    /// "settings preserved; reconnect the display to manage it here" philosophy for a
    /// stale entry (it never blocks Save over a stale monitor either), and keeps the
    /// legitimate, still-live monitors working, which is what matters day-to-day. The
    /// caller (ToggleToRigMode/ToggleToNormalMode) surfaces the returned stale list via
    /// ToggleResult.StaleMonitorsSkipped so the UI can name exactly which path(s) were
    /// skipped, instead of the previous opaque "not detected" exception message.
    ///
    /// GetAllMonitors() returns active AND currently OS-disabled-but-available displays
    /// (IMonitorController's own doc comment) — a monitor that is merely OS-disabled right
    /// now (not physically gone) is correctly still treated as live here, exactly matching
    /// WindowsMonitorController's own IsAvailable-based "is this device known to Windows at
    /// all" oracle. If GetAllMonitors() itself throws (an enumeration hiccup, matching
    /// MainForm's own defensive posture for the identical call in
    /// ToggleSwitch_ActionRequested's confirm-dialog name-resolution), this degrades to
    /// "treat every requested path as live" — i.e. skip filtering entirely and fall
    /// through to the exact pre-round-18 behavior for this call — rather than let a
    /// transient enumeration failure block or partially break the toggle on its own.
    /// </summary>
    private (HashSet<string> LiveDisableSet, HashSet<string> LiveEnableSet, IReadOnlyList<string> StaleDevicePaths) LiveFilterMonitorSets(
        HashSet<string> disableSet, HashSet<string> enableSet)
    {
        HashSet<string> livePaths;
        try
        {
            livePaths = _monitorController.GetAllMonitors().Select(m => m.DevicePath).ToHashSet();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"LiveFilterMonitorSets: GetAllMonitors failed, skipping stale-path filtering for this call (falls through to pre-round-18 behavior unfiltered): {ex}");
            return (disableSet, enableSet, Array.Empty<string>());
        }

        var staleDevicePaths = disableSet.Concat(enableSet).Where(dp => !livePaths.Contains(dp)).Distinct().ToArray();

        if (staleDevicePaths.Length == 0)
        {
            return (disableSet, enableSet, Array.Empty<string>());
        }

        System.Diagnostics.Trace.WriteLine(
            $"LiveFilterMonitorSets: filtering stale device path(s) not currently detected: [{string.Join(", ", staleDevicePaths)}] -- proceeding with the remaining live monitor(s) only, instead of blocking the whole toggle.");

        return (
            disableSet.Where(livePaths.Contains).ToHashSet(),
            enableSet.Where(livePaths.Contains).ToHashSet(),
            staleDevicePaths);
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
    /// Shared CR-01 safety net (16-RESEARCH.md Pattern 2), called from BOTH
    /// ToggleToRigMode's and ToggleToNormalMode's Monitor-step failure paths — both
    /// directions now call the same guarded DeactivateMonitors, so both can hit a
    /// zero-survivors (or other) guard failure and both need the same protection
    /// against the mode flag misrepresenting whether the display was really touched.
    /// Recaptures MonitorState and compares against the state captured before the
    /// mutation attempt:
    /// - Unchanged (a pre-mutation guard threw before any real CCD mutation): the
    ///   mode flag is left exactly as-is — nothing needs undoing because nothing
    ///   happened.
    /// - Changed (a real, possibly partial mutation did happen): the mode flag is
    ///   still left at its PRIOR value rather than guessing a new one — the physical
    ///   topology no longer cleanly matches either configured target, and this phase
    ///   deliberately does not introduce a third "Indeterminate" mode value.
    /// - Re-capture itself throws: same fail-safe posture — do nothing, leave the
    ///   mode flag as-is rather than guess.
    /// In every sub-case the mode flag is simply never written here — unlike the
    /// retired pre-mutation snapshot design, there is nothing to "clear."
    ///
    /// 16-REVIEW.md WR-03: the three cases below were previously observationally
    /// indistinguishable (all three did nothing). Each now emits its own Trace
    /// diagnostic, using this file's established fully-qualified Trace.WriteLine
    /// idiom (see the other step-failure call sites above), so debug.log can
    /// distinguish no-change/safe from partial-mutation from recapture-failure —
    /// the mode-flag behavior itself (never write here) is unchanged.
    /// </summary>
    private void ReconcileModeAfterMonitorFailure(Models.MonitorState before)
    {
        try
        {
            if (MonitorStateUnchanged(before, _monitorController.CaptureState()))
            {
                System.Diagnostics.Trace.WriteLine(
                    "Monitor step failed with no observable topology change; mode flag deliberately left at its prior value.");
                return;
            }

            // Partial mutation: leave the mode flag at its prior value (Assumptions
            // Log A3, 16-RESEARCH.md) rather than guess a new mode.
            System.Diagnostics.Trace.WriteLine(
                "Monitor step failed after a partial topology mutation; mode flag deliberately left at its prior value rather than guessed.");
        }
        catch (Exception ex)
        {
            // Re-capture failed — can't confirm anything, same fail-safe posture as
            // the original CR-01 catch block: do nothing, leave the mode flag as-is.
            System.Diagnostics.Trace.WriteLine(
                $"Monitor state re-capture failed during reconcile; nothing could be confirmed, mode flag left as-is: {ex}");
        }
    }

    /// <summary>
    /// WR-04 (code review): _modeStore.Save() runs immediately after the Monitor
    /// step has already made a real, successful physical change — an unguarded write
    /// failure here (disk full, sharing violation, AV lock) would propagate out of
    /// ToggleToRigMode()/ToggleToNormalMode() entirely, breaking D-04's "the result
    /// always has all 3 steps" contract and leaving Audio/App never attempted after
    /// the display was already mutated. Traced (IN-02 convention) and swallowed so
    /// the caller always gets a full ToggleResult; the persisted mode flag may then
    /// lag the physical state until a later successful toggle corrects it, which is
    /// the same fail-safe posture ReconcileModeAfterMonitorFailure already uses above
    /// for the read side of this exact ambiguity.
    /// </summary>
    private void TrySaveMode(Models.ToggleMode mode)
    {
        try
        {
            _modeStore.Save(mode);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to persist mode flag ({mode}): {ex}");
        }
    }

    /// <summary>
    /// True when the monitor set is configured (at least one monitor in either
    /// MonitorsToDisable or MonitorsToEnable). Phase 15/D-05: Audio and App targets are
    /// now genuinely optional (AUDIO-03/AUDIO-04/APP-04) — leaving them unset never blocks
    /// a toggle in either direction, so this gate no longer checks them. Only the
    /// monitor-disable step is safety-relevant and non-optional. Exposed publicly so
    /// MainForm can pre-check before offering "Switch to Rig Mode" at all, rather than
    /// relying solely on the exception thrown by ToggleToRigMode above.
    /// </summary>
    public bool IsSettingsConfigured() => IsFullyConfigured(_settingsStore.Load());

    // D-07: an enable-only or disable-only configuration is fully configured — a
    // single required MonitorDevicePath no longer exists (v1.1 generalizes to
    // arbitrary disable/enable sets), so this checks that at least one of the two
    // sets is non-empty rather than requiring a specific monitor. Phase 15/D-05: the
    // audio/app field requirements have been dropped entirely — see IsSettingsConfigured
    // doc comment above.
    private static bool IsFullyConfigured(Models.AppSettings settings) =>
        settings.MonitorsToDisable?.Count > 0 || settings.MonitorsToEnable?.Count > 0;

    /// <summary>
    /// Applies the explicit Normal-mode monitor set (DISPLAY-10) — mirrors
    /// ToggleToRigMode's Monitor-step shape exactly, against
    /// settings.NormalMonitorsToDisable/NormalMonitorsToEnable instead of Rig's
    /// MonitorsToDisable/MonitorsToEnable. No snapshot restore path remains: a
    /// monitor not listed in either Normal-mode set is left untouched (D-01), the
    /// same documented default Rig mode already uses. Audio applies
    /// settings.NormalAudioDeviceId via the same forward-mode SetDefault call the
    /// Rig-mode path uses (unchanged since Phase 15/AUDIO-04), skipped when unset.
    /// The App step minimizes the companion app if running (or Skipped if unset,
    /// Phase 15/APP-04). Isolate-and-continue (D-05): every step here is attempted
    /// regardless of whether an earlier one failed, and no step throws — each
    /// outcome is recorded as a ToggleStepResult instead (D-02).
    ///
    /// Monitor-step failure is NOT swallowed (04-CONTEXT.md D-05): a failed Monitor
    /// step means the physical display state may not match either mode, so it is
    /// recorded as a Failed step so MainForm's checklist can surface it. The shared
    /// ReconcileModeAfterMonitorFailure helper (Pattern 2) decides whether the mode
    /// flag should be left as-is — see that method's own doc comment. Audio is not
    /// attempted when Monitor fails (D-05: no further recovery is attempted once the
    /// display state itself is in question).
    ///
    /// Gap-closure 03-04 (AUDIO-02 recovery / APP-03 reachability, T-03-04-02): audio
    /// failure keeps its original swallow-and-continue behavior — a genuinely-gone
    /// audio device (unplugged) can never succeed on retry, so MinimizeIfRunning must
    /// still run afterward instead of getting permanently stuck. This does NOT apply
    /// to the Monitor step above it.
    /// </summary>
    public ToggleResult ToggleToNormalMode()
    {
        var settings = _settingsStore.Load();
        var steps = new List<ToggleStepResult>();

        // Pattern 2 (16-RESEARCH.md): pre-mutation baseline for the shared CR-01
        // reconcile helper — the mode flag is written only after the Monitor step's
        // real outcome is known.
        var monitorState = _monitorController.CaptureState();

        var disableSet = (settings.NormalMonitorsToDisable ?? new List<string>()).ToHashSet();
        var enableSet = (settings.NormalMonitorsToEnable ?? new List<string>()).ToHashSet();

        // Debug session monitor-position-regre, round 18 (item 1a): same live-filter as
        // ToggleToRigMode above — confirmed (round 17/18) this Normal-mode Monitor step
        // reads settings.NormalMonitorsToDisable/NormalMonitorsToEnable via the identical
        // unfiltered-pass-to-ActivateMonitors/DeactivateMonitors shape, so it is equally
        // exposed to a stale device path blocking the whole toggle. See
        // LiveFilterMonitorSets' own remarks for the full rationale.
        (disableSet, enableSet, IReadOnlyList<string> staleDevicePaths) = LiveFilterMonitorSets(disableSet, enableSet);

        Exception? monitorFailure = null;
        try
        {
            // Same 06-RESEARCH.md Pitfall 2 ordering constraint as ToggleToRigMode's
            // Monitor step: ActivateMonitors MUST run BEFORE DeactivateMonitors. Same
            // monitor-position-resets-to-de (Symptom 2, round 3) monitorSwapDisableSet signal
            // as ToggleToRigMode's Monitor step above — this is the reverse-direction call
            // where the rig log showed both the round-5 scoped path throwing PathChangeException
            // outright AND (round 3) the Extend fallback it led to failing to activate the
            // requested target while reactivating an unrelated, independently-disabled one.
            _monitorController.ActivateMonitors(enableSet, monitorSwapDisableSet: disableSet);
            _monitorController.DeactivateMonitors(disableSet);
        }
        catch (Exception ex)
        {
            // IN-02 (code review): traced for the same reason as every other step
            // failure in this codebase — no other logging exists, and a Trace
            // breadcrumb outlives the single ToggleStepResult/MessageBox prompt.
            System.Diagnostics.Trace.WriteLine($"Normal-mode monitor apply failed: {ex}");
            monitorFailure = ex;
        }

        steps.Add(new ToggleStepResult(
            "Monitor",
            monitorFailure is null ? ToggleStepOutcome.Succeeded : ToggleStepOutcome.Failed,
            monitorFailure?.Message));

        if (monitorFailure is not null)
        {
            // D-05: no further recovery is attempted once the Monitor step fails —
            // Audio/App below are skipped (NotAttempted), and the shared reconcile
            // helper decides the mode-flag outcome.
            ReconcileModeAfterMonitorFailure(monitorState);
            steps.Add(new ToggleStepResult("Audio", ToggleStepOutcome.NotAttempted, null));
            steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));
            return new ToggleResult(steps) { StaleMonitorsSkipped = staleDevicePaths };
        }

        // Mode is written only after a confirmed successful Monitor step (Pattern 2),
        // mirrored identically in ToggleToRigMode above.
        TrySaveMode(Models.ToggleMode.Normal);

        // Phase 15/AUDIO-04: applies settings.NormalAudioDeviceId via SetDefault, the
        // same optional-target pattern Rig mode already uses for RigAudioDeviceId,
        // skipped when unset. Isolate-and-continue preserved: a failure here does not
        // block MinimizeIfRunning below.
        Exception? audioFailure = null;
        ToggleStepOutcome audioOutcome;
        if (string.IsNullOrEmpty(settings.NormalAudioDeviceId))
        {
            audioOutcome = ToggleStepOutcome.Skipped;
        }
        else
        {
            try
            {
                if (_audioController.TryResolveDevice(settings.NormalAudioDeviceId) is null)
                {
                    throw new InvalidOperationException(
                        "The configured Normal-mode audio device could not be found. Open Settings and reselect it.");
                }

                _audioController.SetDefault(settings.NormalAudioDeviceId);
                audioOutcome = ToggleStepOutcome.Succeeded;
            }
            catch (Exception ex)
            {
                // Gap-closure 03-04: audio failure is intentionally swallowed
                // (continues rather than short-circuiting) — see class-level remarks.
                // Traced for the same reason as every other step failure now (IN-02
                // code review): this codebase has no other logging, and a Trace
                // breadcrumb outlives the single ToggleStepResult/MessageBox prompt.
                System.Diagnostics.Trace.WriteLine($"Audio switch failed, continuing: {ex}");
                audioFailure = ex;
                audioOutcome = ToggleStepOutcome.Failed;
            }
        }

        steps.Add(new ToggleStepResult("Audio", audioOutcome, audioFailure?.Message));

        // CompanionAppPath can be null/empty if settings.json is corrupted
        // (JsonSettingsStore.Load() degrades to a blank AppSettings on any read
        // failure) — propagating null into MinimizeIfRunning would throw.
        if (!string.IsNullOrEmpty(settings.CompanionAppPath))
        {
            // CR-02 (code review): wrapped in try/catch, matching the class doc's own
            // "isolate-and-continue... no step throws" invariant (D-05).
            try
            {
                _appController.MinimizeIfRunning(settings.CompanionAppPath);
                steps.Add(new ToggleStepResult("App", ToggleStepOutcome.Succeeded, null));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"App minimize failed: {ex}");
                steps.Add(new ToggleStepResult("App", ToggleStepOutcome.Failed, ex.Message));
            }
        }
        else
        {
            // Phase 15/D-03/D-04: an unset companion app path is a deliberate choice
            // (APP-04), not blocked-by-an-earlier-failure — Skipped, never NotAttempted.
            steps.Add(new ToggleStepResult("App", ToggleStepOutcome.Skipped, null));
        }

        return new ToggleResult(steps) { StaleMonitorsSkipped = staleDevicePaths };
    }

    /// <summary>
    /// True when the current mode is unambiguously known — the mode file exists and
    /// parsed successfully. False when the file is missing or corrupted, per D-06:
    /// the app fails loudly (via the startup dialog) rather than silently defaulting
    /// to a mode. Exposed for MainForm's toggle-trigger guards (Plan 04).
    /// </summary>
    public bool IsModeKnown() => _modeStore.TryLoad() is not null;

    /// <summary>
    /// Current mode is read from IModeStore (DISPLAY-11), not derived from
    /// snapshot-file presence. Thin convenience wrapper — does NOT default a
    /// null/corrupted mode to Normal; callers needing to distinguish "known Normal"
    /// from "unknown" must use IsModeKnown() first.
    /// </summary>
    public bool IsInRigMode() => _modeStore.TryLoad() == Models.ToggleMode.Rig;
}
