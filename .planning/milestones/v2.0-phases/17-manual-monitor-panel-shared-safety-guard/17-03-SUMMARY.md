---
phase: 17-manual-monitor-panel-shared-safety-guard
plan: 03
subsystem: ui
tags: [winforms, composition-root, entry-points]

# Dependency graph
requires:
  - phase: 17-manual-monitor-panel-shared-safety-guard (Plan 02)
    provides: "MonitorPanelForm with constructor signature public MonitorPanelForm(IMonitorController, ISettingsStore, IThemeProvider, ToggleOrchestrator)"
provides:
  - "MainForm.OpenMonitorPanel(): shared non-modal open/focus entry point for the Monitors button and tray item"
  - "Program.cs MonitorPanelFormFactory: composition-root factory reusing existing monitorController/settingsStore/themeProvider/toggleOrchestrator locals"
affects: [17-04-rig-checkpoint]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Cached-instance non-modal window pattern: MonitorPanelForm? field re-created only when null or IsDisposed, contrasted with SettingsForm's using-var-per-open ShowDialog pattern"
    - "Composition-root factory that deliberately narrows injected dependencies (T-17-12): MonitorPanelFormFactory passes only 4 of 8+ available composition-root locals, structurally excluding audioController/appController/autostartConfigurator/modeStore/markerStore"

key-files:
  created: []
  modified:
    - src/RigToggle.App/MainForm.Designer.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/Program.cs

key-decisions:
  - "OpenMonitorPanel() does NOT call UnregisterConfiguredHotkey() or RefreshUi(), unlike OpenSettingsDialog -- both omissions are load-bearing per the plan's key_differences_from_settings, verified by grep count-unchanged-at-3 and placement checks"
  - "btnMonitors placed at y=148 (8px below btnSettings' y=140 end), ClientSize left at (320,200) unchanged -- 20px bottom margin already exceeds the form's 16px margin rhythm"

requirements-completed: [PANEL-01, PANEL-02, PANEL-03]

# Metrics
duration: 4min
completed: 2026-08-08
---

# Phase 17 Plan 03: Manual Monitor Panel Entry Points Summary

**Added a `Monitors…` button to `MainForm` and a `Monitors` tray context-menu entry, both routing through one shared `OpenMonitorPanel()` that shows a cached, non-modal `MonitorPanelForm` built by a new composition-root factory in `Program.cs` — closing the App-tier wiring gap left open by Plan 02 and restoring a fully green solution build.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-08-08T17:26:30Z (base commit)
- **Completed:** 2026-08-08T17:30:22Z
- **Tasks:** 2 completed
- **Files modified:** 3

## Accomplishments

- `MainForm.Designer.cs`: added `btnMonitors` (`Text = "Monitors…"`, `Location = (16, 148)`, `Size = (288, 32)`, `FlatStyle.Flat`) positioned with an 8px gap below `btnSettings`, `ClientSize` left unchanged at `(320, 200)` (checked and commented — 20px bottom margin already exceeds the form's 16px rhythm). Added `trayMonitorsMenuItem` (`Text = "Monitors"`, no ellipsis) inserted into `trayContextMenu.Items.AddRange` in the locked order: Toggle, Settings, Monitors, separator, Exit. Both new fields declared alongside their Settings counterparts; `Dispose(bool)` untouched (no special disposal needed — `btnMonitors` is `Controls`-owned, `trayMonitorsMenuItem` is `trayContextMenu`-owned, and `components` already disposes the menu).
- `MainForm.cs`: extended the constructor with a new `Func<MonitorPanelForm> monitorPanelFormFactory` parameter (immediately after `settingsFormFactory`, null-guarded like every other parameter) and a mutable `MonitorPanelForm? _monitorPanelForm` cache field. Added `BtnMonitors_Click`/`TrayMonitorsMenuItem_Click`, both one-liners delegating to a new `OpenMonitorPanel()` that: re-creates the cached instance only when null or `IsDisposed`, calls `Show()` (never `ShowDialog()`), restores from minimized state if needed, then `Activate()`s. The method's doc comment enumerates all four deliberate divergences from `OpenSettingsDialog` (non-modal, cached-not-transient, no hotkey unregister, no `RefreshUi()`) and explicitly warns future editors not to normalize them — the hotkey behavior is described only in prose ("the hotkey is deliberately left registered"), never as a literal `UnregisterConfiguredHotkey()` call, keeping the pre-task baseline count of 3 occurrences unchanged. Theming wired at both `OnThemeChanged` (live flip) and `InitializeTrayState` (covers the `--tray` startup path where `OnLoad`/`OnShown` never fire) via `ThemeApplier.ThemeButton(btnMonitors, IsDark)`. Class doc comment updated to mention the Monitors tray entry and the non-modal Phase 17 panel it opens.
- `Program.cs`: added a `MonitorPanelFormFactory` local function immediately after the existing `SettingsFormFactory`, constructing `new MonitorPanelForm(monitorController, settingsStore, themeProvider, toggleOrchestrator)` — reusing the exact composition-root locals already built for other adapters, with zero new adapter construction. A comment records that, unlike `SettingsFormFactory`, this factory captures no `mainForm` member and so has no part in the textual pre-declaration cycle the surrounding comment explains, and that `toggleOrchestrator` is threaded through solely for the panel's `BeginExclusiveMonitorAccess()` lease, never to run a toggle. Updated the `MainForm` construction call site to pass `MonitorPanelFormFactory` as the new fifth positional argument. No other startup ordering, `StartupRecoveryChecker.Run`, `InitializeTrayState()`, `RegisterHotkeyAtStartup()`, or `Application.Run` branch touched.
- Full solution builds with 0 errors across all 6 projects; core test suite unchanged at 84/84 (this plan wires existing components — no `RigToggle.Core`/`RigToggle.Windows` logic changed). No `.csproj` file modified.

## Task Commits

Each task was committed atomically:

1. **Task 1: MainForm.Designer.cs — btnMonitors button + trayMonitorsMenuItem** - `0920267` (feat)
2. **Task 2: MainForm.cs + Program.cs — panel factory, cached instance, handlers, theming** - `d3e2a9a` (feat)

**Plan metadata:** (this commit, following SUMMARY.md creation)

## Files Created/Modified

- `src/RigToggle.App/MainForm.Designer.cs` (modified) — `btnMonitors` button, `trayMonitorsMenuItem` tray entry, updated `Items.AddRange` order and comment
- `src/RigToggle.App/MainForm.cs` (modified) — `_monitorPanelFormFactory`/`_monitorPanelForm` fields, extended constructor, `BtnMonitors_Click`/`TrayMonitorsMenuItem_Click`/`OpenMonitorPanel()`, theming call sites, class doc comment
- `src/RigToggle.App/Program.cs` (modified) — `MonitorPanelFormFactory` local function, updated `MainForm` construction call

## Acceptance Criteria Verification

All plan-specified grep-based acceptance criteria were run and passed, including the negative/placement checks:
- `_monitorPanelForm.ShowDialog` occurrences: 0 (never modal)
- Both entry points route through `OpenMonitorPanel()`: `=> OpenMonitorPanel();` count = 2
- Re-create guard present: `_monitorPanelForm.IsDisposed` count = 1
- Theming wired at both call sites: `ThemeApplier.ThemeButton(btnMonitors, IsDark)` count = 2
- `UnregisterConfiguredHotkey` count unchanged from pre-task baseline of 3 (comment ~line 51, `OpenSettingsDialog` call ~line 468, method definition ~line 770) — confirmed zero occurrences inside `OpenMonitorPanel`/`BtnMonitors_Click`/`TrayMonitorsMenuItem_Click` bodies
- `new WindowsMonitorController()` / `new WindowsThemeProvider()` / `new JsonSettingsStore(` each still construct exactly once in `Program.cs`
- `git diff --name-only | grep -c '\.csproj$'` → `0`
- `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` → 0 Errors
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` → Failed: 0, Total: 84

## Decisions Made

- Kept the hotkey-unregister omission and `RefreshUi()` omission exactly as scoped by the plan's `<key_differences_from_settings>` — verified these are load-bearing via the plan's own grep/placement acceptance criteria rather than treating them as oversights to "fix."
- `OpenMonitorPanel()`'s doc comment intentionally never writes a literal call to `UnregisterConfiguredHotkey`, describing the behavior only in prose, so the identifier's total-occurrence count stays pinned at the pre-task baseline of 3.

## Deviations from Plan

None — plan executed exactly as written. All grep-based acceptance criteria across both tasks passed on the first implementation without requiring rework; the full-solution build succeeded with 0 errors and the core test suite stayed at 84/84 (unchanged from Plan 02).

## Issues Encountered

None.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Both entry points (`Monitors…` button, `Monitors` tray item) are fully wired to a single non-modal, cached `MonitorPanelForm` instance built by a composition-root factory that injects only the four dependencies `MonitorPanelForm` needs.
- Full solution builds with 0 errors across all 6 projects; core test suite green at 84/84 (unchanged).
- Runtime behavior (button opens the panel, tray entry works while hidden to tray, second click focuses rather than duplicates, theme follows on both startup paths and live flips) is NOT provable in this Linux build environment — deferred to Plan 04's rig checkpoint by design, per this plan's own `<verification>` section.
- No `.csproj` file was modified (`git diff --name-only | grep -c '\.csproj$'` → `0`).

## Known Stubs

None. Both entry points call real `MonitorPanelForm` construction/`Show()`/`Activate()` logic against the real composition-root singletons — nothing here is mocked or placeholder.

---
*Phase: 17-manual-monitor-panel-shared-safety-guard*
*Completed: 2026-08-08*

## Self-Check: PASSED

- FOUND: src/RigToggle.App/MainForm.Designer.cs
- FOUND: src/RigToggle.App/MainForm.cs
- FOUND: src/RigToggle.App/Program.cs
- FOUND: 0920267 (Task 1 commit)
- FOUND: d3e2a9a (Task 2 commit)
