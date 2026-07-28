---
phase: 06-multi-monitor-data-model-controller-generalization
plan: 06
subsystem: infra
tags: [winforms, ccd, windowsdisplayapi, monitor-enumeration]

requires:
  - phase: 06-03
    provides: WindowsMonitorController.GetAllMonitors()/ActivateMonitors()/DeactivateMonitors()
provides:
  - Rig-hardware evidence that GetAllMonitors() has a real enumeration bug
affects: [06-03, gap-closure]

tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified: []

key-decisions:
  - "Checkpoint recorded as NO-GO — phase not marked complete pending a gap-closure fix to GetAllMonitors()"

patterns-established: []

requirements-completed: []  # No requirements close out on a NO-GO — DISPLAY-04/05 remain open pending the fix.

duration: (interrupted — precondition failed before gate scenarios (a)/(b) could be attempted)
completed: 2026-07-28
---

# Phase 6: Multi-Monitor Data Model & Controller Generalization — Plan 06 (Rig Checkpoint) Summary

**Rig testing surfaced a real `GetAllMonitors()` enumeration bug before gate scenarios (a)/(b) could even be attempted — checkpoint is a NO-GO.**

## Performance

- **Duration:** N/A — stopped at the "grid lists every monitor" precondition (step 2 of `<how-to-verify>`), before reaching scenarios (a)/(b)
- **Tasks:** 1/1 attempted, FAILED (precondition, not the gate scenarios themselves)

## Accomplishments

- Confirmed the app builds and runs on the real Windows rig (`dotnet publish`/`dotnet build` succeeded; the self-contained exe launches).
- Rig hardware exposed a real bug in `WindowsMonitorController.GetAllMonitors()` that source-only/grep-based verification (the only verification available in the Linux planning sandbox) could not have caught.

## Go/No-Go Result

**NO-GO.**

### Precondition check ("Settings grid lists every monitor") — FAILED

Physical rig has exactly 2 monitors (a VG248 and a Dell U2415). The Settings grid instead showed:
- `VG248` — Primary
- `Dell U2415` — Primary
- `Dell U2415` again — no primary tag
- `VG248` × 4 — "(currently OS-disabled)"
- `Dell U2415` × 3 — "(currently OS-disabled)"

10 rows for 2 physical monitors, including two simultaneously-"Primary" rows (structurally impossible in real Windows — there is only ever one GDI primary).

### Root cause (confirmed by source read, not yet rig-retested)

`WindowsMonitorController.GetAllMonitors()` (added in Plan 06-03) iterates every `PathInfo` returned by `PathInfo.GetAllPaths()` and every `PathTargetInfo` within it, without deduplicating by the stable `DevicePath` identifier. `GetAllPaths()` returns one entry per historical CCD path — Windows accumulates multiple stale/inactive paths referencing the same physical monitor over time (port changes, driver updates, etc.) — so each physical monitor appears once per stale path instead of once total.

Compounding this, `IsPrimary` is computed per-`PathInfo` (`path.IsModeInformationAvailable && path.IsGDIPrimary`) directly off these same potentially-stale/inactive paths. This is the same "inactive-path fields are set to default/unreliable values" landmine already documented elsewhere in this codebase (`Restore()`'s own comments, citing Microsoft's docs) — it was correctly worked around in `Restore()` and `DeactivateMonitors()`, but `GetAllMonitors()` (new, Plan 06-03) didn't apply the same discipline.

06-RESEARCH.md's own Environment Availability section flagged this exact class of risk as unvalidatable without live hardware (Assumptions A1/A2, confidence MEDIUM) — this is precisely why the rig-validation checkpoint exists, and it did its job.

### Gate scenarios (a) and (b) — NOT ATTEMPTED

Testing stopped at the precondition; the sleep/wake/reboot re-enable scenario and the combined disable+enable topology scenario were never reached.

## Next Phase Readiness

**Blocking:** `GetAllMonitors()` must be fixed to (1) deduplicate by `DevicePath` — exactly one `MonitorInfo` row per physical monitor — and (2) source `IsPrimary`/`IsActive` only from `GetActiveMonitors()` (already correct, `GetActivePaths()`-based) for monitors that are currently active, and hard-code `IsPrimary: false` for monitors not currently active (a disabled monitor cannot be primary by definition).

Phase 6 remains **not complete**. Fix tracked as a gap-closure task against Plan 06-03; this checkpoint (06-06) must be re-run in full (including the two gate scenarios) once the fix lands.

---
*Phase: 06-multi-monitor-data-model-controller-generalization*
*Completed: 2026-07-28 (NO-GO)*
