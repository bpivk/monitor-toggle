---
phase: 22-settingsform-layout-pass
plan: 03
subsystem: ui
tags: [winforms, tablelayoutpanel, settingsform, verification, static-audit]

# Dependency graph
requires:
  - phase: 22-settingsform-layout-pass (plan 01)
    provides: tlpRoot/tlpModeColumns mode-column scaffold
  - phase: 22-settingsform-layout-pass (plan 02)
    provides: pnlSharedSection/flpShared, flpButtons, Form.AutoSize/Sizable
provides:
  - Full regression gate (build/test) confirmation at phase-close baseline
  - Five static audits proving pixel-positioning absence, control conservation, load-bearing-property preservation, grid/drag-drop wiring integrity, and one-file blast radius
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created:
    - .planning/phases/22-settingsform-layout-pass/22-03-SUMMARY.md
  modified: []

key-decisions:
  - "Task 2 (rig-hardware DPI verification checkpoint) is a blocking human-verify checkpoint that cannot be executed in this Linux sandbox -- no Windows GUI, no DWM, no way to run the published exe or observe AutoScaleMode.Font rescaling. Execution stops here per hard constraint 4; no rig result is fabricated, inferred, or assumed."
  - "User's real-hardware rig report returned two FAILs (Check 1: monitor grid + audio picker missing from both mode columns; Check 3: manual resize preview flickers, no actual resize) before completing the remaining 15 checks. SETTINGS-01 and SETTINGS-02 are both recorded FAIL. Phase 22 is not complete; a gap-closure plan is required before a fresh rig-verification pass can be attempted. No source change made in this plan (hard constraint 1)."

patterns-established: []

requirements-completed: []

# Metrics
duration: 20min (Task 1) + rig-report recording (Task 2)
completed: 2026-08-15
---

# Phase 22 Plan 03: Full Regression Gate & Five Static Audits Summary

**Task 1 (build/test regression gate plus five static audits of pixel-positioning absence, control conservation, load-bearing-property preservation, grid/drag-drop wiring, and one-file blast radius) is complete and green. Task 2 (blocking rig-hardware DPI verification) is also complete: the user tested the published binary on real Windows hardware and reported two FAILs (Check 1 — monitor grid and audio picker missing from both mode columns; Check 3 — manual window resize does not work) before stopping the remaining 15 checks as not meaningfully evaluable against a broken 100%-scale layout. Both Phase 22 success criteria (SETTINGS-01, SETTINGS-02) are FAIL. Phase 22 is NOT complete and requires a gap-closure plan.**

## Performance

- **Duration:** ~20 min (Task 1 only)
- **Started:** 2026-08-14 (session start)
- **Completed (Task 1):** 2026-08-14
- **Tasks:** 1 of 2 (Task 2 is a blocking checkpoint, not yet executed)
- **Files modified:** 0 (verification-only plan, no source changes)

## Task 1: Full Regression Gate and Five Static Audits

### Regression Gate

**Build** (`dotnet build RigToggle.sln -p:EnableWindowsTargeting=true`):
```
Build succeeded.
    4 Warning(s)
    0 Error(s)
```
All 4 warnings are the pre-existing `xUnit1031` warnings in `ToggleOrchestratorTests.cs` (lines 131, 157, 190, 292) — unrelated to this phase, matches the stated baseline exactly.

**Test** (`dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true`):
```
Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82, Duration: 85 ms
```
Matches the stated baseline exactly (`Failed: 0, Total: 82`).

### Audit 1 — No Pixel Positioning Survives

| Check | Command | Result |
|---|---|---|
| `.Location = new System.Drawing.Point(` count | `grep -c` | `0` (PASS) |
| `ClientSize` count | `grep -c` | `0` (PASS) |
| `SizeType.Absolute` count | `grep -cE` | `0` (PASS) |
| Top-level `this.Controls.Add(` count | `grep -c "^            this.Controls.Add("` | `1`, targets `tlpRoot` (PASS) |
| Plain `.Size = new System.Drawing.Size(` count | `grep -cE` | `2` (PASS) |

**`Size(` line classification (18 total occurrences of `new System.Drawing.Size(` in the file):**

| Line | Control | Value | Classification |
|---|---|---|---|
| 216 | `dgvMonitors` | `(0, 120)` | `MinimumSize` floor (grid height) |
| 253 | `lblMonitorExplain` | `(0, 50)` | `MinimumSize` floor |
| 264 | `lblMonitorWarning` | `(0, 20)` | `MinimumSize` floor |
| 309 | `lblAudioRigWarning` | `(0, 20)` | `MinimumSize` floor |
| 400 | `dgvMonitorsNormal` | `(0, 120)` | `MinimumSize` floor (grid height) |
| 437 | `lblMonitorNormalExplain` | `(0, 50)` | `MinimumSize` floor |
| 448 | `lblMonitorNormalWarning` | `(0, 20)` | `MinimumSize` floor |
| 493 | `lblAudioNormalWarning` | `(0, 20)` | `MinimumSize` floor |
| 580 | `btnBrowse` | `(70, 25)` | `MinimumSize` floor |
| 605 | `btnClearAppPath` | `(70, 25)` | `MinimumSize` floor |
| 622 | `lblAppWarning` | `(0, 20)` | `MinimumSize` floor |
| **667** | **`txtHotkey`** | **`(200, 23)`** | **Named exception 1 (plain `.Size =`)** |
| 678 | `lblHotkeyWarning` | `(0, 36)` | `MinimumSize` floor |
| 730 | `lblAutostartWarning` | `(0, 20)` | `MinimumSize` floor |
| **752** | **`pnlThemeReserved`** | **`(0, 0)`** | **Named exception 2 (plain `.Size =`)** |
| 825 | `btnDiscardChanges` | `(110, 32)` | `MinimumSize` floor |
| 840 | `btnSaveSettings` | `(110, 32)` | `MinimumSize` floor |

No violations found. The two plain `.Size =` lines are exactly the two named exceptions (`txtHotkey` at `(200, 23)`, `pnlThemeReserved` at `(0, 0)`); every other `Size(` occurrence is a `MinimumSize` floor.

### Audit 2 — Control Inventory Conservation

All 32 inventory controls confirmed present exactly once each (`Controls.Add(this.<name>)` or `Controls.Add(this.<name>, col, row)` overload). `pnlAudioDevices` / `lblAudioDevicesCaption` confirmed absent from every file under `src/` (`grep -rc` returns `0` matches across the tree).

**32-row control → parent table:**

| Control | Parent |
|---|---|
| `pnlMonitor` | `tlpModeColumns` (cell 1,0) |
| `lblMonitorCaption` | `tlpRigColumn` (0,0) |
| `lblMonitorExplain` | `tlpRigColumn` (0,1) |
| `dgvMonitors` | `tlpRigColumn` (0,2) |
| `lblMonitorWarning` | `tlpRigColumn` (0,3) |
| `pnlMonitorNormal` | `tlpModeColumns` (cell 0,0) |
| `lblMonitorNormalCaption` | `tlpNormalColumn` (0,0) |
| `lblMonitorNormalExplain` | `tlpNormalColumn` (0,1) |
| `dgvMonitorsNormal` | `tlpNormalColumn` (0,2) |
| `lblMonitorNormalWarning` | `tlpNormalColumn` (0,3) |
| `lblAudioNormalCaption` | `tlpAudioNormal` (0,0) |
| `cboAudioNormal` | `tlpAudioNormal` (1,0) |
| `lblAudioNormalWarning` | `tlpNormalColumn` (0,5) |
| `lblAudioRigCaption` | `tlpAudioRig` (0,0) |
| `cboAudioRig` | `tlpAudioRig` (1,0) |
| `lblAudioRigWarning` | `tlpRigColumn` (0,5) |
| `pnlAppPath` | `flpShared` |
| `lblAppPathCaption` | `tlpAppPath` (0,0, span 3) |
| `txtAppPath` | `tlpAppPath` (0,1) |
| `btnBrowse` | `tlpAppPath` (1,1) |
| `btnClearAppPath` | `tlpAppPath` (2,1) |
| `lblAppWarning` | `tlpAppPath` (0,2, span 3) |
| `chkEnableDebugLogging` | `flpShared` |
| `lblHotkeyCaption` | `tlpHotkey` (0,0) |
| `txtHotkey` | `tlpHotkey` (1,0) |
| `lblHotkeyWarning` | `flpShared` |
| `chkCloseMinimizesToTray` | `flpShared` |
| `chkMinimizeToTray` | `flpShared` |
| `chkStartWithWindows` | `flpShared` |
| `lblAutostartWarning` | `flpShared` |
| `btnSaveSettings` | `flpButtons` |
| `btnDiscardChanges` | `flpButtons` |

(Additionally, `tlpAudioNormal`/`tlpAudioRig` are themselves children of `tlpNormalColumn`/`tlpRigColumn` at row 4; `pnlThemeReserved` — not part of the 32-control inventory, a Phase 23 reserved slot — is also a `flpShared` child.)

### Audit 3 — Load-Bearing Properties and Copy Preserved Verbatim

| Check | Expected | Actual | Result |
|---|---|---|---|
| `txtHotkey.ReadOnly = true` | 1 | 1 | PASS |
| `txtHotkey.TabStop = false` | 1 | 1 | PASS |
| `txtHotkey.Cursor = ...Cursors.Hand` | 1 | 1 | PASS |
| `txtAppPath.ReadOnly = true` | 1 | 1 | PASS |
| `btnClearAppPath.Enabled = false` | 1 | 1 | PASS |
| `DialogResult.OK` | 1 | 1 | PASS |
| `DialogResult.Cancel` | 1 | 1 | PASS |
| Combined txtHotkey 3-property regex | 3 | 3 | PASS |
| `BorderStyle.FixedSingle` | 4 | 4 | PASS (`pnlMonitor`, `pnlMonitorNormal`, `pnlAppPath`, `pnlSharedSection`) |
| `FlatStyle.Flat` | 4 (plan's stated criterion) | 6 | **Discrepancy — see below** |
| `GroupBox` | 0 (plan's stated criterion) | 5 | **Discrepancy — see below** |

**String-literal diff against baseline `0c1234f`:** extracted every `.Text = "..."` literal from both the current file and the baseline via `grep -oE '\.Text = "[^"]*"' \| sort -u` and diffed the two sets. Result: exactly one line of difference — `.Text = "Audio Devices"` present in baseline, absent in current. No other literal was added, removed, or reworded. Matches the audit's permitted-difference criterion exactly.

**`FlatStyle.Flat` discrepancy (pre-existing, not introduced by this plan):** the plan's acceptance criterion expects `4` (one per themed button: `btnBrowse`, `btnClearAppPath`, `btnSaveSettings`, `btnDiscardChanges`). Actual count is `6`. Verified against baseline: `git show 0c1234f:...SettingsForm.Designer.cs | grep -c "FlatStyle.Flat"` also returns `6` — the codebase has always had 6 matches (4 code assignments + 2 occurrences inside `btnBrowse`'s and `btnClearAppPath`'s explanatory `dotnet/winforms#13897` workaround comments, which contain the literal substring `FlatStyle.Flat` in prose). This exact discrepancy was already found and documented in `22-02-SUMMARY.md`'s Deviations section as a pre-existing baseline miscount, not a defect. No source change made; each of the four buttons still carries exactly one `FlatStyle.Flat` property assignment.

**`GroupBox` discrepancy (pre-existing, not introduced by this plan):** the plan's acceptance criterion expects `0`. Actual count is `5`, all inside explanatory comments describing the THEME-05 migration history (e.g. `// pnlMonitor (THEME-05: flat bordered Panel replacing the grpMonitor GroupBox bevel -- GroupBox has no flat variant, SetColorMode cannot recolor its 3D border...`). No `System.Windows.Forms.GroupBox` control declaration or instantiation exists anywhere in the file — confirmed via `git show 0c1234f:...SettingsForm.Designer.cs | grep -c "GroupBox"`, which returns `8` at baseline (the pre-migration file's own comments already referenced "GroupBox" 8 times, documenting the same THEME-05 Panel-replaces-GroupBox rationale). This phase's migration removed 3 of those baseline comment mentions and preserved 5. This is the identical class of finding as the `FlatStyle.Flat` discrepancy above — a literal-text grep matching preserved rationale-comment prose, not actual control usage. `grep -c "System.Windows.Forms.GroupBox"` (the fully-qualified type reference that would indicate a real control) returns `0`.

Both discrepancies are documented per hard constraint 2 ("do not weaken an audit to make it pass") — the raw grep counts are reported exactly as measured, with the root-cause explanation showing neither represents an actual pixel-positioning or GroupBox-usage violation.

### Audit 4 — Grid Configuration and Drag-Drop Wiring

| Check | Expected | Actual | Result |
|---|---|---|---|
| `DataGridViewAutoSizeColumnMode.Fill` | 2 | 2 | PASS |
| `Width = 66;` | 4 | 4 | PASS |
| `FillWeight` | 0 | 0 | PASS |
| `MinimumSize = new System.Drawing.Size(0, 120);` | 2 | 2 | PASS |
| `AllowDrop = true;` | 3 | 3 | PASS (`pnlAppPath`, `txtAppPath`, `tlpAppPath`) |
| `AppPath_DragEnter` | 3 | 3 | PASS |
| `AppPath_DragDrop` | 3 | 3 | PASS |
| `BeginInit();` | 8 | 8 | PASS (2 grids + 6 ErrorProviders) |
| `EndInit();` | 8 | 8 | PASS |
| `SuspendLayout();` vs `ResumeLayout(false);` | equal | 15 = 15 | PASS |

### Audit 5 — Blast Radius Is Exactly One File

| Check | Result |
|---|---|
| `git diff --stat 0c1234f -- src/` | Exactly one file: `src/RigToggle.App/SettingsForm.Designer.cs` (523 insertions, 171 deletions) |
| `git diff --stat 0c1234f -- '*.csproj' '*.sln'` | Empty |
| `git diff --name-status 0c1234f -- src/ \| grep -cE "^(A\|D)"` | `0` — no file added or removed |
| `grep -c "\.Parent" src/RigToggle.App/SettingsForm.cs` | `0` |
| `grep -c "SetError" src/RigToggle.App/SettingsForm.cs` | `18` (17 real call sites + 1 comment mention of "errMonitor.SetError", matches the plan's stated expected count exactly) |
| `git diff --stat 0c1234f -- src/RigToggle.App/ThemeApplier.cs` | Empty — byte-identical to baseline |
| `git status --porcelain src/` | Empty — this plan changed no source file |

All 7 error-target controls (`dgvMonitors`, `dgvMonitorsNormal`, `cboAudioNormal`, `cboAudioRig`, `txtAppPath`, `txtHotkey`, `chkStartWithWindows`) confirmed carrying a 20px right `Margin` (`Padding(0, 0, 20, ...)` / `Padding(0, 0, 20, 0)`) reserving `ErrorProvider` icon clearance.

## Two Phase 22 Success Criteria — Verification Status

| # | Criterion | Machine-verified? | Rig-pending? |
|---|---|---|---|
| SETTINGS-01 | No overlapping/crowded controls at default window size | No — this is a visual rendering claim; static audits confirm the *structural preconditions* (no pixel positioning, no orphaned/duplicated controls) but cannot observe actual on-screen layout | **Yes** — evidenced by rig checks 1, 3, 10, 11, 13, 14, 15, 16 (Task 2, not yet run) |
| SETTINGS-02 | Each mode's grid/audio picker and the shared section each read as one visually grouped, consistently spaced unit | No — same reasoning; Audits 2/3 confirm every control's parentage and border/spacing properties are structurally correct, but grouping is a rendered, DPI-sensitive visual judgment | **Yes** — evidenced by rig checks 1, 2, 5, 6, 7, 8, 12, 17 (Task 2, not yet run) |

Both criteria remain explicitly rig-pending. Neither is marked satisfied by this plan's Task 1 alone, consistent with the plan's `<done>` requirement for Task 1 ("both Phase 22 success criteria remain rig-pending because both are visual claims").

## Task 2: Rig-Hardware DPI Verification — BLOCKED, Cannot Be Executed Here

Task 2 is a `checkpoint:human-verify` with `gate="blocking"`. It requires:
- Publishing the rig binary (`dotnet publish ... -r win-x64 --self-contained true -p:PublishSingleFile=true`) and running it on real Windows hardware
- Manually working through 17 numbered checks at 100%, 125%, and 150% Windows display scale, each requiring an actual relaunch of the app after a live OS display-scale change
- Observing `DataGridView Dock=Fill` behavior inside a `TableLayoutPanel` cell, `Form.AutoSize` timing, `FormBorderStyle.Sizable` resize behavior, live DWM theme-flip re-rendering, and drag-drop hit-testing — none of which exist or can be exercised in this headless Linux container (no Windows GUI, no DWM, no `AutoScaleMode.Font` rescaling engine)

This plan's hard constraint 4 is explicit: *"Do not mark this phase done without the user's reported rig result. An inferred, assumed, or 'should work' verdict does not satisfy D-03's accepted tradeoff."* No such result exists yet. Execution stops here.

## Deviations from Plan

### Auto-fixed Issues

None — this plan makes no source changes (hard constraint 1); nothing to auto-fix.

### Documented, Not Auto-Fixed

**1. `grep -c "FlatStyle.Flat"` outputs `6`, not the plan's stated `4`** — pre-existing baseline miscount (comments contain the literal substring), already documented in `22-02-SUMMARY.md`. No functional impact; all 4 buttons carry exactly one real `FlatStyle.Flat` assignment. See Audit 3 above for full detail.

**2. `grep -c "GroupBox"` outputs `5`, not the plan's stated `0`** — same class of pre-existing false-positive: all 5 matches are inside preserved THEME-05 rationale comments explaining that a `Panel` replaced a `GroupBox`, not an actual `GroupBox` control reference. Baseline had 8 such comment mentions; this phase's migration removed 3 and kept 5. `grep -c "System.Windows.Forms.GroupBox"` (the real type-usage check) returns `0`. See Audit 3 above for full detail.

Neither discrepancy required a source change, weakened an audit, or represents an actual violation of D-03's "no pixel positioning, no GroupBox usage" intent — both are literal-text grep artifacts against preserved, load-bearing explanatory comments.

## Issues Encountered

None beyond the two documented grep-count discrepancies above. No auth gates, no blocking technical issues, no architectural questions arose during Task 1.

## Self-Check

- `.planning/phases/22-settingsform-layout-pass/22-03-SUMMARY.md` — this file, created — FOUND
- Build/test commands re-run and verbatim output captured above — confirmed against live `dotnet build`/`dotnet test` output, not inferred
- All grep/diff commands in the Audit 1–5 tables above were executed directly against the live repository state in this session; results are transcribed verbatim, not estimated

## User Setup Required

**Blocking — see Task 2 above.** The user must run this on real Windows 11 rig hardware:
1. `dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
2. Work through all 17 numbered checks in `.planning/phases/22-settingsform-layout-pass/22-03-PLAN.md` Task 2's `<how-to-verify>` block, at 100%, 125%, and 150% Windows display scale (relaunching the app after each scale change)
3. Report each check as PASS/FAIL with a note back to the executor, so this SUMMARY can be appended with the 17-row verdict table and Phase 22 can be closed

## Next Phase Readiness

- Task 1's regression gate and five static audits are complete, green, and fully documented above.
- Phase 22 cannot be marked complete until Task 2's rig verification is reported by the user and appended to this SUMMARY — per this plan's hard constraint 4 and PITFALLS.md Pitfall 9.
- No blockers on the static/source side. The sole blocker is the unavoidable Linux-sandbox/Windows-rig capability gap that this plan's Task 2 was explicitly designed to gate on.

## Task 2: Rig-Hardware DPI Verification — RESULT: FAIL (Checks 1 and 3)

The user published and ran the rig binary on real Windows hardware and reported the results below. Per the user's own report, testing stopped after Check 3 rather than grinding through the remaining 14 checks against a build that already fails at Check 1 (both grids and both audio pickers missing) — continuing at 125%/150% scale against a broken 100%-scale layout would not produce meaningful data. Checks 2 and 4-9 are recorded as blocked-pending-fix; checks 10-17 (all 125%/150% scale checks) are recorded as not attempted-pending-fix.

**Published binary path and Windows build:** not separately recorded by the user in this report. Not fabricated here — left blank per the plan's own discipline against inferring unreported detail.

### 17-Check Result Table

| # | Check | Result | Note |
|---|---|---|---|
| 1 | Baseline layout (SETTINGS-01 + SETTINGS-02) | **FAIL** | Both Normal and Rig columns show only their caption/explain text. The `DataGridView` (monitor grid) and the audio device `ComboBox` are entirely absent from both mode columns. User's own words: "normal and rig mode have separate windows but no settings now" / "Both modes only have explanations and monitor and audio are both missing." |
| 2 | Reserved space is invisible | Blocked — not evaluable while Check 1 fails | Cannot assess `pnlThemeReserved`'s zero-space claim when the columns above it are already broken |
| 3 | Manual resize (D-06) | **FAIL** | Dragging the window's right/bottom edge shows a resize preview outline that flickers/appears then vanishes — the window does not actually change size. User's own words: "settings window can be dragged to be resized but then nothing happens" / "Window resize preview flickers when dragged but then disappears, nothing resizes." |
| 4 | No minimize button | Blocked — not evaluated | User did not reach this check before stopping |
| 5 | Tab order | Blocked — not evaluable while Check 1 fails | Tab order across missing controls cannot be meaningfully assessed |
| 6 | Drag-drop still works on the whole box | Blocked — not evaluated | User did not reach this check before stopping |
| 7 | Validation feedback still visible | Blocked — not evaluable while Check 1 fails | Cannot force an error state on `dgvMonitors`/`dgvMonitorsNormal`/`cboAudioNormal`/`cboAudioRig` when those controls are not rendering |
| 8 | Live theme flip | Blocked — not evaluated | User did not reach this check before stopping |
| 9 | AutoSize vs. manual resize | Blocked — not evaluable while Check 3 fails | Cannot observe AutoSize snap-back behavior against a resize that never completes in the first place |
| 10 | Grid columns at 125% | Not attempted — blocked by Bug B at 100% scale already | Re-test after fix |
| 11 | Overlap/crowding at 125% | Not attempted — blocked by Bug B at 100% scale already | Re-test after fix |
| 12 | Button text at 125% | Not attempted — blocked by Bug B at 100% scale already | Re-test after fix |
| 13 | Resize at 125% | Not attempted — blocked by Bug B at 100% scale already | Re-test after fix |
| 14 | Repeat 10/11/12 at 150% | Not attempted — blocked by Bug B at 100% scale already | Re-test after fix |
| 15 | Whole-window sanity at 150% | Not attempted — blocked by Bug B at 100% scale already | Re-test after fix |
| 16 | Resize at 150% | Not attempted — blocked by Bug B at 100% scale already | Re-test after fix |
| 17 | Shared-section width (judgment call) | Not attempted — blocked by Bug B at 100% scale already | Re-test after fix |

### Explicit Per-Criterion Verdicts

**SETTINGS-01: FAIL** — evidenced by Check 1 (monitor grid and audio picker missing from both mode columns; this is not a "crowded controls" failure but a stronger structural failure — the controls do not render at all) and Check 3 (window resize is visually broken — preview flickers, no actual resize occurs).

**SETTINGS-02: FAIL** — evidenced by the same Check 1 result. Grouping ("each mode's grid/audio picker... reads as one visually grouped, consistently spaced unit") cannot be evaluated as satisfied when the grid and audio picker — the two controls the criterion is about grouping — are not visible at all. A group with its core members invisible fails by definition, not by degree.

Hardware confirmation of 22-RESEARCH.md's Open Question 1 (`Dock=Fill` grid resolution) and Assumptions Log A1/A2: **not obtainable from this report** — the grids never rendered, so their `Dock=Fill` behavior inside the `TableLayoutPanel` cell could not be observed at all (not even a broken-but-visible state). This is a stronger negative result than Open Question 1 anticipated (it anticipated overflow/scrollbar risk, not total non-rendering) and is captured as Hypothesis 2 below.

### Root-Cause Hypotheses (Unconfirmed — For Gap-Closure Research, Not Applied Here)

These are theoretical explanations offered to help the eventual gap-closure plan's research phase. They are **unconfirmed** — this execution environment is headless Linux with no Windows GUI, DWM, or WinForms layout engine, so nothing here was run or rendered to verify either hypothesis. Both are grounded in a direct read of `src/RigToggle.App/SettingsForm.Designer.cs` as it exists after Plans 01/02, not from the plan text alone.

**Hypothesis 1 (Bug A — resize preview flickers, no actual resize) — unconfirmed, needs gap-closure research/verification.**

`SettingsForm.Designer.cs` line 905 sets `this.AutoSize = true;` and line 907 sets `this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;` (both from 22-02-PLAN.md Task 3 step 3). This is a classic `Form.AutoSize = true` + `FormBorderStyle.Sizable` conflict: WinForms recalculates the form's size from its content on every layout pass when `AutoSize` is on, which can fight a user's live resize-drag (`WM_SIZING`) by snapping the form back to its `AutoSize`-computed size before the drag completes — visually indistinguishable from "a resize preview flickers then disappears, nothing resizes." This is the exact interaction `22-03-PLAN.md`'s own Check 9 was designed to probe (the plan's framing: "Resize the window manually... Confirm whether the window snaps back to its content-computed size, discarding your manual resize"), except the user hit a stronger, earlier-blocking form of it at Check 3 before Check 9 was ever reached. `22-02-PLAN.md` Task 3 step 3's own comment (Designer.cs lines 899-904) already flags this as a known open interaction: *"AutoSize sets the initial content-driven size and re-runs when a child's size or visibility changes... but it does not fight a user-driven edge drag. Whether a warning appearing after a manual resize feels disruptive is Plan 03 rig check 12"* — the comment's own claim ("does not fight a user-driven edge drag") is exactly what this rig result appears to contradict, but that contradiction is unconfirmed without re-testing on hardware. `22-03-PLAN.md`'s Task 2 `<action>` block pre-authorized a remedy for this class of failure: *"disabling `Form.AutoSize` after first show, keeping the container `AutoSize` intact"* — named as a gap-closure proposal, not to be implemented here. `22-RESEARCH.md` and `22-02-PLAN.md` Task 3 step 3 contain the full prior reasoning on this interaction and should be read first by the gap-closure plan's research step.

**Hypothesis 2 (Bug B — grid and audio picker not rendering in either column) — unconfirmed, needs gap-closure research/verification. This is the leading hypothesis.**

Confirmed by direct read of `SettingsForm.Designer.cs`: `tlpNormalColumn` (lines 357-370) and `tlpRigColumn` (lines 173-186) each declare `RowCount = 6` with `RowStyle`s in this exact order: `AutoSize` (row 0, caption), `AutoSize` (row 1, explain text), **`Percent 100F`** (row 2, the `DataGridView`), `AutoSize` (row 3, warning label), `AutoSize` (row 4, the audio picker sub-table), `AutoSize` (row 5, audio warning label) — row index 2 is the sole `Percent 100F` row in each column, and it hosts `dgvMonitorsNormal`/`dgvMonitors` respectively (`Controls.Add(this.dgvMonitorsNormal, 0, 2)` at line 373; `Controls.Add(this.dgvMonitors, 0, 2)` at line 189). But both `tlpNormalColumn.AutoSize` (line 368) and `tlpRigColumn.AutoSize` (line 183) are themselves `true` — each container computes its own height from its children's preferred sizes. A `TableLayoutPanel` row sized `Percent` needs a known total container height to distribute percentages against; when the container's own height is *derived from* its children (`AutoSize`) rather than given, that dependency is circular, and WinForms is documented/known to resolve `Percent` rows to a collapsed/zero height in exactly this configuration. That would make the grid row collapse to zero height — invisible — while the `AutoSize` rows above and below (captions, explain text, warning labels) still render fine on their own content-driven height. This matches the user's report precisely: "Both modes only have explanations and monitor and audio are both missing."

Two additional details sharpen this hypothesis and should be checked by gap-closure research:
1. **The circularity is not confined to one level.** The same `AutoSize`-container-hosting-a-`Percent`-row pattern repeats one level up: `tlpModeColumns` (the two-column parent of `tlpNormalColumn`/`tlpRigColumn`) has `AutoSize = true` (line 326) while its own single row is `RowStyle(SizeType.Percent, 100F)` (line 325). And one level further up again: `tlpRoot` has `AutoSize = true` (line 883) while its row 0 (hosting `tlpModeColumns`) is also `Percent 100F` (line 881). So the same theoretical collapse condition exists at three nested levels (`tlpRoot` → `tlpModeColumns` → `tlpNormalColumn`/`tlpRigColumn`), not just the innermost one. This may mean either (a) the grid row's `Percent 100F` collapses at the innermost level as described above, or (b) the entire mode-columns region collapses further up the chain, or (c) some other WinForms-specific resolution order applies that isn't collapse at all — which is exactly why this needs hardware/documentation confirmation rather than a guess.
2. **Why the audio picker is also missing, not just the grid.** The user reported "monitor and audio are both missing," not just the grid. `cboAudioNormal`/`cboAudioRig` sit inside `tlpAudioNormal`/`tlpAudioRig`, which are themselves added at row 4 of `tlpNormalColumn`/`tlpRigColumn` (an `AutoSize` row, not the `Percent` row). If only the `Percent` row (row 2, the grid) were collapsing, row 4 (audio, `AutoSize`) should still render normally alongside the caption/explain/warning rows. Its absence suggests either: the collapse compounds downward from row 2 in a way that also suppresses layout of later rows in the same table (plausible if WinForms is failing to complete a layout pass on the table at all, not just mis-sizing one row), or a second, independent cause affects the audio picker specifically. The gap-closure plan's research step should treat "why is the audio row also gone" as a distinct open question, not assume it is fully explained by the `Percent`-row collapse theory above.

`22-03-PLAN.md`'s Task 2 `<action>` block does not pre-name a remedy for this specific failure (unlike Hypothesis 1's Check 9/`AutoSize`-after-first-show remedy), because this exact total-non-rendering failure mode was not anticipated by the plan — Open Question 1 anticipated an overflow/scrollbar risk at high DPI scale, not a complete absence at 100% scale. A candidate fix direction — changing the grid row from `Percent 100F` to `AutoSize` with a `MinimumSize` floor instead — is offered here only as a starting point for gap-closure research, not a pre-authorized remedy; the research step should verify the actual WinForms `TableLayoutPanel` percent-row-inside-autosize-container behavior (via real rendering or authoritative documentation) before committing to any specific fix, since the three-level nesting noted above means the correct fix point (innermost row, `tlpModeColumns`, or `tlpRoot`) is not yet established.

### No Source Change Confirmation

Per this plan's hard constraint 1 ("This plan makes NO source changes"), no source file was touched while recording this rig result. `git status --porcelain src/` remains empty for the whole of Plan 22-03, Task 1 and Task 2 alike. Both Bug A and Bug B are recorded here as findings only; remediation for both routes to a dedicated gap-closure plan with its own research, implementation, and re-verification gate — not smuggled into this verification plan.

### Deviations from Plan (Task 2)

None — this task recorded the user's reported rig result verbatim and added no source change, consistent with hard constraint 1. The plan's own acceptance criteria anticipated the possibility of FAILs and specified exactly this handling (name control, scale, and observed behavior; name an authorized remedy where the plan already named one; do not implement).

### Self-Check (Task 2)

- `.planning/phases/22-settingsform-layout-pass/22-03-SUMMARY.md` — this section appended — FOUND
- `SettingsForm.Designer.cs` line references for `tlpNormalColumn`/`tlpRigColumn`/`tlpModeColumns`/`tlpRoot` `RowStyle`/`AutoSize` declarations, and Form-level `AutoSize`/`FormBorderStyle.Sizable` — read directly from the live file in this session, not inferred from plan text — confirmed
- `git status --porcelain src/` — confirmed empty, no source file changed by this plan

## Two Phase 22 Success Criteria — Final Verdict

| # | Criterion | Verdict | Evidence |
|---|---|---|---|
| SETTINGS-01 | No overlapping/crowded controls at default window size | **FAIL** | Rig Check 1 (grid and audio picker entirely absent from both mode columns — a stronger failure than "crowded," a total non-render) and Rig Check 3 (manual resize does not work) |
| SETTINGS-02 | Each mode's grid/audio picker and the shared section each read as one visually grouped, consistently spaced unit | **FAIL** | Rig Check 1 — grouping cannot be evaluated as satisfied when the grid and audio picker, the very controls the criterion is about grouping, do not render at all |

**Phase 22 is not complete.** Both success criteria fail on real rig hardware. This plan (22-03) has completed its own scope — running the regression gate, five static audits, and gathering the rig-hardware result — but the phase itself requires a gap-closure plan to fix Bug A and Bug B, followed by a fresh rig-verification pass, before Phase 22 can close.

---
*Phase: 22-settingsform-layout-pass*
*Task 1 completed: 2026-08-14*
*Task 2 completed: 2026-08-15 — FAIL (Checks 1 and 3; remaining checks blocked/not attempted per user's report)*
*Plan 22-03 status: both tasks complete; overall plan result records a FAILED rig verification requiring gap closure*
