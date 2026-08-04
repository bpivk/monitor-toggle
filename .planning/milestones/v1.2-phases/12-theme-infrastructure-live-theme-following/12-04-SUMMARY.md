---
phase: 12-theme-infrastructure-live-theme-following
plan: 04
subsystem: ui
tags: [winforms, dwm, theming, dark-mode, rig-verification]

requires:
  - phase: 12-02
    provides: Application.SetColorMode base layer, WindowsThemeProvider live-follow, MainForm/MonitorConfirmDialog theming
  - phase: 12-03
    provides: SettingsForm control-level theming (DataGridView, GroupBox->Panel, txtHotkey), ThemeApplier
provides:
  - Release build + self-contained single-file publish verified working (dotnet publish win-x64)
  - Rig verification results (Windows 11) — 3 concrete gaps identified, not a clean pass
affects: [theme-infrastructure-gap-closure]

tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified: []

key-decisions:
  - "Rig checkpoint FAILED — phase not marked complete. Routed to gap closure instead of proceeding to verify_phase_goal/update_roadmap."

patterns-established: []

requirements-completed: []  # THEME-01/02/04/06/D-01/D-03 confirmed by human tester; THEME-03/05 and the audio-device ComboBox NOT satisfied — see Gaps below. Do not mark complete in REQUIREMENTS.md until gap closure lands.

duration: ~15min (Task 1 automated + human rig session)
completed: 2026-08-03
---

# Phase 12: Theme Infrastructure & Live Theme-Following — Plan 04 Summary

**Release build + publish succeeded; rig verification on Windows 11 found the title bar, all buttons, and the Settings audio-device dropdowns still rendering in the light/white system theme in dark mode**

## Performance

- **Duration:** ~15 min (Task 1 build/publish automated; Task 2 human rig session)
- **Tasks:** 1/2 (Task 1 passed; Task 2 checkpoint FAILED)
- **Files modified:** 0 (verification-only plan)

## Accomplishments
- Confirmed `dotnet build RigToggle.sln -c Release` and `dotnet publish src/RigToggle.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` both succeed, producing a current `RigToggle.App.exe` for rig testing.
- Human rig verification (Windows 11) confirmed several checks pass: launch-theme detection, live theme flip (visible and `--tray`-hidden), monitor DataGridView recoloring, `txtHotkey` state palette, GroupBox→Panel flat layout, MonitorConfirmDialog theming, and the accepted D-01/D-03 limitations (MessageBox popups, tray separator).
- Human rig verification found **3 concrete gaps** — see below.

## Task Commits

1. **Task 1: Build and publish the app for rig testing** — no commit (build artifact only, `files_modified: []` per plan frontmatter). Verified: `dotnet build RigToggle.sln -c Release` (0 errors), `dotnet publish src/RigToggle.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` produced `src/RigToggle.App/bin/Release/net10.0-windows/win-x64/publish/RigToggle.App.exe` (112 MB, single-file self-contained, `PublishTrimmed` left false per CLAUDE.md).
2. **Task 2: Rig verification of theme behavior (THEME-01..06)** — **checkpoint FAILED**. No implementation commit; this task is human-verify only.

No `docs(12-04): ...` plan-metadata commit — this SUMMARY.md itself is committed as the record of the failed checkpoint.

## Files Created/Modified
None — this plan is verification-only (`files_modified: []`).

## Decisions Made
- Did not proceed to `verify_phase_goal`/`update_roadmap` after the checkpoint failed — the automated `gsd-verifier` agent cross-references code artifacts against `must_haves`, not actual rendered pixels, so it cannot itself catch a "the code sets `FlatStyle.System` but the button still renders white" class of bug. The human rig report is authoritative here and is recorded directly as gaps for `/gsd:plan-phase 12 --gaps` instead.

## Deviations from Plan

None — plan executed exactly as written; Task 2 failing is a genuine verification finding, not a deviation.

## Issues Encountered

### Rig checkpoint: 3 failures on Windows 11 (dark mode)

**Rig environment:** Windows 11 (confirmed by user). This resolves the plan's open question about which Windows-version fallback path would be exercised — Win11, so Mica/rounded-corners were expected to apply; separately, Mica/corners were not reported as a problem, so THEME-06 appears to be working. The failures below are unrelated to the Windows-version fallback logic.

**1. THEME-03 (Title bar) — FAILED**
- **Expected:** In dark mode, both MainForm's and SettingsForm's title bars render dark.
- **Observed:** Title bar remains white/light in dark mode on both forms.
- **Likely area:** Plan 12-01 declared `DWMWA_USE_IMMERSIVE_DARK_MODE = 20` in `NativeMethods` but deliberately did NOT call it manually — 12-01-PLAN.md's own comment states it is "owned by Application.SetColorMode — declared but never called manually here," i.e. the design bet that `.NET 10`'s `Application.SetColorMode(SystemColorMode.System)` alone would flip the DWM title-bar attribute. The rig result contradicts that assumption. Needs investigation: either `SetColorMode` does not actually call `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)` under net10.0-windows/WinForms the way research assumed, or it requires an explicit manual call (the const is already declared and ready to use in `NativeMethods`).

**2. THEME-05 (Flat button/panel styling) — PARTIALLY FAILED**
- **Expected:** Buttons on all three forms render flat, colored per current theme.
- **Observed:** Buttons render white/light-themed even in dark mode (flatness itself wasn't reported as wrong — the coloring was).
- **Likely area:** Plans 12-02/12-03 deliberately used `FlatStyle.System` (not `.Flat`) per Pitfall 6 (`dotnet/winforms#13897`) to avoid a different bug — but `FlatStyle.System` may not receive the dark-mode color treatment from `Application.SetColorMode` the way plain/default-style buttons do. Needs research into whether .NET 10's SystemColorMode dark support actually covers `FlatStyle.System` buttons, or whether an explicit `BackColor`/`ForeColor` override (or a different FlatStyle) is required.

**3. Audio-device ComboBox selection (SettingsForm) — FAILED (new finding, not explicitly enumerated in 12-UI-SPEC.md's original control list)**
- **Expected (implied by THEME-04's "every control... is legibly recolored" clause):** The audio-device selection dropdown(s) in SettingsForm follow dark mode like the rest of the form.
- **Observed:** The audio-device dropdown selection remains white.
- **Likely area:** Same class of gap as the `dgvMonitors`/`txtHotkey` fixes already built in `ThemeApplier` (plan 12-03) — this control was apparently not in that pass's scope and needs its own targeted override, or `Application.SetColorMode` doesn't reach ComboBox selection highlighting either.

## User Setup Required

None.

## Next Phase Readiness

**Phase 12 is NOT complete.** `update_roadmap`/`verify_phase_goal` were intentionally skipped — do not mark THEME-03/THEME-05 as satisfied in REQUIREMENTS.md. Next step is a gap-closure planning pass:

```
/gsd:plan-phase 12 --gaps
```

Gap-closure should investigate .NET 10's `Application.SetColorMode(SystemColorMode.System)` actual coverage (title bar attribute, button FlatStyle.System coloring, ComboBox selection coloring) and either fix the coverage assumption or add explicit `DwmSetWindowAttribute`/`ThemeApplier`-style overrides for the three failing surfaces. Everything else verified clean on the Windows 11 rig (THEME-01, THEME-02, THEME-04's DataGridView/GroupBox/txtHotkey work, THEME-06, D-01, D-03).

---
*Phase: 12-theme-infrastructure-live-theme-following*
*Completed: 2026-08-03 (Task 2 checkpoint failed — gap closure required)*
