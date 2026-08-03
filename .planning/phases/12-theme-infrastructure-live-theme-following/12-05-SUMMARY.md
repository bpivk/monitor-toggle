---
phase: 12-theme-infrastructure-live-theme-following
plan: 05
subsystem: ui
tags: [winforms, dwm, theming, dark-mode, gap-closure, code-review-fix]

# Dependency graph
requires:
  - phase: 12-01
    provides: DwmTitleBar/NativeMethods DWM P/Invoke surface, WindowsThemeProvider
  - phase: 12-02
    provides: Application.SetColorMode base layer, MainForm/MonitorConfirmDialog live theming, composition root
  - phase: 12-03
    provides: ThemeApplier (ThemeMonitorGrid/ApplyHotkey* helpers), SettingsForm live theme-follow
  - phase: 12-04
    provides: Rig-tester findings (title bar, buttons, audio combos NOT dark in dark mode) that this plan closes
provides:
  - DwmTitleBar.ApplyRoundedCornersAndMica now sets DWMWA_USE_IMMERSIVE_DARK_MODE from a live darkMode flag (CR-01) — the title bar actually goes dark
  - ThemeApplier.ThemeButton/ThemeComboBox — idempotent, never-throw button and combo recolor helpers (CR-02/CR-03), wired into all 7 buttons and both audio combos at load and on every live theme flip
  - WindowsThemeProvider.CurrentTheme is lock-guarded (WR-02) against the cross-thread read/write race its own docs already claimed was safe
  - MonitorConfirmDialog/SettingsForm gain a Dispose(bool) ThemeChanged unsubscribe backstop (WR-01), matching MainForm's existing pattern
affects: [12-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Explicit-override theming pattern (already established for dgvMonitors/txtHotkey in 12-03) now extended to Button and ComboBox — never trust Application.SetColorMode/FlatStyle.System for a control until rig-verified, always have an explicit ThemeApplier fallback ready"
    - "FlatStyle.Flat + BorderSize=0 + explicit FlatAppearance.MouseOverBackColor/MouseDownBackColor as the dotnet/winforms#13897 workaround for dark-mode button theming"

key-files:
  created: []
  modified:
    - src/RigToggle.Windows/DwmTitleBar.cs
    - src/RigToggle.Windows/NativeMethods.cs
    - src/RigToggle.Windows/WindowsThemeProvider.cs
    - src/RigToggle.App/ThemeApplier.cs
    - src/RigToggle.App/MainForm.Designer.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/MonitorConfirmDialog.Designer.cs
    - src/RigToggle.App/MonitorConfirmDialog.cs
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs

key-decisions:
  - "DWMWA_USE_IMMERSIVE_DARK_MODE is now called explicitly by DwmTitleBar, replacing the falsified 'owned by Application.SetColorMode' assumption from 12-01 — no double-set risk because SetColorMode demonstrably never touched this attribute on the rig runtime"
  - "Buttons switched from FlatStyle.System to FlatStyle.Flat with BorderSize=0 + explicit MouseOverBackColor/MouseDownBackColor (12-REVIEW.md CR-02 Fix Option 2), not FlatStyle.System + a dark-mode-signal-only fix — CR-02's Option 1 (fix CR-01 and hope FlatStyle.System follows) was explicitly rejected in favor of the more robust explicit-override path, since the plan's own risk note flagged Option 1 as unverified without a second rig round"
  - "Task 2/Task 3 boundary kept clean per the plan's task ordering note: Task 2 only updates the 6 DWM call sites to pass the dark flag + adds the ThemeApplier helpers + Designer FlatStyle changes + Dispose backstops (all uncalled-helper-safe); Task 3 is purely the runtime ThemeButton/ThemeComboBox wiring into all three forms — kept as two atomic commits matching the plan's intended task separation"

requirements-completed: [THEME-03, THEME-04, THEME-05]

# Metrics
duration: ~35min
completed: 2026-08-03
---

# Phase 12 Plan 05: Dark-Mode Gap Closure — Title Bar, Buttons, Audio Combos Summary

**Closes all three rig-confirmed dark-mode gaps from 12-04 (white title bar, white/light buttons, white audio-device dropdowns) by replacing three falsified "Application.SetColorMode will handle it" assumptions with explicit DWM/ThemeApplier overrides, plus the two low-cost robustness warnings (Dispose backstop, provider thread-safety lock) from the code review.**

## Performance

- **Duration:** ~35 min
- **Tasks:** 3/3
- **Files modified:** 10 (3 in RigToggle.Windows, 7 in RigToggle.App)

## Accomplishments

- `DwmTitleBar.ApplyRoundedCornersAndMica` now takes a `darkMode` flag and explicitly sets `DWMWA_USE_IMMERSIVE_DARK_MODE` before the corner/backdrop attributes (CR-01) — closes THEME-03. All 6 call sites across `MainForm`/`MonitorConfirmDialog`/`SettingsForm` pass the live theme state via new/existing `IsDark`/`IsDarkTheme` per-form properties.
- `WindowsThemeProvider.CurrentTheme` is lock-guarded (WR-02): the getter and the `OnUserPreferenceChanged` read-compare-write both run under `_themeLock`, with `ThemeChanged?.Invoke` dispatched outside the lock so no subscriber callback runs while the lock is held.
- `ThemeApplier` gains `ThemeButton` (FlatStyle.Flat + `BorderSize=0` + explicit `MouseOverBackColor`/`MouseDownBackColor`, working around the open `dotnet/winforms#13897` bug per 12-REVIEW.md CR-02) and `ThemeComboBox` (CR-03), both idempotent and never-throw, matching the existing `ThemeMonitorGrid`/`ApplyHotkey*` pattern.
- All 7 buttons (`btnToggle`, `btnSettings`, `btnContinue`, `btnCancel`, `btnBrowse`, `btnSaveSettings`, `btnDiscardChanges`) switch from `FlatStyle.System` to `FlatStyle.Flat` in their Designer files, and get `ThemeApplier.ThemeButton` calls at both load time and on every live theme flip — closes THEME-05.
- `cboAudioNormal`/`cboAudioRig` get `ThemeApplier.ThemeComboBox` calls at the end of `PopulateAudioCombo` (covers every population, not just the first) and again in `OnThemeChanged` — closes THEME-04.
- `MonitorConfirmDialog` and `SettingsForm` gain a `Dispose(bool)` `ThemeChanged` unsubscribe backstop (WR-01), matching `MainForm`'s existing pattern, so an abnormal dispose path (exception between construction and `ShowDialog` returning) can never leak a handler onto the app-lifetime `WindowsThemeProvider`.
- Full solution builds (`dotnet build RigToggle.sln -c Release`, 0 errors) and all 70 tests pass.

## Task Commits

Each task was committed atomically:

1. **Task 1: RigToggle.Windows infra — immersive dark-mode title bar + provider lock** — `5094a3a` (feat)
2. **Task 2: CR-01 App call-site fixes + ThemeApplier button/combo helpers + Designer flat buttons + Dispose backstops** — `cacd862` (feat)
3. **Task 3: Wire button + combo theming into all three forms (load + live flip)** — `689147f` (feat)

## Files Created/Modified

- `src/RigToggle.Windows/DwmTitleBar.cs` — `ApplyRoundedCornersAndMica(IntPtr, bool darkMode)` now sets `DWMWA_USE_IMMERSIVE_DARK_MODE` first, before the corner/backdrop calls; doc comment corrected
- `src/RigToggle.Windows/NativeMethods.cs` — comment above `DWMWA_USE_IMMERSIVE_DARK_MODE` corrected to no longer claim the attribute is "never call it manually"
- `src/RigToggle.Windows/WindowsThemeProvider.cs` — `CurrentTheme` is now a manually-backed property with `_themeLock`-guarded getter and read-compare-write; event dispatch stays outside the lock
- `src/RigToggle.App/ThemeApplier.cs` — new `ThemeButton`/`ThemeComboBox` methods; class doc comment corrected to no longer claim Button/ComboBox are "already owned by SetColorMode"
- `src/RigToggle.App/MainForm.Designer.cs` — `btnToggle`/`btnSettings` `FlatStyle.System` → `FlatStyle.Flat`, comments updated
- `src/RigToggle.App/MainForm.cs` — new `IsDark` property; DWM call site passes it; `ThemeButton` calls added to `InitializeTrayState()` and `OnThemeChanged`
- `src/RigToggle.App/MonitorConfirmDialog.Designer.cs` — `btnContinue`/`btnCancel` `FlatStyle.System` → `FlatStyle.Flat`; `Dispose(bool)` gains the `ThemeChanged` unsubscribe backstop
- `src/RigToggle.App/MonitorConfirmDialog.cs` — `using RigToggle.Core.Models;` added; new `IsDark` property; DWM call sites pass it; `ThemeButton` calls added to ctor and `OnThemeChanged`
- `src/RigToggle.App/SettingsForm.Designer.cs` — `btnBrowse`/`btnSaveSettings`/`btnDiscardChanges` `FlatStyle.System` → `FlatStyle.Flat`; `Dispose(bool)` gains the `ThemeChanged` unsubscribe backstop
- `src/RigToggle.App/SettingsForm.cs` — DWM call sites pass `IsDarkTheme`; `ThemeButton` calls added to `SettingsForm_Load` and `OnThemeChanged`; `ThemeComboBox` call added to `PopulateAudioCombo` and `OnThemeChanged`

## Decisions Made

- Kept the Task 2/Task 3 boundary exactly as the plan specified (Task 2 = call-site fixes + helpers + Designer changes + Dispose backstops, uncalled-helper-safe; Task 3 = pure runtime wiring) rather than collapsing all button-theming work into a single commit — this matches the plan's explicit "Task ordering note" and keeps each task's `<verify>` gate meaningful (Task 2 verifies `RigToggle.App` compiles with the new call sites; Task 3 verifies the full solution + tests with the helpers actually invoked).
- No changes beyond what the plan specified. All palette values (dark surface `#2D2D30`, primary text `#F0F0F0`, hover `#3E3E42`, pressed `#1C1C1E`) were taken directly from the plan's `<interfaces>` section and match the existing grid/hotkey helpers exactly.

## Deviations from Plan

None — plan executed exactly as written. The one self-correction (initially wiring Task 3's `ThemeButton` calls into `MainForm.cs`/`MonitorConfirmDialog.cs` while still doing Task 2's edits, then reverting to keep the task boundary clean) was caught and fixed before any commit, so it left no trace in the git history and is not a deviation from the delivered plan — it is documented here only for full transparency of the execution process.

## Issues Encountered

None. All three tasks' acceptance criteria (grep gates + `dotnet build`/`dotnet test`) passed on the first attempt.

## User Setup Required

None.

## Next Phase Readiness

- Plan 12-06 (rig re-verification) can now confirm on real Windows 11 hardware that the title bar renders dark, all 7 buttons render dark with correct hover/pressed states, and both audio-device combos render dark, all at load and after a live theme flip.
- Full solution builds and all 70 tests pass with no regressions.
- No new NuGet packages; the two robustness warnings (WR-01 Dispose backstop, WR-02 provider lock) are closed alongside the three critical rig-confirmed gaps.

## Self-Check: PASSED

- FOUND: src/RigToggle.Windows/DwmTitleBar.cs (modified)
- FOUND: src/RigToggle.Windows/NativeMethods.cs (modified)
- FOUND: src/RigToggle.Windows/WindowsThemeProvider.cs (modified)
- FOUND: src/RigToggle.App/ThemeApplier.cs (modified)
- FOUND: src/RigToggle.App/MainForm.Designer.cs (modified)
- FOUND: src/RigToggle.App/MainForm.cs (modified)
- FOUND: src/RigToggle.App/MonitorConfirmDialog.Designer.cs (modified)
- FOUND: src/RigToggle.App/MonitorConfirmDialog.cs (modified)
- FOUND: src/RigToggle.App/SettingsForm.Designer.cs (modified)
- FOUND: src/RigToggle.App/SettingsForm.cs (modified)
- FOUND commit 5094a3a
- FOUND commit cacd862
- FOUND commit 689147f

---
*Phase: 12-theme-infrastructure-live-theme-following*
*Completed: 2026-08-03*
