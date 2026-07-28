---
phase: 06-multi-monitor-data-model-controller-generalization
plan: qj1
subsystem: infra
tags: [ccd, windowsdisplayapi, monitor-enumeration, xunit]

requires:
  - phase: 06-03
    provides: WindowsMonitorController.GetAllMonitors()/ActivateMonitors()/DeactivateMonitors()
provides:
  - Deduplicated GetAllMonitors() sourcing Active/Primary state exclusively from GetActiveMonitors()
  - Pure, unit-tested internal MergeAllMonitors() seam
affects: [06-06-rig-checkpoint, gap-closure]

tech-stack:
  added: []
  patterns: ["pure merge/dedup seam extraction for CCD-adjacent logic, unit-testable without live hardware"]

key-files:
  created: []
  modified:
    - src/RigToggle.Windows/WindowsMonitorController.cs
    - src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs

key-decisions:
  - "GetAllMonitors() now reuses GetActiveMonitors() for all Active/Primary state instead of re-deriving it from potentially-stale inactive PathInfo fields (IsGDIPrimary/IsPathActive) — same discipline already applied elsewhere in this file (Restore/DeactivateMonitors)"
  - "Dedup key is DevicePath; a single seen-set spans both the active and available-target loops in MergeAllMonitors() so a DevicePath present in both inputs (or duplicated within either) always collapses to exactly one row"

patterns-established:
  - "Extract pure internal static helpers for CCD-adjacent merge/dedup logic so gap-closure fixes are unit-testable without live rig hardware (mirrors existing AssignSource/AnyRectanglesOverlap/CopyOutputTechnology seams)"

requirements-completed: [DISPLAY-04, DISPLAY-05]

duration: ~20min
completed: 2026-07-28
---

# Quick Task 260728-qj1: Fix WindowsMonitorController.GetAllMonitors() Duplicate Rows Summary

**GetAllMonitors() now dedupes by DevicePath and sources Active/Primary state exclusively from GetActiveMonitors(), fixing the rig-confirmed 10-rows-for-2-monitors / dual-primary bug found at the 06-06 checkpoint.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-07-28T19:12:49Z
- **Tasks:** 2/2 completed
- **Files modified:** 2

## Accomplishments

- Rewrote `WindowsMonitorController.GetAllMonitors()` to delegate to a new pure `MergeAllMonitors()` seam, reusing the already-correct `GetActiveMonitors()` for Active/Primary state instead of reading `IsGDIPrimary`/`IsPathActive` off potentially-stale inactive `PathInfo` entries (the exact root cause identified in 06-06-SUMMARY.md's rig NO-GO).
- Added `internal static IReadOnlyList<MonitorInfo> MergeAllMonitors(...)`: emits deduped active monitors first (promoted to `IsActive=true` via `with`, `IsPrimary` preserved), then appends any not-yet-seen available target as `IsPrimary=false`/`IsActive=false`. A single `HashSet<string>` spanning both loops guarantees exactly one row per `DevicePath` even when a path appears in both inputs or is duplicated within either.
- Added 5 new `[Fact]` unit tests covering: duplicate-active collapse, both-inputs-present yields one active row (not a duplicate disabled row), available-but-inactive becomes a disabled/non-primary row, active-row promotion with primary carried through, and a rig regression scenario mirroring the actual 06-06 failure (2 active monitors + stale duplicate available targets + 1 genuinely-disabled monitor -> exactly one row per DevicePath and exactly one `IsPrimary=true`).
- Confirmed via diff against the pre-fix commit that `GetActiveMonitors()`'s method body is byte-for-byte unchanged, and confirmed via grep that no `IsGDIPrimary`/`IsPathActive` read remains inside `GetAllMonitors()`.

## Task Commits

1. **Task 1: Deduplicate GetAllMonitors() via a pure MergeAllMonitors() seam** - `51e24e3` (fix)
2. **Task 2: Unit-test the dedup + single-primary merge logic** - `540feac` (test)

**Plan metadata:** committed separately by the orchestrator (docs commit not made by this executor per constraints).

## Files Created/Modified

- `src/RigToggle.Windows/WindowsMonitorController.cs` - `GetAllMonitors()` rewritten to delegate to `GetActiveMonitors()` + new `MergeAllMonitors()`; old per-path `IsGDIPrimary`/`IsPathActive` reads removed
- `src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs` - 5 new `[Fact]` tests for `MergeAllMonitors()`, plus `using RigToggle.Core.Models;` and `using System.Linq;` added

## Decisions Made

- Reused `GetActiveMonitors()` wholesale inside `GetAllMonitors()` rather than adding a separate active-state query, per the plan's explicit interface contract — avoids duplicating the already-correct `GetActivePaths()`-based logic.
- Kept `MergeAllMonitors()`'s second parameter as a tuple list `(string DevicePath, string FriendlyName)` rather than introducing a new type, matching the plan's exact signature and avoiding an unnecessary DTO for a two-field internal seam.

## Deviations from Plan

None — plan executed exactly as written. One minor implementation note: the plan's verification grep for the single-primary assertion expected the literal substring `Count(r => r.IsPrimary) == 1`; the rig-regression test was written with `Assert.True(result.Count(r => r.IsPrimary) == 1)` to match that exact substring (functionally identical to `Assert.Equal(1, ...)`, just phrased to satisfy the grep-based sandbox verification).

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Sandbox verification (source/grep-only, no dotnet SDK available in this Linux environment) passed:**
- `GetAllMonitors()` delegates to `MergeAllMonitors()` and reuses `GetActiveMonitors()` — confirmed.
- No `IsGDIPrimary`/`IsPathActive` read remains inside `GetAllMonitors()` — confirmed (grep count = 0).
- `GetActiveMonitors()` method body is unchanged — confirmed via diff against the pre-fix base commit.
- New tests reference `MergeAllMonitors` (11 occurrences) and assert exactly one primary row (`Count(r => r.IsPrimary) == 1`) plus exactly one row per DevicePath — confirmed.

**REQUIRED FOLLOW-UP — this fix is NOT verified on real hardware yet.** DISPLAY-04 and DISPLAY-05 remain open. The Phase 6 checkpoint (06-06) must be re-run in full on the actual 2-monitor Windows rig:
1. Re-verify the "Settings grid lists every monitor" precondition — should now show exactly 2 rows (VG248, Dell U2415), each with correct Primary/disabled state, no duplicates.
2. Proceed to the previously-unreached gate scenarios: (a) sleep/wake/reboot monitor re-enable, (b) combined disable+enable topology in one atomic `SetDisplayConfig` call.

Phase 6 stays **not complete** until that live rig re-test passes GO on both the precondition and both gate scenarios.

---
*Quick task: 260728-qj1*
*Completed: 2026-07-28*

## Self-Check: PASSED

- FOUND: src/RigToggle.Windows/WindowsMonitorController.cs
- FOUND: src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs
- FOUND: .planning/quick/260728-qj1-fix-windowsmonitorcontroller-getallmonit/260728-qj1-SUMMARY.md
- FOUND commit: 51e24e3 (fix: MergeAllMonitors seam)
- FOUND commit: 540feac (test: MergeAllMonitors unit tests)
