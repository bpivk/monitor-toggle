---
phase: 22-settingsform-layout-pass
plan: 01
subsystem: ui
tags: [winforms, tablelayoutpanel, settingsform, designer, dotnet10]

# Dependency graph
requires:
  - phase: 12-theme-infrastructure-live-theme-following
    provides: THEME-05 flat-bordered-Panel-as-GroupBox convention, container-agnostic theming pipeline
provides:
  - tlpRoot (3-row root TableLayoutPanel) and tlpModeColumns (50/50 Percent-split mode-column container)
  - tlpNormalColumn/tlpAudioNormal and tlpRigColumn/tlpAudioRig fully table-driven mode columns
  - pnlAudioDevices dissolved; each mode's audio picker relocated into its own mode column (D-01)
affects: [22-02 (shared-section + button-row + form-sizing plan, depends on this scaffold)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "First use of System.Windows.Forms.TableLayoutPanel anywhere in this codebase (Percent/AutoSize row/column styles only, never Absolute)"
    - "Section-container Panel (BorderStyle.FixedSingle, THEME-05) now hosts a single nested TableLayoutPanel child instead of multiple absolutely-positioned children"
    - "20px right-Margin convention reserving ErrorProvider icon clearance on every error-target control (dgvMonitors/dgvMonitorsNormal/cboAudioNormal/cboAudioRig)"

key-files:
  created: []
  modified:
    - src/RigToggle.App/SettingsForm.Designer.cs

key-decisions:
  - "Normal mode placed in tlpModeColumns column 0 (left), Rig mode in column 1 (right) -- a deliberate swap from the pre-migration x=12/x=420 order, per the user's own framing in 22-CONTEXT.md"
  - "tlpRoot Row 0 (mode columns) uses SizeType.Percent 100F rather than AutoSize so future window-resize growth (Plan 02's Sizable border) has somewhere to land, per 22-RESEARCH.md's rig-check 7(d) rationale"
  - "pnlAudioDevices dissolved entirely (not deprecated-in-place) -- each mode panel's own caption already labels the section that now also holds that mode's audio picker"
  - "Task 1 deliberately left pnlMonitor absolutely positioned and left the old flat this.Controls.Add(pnlMonitor)/(pnlMonitorNormal) entries in place per hard constraint 7's accepted intermediate state; Task 2 completed the migration and removed those flat entries"

patterns-established:
  - "Mode column shape: 6-row TableLayoutPanel (caption/explain/grid/warning/audio-row/audio-warning), grid row is the only Percent 100F row so vertical growth lands on the grid"
  - "Audio-picker row shape: 2-column TableLayoutPanel (AutoSize caption, Percent 100F combo)"

requirements-completed: [SETTINGS-01, SETTINGS-02]

# Metrics
duration: 15min
completed: 2026-08-12
---

# Phase 22 Plan 01: TableLayoutPanel Scaffold & Mode-Column Migration Summary

**Migrated SettingsForm's two monitor sections from hardcoded Panel+Location/Size positioning into a 50/50 Percent-split TableLayoutPanel scaffold (tlpRoot/tlpModeColumns), with each mode's audio picker moved out of the shared pnlAudioDevices panel into its own mode column (D-01).**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-08-12T06:48:00Z (approx, session start)
- **Completed:** 2026-08-12T07:03:58Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments

- Built `tlpRoot` (3-row root container: Percent 100F mode-column row, two reserved AutoSize rows for Plan 02) and `tlpModeColumns` (2-column 50/50 Percent split, Normal=col 0/Rig=col 1)
- Fully migrated both `pnlMonitorNormal` and `pnlMonitor` off fixed `Location`/`Size` onto `Dock=Fill` inside `tlpModeColumns`, each driven by its own 6-row `tlpNormalColumn`/`tlpRigColumn` table
- Dissolved the shared `pnlAudioDevices` panel entirely (D-01): `cboAudioNormal`/`lblAudioNormalCaption`/`lblAudioNormalWarning` now live in `tlpAudioNormal` inside the Normal column; `cboAudioRig`/`lblAudioRigCaption`/`lblAudioRigWarning` now live in `tlpAudioRig` inside the Rig column; `lblAudioDevicesCaption` has no replacement (each mode panel's own caption already labels the section)
- Added ErrorProvider icon clearance (20px right `Margin`) and a 120px `MinimumSize` height floor on both grids, per the plan's threat-mitigation requirements (T-22-02, T-22-04)
- Preserved both grids' `AutoSizeColumnMode.Fill`/`Width=66` column configuration verbatim (untouched by this phase, per hard constraint 3)
- Build and test suite held at their measured baselines throughout (`0 Error(s)`, `4 Warning(s)`; `82/82` tests passing)

## Task Commits

Each task was committed atomically:

1. **Task 1: Root scaffold, mode-column container, and the migrated Normal column** - `8cf0d97` (feat)
2. **Task 2: Mirror the Rig column and dissolve pnlAudioDevices** - `a0cdae6` (feat)

_Both tasks touched only `src/RigToggle.App/SettingsForm.Designer.cs`; `SettingsForm.cs` and `ThemeApplier.cs` remain byte-identical to the phase baseline commit (`0c1234f`), confirmed via `git diff --stat` after each task._

## Files Created/Modified

- `src/RigToggle.App/SettingsForm.Designer.cs` - Added `tlpRoot`/`tlpModeColumns`/`tlpNormalColumn`/`tlpAudioNormal`/`tlpRigColumn`/`tlpAudioRig`; migrated `pnlMonitorNormal`/`pnlMonitor` to `Dock=Fill` inside `tlpModeColumns`; deleted `pnlAudioDevices`/`lblAudioDevicesCaption`; relocated all seven audio-picker children into their mode columns

## Row/Column Style Table (final state)

| Container | ColumnStyles | RowStyles |
|-----------|-------------|-----------|
| `tlpRoot` | `[Percent 100F]` | `[Percent 100F, AutoSize, AutoSize]` |
| `tlpModeColumns` | `[Percent 50F, Percent 50F]` | `[Percent 100F]` |
| `tlpNormalColumn` / `tlpRigColumn` | `[Percent 100F]` | `[AutoSize, AutoSize, Percent 100F, AutoSize, AutoSize, AutoSize]` |
| `tlpAudioNormal` / `tlpAudioRig` | `[AutoSize, Percent 100F]` | `[AutoSize]` |

## Location/Size Lines Removed

All `Location`/`Size` assignments dropped from: `pnlMonitorNormal`, `pnlMonitor`, `lblMonitorNormalCaption`, `lblMonitorCaption`, `lblMonitorNormalExplain`, `lblMonitorExplain`, `dgvMonitorsNormal`, `dgvMonitors`, `lblMonitorNormalWarning`, `lblMonitorWarning`, `lblAudioNormalCaption`, `lblAudioRigCaption`, `cboAudioNormal`, `cboAudioRig`, `lblAudioNormalWarning`, `lblAudioRigWarning` (16 controls total, verified via grep: zero `.Location`/`.Size` assignments remain for any of them). `pnlAudioDevices` and `lblAudioDevicesCaption` themselves were deleted outright rather than having their positioning changed.

## Verbatim Build/Test Output

**Build** (`dotnet build RigToggle.sln -p:EnableWindowsTargeting=true`, final state after both tasks):
```
Build succeeded.
    4 Warning(s)
    0 Error(s)
```
(All 4 warnings are the pre-existing `xUnit1031` warnings in `ToggleOrchestratorTests.cs`, unrelated to this plan — matches the phase baseline exactly.)

**Test** (`dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true`, final state after both tasks):
```
Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82, Duration: 76 ms
```

## Decisions Made

- Normal mode placed in `tlpModeColumns` column 0 (left), Rig mode in column 1 (right) — a deliberate swap from today's `pnlMonitor`-at-x=12/`pnlMonitorNormal`-at-x=420 order, per 22-CONTEXT.md's user framing ("One side is for normal mode... second for rig mode")
- `tlpRoot`'s mode-column row uses `Percent 100F` rather than `AutoSize` so that Plan 02's `Sizable` border growth has somewhere to land (documented inline as a code comment, per plan instruction)
- Task 1 deliberately left `pnlMonitor` absolutely positioned and the flat `this.Controls.Add(pnlMonitor)`/`(pnlMonitorNormal)` entries in place (per hard constraint 7's accepted intermediate state) — Task 2 completed the reparenting and removed those now-redundant flat entries, along with `this.Controls.Add(pnlAudioDevices)`

## Deviations from Plan

None — plan executed exactly as written. Every acceptance-criteria grep check (row/column style counts, `Location`/`Size` absence, `MinimumSize` presence, `pnlAudioDevices`/`lblAudioDevicesCaption` absence, grid column configuration preservation, `git diff --stat` against `SettingsForm.cs`/`ThemeApplier.cs`) passed on the first attempt for both tasks with no fix-up needed.

## Issues Encountered

None. No auth gates, no blocking issues, no architectural questions arose during execution.

## User Setup Required

None — no external service configuration required. No GUI verification was possible in this build environment (headless Linux container); all layout/rendering claims remain deferred to Plan 03's rig checkpoint, per the plan's own `<verification>` section.

## Next Phase Readiness

- `tlpRoot`/`tlpModeColumns`/`tlpNormalColumn`/`tlpAudioNormal`/`tlpRigColumn`/`tlpAudioRig` are all in place with a green build and passing test suite — Plan 02 can now build the shared section (`pnlSharedSection`/`flpShared`), the button row, and the form-level `AutoSize`/`FormBorderStyle=Sizable` sizing changes on top of this scaffold
- No blockers. The intermediate visual state (mode columns correctly nested, but shared-section controls and button row still absolutely positioned) is expected and does not block Plan 02
- Rig-verification of the full layout (DPI scaling, tab order, live theme-flip, overlap/crowding) remains deferred to Plan 03 as designed

---
*Phase: 22-settingsform-layout-pass*
*Completed: 2026-08-12*
