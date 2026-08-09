---
phase: 17-manual-monitor-panel-shared-safety-guard
plan: 02
subsystem: ui
tags: [winforms, datagridview, ccd, monitor-control, systemevents]

# Dependency graph
requires:
  - phase: 17-manual-monitor-panel-shared-safety-guard (Plan 01)
    provides: "ToggleOrchestrator.BeginExclusiveMonitorAccess() lease and MonitorIdentifyOverlay, both consumed directly by this plan's panel"
provides:
  - "MonitorPanelForm: non-modal WinForms window (PANEL-01..05) backed directly by IMonitorController, with zero re-implementation of the zero-survivors guard (DISPLAY-12)"
  - "Exact constructor signature `public MonitorPanelForm(IMonitorController monitorController, ISettingsStore settingsStore, IThemeProvider themeProvider, ToggleOrchestrator orchestrator)` for Plan 03's composition-root factory"
affects: [17-03-entry-points, 17-04-rig-checkpoint]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Panel row actions call IMonitorController.ActivateMonitors/DeactivateMonitors directly, never ToggleService/ToggleOrchestrator's toggle methods -- the second, independent caller of the same DeactivateMonitors zero-survivors guard Rig/Normal toggle already uses"
    - "Every panel mutation acquires the 17-01 BeginExclusiveMonitorAccess() lease BEFORE opening the (nested-message-loop) confirmation dialog, held across the whole mutation via `using`"
    - "Two status-dot Bitmaps built once in the constructor and shared across every PopulateMonitorGrid() call (hotplug-triggered or not), never re-allocated per row"
    - "Identify overlay numbering derives from DataGridView row display order, not CaptureState().Paths order, so overlay numbers match what the user just saw in the grid"

key-files:
  created:
    - src/RigToggle.App/MonitorPanelForm.Designer.cs
    - src/RigToggle.App/MonitorPanelForm.cs
  modified: []

key-decisions:
  - "DataGridViewButtonColumn (colAction) kept ReadOnly=true at the grid level -- a button-column cell still raises CellClick in a read-only grid, and read-only mode structurally rules out the DataGridViewCheckBoxCell dirty-state commit-lag quirk (17-RESEARCH.md Pitfall 5) without needing SettingsForm's CurrentCellDirtyStateChanged workaround"
  - "CreateStatusDot(bool isActive) takes no isDark parameter -- both status-dot colors (#2ECC71 active / #C83C3C inactive) are theme-independent per 17-UI-SPEC.md, so re-rendering on theme flip would be a no-op"

patterns-established:
  - "Second-caller convergence on a shared safety guard: a UI entry point proves DISPLAY-12 by calling the exact same IMonitorController method the existing callers use, adding no new pre-check"

requirements-completed: [PANEL-01, PANEL-02, PANEL-03, PANEL-04, PANEL-05, DISPLAY-12]

# Metrics
duration: 8min
completed: 2026-08-08
---

# Phase 17 Plan 02: Manual Monitor Panel & Shared Safety Guard Summary

**`MonitorPanelForm` — a non-modal DataGridView-based panel with per-row status-dot icons, immediate-effect Enable/Disable action buttons, live hotplug refresh, a reused single-monitor confirmation gate, and a grid-row-ordered Identify overlay — mutates monitors through the exact same `IMonitorController.DeactivateMonitors`/`ActivateMonitors` calls the Rig/Normal toggle already uses, so DISPLAY-12's zero-survivors guard applies with zero new code.**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-08-08T17:15:43Z
- **Completed:** 2026-08-08T17:23:03Z
- **Tasks:** 3 completed
- **Files modified:** 2 (both created)

## Accomplishments

- `MonitorPanelForm.Designer.cs` — status-icon column (32px `DataGridViewImageColumn`), Fill-mode name column, per-row 80px `DataGridViewButtonColumn` (label set per-row, never column-wide), empty-state label, `Identify` button, and a `Dispose(bool)` backstop that unsubscribes `ThemeChanged`/`DisplaySettingsChanged` and disposes the two shared status-dot bitmaps.
- `MonitorPanelForm.cs` — constructor takes `IMonitorController`, `ISettingsStore`, `IThemeProvider`, `ToggleOrchestrator` (no `IAudioController`/`IAutostartConfigurator`, confirming PANEL-02's independence from the full Settings surface); builds two shared status-dot `Bitmap`s once; subscribes `IThemeProvider.ThemeChanged` and `Microsoft.Win32.SystemEvents.DisplaySettingsChanged` with `FormClosed`-based unsubscribe; applies DWM Mica/rounded-corner chrome and grid/button theming at construction.
- `PopulateMonitorGrid()` renders one `Tag`-keyed row per `GetAllMonitors()` entry (active AND OS-disabled), reproducing `SettingsForm`'s Primary/OS-disabled suffix logic verbatim, with an empty-state fallback when zero monitors are detected.
- `OnDisplaySettingsChanged` marshals via `InvokeRequired`/`BeginInvoke` and re-populates the grid on every hotplug event (PANEL-03) — the self-triggered double-refresh from the panel's own mutations is documented inline as intentional, not suppressed.
- Row actions: `DgvMonitorPanel_CellClick` resolves the clicked row's `DevicePath` via `Tag`, branching to `DisableMonitor`/`EnableMonitor` based on the resolved `MonitorInfo.IsActive`. Both acquire the Plan 01 `BeginExclusiveMonitorAccess()` lease before doing anything else (including opening the confirmation dialog, which pumps `WM_HOTKEY` via its nested message loop). `DisableMonitor` gates on `AppSettings.SkipMonitorConfirmation` via the existing `MonitorConfirmDialog` (single-monitor, `enableNames: Array.Empty<string>()`), persists `SkipMonitorConfirmation = true` on "don't ask again", then calls `IMonitorController.DeactivateMonitors(new HashSet<string> { devicePath })` directly — no monitor-count pre-check anywhere in the file (verified by the DISPLAY-12 no-duplicate-guard grep audit: zero matches for `Count(...IsActive`, `Where(...IsActive...).Count`, `"at least one active display"`, `"survivors"`). `EnableMonitor` calls `ActivateMonitors` the same way, ungated (PANEL-04 only gates Disable).
- `BtnIdentify_Click` calls `CaptureState()`, builds a duplicate-tolerant `DevicePath -> MonitorPathSnapshot` lookup, then iterates the grid's own `DataGridViewRow` collection in display order (not `state.Paths` order) so overlay numbers match what the user is looking at — skipping rows with no matching snapshot (OS-disabled monitors have no active desktop surface) or a degenerate zero-size mode record, and showing one `MonitorIdentifyOverlay` per surviving row. Identify does not acquire the exclusive lease (read-only, mutates no topology).
- Full solution builds with 0 errors across all 6 projects; core test suite unchanged at 84/84 (this plan adds no `RigToggle.Core`/`RigToggle.Windows` logic — every capability is consumed via `IMonitorController` as-is).

## Task Commits

Each task was committed atomically:

1. **Task 1: MonitorPanelForm.Designer.cs — layout, columns, disposal backstop** - `74d35e2` (feat)
2. **Task 2: MonitorPanelForm.cs — lifecycle, theming, row population, live hotplug refresh** - `a63c100` (feat)
3. **Task 3: MonitorPanelForm.cs — row actions, confirmation gate, shared safety guard, Identify** - `282e67c` (feat)

**Plan metadata:** (this commit, following SUMMARY.md creation)

## Files Created/Modified

- `src/RigToggle.App/MonitorPanelForm.Designer.cs` (new) — `partial class MonitorPanelForm`: `dgvMonitorPanel` (`colStatus`/`colMonitorName`/`colAction`), `lblEmptyState`, `btnIdentify`, `Dispose(bool)` backstop
- `src/RigToggle.App/MonitorPanelForm.cs` (new) — `public partial class MonitorPanelForm : Form` with the full constructor, `PopulateMonitorGrid`, `OnDisplaySettingsChanged`, `OnThemeChanged`, `DgvMonitorPanel_CellClick`, `TryAcquireMonitorAccess`, `DisableMonitor`, `EnableMonitor`, `BtnIdentify_Click`

## Exact Signature for Plan 03

```csharp
public MonitorPanelForm(IMonitorController monitorController, ISettingsStore settingsStore, IThemeProvider themeProvider, ToggleOrchestrator orchestrator)
```

Namespace `RigToggle.App`. Null-guards all four parameters. Non-modal — callers must use `Show()`/`Activate()`, never `ShowDialog()` (PANEL-03 requires the panel to stay open and live-refresh without blocking the rest of the app). Plan 03's composition-root factory should thread the same already-constructed `monitorController`/`settingsStore`/`themeProvider`/`toggleOrchestrator` locals `Program.cs` already has for `SettingsFormFactory`.

## Decisions Made

- `colAction`'s `DataGridViewButtonColumn` is kept inside a `ReadOnly = true` grid (with an inline comment explaining why this is safe) rather than converting it to a checkbox column — sidesteps 17-RESEARCH.md Pitfall 5 (checkbox dirty-state commit lag) entirely instead of reproducing `SettingsForm`'s `CurrentCellDirtyStateChanged` workaround.
- `CreateStatusDot(bool isActive)` deliberately has no `isDark` parameter, since 17-UI-SPEC.md pins both status-dot colors as theme-independent literals — documented inline so a future editor doesn't add a parameter that would never change rendering.

## Deviations from Plan

None — plan executed exactly as written. All grep-based acceptance criteria across all three tasks passed on the first implementation without requiring rework; the full-solution build succeeded with 0 errors and the core test suite stayed at 84/84 (unchanged from Plan 01), matching the plan's stated `<done>` criteria for Task 3.

## Issues Encountered

None.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- `MonitorPanelForm` is fully wired and ready for Plan 03 to construct via a composition-root factory and expose from `MainForm`'s new button and tray menu entry — this plan intentionally does not wire anything to open it yet (per its own `<objective>`).
- Full solution builds with 0 errors across all 6 projects (`RigToggle.Core`, `RigToggle.Windows`, `RigToggle.App`, `RigToggle.Tests`, `RigToggle.Windows.Tests`, `RigToggle.IconGen`).
- Core test suite green at 84/84 (unchanged — this plan adds no `RigToggle.Core` logic). `RigToggle.Windows.Tests` still cannot execute on this Linux dev host (`Microsoft.WindowsDesktop.App` runtime not installed — known, documented environment limitation carried over from prior phases), but builds cleanly.
- No `.csproj` file was modified (`git diff --name-only | grep -c '\.csproj$'` -> `0`).
- Runtime behavior (grid rendering, hotplug live-refresh, Identify overlay on-screen placement/DPI accuracy) is NOT provable in this Linux build environment — deferred to Plan 04's rig checkpoint by design, per this plan's own `<verification>` section.

## Known Stubs

None. `MonitorPanelForm` is a complete, self-contained implementation of PANEL-01..05 — every row action, the confirmation gate, and the Identify overlay call real `IMonitorController`/`ToggleOrchestrator` methods, not mocked/placeholder data. It is simply not yet referenced by any caller (`MainForm`/`Program.cs`), exactly as this plan's `<objective>` states: "Nothing opens this window yet — Plan 03 adds the entry points."

---
*Phase: 17-manual-monitor-panel-shared-safety-guard*
*Completed: 2026-08-08*

## Self-Check: PASSED

- FOUND: src/RigToggle.App/MonitorPanelForm.Designer.cs
- FOUND: src/RigToggle.App/MonitorPanelForm.cs
- FOUND: 74d35e2 (Task 1 commit)
- FOUND: a63c100 (Task 2 commit)
- FOUND: 282e67c (Task 3 commit)
