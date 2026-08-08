---
phase: 17-manual-monitor-panel-shared-safety-guard
plan: 01
subsystem: ui
tags: [winforms, ccd, concurrency, interlocked, monitor-control]

# Dependency graph
requires:
  - phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign
    provides: unified ActivateMonitors/DeactivateMonitors controller-call path used by both toggle directions
provides:
  - "ToggleOrchestrator.BeginExclusiveMonitorAccess(): IDisposable lease sharing the existing _busy flag with RunGuarded, giving bidirectional mutual exclusion between manual monitor panel actions and Rig/Normal toggles"
  - "MonitorIdentifyOverlay: borderless, topmost, self-closing per-monitor Form positioned from a CCD-sourced MonitorPathSnapshot"
affects: [17-02-manual-monitor-panel, 17-03, 17-04-rig-checkpoint]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Shared single-flag mutual exclusion: a second entry point (BeginExclusiveMonitorAccess) claims the SAME Interlocked.CompareExchange(ref _busy, 1, 0) flag RunGuarded uses, rather than introducing a second flag or a lock — both directions of contention resolve to the existing ToggleInProgressException with the existing copy"
    - "IDisposable lease with an internal released-guard (Interlocked.Exchange(ref _released, 1)) to make double-dispose safe and prevent a stale lease from releasing a different holder's claim"
    - "Deliberate asymmetry from the toggle path: a lease that shares the busy flag but skips crash-recovery marker writes, documented inline as an intentional divergence"
    - "Borderless/topmost/ShowWithoutActivation Form positioned exclusively from CCD snapshot coordinates, never Screen.AllScreens"

key-files:
  created:
    - src/RigToggle.App/MonitorIdentifyOverlay.cs
  modified:
    - src/RigToggle.Core/ToggleOrchestrator.cs
    - src/RigToggle.Tests/ToggleOrchestratorTests.cs

key-decisions:
  - "Added a lightweight shared lease (BeginExclusiveMonitorAccess) rather than accepting the panel/toggle race, resolving 17-RESEARCH.md Open Question 1 — MonitorConfirmDialog.ShowDialog()'s nested message loop dispatches WM_HOTKEY, making the race reachable, and the mitigation reuses ~25 lines of existing machinery (_busy, Interlocked.CompareExchange, ToggleInProgressException, existing busy copy) with no new abstraction"

patterns-established:
  - "Pattern: lease-based mutual exclusion sharing a single primitive flag across two independent orchestrator entry points"

requirements-completed: [DISPLAY-12, PANEL-02, PANEL-05]

# Metrics
duration: 5min
completed: 2026-08-08
---

# Phase 17 Plan 01: Toggle/Panel Serialization Lease & Identify Overlay Summary

**Added `ToggleOrchestrator.BeginExclusiveMonitorAccess()` — an `IDisposable` lease sharing the existing `_busy` flag with `RunGuarded` for bidirectional mutual exclusion between manual monitor panel actions and Rig/Normal toggles — plus the standalone `MonitorIdentifyOverlay` borderless per-monitor identify window, both landed as leaf dependencies ahead of Plan 02's panel.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-08-08T17:08:03Z
- **Completed:** 2026-08-08T17:13:11Z
- **Tasks:** 2 completed
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments

- `ToggleOrchestrator.BeginExclusiveMonitorAccess()` claims the exact same `Interlocked.CompareExchange(ref _busy, 1, 0)` primitive `RunGuarded` uses, so a manual monitor mutation cannot begin while a Rig/Normal toggle is in flight (and vice versa), rejecting immediately with the existing `ToggleInProgressException("A toggle is already in progress. Wait for it to finish, then try again.")` — no new exception type, no new user-facing copy.
- The lease deliberately never touches `_markerStore.Save`/`Clear`, so acquiring/releasing it never writes the DISPLAY-13 crash-recovery marker — a crash during a manual monitor action will never surface a spurious "interrupted toggle" dialog naming a mode the user never selected.
- The nested `ExclusiveMonitorAccessLease` releases the flag exactly once via `Interlocked.Exchange(ref _released, 1)`, so a stale double-dispose can never steal a later holder's claim.
- `MonitorIdentifyOverlay` (`src/RigToggle.App/MonitorIdentifyOverlay.cs`) — borderless, topmost, `ShowInTaskbar = false` Form positioned and sized exclusively from a CCD-sourced `MonitorPathSnapshot`, displays a 120pt Segoe UI Bold ordinal number full-bleed centered, auto-closes after `AutoCloseMilliseconds` (2500ms) via a `System.Windows.Forms.Timer`, and never steals focus (`ShowWithoutActivation`). Deterministically disposes its timer and font.
- Core test suite: 84/84 passing (79 baseline + 5 new).

## Task Commits

Each task was committed atomically (TDD RED/GREEN for Task 1):

1. **Task 1: Add BeginExclusiveMonitorAccess() lease to ToggleOrchestrator**
   - `b930bb2` (test) — 5 failing tests added (RED: compile fails, `BeginExclusiveMonitorAccess` did not yet exist)
   - `3aceaa4` (feat) — lease implementation, all 84 tests pass (GREEN)
2. **Task 2: Create MonitorIdentifyOverlay** - `0a282fb` (feat)

**Plan metadata:** (this commit, following SUMMARY.md creation)

## Files Created/Modified

- `src/RigToggle.Core/ToggleOrchestrator.cs` — added `public IDisposable BeginExclusiveMonitorAccess()` and the nested `private sealed class ExclusiveMonitorAccessLease : IDisposable`
- `src/RigToggle.Tests/ToggleOrchestratorTests.cs` — added 5 tests: `BeginExclusiveMonitorAccess_WhileToggleInFlight_ThrowsToggleInProgress`, `ToggleToRigMode_WhileExclusiveMonitorAccessHeld_ThrowsToggleInProgress`, `BeginExclusiveMonitorAccess_Dispose_ReleasesFlagForSubsequentToggle`, `BeginExclusiveMonitorAccess_DoesNotWriteCrashRecoveryMarker`, `BeginExclusiveMonitorAccess_DoubleDispose_DoesNotReleaseAnotherHoldersLease`
- `src/RigToggle.App/MonitorIdentifyOverlay.cs` (new) — `public sealed class MonitorIdentifyOverlay : Form` with `public MonitorIdentifyOverlay(MonitorPathSnapshot snapshot, int number)`

## Exact Signatures for Plan 02

**`ToggleOrchestrator.BeginExclusiveMonitorAccess()`:**
```csharp
public IDisposable BeginExclusiveMonitorAccess()
```
Throws `RigToggle.Core.ToggleInProgressException` (message: `"A toggle is already in progress. Wait for it to finish, then try again."`) if a toggle is already in flight. On success, returns an `IDisposable` lease; `Dispose()` releases the shared flag exactly once. Consume via `using` at the panel's mutation call sites (Plan 02).

**`MonitorIdentifyOverlay` constructor:**
```csharp
public MonitorIdentifyOverlay(RigToggle.Core.Models.MonitorPathSnapshot snapshot, int number)
```
Namespace `RigToggle.App`. Null-guards `snapshot`. Call `.Show()` (never `ShowDialog()`) once per active `MonitorPathSnapshot` from `IMonitorController.CaptureState().Paths`, numbering sequentially from 1.

## Decisions Made

- Resolved 17-RESEARCH.md Open Question 1 / Assumption A3 by adding a lightweight shared lease rather than accepting the panel/toggle race — `MonitorConfirmDialog.ShowDialog()`'s nested message loop dispatches `WM_HOTKEY`, making a hotkey-triggered toggle mid-panel-action reachable, not merely theoretical. The lease reuses existing machinery (`_busy`, `Interlocked.CompareExchange`, `ToggleInProgressException`, existing busy copy) rather than introducing a new abstraction — see the plan's `<planner_decision>` block for full rationale (already recorded in 17-01-PLAN.md, not duplicated in STATE.md's Key Decisions per the plan's own scope).

## Deviations from Plan

None - plan executed exactly as written. One reformatting adjustment during Task 1 to satisfy an acceptance-criteria grep gate (the doc comment's inline code sample of `Interlocked.CompareExchange(ref _busy, 1, 0)` was reworded to `Interlocked.CompareExchange` alone, since the literal grep count expected exactly 2 occurrences — one in `RunGuarded`, one in `BeginExclusiveMonitorAccess` — and a third occurrence inside a doc comment would have inflated that count to 3 without representing a second flag). Not a deviation from the plan's design intent, purely a phrasing tweak to keep the doc comment's illustrative snippet from tripping a literal-count verification gate.

Similarly for Task 2, the constructor's `Location`/`Size` assignment was split into per-field local variables (`x`, `y`, `width`, `height`) on their own lines, purely so the acceptance criterion's line-based grep (`snapshot.PositionX|snapshot.PositionY|snapshot.ResolutionWidth|snapshot.ResolutionHeight`, expecting a count of at least 4) matched each field's own line rather than two lines each containing two fields. No behavior change.

## Issues Encountered

None beyond the two grep-gate phrasing adjustments described above, both caught and fixed during the same task's verification step before committing.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `BeginExclusiveMonitorAccess()` and `MonitorIdentifyOverlay` are both ready for Plan 02's `MonitorPanelForm` to consume directly — Plan 02 can focus purely on the panel itself with zero file overlap with this plan's two files.
- Full solution builds with 0 errors across all 6 projects (`RigToggle.Core`, `RigToggle.Windows`, `RigToggle.App`, `RigToggle.Tests`, `RigToggle.Windows.Tests`, `RigToggle.IconGen`).
- Core test suite green at 84/84. `RigToggle.Windows.Tests` still cannot execute on this Linux dev host (`Microsoft.WindowsDesktop.App` runtime not installed — known, documented environment limitation carried over from Phase 16), but builds cleanly.
- No `.csproj` file was modified (`git diff --name-only | grep -c '\.csproj$'` → `0`), matching 17-RESEARCH.md's Package Legitimacy Audit ("no new external packages introduced").

## Known Stubs

None. Both deliverables are fully wired: `BeginExclusiveMonitorAccess()` is exercised by 5 real tests against the live `ToggleOrchestrator`/`RunGuarded` code path, and `MonitorIdentifyOverlay` is a complete, self-contained `Form` (not yet called by any caller — `MonitorPanelForm` in Plan 02 is its intended first caller, exactly as the plan's `<action>` states: "This file is not referenced by anything yet").

---
*Phase: 17-manual-monitor-panel-shared-safety-guard*
*Completed: 2026-08-08*
