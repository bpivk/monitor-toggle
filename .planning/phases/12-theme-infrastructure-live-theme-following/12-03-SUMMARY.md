---
phase: 12-theme-infrastructure-live-theme-following
plan: 03
subsystem: ui
tags: [winforms, datagridview, theming, groupbox-panel, live-theme-follow]

# Dependency graph
requires: [12-01, 12-02]
provides:
  - ThemeApplier (RigToggle.App) — internal static helper with ThemeMonitorGrid(DataGridView, bool) and three ApplyHotkey* per-state helpers (Configured/Unconfigured/Recording), all idempotent and non-throwing
  - SettingsForm live theme-follow — ThemeChanged subscribe (ctor) / unsubscribe (FormClosed), marshaled OnThemeChanged re-applying SetColorMode + DWM chrome + grid theming + txtHotkey re-render
  - dgvMonitors dark/light cell + header theming (dotnet/winforms#11893 gap closed)
  - txtHotkey fully theme-aware across all three states, zero SystemColors.* remaining
  - SettingsForm.Designer.cs GroupBox→Panel refactor (pnlMonitor/pnlAudioDevices/pnlAppPath) with FixedSingle borders + caption Labels, zero layout drift
  - FlatStyle.System on btnBrowse/btnSaveSettings/btnDiscardChanges
affects: [12-04, ui, theme-application]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ThemeApplier: first shared static control-recolor helper class in RigToggle.App — targeted per-control overrides only (DataGridView, txtHotkey), never a recursive Controls-tree walk, so base controls stay owned by Application.SetColorMode"
    - "GroupBox→Panel+Label replacement: Panel.BorderStyle=FixedSingle for the flat border GroupBox has no equivalent of, caption rendered by a separate Label positioned at the GroupBox's native ~9px caption inset so every re-parented child keeps its existing Location unchanged"
    - "SettingsForm joins MainForm/MonitorConfirmDialog's marshal-then-try/catch OnThemeChanged pattern, with FormClosed-based unsubscribe (transient dialog vs. app-lifetime WindowsThemeProvider, matching MonitorConfirmDialog's precedent, not MainForm's Dispose-time unsubscribe)"

key-files:
  created:
    - src/RigToggle.App/ThemeApplier.cs
  modified:
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/SettingsForm.Designer.cs

key-decisions:
  - "ThemeApplier's txtHotkey helpers take the control directly (ApplyHotkeyIdleConfigured/Unconfigured/Recording(TextBox, bool)) and mutate it in-place, mirroring ThemeMonitorGrid's own (control, bool) shape, rather than returning color tuples for the caller to apply — keeps every call site in SettingsForm.cs a single one-line call"
  - "DwmTitleBar.ApplyRoundedCornersAndMica(Handle) is called unwrapped (no try/catch) in SettingsForm's constructor, matching MonitorConfirmDialog's exact precedent — DwmSetWindowAttribute is declared to return an HRESULT and never throws (D-07 from 12-01), so the extra try/catch MainForm uses around its own ApplyDwmChrome wrapper is unnecessary here"
  - "OnThemeChanged re-renders txtHotkey by branching on _recordingHotkey: Recording gets its own ThemeApplier.ApplyHotkeyRecording re-apply (it isn't driven by RenderHotkeyIdleDisplay), every other state re-derives correctly by just calling RenderHotkeyIdleDisplay() again"

requirements-completed: []

# Metrics
duration: 40min
completed: 2026-08-03
---

# Phase 12 Plan 03: Per-Control Theming — DataGridView, txtHotkey, GroupBox→Panel Summary

**New `ThemeApplier` helper closes the confirmed `dgvMonitors` DataGridView and `txtHotkey` hand-rolled-`SystemColors` theming gaps, SettingsForm gains its own live theme-follow (subscribe/unsubscribe + marshaled `OnThemeChanged`) and Windows-11 DWM chrome, and all three `GroupBox`es become flat bordered `Panel`s with captions and zero layout drift — full rig-visual verification deferred to plan 12-04 per this phase's plan.**

## Performance

- **Duration:** 40 min
- **Started:** 2026-08-03T03:55:00Z (approximate)
- **Completed:** 2026-08-03T04:35:00Z
- **Tasks:** 3
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments

- `ThemeApplier` (new, `RigToggle.App`) provides `ThemeMonitorGrid(DataGridView, bool)` and three `ApplyHotkeyIdleConfigured`/`ApplyHotkeyIdleUnconfigured`/`ApplyHotkeyRecording(TextBox, bool)` helpers, all idempotent, non-throwing, and grep-verified against every exact ARGB literal from `12-UI-SPEC.md`'s Color section
- `dgvMonitors` gets full dark/light theming (background, grid lines, cell colors, selection highlight, column headers) with `EnableHeadersVisualStyles = false` correctly ordered before the header-style assignments — closes the confirmed `dotnet/winforms#11893` gap
- `txtHotkey`'s three hand-rolled `SystemColors.*` state-machine sites (`RenderHotkeyIdleDisplay`'s two branches, `TxtHotkey_MouseDown`, `TxtHotkey_KeyDown`'s capture-complete branch) are fully replaced with `ThemeApplier` calls sourced from live `IThemeProvider.CurrentTheme` — grep-verified zero `SystemColors.*` remain on `txtHotkey`
- `SettingsForm` subscribes `ThemeChanged` in its constructor and unsubscribes on `FormClosed` (transient-dialog lifecycle, mirroring `MonitorConfirmDialog`'s 12-02 precedent, not `MainForm`'s app-lifetime `Dispose`-time unsubscribe), gets Windows-11 Mica/rounded corners applied post-`InitializeComponent`, and a marshaled `OnThemeChanged` that re-applies `SetColorMode` + DWM chrome + grid theming + the current `txtHotkey` state + `Refresh()`
- All three `GroupBox`es (`grpMonitor`/`grpAudioDevices`/`grpAppPath`) are replaced with `Panel`s (`pnlMonitor`/`pnlAudioDevices`/`pnlAppPath`, `BorderStyle.FixedSingle`) carrying a caption `Label` at the native ~9px inset — every child control keeps its exact original `Location`, and each Panel's `Location`/`Size` matches its predecessor GroupBox exactly (grep-verified zero bounding-box drift)
- `AppPath_DragEnter`/`AppPath_DragDrop`/`AllowDrop` moved from `grpAppPath` to `pnlAppPath` verbatim — not dropped (grep-verified, no orphaned handler)
- `btnBrowse`/`btnSaveSettings`/`btnDiscardChanges` get `FlatStyle.System`
- Full solution builds (`dotnet build RigToggle.sln`) and the full 70-test suite passes with no regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: ThemeApplier — DataGridView + txtHotkey palette helpers** - `22a484a` (feat)
2. **Task 2: SettingsForm code-behind — live-follow, DWM chrome, txtHotkey theme fix, grid theming** - `4fb1595` (feat)
3. **Task 3: SettingsForm.Designer.cs — GroupBox→Panel refactor + flat buttons** - `16ad48a` (feat)

**Plan metadata:** committed alongside this SUMMARY (docs)

## Files Created/Modified

- `src/RigToggle.App/ThemeApplier.cs` (new) - `internal static class ThemeApplier` with `ThemeMonitorGrid` + three `ApplyHotkey*` per-state helpers, all idempotent and non-throwing
- `src/RigToggle.App/SettingsForm.cs` - `using RigToggle.Windows;` added; ctor subscribes `ThemeChanged`/unsubscribes on `FormClosed`; `DwmTitleBar.ApplyRoundedCornersAndMica(Handle)` called post-`InitializeComponent`; new `IsDarkTheme` property and marshaled `OnThemeChanged`; `ThemeApplier.ThemeMonitorGrid` called from `SettingsForm_Load`; all three `txtHotkey` `SystemColors.*` sites replaced with `ThemeApplier` calls
- `src/RigToggle.App/SettingsForm.Designer.cs` - `grpMonitor`/`grpAudioDevices`/`grpAppPath` replaced with `pnlMonitor`/`pnlAudioDevices`/`pnlAppPath` (`Panel`, `BorderStyle.FixedSingle`) + `lblMonitorCaption`/`lblAudioDevicesCaption`/`lblAppPathCaption`; drag-drop wiring moved to `pnlAppPath`; `FlatStyle.System` on the three buttons; field declarations and `Controls.Add`/`ResumeLayout` calls updated to match

## Decisions Made

None beyond what the plan already specified, plus the three `key-decisions` documented above (ThemeApplier method shape, unwrapped `DwmTitleBar` call matching `MonitorConfirmDialog` precedent, `OnThemeChanged`'s Recording-vs-idle txtHotkey re-render branch) — all directly derivable from 12-RESEARCH.md's verified code examples and the established 12-02 patterns.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking, self-caught] Reworded a `SettingsForm.Designer.cs` comment to avoid a literal `FlatStyle.Flat` grep match**
- **Found during:** Task 3, acceptance-criteria verification
- **Issue:** Task 3's acceptance criteria requires no `FlatStyle.Flat` to appear in `SettingsForm.Designer.cs`. My first draft of the rationale comment above `btnBrowse.FlatStyle = FlatStyle.System` explained the choice by naming the avoided alternative literally (`` FlatStyle.Flat``), which itself matched the negative grep gate.
- **Fix:** Reworded the comment to describe the avoided rendering mode generically ("WinForms' other flat-button rendering mode") without repeating the literal `FlatStyle.Flat` expression, preserving the rationale without tripping the gate.
- **Files modified:** `src/RigToggle.App/SettingsForm.Designer.cs`
- **Verification:** `grep -c "FlatStyle.Flat" ...` returns 0; `grep -c "FlatStyle.System" ...` still returns 4 (3 real assignments + 1 comment mention, `>= 3` requirement satisfied); `dotnet build` still succeeds.
- **Committed in:** `16ad48a` (Task 3 commit — caught before commit, no separate fix commit needed)

---

**Total deviations:** 1 auto-fixed (1 self-caught grep-gate wording fix, no functional change)
**Impact on plan:** Cosmetic-only; no scope creep, no behavior change.

## Issues Encountered

None. All three tasks' acceptance criteria (grep gates + `dotnet build`/`dotnet test`) passed on first or second attempt (the one grep-gate wording fix above).

## User Setup Required

None - no external service configuration required.

## Requirements Completion

This plan's frontmatter lists `[THEME-02, THEME-04, THEME-05, THEME-06]` as the requirements it addresses, but per this phase's own `<verification>` section ("Visual legibility of the dark grid, the three txtHotkey states, the Panel borders, and live-flip-while-Settings-open are confirmed on the rig in plan 12-04"), these requirements are only fully validated after 12-04's rig checkpoint — matching this project's established precedent (see git history `c678bb6`, "revert premature REQUIREMENTS.md completion flags pending rig checkpoint," from Phase 8). `REQUIREMENTS.md` is intentionally left untouched by this plan; it is updated at phase-completion time, not per-plan, consistent with every prior phase in this project.

## Next Phase Readiness

- Plan 12-04's rig checkpoint can now verify the full launch-time + live-flip visual behavior for SettingsForm (grid, txtHotkey's three states, Panel borders, DWM chrome) alongside 12-02's already-verified MainForm/MonitorConfirmDialog behavior
- Full solution builds and all 70 tests pass with no regressions
- No consumers outside `SettingsForm.cs`/`SettingsForm.Designer.cs` were touched — `ThemeApplier` is a pure addition with no ripple effect on other forms

## Self-Check: PASSED

- FOUND: src/RigToggle.App/ThemeApplier.cs
- FOUND: src/RigToggle.App/SettingsForm.cs (modified)
- FOUND: src/RigToggle.App/SettingsForm.Designer.cs (modified)
- FOUND commit 22a484a
- FOUND commit 4fb1595
- FOUND commit 16ad48a

---
*Phase: 12-theme-infrastructure-live-theme-following*
*Completed: 2026-08-03*
