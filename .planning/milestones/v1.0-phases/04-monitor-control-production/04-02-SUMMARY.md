---
phase: 04-monitor-control-production
plan: 02
subsystem: infra
tags: [csharp, dotnet, windowsdisplayapi, ccd, json-persistence, monitor-state]

# Dependency graph
requires:
  - phase: 03-app-audio-control
    provides: IAudioController.CaptureState() no-param precedent mirrored here
provides:
  - "Full-topology, primitive, JSON-serializable MonitorState/MonitorPathSnapshot contract"
  - "Parameterless IMonitorController.CaptureState() mirroring IAudioController"
  - "Real WindowsMonitorController.CaptureState() implementation capturing all active CCD paths"
  - "Updated ToggleService/FakeMonitorController/JsonStoreTests consuming the new shape"
affects: [04-03, 04-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Full-topology capture: CaptureState() snapshots every active CCD path (not just the target monitor) into MonitorPathSnapshot records, with CCD enums stored as int in RigToggle.Core to preserve the Core/Windows dependency boundary"

key-files:
  created:
    - src/RigToggle.Core/Models/MonitorPathSnapshot.cs
  modified:
    - src/RigToggle.Core/Models/MonitorState.cs
    - src/RigToggle.Core/Abstractions/IMonitorController.cs
    - src/RigToggle.Windows/WindowsMonitorController.cs
    - src/RigToggle.Core/ToggleService.cs
    - src/RigToggle.Tests/Doubles/FakeControllers.cs
    - src/RigToggle.Tests/JsonStoreTests.cs

key-decisions:
  - "MonitorState.TargetDevicePath is set from the active GDI-primary path at capture time (with first-entry fallback), since the configured monitor to disable is always the current primary when CaptureState() runs"
  - "Kept AppSettings.MonitorDevicePath untouched (a distinct, pre-existing settings field) — only the removed MonitorState.MonitorDevicePath record member was ripped out"

patterns-established:
  - "Pattern 2 (04-RESEARCH.md): full-topology capture via GetActivePaths().SelectMany(TargetsInfo) into primitive per-path records, CCD enums cast to int at the Windows-adapter boundary"

requirements-completed: [DISPLAY-02]

# Metrics
duration: ~35min
completed: 2026-07-24
---

# Phase 4 Plan 02: Monitor Snapshot Contract Reshape Summary

**Reshaped MonitorState from a single device-path string into a full-topology, JSON-durable record (`MonitorPathSnapshot[]` + `TargetDevicePath`) and made `IMonitorController.CaptureState()` parameterless, with a real WindowsMonitorController implementation capturing every active CCD path.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-07-24T19:38:21Z
- **Completed:** 2026-07-24T19:44:17Z
- **Tasks:** 3 completed
- **Files modified:** 6 (1 created, 5 modified)

## Accomplishments
- New `MonitorPathSnapshot` primitive record (13 fields: device path, friendly name, position, resolution, five CCD enum values as `int`, frequency as `ulong`, is-primary) — JSON-round-trippable and free of any WindowsDisplayAPI type reference
- `MonitorState` reshaped to `(IReadOnlyList<MonitorPathSnapshot> Paths, string TargetDevicePath)`, replacing the Phase-2-minimal single-string shape
- `IMonitorController.CaptureState()` is now parameterless, mirroring `IAudioController.CaptureState()`'s Phase 3 precedent
- `WindowsMonitorController.CaptureState()` is now a real implementation: enumerates `PathInfo.GetActivePaths()`, flattens every path's `TargetsInfo` into `MonitorPathSnapshot` records, casts CCD enums to `int`, and resolves `TargetDevicePath` to the active GDI-primary (first-entry fallback). `Disable`/`Restore` remain compiling no-op stubs, explicitly deferred to Plan 03
- Rippled the shape change through `ToggleService.ToggleToRigMode` (no-arg call site), `FakeMonitorController` (no-param `CaptureState()`, `Restore` reads `TargetDevicePath`), and `JsonStoreTests` (all `MonitorState` constructions updated, assertion updated, plus a new `SnapshotStore_MonitorState_RoundTripsAllPathFields` test covering every field of a two-entry topology through JSON persistence)

## Task Commits

Each task was committed atomically:

1. **Task 1: Define MonitorPathSnapshot and reshape MonitorState + IMonitorController** - `c32bfc5` (feat)
2. **Task 2: Implement real full-topology CaptureState() in WindowsMonitorController** - `453ce26` (feat)
3. **Task 3: Ripple the shape change through ToggleService, the test double, and JsonStore tests** - `86136ef` (feat)

**Plan metadata:** commit pending (this SUMMARY + REQUIREMENTS.md, per worktree-mode exclusions)

## Files Created/Modified
- `src/RigToggle.Core/Models/MonitorPathSnapshot.cs` - New per-path primitive snapshot record (13 fields)
- `src/RigToggle.Core/Models/MonitorState.cs` - Reshaped to `Paths` list + `TargetDevicePath`
- `src/RigToggle.Core/Abstractions/IMonitorController.cs` - `CaptureState()` now parameterless
- `src/RigToggle.Windows/WindowsMonitorController.cs` - Real full-topology `CaptureState()`; `Disable`/`Restore` doc-comments updated to point at Plan 03
- `src/RigToggle.Core/ToggleService.cs` - `ToggleToRigMode` calls `CaptureState()` with no argument
- `src/RigToggle.Tests/Doubles/FakeControllers.cs` - `FakeMonitorController.CaptureState()` no-param, returns new shape; `Restore` call-log reads `TargetDevicePath`
- `src/RigToggle.Tests/JsonStoreTests.cs` - All `MonitorState` constructions updated to new shape; assertion updated; new round-trip test added

## Decisions Made
- `TargetDevicePath` is derived from the active GDI-primary at capture time rather than passed in, since `CaptureState()` is now parameterless and the configured-to-disable monitor is always the current primary when the toggle sequence runs (per RESEARCH Pattern 2's guidance)
- Left `AppSettings.MonitorDevicePath` (a distinct, unrelated settings-store field) untouched — only the removed `MonitorState.MonitorDevicePath` record member was targeted by this reshape

## Deviations from Plan

None — plan executed exactly as written. One note on the automated verification gate:

**Task 3's literal verify command** (`! grep -rq "MonitorDevicePath" src/RigToggle.Tests/JsonStoreTests.cs`) is a substring match that also matches `AppSettings.MonitorDevicePath` — a distinct, pre-existing settings field unrelated to the removed `MonitorState.MonitorDevicePath` member and explicitly out of scope for this reshape (confirmed via `grep -rn "\.MonitorDevicePath\b" src/` — every remaining hit resolves to `AppSettings`/`settings`, never `MonitorState`). The actual acceptance criterion — "No reference to the removed `MonitorState.MonitorDevicePath` member remains anywhere under `src/`" — is satisfied; the blunt grep produces a false positive due to name overlap between two distinct types' properties. No code change was made in response to this; it is documented here for visibility, not treated as a deviation requiring a fix.

## Issues Encountered

**Build/test verification unavailable in this sandbox.** This is a Linux sandbox; the `dotnet` CLI is not installed at all (`which dotnet` returns nothing), so neither `dotnet build` nor `dotnet test src/RigToggle.Tests` could be run — not even for `RigToggle.Core`, which targets plain `net10.0` (not `-windows`). All verification in this plan was performed via:
- Static grep gates specified in each task's `<verify>` block (all passed)
- Manual cross-file consistency sweep (`grep -rn "CaptureState(" src/`, `grep -rn "new MonitorState(" src/`, `grep -rn "\.MonitorDevicePath\b" src/`) confirming every call site and construction matches the new signatures/shape, with no stale references
- Manual review of `using` statements and `ImplicitUsings` settings in each touched `.csproj` to confirm `System.Linq` (`SelectMany`/`FirstOrDefault`) and `System.Collections.Generic` (`List<>`) resolve without explicit imports

**This remains unverified pending a real Windows build.** Per the task's `<acceptance_criteria>`, `dotnet test src/RigToggle.Tests` passing "on the rig" is a stated criterion that could not be executed here. The next session with access to a Windows/`dotnet` toolchain should run `dotnet build` and `dotnet test src/RigToggle.Tests` before Plan 03 begins, to confirm the reshape compiles and all existing + new tests (including `SnapshotStore_MonitorState_RoundTripsAllPathFields`) pass for real.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

The full-topology `MonitorState`/`MonitorPathSnapshot` contract is in place and consumed consistently across `ToggleService`, `WindowsMonitorController`, and the test suite. Plan 03 can now implement the real repositioning-aware `Disable`/`Restore` in `WindowsMonitorController` against this stable shape — `MonitorState.Paths` gives it every active path's captured position/resolution/rotation/etc. to reconstruct topology, and `TargetDevicePath` identifies which entry to remove/re-add, with no further contract changes needed.

**Blocker:** A real `dotnet build`/`dotnet test` run on the actual Windows rig (or any machine with the .NET 10 SDK + Windows targeting pack) has not yet happened for this plan's changes — flagged above under Issues Encountered. This should be the first action before starting Plan 03.

---
*Phase: 04-monitor-control-production*
*Completed: 2026-07-24*

## Self-Check: PASSED

All created files verified present; all task and metadata commit hashes verified in git log.
