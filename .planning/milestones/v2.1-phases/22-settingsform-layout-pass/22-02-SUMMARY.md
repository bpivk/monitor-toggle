---
phase: 22-settingsform-layout-pass
plan: 02
subsystem: ui
tags: [winforms, tablelayoutpanel, flowlayoutpanel, settingsform, designer, dotnet10]

# Dependency graph
requires:
  - phase: 22-settingsform-layout-pass (plan 01)
    provides: tlpRoot/tlpModeColumns scaffold with a Percent-100F mode-column row and two reserved AutoSize rows for this plan to fill
provides:
  - pnlSharedSection/flpShared (D-02): full-width shared settings section holding app path, hotkey, debug-logging checkbox, tray/autostart checkboxes as one flat top-down stack
  - tlpAppPath: internal 3-column table for the target-app box, with drag-drop coverage widened from panel-only to the whole table (T-12-07)
  - tlpHotkey: 2-column caption+field row for the hotkey capture control
  - pnlThemeReserved (D-04): named, empty, zero-size Phase 23 insertion point
  - flpButtons: right-aligned Save/Discard row with growth-only MinimumSize
  - Form.AutoSize/AutoSizeMode/FormBorderStyle=Sizable (D-05/D-06): content-driven, edge-resizable window replacing the fixed 828x768 FixedDialog
affects: [23-manual-light-dark-override (consumes pnlThemeReserved as its insertion point)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "FlowLayoutPanel with FlowDirection.TopDown + AutoSize=true (flpShared) and FlowDirection.RightToLeft + AutoSize=true (flpButtons) -- first AutoSize=true FlowLayoutPanel usage in this codebase (MainForm's tileStrip deliberately avoided it)"
    - "Form.AutoSize=true + AutoSizeMode.GrowAndShrink replacing a fixed ClientSize -- first AutoSize=true Form in this codebase"
    - "Reserved-slot pattern: a childless AutoSize Panel with an explicit Size=(0,0) and a comment naming the consuming phase, so a future phase can add children with zero reflow of siblings"

key-files:
  created: []
  modified:
    - src/RigToggle.App/SettingsForm.Designer.cs

key-decisions:
  - "pnlThemeReserved deliberately has no SuspendLayout/ResumeLayout pair -- it has no children by design (hard constraint 6), so the bracket would be a no-op; Task 2's Suspend/Resume audit asserts exactly 15 pairs, which only holds if this one is excluded"
  - "tlpAppPath and pnlAppPath both carry AllowDrop=true plus their own DragEnter/DragDrop subscriptions (T-12-07) -- once tlpAppPath is docked to fill pnlAppPath's client area, the panel's own surface stops being hit-testable for drops, so without the table's own wiring the drop target would silently shrink to the text box"
  - "txtHotkey keeps Anchor=Left (not Left|Right) inside tlpHotkey, preserving its fixed 200x23 footprint rather than stretching across the row -- deviating from most other shared-section children specifically per plan instruction"
  - "flpButtons adds btnDiscardChanges before btnSaveSettings so that RightToLeft flow direction reproduces today's left-to-right Save-then-Discard reading order (Save at the old x=180, Discard at the old x=298)"

patterns-established:
  - "Shared-section child shape: FlowLayoutPanel(TopDown) with each child's own explicit TabIndex kept in sync with its Controls.Add position (Pitfall 5 tab-order rule)"
  - "Button-row shape: FlowLayoutPanel(RightToLeft) + AutoSize/MinimumSize per button, replacing fixed pixel Size so button text can never truncate at any DPI scale"

requirements-completed: [SETTINGS-01, SETTINGS-02]

# Metrics
duration: 15min
completed: 2026-08-12
---

# Phase 22 Plan 02: Shared Settings Section, Button Row & Form-Level Sizing Summary

**Completed SettingsForm's TableLayoutPanel/FlowLayoutPanel migration: built the flat, full-width shared section (D-02) with a reserved Phase 23 theme slot (D-04), right-aligned the Save/Discard buttons in their own growth-only row, and switched the form from a fixed 828x768 FixedDialog to a content-driven, edge-resizable Sizable window (D-05/D-06).**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-08-12T07:06:02Z (approx, base commit)
- **Completed:** 2026-08-12T07:16:35Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments

- Built `pnlSharedSection`/`flpShared` (D-02): a single flat top-down stack holding `pnlAppPath`, `tlpHotkey`, `lblHotkeyWarning`, all four tray/autostart checkboxes, `lblAutostartWarning`, and `pnlThemeReserved` -- no sub-grouping boxes, matching every grep acceptance criterion (9 `flpShared.Controls.Add` calls, none docked)
- Rebuilt `pnlAppPath`'s internals as `tlpAppPath`, a 3-column table (`Percent 100F`/`AutoSize`/`AutoSize`); widened drag-drop coverage so `AllowDrop`+`DragEnter`+`DragDrop` now live on `pnlAppPath`, `txtAppPath`, AND `tlpAppPath` (3 occurrences each, T-12-07)
- Added `tlpHotkey`, a 2-column caption+field row, preserving `txtHotkey`'s load-bearing `ReadOnly`/`TabStop`/`Cursor`/`Size(200,23)` properties verbatim
- Added `pnlThemeReserved` (D-04/THEME-09): named, empty, zero-size (`Size(0,0)`) insertion point for Phase 23's radio group, with no `Text`/`BorderStyle`/`Controls.Add` and a comment naming both "Phase 23" and "THEME-09" so a future planner can grep for either
- Added `flpButtons`: right-aligned Save/Discard row (`FlowDirection.RightToLeft`, Discard added first so Save still reads left of Discard), both buttons `AutoSize`+`MinimumSize` so text can never truncate
- Removed `Form.ClientSize` entirely; added `AutoSize=true`/`AutoSizeMode.GrowAndShrink`; changed `FormBorderStyle` from `FixedDialog` to `Sizable`; `MaximizeBox`/`MinimizeBox` both stay `false` (the latter is a functional-correctness requirement per 22-RESEARCH.md Pitfall 3, not a style default)
- Zero `Location` assignments and zero `ClientSize` references remain anywhere in the file; `this.Controls.Add(this.tlpRoot);` is the form's only direct child
- Build and test suite held at their measured baselines throughout (`0 Error(s)`, `4 Warning(s)`; `82/82` tests passing)

## Task Commits

Each task was committed atomically:

1. **Task 1: Build the shared full-width settings section and reserve Phase 23's slot** - `39bdd92` (feat)
2. **Task 2: Right-aligned button row, content-driven form sizing, and resizable window chrome** - `63c0ef1` (feat)

_Both tasks touched only `src/RigToggle.App/SettingsForm.Designer.cs`; `SettingsForm.cs` and `ThemeApplier.cs` remain byte-identical to the phase baseline commit (`0c1234f`), confirmed via `git diff --stat` after each task._

## Files Created/Modified

- `src/RigToggle.App/SettingsForm.Designer.cs` - Added `pnlSharedSection`/`flpShared`/`tlpAppPath`/`tlpHotkey`/`pnlThemeReserved`/`flpButtons`; migrated `pnlAppPath` and the checkbox/hotkey stack off `Location`/`Size` into the new shared-section flow; migrated the Save/Discard buttons into `flpButtons`; removed `Form.ClientSize`, added `Form.AutoSize`/`AutoSizeMode`, changed `FormBorderStyle` to `Sizable`

## flpShared Final Child Order (Tab order == Add order)

| # / TabIndex | Control | Anchor | Margin |
|---|---------|--------|--------|
| 0 | `pnlAppPath` | `Top \| Left \| Right` | `(0, 0, 0, 8)` |
| 1 | `tlpHotkey` | `Top \| Left \| Right` | `(0, 0, 0, 8)` |
| 2 | `lblHotkeyWarning` | `Top \| Left \| Right` | `(0, 0, 0, 8)` |
| 3 | `chkEnableDebugLogging` | `Left` | `(0, 0, 0, 8)` |
| 4 | `chkCloseMinimizesToTray` | `Left` | `(0, 0, 0, 8)` |
| 5 | `chkMinimizeToTray` | `Left` | `(0, 0, 0, 8)` |
| 6 | `chkStartWithWindows` | `Left` | `(0, 0, 20, 8)` (20 = ErrorProvider clearance) |
| 7 | `lblAutostartWarning` | `Top \| Left \| Right` | `(0, 0, 0, 8)` |
| 8 | `pnlThemeReserved` | `Top \| Left \| Right` | `(0, 0, 0, 0)` |

## tlpAppPath Cell Map

| Cell | Control | Notes |
|------|---------|-------|
| (0,0), ColumnSpan 3 | `lblAppPathCaption` | `AutoSize=true`, `Anchor=Left` |
| (0,1) | `txtAppPath` | `Anchor=Left\|Right`, column 0 is `Percent 100F` |
| (1,1) | `btnBrowse` | `AutoSize=true`, `MinimumSize(70,25)` |
| (2,1) | `btnClearAppPath` | `AutoSize=true`, `MinimumSize(70,25)` |
| (0,2), ColumnSpan 3 | `lblAppWarning` | `Dock=Fill`, `MinimumSize(0,20)` |

`ColumnStyles`: `[Percent 100F, AutoSize, AutoSize]`. `RowStyles`: `[AutoSize, AutoSize, AutoSize]`.

## Form Property Diff (D-05/D-06)

- **Removed:** `this.ClientSize = new System.Drawing.Size(828, 768);`
- **Added:** `this.AutoSize = true;` and `this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;`
- **Changed:** `this.FormBorderStyle` from `FixedDialog` to `Sizable`
- **Unchanged:** `AutoScaleDimensions(7F,15F)`, `AutoScaleMode.Font`, `MaximizeBox=false`, `MinimizeBox=false`, `ShowInTaskbar=false`, `StartPosition=CenterParent`, `Text`, `Name`

## Suspend/Resume Audit

15 `SuspendLayout()`/`ResumeLayout(false)` pairs, balanced: the 11 containers this phase's two plans introduced (`tlpRoot`, `tlpModeColumns`, `tlpNormalColumn`, `tlpAudioNormal`, `tlpRigColumn`, `tlpAudioRig`, `pnlSharedSection`, `flpShared`, `tlpAppPath`, `tlpHotkey`, `flpButtons`) plus the 3 surviving pre-existing panels (`pnlMonitor`, `pnlMonitorNormal`, `pnlAppPath`) plus the form itself. `pnlThemeReserved` is deliberately excluded (0 children to batch) -- `grep -c "this.pnlThemeReserved.SuspendLayout();"` is `0`.

## Verbatim Build/Test Output

**Build** (`dotnet build RigToggle.sln -p:EnableWindowsTargeting=true`, final state after both tasks):
```
Build succeeded.
    4 Warning(s)
    0 Error(s)
```
(All 4 warnings are the pre-existing `xUnit1031` warnings in `ToggleOrchestratorTests.cs`, unrelated to this plan -- matches the phase baseline exactly.)

**Test** (`dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true`, final state after both tasks):
```
Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82, Duration: 80 ms
```

## Decisions Made

- `pnlThemeReserved` deliberately gets no `SuspendLayout`/`ResumeLayout` pair -- it has zero children by design (hard constraint 6), so the bracket would be a no-op; this was required to keep Task 2's audited pair count at exactly 15, not 16
- `tlpAppPath` carries its own `AllowDrop=true` plus `DragEnter`/`DragDrop` subscriptions in addition to `pnlAppPath`'s existing ones -- once the table is docked to fill the panel's client area, the panel's own surface stops being hit-testable for drops, so all three levels (`pnlAppPath`, `txtAppPath`, `tlpAppPath`) needed the wiring to keep the whole box a drop target (T-12-07)
- `txtHotkey` keeps `Anchor=Left` (not `Left|Right`) inside `tlpHotkey`, per plan instruction, so the hotkey field stays a fixed 200x23 display rather than stretching to the row's full width
- `flpButtons` adds `btnDiscardChanges` before `btnSaveSettings` so that `FlowDirection.RightToLeft` reproduces today's left-to-right Save-then-Discard reading order

## Deviations from Plan

### Auto-fixed Issues

None -- both tasks' functional acceptance criteria (build/test baselines, control property changes, Controls.Add graph, Suspend/Resume balance, drag-drop wiring, `git diff --stat` against `SettingsForm.cs`/`ThemeApplier.cs`) passed on the first attempt.

### Acceptance-Criteria Discrepancy (documented, not auto-fixed)

**1. `grep -c "FlatStyle.Flat"` outputs `6`, not the plan's stated `4`**
- **Found during:** Task 2 acceptance-criteria verification
- **Issue:** The plan's acceptance criterion expected exactly 4 occurrences of the literal string `FlatStyle.Flat` (one per themed button: `btnBrowse`, `btnClearAppPath`, `btnSaveSettings`, `btnDiscardChanges`). The actual, and pre-existing baseline, count is 6.
- **Root cause:** Verified directly against the phase baseline commit (`git show 0c1234f:src/RigToggle.App/SettingsForm.Designer.cs | grep -c "FlatStyle.Flat"` also returns `6`) -- `btnBrowse` and `btnClearAppPath` each carry a multi-line explanatory comment ("`FlatStyle.Flat, not .System --`...") documenting the `dotnet/winforms#13897` workaround, and that comment text itself contains the literal substring `FlatStyle.Flat`. This is not something introduced by this plan; the codebase has always had 6 matching lines (4 code assignments + 2 comment mentions), not 4.
- **Resolution:** No fix applied. The comment is explicitly load-bearing per this plan's own hard constraints ("Text, DialogResult, FlatStyle.Flat, and Click wiring are untouched" and 22-PATTERNS.md's explicit instruction to preserve the `dotnet/winforms#13897` comment) -- deleting or rewording it to force the grep count down to 4 would violate that requirement for no functional benefit. All 4 buttons still carry exactly one `FlatStyle.Flat` property assignment each, which is the criterion's actual intent.
- **Files affected:** None (no code change) -- `src/RigToggle.App/SettingsForm.Designer.cs` verified unchanged in this respect relative to baseline behavior.
- **Impact:** None on functionality or the plan's underlying intent (every themed button is still `FlatStyle.Flat`). Flagging here for verifier awareness so this specific grep line in the plan is corrected before it's reused for a future plan.

---

**Total deviations:** 0 auto-fixed; 1 documented acceptance-criterion discrepancy (pre-existing baseline miscount, not a defect in this plan's execution).
**Impact on plan:** No scope creep, no functional gaps. Every other Task 1/Task 2 acceptance criterion (grep counts, build/test baselines, `git diff --stat`) passed exactly as specified.

## Issues Encountered

None. No auth gates, no blocking issues, no architectural questions arose during execution.

## User Setup Required

None -- no external service configuration required. No GUI verification was possible in this build environment (headless Linux container); all layout/rendering claims remain deferred to Plan 03's rig checkpoint, per the plan's own `<verification>` section.

## Next Phase Readiness

- `SettingsForm.Designer.cs`'s TableLayoutPanel/FlowLayoutPanel migration is now complete end to end: zero `Location` assignments, zero `ClientSize`, one `Controls.Add` on the form. Green build, passing 82/82 test suite.
- `pnlThemeReserved` is in place and ready for Phase 23 to add its System/Light/Dark radio group as children with no reflow of anything above it.
- Rig-verification of the full layout (DPI scaling at 100%/125%/150%, tab order, live theme-flip, overlap/crowding, manual-resize interaction, MinimizeBox-absence confirmation, drag-drop on the widened target-app box) remains deferred to Plan 03 as designed -- no visual or DPI claim in this summary is independently verified, only source-level/grep-level and build/test-level.
- No blockers.

---
*Phase: 22-settingsform-layout-pass*
*Completed: 2026-08-12*
