---
phase: 06-multi-monitor-data-model-controller-generalization
reviewed: 2026-07-29T00:00:00Z
depth: standard
files_reviewed: 15
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/MonitorConfirmDialog.cs
  - src/RigToggle.App/MonitorConfirmDialog.Designer.cs
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/SettingsForm.Designer.cs
  - src/RigToggle.Core/Abstractions/IMonitorController.cs
  - src/RigToggle.Core/Models/AppSettings.cs
  - src/RigToggle.Core/Models/MonitorInfo.cs
  - src/RigToggle.Core/Persistence/JsonSettingsStore.cs
  - src/RigToggle.Core/ToggleService.cs
  - src/RigToggle.Tests/Doubles/FakeControllers.cs
  - src/RigToggle.Tests/JsonStoreTests.cs
  - src/RigToggle.Tests/ToggleServiceTests.cs
  - src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs
  - src/RigToggle.Windows/WindowsMonitorController.cs
findings:
  critical: 2
  warning: 3
  info: 3
  total: 8
status: issues_found
---

# Phase 06: Code Review Report

**Reviewed:** 2026-07-29T00:00:00Z
**Depth:** standard
**Files Reviewed:** 15
**Status:** issues_found

## Summary

Reviewed the Phase 6 multi-monitor data-model/controller generalization (N-monitor
disable/enable sets, `GetAllMonitors()`, `ActivateMonitors`/`DeactivateMonitors`, the
Settings grid, the confirmation dialog, and the v1.0→v1.1 settings migration). The CCD
mutation code in `WindowsMonitorController` is unusually well-documented, with extensive
rig-tested rationale baked into comments, and the `ToggleService` sequencing logic
(snapshot-before-mutate, stop-on-first-failure vs. isolate-and-continue) is mostly sound
and well covered by `ToggleServiceTests`.

However, two real defects were found that undercut the app's stated core guarantees:

1. The legacy-settings migration in `JsonSettingsStore.Load()` re-triggers on every load
   whenever `MonitorsToDisable` is an **empty list** (not just `null`), silently
   reintroducing a monitor into the disable set that the user deliberately removed via
   Settings — directly contradicting the app's "restore/respect exactly what the user
   configured" contract.
2. `ToggleService.ToggleToNormalMode()`'s final companion-app minimize step is the only
   step in that method not wrapped in try/catch, contradicting the class's own documented
   "isolate-and-continue... no step throws" invariant — an exception there leaves the
   snapshot uncleared and the UI permanently reporting "Mode: Rig" even after monitor and
   audio were already successfully restored.

Both are backed by tracing the exact code paths and cross-checking the existing test
suite, which does not exercise either scenario.

## Critical Issues

### CR-01: Settings-load migration re-corrupts an intentionally-emptied MonitorsToDisable on every load

**File:** `src/RigToggle.Core/Persistence/JsonSettingsStore.cs:45-49`

**Issue:** The v1.0→v1.1 migration guard is:

```csharp
if (!string.IsNullOrEmpty(loaded.MonitorDevicePath)
    && (loaded.MonitorsToDisable is null || loaded.MonitorsToDisable.Count == 0))
{
    loaded.MonitorsToDisable = new List<string> { loaded.MonitorDevicePath };
}
```

The class doc comment explicitly frames the distinguishing signal as "a genuine legacy
file has no plural fields at all (they deserialize to **null**)... an already-migrated
v1.1 file has a non-empty `MonitorsToDisable`, which must NOT be overwritten" — but the
code also treats an **empty list** (`Count == 0`) the same as `null`.

`AppSettings.MonitorDevicePath`/`MonitorFriendlyName` are documented as "retained ONLY as
the legacy v1.0 migration source... do not delete or repurpose them" and
`SettingsForm.BtnSaveSettings_Click` (`SettingsForm.cs:531-546`) deliberately preserves
`MonitorDevicePath` verbatim on every save, never clearing it.

Concrete reproduction: a user who ever had a legacy `MonitorDevicePath` populated (i.e.
anyone who used the app before Phase 6) later reconfigures Settings to an enable-only
setup, or simply unchecks the "Off (Rig)" box for that monitor. `BtnSaveSettings_Click`
correctly persists `MonitorsToDisable = []` while leaving `MonitorDevicePath` untouched
(per the "legacy fields are migration source only" contract). On the very next
`Load()` — which happens on every app start, every Settings-dialog open, and inside
every `ToggleToRigMode()`/`ToggleToNormalMode()` call — the migration guard fires again
because `MonitorsToDisable.Count == 0`, silently re-inserting the old device path back
into the disable set. The user's explicit choice to stop disabling that monitor is
permanently unpersistable; the app will keep disabling a monitor they removed from the
configuration, directly undermining the "one reliable action... just as reliably
restores everything to exactly how it was before" core value in CLAUDE.md.

`JsonStoreTests.cs` only exercises the "already has one entry" case
(`SettingsStore_Load_DoesNotRemigrate_WhenDisableSetAlreadyPopulated`) — there is no test
for "legacy path set + plural field explicitly emptied," which is exactly the case that
is broken.

**Fix:** Only re-migrate when the field was never persisted at all — the JSON model
needs to be able to distinguish "field absent from the file" from "field present but
empty." Deserialize into a sentinel/nullable check on the raw JSON, or (simpler) change
`AppSettings.MonitorsToDisable` migration to check only `is null`, and have
`BtnSaveSettings_Click` always assign an explicit `new List<string>()` (never leave the
field `null` once the app has ever gone through Settings) so `null` unambiguously means
"pre-Phase-6 legacy file, never migrated":

```csharp
// JsonSettingsStore.Load()
if (!string.IsNullOrEmpty(loaded.MonitorDevicePath) && loaded.MonitorsToDisable is null)
{
    loaded.MonitorsToDisable = new List<string> { loaded.MonitorDevicePath };
}
```

Add a regression test asserting that a saved empty `MonitorsToDisable` survives a
subsequent `Load()` unchanged when `MonitorDevicePath` is still populated.

---

### CR-02: ToggleToNormalMode's companion-app minimize/clear tail is not exception-safe, contradicting its own documented invariant

**File:** `src/RigToggle.Core/ToggleService.cs:342-355`

**Issue:** The class doc comment states: "Isolate-and-continue (D-05): unlike
ToggleToRigMode's stop-on-first-failure, every restore step here is attempted regardless
of whether an earlier one failed, and **no step throws** — each outcome is recorded as a
ToggleStepResult instead." The Monitor-restore step (`ToggleService.cs:273-301`) and the
Audio-restore step (`ToggleService.cs:303-317`) are both correctly wrapped in try/catch
and recorded as `ToggleStepResult`s. The final App step is not:

```csharp
if (!string.IsNullOrEmpty(settings.CompanionAppPath))
{
    _appController.MinimizeIfRunning(settings.CompanionAppPath);
    steps.Add(new ToggleStepResult("App", ToggleStepOutcome.Succeeded, null));
}
else
{
    steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));
}

_snapshotStore.Clear();
```

If `IAppController.MinimizeIfRunning` throws (a live Win32 window-manipulation call —
`ShowWindow`/`SetForegroundWindow` on a process that may have exited or changed state
between the `IsRunning` check and this call is a realistic failure mode, and the CLAUDE.md
tech notes explicitly call out that a "running but no window to manipulate" case must not
"fail the whole toggle over it"), the exception propagates out of `ToggleToNormalMode()`
uncaught. At that point Monitor and Audio have *already been successfully restored*, but:

- `_snapshotStore.Clear()` never runs, so `IsInRigMode()` (snapshot-presence-derived, D-14)
  keeps reporting `true` — the UI is now permanently stuck showing "Mode: Rig" and
  offering "Switch to Normal Mode" even though normal mode was already restored.
- The caller (`MainForm.BtnToggle_Click`) falls into the generic catch-all
  (`MainForm.cs:142-160`), showing a raw exception message instead of the CORE-04
  per-step checklist the rest of this method is built to produce.
- A subsequent retry of "Switch to Normal Mode" would re-run `Restore()` against monitor
  hardware that is already back to normal, which is at best a no-op and at worst produces
  spurious/inconsistent step results.

`FakeAppController` (`FakeControllers.cs`) has no `throwOnMinimize` option and
`ToggleServiceTests.cs` has no test simulating this — the untested path is exactly the
one carrying the bug.

**Fix:** Wrap the minimize call the same way Monitor/Audio are wrapped, and always run
`_snapshotStore.Clear()` from a `finally`/unconditional path so a companion-app failure
can't strand the mode indicator:

```csharp
Exception? appFailure = null;
if (!string.IsNullOrEmpty(settings.CompanionAppPath))
{
    try
    {
        _appController.MinimizeIfRunning(settings.CompanionAppPath);
        steps.Add(new ToggleStepResult("App", ToggleStepOutcome.Succeeded, null));
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine($"App minimize failed: {ex}");
        appFailure = ex;
        steps.Add(new ToggleStepResult("App", ToggleStepOutcome.Failed, ex.Message));
    }
}
else
{
    steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));
}

_snapshotStore.Clear();
return new ToggleResult(steps);
```

## Warnings

### WR-01: Primary CCD mutation calls lack the diagnosability wrapper applied elsewhere in the same file

**File:** `src/RigToggle.Windows/WindowsMonitorController.cs:237` (`ActivateMonitors`), `:340` (`DeactivateMonitors`)

**Issue:** `RestoreViaReconstruction()` explicitly wraps both its `ApplyTopology(Extend)`
call (lines 498-510, labeled "WR-03 (code review)") and its `ApplyPathInfos` call (lines
575-588) in try/catch to convert the library's "raw, comparatively cryptic exception"
into a diagnosable `InvalidOperationException` with contextual detail — because, per the
inline comment, "ValidatePathInfos discards the underlying Win32 error code entirely."
`ActivateMonitors`'s `PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, ...)` call
(line 237) and `DeactivateMonitors`'s `PathInfo.ApplyPathInfos(...)` call (line 340) — the
two calls that perform the app's single most critical operation, the actual monitor
disable — have no equivalent wrapper. A CCD failure there surfaces to the user via
`MainForm`'s generic catch-all with only the library's undecorated message, exactly the
outcome WR-03 was created to avoid elsewhere in this same class. Per the class's own
docs, these failures are "otherwise unreproducible without rig hardware," making the lost
diagnosability especially costly.

**Fix:** Apply the same try/catch-and-rewrap pattern used in `RestoreViaReconstruction` to
the `ApplyTopology`/`ApplyPathInfos` calls in `ActivateMonitors` and `DeactivateMonitors`.

### WR-02: Settings-load exceptions outside JsonSettingsStore's narrow catch list are unhandled

**File:** `src/RigToggle.App/SettingsForm.cs:60` (`SettingsForm_Load`), `src/RigToggle.App/MainForm.cs:185-187` (`BtnSettings_Click`); root cause in `src/RigToggle.Core/Persistence/JsonSettingsStore.cs:53-64`

**Issue:** `JsonSettingsStore.Load()` only catches `JsonException` and `IOException`.
`UnauthorizedAccessException` (e.g. antivirus lock, restrictive ACL, OneDrive sync
conflict on `%LocalAppData%`) does **not** derive from `IOException` and is not caught,
so `Load()` can throw despite the file existing. `MainForm.BtnToggle_Click` wraps its
`_settingsStore.Load()` call inside a broad `try/catch (Exception ex)`
(`MainForm.cs:58-160`), so that call site is safe. But:

- `SettingsForm.SettingsForm_Load` (`SettingsForm.cs:57-66`) calls
  `_settingsStore.Load()` with no surrounding try/catch.
- `MainForm.BtnSettings_Click` (`MainForm.cs:180-188`) calls `settingsForm.ShowDialog(this)`
  — which raises `Load` synchronously — with no surrounding try/catch either.

An exception here propagates unhandled out of the Settings dialog's `Load` event, with no
local recovery, unlike every other settings-load call site in the app.

**Fix:** Either broaden `JsonSettingsStore.Load()`'s catch list to cover
`UnauthorizedAccessException`/`SecurityException` (matching its documented "never
throws" intent), or wrap `SettingsForm_Load`'s body / `BtnSettings_Click`'s `ShowDialog`
call in a try/catch that degrades gracefully (e.g., shows a MessageBox and closes the
dialog) instead of letting the exception surface as an unhandled crash.

### WR-03: MonitorConfirmDialog's fixed-size label can silently clip the informed-consent message for larger monitor counts

**File:** `src/RigToggle.App/MonitorConfirmDialog.cs:1-14`, `src/RigToggle.App/MonitorConfirmDialog.Designer.cs:41-44`

**Issue:** The class doc comment states the dialog names "every monitor in both sets by
friendly name (full, comma-separated, **never truncated**)" — but `lblMessage` is created
with `AutoSize = false` and a fixed `Size(360, 72)` in the Designer, with no
`AutoEllipsis`, scroll, or dynamic resize logic in the constructor. This was written for
the Phase 6 generalization from a single named monitor to arbitrary N-monitor
disable/enable sets, so the message length is no longer bounded by a single friendly
name — a realistic 3-4 monitor rig with longer manufacturer/model names in both the
disable and enable clauses can exceed the label's fixed bounds, silently clipping text
that is supposed to be a safety confirmation the user relies on before their display
topology changes.

**Fix:** Either size `lblMessage`/the dialog dynamically based on the rendered text
(`TextRenderer.MeasureText`) before `ShowDialog`, or use `AutoSize = true` with a
max-width constraint and let the dialog grow vertically to fit.

## Info

### IN-01: Null-forgiving operator on DragEventArgs.Data assumes a non-null value that isn't actually guaranteed

**File:** `src/RigToggle.App/SettingsForm.cs:466`

**Issue:** `TryGetSingleDroppedLaunchTarget` does `if (!e.Data!.GetDataPresent(...))`. `DragEventArgs.Data` is typed `IDataObject?`; while unlikely in the common Explorer-drag case, some non-standard drag sources can supply a null/empty data object, which would throw a `NullReferenceException` from inside a `DragEnter`/`DragDrop` handler rather than degrading to "reject the drop."

**Fix:** `if (e.Data is null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return false;`

### IN-02: Reentrant sibling-checkbox write causes ValidateSettingsForm to run twice per check-event

**File:** `src/RigToggle.App/SettingsForm.cs:159-191`

**Issue:** `OnMonitorCellValueChanged` sets the sibling cell's `Value` directly
(`row.Cells[siblingIndex].Value = false;`), which itself synchronously re-raises
`CellValueChanged` for that cell. The reentrancy guard correctly suppresses the recursive
sibling-clearing logic, but the unconditional `ValidateSettingsForm();` call at the end of
the method still runs once for the recursive (guarded) invocation and once more for the
original invocation — a harmless but avoidable double validation pass on every
check-a-checkbox interaction.

**Fix:** Move the `ValidateSettingsForm();` call inside the outer `if` guard's `else`
branch structure, or short-circuit with `if (_updatingMonitorGridProgrammatically) return;`
at the top of the method.

### IN-03: Rig-hardened fallback helpers are permanently unreachable from production code

**File:** `src/RigToggle.Windows/WindowsMonitorController.cs:663-727` (`CopyOutputTechnology`, `AssignSource`)

**Issue:** Both methods carry `internal` visibility, dedicated unit tests, and doc
comments explicitly noting they are "NOT currently called by DeactivateMonitors() or
Restore() in this file" and kept only as a "documented known-good fallback." This is
intentional and well-justified per the comments, but is still dead code from a
call-graph/coverage perspective — worth a periodic sanity check that it doesn't silently
bit-rot (e.g. via a future `WindowsDisplayAPI` upgrade changing `PathTargetInfo`'s backing
field name, which `CopyOutputTechnology` already guards against by throwing).

**Fix:** No action required now; consider a comment/issue link to track removal if a
`WindowsDisplayAPI` upgrade ever definitively rules out needing the manual-reconstruction
fallback again.

---

_Reviewed: 2026-07-29T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
