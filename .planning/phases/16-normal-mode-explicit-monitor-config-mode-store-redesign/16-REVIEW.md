---
phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign
reviewed: 2026-08-08T00:00:00Z
depth: standard
files_reviewed: 18
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/Program.cs
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/SettingsForm.Designer.cs
  - src/RigToggle.App/StartupRecoveryChecker.cs
  - src/RigToggle.Core/Abstractions/IModeStore.cs
  - src/RigToggle.Core/Abstractions/IToggleInProgressStore.cs
  - src/RigToggle.Core/Models/AppSettings.cs
  - src/RigToggle.Core/Models/ToggleInProgressMarker.cs
  - src/RigToggle.Core/Models/ToggleMode.cs
  - src/RigToggle.Core/Persistence/JsonModeStore.cs
  - src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs
  - src/RigToggle.Core/ToggleOrchestrator.cs
  - src/RigToggle.Core/ToggleService.cs
  - src/RigToggle.Tests/Doubles/InMemoryStores.cs
  - src/RigToggle.Tests/ToggleOrchestratorTests.cs
  - src/RigToggle.Tests/ToggleServiceTests.cs
  - src/RigToggle.Windows/WindowsMonitorController.cs
findings:
  critical: 2
  warning: 5
  info: 1
  total: 8
status: issues_found
---

# Phase 16: Code Review Report

**Reviewed:** 2026-08-08
**Depth:** standard
**Files Reviewed:** 18
**Status:** issues_found

## Summary

Reviewed the mode-store redesign (explicit `IModeStore` replacing snapshot-presence
inference), the new explicit Normal-mode monitor set, the `IToggleInProgressStore`
crash marker, the blocking startup-recovery dialogs, and the re-flowed
`SettingsForm.Designer.cs` layout. The core `ToggleService`/`ToggleOrchestrator`
sequencing logic (mode-write-only-after-success, D-04 stop-on-first-failure,
D-05 isolate-and-continue) is well tested and internally consistent with its own
doc comments.

Two blocker-level issues were found: (1) the crash-marker `Clear()` call runs
*before* the busy-flag reset inside `ToggleOrchestrator`'s `finally` block, and
`JsonToggleInProgressStore.Clear()` is unguarded — a single I/O failure (locked
file, AV, permissions) on marker cleanup permanently wedges the orchestrator in
"busy," rejecting every future toggle for the rest of the process's lifetime,
which is exactly the failure mode the surrounding doc comments say must never
happen. (2) The new explicit Normal-mode monitor set has no migration path from
existing `MonitorsToDisable`/`MonitorsToEnable` settings and no toggle-time or
save-time guard analogous to `IsSettingsConfigured()` — any user (upgrading or
new) who configures only the Rig grid and never touches the mirrored Normal grid
gets a silent no-op "Switch to Normal Mode" that leaves the primary monitor
OS-disabled indefinitely, directly contradicting the project's stated core value
("just as reliably restores everything to exactly how it was before").

Additional warnings cover an incomplete exception catch set in the new JSON
stores, an enum-deserialization gap that can defeat the "fail loudly on
corruption" design goal, dead/misleading branching in the shared CR-01 reconcile
helper, an unguarded `_modeStore.Save()` call that can desync persisted vs.
physical state, and duplicated toggle-trigger logic across three MainForm
handlers.

## Critical Issues

### CR-01: Crash-marker `Clear()` can permanently wedge the reentrancy guard

**File:** `src/RigToggle.Core/ToggleOrchestrator.cs:86-101`
**Issue:** `RunGuarded`'s `finally` block calls `_markerStore.Clear()` *before*
`Volatile.Write(ref _busy, 0)`:

```csharp
finally
{
    _markerStore.Clear();
    Volatile.Write(ref _busy, 0);
}
```

`JsonToggleInProgressStore.Clear()` (`src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs:62-68`)
is completely unguarded:

```csharp
public void Clear()
{
    if (Exists())
    {
        File.Delete(_path);
    }
}
```

`File.Delete` can throw (`IOException` from an AV lock or another process holding
the handle, `UnauthorizedAccessException` from ACLs, etc.). If it does, the
exception propagates out of the `finally` block *before* `Volatile.Write` ever
runs, so `_busy` is left at `1` forever. Every subsequent `ToggleToRigMode()`/
`ToggleToNormalMode()` call — from the button, tray menu, or hotkey — will then
throw `ToggleInProgressException` for the rest of the process's lifetime, with no
way to recover short of restarting the app. This is the exact scenario the
class's own doc comment (lines 24-29) says must never happen ("otherwise a single
failed toggle would permanently wedge the orchestrator in 'busy' for the rest of
the app's lifetime") — the guarantee is stated but not actually upheld once
marker cleanup can fail.

**Fix:** Never let marker cleanup risk the busy-flag release. Either swallow
`Clear()` failures (matching `TryLoad()`'s own degrade-to-null philosophy) or use
a nested `try/finally` so the busy flag always clears regardless:

```csharp
finally
{
    try
    {
        _markerStore.Clear();
    }
    catch
    {
        // Best-effort marker cleanup — must never block the busy-flag release.
    }
    finally
    {
        Volatile.Write(ref _busy, 0);
    }
}
```
and/or wrap `File.Delete` in `JsonToggleInProgressStore.Clear()` with a
try/catch consistent with `TryLoad()`'s IOException handling.

### CR-02: No migration or safeguard for the new explicit Normal-mode monitor set — silent "restore" no-op

**File:** `src/RigToggle.Core/ToggleService.cs:313-361`, `src/RigToggle.App/MainForm.cs:307-311`, `src/RigToggle.App/SettingsForm.cs:888-913`
**Issue:** `ToggleToNormalMode()` now applies `settings.NormalMonitorsToDisable`/
`NormalMonitorsToEnable` instead of restoring a captured snapshot (DISPLAY-10).
When both sets are empty/null (the default for every existing `settings.json`
written before this phase, and for any new user who configures only the Rig
grid), `DeactivateMonitors(emptySet)`/`ActivateMonitors(emptySet)` are no-ops
(`WindowsMonitorController.cs:209`, `270`) — the Monitor step is reported
`Succeeded` even though nothing happened to the display. This is exercised (and
implicitly endorsed) by `ToggleServiceTests.ToggleToNormalMode_RunsAllSteps_EvenWhenNeverInRigMode`,
whose `ConfiguredSettings` fixture has no `NormalMonitorsToDisable`/`ToEnable` at
all.

Three compounding gaps make this a real-world trap rather than a theoretical one:

1. **No settings migration**: `JsonSettingsStore.Load()`'s D-08 migration block
   only seeds `MonitorsToDisable` from the legacy `MonitorDevicePath` — there is
   no analogous seeding of `NormalMonitorsToDisable`/`ToEnable`. Every user
   upgrading from a pre-Phase-16 build will have a fully-configured Rig set and
   an empty Normal set.
2. **No save-time guard**: `SettingsForm.ValidateSettingsForm()` explicitly makes
   `monitorNormalOk` unconditionally `true` regardless of whether the Normal set
   is empty (`src/RigToggle.App/SettingsForm.cs:895`, comment: "an all-empty
   Normal config is valid").
3. **No toggle-time guard**: unlike the Rig-mode branch of `BtnToggle_Click`,
   which checks `_orchestrator.IsSettingsConfigured()` before allowing the
   toggle (`src/RigToggle.App/MainForm.cs:313-325`), the Normal-mode branch
   (`src/RigToggle.App/MainForm.cs:307-311`) has no equivalent check at all.

Net effect: a user disables their primary monitor via "Switch to Rig Mode," then
clicks "Switch to Normal Mode" expecting their monitor back — and it silently
stays disabled, with the mode indicator now claiming "Mode: Normal." This is the
project's single stated core value ("A single reliable action... and just as
reliably restores everything to exactly how it was before," per CLAUDE.md)
being broken by omission, with no error, warning, or log to point the user at
the cause.

**Fix:** At minimum, add a toggle-time guard mirroring `IsSettingsConfigured()`
for the Normal direction (e.g. warn/block when both `NormalMonitorsToDisable`
and `NormalMonitorsToEnable` are empty while `MonitorsToDisable`/`ToEnable` are
non-empty), and/or have `SettingsForm` default-populate the Normal grid's
Disable column with the Rig grid's Enable/Disable selections mirrored (Disable ↔
Enable swap) the first time a monitor set is configured, so the common "mirror
Rig mode" case works without the user having to notice and separately fill in a
second grid. Consider also a one-time settings-migration step that seeds
`NormalMonitorsToDisable` from the legacy `MonitorDevicePath` (or from
`MonitorsToEnable`) for upgrading installs.

## Warnings

### WR-01: `TryLoad()` in the new JSON stores does not catch `UnauthorizedAccessException`

**File:** `src/RigToggle.Core/Persistence/JsonModeStore.cs:50-61`, `src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs:48-59`
**Issue:** Both `TryLoad()` implementations only catch `JsonException` and
`IOException`:

```csharp
catch (JsonException) { return null; }
catch (IOException) { return null; }
```

`UnauthorizedAccessException` derives from `SystemException`, not `IOException`
— it is a sibling, not a subclass — so a permissions problem reading
`mode.json`/`toggle-in-progress.json` (e.g. restrictive ACLs, a security product
quarantining the file) is not caught here. `StartupRecoveryChecker.Run()`
(`src/RigToggle.App/StartupRecoveryChecker.cs:29-31`) calls `modeStore.TryLoad()`
and `markerStore.TryLoad()` with no surrounding try/catch — deliberately, per its
own doc comment, since this is meant to be the one place that "fails loudly."
But an uncaught `UnauthorizedAccessException` here means the app crashes with an
unhandled-exception dialog at the very first line of `Main()`'s recovery check,
instead of the intended graceful "mode unknown, check manually" `MessageBox`
this code was written to guarantee.

**Fix:** Catch `UnauthorizedAccessException` alongside `IOException` in both
`TryLoad()` implementations (or catch the common `SystemException`/`IOException`
superset intentionally), matching the class doc's claim of "an interrupted read"
degrading gracefully.

### WR-02: Corrupted `mode.json` with an out-of-range integer silently deserializes instead of failing

**File:** `src/RigToggle.Core/Persistence/JsonModeStore.cs:50-57`
**Issue:** `ToggleMode` is serialized as its underlying `int` (0/1) by
`JsonSerializer.Serialize(mode)` since no `JsonStringEnumConverter` is
registered. `System.Text.Json` deserializes any JSON integer into an enum-typed
property without validating that the value corresponds to a defined enum member
— e.g. a hand-edited or corrupted file containing `2` deserializes successfully
to `(ToggleMode)2`, not `null`. `IsModeKnown()`
(`src/RigToggle.Core/ToggleService.cs:435`) would then report `true` and
`IsInRigMode()` would report `false` (since `(ToggleMode)2 != ToggleMode.Rig`),
silently defaulting to the "Normal" display and skipping the "mode unknown"
startup dialog and toggle-trigger guard entirely — the opposite of the D-06/D-07
"fail loudly, never silently default to a mode" design goal this whole class
exists to satisfy.

**Fix:** Validate the deserialized value with `Enum.IsDefined(typeof(ToggleMode), value)`
in `TryLoad()` and return `null` when it isn't a recognized member.

### WR-03: `ReconcileModeAfterMonitorFailure`'s unchanged/changed branches are functionally identical

**File:** `src/RigToggle.Core/ToggleService.cs:246-263`
**Issue:**

```csharp
private void ReconcileModeAfterMonitorFailure(Models.MonitorState before)
{
    try
    {
        if (MonitorStateUnchanged(before, _monitorController.CaptureState()))
        {
            return;
        }

        // Partial mutation: leave the mode flag at its prior value ...
    }
    catch
    {
        // Re-capture failed — ...
    }
}
```

Both the "unchanged" branch (early `return`) and the "changed" branch (falls
through to the end of the `try`, i.e. the method also just returns) do exactly
the same thing: nothing. Neither branch ever calls `_modeStore.Save(...)`. The
comparison via `MonitorStateUnchanged` — which performs a real, non-trivial CCD
`CaptureState()` re-query — is computed purely to decide between two paths that
are observationally indistinguishable. This is not incorrect today (the method's
own doc comment confirms "the mode flag is simply never written here" in every
sub-case), but the branching makes the code read as though the two cases are
handled differently when they are not, and the recapture-and-compare work is
currently pure overhead.

**Fix:** Either give the two branches genuinely different behavior (e.g. trace a
diagnostic message distinguishing "nothing changed, safe" from "partial mutation
detected, leaving mode as-is" for future debugging), or collapse the method to
make the "always leave the mode flag alone" behavior explicit and drop the now-
pointless comparison, with a comment explaining that the check itself is
retained for future extensibility rather than to gate current behavior.

### WR-04: Unguarded `_modeStore.Save(...)` can desync persisted mode from physical state and break the "always 3 steps" contract

**File:** `src/RigToggle.Core/ToggleService.cs:124`, `src/RigToggle.Core/ToggleService.cs:361`
**Issue:** In both `ToggleToRigMode()` and `ToggleToNormalMode()`, `_modeStore.Save(...)`
is called directly, with no try/catch, immediately after the Monitor step's real
mutation calls have already succeeded. `JsonModeStore.Save()` performs a
`File.WriteAllText` + `File.Move(..., overwrite: true)`, either of which can
throw (disk full, sharing violation, AV lock). If that happens here, the
exception propagates all the way out of `ToggleToRigMode()`/`ToggleToNormalMode()`
— no `ToggleResult` is ever returned (Audio/App steps never run and are never
recorded), which contradicts the class's own documented D-04 invariant that "the
result always has all 3 steps." Worse, the physical monitor state has already
been changed successfully by this point, but the persisted mode flag was never
updated — leaving `IsInRigMode()` reporting the stale prior mode indefinitely,
until some later successful toggle happens to correct it.

**Fix:** Wrap the `_modeStore.Save(...)` call in a try/catch, at minimum tracing
the failure (matching the `IN-02` convention already used throughout this file)
and still returning a `ToggleResult` — e.g. treat a mode-write failure as part of
the Monitor step's own outcome, or add explicit handling so the caller always
receives a checklist rather than an unstructured exception after a partially-
successful mutation.

### WR-05: `IsModeKnown()` toggle-trigger guard duplicated verbatim across three handlers

**File:** `src/RigToggle.App/MainForm.cs:293-305`, `src/RigToggle.App/MainForm.cs:602-650`, `src/RigToggle.App/MainForm.cs:661-709`
**Issue:** The new "unknown mode" guard (check `IsModeKnown()`, show a warning,
`return`) was added identically to `BtnToggle_Click`, `TrayToggleMenuItem_Click`,
and `HandleHotkeyToggle`. The latter two go further: `TrayToggleMenuItem_Click`
(lines 602-650) and `HandleHotkeyToggle` (lines 661-709) are, in their entirety,
line-for-line identical method bodies (mode-check, try/catch around the toggle
call, and the final result-toast) — the only difference between them is the
method name and a doc comment. This is pre-existing duplication that this phase
extended rather than introduced, but three independent copies of the same
control flow means any future fix (e.g. to the `ToggleInProgressException`
message, or the balloon-tip wording) risks being applied to only one or two of
the three call sites.

**Fix:** Extract a single private helper, e.g.
`private void PerformBackgroundToggle(Action<ToggleResult> onSuccess)` or
similar, that both `TrayToggleMenuItem_Click` and `HandleHotkeyToggle` call, and
factor the `IsModeKnown()` check into a small shared guard method reused by all
three trigger handlers.

## Info

### IN-01: Stale doc comments still describe mode as derived from snapshot-file presence

**File:** `src/RigToggle.App/MainForm.cs:9-22`, `src/RigToggle.App/MainForm.cs:251-254`
**Issue:** The class-level doc comment ("Mode is derived from
ToggleOrchestrator.IsInRigMode() ... which itself derives from snapshot-file
presence (D-14)") and `RefreshUi()`'s own doc comment ("Re-derives the mode
indicator (from snapshot-file presence, D-14)") were not updated by this phase.
Mode is now derived from the explicit `IModeStore` (DISPLAY-11), not
snapshot-file presence — this phase's entire purpose was to retire that exact
mechanism. Leaving the old wording is actively misleading for a future reader
trying to understand where the mode value comes from.

**Fix:** Update both comments to reference `IModeStore`/DISPLAY-11 instead of
the retired snapshot-presence/D-14 mechanism.

---

_Reviewed: 2026-08-08_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
