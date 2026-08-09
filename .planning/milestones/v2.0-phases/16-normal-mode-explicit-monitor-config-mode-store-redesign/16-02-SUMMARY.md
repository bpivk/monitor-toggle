---
phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign
plan: 02
subsystem: ui
tags: [winforms, datagridview, settings-form, app-settings, monitor-config]

# Dependency graph
requires:
  - phase: 15-optional-app-audio-targets
    provides: AppSettings' established all-nullable-field convention and SettingsForm's Save/Validate gating pattern
provides:
  - AppSettings.NormalMonitorsToDisable / NormalMonitorsToEnable nullable List<string> fields
  - A second "Normal Mode" monitor grid (dgvMonitorsNormal) in SettingsForm, stacked below the Rig grid
  - PopulateMonitorGridNormal / OnMonitorNormalCellValueChanged / GetGridSelectionNormal duplicated (not shared) grid logic
  - Save-time stale-preserving merge for the Normal grid's selection
  - Corrected (no-longer-false) Rig-grid explanation/tooltip strings
affects: [16-03 (ToggleService rewrite consumes NormalMonitorsToDisable/ToEnable), 16-04 (mode-store/startup dialogs), 17 (manual monitor panel, shared safety guard unification)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Duplicated (not shared/generalized) per-grid population/validation/save logic for a second DataGridView instance, mirroring the existing single-grid precedent exactly"
    - "Independent per-grid reentrancy guard field (_updatingMonitorGridNormalProgrammatically) for the D-04 sibling-uncheck mechanism, never sharing the Rig grid's flag"

key-files:
  created: []
  modified:
    - src/RigToggle.Core/Models/AppSettings.cs
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs

key-decisions:
  - "AppSettings gains flat sibling NormalMonitorsToDisable/NormalMonitorsToEnable properties (not a nested MonitorTarget structure) to match the existing MonitorsToDisable/MonitorsToEnable precedent exactly and minimize call-site churn"
  - "JsonSettingsStore.cs left completely untouched — the two new nullable List<string>? properties round-trip through System.Text.Json with zero migration/default-population code (Pitfall 5)"
  - "Normal grid's ValidateSettingsForm check never blocks Save on an all-empty configuration (D-01) — no WouldLeaveAtLeastOneMonitorActive cross-check is applied to the Normal grid; that safety guard remains apply-time-only in WindowsMonitorController per RESEARCH.md's explicit anti-pattern warning"

requirements-completed: [DISPLAY-09]

# Metrics
duration: ~15min
completed: 2026-08-05
---

# Phase 16 Plan 02: Normal-Mode Monitor Settings UI Summary

**Added symmetric `NormalMonitorsToDisable`/`NormalMonitorsToEnable` fields to `AppSettings` plus a fully wired second "Normal Mode" monitor grid in `SettingsForm`, stacked below the existing Rig grid, with independent population/validation/save-merge logic and corrected stale "always restored" copy.**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-08-05T05:10:00Z (approx)
- **Completed:** 2026-08-05T05:15:25Z
- **Tasks:** 2 completed
- **Files modified:** 3

## Accomplishments
- `AppSettings` now carries `NormalMonitorsToDisable`/`NormalMonitorsToEnable` as flat nullable sibling fields to the existing Rig-mode fields, with zero changes to `JsonSettingsStore.cs` (correct diff is empty, per Pitfall 5)
- `SettingsForm` gained a second, fully-themed "Normal Mode" monitor grid (`dgvMonitorsNormal`) stacked directly below the Rig grid (D-04/D-05), with `"Off (Normal)"`/`"On (Normal)"` columns, its own explanation label, and every downstream control reflowed +246px (`ClientSize` grew from `(420,768)` to `(420,1014)`)
- Normal grid population, sibling-uncheck validation, save-merge (with stale-entry preservation), and theming (`Load` + `OnThemeChanged`) are fully duplicated (not shared) alongside the Rig grid's existing logic, per the established single-grid precedent
- Three now-false "Normal Mode is always restored exactly as it was before" strings in the Rig grid were corrected to mode-agnostic wording ahead of Plan 16-03's snapshot-restore removal

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Normal-mode fields to AppSettings and lay out the Normal grid + reflow in the designer** - `fc5ee27` (feat)
2. **Task 2: Duplicate grid population, validation, save-merge, and theming for the Normal grid** - `8b2ea39` (feat)

**Plan metadata:** (this commit) - `docs: complete plan`

## Files Created/Modified
- `src/RigToggle.Core/Models/AppSettings.cs` - Added `NormalMonitorsToDisable`/`NormalMonitorsToEnable` nullable `List<string>?` properties
- `src/RigToggle.App/SettingsForm.Designer.cs` - New `pnlMonitorNormal` panel/grid/labels, full downstream Y-reflow (+246px), `ClientSize` grown to `(420,1014)`, three stale Rig-grid strings corrected
- `src/RigToggle.App/SettingsForm.cs` - `PopulateMonitorGridNormal`, `OnMonitorNormalCellValueChanged`, `DgvMonitorsNormal_CurrentCellDirtyStateChanged`, `GetGridSelectionNormal`, `GetStaleSavedDevicePathsNormal`, `ShowStaleMonitorWarningNormal`, `ValidateSettingsForm` extension (non-blocking Normal check), `BtnSaveSettings_Click` extension (stale-preserving merge), theming call sites at Load + `OnThemeChanged`

## Decisions Made
- Flat sibling fields on `AppSettings` (not a nested `MonitorTarget` structure) — matches the existing `MonitorsToDisable`/`MonitorsToEnable` precedent exactly, per RESEARCH.md's own recommendation and every downstream reference in the research/patterns docs
- `ValidateSettingsForm`'s Normal-grid check computes `GetGridSelectionNormal()` purely for its stale-warning side effect and is otherwise unconditionally `true` — an all-empty Normal grid is a valid, saveable configuration (D-01), diverging deliberately from the Rig grid's blocking "select at least one" gate

## Deviations from Plan

**1. [Rule 2 - Missing Critical] Added `DgvMonitorsNormal_CurrentCellDirtyStateChanged` handler**
- **Found during:** Task 2
- **Issue:** The plan's task description didn't explicitly call out `DgvMonitors_CurrentCellDirtyStateChanged`'s Normal-grid analog, but without it the Normal grid's checkbox cells would not commit their value until losing focus — breaking the same-click sibling-uncheck mutual exclusivity (Pitfall 5) that the Rig grid relies on this exact mechanism to satisfy.
- **Fix:** Added `DgvMonitorsNormal_CurrentCellDirtyStateChanged`, wired to `dgvMonitorsNormal.CurrentCellDirtyStateChanged` in the constructor, mirroring `DgvMonitors_CurrentCellDirtyStateChanged` exactly.
- **Files modified:** `src/RigToggle.App/SettingsForm.cs`
- **Verification:** `dotnet build src/RigToggle.App/RigToggle.App.csproj -p:EnableWindowsTargeting=true` succeeds; code path mirrors the already-correct Rig-grid handler verbatim.
- **Committed in:** `8b2ea39` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 2 - missing critical functionality)
**Impact on plan:** Necessary for the Normal grid's checkbox mutual-exclusivity to actually work on a single click, matching the Rig grid's proven behavior. No scope creep — this is the same mechanism the plan already required for `OnMonitorNormalCellValueChanged`, just its required commit-trigger counterpart.

## Issues Encountered
- `dotnet` was not on `PATH` in this sandbox by default (only found at `/home/bpivk/.dotnet/dotnet`); once added to `PATH`, `dotnet build` still failed with `NETSDK1100` (Windows-targeting requires `EnableWindowsTargeting=true` on this non-Windows build host) until that MSBuild property was passed explicitly on the command line. Not a code issue — purely a sandbox build-invocation detail, resolved by adding `-p:EnableWindowsTargeting=true` to the verification command.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `AppSettings.NormalMonitorsToDisable`/`NormalMonitorsToEnable` are now available for Plan 16-03's `ToggleToNormalMode` rewrite to read directly.
- The Settings UI is fully independent of the mode-store rewrite (Plan 03) and the startup dialogs (Plan 04) — no blockers for either.
- Visual/overlap confirmation of the re-flowed dialog is explicitly deferred to Plan 05's rig checkpoint per RESEARCH.md Pitfall 4 (not compiler-catchable); flagged here for that plan's awareness.

---
*Phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign*
*Completed: 2026-08-05*

## Self-Check: PASSED

- FOUND: src/RigToggle.Core/Models/AppSettings.cs
- FOUND: src/RigToggle.App/SettingsForm.Designer.cs
- FOUND: src/RigToggle.App/SettingsForm.cs
- FOUND: .planning/phases/16-normal-mode-explicit-monitor-config-mode-store-redesign/16-02-SUMMARY.md
- FOUND commit: fc5ee27 (Task 1)
- FOUND commit: 8b2ea39 (Task 2)
- FOUND commit: 68f4303 (SUMMARY.md)
