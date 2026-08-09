---
phase: 19-monitor-tile-dashboard-monitorpanelform-retirement
plan: 02
subsystem: ui
tags: [winforms, monitor-tile, dashboard, theming, hotplug, gdi+]

requires:
  - phase: 19-01
    provides: MonitorIconGeometry (icon/badge/gear GDI+ geometry), MonitorTile (presentational UserControl)
provides:
  - ThemeApplier.ThemeMonitorTile — per-tile palette entry point wired into the two-call-site theming rule
  - MainForm dashboard: tileStrip FlowLayoutPanel, lblNoMonitors empty state, btnIdentify, icon-only btnSettings gear
  - MainForm.RefreshMonitorTiles/CreateTile/LayoutDashboard/ApplyDashboardTheming/OnDisplaySettingsChanged
  - Canonical DevicePath-ordinal-sorted monitor enumeration shared by tile numbering
affects: [19-03 (tile click mutation + Identify), 19-04 (MonitorPanelForm retirement), 19-05 (rig verification)]

tech-stack:
  added: []
  patterns:
    - "Font-derived Scaled() helper for programmatic layout, since AutoScaleMode.Font has zero effect on positions/sizes assigned in code"
    - "Manual arithmetic ClientSize computation (not Form.AutoSize) recomputed on every population/hotplug, for --tray-hidden-start timing safety"
    - "Tile control reconciliation (grow/shrink _tiles list) instead of clear-and-rebuild, to avoid handle churn across repeated hotplug events"
    - "Single ApplyDashboardTheming() helper invoked from both OnThemeChanged and InitializeTrayState() to prevent the two-call-site theming drift bug (already shipped twice in Phase 12)"

key-files:
  created: []
  modified:
    - src/RigToggle.App/ThemeApplier.cs
    - src/RigToggle.App/MainForm.Designer.cs
    - src/RigToggle.App/MainForm.cs

decisions:
  - "RefreshMonitorTiles()'s empty-case branch calls only LayoutDashboard(), not ApplyDashboardTheming() — matches the plan's literal STEP D structure (theming is applied once at the shared end-of-function point in the non-empty path); an initial draft over-called ApplyDashboardTheming() in both branches and was corrected to match the acceptance-criteria's exact-3-call-sites count."

requirements-completed: [TILE-01, TILE-06, MAIN-01, MAIN-02]

duration: 45min
completed: 2026-08-09
---

# Phase 19 Plan 02: Monitor-Tile Dashboard & MonitorPanelForm Retirement Summary

**MainForm now renders a live, hotplug-reactive monitor-tile dashboard — one `MonitorTile` per detected monitor, numbered from a single canonical `DevicePath`-sorted list, laid out and auto-sized entirely arithmetically via a font-derived `Scaled()` helper, with the old full-width Monitors button replaced by a de-emphasized icon-only Settings gear.**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-08-09T20:49:00Z (approx, after worktree fast-forward sync)
- **Completed:** 2026-08-09T20:58:27Z
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments

- `ThemeApplier.ThemeMonitorTile` gives every live tile a themed palette (chrome, accent, focus ring, OFF-state outline, hover tint) from pre-existing literals only, following the file's established try/catch-swallow shape.
- `MainForm.Designer.cs` now declares the tile strip, empty-state label, Identify button, a components-owned tooltip, and an icon-only 32×32 Settings gear; `btnMonitors` is fully gone from the Designer while `trayMonitorsMenuItem` stays untouched for Plan 19-04.
- `MainForm.cs` populates one tile per monitor from `GetAllMonitors()` sorted once by `DevicePath` (ordinal), reconciles tile control instances against monitor count instead of rebuilding, and computes the entire dashboard layout (tile strip, Identify, toggle, gear, and the form's own `ClientSize`) arithmetically through a font-derived `Scaled()` helper — no WinForms layout-pass dependency, matching the `--tray` hidden-start safety requirement.
- `SystemEvents.DisplaySettingsChanged` is subscribed once in the constructor, app-lifetime, with a real-process-exit-only backstop unsubscribe in `Dispose(bool)` — never gated on visibility.
- Both `OnThemeChanged` and `InitializeTrayState()` reach every new control through a single shared `ApplyDashboardTheming()` helper, closing off the exact two-call-site drift bug this codebase already shipped twice in Phase 12.
- Tile clicks and the Identify button are wired as inert stubs (`OnTileAction`, `BtnIdentify_Click`) — Plan 19-03 implements the actual mutation/Identify behavior. No `ActivateMonitors`/`DeactivateMonitors`/`BeginExclusiveMonitorAccess`/`MonitorConfirmDialog`/`MonitorIdentifyOverlay` calls were added by this plan's diff.

## Task Commits

1. **Task 1: ThemeApplier.ThemeMonitorTile — the tile's palette entry point** - `46d5ea3` (feat)
2. **Task 2: MainForm.Designer.cs — dashboard controls, icon-only Settings gear, Monitors button removal** - `8e38838` (feat)
3. **Task 3: MainForm.cs — canonical ordering, tile population, deterministic layout, hotplug, dashboard theming** - `24ed7f8` (feat)

_Note: Task 2 deliberately leaves the solution non-compiling — `BtnIdentify_Click`, `BtnSettings_Paint`, and `OnDisplaySettingsChanged` are added by Task 3, matching the plan's documented Plan 17-02 Task 1 precedent. Verified via an intermediate build showing exactly the 5 expected `CS0103`/`CS1061` errors before Task 3 landed._

## Files Created/Modified

- `src/RigToggle.App/ThemeApplier.cs` - Adds `ThemeMonitorTile(MonitorTile tile, bool dark)`, reusing only pre-existing palette literals
- `src/RigToggle.App/MainForm.Designer.cs` - Adds `tileStrip`/`lblNoMonitors`/`btnIdentify`/`tileToolTip`; removes `btnMonitors`; reconfigures `btnSettings` to an icon-only gear; reorders `Controls.Add` to the D-09 reading order; adds the `Dispose(bool)` hotplug-unsubscribe backstop
- `src/RigToggle.App/MainForm.cs` - Adds tile fields/layout constants, `Scaled()`, `RefreshMonitorTiles()`, `CreateTile()`, `OnTileAction()` stub, `BtnIdentify_Click()` stub, `LayoutDashboard()`, `ApplyDashboardTheming()`, `OnDisplaySettingsChanged()`, `BtnSettings_Paint()`; removes `BtnMonitors_Click()`; wires the constructor's app-lifetime hotplug subscription; updates both theming call sites and the class doc comment

## Final Signatures (per plan's `<output>` requirement)

- `private void RefreshMonitorTiles()`
- `private void LayoutDashboard()`
- `private void ApplyDashboardTheming()`
- `private void OnTileAction(MonitorTile tile)` — empty body, `// Implemented in Plan 19-03 (TILE-02/TILE-03).`
- `private void BtnIdentify_Click(object? sender, EventArgs e)` — empty body, `// Implemented in Plan 19-03 (TILE-04).`

## Computed ClientSize by Monitor Count

Derived from `LayoutDashboard()`'s arithmetic at 100% scale (`Scaled()` is a no-op when `Font.Height == 15`), with `MarginPx=16`, `TileWidthPx=72`, `TileHeightPx=88`, `TileMarginPx=6` (cell = 84×100px), `MaxTilesPerRow=4`, `ContentWidthFloorPx=288`, `ModeLabelHeightPx=20`, `GapSmPx=8`, `IdentifyHeightPx=32`, `GapMdPx=16`, `TogglePx=40`, `GapLgPx=24`, `GearSizePx=32`:

- **1 monitor:** 1 row × 1 tile, `stripW=84` < floor → `contentWidth=288`. `ClientSize = (288+32, ...) = (320, 288)`.
- **2 monitors:** 1 row × 2 tiles, `stripW=168` < floor → `contentWidth=288`. `ClientSize = (320, 288)`.
- **4 monitors:** 1 row × 4 tiles, `stripW=336` > floor → `contentWidth=336`. `ClientSize = (368, 288)`.
- **5 monitors:** 2 rows (4 + 1), `stripW=336`, `stripH=200` → `contentWidth=336`. `ClientSize = (368, 388)`.

(All heights computed via `stripTop=16+20+8=44`; `contentBottom=stripTop+stripH`; `btnIdentify.Bottom=contentBottom+16+32`; `btnToggle.Bottom=btnIdentify.Bottom+8+40`; `btnSettings.Bottom=btnToggle.Bottom+24+32`; `ClientSize.Height=btnSettings.Bottom+16`. Verified arithmetically, not rendered — this Linux build environment cannot open a WinForms window; visual/proportion confirmation is deferred to Plan 19-05's rig checkpoint per the plan's own verification note.)

## Build/Test Output

- `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` — **0 Warning(s), 0 Error(s)** (final state; Task 2's intermediate state showed the plan-documented 5 expected errors before Task 3 landed)
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true` — **Passed: 81, Failed: 0, Total: 81** (matches Plan 19-01's baseline, no regression)
- `grep -c "MonitorPanelForm" src/RigToggle.App/Program.cs` — `2` (unchanged; this plan does not touch composition-root wiring)

## Decisions Made

- `RefreshMonitorTiles()`'s empty-case early-return calls only `LayoutDashboard()`, not `ApplyDashboardTheming()` — matches the plan's literal STEP D wording ("call LayoutDashboard(), and return" for the empty case; "Call ApplyDashboardTheming(); then LayoutDashboard(); at the end" for the shared end-of-function path). An initial draft called `ApplyDashboardTheming()` in both branches (4 total call sites instead of the acceptance criteria's required 3); caught by the plan's own grep check and corrected before commit.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Worktree was 21 commits behind `master`, missing Plan 19-01's code and Phase 19's planning docs entirely**
- **Found during:** Initial file-read step, before Task 1 — `19-02-PLAN.md` and the entire `.planning/phases/19-*` directory did not exist in this worktree's checkout.
- **Issue:** This worktree branch (`worktree-agent-a5c7fc35688b723ab`) was created before Phase 19 was planned/executed on `master`. It was a strict, non-diverging ancestor of `master` (verified via `git merge-base --is-ancestor` and an empty `master..HEAD` diff), 19 commits behind.
- **Fix:** Fast-forward merged `master` into the worktree branch (`git merge --ff-only master`) to pick up the v2.1 milestone scoping, Phase 19 planning artifacts, and Plan 19-01's `MonitorTile`/`MonitorIconGeometry` code this plan depends on. No worktree-isolation rules were violated — the merge is a plain fast-forward on the agent's own branch, not a cross-worktree write.
- **Files modified:** None directly (bulk fast-forward of pre-existing upstream commits, not a new code change).
- **Commit:** N/A (fast-forward, not a new commit — `HEAD` moved from `ec29345` to `b652c68`)

**2. [Rule 1 - Bug] Corrected `RefreshMonitorTiles()`'s empty-case theming call count**
- **Found during:** Task 3's acceptance-criteria verification (grep count for `ApplyDashboardTheming();` returned 4, not the required 3)
- **Issue:** The empty-monitor-count branch of `RefreshMonitorTiles()` called both `ApplyDashboardTheming()` and `LayoutDashboard()`, but the plan's STEP D text only specifies `LayoutDashboard()` for that branch.
- **Fix:** Removed the extraneous `ApplyDashboardTheming()` call from the empty-case branch, leaving `LayoutDashboard()` as the only call there.
- **Files modified:** `src/RigToggle.App/MainForm.cs`
- **Verification:** Rebuild succeeded (0 errors), all 81 tests still passed, and the grep count matched the required `3`.
- **Committed in:** `24ed7f8` (part of the Task 3 commit — caught before the commit was made)

---

**Total deviations:** 2 (1 blocking worktree-sync fix, 1 self-caught bug fix before commit)
**Impact on plan:** Neither deviation altered scope or behavior beyond what the plan specified. No scope creep.

## Issues Encountered

- Two of the plan's acceptance-criteria greps (`grep -c 'GetActiveMonitors'` expecting `0`, and the mutation-keyword audit expecting `0` matches) return `1` each against the final `src/RigToggle.App/MainForm.cs`, but both matches are pre-existing text that predates this plan and is untouched by its diff: a `GetActiveMonitors()` mention inside a pre-existing code comment in `BtnToggle_Click` (Phase 5/6 rig-toggle confirmation flow), and a `MonitorConfirmDialog`/`BeginExclusiveMonitorAccess` usage/doc-comment in that same pre-existing `BtnToggle_Click`/`OpenMonitorPanel()` code (Phase 5/6/17). Verified via `git show HEAD~3:...` (the pre-Task-1 commit) that both lines were already present before this plan's first commit. The plan's hard constraint 5 ("must not appear in any code this plan writes") is satisfied — this plan's diff introduces zero new occurrences of either forbidden pattern — but the literal whole-file grep as written in the acceptance criteria does not exempt pre-existing legitimate usage. No code change was made in response to this; documenting it here as a clarification rather than a defect.

## Next Phase Readiness

- Plan 19-03 can proceed: `OnTileAction`/`BtnIdentify_Click` stubs and the canonical `_lastKnownMonitors` field are in place for it to wire the ported lease/confirm/mutate path and the Identify overlay against.
- Plan 19-04 (MonitorPanelForm retirement) can proceed once 19-03 lands: `MonitorPanelForm.cs`, `trayMonitorsMenuItem`, `TrayMonitorsMenuItem_Click`, `OpenMonitorPanel()`, `_monitorPanelForm`, and `_monitorPanelFormFactory` are all still present and untouched, as required.
- Rendered proportions, real hotplug behavior, `--tray` first-`Show()` sizing, and 125%/150% DPI correctness remain unverified in this Linux build environment — deferred to Plan 19-05's rig checkpoint, per the plan's own verification note.

---
*Phase: 19-monitor-tile-dashboard-monitorpanelform-retirement*
*Completed: 2026-08-09*

## Self-Check: PASSED

- FOUND: src/RigToggle.App/ThemeApplier.cs
- FOUND: src/RigToggle.App/MainForm.Designer.cs
- FOUND: src/RigToggle.App/MainForm.cs
- FOUND: .planning/phases/19-monitor-tile-dashboard-monitorpanelform-retirement/19-02-SUMMARY.md
- FOUND commit: 46d5ea3 (Task 1 — ThemeApplier.ThemeMonitorTile)
- FOUND commit: 8e38838 (Task 2 — MainForm.Designer.cs)
- FOUND commit: 24ed7f8 (Task 3 — MainForm.cs)
