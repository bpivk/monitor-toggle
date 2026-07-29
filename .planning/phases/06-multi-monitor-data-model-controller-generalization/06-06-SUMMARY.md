---
phase: 06-multi-monitor-data-model-controller-generalization
plan: 06
subsystem: infra
tags: [winforms, ccd, windowsdisplayapi, monitor-enumeration]

requires:
  - phase: 06-03
    provides: WindowsMonitorController.GetAllMonitors()/ActivateMonitors()/DeactivateMonitors()/Restore()
provides:
  - Rig-validated multi-monitor disable/enable toggle, including the combined disable+enable topology and reboot/sleep re-enable scenarios
affects: [06-03, milestone-v1.1]

tech-stack:
  added: []
  patterns:
    - "GetAllMonitors() dedups by stable DevicePath and sources Active/Primary state only from GetActiveMonitors(), never from potentially-stale inactive PathInfo fields"
    - "Restore() only takes the raw in-process cache-replay fast path on an exact SetEquals match; any DISPLAY-05 enable-set monitor (or genuinely stale cache) routes through RestoreViaReconstruction(), which never submits a Source-ID hint captured across a mutation boundary"

key-files:
  created: []
  modified:
    - src/RigToggle.Windows/WindowsMonitorController.cs

key-decisions:
  - "Checkpoint required two gap-closure rounds (quick task 260728-qj1 for GetAllMonitors dedup; debug session monitor-not-active-on-restore for Restore()'s Source-staleness bug, itself requiring two investigation rounds) before reaching GO — both are real rig-only bugs the Linux planning sandbox's source-only verification could not have caught, confirming the mandatory rig-validation gate did its job."

patterns-established:
  - "Never replay a cached PathInfo/PathDisplaySource captured before an intervening CCD mutation that could have triggered driver source-ID renumbering — always re-query live immediately before reuse (RestoreViaReconstruction)."

requirements-completed: [DISPLAY-04, DISPLAY-05]

duration: multi-session (2026-07-28 initial NO-GO -> 2026-07-29 GO, across 2 gap-closure quick tasks + 1 two-round debug session)
completed: 2026-07-29
---

# Phase 6: Multi-Monitor Data Model & Controller Generalization — Plan 06 (Rig Checkpoint) Summary

**Full rig-validation checkpoint now GO — multi-monitor disable/enable, combined-topology toggle, and reboot/sleep re-enable all confirmed working on the real 2-monitor rig, after two rounds of rig-discovered gap-closure fixes.**

## Performance

- **Duration:** Spans 2026-07-28 (initial attempt, NO-GO) through 2026-07-29 (final GO), interleaved with 2 gap-closure quick tasks and a 2-round debug session
- **Tasks:** 1/1 — final result GO across all four checklist items (build, migration check, gate scenario (a), gate scenario (b))

## Go/No-Go Result

**GO.** All four checklist items pass:

1. **Build/test** — `dotnet publish`/`dotnet build` succeed on the rig; self-contained exe launches.
2. **DISPLAY-08 migration spot-check** — a genuine v1.0-era `settings.json` (singular `MonitorDevicePath` only) loads with that monitor already checked in the Disable column, no prompt/banner.
3. **Gate scenario (a) — long-idle/reboot re-enable** — disable VG248 (Rig Mode), sleep/wake or reboot, confirmed it comes back enumerable at its correct/native resolution. Confirmed by user 2026-07-29: "Everything is just fine."
4. **Gate scenario (b) — combined disable+enable topology** — disable-set = VG248, enable-set = Dell U2415 (a monitor normally kept OS-disabled). Toggle to Rig Mode: VG248 detaches, Dell activates, exactly one GDI primary, no overlap. Toggle back to Normal Mode: VG248 restored exactly, Dell returned to OS-disabled. Confirmed by user 2026-07-29 after the second gap-closure fix landed: "This works without errors now."

## Gap-Closure History (both rig-discovered, neither catchable from the Linux planning sandbox)

### Round 1 — `GetAllMonitors()` duplicate-row / dual-primary bug (initial NO-GO, 2026-07-28)

First rig attempt failed at the precondition step ("Settings grid lists every monitor"), before either gate scenario could even be attempted. Physical rig has 2 monitors; the grid showed 10 rows, including two simultaneously-"Primary" rows (structurally impossible in real Windows).

**Root cause:** `GetAllMonitors()` iterated every `PathInfo` from `PathInfo.GetAllPaths()` without deduplicating by the stable `DevicePath` — Windows accumulates multiple stale/inactive CCD paths per physical monitor over time, so each monitor appeared once per stale path. `IsPrimary` was also read directly off these same potentially-stale inactive paths.

**Fix:** Quick task `260728-qj1` — `GetAllMonitors()` rewritten to dedupe by `DevicePath` via a pure `MergeAllMonitors()` seam, sourcing Active/Primary state exclusively from the already-correct `GetActiveMonitors()`. Verified fixed on rig re-test (grid showed exactly 2 rows, correct single primary).

Separately, a follow-up quick task (`260728-rmp`) relabeled the Settings grid's Disable/Enable columns to "Off (Rig)"/"On (Rig)" with tooltips and an explanatory caption, after user feedback that the original labels didn't make clear the grid only configures the transition into Rig Mode (Normal Mode is always restored exactly as it was, never separately configured) — a UX clarification, not a correctness bug.

### Round 2 — `Restore()` Source-staleness bug on toggle-back with an enable-set monitor (2026-07-29)

Gate scenario (b)'s toggle-back direction failed: `Monitor: FAILED (Configured monitor(s) not currently active: {Dell U2415 device path})`, thrown by `DeactivateMonitors()`'s D-02 enable-set teardown call, immediately after `Restore()` had returned successfully with no exception.

**Root cause (found across 2 debug-session rounds — see `.planning/debug/resolved/monitor-not-active-on-restore.md` for full investigation detail):**
- Round 1 of the debug session found that `Restore()`'s in-process fast-path cache-acceptance guard (`SetEquals`) always failed whenever an enable-set was configured (the cache legitimately contains the enable-set monitor, but the pre-Rig-Mode snapshot never does) — fixed by widening to `IsSupersetOf`. Rig re-test showed the exact same failure recurred, proving this fix insufficient.
- Round 2 found the real defect: taking the fast path was itself unsafe. The cached entry for the enable-set monitor was captured *before* `DeactivateMonitors()`'s own topology-reducing mutation, which can trigger CCD/driver source-ID renumbering for the surviving target. Replaying that stale cached Source assignment via `ApplyPathInfos` doesn't throw (confirmed via reading WindowsDisplayAPI's own source — it only checks the raw Win32 status, never per-target outcome) but can silently leave the enable-set monitor inactive.

**Fix:** `Restore()`'s fast-path gate reverted to strict `SetEquals` (raw cache replay now used only for the simple no-enable-set case it was originally rig-proven for); every other case (including any DISPLAY-05 enable-set monitor) now routes through `RestoreViaReconstruction()` — the same Extend-plus-live-requery mechanism the crash-recovery path already used, extended to explicitly verify any enable-set monitor is still active after the corrected topology is applied. Never trusts a Source-ID hint captured across a mutation boundary. Rig-confirmed fixed 2026-07-29.

## Next Phase Readiness

Phase 6 is now complete — DISPLAY-04, DISPLAY-05, DISPLAY-06, DISPLAY-07, DISPLAY-08 all rig-validated. `IMonitorController`'s N-monitor triad (`GetAllMonitors`/`ActivateMonitors`/`DeactivateMonitors`/`Restore`) is proven correct on real hardware for both single-set and combined disable+enable configurations, including the crash-recovery-style reconstruction path. Phase 7 (shared toggle-orchestration helper) can build on this generalized, rig-hardened controller with confidence.

**Known residual behavior (not a defect):** RigToggle's disable/enable mutations are deliberately non-persistent (`saveToDatabase: false`/`allowPersistence: false`) — a sleep/wake or reboot resets monitors to Windows' own last-known topology independent of RigToggle's own mode state, in either direction (a disabled monitor can come back on its own, and an enable-set monitor can come back on even while nominally in Normal Mode). This is an accepted consequence of the deliberate choice never to persist changes to Windows' own display database outside explicit user-triggered toggles, not something Phase 6 was ever scoped to control.

---
*Phase: 06-multi-monitor-data-model-controller-generalization*
*Completed: 2026-07-29 (GO)*
