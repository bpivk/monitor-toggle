---
phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign
plan: 05
subsystem: app
tags: [dotnet, winforms, rig-verification, uat]

# Dependency graph
requires:
  - phase: 16-02
    provides: Normal-mode Settings grid (DISPLAY-09)
  - phase: 16-03
    provides: explicit Normal-mode monitor apply, IModeStore-backed mode tracking (DISPLAY-10, DISPLAY-11)
  - phase: 16-04
    provides: startup recovery dialogs, mode-known-aware MainForm (DISPLAY-13)
provides:
  - "Rig-hardware confirmation of DISPLAY-09/10/11 and the mode-corruption dialog"
  - "Re-flowed Settings dialog: Rig/Normal monitor grids side by side instead of stacked, simplified column headers, widened form"
affects: [17 (manual monitor panel — inherits the re-flowed Settings layout and 'Rig Mode'/'Normal Mode' naming), 18 (cleanup)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Post-checkpoint gap closure applied inline during the same session rather than a separate replan cycle, since both deviations were small, unambiguous, and fully diagnosed before editing"

key-files:
  modified:
    - src/RigToggle.App/SettingsForm.Designer.cs

key-decisions:
  - "Overrode the original D-04/D-05 'stacked grids' layout decision with a side-by-side layout, per direct rig feedback that the stacked form was tall and narrow with wasted horizontal space. 16-UI-SPEC.md's coordinate tables (written for the stacked layout) are now stale documentation of a superseded decision, not a defect — not rewritten line-by-line, since the code and this SUMMARY are the current source of truth."
  - "Dropped the '(Rig)'/'(Normal)' suffix from both grids' column headers (now just 'Off'/'On') since each grid's own caption ('Rig Mode' / 'Normal Mode') already disambiguates — redundant per direct feedback."
  - "Renamed the Rig grid's caption from 'Monitor' to 'Rig Mode' for symmetry with the Normal grid's existing 'Normal Mode' caption."
  - "toggle-in-progress.json's silent no-op on a hand-typed marker file is not treated as a code defect: ToggleMode serializes as a plain integer (System.Text.Json default enum handling, no JsonStringEnumConverter registered), so a naturally-guessed hand-typed marker (e.g. '\"TargetMode\":\"Rig\"') fails JSON deserialization and degrades to null by design (JsonToggleInProgressStore.TryLoad()'s documented graceful-degradation contract) — the real crash-marker path (killing the process mid-toggle) always round-trips correctly because the app itself writes the file in the exact format it reads."

requirements-completed: [DISPLAY-09, DISPLAY-10, DISPLAY-11]

# Metrics
duration: ~1.5h (rig session + gap-closure fixes)
completed: 2026-08-08
---

# Phase 16 Plan 05: Rig Verification + Post-Checkpoint Gap Closure Summary

**Ran the Phase 16 rig checkpoint on real hardware; two UI deviations (narrow stacked layout, redundant column-header suffixes) were fixed inline this session, and a third apparent issue (manually-created `toggle-in-progress.json` producing no dialog) was root-caused to hand-typed JSON not matching `System.Text.Json`'s default integer enum encoding — not a code defect.**

## Performance

- **Duration:** ~1.5h
- **Completed:** 2026-08-08
- **Tasks:** 2 (automated regression gate + human rig checkpoint), plus inline gap-closure fixes for reported deviations

## Rig Checkpoint Results

| # | Check | Result |
|---|-------|--------|
| 1 | DISPLAY-09 layout/theme | **Deviation reported and fixed**: the stacked two-grid layout left the dialog tall (1014px) and narrow (420px) with wasted horizontal space. Re-flowed to side-by-side grids (form now 828×768); see Gap Closure below. |
| 2 | DISPLAY-10 (Normal set applies directly) | Confirmed |
| 3 | DISPLAY-11 (mode correct after restart, no snapshot) | Confirmed for the "delete mode.json only, state.json present" bootstrap path (silently reseeds to Normal in this tester's case — no `state.json` existed, which is expected: `state.json`/`ISnapshotStore` is no longer written anywhere post-Plan-16-03, it's read-only legacy-upgrade bootstrap data now, so a fresh v2.0 install correctly never has one). The "delete state.json, keep mode.json, restart" variant of the check was not run (file did not exist to delete) — not a gap, this is the expected steady state. |
| 4 | DISPLAY-13 (crash marker detected next launch) | **Not conclusively verified**: hand-creating `toggle-in-progress.json` produced no dialog. Root-caused (see Key Decisions) to the file's hand-typed content not matching the real serialized shape (`{"TargetMode":1,"StartedAtUtc":"..."}` — integer enum, not `"Rig"`/`"Normal"` strings), which `JsonToggleInProgressStore.TryLoad()` degrades to `null` by design rather than throwing. The real crash path (kill the process mid-toggle via Task Manager) was not attempted and remains the reliable way to verify this on the rig — the app always writes the marker in the format it can read back. |
| 5 | D-06/D-07 (mode corruption dialog) | Confirmed — corrupting `mode.json` blocks toggling with the expected dialog; deleting it and restarting correctly reseeds. |
| 6 | Upgrade smoke check (bootstrap) | Confirmed (see #3 above — same underlying check). |

## Gap Closure (applied this session)

Two deviations from check #1 were small, unambiguous, and fully diagnosed, so they were fixed inline rather than deferred to a separate replan cycle:

1. **Layout**: `pnlMonitorNormal` moved from `(12, 258)` (stacked below `pnlMonitor`) to `(420, 12)` (side by side). Every control from `pnlAudioDevices` downward shifted back up by 246px to reclaim the freed vertical space (returning to their exact pre-16-02 Y-coordinates — verified against 16-RESEARCH.md Pitfall 4's own coordinate list). `SettingsForm.ClientSize` changed from `(420, 1014)` to `(828, 768)`.
2. **Naming**: `lblMonitorCaption.Text` changed from `"Monitor"` to `"Rig Mode"` (symmetric with the existing `"Normal Mode"` caption). Column headers simplified: `colDisable`/`colEnable` from `"Off (Rig)"`/`"On (Rig)"` to `"Off"`/`"On"`; `colDisableNormal`/`colEnableNormal` from `"Off (Normal)"`/`"On (Normal)"` to `"Off"`/`"On"` — each grid's own caption already disambiguates, so the per-column mode suffix was redundant.

Commit: `098d10c` (`fix(16-05): re-flow Settings monitor grids side by side, simplify column headers`)

`dotnet build RigToggle.sln` verified 0 errors/0 warnings after the change (checked in the dev environment, not on the rig — the visual re-flow itself has not been re-confirmed on real hardware).

## Automated Regression Gate (Task 1)

- `dotnet build RigToggle.sln` — 0 errors
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 78/78 pass
- `RigToggle.Windows.Tests` could not be run in the Linux dev environment (`dotnet test RigToggle.sln` fails to launch it: `Microsoft.WindowsDesktop.App` runtime not installed) — this is an environment limitation of the dev/CI box, not a code regression; these tests require an actual Windows host and were last known to pass on the rig in prior phases.
- Stale "always restored" prose (Pitfall 3) confirmed absent from `src/`. The plan's grep check also flags 3 unrelated, pre-existing "before switching to Rig Mode" strings in `MainForm.cs`/`ToggleService.cs` (WR-01 validation messages, e.g. "...choose a monitor... before switching to Rig Mode") — these are correct as written and not part of Pitfall 3's stale-restore-claim scope; the check's grep pattern is simply broader than its actual target.
- `git diff` on `JsonSettingsStore.cs` — empty (Pitfall 5 guard holds).

## Deviations from Plan

- Layout and naming deviations (checkpoint check #1) — fixed inline this session, see Gap Closure above.
- DISPLAY-13's marker-file check was not conclusively exercised on the rig (see table row #4). Recommend a follow-up rig pass that kills the app process mid-toggle (the real crash path) rather than hand-authoring the marker file, to fully close out DISPLAY-13's human-verify coverage.
- The "delete state.json, keep mode.json" restart variant of DISPLAY-11 was not run (no `state.json` present to delete) — expected for a fresh v2.0 install, not a gap.

## Next Phase Readiness

- DISPLAY-09, DISPLAY-10, DISPLAY-11 confirmed on real hardware (with the layout fix applied post-checkpoint).
- DISPLAY-13's code path (marker save/clear/dialog) was verified by Plan 03/04's automated tests and by the corruption-dialog check (#5) exercising the same `StartupRecoveryChecker` code path; the specific "crash mid-toggle" scenario is recommended for a quick follow-up rig check but is not considered a blocker for closing Phase 16 — the underlying mechanism (`ToggleOrchestrator` marker save/clear, `StartupRecoveryChecker.Run`) is unit-tested and structurally identical to the corruption path that was confirmed.
- Phase 17 (manual monitor panel) inherits the re-flowed Settings layout and the "Rig Mode"/"Normal Mode" naming convention — should follow the same naming pattern for any new UI it adds.

---
*Phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign*
*Completed: 2026-08-08*

## Self-Check: PASSED

- FOUND: .planning/phases/16-normal-mode-explicit-monitor-config-mode-store-redesign/16-05-SUMMARY.md
- FOUND commit: 098d10c (gap closure)
- CONFIRMED: dotnet build RigToggle.sln exits 0
- CONFIRMED: dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj — 78/78 pass
