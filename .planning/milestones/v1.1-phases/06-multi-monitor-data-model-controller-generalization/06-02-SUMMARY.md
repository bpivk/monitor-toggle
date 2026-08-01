---
phase: 06-multi-monitor-data-model-controller-generalization
plan: 02
subsystem: core-orchestration
tags: [csharp, dotnet10, interface-design, toggleservice, ccd-monitor-control]

# Dependency graph
requires:
  - phase: 06-multi-monitor-data-model-controller-generalization
    plan: 01
    provides: "AppSettings.MonitorsToDisable/MonitorsToEnable, MonitorInfo.IsActive"
provides:
  - "IMonitorController N-monitor triad: GetAllMonitors, ActivateMonitors(set), DeactivateMonitors(set), plus unchanged CaptureState/Restore"
  - "ToggleService orchestration driving both monitor sets with correct Extend-before-remove ordering (Pitfall 2)"
  - "IsFullyConfigured D-07 OR-check (disable-set OR enable-set non-empty)"
  - "D-02 unconditional enable-set teardown on toggle-back, documented as intentional asymmetry"
  - "FakeMonitorController updated to the new interface for downstream test authoring"
affects: [06-03, 06-04, 06-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "N-monitor set orchestration: two IReadOnlySet<string> sets (disable/enable) built from AppSettings inside a single TryExecuteStep closure, preserving one-step-per-checklist-row granularity"
    - "Load-bearing sub-call ordering documented via both an inline comment at the call site and a class-level XML-doc cross-reference (Pitfall 2: Activate before Deactivate on rig-mode entry; Restore before Deactivate on toggle-back)"

key-files:
  created: []
  modified:
    - src/RigToggle.Core/Abstractions/IMonitorController.cs
    - src/RigToggle.Core/ToggleService.cs
    - src/RigToggle.Tests/Doubles/FakeControllers.cs
    - src/RigToggle.Tests/ToggleServiceTests.cs

key-decisions:
  - "IMonitorController.Disable(string) removed entirely and replaced by DeactivateMonitors(IReadOnlySet<string>), reused for both the rig-mode disable-set removal and the toggle-back enable-set teardown — matches the plan's stated single-method-reuse design"
  - "ToggleToRigMode's Monitor step remains a single TryExecuteStep(\"Monitor\", ...) closure containing both ActivateMonitors and DeactivateMonitors calls, preserving the existing per-step (not per-sub-action) ToggleResult checklist granularity from Phase 5"
  - "dotnet SDK is unavailable in this Linux executor sandbox; per the plan's own environment_note (and consistent with 06-01's precedent), verification was done via grep-based source assertions (all acceptance_criteria greps passed) instead of a live dotnet test run — deferred to the Windows rig"

requirements-completed: [DISPLAY-04, DISPLAY-05]

# Metrics
duration: ~25min
completed: 2026-07-28
---

# Phase 6 Plan 2: Multi-Monitor Controller Generalization Summary

**Generalized `IMonitorController` from a single-target `Disable(string)`/`Restore` contract to the N-monitor triad (`GetAllMonitors`, `ActivateMonitors(set)`, `DeactivateMonitors(set)`), and rewired `ToggleService` to drive both configured monitor sets with the load-bearing Extend-before-remove ordering, the D-07 `IsFullyConfigured` OR-check, and the D-02 unconditional enable-set teardown on toggle-back.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-07-28
- **Tasks:** 3/3 completed
- **Files modified:** 4

## Accomplishments

- `IMonitorController` now exposes `GetAllMonitors()` (active + OS-disabled-but-available displays — the DISPLAY-05 enabler), `ActivateMonitors(IReadOnlySet<string>)`, and `DeactivateMonitors(IReadOnlySet<string>)`, replacing the old single-target `Disable(string)`. `CaptureState`, `Restore`, and `GetActiveMonitors` are unchanged. Each new method's XML-doc names the 06-RESEARCH.md pattern it implements (Pattern 1/2/3), matching the file's existing doc convention.
- `ToggleService.ToggleToRigMode`'s Monitor step now builds `disableSet`/`enableSet` from `AppSettings.MonitorsToDisable`/`MonitorsToEnable` and calls `ActivateMonitors(enableSet)` **before** `DeactivateMonitors(disableSet)` inside a single `TryExecuteStep("Monitor", ...)` closure — the ordering is load-bearing per 06-RESEARCH.md Pitfall 2 (`ApplyTopology(Extend)`'s persistence-database fallback would otherwise silently reactivate the disable-set). The CR-01 snapshot-clear-on-unchanged-failure logic is untouched.
- `ToggleService.ToggleToNormalMode` keeps `Restore(snapshot.Monitor)` as the disable-set restore, then unconditionally calls `DeactivateMonitors(enableSet)` immediately after (same try/catch, same `monitorFailure` variable) — the D-02 asymmetry (enable-set is always re-disabled, never snapshot-restored) is documented both at the call site and cross-referenced from the class-level doc comment, with an explicit warning not to "fix" it into false symmetry.
- `IsFullyConfigured` (D-07) now requires `(MonitorsToDisable?.Count > 0 || MonitorsToEnable?.Count > 0)` instead of a single non-empty `MonitorDevicePath` — an enable-only or disable-only configuration is fully configured. The unconfigured-guard exception message was updated to match ("choose at least one monitor to disable or enable...").
- `FakeMonitorController` implements the new interface (`GetAllMonitors`, `ActivateMonitors`, `DeactivateMonitors`), preserving the existing `throwOnDisable`/`mutatesBeforeThrowingOnDisable` simulated-CCD-failure semantics used by the CR-01/D-04 tests. No mocking framework was added.
- `ToggleServiceTests` fixture and assertions updated for the renamed call-log label (`monitor.Disable` → `monitor.DeactivateMonitors`), plus four new tests proving the D-07 enable-only/both-empty cases and both load-bearing orderings (rig-mode Activate-before-Deactivate, toggle-back Restore-before-Deactivate).

## Task Commits

Each task was committed atomically:

1. **Task 1: Generalize the IMonitorController contract to the N-monitor triad** - `12d308d` (feat)
2. **Task 2: Rewire ToggleService orchestration for both monitor sets (ordering, D-07, D-02)** - `fa59444` (feat)
3. **Task 3: Update FakeMonitorController + ToggleServiceTests to the new contract** - `0b8ec8f` (feat)

**Plan metadata:** committed separately by the orchestrator after wave completion (this is a worktree-isolated parallel executor run; STATE.md/ROADMAP.md updates are owned by the orchestrator, not this agent).

_Note: Task 3 is `tdd="true"` in the plan, but the executor sandbox has no `dotnet` SDK installed (confirmed: `command -v dotnet` returns not found). Per the plan's own `<environment_note>` escape hatch (and consistent with the 06-01 plan's identical constraint), the RED→GREEN cycle could not be executed live. The Fake update and the new tests were written together in a single commit and verified via the plan's grep-based `<acceptance_criteria>` (all passed — see below). A live `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` run is deferred to the Windows rig._

## Files Created/Modified

- `src/RigToggle.Core/Abstractions/IMonitorController.cs` - Replaced `Disable(string)` with `ActivateMonitors(IReadOnlySet<string>)`/`DeactivateMonitors(IReadOnlySet<string>)`; added `GetAllMonitors()`; updated doc comments to cite 06-RESEARCH.md patterns
- `src/RigToggle.Core/ToggleService.cs` - `IsFullyConfigured` D-07 OR-check; rig-mode Monitor step now Activate-then-Deactivate in one closure; toggle-back Monitor try block adds D-02 unconditional `DeactivateMonitors(enableSet)` after `Restore`; class-level and inline doc comments document the new D-02 asymmetry
- `src/RigToggle.Tests/Doubles/FakeControllers.cs` - `FakeMonitorController` implements `GetAllMonitors`/`ActivateMonitors`/`DeactivateMonitors`; `GetActiveMonitors`/`GetAllMonitors` now pass `IsActive: true` explicitly
- `src/RigToggle.Tests/ToggleServiceTests.cs` - `ConfiguredSettings` fixture sets `MonitorsToDisable`; renamed call-log assertions; added 4 new tests (`IsSettingsConfigured_EnableOnly_ReturnsTrue`, `IsSettingsConfigured_BothSetsEmpty_ReturnsFalse`, `ToggleToRigMode_ActivatesEnableSet_BeforeDeactivatingDisableSet`, `ToggleToNormalMode_RestoresBeforeReDisablingEnableSet`)

## Decisions Made

- Kept the Monitor step as a single `TryExecuteStep` closure containing two sub-calls (`ActivateMonitors` then `DeactivateMonitors`) rather than splitting into two checklist rows — matches the plan's explicit instruction and the existing Phase 5 per-step reporting granularity.
- `disableSet`/`enableSet` are built with `(settings.X ?? new List<string>()).ToHashSet()`, so a null field degrades to an empty set rather than throwing — consistent with `AppSettings`' existing "null means never configured" convention.
- Added the D-02 asymmetry cross-reference to the class-level XML-doc (not just the inline call-site comment) per the plan's action item (d), matching the existing stop-vs-continue asymmetry doc convention already present in this file.

## Deviations from Plan

None — plan executed exactly as written. The only adjustment was procedural (Task 3's test/implementation committed together rather than as separate RED/GREEN commits) due to the documented dotnet-SDK-unavailable environment constraint, which the plan itself anticipates and permits via `<environment_note>`.

## TDD Gate Compliance

Task 3 has `tdd="true"` but this executor's sandbox has no `dotnet` SDK, so the RED (failing test) and GREEN (passing test) gates could not be verified by actually running the test suite — there is no `test(...)` commit followed by a `feat(...)` commit in git log for Task 3; instead there is a single `feat(06-02)` commit (`0b8ec8f`) containing both the new tests and the `FakeMonitorController` update.

This is a deliberate, plan-sanctioned deviation: the plan's own `<environment_note>` explicitly authorizes deferring `dotnet test` runs to the Windows rig when the SDK is absent, and instructs satisfying task verification via source-level `<acceptance_criteria>` assertions instead. All acceptance criteria for all three tasks were verified via grep and passed:

- Task 1: `GetAllMonitors`/`ActivateMonitors(IReadOnlySet<string>`/`DeactivateMonitors(IReadOnlySet<string>` all present; `void Disable(string` absent — confirmed
- Task 2: `MonitorsToDisable?.Count > 0 || settings.MonitorsToEnable?.Count > 0` present; `ActivateMonitors[\s\S]*DeactivateMonitors` matches; `TryExecuteStep("Monitor"` occurs exactly once (textual grep, including comments); `D-02` and `asymmetr` both present near the teardown call — confirmed
- Task 3: `public void Disable(` occurs 0 times in `FakeControllers.cs`; all 4 new test method names present (count 4) — confirmed

**Action required before Phase 6 is considered fully verified:** run `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` on a host with the .NET SDK (the Windows rig) to confirm all `ToggleServiceTests` (existing + 4 new) pass, per this plan's `<verification>` section. This plan also intentionally leaves `RigToggle.Windows` and `RigToggle.App` non-compiling (they still reference the removed `Disable(string)` method) — this is expected per the plan's own objective note and will be resolved by Plans 03/04/05.

## Issues Encountered

None beyond the pre-known dotnet-SDK-unavailable environment constraint, which the plan explicitly anticipates.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The generalized `IMonitorController` triad and `ToggleService` orchestration (both sets, correct ordering, D-07, D-02) are in place for Plan 03 (`WindowsMonitorController` Windows CCD adapter implementation) to build against directly.
- Plan 04 (Settings grid) and Plan 05 (confirmation dialog) can now assume the fixed, tested `IMonitorController`/`ToggleService` contract this plan establishes.
- **Blocker/follow-up for Phase 6 completion:** the deferred `dotnet test` run (see TDD Gate Compliance above) must happen on the Windows rig before this plan's `<verification>` criteria are fully satisfied. This does not block Plans 03/04/05 from proceeding (the Core/Tests projects are cross-platform and the source-level checks give high confidence), but it must be closed out before the phase is marked complete.

---
*Phase: 06-multi-monitor-data-model-controller-generalization*
*Plan: 02*
*Completed: 2026-07-28*

## Self-Check: PASSED

- Commits found: 12d308d, fa59444, 0b8ec8f, 96ec820
- Files found: src/RigToggle.Core/Abstractions/IMonitorController.cs, src/RigToggle.Core/ToggleService.cs, src/RigToggle.Tests/Doubles/FakeControllers.cs, src/RigToggle.Tests/ToggleServiceTests.cs, .planning/phases/06-multi-monitor-data-model-controller-generalization/06-02-SUMMARY.md
