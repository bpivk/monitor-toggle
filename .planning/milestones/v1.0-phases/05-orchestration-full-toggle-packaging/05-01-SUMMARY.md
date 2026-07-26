---
phase: 05-orchestration-full-toggle-packaging
plan: 01
subsystem: core
tags: [csharp, dotnet, records, tdd, toggle-orchestration]

# Dependency graph
requires:
  - phase: 04-monitor-disable-restore
    provides: MonitorState/MonitorPathSnapshot full-topology snapshot shape consumed by ToggleService
provides:
  - "ToggleStepOutcome/ToggleStepResult/ToggleResult model types in RigToggle.Core.Models"
  - "ToggleService.ToggleToRigMode()/ToggleToNormalMode() returning ToggleResult instead of void"
  - "FakeMonitorController.throwOnDisable test double flag"
affects: [05-02 MainForm checklist UI, 05-03 packaging]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Structured per-step result contract (ToggleStepOutcome/ToggleStepResult/ToggleResult) replacing void-and-throw for multi-step orchestration methods"
    - "TryExecuteStep private static helper for stop-on-first-failure step sequencing"

key-files:
  created:
    - src/RigToggle.Core/Models/ToggleStepOutcome.cs
    - src/RigToggle.Core/Models/ToggleStepResult.cs
    - src/RigToggle.Core/Models/ToggleResult.cs
  modified:
    - src/RigToggle.Core/ToggleService.cs
    - src/RigToggle.Tests/Doubles/FakeControllers.cs
    - src/RigToggle.Tests/ToggleServiceTests.cs

key-decisions:
  - "ToggleToRigMode is stop-on-first-failure (D-04); ToggleToNormalMode remains isolate-and-continue (D-05) — asymmetry documented inline in ToggleService's class and method doc comments so it is never 'fixed' into false symmetry"
  - "Preflight guards (unconfigured settings, missing companion app path, corrupted snapshot) stay exception-based, not represented as ToggleResult steps"
  - "Empty/no CompanionAppPath in ToggleToNormalMode records App as NotAttempted (not Succeeded) so the checklist always carries a semantically accurate 3-entry shape"

patterns-established:
  - "Pattern: structured multi-step result (ToggleStepOutcome/ToggleStepResult/ToggleResult) for orchestration methods where partial success must be reported per-step rather than collapsed into a single exception"

requirements-completed: [CORE-04, CORE-03]

# Metrics
duration: 25min
completed: 2026-07-24
---

# Phase 5 Plan 1: CORE-04 Structured Toggle Result Contract Summary

**ToggleService.ToggleToRigMode/ToggleToNormalMode now return a ToggleResult (Monitor/Audio/App per-step outcomes) instead of throwing a single generic exception, with stop-on-first-failure for rig mode and isolate-and-continue preserved for normal mode.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-24 (session start, worktree agent-a034da25263f4f34c)
- **Completed:** 2026-07-24
- **Tasks:** 3/3 completed
- **Files modified:** 6 (3 new, 3 modified)

## Accomplishments
- Added `ToggleStepOutcome` (enum), `ToggleStepResult` (record), and `ToggleResult` (wrapping record with computed `Success`) to `RigToggle.Core.Models`, matching house style (file-scoped namespace, XML-doc referencing decision IDs, positional records).
- Refactored `ToggleService.ToggleToRigMode()` to stop-on-first-failure (D-04): the first mutation step (Monitor → Audio → App) to throw is recorded `Failed` with the exception message, every subsequent step is recorded `NotAttempted`, no rollback, no further steps executed. The `_snapshotStore.Save(...)` call still precedes any mutation call (CORE-03 preserved).
- Refactored `ToggleService.ToggleToNormalMode()` to isolate-and-continue (D-05, unchanged since gap-closure 03-04): monitor Restore failure is captured but not re-thrown, audio Restore failure is still swallowed-and-traced, both now recorded as `ToggleStepResult`s instead of throwing; `MinimizeIfRunning`/`Clear` are still skipped only when monitor restore failed.
- Updated the class-level and method-level doc comments to remove the now-stale "Partial-failure handling (CORE-04) is explicitly out of scope" sentence and explicitly document the deliberate D-04 (stop-on-first-failure) vs. D-05 (isolate-and-continue) asymmetry.
- Extended `FakeMonitorController` with a `throwOnDisable` constructor flag mirroring `FakeAudioController`'s existing `throwOnRestore` pattern.
- Added two new tests locking the new failure-path contract, and strengthened two existing happy-path tests to assert on the returned `ToggleResult`. The existing preflight-guard test (missing companion app path) is unchanged — still `Assert.Throws<InvalidOperationException>`.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the three structured-result model types** - `36158d8` (feat)
2. **Task 2: Refactor ToggleService to return ToggleResult** - `e2f88c7` (feat)
3. **Task 3: Extend FakeMonitorController and update tests for the ToggleResult contract** - `b057b09` (test)

**Plan metadata:** (this commit, see below)

_Note: Task 3 is a single `test`-type commit (not a separate RED/GREEN pair) — the plan sequenced model creation (Task 1) and service implementation (Task 2) ahead of the fake/test updates (Task 3) rather than a strict test-first loop, since the tests exist to lock down a contract whose implementation the plan itself specifies in Task 2._

## Files Created/Modified
- `src/RigToggle.Core/Models/ToggleStepOutcome.cs` - `enum ToggleStepOutcome { Succeeded, Failed, NotAttempted }`
- `src/RigToggle.Core/Models/ToggleStepResult.cs` - `record ToggleStepResult(string StepName, ToggleStepOutcome Outcome, string? Reason)`
- `src/RigToggle.Core/Models/ToggleResult.cs` - `record ToggleResult(IReadOnlyList<ToggleStepResult> Steps)` with computed `bool Success`
- `src/RigToggle.Core/ToggleService.cs` - both toggle methods now return `ToggleResult`; added private static `TryExecuteStep` helper; updated class/method doc comments
- `src/RigToggle.Tests/Doubles/FakeControllers.cs` - `FakeMonitorController` gained `throwOnDisable` flag
- `src/RigToggle.Tests/ToggleServiceTests.cs` - `CreateService` forwards `monitorThrowsOnDisable`; 2 new tests; 2 strengthened happy-path assertions

## Decisions Made
- Kept the three preflight/corrupted-snapshot guards exception-based per plan design (D-02 scope boundary) — only the three mutation steps (Monitor/Audio/App) are represented in `ToggleResult.Steps`.
- Used a small private static `TryExecuteStep(stepName, action, steps)` helper in `ToggleToRigMode` rather than duplicating try/catch/append blocks three times, keeping the stop-on-first-failure logic linear and easy to read.
- In `ToggleToNormalMode`, when `CompanionAppPath` is empty/null the "App" step is recorded `NotAttempted` (not `Succeeded`) since minimize was never actually attempted — this was an open representation choice the plan explicitly flagged ("pick and document the representation").

## Deviations from Plan

None - plan executed exactly as written. All three tasks' automated grep-based verification gates pass (see below); the `dotnet test`/`dotnet build` verification is explicitly deferred to the Windows checkpoint in Plan 02 per the plan's own `<verification>` section (no .NET SDK / net10.0-windows available in this Linux sandbox).

## Issues Encountered

None. All static verification gates specified in each task's `<verify>` block passed:
- Task 1: model file shape/identifier grep gates — pass.
- Task 2: return-type, stale-doc-removal, D-04/D-05 doc references, NotAttempted usage, and Save-before-Disable source-order (awk) gates — pass.
- Task 3: `throwOnDisable`, `monitorThrowsOnDisable`, `ToggleStepOutcome.(Failed|NotAttempted)`, and preserved `Assert.Throws<InvalidOperationException>` gates — pass.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The `ToggleResult`/`ToggleStepResult`/`ToggleStepOutcome` contract is in place and locked by tests, ready for Plan 02's `MainForm.BtnToggle_Click` to consume (`result.Success` branch + per-step checklist rendering).
- `dotnet build`/`dotnet test` for this plan's changes have NOT been run on real .NET tooling yet (Linux sandbox has no net10.0-windows SDK) — this is explicitly deferred to Plan 02's Windows human-verify checkpoint per the plan's `<verification>` section, not a gap introduced by this plan.

---
*Phase: 05-orchestration-full-toggle-packaging*
*Completed: 2026-07-24*

## Self-Check: PASSED

All created/modified files verified present on disk; all task commits (`36158d8`, `e2f88c7`, `b057b09`) and the plan-metadata commit (`ea2341e`) verified present in `git log --oneline --all`.
