---
phase: 06-multi-monitor-data-model-controller-generalization
plan: 01
subsystem: database
tags: [csharp, dotnet10, system-text-json, settings-migration, data-model]

# Dependency graph
requires:
  - phase: 02-foundations-gui-shell
    provides: AppSettings/MonitorInfo/JsonSettingsStore original single-monitor shapes
provides:
  - "AppSettings.MonitorsToDisable / MonitorsToEnable plural device-path sets"
  - "MonitorInfo.IsActive flag (defaulted, backward-compatible 4th positional param)"
  - "Silent v1.0->v1.1 settings migration inside JsonSettingsStore.Load()'s existing degrade-gracefully try block"
affects: [06-02, 06-03, 06-04, 06-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Two independently-persisted sibling sets on a mutable settings POCO (MonitorsToDisable/MonitorsToEnable), following AppSettings' existing NormalAudio/RigAudio role-pair precedent"
    - "Migration-inside-existing-try-block: new persisted-shape migrations must live inside the pre-existing JsonException/IOException degrade-to-fresh-AppSettings() handler, never add a parallel failure path"

key-files:
  created: []
  modified:
    - src/RigToggle.Core/Models/AppSettings.cs
    - src/RigToggle.Core/Models/MonitorInfo.cs
    - src/RigToggle.Core/Persistence/JsonSettingsStore.cs
    - src/RigToggle.Tests/JsonStoreTests.cs

key-decisions:
  - "MonitorsToDisable/MonitorsToEnable are nullable List<string> (not a wrapper type) so absence-in-JSON deserializes to null, which is the exact signal the migration check uses to distinguish a genuine v1.0 file from an already-migrated one"
  - "Legacy MonitorDevicePath/MonitorFriendlyName fields kept unchanged and undeleted — they remain the permanent migration source, per D-08"
  - "dotnet SDK is unavailable in this Linux executor sandbox; per the plan's own environment_note, verification was done via grep-based source assertions (all acceptance_criteria greps passed) instead of a live dotnet test run — deferred to the Windows rig"

patterns-established:
  - "Silent, dialog-free settings migrations belong inside JsonSettingsStore.Load()'s existing try block, guarded by an idempotency check on the target field"

requirements-completed: [DISPLAY-04, DISPLAY-05, DISPLAY-08]

# Metrics
duration: ~20min
completed: 2026-07-28
---

# Phase 6 Plan 1: Multi-Monitor Data Model Foundation Summary

**Added plural `MonitorsToDisable`/`MonitorsToEnable` device-path sets to `AppSettings`, an `IsActive` flag to `MonitorInfo`, and a fully silent v1.0→v1.1 settings migration inside `JsonSettingsStore.Load()`'s existing degrade-gracefully try block.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-07-28
- **Tasks:** 2/2 completed
- **Files modified:** 4

## Accomplishments
- `AppSettings` now carries independently-persisted `MonitorsToDisable`/`MonitorsToEnable` plural sets alongside the preserved legacy `MonitorDevicePath`/`MonitorFriendlyName` fields — the data foundation every later Phase 6 plan (and every v1.1 trigger phase) reads/writes.
- `MonitorInfo` gained a defaulted `IsActive` flag, so existing 3-arg construction sites (`WindowsMonitorController`, `FakeMonitorController`) keep compiling untouched until their own plans update them.
- `JsonSettingsStore.Load()` silently migrates a genuine v1.0-era settings.json's `MonitorDevicePath` into `MonitorsToDisable` on first load after upgrade — no dialog/toast/banner (DISPLAY-08) — and is idempotent against files that already have a populated disable-set.
- Migration lives inside the pre-existing `try` block, so a corrupted/malformed legacy file still degrades to a fresh `AppSettings()` exactly as before — migration introduces zero new failure modes.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add plural monitor-set fields to AppSettings and IsActive to MonitorInfo** - `75bd84c` (feat)
2. **Task 2: Silent v1.0->v1.1 migration in JsonSettingsStore.Load() + acceptance test** - `1edde15` (feat)

**Plan metadata:** committed separately by the orchestrator after wave completion (this is a worktree-isolated parallel executor run; STATE.md/ROADMAP.md updates are owned by the orchestrator, not this agent).

_Note: Task 2 is `tdd="true"` in the plan, but the executor sandbox has no `dotnet` SDK installed (confirmed: `command -v dotnet` and common install paths all empty). Per the plan's own `<environment_note>` escape hatch, the RED→GREEN cycle could not be executed live (no way to run the test suite to observe a failing test before the implementation). Test and implementation were written together in a single commit and verified via the plan's grep-based `<acceptance_criteria>` (all passed — see below). A live `dotnet test` run is deferred to the Windows rig, consistent with this plan's stated environment constraint._

## Files Created/Modified
- `src/RigToggle.Core/Models/AppSettings.cs` - Added `MonitorsToDisable`/`MonitorsToEnable` (`List<string>?`), updated class doc comment; legacy fields unchanged
- `src/RigToggle.Core/Models/MonitorInfo.cs` - Added defaulted `bool IsActive = false` 4th positional parameter; updated doc comment to reference `GetAllMonitors()` and the DISPLAY-06 predicate
- `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` - `Load()` now performs the D-08 migration inside its existing `try` block, before `return`
- `src/RigToggle.Tests/JsonStoreTests.cs` - Added `SettingsStore_Load_MigratesLegacyMonitorDevicePath_IntoDisableSet` and `SettingsStore_Load_DoesNotRemigrate_WhenDisableSetAlreadyPopulated`

## Decisions Made
- Followed the plan's explicit instruction to use nullable `List<string>?` (not a wrapper record) for the plural sets, since a `null` value after deserialization is the exact migration-trigger signal.
- Kept `AppSettings` as a mutable `sealed class` with `{ get; set; }` properties (not converted to a record) — matches existing convention and the plan's explicit instruction.

## Deviations from Plan

None — plan executed exactly as written. The only adjustment was procedural (test/implementation committed together rather than as separate RED/GREEN commits) due to the documented dotnet-SDK-unavailable environment constraint, which the plan itself anticipates and permits via `<environment_note>`.

## TDD Gate Compliance

Task 2 has `tdd="true"` but this executor's sandbox has no `dotnet` SDK, so the RED (failing test) and GREEN (passing test) gates could not be verified by actually running the test suite — there is no `test(...)` commit followed by a `feat(...)` commit in git log for this task; instead there is a single `feat(06-01)` commit containing both the new tests and the implementation.

This is a deliberate, plan-sanctioned deviation: the plan's own `<environment_note>` explicitly authorizes deferring `dotnet test` runs to the Windows rig when the SDK is absent, and instructs satisfying task verification via source-level `<acceptance_criteria>` assertions instead. All of Task 2's grep-based acceptance criteria were verified and passed:
- `grep -n "MonitorsToDisable = new" src/RigToggle.Core/Persistence/JsonSettingsStore.cs` — found at line 48, inside the `try` block
- `grep -c "catch ("` — still 2 (no new catch clause added)
- Both new test method names present (count 2)
- The migration test's fixture JSON literal contains no `MonitorsToDisable`/`MonitorsToEnable` keys (genuine v1.0 shape confirmed)

**Action required before Phase 6 is considered fully verified:** run `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj --filter "FullyQualifiedName~JsonStoreTests"` on a host with the .NET SDK (the Windows rig) to confirm all `JsonStoreTests` (existing + 2 new) pass, per this plan's `<verification>` section.

## Issues Encountered
None beyond the pre-known dotnet-SDK-unavailable environment constraint, which the plan explicitly anticipates.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- The persisted `AppSettings` shape (`MonitorsToDisable`/`MonitorsToEnable`) and `MonitorInfo.IsActive` are now in place for Plan 02 (`IMonitorController` generalization: `GetAllMonitors()`, `ActivateMonitors()`, `DeactivateMonitors()`) to build on directly.
- `IMonitorController` itself, `WindowsMonitorController`, `ToggleService`, and all `RigToggle.App`/`RigToggle.Windows` code are untouched by this plan, exactly as scoped — nothing in those projects is disturbed.
- **Blocker/follow-up for Phase 6 completion:** the deferred `dotnet test` run (see TDD Gate Compliance above) must happen on the Windows rig before this plan's `<verification>` criteria are fully satisfied. This does not block Plan 02+ from proceeding (the Core/Tests projects are cross-platform and the source-level checks give high confidence), but it must be closed out before the phase is marked complete.

---
*Phase: 06-multi-monitor-data-model-controller-generalization*
*Plan: 01*
*Completed: 2026-07-28*
