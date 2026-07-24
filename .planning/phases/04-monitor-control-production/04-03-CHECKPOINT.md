---
phase: 04-monitor-control-production
plan: 03
status: paused-at-checkpoint
checkpoint-task: "Task 3: End-to-end rig verification of disable/restore + confirmation dialog"
---

# Phase 4 Plan 03: Checkpoint Progress Note

**This plan is NOT complete.** Tasks 1 and 2 (auto) are implemented and committed. Task 3 is a
blocking `checkpoint:human-verify` gate requiring real Windows/AMD Radeon/DisplayPort rig
hardware, which this Linux execution sandbox cannot provide (per the plan's own scope note:
"Because this sandbox cannot build/run net10.0-windows code, the executor writes the
implementation and a blocking rig human-verify checkpoint confirms the full end-to-end phase
behavior... on real hardware"). No `04-03-SUMMARY.md` has been created — that will happen once
Task 3's rig verification result is available (either in this worktree if resumed, or by a
fresh continuation agent).

## Tasks Completed This Session

1. **Task 1: Implement repositioning-aware Disable with verify-and-throw** — `9152c73` (feat)
   - `WindowsMonitorController.Disable(string monitorDevicePath)` implements 04-RESEARCH.md
     Pattern 1 (repositioning-aware survivor reconstruction via a uniform position delta,
     Pitfall 1) + Pattern 3 (verify-and-throw against a fresh `GetActivePaths()` re-query, D-03).
   - Static grep gates verified: `ApplyPathInfos` present, `exactlyOnePrimary` present, no
     `Screen.AllScreens` reference anywhere in the file (D-04).

2. **Task 2: Implement Restore via live-identity re-resolution with verify-and-throw** — `bd7e692` (feat)
   - `WindowsMonitorController.Restore(MonitorState previousState)` implements Pattern 4
     (live-identity re-resolution via `GetAllPaths()` matched on stored `DevicePath`, mode/signal
     rebuilt from the STORED snapshot per Pitfall 2, missing-match throws per Pitfall 5, uses
     `First`/`FirstOrDefault` never `Single` per Open Question 2) + the same verify-and-throw
     idiom (D-03), throwing `InvalidOperationException` with message containing "did not
     reproduce" on mismatch.
   - Static grep gates verified: `GetAllPaths` present, `PathTargetInfo(` present, "did not
     reproduce" present.

Both tasks were code-reviewed against the plan's `<acceptance_criteria>` and the
`04-RESEARCH.md` Pattern 1/3/4 code examples; no `dotnet build`/`dotnet test` could be run in
this Linux sandbox (net10.0-windows + WindowsDisplayAPI has no Linux target). No deviations
from the plan were needed — both methods follow the plan's action text and the RESEARCH
patterns essentially verbatim, matching the `WindowsAudioController.ApplyAndVerify` message
style (Phase 3 precedent) for exception wording.

## Task Pending

**Task 3: End-to-end rig verification of disable/restore + confirmation dialog**
(`type="checkpoint:human-verify" gate="blocking"`) — requires the user's real Windows rig.
See the CHECKPOINT REACHED report returned alongside this commit for the full
what-built/how-to-verify/resume-signal content (mirrors the plan's Task 3 verbatim).

## Files Modified

- `src/RigToggle.Windows/WindowsMonitorController.cs` — `Disable`/`Restore` real
  implementations (Tasks 1-2); `GetActiveMonitors`/`CaptureState` unchanged from Plan 02.

## Self-Check

- FOUND: src/RigToggle.Windows/WindowsMonitorController.cs (modified, contains
  `ApplyPathInfos`, `exactlyOnePrimary`, `GetAllPaths`, `PathTargetInfo(`, "did not reproduce";
  no `Screen.AllScreens` reference)
- FOUND: commit 9152c73 (Task 1)
- FOUND: commit bd7e692 (Task 2)

---
*Phase: 04-monitor-control-production*
*Paused at: Task 3 (blocking rig human-verify checkpoint) — 2026-07-24*
