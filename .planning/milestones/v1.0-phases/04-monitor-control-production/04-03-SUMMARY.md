---
phase: 04-monitor-control-production
plan: 03
subsystem: display
tags: [ccd, windowsdisplayapi, displayconfig, monitor-toggle]

requires:
  - phase: 04-monitor-control-production (plan 01)
    provides: "Empirically-confirmed GO decision for repositioning-aware primary removal (spike/PHASE4-RETEST.md)"
  - phase: 04-monitor-control-production (plan 02)
    provides: "Full-topology MonitorState/MonitorPathSnapshot contract, real CaptureState()"
  - phase: 04-monitor-control-production (plan 04)
    provides: "DISPLAY-03 confirmation dialog, exercised by this plan's end-to-end rig checkpoint"
provides:
  - "Real WindowsMonitorController.Disable/Restore delivering DISPLAY-01 (true CCD disable) and DISPLAY-02 (exact restore)"
  - "In-process fast-path restore mechanism (cached pre-mutation PathInfo[]) as the primary restore path"
  - "Snapshot-reconstruction restore mechanism as the CORE-05 crash-recovery fallback path"
affects: [phase-05]

tech-stack:
  added: []
  patterns:
    - "In-process fast-path + snapshot-reconstruction fallback for CCD state restore"
    - "Reflection-patch of a read-only WindowsDisplayAPI backing field (OutputTechnology) where the public constructor surface has a gap"

key-files:
  created: []
  modified:
    - src/RigToggle.Windows/WindowsMonitorController.cs
    - src/RigToggle.Core/ToggleService.cs
    - src/RigToggle.App/MainForm.cs

key-decisions:
  - "Restore() tries an in-process cache of the exact pre-Disable() PathInfo[] array first (same mechanism proven by the Phase 1 spike and Plan 01's rig re-test), falling back to snapshot-based reconstruction only when no process-lifetime cache is available (crash recovery)."
  - "Monitor restore failures must propagate to MainForm's handler and must NOT clear the snapshot — only audio restore keeps the prior swallow-and-continue behavior (gap-closure 03-04), since a failed monitor restore leaves the screen disabled and the snapshot is the only way to retry."
  - "Toggle error dialog now shows the real exception type/message — this is a single-user diagnostic tool, not hardened multi-user software, so hiding errors cost more than it protected."

patterns-established:
  - "Two-tier restore strategy (in-process fast path / persisted-snapshot fallback) for any future CCD or hardware-state restore work."

requirements-completed: [DISPLAY-01, DISPLAY-02]

duration: ~4h (including live rig debugging across 5 iterations)
completed: 2026-07-24
---

# Phase 4 Plan 03: Real CCD Disable/Restore Summary

**Real repositioning-aware CCD monitor disable/restore, debugged live against the user's rig through 5 iterations after the sandbox-only implementation shipped with real bugs no Linux build could catch.**

## Performance

- **Duration:** ~4h wall-clock (includes real-hardware debugging, not just implementation)
- **Tasks:** 3/3 (Task 1 auto, Task 2 auto, Task 3 human-verify checkpoint — resolved via extended live debugging, not a single clean rig test)
- **Files modified:** 3

## Accomplishments

- `WindowsMonitorController.Disable`/`Restore` deliver true CCD-level monitor disable and exact restore (DISPLAY-01, DISPLAY-02), verified end-to-end against the user's real AMD Radeon/DisplayPort rig (VG248 primary + DELL U2415).
- Discovered and fixed 5 real bugs that only manifested on Windows hardware — none were catchable in this Linux execution sandbox, which has no `dotnet` toolchain and cannot build `net10.0-windows`/`WindowsDisplayAPI`-dependent code.
- Added a two-tier restore strategy: an in-process fast path (replays the exact pre-disable `PathInfo[]` array, mirroring the mechanism already proven by the Phase 1 spike and Plan 01's rig re-test) as primary, with the original snapshot-reconstruction approach demoted to a crash-recovery-only fallback.
- Fixed a serious state-loss bug in `ToggleService.ToggleToNormalMode()` (inherited from a Phase 3 gap-closure pattern) that silently discarded the only recoverable snapshot on a failed monitor restore.

## Task Commits

1. **Task 1: Implement repositioning-aware Disable with verify-and-throw** - `9152c73` (feat)
2. **Task 2: Implement Restore via live-identity re-resolution with verify-and-throw** - `bd7e692` (feat)
3. **Task 3: End-to-end rig verification** - resolved via the fix commits below, not a single pass/fail rig run

**Post-checkpoint fix commits (all found via live rig testing, not planned work):**

4. `6fde4b7` fix: import `WindowsDisplayAPI.Native.DisplayConfig` — the four CCD enum types used in `Restore()` (`DisplayConfigPixelFormat`/`Rotation`/`Scaling`/`ScanLineOrdering`) live in a different namespace than `WindowsDisplayAPI.DisplayConfig`; Windows build failed with 5 compiler errors (CS0246 x4, CS1503 x1 cascading from the first).
5. `224a057` fix: `ToggleToNormalMode()` was wrapping monitor `Restore()` in a swallow-and-continue try/catch borrowed from a Phase 3 audio-only gap-closure, then unconditionally clearing the snapshot — directly violating 04-CONTEXT.md D-05 and destroying the only recoverable state on a failed restore. Reproduced live: disable worked, restore silently failed, snapshot cleared anyway, black screen with nothing to retry against; recovered manually via Win+Ctrl+Shift+B.
6. `2bd53f6` fix: surfaced the real exception type/message in the toggle error dialog (was a generic "Something went wrong" with zero diagnostic detail) — needed to make the remaining bugs debuggable at all.
7. `e878ace` fix: `PathDisplayTarget.DevicePath` throws `TargetNotAvailableException` when `IsAvailable` is false; `Restore()`'s search over `GetAllPaths()` read `.DevicePath` unconditionally across every GPU output slot (not just the two physical monitors), crashing on an unrelated unavailable port. Reproduced live as `TargetNotAvailableException`.
8. `1b74f5d` fix: `Restore()` was trusting the inactive target's live-reported `DisplaySource`, risking collision with the source the active survivor was already using. Reserved active sources first, assigned inactive targets the first genuinely free source via `PathDisplaySource.GetDisplaySources()`. Reproduced live as `PathChangeException` ("Invalid paths information.").
9. `fd4fb2c` fix: `PathTargetInfo.OutputTechnology` has no public constructor parameter — every manually-reconstructed target silently defaulted to `Other` instead of the real `DisplayPortExternal`, a plausible CCD-validation rejection cause. Patched via reflection on the compiler-generated backing field, copying the correct value from the live query object. `PathChangeException` still persisted after this fix alone (confirmed via the added diagnostic message showing `tech=DisplayPortExternal` correctly, ruling this fix out as sufficient on its own).
10. `4eb5915` fix (the one that actually resolved it): added an in-process cache of the exact pre-`Disable()` `PathInfo[]` array, replayed directly by `Restore()` via the same mechanism already empirically proven twice on this rig (Phase 1 spike GO, Plan 01 rig re-test GO), instead of continuing to patch individual reconstruction-from-primitives bugs one at a time. Confirmed working live by the user.

## Files Created/Modified

- `src/RigToggle.Windows/WindowsMonitorController.cs` - Real `Disable`/`Restore`, in-process fast-path cache, reflection-based `OutputTechnology` patch (fallback path only)
- `src/RigToggle.Core/ToggleService.cs` - Monitor restore failures now propagate instead of being silently swallowed; snapshot survives a failed restore
- `src/RigToggle.App/MainForm.cs` - Toggle error dialog surfaces real exception detail

## Decisions Made

- **Two-tier restore strategy**, not a single mechanism: the in-process fast path (exact replay of captured `PathInfo[]`) is primary since it reuses an already-proven mechanism and sidesteps every reconstruction pitfall found live; the snapshot-based reconstruction (with its 3 field-level fixes) remains only for the CORE-05 crash-recovery case where no in-memory state can survive a process restart. This is a stronger contract than the plan originally specified, added because the original single-mechanism design proved fragile against real Windows CCD validation.
- **Monitor restore failures must never be silently swallowed or allowed to destroy the snapshot** — this was a real, reproduced incident (black screen, no state left to retry from), not a hypothetical. Audio restore's existing swallow-and-continue behavior (Phase 3 gap-closure 03-04) was deliberately left untouched since a genuinely-unplugged audio device is a different, unrecoverable-on-retry failure mode.
- **Exception detail is shown to the user**, not hidden behind a generic message — this is a single-user diagnostic desktop tool, and hiding the real error made every one of the above bugs undiagnosable from the app alone.

## Deviations from Plan

The plan anticipated a single blocking rig checkpoint (Task 3) resolved by one pass/fail rig run. What actually happened was 5 iterations of real Windows-only bugs surfaced by live testing, none reproducible or catchable in this Linux sandbox (no `dotnet` toolchain, no way to build `net10.0-windows` or exercise `WindowsDisplayAPI`). Each was root-caused against the library's actual source (fetched via GitHub) rather than guessed, except the final structural fix (in-process fast-path), which was a deliberate architecture change once 3 consecutive field-level reconstruction bugs made clear that from-scratch CCD topology reconstruction is inherently fragile.

**Total deviations:** 6 unplanned fix commits beyond the original 2 implementation commits.
**Impact on plan:** All fixes were required for DISPLAY-01/DISPLAY-02 to actually work — none were scope creep. The in-process fast-path is architecturally stronger than the original plan's single-mechanism design.

## Issues Encountered

- **Real outage risk during debugging:** disabling the primary monitor without a working restore path left the user's screen black on 2 separate occasions during this session. Recovered via `Win+Ctrl+Shift+B` (Windows display re-detection) both times — confirmed safe in advance because `Disable`/`Restore` always call `ApplyPathInfos` with `saveToDatabase: false`, so nothing is persisted as Windows' saved boot configuration; a reboot was always available as a guaranteed-safe fallback.
- **Filesystem/sync confusion:** the user's Windows build folder is a separate manual copy (`C:\Users\Blaz\Desktop\New folder (2)`, later also referenced as `bpivk/moza`), not a git-synced clone — several early fix attempts required extracting file content from unmerged worktree branches for manual copy-paste before this was clarified.
- **Known follow-up (out of this phase's scope):** Moza Companion app is confirmed running after toggling to Rig Mode but its window sometimes doesn't come to the foreground — plausibly `SetForegroundWindow`'s well-known Win32 restriction (fails when the calling process isn't already in the foreground). This is Phase 3 scope (APP-01/APP-02, already shipped) and does not block DISPLAY-01/02/03. Logged in STATE.md Pending Todos for future investigation.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- DISPLAY-01, DISPLAY-02, DISPLAY-03 all confirmed working end-to-end on real hardware: disable, restore (exact position/primary/orientation), confirmation dialog (show-once + don't-ask-again + reset-on-monitor-change).
- Phase 5 (orchestration/packaging) can proceed — the core monitor toggle mechanism is now real and rig-verified, not a stub.
- Follow-up (non-blocking): Moza Companion foreground-focus reliability after a toggle — worth a small investigation in a future phase, not urgent.

---
*Phase: 04-monitor-control-production*
*Completed: 2026-07-24*
