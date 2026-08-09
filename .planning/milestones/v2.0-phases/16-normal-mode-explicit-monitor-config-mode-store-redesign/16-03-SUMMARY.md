---
phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign
plan: 03
subsystem: core
tags: [dotnet, toggle-orchestration, mode-store, crash-recovery, monitor-control]

# Dependency graph
requires:
  - phase: 16-01
    provides: IModeStore/IToggleInProgressStore contracts, ToggleMode/ToggleInProgressMarker types, InMemoryModeStore/InMemoryToggleInProgressStore doubles
  - phase: 16-02
    provides: AppSettings.NormalMonitorsToDisable/NormalMonitorsToEnable fields
provides:
  - "ToggleService rewritten: IModeStore-backed mode (constructor drops ISnapshotStore, adds IModeStore), ToggleToNormalMode applies its own explicit symmetric monitor set instead of restoring a snapshot"
  - "Shared ReconcileModeAfterMonitorFailure helper (CR-01 safety net), called from both ToggleToRigMode and ToggleToNormalMode failure paths"
  - "Mode flag written only after a confirmed successful Monitor step, in both toggle directions"
  - "ToggleOrchestrator: crash-in-progress marker lifecycle (Save at guarded-toggle start, Clear in finally) plus IsModeKnown() pass-through"
  - "WindowsMonitorController zero-survivors guard exception text generalized to be mode-agnostic (now reachable from both toggle directions)"
  - "ToggleServiceTests/ToggleOrchestratorTests rewired to the new IModeStore/marker-aware constructors"
affects: [16-04 (Program.cs wiring + startup mode-corruption/crash-recovery dialogs, MainForm mode-known guards), 17 (manual monitor panel, shared safety guard unification), 18 (ISnapshotStore/StateSnapshot/Restore cleanup)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Mode-write-only-after-confirmed-success (Pattern 2, 16-RESEARCH.md) replacing the retired pre-mutation snapshot-save timing"
    - "Shared CR-01 recapture-and-compare reconcile helper reused by both toggle directions, rather than duplicated logic"
    - "ToggleToNormalMode mirrors ToggleToRigMode's Monitor-step shape exactly (ActivateMonitors before DeactivateMonitors) against a distinct settings field pair"

key-files:
  created: []
  modified:
    - src/RigToggle.Core/ToggleService.cs
    - src/RigToggle.Core/ToggleOrchestrator.cs
    - src/RigToggle.Windows/WindowsMonitorController.cs
    - src/RigToggle.Tests/ToggleServiceTests.cs
    - src/RigToggle.Tests/ToggleOrchestratorTests.cs

key-decisions:
  - "IsModeKnown()/IsInRigMode() added to ToggleService as thin wrappers over _modeStore.TryLoad() — IsInRigMode() does NOT default a null/corrupted mode to Normal, preserving the honest 'unknown' signal for Plan 04's startup dialog and MainForm guards"
  - "ReconcileModeAfterMonitorFailure never writes a mode on failure — on a partial mutation it deliberately leaves the mode flag at its PRIOR value rather than introducing a third 'Indeterminate' mode value (16-RESEARCH.md Assumptions Log A3)"
  - "ToggleToNormalMode's old 'never was in rig mode, no-op' branch is fully retired — the method now always attempts its 3 steps regardless of prior mode state; the mode-unknown precondition check moves to Plan 04's startup/MainForm-guard layer, not a per-call ToggleService guard"
  - "ISnapshotStore/StateSnapshot/WindowsMonitorController.Restore are left untouched in this plan (not referenced by ToggleService any more) — full removal is explicitly Phase 18 scope"

patterns-established:
  - "Mode-flag write timing: after confirmed Monitor-step success, never before — mirrored identically in both ToggleToRigMode and ToggleToNormalMode"

requirements-completed: [DISPLAY-10, DISPLAY-11, DISPLAY-13]

# Metrics
duration: ~35min
completed: 2026-08-07
---

# Phase 16 Plan 03: Core Toggle Logic Rewrite (IModeStore + Explicit Normal-Mode Set) Summary

**Rewrote `ToggleService`/`ToggleOrchestrator` to track mode via `IModeStore` (not snapshot-file presence) and to apply Normal mode's own explicit symmetric monitor set instead of restoring a pre-toggle snapshot, with a shared CR-01 safety net now protecting both toggle directions and a disk-persisted crash-in-progress marker wrapping every guarded toggle.**

## Performance

- **Duration:** ~35 min
- **Completed:** 2026-08-07T22:xx:xxZ
- **Tasks:** 3 completed
- **Files modified:** 5

## Accomplishments
- `ToggleService`'s constructor no longer takes `ISnapshotStore` — it takes `IModeStore` instead, and `_snapshotStore` no longer appears anywhere in the file (confirmed via grep)
- `ToggleToNormalMode` is a genuine rewrite: it builds `disableSet`/`enableSet` from `settings.NormalMonitorsToDisable`/`NormalMonitorsToEnable` and calls `ActivateMonitors` then `DeactivateMonitors` — mirroring `ToggleToRigMode`'s Monitor-step shape exactly. The snapshot-null-branch spine, the corrupted-snapshot throw, and the "never was in rig mode, no-op" branch are all removed
- A shared `ReconcileModeAfterMonitorFailure(MonitorState before)` helper extracts and generalizes the CR-01 recapture-and-compare safety net, called from both `ToggleToRigMode`'s and `ToggleToNormalMode`'s Monitor-step failure paths — previously only Rig mode could reach this guard at all
- The mode flag (`_modeStore.Save(...)`) is written only after a confirmed successful Monitor step, identically in both directions — never before mutation, unlike the retired pre-mutation snapshot-save timing
- `ToggleOrchestrator` now takes an `IToggleInProgressStore` and threads a `ToggleMode targetMode` through `RunGuarded`: the crash marker is saved as the first action inside the guarded `try` and cleared as the first action inside `finally` (before the busy-flag reset) — surviving everything except a real process kill, which is exactly the DISPLAY-13 condition it exists to detect at next launch
- `ToggleOrchestrator.IsModeKnown()` pass-through added alongside the existing `IsInRigMode()`/`IsSettingsConfigured()` pass-throughs
- `WindowsMonitorController`'s zero-survivors guard exception text is now mode-agnostic ("Cannot disable all configured monitors — at least one active display must remain.") since `DeactivateMonitors` is reachable from both toggle directions as of this plan
- Both `ToggleServiceTests.cs` and `ToggleOrchestratorTests.cs` are fully rewired to the new constructors and mode-write timing; all 78 tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Rewrite ToggleService — IModeStore-backed mode, Normal Monitor step, shared CR-01 helper, drop ISnapshotStore** - `b769a8b` (feat)
2. **Task 2: Add marker lifecycle + IsModeKnown to ToggleOrchestrator and generalize the guard text** - `380f728` (feat)
3. **Task 3: Rewire BOTH ToggleServiceTests AND ToggleOrchestratorTests to the new ctors/doubles and rewrite snapshot/timing-specific assertions** - `6d35e9b` (test)

_Note: SUMMARY.md commit follows this list — this is a worktree-isolated parallel executor, so STATE.md/ROADMAP.md are excluded from the metadata commit and updated centrally by the orchestrator after merge._

## Files Created/Modified
- `src/RigToggle.Core/ToggleService.cs` - Constructor drops `ISnapshotStore`, adds `IModeStore`; `ToggleToNormalMode` fully rewritten to apply its own explicit set; new `ReconcileModeAfterMonitorFailure` shared helper; `IsModeKnown()` added; `IsInRigMode()` now `_modeStore`-backed; class doc comment replaced (no longer describes the retired D-14/D-02 asymmetries)
- `src/RigToggle.Core/ToggleOrchestrator.cs` - New `IToggleInProgressStore` dependency; `RunGuarded` takes a `ToggleMode targetMode` and saves/clears the crash marker around the guarded pipeline; `IsModeKnown()` pass-through added
- `src/RigToggle.Windows/WindowsMonitorController.cs` - Zero-survivors guard exception message generalized, dropping the Rig-specific "before switching to Rig Mode" trailer
- `src/RigToggle.Tests/ToggleServiceTests.cs` - `CreateService` factory rewired to `InMemoryModeStore`; the three previously snapshot-specific tests rewritten for mode-write-after-success/mode-unchanged-on-partial-mutation semantics; new test covers the CR-01 reconcile path for `ToggleToNormalMode`; the "never was in rig mode" no-op test replaced with a test proving the new always-runs-3-steps behavior; the Normal-mode Restore-then-Deactivate ordering test replaced with an Activate-then-Deactivate ordering test against the new Normal-mode fields
- `src/RigToggle.Tests/ToggleOrchestratorTests.cs` - `CreateOrchestrator` factory rewired to `InMemoryModeStore` + `InMemoryToggleInProgressStore`; the Normal-toggle delegation test and the in-flight pass-through test rewritten for the new mode-write timing; new tests cover the marker Save/Clear lifecycle and `IsModeKnown()`

## Decisions Made
- Followed 16-RESEARCH.md's Pattern 2/4/5 exactly: mode write moved to post-success, `ToggleToNormalMode` mirrors `ToggleToRigMode`'s Monitor-step shape verbatim against the Normal-mode field pair, and `ISnapshotStore` was removed entirely from `ToggleService`'s dependency list
- On a partial-mutation Monitor-step failure, the mode flag is deliberately left at its prior value rather than introducing a third "Indeterminate" mode — matches 16-RESEARCH.md's Assumptions Log A3 and keeps this plan's blast radius to the two already-scoped stores
- The now-retired "never was in rig mode, no-op" precondition is NOT reintroduced as a ToggleService-level guard — per 16-RESEARCH.md Pattern 4, that responsibility is deliberately deferred to Plan 04's startup mode-corruption check / MainForm's `IsModeKnown()` guards, keeping `ToggleService` a pure step sequencer with no cross-cutting precondition logic

## Deviations from Plan

None - plan executed exactly as written. The plan's own interface listing and RESEARCH.md/PATTERNS.md code sketches matched the actual current source closely enough that no architectural adjustments were needed; all deviations were purely mechanical grep-format alignment (see Issues Encountered).

## Issues Encountered
- Two of the plan's own verification greps required exact-string alignment I hadn't initially matched: (1) the plan's automated verify checks for the literal substring `_markerStore.Save(new ToggleInProgressMarker` (no `Models.` qualifier) — my first draft used the fully-qualified `new Models.ToggleInProgressMarker(...)`, which the file's own `using RigToggle.Core.Models;` already makes redundant, so I dropped the qualifier to match; (2) the plan's verify grep for `"at least one active display must remain"` expects that exact phrase to appear on a single line, but my first edit kept the original two-string-literal-concatenation formatting (`"...must " + "remain.");`), which split the phrase across two lines invisibly to a literal grep — collapsed into one string literal to match. Neither was a design decision, both were caught immediately by the plan's own verification commands before committing.
- One explanatory code comment in `ToggleOrchestratorTests.cs` used the literal phrase `monitor.Restore` to describe what is *no longer* called — this collided with the plan's own `! grep -q "monitor.Restore"` verification check (which is checking for the call-log string, not prose). Reworded the comment to avoid the literal substring while keeping the same explanation.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `IModeStore`/`IToggleInProgressStore`-backed `ToggleService`/`ToggleOrchestrator` are ready for Plan 04's `Program.cs` composition-root rewiring (mode-store bootstrap for upgrading installs, the two startup blocking dialogs, and constructing `ToggleService`/`ToggleOrchestrator` with the new dependencies) and `MainForm`'s `IsModeKnown()`-gated toggle-trigger guards.
- `dotnet build` succeeds for `RigToggle.Core`, `RigToggle.Windows`, `RigToggle.Tests`, `RigToggle.IconGen`, and `RigToggle.Windows.Tests`. `RigToggle.App` currently fails to build (`Program.cs` still constructs the old `ToggleService(settingsStore, snapshotStore, ...)`/`ToggleOrchestrator(toggleService)` signatures) — this is the exact, plan-anticipated App-tier build gap this plan's own `<verification>` block calls out as expected until Plan 04 lands. Not a regression; documented here for Plan 04's awareness.
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` passes all 78 tests (up from 75 pre-phase-16, +3 new: the Normal-mode CR-01 reconcile test, the marker Save/Clear lifecycle test, and the `IsModeKnown()` pass-through test).
- No blockers or concerns for Plan 04.

---
*Phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign*
*Completed: 2026-08-07*

## Self-Check: PASSED

- FOUND: src/RigToggle.Core/ToggleService.cs
- FOUND: src/RigToggle.Core/ToggleOrchestrator.cs
- FOUND: src/RigToggle.Windows/WindowsMonitorController.cs
- FOUND: src/RigToggle.Tests/ToggleServiceTests.cs
- FOUND: src/RigToggle.Tests/ToggleOrchestratorTests.cs
- FOUND: .planning/phases/16-normal-mode-explicit-monitor-config-mode-store-redesign/16-03-SUMMARY.md
- FOUND commit: b769a8b (Task 1)
- FOUND commit: 380f728 (Task 2)
- FOUND commit: 6d35e9b (Task 3)
- FOUND commit: cbe6e3e (SUMMARY.md + REQUIREMENTS.md metadata commit)
