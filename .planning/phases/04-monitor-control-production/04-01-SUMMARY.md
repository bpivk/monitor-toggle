---
phase: 04-monitor-control-production
plan: 01
subsystem: infra
tags: [windowsdisplayapi, ccd, spike, display-config, dotnet]

# Dependency graph
requires:
  - phase: 01-monitor-disable-spike
    provides: "Confirmed non-primary CCD topology-path-removal works on this rig (AMD Radeon/DisplayPort), and identified the primary-removal PathChangeException gap (Finding 3)."
provides:
  - "Extended spike tool `--disable-primary` mode implementing RESEARCH Pattern 1 (repositioning-aware survivor reconstruction)"
  - "spike/PHASE4-RETEST.md capture template for the rig operator to record the A1/A2 GO/NO-GO outcome"
affects: [04-monitor-control-production plan 03 (WindowsMonitorController.Disable/Restore implementation)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Repositioning-aware primary-path removal: shift ALL survivor PathInfo objects by a single uniform delta (not just the promoted one) via the public PathInfo constructor before ApplyPathInfos, since Position has no setter."
    - "GetActivePaths() (not Screen.AllScreens) as the authoritative disable/enable oracle; Screen.AllScreens is informational only due to known staleness (Phase 1 Finding 2)."

key-files:
  created: [spike/PHASE4-RETEST.md]
  modified: [spike/MonitorDetachSpike/Program.cs]

key-decisions:
  - "Reused the existing spike tool's RunDisable/VerifyOnce/prompt-to-restore idiom for the new RunDisablePrimary method rather than introducing new abstractions — this remains a throwaway diagnostic tool per plan scope."
  - "Wrapped ApplyPathInfos in try/catch with an explicit restore-after-failure attempt, so a NO-GO run doesn't leave the rig in a half-applied topology."

patterns-established:
  - "Pattern 1 (repositioning-aware removal): documented in RESEARCH lines 167-217, now has a concrete throwaway-tool implementation the rig operator can run before any production code lands."

requirements-completed: []  # DISPLAY-01/DISPLAY-02 remain pending production implementation (Plan 03); this plan only de-risks the mechanism.

# Metrics
duration: ~15min (Tasks 1-2; Task 3 is a blocking rig checkpoint, not yet resolved)
completed: 2026-07-24
---

# Phase 4 Plan 01: Repositioning-Aware Primary-Removal Spike Summary

**Extended the Phase 1 throwaway spike tool with a `--disable-primary` mode implementing RESEARCH Pattern 1 (delta-shift survivor reconstruction), plus a PHASE4-RETEST.md capture template — de-risking Assumption A1/A2 before any production `WindowsMonitorController` code is written. Rig hardware verification (Task 3) is a blocking checkpoint awaiting the user.**

## Performance

- **Duration:** ~15 min for Tasks 1-2 (auto tasks)
- **Started:** 2026-07-24T19:40:00Z (approx)
- **Tasks:** 2 of 3 completed (Task 3 is a blocking human-verify checkpoint requiring real Windows/AMD/DisplayPort hardware this sandbox cannot provide)
- **Files modified:** 2

## Accomplishments
- `spike/MonitorDetachSpike/Program.cs` now has a `--disable-primary <index>` mode that reconstructs every survivor `PathInfo` via the public constructor, shifted by a single computed delta, so exactly one lands at `(0,0)` before calling `ApplyPathInfos` — exactly RESEARCH Pattern 1.
- `ApplyPathInfos` is wrapped in try/catch surfacing `PathChangeException` type + message (rather than an unhandled crash as in the Phase 1 spike), with a restore-after-failure attempt so a NO-GO run doesn't strand the rig in a broken topology.
- Verification uses `PathInfo.GetActivePaths()` as the authoritative oracle (not `Screen.AllScreens`, consistent with Phase 1 Finding 2's staleness caveat) and adds a new `PathInfo.GetAllPaths()` probe to confirm Assumption A2 (the disabled monitor stays discoverable for restore-time re-attachment).
- `spike/PHASE4-RETEST.md` created mirroring `spike/RESULTS-TEMPLATE.md`'s structure: build/run instructions, a results table covering all five required checks, and an explicit GO (Pattern 1 as-is) / NO-GO (P/Invoke fallback per STACK.md) decision branch for Plan 03.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add repositioning-aware --disable-primary mode to the spike tool** - `59eb404` (feat)
2. **Task 2: Add the Phase 4 re-test capture template** - `c5da4a1` (docs)
3. **Task 3: Rig re-test of repositioning-aware primary removal (A1/A2)** - NOT STARTED (blocking checkpoint, requires Windows rig hardware)

**Plan metadata:** pending — plan not yet complete (checkpoint outstanding); no `docs: complete plan` commit made.

## Files Created/Modified
- `spike/MonitorDetachSpike/Program.cs` - Added `--disable-primary` command case and `RunDisablePrimary` method implementing Pattern 1
- `spike/PHASE4-RETEST.md` - New capture template for the rig operator's GO/NO-GO decision

## Decisions Made
- Reused the existing spike tool's structural idioms (bounds-check style, restore-on-Enter prompt) rather than introducing new abstractions, keeping this a throwaway diagnostic per plan scope (no production abstractions, no new source files beyond the template).
- Added a restore-after-failure attempt inside the `PathChangeException` catch block (not explicitly required by the plan's numbered steps, but consistent with the plan's threat model T-04-01-01 mitigation: "a restore path... is always offered so a bad topology is reversible") — this is a Rule 2 (missing critical functionality) addition since a NO-GO run without an attempted restore could leave the rig visually broken until the operator manually intervenes.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added restore-after-failure inside the PathChangeException catch block**
- **Found during:** Task 1 (RunDisablePrimary implementation)
- **Issue:** The plan's step 5 only specifies printing the exception type/message on `PathChangeException`; it doesn't explicitly require attempting a restore after a caught failure. Given the plan's own threat model (T-04-01-01) requires "a restore path is always offered so a bad topology is reversible," a caught exception that returns without restoring would leave the rig operator in a state where the reduced-but-failed-to-fully-apply topology (if partially applied) or an unclear state persists with no offered recovery.
- **Fix:** Added a nested try/catch that attempts `PathInfo.ApplyPathInfos(originalActivePaths, allowChanges: true)` immediately after printing the `PathChangeException` details, with its own failure surfaced as a WARNING (not a second crash).
- **Files modified:** spike/MonitorDetachSpike/Program.cs
- **Verification:** Manual code review against the plan's threat model T-04-01-01 mitigation text; grep gates for Task 1 all pass.
- **Committed in:** 59eb404 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 missing critical / threat-model mitigation)
**Impact on plan:** Necessary for correctness per the plan's own threat model. No scope creep — still a throwaway diagnostic tool, no new files or production abstractions added.

## Issues Encountered
- This sandbox is Linux and cannot build or run `net10.0-windows` targets or execute the WindowsDisplayAPI-dependent code. Tasks 1 and 2 (writing C# source and the markdown template) were completable without compilation. Task 3 (the rig hardware re-test) fundamentally requires the user's actual Windows/AMD Radeon/DisplayPort rig and cannot be simulated, fabricated, or skipped — per RESEARCH Open Question 1, no further static research can resolve Assumption A1/A2, only empirical hardware testing can.

## User Setup Required

None - no external service configuration required. However, **Task 3 requires the user to physically run the extended spike tool on the rig PC** (see Checkpoint below) — this is not an external service but a hardware-dependent verification step that only the user can perform.

## Next Phase Readiness

**Plan 01 is NOT complete.** Tasks 1 and 2 (the auto tasks) are done and committed. Task 3 is a blocking `checkpoint:human-verify` gate that requires the user to run `dotnet run --project spike/MonitorDetachSpike -- --disable-primary <index>` on the actual rig hardware and record the GO/NO-GO outcome in `spike/PHASE4-RETEST.md`.

Plan 03 (production `WindowsMonitorController.Disable`/`Restore` implementation) is blocked on this plan's Task 3 outcome:
- **If GO:** Plan 03 implements RESEARCH Pattern 1 as documented (lines 167-217 of `04-RESEARCH.md`) as-is.
- **If NO-GO:** Plan 03 must pivot to the raw P/Invoke `SetDisplayConfig` fallback documented in `.planning/research/STACK.md`'s Alternatives table.

## Self-Check: PASSED

Verified all claims below before finalizing this summary:
- FOUND: spike/MonitorDetachSpike/Program.cs (modified, contains `--disable-primary`, `GetAllPaths`, `new PathInfo(`)
- FOUND: spike/PHASE4-RETEST.md (created, contains `GO`/`NO-GO` decision field and `--disable-primary` references)
- FOUND: commit 59eb404 (Task 1)
- FOUND: commit c5da4a1 (Task 2)

---
*Phase: 04-monitor-control-production*
*Completed: Tasks 1-2 only; Task 3 pending user rig verification*
