---
phase: 15-optional-app-audio-targets
plan: 03
subsystem: ui
tags: [winforms, settings-form, validation, csharp]

# Dependency graph
requires: []
provides:
  - Explicit Clear button (btnClearAppPath) that unsets the companion-app path, since txtAppPath is ReadOnly and had no other way to clear it (D-01/APP-04)
  - "(None — don't switch audio)" sentinel entry prepended to both audio dropdowns, persisting a null device ID when selected (D-02/AUDIO-03/AUDIO-04)
  - Relaxed SettingsForm Save gate — enabled once the monitor grid validates, regardless of audio/app unset state; still blocks on a configured-but-broken audio device or app path (D-06)
  - Reworded MainForm not-configured message referencing the monitor set only (D-05/Pitfall 4)
affects: [15-01, 15-02, 16]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "_pending* nullable field mirrors _pendingHotkeyModifiers/_pendingHotkeyKey: mutated only by explicit user actions, read directly at Save time, never derived from a Text/display property"
    - "Split Load-time seed (PopulateAppPathField) from pure re-render (RenderAppPathDisplay) so Clear/Browse/DragDrop never re-read stale persisted settings"
    - "Sentinel PickerItem(Id: null, DisplayLabel: \"(None...)\") prepended unconditionally to a picker list represents an intentional 'unset' choice, distinct from a blank SelectedIndex = -1 state"

key-files:
  created: []
  modified:
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/MainForm.cs

key-decisions:
  - "Split PopulateAppPathField (Load-only, seeds _pendingAppPath from _settings) from a new RenderAppPathDisplay (pure render off _pendingAppPath) — the plan's literal instruction to call PopulateAppPathField() from the Clear handler would have re-read the stale saved path and undone the clear"
  - "Narrowed txtAppPath (288px to 220px) and btnBrowse (78px to 70px, moved left) to fit btnClearAppPath on the same row within the existing 396px-wide pnlAppPath panel, avoiding any cascading height/Y-position changes to the rest of the dialog"

patterns-established:
  - "Optional Settings-form field UI: sentinel list entry for enum-like pickers (ComboBox), explicit Clear button for free-text/path fields that are ReadOnly"

requirements-completed: [APP-04, AUDIO-03, AUDIO-04]

# Metrics
duration: ~35min
completed: 2026-08-04
---

# Phase 15 Plan 03: Optional App & Audio Settings-UI Affordances Summary

**Settings UI gains a Clear button for the app path and a "(None — don't switch audio)" sentinel for both audio dropdowns, with Save gated on the monitor grid only — a configured-but-broken target still blocks Save.**

## Performance

- **Duration:** ~35 min
- **Tasks:** 3 completed
- **Files modified:** 3

## Accomplishments
- Added a themed, initially-disabled `btnClearAppPath` button to `SettingsForm.Designer.cs`, wired to a new `BtnClearAppPath_Click` handler, enabled only when a path is currently set
- Introduced `_pendingAppPath` (nullable, mirroring the existing `_pendingHotkey*` idiom) as the single source of truth for the persisted app path — `txtAppPath.Text` is now purely a display concern and is never read back at Save time
- Widened `PickerItem.Id` to `string?` and prepended a `(None — don't switch audio)` sentinel to both audio dropdowns unconditionally (even with zero enumerated devices), explicitly selected when no device is saved instead of leaving `SelectedIndex = -1`
- Relaxed `ValidateSettingsForm`/`BtnSaveSettings_Click`'s app-path gate to "cleanly unset OR valid," matching D-06's "broken != unset" requirement — audio's existing `SelectedItem is PickerItem` check already satisfied this since the sentinel is a real `PickerItem`
- Reworded `MainForm`'s not-configured `MessageBox` to reference the monitor-set requirement only, matching the relaxed `IsFullyConfigured`/`IsSettingsConfigured` gate landing in Plan 02

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the btnClearAppPath button to the designer** - `94b719b` (feat)
2. **Task 2: Wire _pendingAppPath, the '(None...)' sentinel, Clear handler, and relaxed validation** - `ac77766` (feat)
3. **Task 3: Reword the MainForm not-configured message to monitor-set only** - `622ffc3` (fix)

_Note: no plan-metadata commit is created in worktree mode — the orchestrator commits SUMMARY.md separately after merge._

## Files Created/Modified
- `src/RigToggle.App/SettingsForm.Designer.cs` - Declares/instantiates/registers `btnClearAppPath` (FlatStyle.Flat, starts disabled), narrows `txtAppPath`/`btnBrowse` to fit it on the same row
- `src/RigToggle.App/SettingsForm.cs` - `_pendingAppPath` field, `PickerItem.Id` widened to `string?`, audio "(None...)" sentinel, `PopulateAppPathField`/`RenderAppPathDisplay` split, `BtnClearAppPath_Click`, relaxed `ValidateSettingsForm`/`BtnSaveSettings_Click` app-path gate, `btnClearAppPath` registered in both `ThemeApplier.ThemeButton` call sites
- `src/RigToggle.App/MainForm.cs` - Reworded the "Please finish configuring Settings..." `MessageBox` string to drop the audio/app-required claim

## Decisions Made
- **Split `PopulateAppPathField` from `RenderAppPathDisplay`** (documented as a deviation below) — required so the Clear button's re-render doesn't overwrite the just-cleared pending value with the stale saved path.
- **Narrowed `txtAppPath`/`btnBrowse` rather than stacking `btnClearAppPath` on a second row** — keeps the diff local to three controls on the existing row, with no cascading Y-position changes to any other control in the 768px-tall dialog. `txtAppPath`'s new range (x=12..232) stays a strict subset of its original x=12..300 span, satisfying the plan's "do not overlap txtAppPath at x=12..300" constraint.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `PopulateAppPathField()` re-render would have undone Clear**
- **Found during:** Task 2 (wiring `BtnClearAppPath_Click`)
- **Issue:** PATTERNS.md's literal guidance was for `BtnClearAppPath_Click` to set `_pendingAppPath = null` and then call `PopulateAppPathField()` to re-render the "not configured" display. But `PopulateAppPathField()` (as specified) reads `_settings.CompanionAppPath` — which still holds the old saved path until the user clicks Save — so calling it after Clear would silently reassign `_pendingAppPath` back to the stale saved value, defeating the Clear button entirely (the exact "impossible to save while unset" bug class this plan exists to remove, one layer deeper).
- **Fix:** Split the method into `PopulateAppPathField()` (Load-time only — seeds `_pendingAppPath` from `_settings.CompanionAppPath`, then delegates to the render step) and a new `RenderAppPathDisplay()` (pure render off `_pendingAppPath` only, never touches `_settings`). `BtnClearAppPath_Click`, `BtnBrowse_Click`, and `AppPath_DragDrop` all now call/rely on `RenderAppPathDisplay`'s rendering logic or set `_pendingAppPath` directly, never re-triggering a re-read of the stale settings snapshot.
- **Files modified:** `src/RigToggle.App/SettingsForm.cs`
- **Verification:** Manual trace of the Clear → Save flow confirms `_pendingAppPath` stays `null` through the Clear→re-render→Save sequence; `dotnet build src/RigToggle.App/RigToggle.App.csproj` compiles clean (0 warnings, 0 errors).
- **Committed in:** `ac77766` (part of Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - bug fix)
**Impact on plan:** Necessary for correctness — implementing the plan's literal instruction verbatim would have shipped a Clear button that visibly clears the field but silently reverts to the old value on the next re-render/Save. No scope creep; the fix stays entirely within `SettingsForm.cs`, one method split, no new files or dependencies.

## Issues Encountered
None — `dotnet build src/RigToggle.App/RigToggle.App.csproj` succeeded with 0 warnings/0 errors after every task in this sandbox (the environment does have Windows-targeting-pack support for `net10.0-windows`/WinForms, contrary to this plan's stated fallback expectation that it might not).

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- This plan is UI-only and shares no files with Plan 01 (Core toggle-step/outcome model) or Plan 02 (ToggleService optionality + IsFullyConfigured relaxation) — no merge conflicts expected within Wave 1.
- `SettingsForm.cs`'s `NormalAudioDeviceId = audioNormalItem.Id` / `RigAudioDeviceId = audioRigItem.Id` already produce the correct nullable value once the sentinel's `Id` is `null` — no further Settings-side change needed once Plan 02's `IsFullyConfigured`/`ToggleService` optionality changes land.
- Known limitation carried over (not introduced by this plan): the audio "None" sentinel's `DisplayLabel` ("(None — don't switch audio)") is persisted verbatim into `AppSettings.NormalAudioDeviceName`/`RigAudioDeviceName` when selected, since `BtnSaveSettings_Click` unconditionally sets `*AudioDeviceName = audioNormalItem.DisplayLabel`. This is cosmetic only (the Name fields are display-only convenience fields, never read by `ToggleService`'s device-resolution logic) and was not called out as in-scope by the plan; flagging here for awareness, not treating as a defect requiring a fix in this plan.

---
*Phase: 15-optional-app-audio-targets*
*Completed: 2026-08-04*

## Self-Check: PASSED

- FOUND: src/RigToggle.App/SettingsForm.Designer.cs
- FOUND: src/RigToggle.App/SettingsForm.cs
- FOUND: src/RigToggle.App/MainForm.cs
- FOUND: .planning/phases/15-optional-app-audio-targets/15-03-SUMMARY.md
- FOUND commit: 94b719b (Task 1)
- FOUND commit: ac77766 (Task 2)
- FOUND commit: 622ffc3 (Task 3)
- FOUND commit: b373145 (SUMMARY.md)
