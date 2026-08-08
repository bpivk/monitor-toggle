---
phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign
verified: 2026-08-08T12:03:44Z
status: human_needed
score: 3/4 must-haves fully verified, 1 verified-in-code but requiring live-hardware confirmation
overrides_applied: 0
gaps: []
human_verification:
  - test: "Kill the RigToggle process (Task Manager 'End Task') mid-toggle on the real rig, then relaunch"
    expected: "A blocking startup dialog states the last toggle to {Rig/Normal} Mode didn't finish cleanly and that no auto-retry was attempted; after clicking OK, %LocalAppData%\\RigToggle\\toggle-in-progress.json is gone (marker cleared)"
    why_human: "DISPLAY-13's real crash-mid-toggle scenario was never exercised end-to-end on Windows hardware. The Plan 05 rig checkpoint attempted a hand-typed toggle-in-progress.json instead of a real process kill; that hand-typed file did not round-trip (ToggleMode serializes as a plain int via System.Text.Json, not a string, so the hand-typed JSON failed to deserialize and degraded to null by design) — root-caused as not a code defect in 16-05-SUMMARY.md, but the actual detect-and-dialog flow remains unconfirmed on real hardware. No App-tier automated test project exists for StartupRecoveryChecker.cs (WinForms MessageBox, not unit-testable without a Windows test host), so this can only be confirmed by a human on the rig."
  - test: "Re-open Settings on the real rig (current side-by-side grid layout, post commit 098d10c) and confirm no control overlap/clipping across the full dialog, and that the Normal grid themes correctly in dark mode"
    expected: "Rig Mode and Normal Mode grids sit side by side with 'Off'/'On' headers, all downstream controls (audio, app path, hotkey, checkboxes, Save/Discard) visible and unclipped, and the Normal grid matches the Rig grid's dark-mode colors"
    why_human: "The originally rig-tested layout (stacked grids, D-04/D-05) was found deficient (narrow/tall) and replaced inline by commit 098d10c with a new side-by-side layout. 16-05-SUMMARY.md explicitly states this replacement layout's build was 'checked in the dev environment, not on the rig — the visual re-flow itself has not been re-confirmed on real hardware.' Layout overlap/clipping and dark-theme rendering are not compiler-catchable (16-RESEARCH.md Pitfall 4) and require visual confirmation on the actual Windows rig."
---

# Phase 16: Normal-Mode Explicit Monitor Config & Mode-Store Redesign Verification Report

**Phase Goal:** Normal mode gets its own explicitly configured monitor set — symmetric with Rig mode's existing config — applied directly on toggle instead of restoring a pre-toggle snapshot, and the app's notion of "which mode am I in" becomes an explicit persisted flag instead of a proxy inferred from snapshot-file presence, with a lightweight crash-recovery marker covering a toggle interrupted mid-flight.
**Verified:** 2026-08-08T12:03:44Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DISPLAY-09: User can configure Normal-mode monitors in Settings, symmetric to Rig | ✓ VERIFIED | `AppSettings.NormalMonitorsToDisable`/`NormalMonitorsToEnable` exist (`src/RigToggle.Core/Models/AppSettings.cs:25-26`); `SettingsForm.Designer.cs` declares `pnlMonitorNormal`/`dgvMonitorsNormal`/`colDisableNormal`/`colEnableNormal`; `SettingsForm.cs` has `PopulateMonitorGridNormal`, `GetGridSelectionNormal`, save-merge writing `NormalMonitorsToDisable`/`ToEnable`, and `ThemeApplier.ThemeMonitorGrid(dgvMonitorsNormal, ...)` at both Load and OnThemeChanged (2 call sites confirmed via grep). Rig-checkpoint (16-05-SUMMARY.md) confirmed on real hardware, with an inline layout/naming fix applied post-checkpoint (commit `098d10c`). **Caveat:** the fixed layout was not re-verified on the rig — see Human Verification item 2. |
| 2 | DISPLAY-10: Toggle-to-Normal applies the explicit Normal set directly, never a snapshot restore | ✓ VERIFIED | `ToggleService.ToggleToNormalMode()` (`src/RigToggle.Core/ToggleService.cs:337-451`) builds `disableSet`/`enableSet` from `settings.NormalMonitorsToDisable`/`NormalMonitorsToEnable` and calls `ActivateMonitors` then `DeactivateMonitors` — no `Restore()`/`StateSnapshot` reference anywhere in the method or the file (`grep _snapshotStore src/RigToggle.Core/ToggleService.cs` → 0 matches). `ToggleOrchestratorTests.ToggleToNormalMode_Idle_DelegatesToToggleServiceAndReturnsItsResult` asserts `monitor.ActivateMonitors`/`monitor.DeactivateMonitors` call-log entries (not `monitor.Restore`). Rig-confirmed (16-05-SUMMARY.md check #2: "Confirmed"). |
| 3 | DISPLAY-11: Mode reported correctly after restart, independent of snapshot-file presence | ✓ VERIFIED | `ToggleService.IsInRigMode()`/`IsModeKnown()` read only `_modeStore.TryLoad()` (`ToggleService.cs:459,467`). `grep "snapshotStore\." src/RigToggle.App/ src/RigToggle.Core/` shows `snapshotStore` is referenced only once, for the one-time bootstrap `.Exists()` check in `Program.cs:104` — `state.json` is never `Save()`d anywhere in the current codebase, confirming mode is genuinely independent of the snapshot file post-bootstrap. Rig-confirmed (16-05-SUMMARY.md check #3 and #6: mode label correct after restart; bootstrap-from-legacy-state path confirmed). |
| 4 | DISPLAY-13: A crash/kill mid-toggle is detected and communicated on next launch via a persisted marker | ? UNCERTAIN | Mechanism is fully implemented and code-reviewed: `ToggleOrchestrator.RunGuarded` saves `ToggleInProgressMarker` before the pipeline runs and clears it in `finally` (`ToggleOrchestrator.cs:81-113`), deliberately NOT on a real process kill. `JsonToggleInProgressStore` round-trips atomically and degrades corrupted/permission-denied reads to null (`JsonToggleInProgressStore.cs`). `StartupRecoveryChecker.Run()` reads the marker, clears it, then shows the crash-recovery dialog with the correct `{TargetMode}` text (`StartupRecoveryChecker.cs:49-66`). Unit tests cover the `ToggleOrchestrator` marker lifecycle including a CR-01 regression test (`RunGuarded_ReleasesFlag_EvenWhenMarkerClearThrows`, `ToggleOrchestratorTests.cs:237`) — 79/79 tests pass (verified by running `dotnet test` in this session). **However**, the actual end-to-end "kill process mid-toggle → dialog appears on next launch" flow was never exercised on real Windows hardware: the Plan 05 rig checkpoint's attempt used a hand-typed `toggle-in-progress.json` that did not match `System.Text.Json`'s actual int-enum serialization format, so it silently produced no dialog (root-caused as expected `TryLoad()` degrade-to-null behavior, not a defect, per 16-05-SUMMARY.md's own analysis — confirmed correct by this verifier via code inspection of `JsonSerializer.Serialize(marker)` with no `JsonStringEnumConverter` registered). No App-tier automated test project exists to close this gap in CI (`StartupRecoveryChecker.cs`'s `MessageBox.Show` calls are not unit-testable without a Windows test host). Routed to human verification (see frontmatter). |

**Score:** 3/4 truths fully verified from code + rig evidence; 1 truth (DISPLAY-13) verified sound in code/unit-tests but requires a real-hardware crash test to close out end-to-end confirmation.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/RigToggle.Core/Models/ToggleMode.cs` | Two-value enum, Normal first | ✓ VERIFIED | `enum ToggleMode { Normal, Rig }`, doc comment references retired D-14 mechanism correctly |
| `src/RigToggle.Core/Models/ToggleInProgressMarker.cs` | Crash-marker record, distinguishes from `ToggleInProgressException` | ✓ VERIFIED | `record ToggleInProgressMarker(ToggleMode TargetMode, DateTimeOffset StartedAtUtc)`, doc comment explicitly distinguishes the two concepts |
| `src/RigToggle.Core/Abstractions/IModeStore.cs` / `IToggleInProgressStore.cs` | Persistence contracts | ✓ VERIFIED | Both interfaces match plan spec exactly (`Exists/TryLoad/Save` and `TryLoad/Save/Clear`) |
| `src/RigToggle.Core/Persistence/JsonModeStore.cs` / `JsonToggleInProgressStore.cs` | Atomic writes, degrade-to-null | ✓ VERIFIED | Temp-file + `File.Move(overwrite:true)`; `TryLoad()` catches `JsonException`, `IOException`, AND `UnauthorizedAccessException` (WR-01 fix); `Enum.IsDefined` guard against undefined ints (WR-02 fix); `Clear()` wrapped in try/catch (CR-01 fix) |
| `src/RigToggle.Tests/Doubles/InMemoryStores.cs` | In-memory doubles + call-log convention | ✓ VERIFIED | `InMemoryModeStore`/`InMemoryToggleInProgressStore` present, plus a `ThrowingClearToggleInProgressStore` regression double for CR-01 |
| `src/RigToggle.Core/ToggleService.cs` | Rewritten Normal Monitor step, IModeStore-backed, shared reconcile helper, no ISnapshotStore | ✓ VERIFIED | `_snapshotStore` absent (0 matches); `ReconcileModeAfterMonitorFailure` called from both directions; `TrySaveMode` wraps `_modeStore.Save` (WR-04 fix) |
| `src/RigToggle.Core/ToggleOrchestrator.cs` | Marker lifecycle + `IsModeKnown` | ✓ VERIFIED | `RunGuarded(ToggleMode, Func<ToggleResult>)`, marker Save first in try / Clear (nested try/catch, CR-01 fix) first in finally, `IsModeKnown()` pass-through present |
| `src/RigToggle.Windows/WindowsMonitorController.cs` | Mode-agnostic zero-survivors guard text | ✓ VERIFIED | `"Cannot disable all configured monitors — at least one active display must remain."` — no "before switching to Rig Mode" trailer |
| `src/RigToggle.App/StartupRecoveryChecker.cs` | Two blocking startup dialogs | ✓ VERIFIED | Mode-corruption dialog first (marker not checked when mode unknown), crash-marker dialog second (marker cleared before dialog shown), both `owner: null`, LOCKED copy matches UI-SPEC verbatim |
| `src/RigToggle.App/Program.cs` | Store construction, one-time bootstrap, wiring | ✓ VERIFIED | `JsonModeStore`/`JsonToggleInProgressStore` constructed; bootstrap seeds mode only `if (!modeStore.Exists())` from `snapshotStore.Exists() ? Rig : Normal`; `StartupRecoveryChecker.Run` called before `ToggleService`/`ToggleOrchestrator` construction and before the tray-safe timing point |
| `src/RigToggle.App/MainForm.cs` | Mode-known-aware UI + three trigger guards | ✓ VERIFIED | `RefreshUi` renders "Mode: Unknown" branch before the Rig/Normal branch; `BtnToggle_Click`, and (via extracted `PerformBackgroundToggle`, WR-05 fix) `TrayToggleMenuItem_Click`/`HandleHotkeyToggle` all gate on `IsModeKnown()` with LOCKED copy |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `JsonModeStore` | `IModeStore` | implements | ✓ WIRED | `class JsonModeStore : IModeStore` |
| `ToggleService` | `IModeStore` | ctor injection, `TryLoad`/`Save` | ✓ WIRED | `_modeStore.TryLoad()` (read paths), `TrySaveMode` → `_modeStore.Save(mode)` (write, post-success only, both directions) |
| `ToggleOrchestrator` | `IToggleInProgressStore` | `Save` in try, `Clear` in finally | ✓ WIRED | Confirmed at `ToggleOrchestrator.cs:83,101` |
| `ToggleService.ToggleToNormalMode` | `WindowsMonitorController.DeactivateMonitors` | second caller of the guarded zero-survivors path | ✓ WIRED | Both `ToggleToRigMode` and `ToggleToNormalMode` call `ActivateMonitors`→`DeactivateMonitors` through the same `IMonitorController` |
| `Program.cs` | `JsonModeStore`/`JsonToggleInProgressStore` | constructs and injects | ✓ WIRED | `mode.json`/`toggle-in-progress.json` under `basePath`, passed into `ToggleService`/`ToggleOrchestrator` |
| `Program.cs` | `StartupRecoveryChecker` | invokes before `Application.Run` | ✓ WIRED | Called after bootstrap seed, before `ToggleService` construction and the tray-safe timing point — runs on both visible and `--tray` paths |
| `MainForm` | `ToggleOrchestrator.IsModeKnown()` | RefreshUi + 3 trigger guards | ✓ WIRED | 4+ call sites confirmed via grep |
| Settings Normal grid (Plan 02) | `ToggleToNormalMode` explicit apply (Plan 03) | `NormalMonitorsTo*` field flow | ✓ WIRED | Settings save writes the fields; `ToggleToNormalMode` reads the same fields directly from `_settingsStore.Load()` |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full solution builds | `dotnet build RigToggle.sln` (run in this session) | 0 errors, 3 pre-existing xUnit1031 warnings (unrelated to this phase) | ✓ PASS |
| Full Core/Tests suite passes | `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` (run in this session) | 79/79 passed, 99ms | ✓ PASS |
| No stale "always restored" prose remains | `grep -rn "restored exactly as it was before\|Restored automatically when switching back" src/` | 0 matches | ✓ PASS |
| `JsonSettingsStore.cs` untouched by this phase (Pitfall 5) | `git log --oneline -- src/RigToggle.Core/Persistence/JsonSettingsStore.cs` | Last touched by Phase 6 commits, none from Phase 16 | ✓ PASS |
| WinForms UI behavior (dialogs, real toggle, real restart) | N/A — requires Windows host | Not runnable in this Linux verification environment | ? SKIP (routed to human_verification / already partially covered by Plan 05's rig checkpoint) |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|--------------|--------|----------|
| DISPLAY-09 | 16-02 | Normal-mode monitor config, symmetric to Rig | ✓ SATISFIED | Code + rig-confirmed (see Truth #1); layout since replaced by unverified-on-rig fix (see human_verification) |
| DISPLAY-10 | 16-03 | Normal toggle applies configured set directly | ✓ SATISFIED | Code + rig-confirmed (see Truth #2) |
| DISPLAY-11 | 16-01, 16-03, 16-04 | Mode tracked via explicit persisted flag | ✓ SATISFIED | Code + rig-confirmed (see Truth #3) |
| DISPLAY-13 | 16-01, 16-03, 16-04 | Crash-mid-toggle marker detected and communicated | ? NEEDS HUMAN | Mechanism sound in code/unit tests; end-to-end hardware confirmation outstanding (see Truth #4) |

No orphaned requirements: `REQUIREMENTS.md`'s Traceability table maps DISPLAY-09/10/11/13 to Phase 16 and DISPLAY-12 to Phase 17 (not claimed by any Phase 16 plan, correctly out of scope here).

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/RigToggle.Core/ToggleService.cs` | 246-263 | `ReconcileModeAfterMonitorFailure`'s "unchanged" and "changed" branches are functionally identical (WR-03) | ℹ️ INFO | Deliberately deferred per REVIEW.md ("not incorrect today," cosmetic-only) — confirmed still present, not fixed, consistent with documented decision. Not a blocker. |

No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK` debt markers found in any of the 15 phase-modified files (grep swept all key files listed in the plan frontmatter `files_modified`/`key-files`). "Placeholder" hits in `SettingsForm.cs` refer to a text-box placeholder string, not a code stub.

### Code Review Fix Regression Check

All 8 findings from `16-REVIEW.md` were checked against current source, not just the SUMMARY's claim:

| Finding | Claimed Fix Commit | Verified in Current Code |
|---------|--------------------|--------------------------|
| CR-01 (marker `Clear()` can wedge busy flag) | `dd9290a` | ✓ `ToggleOrchestrator.cs`: nested try/catch around `_markerStore.Clear()` before `Volatile.Write`; `JsonToggleInProgressStore.Clear()` guarded with `catch (IOException)`/`catch (UnauthorizedAccessException)`; regression test `RunGuarded_ReleasesFlag_EvenWhenMarkerClearThrows` present and passing |
| CR-02 (no feedback for empty Normal set) | `725333e` | ✓ `SettingsForm.cs:909-919`: advisory-only warning label fires when Rig grid has a selection and Normal grid doesn't — non-blocking, matches REVIEW's "not a migration/guard" resolution note |
| WR-01 (missing `UnauthorizedAccessException` catch) | `55b9a76` | ✓ Both `JsonModeStore.TryLoad()` and `JsonToggleInProgressStore.TryLoad()` now catch it |
| WR-02 (undefined enum silently deserializes) | `55b9a76` | ✓ `JsonModeStore.TryLoad()`: `Enum.IsDefined(typeof(ToggleMode), mode)` guard present |
| WR-03 (dead branch in reconcile helper) | not fixed (deferred, cosmetic) | ✓ Confirmed still present as documented — deliberate non-fix |
| WR-04 (unguarded `_modeStore.Save()`) | `9ab33e5` | ✓ `TrySaveMode` helper wraps `_modeStore.Save` in try/catch with `Trace.WriteLine`, called from both toggle directions |
| WR-05 (duplicated tray/hotkey handlers) | `8356bea` | ✓ `PerformBackgroundToggle` extracted; `TrayToggleMenuItem_Click`/`HandleHotkeyToggle` are now one-line delegations |
| IN-01 (stale snapshot-presence doc comments) | `8356bea` | ✓ `MainForm.cs` class doc comment and `RefreshUi()` doc comment reference `IModeStore`/DISPLAY-11, not snapshot-file presence |

All 6 commits (`dd9290a`, `55b9a76`, `9ab33e5`, `8356bea`, `725333e`, plus gap-closure `098d10c`) verified present in `git log` with matching diffs. No regressions found — `dotnet test` still passes 79/79 after all fixes.

### Human Verification Required

See frontmatter `human_verification` block. Summary:

1. **Real crash-mid-toggle test (DISPLAY-13).** Kill the app process (Task Manager End Task) during an actual toggle on the rig, relaunch, and confirm the crash-recovery dialog fires with the correct target-mode text and the marker file is cleared afterward. This is the one Phase-16 success criterion never demonstrated end-to-end on real hardware — the code mechanism is sound (unit-tested at the `ToggleOrchestrator` boundary, atomic-write/degrade-to-null persistence proven equivalent to `JsonModeStore`'s rig-confirmed mechanism) but the actual dialog-on-crash behavior is unconfirmed.

2. **Re-confirm the post-gap-closure Settings layout (DISPLAY-09).** The side-by-side grid layout (commit `098d10c`) that replaced the originally rig-tested-and-rejected stacked layout has only been confirmed to compile (`dotnet build` in the dev environment) — its actual on-screen appearance (no overlap/clipping, correct dark-mode theming of the Normal grid) has not been re-verified on the rig per 16-05-SUMMARY.md's own admission.

### Gaps Summary

No must-have was found to be missing, stubbed, or unwired in the codebase — every artifact, key link, and code-review fix claimed by the SUMMARYs was independently verified against current source, and the full automated test suite (79/79) passes in this session. The phase is functionally complete and well-implemented.

The one open item is not a code defect but a **verification completeness gap**: DISPLAY-13's crash-detection dialog and the post-fix Settings layout have not been confirmed against real Windows behavior since their most recent code changes. Both are flagged by the phase's own SUMMARY as recommended follow-ups rather than accepted risks (no override with `accepted_by`/`accepted_at` exists in this phase's history), so they are routed here as human-verification items rather than accepted as closed.

---

_Verified: 2026-08-08T12:03:44Z_
_Verifier: Claude (gsd-verifier)_
