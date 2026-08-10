---
phase: 20-custom-toggle-switch-control
plan: 01
subsystem: ui
tags: [winforms, owner-draw, custom-control, theming, gdi+]

# Dependency graph
requires:
  - phase: 19-monitor-tile-dashboard-monitorpanelform-retirement
    provides: MonitorTile.cs (structural template), ThemeApplier.ThemeMonitorTile (theming pattern), MainForm's dashboard layout btnToggle currently occupies
provides:
  - "ToggleSwitch : UserControl (Controls/ToggleSwitch.cs) — presentational-only owner-drawn three-state toggle switch"
  - "ToggleSwitchState enum (Off/On/Indeterminate)"
  - "ThemeApplier.ThemeToggleSwitch(ToggleSwitch, bool dark) — the switch's theming pipeline entry point"
affects: [20-02-swap-toggleswitch-into-mainform, 21-accent-color-reading]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Owner-drawn UserControl with fractional (ClientSize-derived) geometry constants, no bare pixel literals inside OnPaint"
    - "Track+thumb drawn as separate GraphicsPath/FillEllipse calls, stroke-then-fill, back-to-front (avoids Phase 13's DrawPath seam-artifact bug)"
    - "State communicated via BOTH fill-presence and thumb position, never color alone"
    - "ControlPaint.Light/ControlPaint.Dark computed at paint time for hover/press variants, not stored as theme properties"

key-files:
  created:
    - src/RigToggle.App/Controls/ToggleSwitch.cs
  modified:
    - src/RigToggle.App/ThemeApplier.cs

key-decisions:
  - "ToggleSwitch never references ToggleOrchestrator/IMonitorController/ISettingsStore/IThemeProvider — mirrors MonitorTile's dumb-presentational contract exactly, verified by a structural grep audit excluding comment lines"
  - "Hover/press variants of On/Indeterminate track fill are computed in OnPaint via ControlPaint.Light/Dark against the already-set OnColor/IndeterminateColor fields, not stored as separate ThemeApplier-set properties — no existing precedent in this codebase for a stored computed-hover-variant theme property"
  - "Indeterminate state renders as a filled (not hollow) track at the exact mid-track thumb position, making it a third unambiguous visual bucket distinguishable from Off (hollow) and On (right-aligned) by both fill-presence and position"

requirements-completed: [THEME-08]

# Metrics
duration: 17min
completed: 2026-08-10
---

# Phase 20 Plan 01: Custom Toggle-Switch Control Building Blocks Summary

**Owner-drawn ToggleSwitch UserControl (track+thumb pill, three states, keyboard-activatable) plus ThemeApplier.ThemeToggleSwitch, built standalone and not yet wired into MainForm**

## Performance

- **Duration:** ~17 min
- **Started:** 2026-08-10T12:27:49Z
- **Completed:** 2026-08-10T12:44:51Z
- **Tasks:** 2 completed
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments
- `ToggleSwitch : UserControl` (`src/RigToggle.App/Controls/ToggleSwitch.cs`) — a presentational-only owner-drawn control exposing `ToggleSwitchState State`/`SetState(ToggleSwitchState)`, `event EventHandler? ActionRequested`, and nine `[DesignerSerializationVisibility(Hidden)]` theme color properties
- Paints a static "Rig Mode" label plus a height-derived pill track (right-aligned) and a separately-drawn circular thumb; Off/On/Indeterminate differ by both track fill-presence and thumb position
- Keyboard-reachable: `TabStop = true`, pill-shaped accent focus ring drawn only when `Focused`, `ActionRequested` raised identically from click and from Space/Enter via `ProcessCmdKey`
- `ThemeApplier.ThemeToggleSwitch(ToggleSwitch, bool dark)` appended after `ThemeMonitorTile`, setting `BackColor` plus all nine theme colors from the locked 20-UI-SPEC.md literals, with the Indeterminate literal deliberately isolated (D-07)
- Solution builds with 0 errors; full test suite remains 81/81 passing (purely additive changes, no existing call site touched)

## Task Commits

Each task was committed atomically:

1. **Task 1: Controls/ToggleSwitch.cs — owner-drawn three-state track+thumb switch with keyboard activation** - `9368b23` (feat)
2. **Task 2: ThemeApplier.ThemeToggleSwitch — the switch's half of the two-call-site theming rule** - `d3eb7c2` (feat)

**Plan metadata:** (this commit, following SUMMARY.md creation)

## Files Created/Modified
- `src/RigToggle.App/Controls/ToggleSwitch.cs` - New owner-drawn `UserControl` with `ToggleSwitchState` enum, nine theme color properties, `SetState`, `ActionRequested`, keyboard activation, hover/press tracking, and fractional-geometry `OnPaint`
- `src/RigToggle.App/ThemeApplier.cs` - Appended `ThemeToggleSwitch(ToggleSwitch, bool dark)` after `ThemeMonitorTile`, writing all nine theme colors plus `BackColor`

## Decisions Made
- Followed the plan's exact structural template (`MonitorTile.cs`) and pattern map (`20-PATTERNS.md`) verbatim: same `SetStyle`/`TabStop`/`ProcessCmdKey`/`OnPaint` try-catch shape, same `BuildRoundedRect` sibling-copy convention, same `ThemeApplier` try/empty-catch/`Invalidate()` shape.
- Hover/press fill variants for On/Indeterminate computed via `ControlPaint.Light`/`ControlPaint.Dark` directly in `OnPaint`, per the plan's explicit instruction — no new theme property was added for these, consistent with there being no prior "stored computed variant" precedent in `ThemeApplier`.

## Deviations from Plan

None requiring code changes — both tasks were implemented exactly as specified. Two internal plan/acceptance-criteria inconsistencies were found and are documented below for transparency (not auto-fixed, since fixing either would mean violating an explicit, different instruction elsewhere in the same plan):

1. **Acceptance-criteria literal-count conflict (not a defect).** Task 1's `<action>` explicitly instructs `_indeterminateColor = Color.FromArgb(200, 200, 200)` as `ToggleSwitch`'s safe non-throwing default field value (matching `MonitorTile`'s convention of giving every color field a real default). Task 2's acceptance criteria then asserts `grep -rn 'Color.FromArgb(200, 200, 200)' src/ --include=*.cs | wc -l` outputs `1`, expecting the literal to appear only in `ThemeApplier.cs`. Because Task 1 was followed exactly, the literal legitimately appears twice (the field default in `ToggleSwitch.cs` and the theme assignment in `ThemeApplier.cs`), so this specific grep returns `2`, not `1`. This is a plan-internal inconsistency between two explicit instructions, not a functional bug — the field default is only ever visible before the first `ThemeApplier.ThemeToggleSwitch` call (theoretically, before any paint that could show it, given the control is always themed at startup per Plan 02's wiring), exactly the same defensive-default pattern `MonitorTile` already uses for its own color fields.
2. **Pre-existing acceptance-criteria false positive (not introduced by this plan).** Task 2's acceptance criteria asserts `grep -cE '\.Controls\b' src/RigToggle.App/ThemeApplier.cs` outputs `0` ("`ThemeApplier` did not become a Controls-tree walk"). The file already contained `using RigToggle.App.Controls;` (the namespace import for `MonitorTile`/`ToggleSwitch`) before this plan touched the file — `git diff` confirms this line is unchanged. The regex incidentally matches the `.Controls` substring inside the namespace name, producing a count of `1` instead of the expected `0`, even though no actual `Controls`-tree walk exists anywhere in the file (manually verified: no `.Controls[` or `foreach (Control ...)` construct present).

All other automated and structural acceptance criteria for both tasks pass exactly as specified (build 0 errors, tests 81/81, all grep/awk structural checks matching expected counts) — see verification output below.

## Issues Encountered

One self-correction during Task 1: the initial `OnPaint` implementation included a code comment that quoted the literal `"Rig Mode"` in explanatory text, which caused `grep -c '"Rig Mode"' ToggleSwitch.cs` to return `2` instead of the required `1` (the comment's quoted mention plus the actual `TextRenderer.DrawText` call argument). Reworded the comment to describe the label without re-quoting the literal string; re-verified the grep count returns `1` and the build/tests still pass.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `ToggleSwitch` and `ThemeApplier.ThemeToggleSwitch` are fully built, compiling, and standalone — ready for Plan 02 to swap them into `MainForm` in place of `btnToggle`/`lblMode`.
- `MainForm.cs` and `MainForm.Designer.cs` remain completely untouched by this plan, exactly as scoped — no wiring, no `Controls.Add`, no event subscription yet.
- Nothing blocks Plan 02: the public surface (`ToggleSwitchState`, `State`, `SetState`, `ActionRequested`, nine theme color properties) matches the plan's frontmatter contract exactly and is ready for Plan 02 to consume verbatim.

## Verification Output

```
dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
  => Build succeeded. 0 Warning(s) [after fix], 0 Error(s)

dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true
  => Passed! - Failed: 0, Passed: 81, Skipped: 0, Total: 81

git status --porcelain src/
  => (clean — all changes committed)
```

---
*Phase: 20-custom-toggle-switch-control*
*Completed: 2026-08-10*

## Self-Check: PASSED

- FOUND: `src/RigToggle.App/Controls/ToggleSwitch.cs`
- FOUND: commit `9368b23` (Task 1)
- FOUND: commit `d3eb7c2` (Task 2)
