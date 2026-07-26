---
phase: 05-orchestration-full-toggle-packaging
reviewed: 2026-07-25T18:52:43Z
depth: standard
files_reviewed: 10
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml
  - src/RigToggle.App/RigToggle.App.csproj
  - src/RigToggle.Core/Models/ToggleResult.cs
  - src/RigToggle.Core/Models/ToggleStepOutcome.cs
  - src/RigToggle.Core/Models/ToggleStepResult.cs
  - src/RigToggle.Core/ToggleService.cs
  - src/RigToggle.Tests/Doubles/FakeControllers.cs
  - src/RigToggle.Tests/ToggleServiceTests.cs
  - src/RigToggle.Windows/WindowsMonitorController.cs
findings:
  critical: 1
  warning: 4
  info: 3
  total: 8
status: issues_found
---

# Phase 05: Code Review Report

**Reviewed:** 2026-07-25T18:52:43Z
**Depth:** standard
**Files Reviewed:** 10
**Status:** issues_found

## Summary

Reviewed the orchestration layer (`ToggleService`), its `ToggleResult`/`ToggleStepResult` models, the `MainForm` UI wiring, the real CCD monitor controller (`WindowsMonitorController`), the publish/packaging config, and the associated tests/fakes.

The stop-on-first-failure vs. isolate-and-continue asymmetry between `ToggleToRigMode` and `ToggleToNormalMode` is well-documented inline and intentional — not flagged. The `Restore()` rewrite to a two-step `ApplyTopology(Extend)` + live-object reposition fallback is sound in its documented scope, but it left dead code behind (`CopyOutputTechnology`/`AssignSource`) from the earlier manual-reconstruction approach.

The most significant finding is a genuine state-integrity bug: because the CORE-03 "snapshot before mutation" guarantee saves the snapshot unconditionally before `Monitor.Disable()` runs, a *pre-mutation validation failure* in `Disable()` (e.g. the configured monitor is the only active display, or is not currently active — both throw before any `ApplyPathInfos` call) still leaves the snapshot file on disk. Since `IsInRigMode()` is derived purely from snapshot-file presence, `MainForm` immediately reports "Mode: Rig" / offers "Switch to Normal Mode" even though nothing on the machine actually changed — directly undermining the app's core reliability promise. See CR-01.

There is also a latent gap in `ToggleToNormalMode()` for the "called while not actually in rig mode" case (unreachable via the current UI, since `MainForm` gates the call behind `IsInRigMode()`, but reachable by any other/future caller of the public `ToggleService` API) — see WR-01.

## Critical Issues

### CR-01: Snapshot is persisted (flipping IsInRigMode to true) even when Monitor.Disable() fails before mutating anything

**File:** `src/RigToggle.Core/ToggleService.cs:74-91` (in conjunction with `src/RigToggle.Windows/WindowsMonitorController.cs:108-130` and `src/RigToggle.Core/ToggleService.cs:266`)

**Issue:** `ToggleToRigMode()` captures state and calls `_snapshotStore.Save(...)` (line 78) *before* attempting `_monitorController.Disable(...)` (line 82), per the documented CORE-03 crash-safety guarantee. That part is correct. However, `WindowsMonitorController.Disable()` has two early-exit validation guards that throw *before* calling `PathInfo.ApplyPathInfos` at all:
- line 111-115: target monitor not currently active
- line 119-130: target monitor is the only active display

In both cases, nothing on the machine has changed — no CCD mutation was attempted. Yet the snapshot was already saved at `ToggleService.cs:78`, and `IsInRigMode()` (`ToggleService.cs:266`) is defined purely as `_snapshotStore.Exists()`. Back in `MainForm.BtnToggle_Click`, `RefreshUi()` runs immediately after the failed `ToggleToRigMode()` call and will show `"Mode: Rig"` / `"Switch to Normal Mode"`, at the exact same moment the follow-up MessageBox says `"Monitor: FAILED (...)"`. The user is told, simultaneously, that rig mode is both active and failed. The monitor is still enabled, audio was never switched, and the companion app was never launched — but the UI's primary mode indicator says otherwise. This directly contradicts the app's core value of "just as reliably restoring everything to exactly how it was" — here it doesn't even reliably report what state it's actually in.

**Fix:** After a failed Monitor step in `ToggleToRigMode`, don't leave the just-written snapshot as the sole source of truth for "are we in rig mode" when nothing was actually mutated. For example, distinguish "no mutation attempted" failures from "partial mutation, verification failed" failures (e.g. via exception type/marker), and clear the snapshot again in the former case:

```csharp
if (!TryExecuteStep("Monitor", () => _monitorController.Disable(settings.MonitorDevicePath!), steps))
{
    steps.Add(new ToggleStepResult("Audio", ToggleStepOutcome.NotAttempted, null));
    steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));

    // Nothing may have actually been mutated (e.g. WindowsMonitorController.Disable's
    // pre-mutation validation guards throw before any ApplyPathInfos call) — re-verify
    // against live state before trusting the just-saved snapshot as "we are in rig mode".
    if (!ActuallyDiffersFromCaptured(monitorState, _monitorController.CaptureState()))
    {
        _snapshotStore.Clear();
    }

    return new ToggleResult(steps);
}
```
(or equivalent: have `Disable()` expose whether it mutated anything, and only keep the snapshot when it did). At minimum, `MainForm` should not label the app "Mode: Rig" in the same breath as reporting "Monitor: FAILED".

## Warnings

### WR-01: ToggleToNormalMode() called while not actually in rig mode skips Monitor/Audio steps but still runs App-minimize and Clear()

**File:** `src/RigToggle.Core/ToggleService.cs:176-260`

**Issue:** When `wasInRigMode` is `false` and `snapshot` is `null` (no snapshot ever existed), the `if (snapshot is null) { if (wasInRigMode) throw ...; }` block (lines 185-194) does nothing — no `ToggleStepResult` is added for "Monitor" or "Audio" — and execution falls straight through to the companion-app block (lines 239-255) and `_snapshotStore.Clear()` (line 257). The returned `ToggleResult` then has only 1 `Steps` entry ("App") instead of the documented "up to 3-entry shape" (the class doc for the `App` `NotAttempted` case explicitly claims this invariant), and `_appController.MinimizeIfRunning(...)` is invoked even though nothing was ever toggled to rig mode.

This path is currently unreachable through the shipped UI, since `MainForm.BtnToggle_Click` only calls `ToggleToNormalMode()` when `_toggleService.IsInRigMode()` is already `true`. But `ToggleService` is a public class and `ToggleToNormalMode()` is a public method with no such precondition documented or enforced — any other caller (future UI feature, a hotkey handler, a retry button, a test) that calls it while already in Normal mode will silently minimize the companion app and report a misleading 1-step, `Success == false` result (since a `NotAttempted` "App" step, when the companion path is unset, makes `Steps.All(... Succeeded)` false even though nothing needed doing).

**Fix:** Add an explicit early return when `!wasInRigMode`:
```csharp
if (snapshot is null)
{
    if (wasInRigMode)
    {
        throw new InvalidOperationException(...);
    }

    // Nothing to restore — never was in rig mode. Don't touch the companion app or
    // call Clear() on a snapshot that never existed.
    return new ToggleResult(new List<ToggleStepResult>());
}
```

### WR-02: Dead code — CopyOutputTechnology/AssignSource are no longer called by production logic

**File:** `src/RigToggle.Windows/WindowsMonitorController.cs:337-386`

**Issue:** `CopyOutputTechnology` (lines 350-363) and `AssignSource` (lines 370-386) are `internal static` helpers whose doc comments claim they are "the one piece of `Restore()`'s reconstruction logic that's fully unit-testable without live display hardware." However, the current `Restore()` implementation (per its own comments at lines 231-243) was rewritten to *never* manually reconstruct `PathTargetInfo`/mode-info from scratch — it now only reuses real, live-queried `PathInfo`/`PathTargetInfo` objects wholesale via the two-step `ApplyTopology(Extend)` + reposition approach. Neither `Restore()` nor `Disable()` calls `CopyOutputTechnology` or `AssignSource` anywhere in this file (confirmed via full-repo grep — the only other references are their own unit tests in `RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs`). These are leftovers from an earlier iteration of `Restore()` and no longer reflect what the shipped code does; the "tested directly ... a routine WindowsDisplayAPI package upgrade could silently reintroduce this exact bug" rationale in the comment no longer applies to any reachable production code path.

**Fix:** Either remove both methods (and their now-purposeless tests) if the manual-reconstruction path is permanently retired, or, if they're being kept as a documented "known good fallback we might need again," update the doc comments to say so explicitly instead of claiming they are currently exercised by `Restore()`.

### WR-03: ApplyTopology(Extend) call has no diagnostic wrapper, unlike the sibling ApplyPathInfos call a few lines later

**File:** `src/RigToggle.Windows/WindowsMonitorController.cs:264`

**Issue:** `PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false);` is called with no surrounding `try`/`catch`. If this native CCD topology switch throws, the raw library exception propagates unmodified up through `ToggleService.TryExecuteStep`/`ToggleToNormalMode`'s catch blocks and becomes the `Reason` shown to the user verbatim. Contrast this with the `ApplyPathInfos(corrected.ToArray(), ...)` call 40 lines later (lines 303-316), which is explicitly wrapped to add diagnostic context ("Extra diagnostic detail beyond the library's generic message, since ValidatePathInfos discards the underlying Win32 error code entirely."). Given this is exactly the kind of rig-hardware-dependent failure this codebase has gone to unusual lengths to make diagnosable elsewhere, the `ApplyTopology` call is a gap in that same effort — a failure here (only reachable in the crash-recovery fallback path) will surface a comparatively cryptic message to a user who has no way to reproduce or debug it themselves.

**Fix:**
```csharp
try
{
    PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false);
}
catch (Exception ex)
{
    throw new InvalidOperationException(
        $"Monitor restore failed while switching to Extend topology: {ex.Message}", ex);
}
```

### WR-04: ToggleService constructor does not null-check its injected dependencies

**File:** `src/RigToggle.Core/ToggleService.cs:27-39`

**Issue:** `MainForm`'s constructor explicitly guards every injected dependency with `?? throw new ArgumentNullException(nameof(...))` (`MainForm.cs:31-35`), establishing a project convention for fail-fast composition-root validation. `ToggleService`'s constructor takes five dependencies (`ISettingsStore`, `ISnapshotStore`, `IMonitorController`, `IAudioController`, `IAppController`) and assigns them directly with no such check. A misconfigured composition root (or a future refactor that passes a nullable value) would not fail at construction time; it would instead surface as a `NullReferenceException` from deep inside `ToggleToRigMode`/`ToggleToNormalMode`, with a far less actionable stack trace than an immediate `ArgumentNullException` at startup.

**Fix:**
```csharp
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
```

## Info

### IN-01: Redundant condition in Disable()'s primary-repositioning branch

**File:** `src/RigToggle.Windows/WindowsMonitorController.cs:133`

**Issue:** `if (targetPath.IsGDIPrimary && survivors.Length > 0)` — `survivors.Length > 0` is always true at this point, because the method already throws and returns above (lines 119-130) whenever `survivors.Length == 0`. The extra clause is dead weight that could mislead a future reader into thinking the empty-survivors case is still possible here.

**Fix:** Drop the redundant clause: `if (targetPath.IsGDIPrimary)`.

### IN-02: Inconsistent failure tracing — only audio-restore failures are written to Trace

**File:** `src/RigToggle.Core/ToggleService.cs:207-219`

**Issue:** Only the audio-restore failure path in `ToggleToNormalMode` calls `System.Diagnostics.Trace.WriteLine(...)` (line 217), justified by the comment "a swallowed exception here would otherwise be forensically invisible." But this exception isn't actually swallowed — it's captured in `audioFailure`, added to `steps` as a `Failed` `ToggleStepResult`, and surfaced to the user via `MainForm`'s checklist MessageBox, same as the (untraced) monitor-restore failure a few lines above and every `TryExecuteStep` failure in `ToggleToRigMode`. The stated rationale for special-casing audio doesn't hold, and the asymmetry means only one of several structurally-identical failure paths gets a `Trace` breadcrumb.

**Fix:** Either trace all step failures consistently (e.g. inside `TryExecuteStep` and both restore `catch` blocks), or remove the one-off `Trace.WriteLine` call and rely solely on the `ToggleStepResult.Reason` surfaced to the user, for consistency.

### IN-03: Unmanaged temp file in test fixture

**File:** `src/RigToggle.Tests/ToggleServiceTests.cs:19`

**Issue:** `private static readonly string ExistingCompanionAppPath = Path.GetTempFileName();` creates a real temp file on disk for the whole test-class lifetime and never deletes it — every test run leaks one more file into the OS temp directory. Not a reliability problem for the tests themselves, but worth cleaning up.

**Fix:** Implement `IDisposable`/`IClassFixture<T>` (or an `xunit` `IAsyncLifetime`) to delete the temp file after the test class runs, e.g.:
```csharp
public class ToggleServiceTests : IDisposable
{
    private static readonly string ExistingCompanionAppPath = Path.GetTempFileName();
    public void Dispose() => File.Delete(ExistingCompanionAppPath);
    ...
}
```

---

_Reviewed: 2026-07-25T18:52:43Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
