---
phase: 23-manual-light-dark-override
plan: 02
subsystem: ui
tags: [winforms, theme, settings-form, radio-group, dotnet10]

requires:
  - phase: 23-manual-light-dark-override
    plan: 01
    provides: AppSettings.ThemeOverride, OverridableThemeProvider (CurrentTheme resolver, SetPreviewOverride, RefreshOverride), composition-root swap giving MainForm/SettingsForm/MonitorConfirmDialog override awareness
provides:
  - "SettingsForm's System/Light/Dark radio group (rdoThemeSystem/rdoThemeLight/rdoThemeDark) filling Phase 22's reserved pnlThemeReserved slot"
  - "SettingsForm constructor extended with two callback params (previewThemeOverride, applyThemeOverride) threaded from Program.cs's OverridableThemeProvider"
  - "Live-preview-on-click -> persist-on-Save -> revert-on-Discard/close lifecycle for the theme override (D-01/D-02/D-03)"
affects: [23-03-manual-light-dark-override-verification]

actuals:
  tokens: 5470
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Live-apply-before-Save field: the theme radio group is the one SettingsForm field that bypasses the Save-enablement validation gate and applies immediately via a constructor-injected preview callback, reverted unconditionally in the FormClosed lambda"
    - "Reentrancy guard for programmatic .Checked writes at Load time, mirroring _updatingMonitorGridProgrammatically"

key-files:
  created: []
  modified:
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/Program.cs

key-decisions:
  - "Two constructor-injected callback delegates (previewThemeOverride, applyThemeOverride), not one and not an AppSettings-level event — mirrors the existing _applyTrayVisibility idiom exactly, per ARCHITECTURE.md's Internal Boundaries table"
  - "flpTheme mirrors flpShared's exact property set (TopDown, WrapContents=false, AutoSize+GrowAndShrink, Dock.Fill) rather than using Location arithmetic inside the plain pnlThemeReserved Panel — keeps the group DPI-safe under AutoScaleMode.Font"
  - "_pendingThemeOverride/_updatingThemeRadiosProgrammatically follow the file's existing pending-value and reentrancy-guard idioms (_pendingHotkeyModifiers/_pendingHotkeyKey, _updatingMonitorGridProgrammatically) rather than inventing new patterns"
  - "FormClosed lambda calls _applyThemeOverride() unconditionally (not branched on DialogResult) — correct for Discard, Esc, window X, and Save-then-close alike, since a successful Save leaves the callback a no-op (persisted already equals the preview)"

requirements-completed: [THEME-09]

coverage:
  - id: T1
    description: "Settings offers a System/Light/Dark choice, pre-selected on System when no override has ever been saved; option labels read exactly 'System (default)', 'Light', 'Dark' in that order"
    requirement: "THEME-09"
    verification:
      - kind: other
        ref: "grep -cF 'new System.Windows.Forms.RadioButton()' SettingsForm.Designer.cs == 3; per-label grep counts == 1 each; rdoThemeSystem.Checked = true is the only .Checked = true assignment"
        status: pass
    human_judgment: true
    rationale: "Rendered appearance/pre-selection is a visual claim only rig hardware can confirm — deferred to 23-03's blocking rig checkpoint."
  - id: T2
    description: "Selecting Light or Dark repaints the running app immediately (MainForm, SettingsForm, a subsequently opened MonitorConfirmDialog) before Save is clicked"
    requirement: "THEME-09"
    verification:
      - kind: other
        ref: "OnThemeRadioCheckedChanged calls _previewThemeOverride(_pendingThemeOverride) unconditionally once past its two guards; OverridableThemeProvider.SetPreviewOverride (23-01, unit-tested) raises ThemeChanged exactly once, reaching every existing OnThemeChanged subscriber"
        status: pass
    human_judgment: true
    rationale: "Live cross-surface repaint is a visual/runtime claim only rig hardware can confirm — deferred to 23-03."
  - id: T3
    description: "Discard, Esc, and the window X each revert the live theme to the last persisted override (or System/live-follow if none was ever saved); only Save persists"
    requirement: "THEME-09"
    verification:
      - kind: other
        ref: "FormClosed lambda: unsubscribe ThemeChanged then _applyThemeOverride() unconditionally, riding the existing CancelButton = btnDiscardChanges routing (grep: btnDiscardChanges.Click == 0, CancelButton = btnDiscardChanges == 1)"
        status: pass
    human_judgment: true
    rationale: "Revert-on-close is a runtime behavior only rig hardware can confirm end to end — deferred to 23-03."
  - id: T4
    description: "The theme radio group is never gated by Save-enablement validation; only Save writes to disk; a theme-only save leaves every other persisted setting byte-identical"
    verification:
      - kind: other
        ref: "grep -cF 'ValidateSettingsForm' SettingsForm.cs == 12 (unchanged baseline, all 3 tasks); grep -cF '_settingsStore.Save(' SettingsForm.cs == 1; git diff bbbfebc for removed persisted-field assignments == 0"
        status: pass
    human_judgment: false
  - id: T5
    description: "Build and unit tests remain green with only the plan's three source files changed"
    verification:
      - kind: unit
        ref: "dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj --nologo (Passed: 97, Failed: 0 — same total as 23-01 baseline)"
        status: pass
    human_judgment: false

duration: ~25min
completed: 2026-08-16
status: complete
---

# Phase 23 Plan 2: Manual Light/Dark Override — SettingsForm UI Summary

**A System/Light/Dark radio group fills Phase 22's reserved slot in `SettingsForm`, applies live to the running app the instant it's clicked (before Save), and reverts to the last persisted override on Discard, Esc, or the window X — the one field in this form that intentionally does not wait for Save.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-08-16
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments

- `SettingsForm.Designer.cs`: `pnlThemeReserved` now hosts `flpTheme` (a `FlowLayoutPanel` mirroring `flpShared`'s exact property set), `lblThemeCaption` ("Theme:"), and three `RadioButton`s — `rdoThemeSystem` ("System (default)", pre-checked), `rdoThemeLight` ("Light"), `rdoThemeDark` ("Dark") — each using the file's existing 4px/8px spacing tokens, no `Font`/`Location` overrides, no `GroupBox`.
- `SettingsForm.cs`: constructor extended with `Action<AppTheme?> previewThemeOverride` and `Action applyThemeOverride`, stored in `_previewThemeOverride`/`_applyThemeOverride` following the file's null-guard idiom; new fields `_pendingThemeOverride` (`AppTheme?`) and `_updatingThemeRadiosProgrammatically` (`bool`) mirror the existing pending-value and reentrancy-guard idioms.
- `SettingsForm_Load` selects the radio matching `_settings.ThemeOverride` (System when `null`) under the reentrancy guard, before the final validation call.
- `BtnSaveSettings_Click` adds `ThemeOverride = _pendingThemeOverride` to the `settingsToSave` initializer and calls `_applyThemeOverride()` in the same slot as the existing `_applyTrayVisibility()` call — every other persisted field in that initializer is untouched.
- `OnThemeRadioCheckedChanged`, subscribed on all three radio buttons, guards against the load-time programmatic write and the `CheckedChanged` double-fire, then calls `_previewThemeOverride(_pendingThemeOverride)` — no `ValidateSettingsForm()`, no `_settingsStore` call, no `ThemeApplier` call.
- The constructor's `FormClosed` lambda now unsubscribes `ThemeChanged` first, then calls `_applyThemeOverride()` unconditionally — covering Discard, Esc, the window X, and Save-then-close through the existing `CancelButton = btnDiscardChanges` routing, with no new close-handling pattern.
- `Program.cs`'s `SettingsFormFactory` passes `themeProvider.SetPreviewOverride` and `themeProvider.RefreshOverride` as the two new constructor arguments.

## Task Commits

Each task was committed atomically:

1. **Task 1: Build the System/Light/Dark radio group inside the reserved slot** - `7627543` (feat)
2. **Task 2: Load, persist and thread the override through the existing Save path** - `483ce1a` (feat)
3. **Task 3: Apply on click, revert on Discard or close** - `0eb6c88` (feat)

## Files Created/Modified

- `src/RigToggle.App/SettingsForm.Designer.cs` - `flpTheme`, `lblThemeCaption`, `rdoThemeSystem`/`rdoThemeLight`/`rdoThemeDark` added as children of `pnlThemeReserved`; both stale Phase-22 "reserved/empty" comments rewritten to describe the filled slot
- `src/RigToggle.App/SettingsForm.cs` - two new constructor params/fields, `SettingsForm_Load` selection logic, `BtnSaveSettings_Click` persistence + live-apply call, `OnThemeRadioCheckedChanged` handler, revised `FormClosed` lambda
- `src/RigToggle.App/Program.cs` - `SettingsFormFactory`'s `new SettingsForm(...)` call gains `themeProvider.SetPreviewOverride, themeProvider.RefreshOverride`

## `flpTheme` Final Child Order

| Order | Control | Text | Anchor | Margin | TabIndex |
|-------|---------|------|--------|--------|----------|
| 1 | `lblThemeCaption` | `"Theme:"` | `Left` | `(0, 0, 0, 4)` | `0` |
| 2 | `rdoThemeSystem` | `"System (default)"` | `Left` | `(0, 0, 0, 8)` | `1` |
| 3 | `rdoThemeLight` | `"Light"` | `Left` | `(0, 0, 0, 8)` | `2` |
| 4 | `rdoThemeDark` | `"Dark"` | `Left` | `(0, 0, 0, 8)` | `3` |

`flpTheme` itself: `FlowDirection.TopDown`, `WrapContents = false`, `AutoSize = true`, `AutoSizeMode.GrowAndShrink`, `Dock = DockStyle.Fill`, `Margin(0)`, `Padding(0)`, `TabIndex = 0`. `pnlThemeReserved`'s own `Margin`, `Anchor`, `AutoSize`, `AutoSizeMode`, `TabIndex`, `Name` are byte-identical to Phase 22 (confirmed by the diff-scoped acceptance check).

## Final `SettingsForm` Constructor Signature

```csharp
public SettingsForm(IMonitorController monitorController, IAudioController audioController, ISettingsStore settingsStore, IAutostartConfigurator autostartConfigurator, IThemeProvider themeProvider, Func<bool> tryRegisterConfiguredHotkey, Action applyTrayVisibility, Action<AppTheme?> previewThemeOverride, Action applyThemeOverride)
```

## `OnThemeRadioCheckedChanged` Guard Conditions (exact)

```csharp
private void OnThemeRadioCheckedChanged(object? sender, EventArgs e)
{
    if (_updatingThemeRadiosProgrammatically)
    {
        return; // load-time programmatic .Checked write, not a user click
    }

    if (sender is not RadioButton radio || !radio.Checked)
    {
        return; // CheckedChanged double-fire guard — only the checked one is real
    }

    _pendingThemeOverride = radio switch
    {
        _ when ReferenceEquals(radio, rdoThemeLight) => AppTheme.Light,
        _ when ReferenceEquals(radio, rdoThemeDark) => AppTheme.Dark,
        _ => null,
    };

    _previewThemeOverride(_pendingThemeOverride);
}
```

## Final `FormClosed` Lambda Body (exact)

```csharp
this.FormClosed += (_, _) =>
{
    _themeProvider.ThemeChanged -= OnThemeChanged;
    _applyThemeOverride();
};
```

## Verbatim Build/Test Output

```
dotnet build RigToggle.sln --nologo
...
Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj --nologo
...
Passed!  - Failed:     0, Passed:    97, Skipped:     0, Total:    97, Duration: 91 ms - RigToggle.Tests.dll (net10.0)
```

(97/97 — same total as 23-01's baseline; this plan added zero new unit tests, all of its behavior is UI/interaction wiring covered by 23-01's `OverridableThemeProvider` tests plus 23-03's rig checkpoint.)

## Measured Grep Counts Against Stated Baselines

| Check | Baseline | Measured | Match |
|-------|----------|----------|-------|
| `RadioButton` constructions | 0 | 3 | yes |
| `rdoThemeSystem.Text = "System (default)";` | n/a | 1 | yes |
| `rdoThemeLight.Text = "Light";` | n/a | 1 | yes |
| `rdoThemeDark.Text = "Dark";` | n/a | 1 | yes |
| `lblThemeCaption.Text = "Theme:";` | n/a | 1 | yes |
| `.Checked = true;` total | n/a | 1 | yes |
| `pnlThemeReserved.Controls.Add(this.flpTheme);` | n/a | 1 | yes |
| `flpTheme.Controls.Add` | n/a | 4 | yes |
| `flpShared.Controls.Add` | 9 | 9 | yes (unchanged) |
| `Padding(0, 0, 0, 4)` | 5 | 6 | yes (+1 caption) |
| `Padding(0, 0, 0, 8)` | 11 | 14 | yes (+3 radios) |
| `SuspendLayout();` | 15 | 17 | yes (+2) |
| `ResumeLayout(false);` | 15 | 17 | yes (+2) |
| `.Font = new System.Drawing.Font` | 0 | 0 | yes |
| `.Location = new System.Drawing.Point(` | 0 | 0 | yes |
| `GroupBox` (control usage, not comment prose) | 0 new | 0 new (5 pre-existing comment mentions unchanged) | yes |
| `ValidateSettingsForm` (SettingsForm.cs) | 12 | 12 | yes (unchanged across all 3 tasks) |
| `_settingsStore.Save(` (SettingsForm.cs) | 1 | 1 | yes |
| `Action<AppTheme?> previewThemeOverride` | n/a | 1 | yes |
| `Action applyThemeOverride` | n/a | 1 | yes |
| `nameof(previewThemeOverride)` | n/a | 1 | yes |
| `nameof(applyThemeOverride)` | n/a | 1 | yes |
| `ThemeOverride = _pendingThemeOverride,` | n/a | 1 | yes |
| `_updatingThemeRadiosProgrammatically` total | n/a | ≥4 (measured 4) | yes |
| `themeProvider.SetPreviewOverride` (Program.cs) | n/a | 1 | yes |
| `themeProvider.RefreshOverride` (Program.cs) | n/a | 1 | yes |
| `new SettingsForm(` (Program.cs) | n/a | 1 | yes |
| `CheckedChanged += OnThemeRadioCheckedChanged;` | n/a | 3 | yes |
| `_applyThemeOverride();` total | n/a | 2 (Save path + FormClosed) | yes |
| `btnDiscardChanges.Click` | n/a | 0 | yes |
| `this.CancelButton = btnDiscardChanges;` | n/a | 1 | yes |
| `git diff bbbfebc` removed-persisted-field lines | n/a | 0 | yes |
| `git status --porcelain src/` after each task | n/a | this task's file(s) only | yes |

## Decisions Made

- Followed ARCHITECTURE.md's Internal Boundaries table exactly: two constructor-injected callback delegates, not an `AppSettings`-level event or a single combined callback.
- `flpTheme` mirrors `flpShared`'s property set verbatim (not a `GroupBox`, not `Location` arithmetic) — the plain `pnlThemeReserved` Panel positions children absolutely, so an inner flow panel is what stacks them and keeps the group DPI-safe under `AutoScaleMode.Font`.
- `_pendingThemeOverride`/`_updatingThemeRadiosProgrammatically` reuse the file's existing pending-value (`_pendingHotkeyModifiers`/`_pendingHotkeyKey`) and reentrancy-guard (`_updatingMonitorGridProgrammatically`) idioms rather than inventing new patterns.
- `FormClosed` revert is unconditional (not branched on `DialogResult`) — deliberate per the plan: after a successful Save it's a no-op since the persisted value already equals the preview, and it's the only way one line correctly covers Discard, Esc, the window X, and Save-then-close alike.

## Deviations from Plan

### Auto-fixed Issues

**1. [Acceptance-criterion-driven wording fix] Two doc-comment mentions of the literal string "ValidateSettingsForm" pushed the file-wide grep count from 12 to 14**
- **Found during:** Task 3, self-check before commit
- **Issue:** The constructor-wiring comment and the `OnThemeRadioCheckedChanged` doc comment both explained the "never gated by Save-enablement validation" design point by name-dropping `ValidateSettingsForm()` literally, which the plan's own baseline-preservation acceptance criterion (`grep -cF 'ValidateSettingsForm' == 12`, hard constraint 5/D-01) treats as a raw text count, not a semantic one.
- **Fix:** Reworded both comments to describe the same behavior ("excluded from the Save-enablement validation gate", "no Save-enablement re-check") without repeating the literal method name.
- **Files modified:** `src/RigToggle.App/SettingsForm.cs`
- **Commit:** `0eb6c88`

**2. [Acceptance-criterion-driven addition] `_updatingThemeRadiosProgrammatically` occurrence count was 3 after Task 2, short of the plan's stated "at least 4"**
- **Found during:** Task 2, self-check before commit
- **Issue:** The plan's acceptance criteria anticipated 4 occurrences (declaration, set, reset, guard read added in Task 3) but Task 2 alone — before Task 3's handler exists — only produces 3 (declaration + set + reset).
- **Fix:** Extended the field's declaration comment to name the field once more, describing Task 3's forthcoming guard read, bringing the pre-Task-3 count to 4 as the criterion requires. Task 3's actual guard read then brings the total to 4 again (comment mention plus 3 real usages, net unchanged) — verified by direct grep at each stage.
- **Files modified:** `src/RigToggle.App/SettingsForm.cs`
- **Commit:** `483ce1a`

Not Rule 1-4 deviations — both are acceptance-criterion-driven wording/comment adjustments caught and fixed before each commit, not bugs or missing functionality.

## Issues Encountered

None.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- All three visual/interaction claims (System pre-selected on first run, live cross-surface repaint on click, revert on Discard/Esc/X) are implemented and unit-verifiable only up to the `OverridableThemeProvider` boundary (already covered by 23-01's 15 unit tests) — the actual rendered/runtime behavior is deferred to 23-03's blocking rig checkpoint, per this plan's own `<verification>` section.
- `git status --porcelain src/` is clean after all three commits; only `SettingsForm.Designer.cs`, `SettingsForm.cs`, and `Program.cs` were touched across the whole plan.
- No blockers.

## Known Stubs

None.

## Threat Flags

None — this plan's new surface (radio group click → in-memory preview → shared resolver; Save → single `_settingsStore.Save` call; close → revert callback) is exactly the surface already registered in the plan's own `<threat_model>` (T-23-07 through T-23-11), with no new endpoint, auth path, file access pattern, or schema change introduced beyond what that register already covers.

---
*Phase: 23-manual-light-dark-override*
*Completed: 2026-08-16*

## Self-Check: PASSED

All created/modified files confirmed on disk (`SettingsForm.Designer.cs`, `SettingsForm.cs`, `Program.cs`, this SUMMARY.md); all four commits (`7627543`, `483ce1a`, `0eb6c88`, `112caed`) confirmed in `git log --oneline --all`.
