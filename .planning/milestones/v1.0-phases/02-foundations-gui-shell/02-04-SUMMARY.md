---
phase: 02-foundations-gui-shell
plan: 04
subsystem: ui
tags: [winforms, settings-dialog, combobox-binding, error-provider, file-browser]

# Dependency graph
requires:
  - phase: 02-foundations-gui-shell (Plan 02-01)
    provides: RigToggle.Core Abstractions (IMonitorController, IAudioController, ISettingsStore) and Models (AppSettings, MonitorInfo, AudioDeviceInfo)
provides:
  - SettingsForm modal dialog (three-GroupBox layout: Monitor, Audio Devices, Application Path)
  - ComboBox stale-selection detection pattern (D-10) with first-run distinction (Pitfall 3)
  - Save-button validation gating (D-12) wired across all four fields
  - .exe-filtered file browse (D-06) and ISettingsStore.Save persistence on Save
affects: [02-05 (MainForm/composition root wiring — will ShowDialog this form)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ComboBox unhook-before-DataSource / rehook-after to avoid spurious SelectedIndexChanged mid-populate"
    - "savedId == null (first-run, no warning) vs savedId != null && not found (D-10 stale warning) as distinct branches"
    - "Fresh List<PickerItem> copy per ComboBox.DataSource to avoid shared CurrencyManager position across two bound combos"
    - "Real-read enumeration wrapped in try/catch degrading to empty-state, never crashing Settings open"

key-files:
  created:
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs
  modified: []

key-decisions:
  - "D-10 stale warning is gated strictly on savedId is not null && no match — first-run (null saved ID) shows zero warnings, matching Pitfall 3"
  - "PickerItem is a C# record so value-based Equals lets SelectedItem resolve correctly even when set from a different List instance than the bound DataSource (needed for audio combos, which get independent list copies)"
  - "Enumeration reads (GetActiveMonitors/GetPlaybackDevices) wrapped in try/catch, degrading to the UI-SPEC empty-state copy rather than allowing an unhandled exception to crash Settings open (Pitfall 2's defensive posture applied uniformly, not just to audio)"

patterns-established:
  - "SettingsForm depends only on IMonitorController/IAudioController/ISettingsStore via constructor injection — zero concrete Windows adapter references in code-behind"

requirements-completed: [SETTINGS-01, SETTINGS-02, SETTINGS-03]

# Metrics
duration: 24min
completed: 2026-07-24
---

# Phase 02 Plan 04: Settings Dialog Summary

**WinForms Settings modal (Monitor / Audio Devices / Application Path GroupBoxes) bound to real IMonitorController/IAudioController enumeration, with D-10 stale-selection warnings, D-12 Save-gating, and .exe-filtered browse persisting via ISettingsStore.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-07-24T13:49:00Z (approx, session start)
- **Completed:** 2026-07-24T14:13:16Z
- **Tasks:** 3 completed
- **Files modified:** 2 (both newly created)

## Accomplishments
- Built the modal three-section Settings dialog layout exactly per UI-SPEC (FixedDialog, 420×380, CenterParent, no taskbar entry, system-default styling only)
- Wired all three ComboBoxes (monitor, normal audio, rig audio) to real enumeration via Core interfaces, with the unhook/rehook `SelectedIndexChanged` pattern to avoid spurious mid-populate validation runs
- Implemented the D-10 stale-selection warning with a correct first-run distinction (Pitfall 3): a brand-new install with null saved IDs shows zero warnings; a previously-saved-but-now-missing selection shows the inline "not found — please reselect" copy
- Implemented Save-button gating (D-12) across all four fields (monitor, both audio devices, app path) and .exe-filtered Browse (D-06) with save-time path validation (V5 input-validation gate, T-02-BADPATH mitigation)

## Task Commits

Each task was committed atomically:

1. **Task 1: SettingsForm.Designer.cs — modal three-section layout** - `a182896` (feat)
2. **Task 2: SettingsForm.cs — populate pickers, stale detection, validation gating** - `4547d48` (feat)
3. **Task 3: SettingsForm.cs — .exe browse + Save/Discard persistence** - `6dcd71b` (feat)

**Plan metadata:** (this commit, docs: complete plan)

## Files Created/Modified
- `src/RigToggle.App/SettingsForm.Designer.cs` - Modal FixedDialog layout: 3 GroupBoxes (Monitor / Audio Devices / Application Path), 3 DropDownList ComboBoxes, read-only app-path TextBox + Browse button, 4 ErrorProviders, hidden inline warning Labels, Save Settings (`DialogResult.OK`) / Discard Changes (`DialogResult.Cancel`) buttons, `OpenFileDialog` filtered to `*.exe`
- `src/RigToggle.App/SettingsForm.cs` - Constructor-injects `IMonitorController`/`IAudioController`/`ISettingsStore`; `PopulateMonitorPicker`/`PopulateAudioPickers`/`PopulateAppPathField` populate from real enumeration with D-10 stale-detection and Pitfall 3's first-run distinction; `ValidateSettingsForm` gates Save on all 4 fields; `BtnBrowse_Click` opens the `.exe`-filtered dialog; `BtnSaveSettings_Click` persists `AppSettings` via `ISettingsStore.Save` before the declarative `DialogResult.OK` closes the dialog

## Decisions Made
- Enumeration calls (`GetActiveMonitors`, `GetPlaybackDevices`) are wrapped in `try/catch` degrading to the empty-state UI copy rather than propagating an exception — extends Pitfall 2's "defensive `GetDevice` handling" recommendation to the enumeration calls themselves, since the research flagged `GetDevice`'s throw-vs-null behavior as unconfirmed and the safest posture is "never let Settings fail to open."
- Each audio ComboBox binds an independent `List<PickerItem>` copy (`items.ToList()`) rather than sharing one list instance, avoiding a known WinForms pitfall where two ComboBoxes bound to the exact same list object share a `CurrencyManager` position (not explicitly called out in 02-RESEARCH.md, but implied by "avoid caching/sharing across repeated calls" — added defensively per Rule 2, this is a correctness requirement for the two independent audio pickers to behave independently).
- Removed `UseVisualStyleBackColor = true` button assignments that the WinForms designer scaffold normally includes, because the literal value (`true`) is already the Button default and the plan's own acceptance-criteria grep (`grep -c "BackColor\|ForeColor\|new Font("`) would otherwise false-positive-match `UseVisualStyleBackColor` even though it sets no custom color. Omitting the redundant explicit assignment keeps both the D-02 intent and the literal grep gate satisfied with no behavior change.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug/gate-correctness] Avoided false-positive on the D-02 color/font grep gate**
- **Found during:** Task 1 (Designer layout)
- **Issue:** Standard WinForms designer output sets `Button.UseVisualStyleBackColor = true` on every button by convention; this is a no-op boolean flag (already the default), but its name contains the substring `BackColor`, which would trip the plan's own acceptance-criteria grep (`grep -c "BackColor\|ForeColor\|new Font("` expected to return 0) even though no custom color is actually being set.
- **Fix:** Omitted the redundant `UseVisualStyleBackColor = true` assignments entirely (default value is already `true`, so no behavior change) on `btnBrowse`, `btnSaveSettings`, `btnDiscardChanges`.
- **Files modified:** src/RigToggle.App/SettingsForm.Designer.cs
- **Verification:** `grep -c "BackColor\|ForeColor\|new Font(" src/RigToggle.App/SettingsForm.Designer.cs` returns 0.
- **Committed in:** a182896 (Task 1 commit)

**2. [Rule 2 - Missing critical correctness] Defensive try/catch around real enumeration reads**
- **Found during:** Task 2 (picker population)
- **Issue:** The plan's acceptance criteria require the zero-enumeration path (empty list) to show empty-state copy, but did not explicitly require guarding against an *exception* from the real `WindowsDisplayAPI`/NAudio-backed calls (only `MMDeviceEnumerator.GetDevice`'s throw-vs-null behavior was flagged as uncertain in 02-RESEARCH.md Pitfall 2/Assumption A2). An unhandled exception from either enumeration call on `Form.Load` would crash the Settings dialog entirely — a much worse failure mode than the intended graceful empty-state.
- **Fix:** Wrapped `_monitorController.GetActiveMonitors()` and `_audioController.GetPlaybackDevices()` calls in `try/catch (Exception)`, degrading to an empty list (which then correctly renders the "No displays detected."/"No audio devices detected." empty-state copy and keeps Save disabled) instead of propagating.
- **Files modified:** src/RigToggle.App/SettingsForm.cs
- **Verification:** Structural review only (no .NET SDK in this sandbox — deferred to the Windows rig per the plan's stated execution boundary); the catch block is unconditionally safe (empty list is already a handled, tested-for-copy case in the same method).
- **Committed in:** 4547d48 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 grep-gate correctness, 1 missing-critical defensive guard)
**Impact on plan:** Both changes are net-positive corrections with zero scope creep — neither adds new user-facing behavior beyond what the plan already specified (the acceptance criteria gate and the empty-state UI path, respectively).

## Issues Encountered
None beyond the two auto-fixed items above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness

- `SettingsForm` is complete and ready to be instantiated from the composition root in Plan 02-05 (`new SettingsForm(monitorController, audioController, settingsStore).ShowDialog(mainForm)`).
- **Build/visual verification deferred to the Windows rig** (no .NET SDK or Windows runtime in this Linux sandbox, per the plan's stated execution boundary and this project's established Phase 1 precedent): `dotnet build src/RigToggle.App/RigToggle.App.csproj` has not been run in this session. All acceptance-criteria checks in this plan were verified structurally via `grep` against the exact patterns specified in the plan (GroupBox count, ComboBox names/DropDownStyle, DialogResult wiring, absence of custom colors/fonts, absence of concrete adapter instantiation, single `_settingsStore.Save` call site) — full compile + the plan's listed manual verification steps (pickers populate from real hardware, Save gating live, stale-device reselect warning, first-run no-warnings, Browse `.exe` filter, `settings.json` write) still need to run on the actual Windows rig.
- No blockers for Plan 02-05 (MainForm + composition root): the constructor signature `SettingsForm(IMonitorController, IAudioController, ISettingsStore)` is the exact contract Plan 02-05 needs to wire up.

## Self-Check: PASSED

- FOUND: src/RigToggle.App/SettingsForm.Designer.cs
- FOUND: src/RigToggle.App/SettingsForm.cs
- FOUND commit a182896 (Task 1)
- FOUND commit 4547d48 (Task 2)
- FOUND commit 6dcd71b (Task 3)

---
*Phase: 02-foundations-gui-shell*
*Completed: 2026-07-24*
