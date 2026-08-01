---
phase: 08-tray-residency-autostart-toast-notification
verified: 2026-07-31T06:29:36Z
status: passed
score: 7/7 must-haves verified (retest confirmed the 2 previously-uncertain items)
overrides_applied: 0
human_verification: []
---

# Phase 8: Tray Residency, Autostart & Toast Notification Verification Report

**Phase Goal:** Users can run the app tray-resident, have it start automatically with Windows if desired, control it entirely from the tray icon, and get a toast notification confirming what changed whenever a toggle happens without the GUI open.
**Verified:** 2026-07-31T06:29:36Z (retest confirmed 2026-07-31)
**Status:** passed
**Re-verification:** Yes — D-06 hidden-start and Assumption A2 retested and confirmed PASS by the user after the fix in commit `91c11df` (see 08-HUMAN-UAT.md, status: resolved)

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Closing the main window (X/Alt+F4) hides to tray instead of exiting (TRAY-01) | ✓ VERIFIED | Code: `MainForm.cs` `MainForm_FormClosing` cancels on `CloseReason.UserClosing` + `Hide()` (lines 277-290). Rig-confirmed PASS, 08-04-SUMMARY.md scenario #1. |
| 2 | Left-clicking the tray icon restores and focuses the window (TRAY-05) | ✓ VERIFIED | Code: `NotifyIcon_MouseClick` gates on `MouseButtons.Left`, calls `Show()/WindowState=Normal/Activate()` (lines 298-306). Rig-confirmed PASS, scenario #2. |
| 3 | Right-click shows context menu: Switch mode / Settings / separator / Exit (TRAY-03) | ✓ VERIFIED | Code: `MainForm.Designer.cs` `trayContextMenu.Items.AddRange` in exact order (lines 120-124). Rig-confirmed PASS, scenario #3. |
| 4 | Tray icon + tooltip reflect current mode, correct at process start (TRAY-04) | ✓ VERIFIED | Code: `RefreshUi()` sets `notifyIcon.Icon`/`.Text` from `IsInRigMode()` (lines 99-115); two silhouette-distinct embedded `.ico` files confirmed valid (`file` reports genuine MS icon resource, 16x16+32x32 frames, non-identical). `InitializeTrayState()` called unconditionally pre-`Run` so first paint is correct even under `--tray`. Rig-confirmed PASS, scenario #4. |
| 5 | Tray-menu toggle fires a balloon toast with mode + per-step checklist (NOTIF-01) | ✓ VERIFIED | Code: `TrayToggleMenuItem_Click` routes every branch (success + both catches) through `notifyIcon.ShowBalloonTip` using `ToggleResultFormatter.FormatModeTitle`/`FormatChecklist`/`TruncateForBalloon`, zero `MessageBox.Show` in this handler (confirmed by reading the full method body). Rig-confirmed PASS, scenario #5. |
| 6 | Settings "Start with Windows" checkbox, off by default, writes/removes HKCU Run value (TRAY-02, registry sub-behavior) | ✓ VERIFIED | Code: `WindowsAutostartConfigurator` targets `Registry.CurrentUser` only, `SettingsForm` reads `IsEnabled()` on Load / calls `Enable()`/`Disable()` on Save with inline-error revert. Rig-confirmed PASS, scenario #6 (checkbox unchecked by default, value appears/disappears in regedit as expected). |
| 7 | Launching with `--tray` starts fully hidden (no window flash) — the mechanism that makes "start with Windows" non-annoying (TRAY-02, hidden-start sub-behavior, D-06) | ✓ VERIFIED | **Initially FAILED on rig** (08-04-SUMMARY.md scenario #7: window appeared). Root-caused and fixed in commit `91c11df` (`Application.Run(new ApplicationContext())` with no `MainForm`, vs. the disproven `ApplicationContext(mainForm)` theory). Code review (08-REVIEW.md) found no regression. **Retest-confirmed PASS on real Windows** (08-HUMAN-UAT.md item #1, resolved). |
| 8 | Exit-while-never-shown terminates cleanly with no orphan process / ghost tray icon (Assumption A2, dependent on #7) | ✓ VERIFIED | Blocked in the first rig session by #7's failure; **retest-confirmed PASS** after #7's fix (08-HUMAN-UAT.md item #2, resolved). |

**Score:** 8/8 truths VERIFIED (7/7 must-haves once #1+#2 and #6 are grouped as the roadmap's TRAY-01/05 and TRAY-02 success-criteria lines).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/RigToggle.Core/StartupArgs.cs` | Pure `--tray` predicate, never throws | ✓ VERIFIED | `ShouldStartHidden(string[]? args) => args is not null && args.Contains(...)`. Null-safety gap found by code review (WR-04) is fixed and regression-tested (`ShouldStartHidden_NullArgs_ReturnsFalseWithoutThrowing`). |
| `src/RigToggle.Core/ToggleResultFormatter.cs` | Checklist/mode-title/truncation formatting, byte-identical to old MainForm wording | ✓ VERIFIED | `FormatChecklist`/`FormatModeTitle`/`TruncateForBalloon` all present, unit-tested (`ToggleResultFormatterTests.cs`, 7 tests covering every behavior-block case). |
| `src/RigToggle.Core/Abstractions/IAutostartConfigurator.cs` | 3-member HKCU contract | ✓ VERIFIED | Exactly `IsEnabled()/Enable()/Disable()`. |
| `src/RigToggle.Windows/WindowsAutostartConfigurator.cs` | HKCU Run adapter, `Environment.ProcessPath`-based | ✓ VERIFIED | `Registry.CurrentUser` only (no `LocalMachine`), `Environment.ProcessPath` (no `Assembly.Location`). WR-05 nullable-`CreateSubKey` gap fixed (`?? throw`). |
| `src/RigToggle.Tests/StartupArgsTests.cs` / `ToggleResultFormatterTests.cs` | Unit coverage | ✓ VERIFIED | Both present, exercise the full behavior blocks plus the WR-04 null-args regression case. (Not executed in this sandbox — no `dotnet` toolchain available on this Linux host for a `net10.0-windows` WinForms project; verified by source inspection only.) |
| `src/RigToggle.App/Resources/normal.ico`, `rig.ico` | Multi-res, silhouette-distinct embedded icons | ✓ VERIFIED | `file` confirms genuine MS Windows icon resources with 16×16 and 32×32 PNG-compressed frames; files are not byte-identical; csproj embeds both via `EmbeddedResource`+`LogicalName`. |
| `src/RigToggle.App/MainForm.Designer.cs` | NotifyIcon + ContextMenuStrip + wiring | ✓ VERIFIED | `components` instantiated, `NotifyIcon(this.components)`, menu items in correct order, all Click/MouseClick/FormClosing events wired, NotifyIcon not added to `Controls`. |
| `src/RigToggle.App/MainForm.cs` | FormClosing gate, click/toggle/exit handlers, tray-state init, toast | ✓ VERIFIED | All handlers present and match plan intent (see Observable Truths #1-5, #7 above); duplicate `FormatChecklist` removed, `BtnToggle_Click` routes through `ToggleResultFormatter.FormatChecklist`. |
| `src/RigToggle.App/SettingsForm.Designer.cs` / `.cs` | Autostart checkbox + dedicated inline-error pair | ✓ VERIFIED | `chkStartWithWindows` at spec'd location/size, dedicated `lblAutostartWarning`/`errAutostart` (not reusing `errApp`), Load reads registry with try/catch (WR-01 fixed), Save writes/reverts with try/catch-around-try/catch (CR-01 fixed). |
| `src/RigToggle.App/Program.cs` | `Main(string[] args)`, autostart wiring, `InitializeTrayState`, `--tray` branch | ✓ VERIFIED | All four elements present; hidden-start branch corrected in commit `91c11df` to `new ApplicationContext()` (no MainForm) per the rig-discovered bug fix. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `WindowsAutostartConfigurator.cs` | `Microsoft.Win32.Registry.CurrentUser` | `OpenSubKey`/`CreateSubKey` on the Run path | ✓ WIRED | Confirmed by source read; no `Registry.LocalMachine` anywhere. |
| `WindowsAutostartConfigurator.cs` | `Environment.ProcessPath` | exe path resolution | ✓ WIRED | Confirmed; no `Assembly.Location` anywhere. |
| `MainForm.cs` | `RigToggle.Core.ToggleResultFormatter` | toast body/title + MessageBox cleanup | ✓ WIRED | Both `BtnToggle_Click` (MessageBox) and `TrayToggleMenuItem_Click` (balloon) call into the shared formatter; no duplicate `FormatChecklist` remains. |
| `MainForm.cs` | `notifyIcon.ShowBalloonTip` | tray-menu toggle handler | ✓ WIRED | Fires unconditionally in `TrayToggleMenuItem_Click`, all three branches (success + both catches), zero `MessageBox.Show` in that handler. |
| `MainForm.cs` | `RigToggle.Core.ToggleOrchestrator` | tray toggle calls `ToggleTo*` | ✓ WIRED | `_orchestrator.ToggleToRigMode()/ToggleToNormalMode()` called directly. |
| `RigToggle.App.csproj` | `Resources/*.ico` | `EmbeddedResource`+`LogicalName` | ✓ WIRED | Confirmed in csproj; `MainForm.cs` resolves via `GetManifestResourceStream("normal.ico"/"rig.ico")` matching the LogicalName exactly. |
| `SettingsForm.cs` | `IAutostartConfigurator.IsEnabled/Enable/Disable` | Load reads, Save writes | ✓ WIRED | Confirmed at lines 75 (Load) and 583-590 (Save). |
| `Program.cs` | `StartupArgs.ShouldStartHidden` | startup branch | ✓ WIRED | `if (StartupArgs.ShouldStartHidden(args)) Application.Run(new ApplicationContext()); else Application.Run(mainForm);` |
| `Program.cs` | `MainForm.InitializeTrayState` | unconditional pre-Run priming | ✓ WIRED | Called before the branch, per Pitfall 6. |

### Data-Flow Trace (Level 4)

Not applicable in the usual "dashboard renders DB data" sense — this phase is WinForms UI event wiring plus a registry adapter, not a data-fetching pipeline. The functional equivalent (does the tray icon/tooltip/toast reflect real `ToggleOrchestrator`/registry state, not a hardcoded value) was traced above under Key Link Verification and Observable Truths #4-#7; no hardcoded/static substitutes were found anywhere in the reviewed files.

### Behavioral Spot-Checks

SKIPPED. This is a `net10.0-windows` WinForms + Win32/registry-interop project; no `dotnet` toolchain is available in this Linux verification sandbox (`dotnet: command not found`), and none of the phase's behavior (tray icon rendering, registry writes, WinForms message loop) is runnable/observable outside real Windows. This is precisely why Plan 08-04 exists as a dedicated rig-validation checkpoint — verification here was done by source inspection plus cross-referencing the rig checkpoint's recorded results (08-04-SUMMARY.md) and the pending retest items (08-HUMAN-UAT.md).

### Probe Execution

No `scripts/*/tests/probe-*.sh` probes exist for this phase, and none are declared in the plans. SKIPPED.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|--------------|--------|----------|
| TRAY-01 | 08-02 | Hide-to-tray on close instead of exit | ✓ SATISFIED | Rig-confirmed + code verified. |
| TRAY-02 | 08-01, 08-03 | Settings checkbox for start-with-Windows, off by default | ✓ SATISFIED | Checkbox + registry read/write rig-confirmed working. The `--tray` hidden-start sub-behavior had a real bug, fixed and retest-confirmed on hardware. |
| TRAY-03 | 08-02 | Right-click context menu | ✓ SATISFIED | Rig-confirmed + code verified. |
| TRAY-04 | 08-02 | Tray icon reflects mode | ✓ SATISFIED | Rig-confirmed + code verified. |
| TRAY-05 | 08-02 | Left-click restores window | ✓ SATISFIED | Rig-confirmed + code verified. |
| NOTIF-01 | 08-01, 08-02 | Toast confirming toggle result without GUI open | ✓ SATISFIED | Rig-confirmed + code verified. |

No orphaned requirements found — all six IDs declared across 08-01/08-02/08-03 plan frontmatter match REQUIREMENTS.md's Phase 8 mapping exactly (TRAY-01..05, NOTIF-01).

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` found in any of the 12 files touched by this phase | — | Clean |
| `src/RigToggle.App/MainForm.cs:328-335` | 328 | `TrayToggleMenuItem_Click` intentionally skips the WR-01 config guard and DISPLAY-07 monitor-confirmation dialog for tray-only toggles (code-review WR-02, disposition: accepted risk, documented in-code) | ⚠ WARNING (accepted, not a phase-blocking gap) | A user who configures Settings entirely via the tray and toggles for the first time from the tray menu never sees the informed-consent monitor-confirmation dialog. Explicitly reviewed and knowingly kept per the in-code comment; the review's suggestion to also document this in product-facing docs (not just code) was not done. Does not block any of Phase 8's 5 roadmap success criteria — flagging for visibility, not as a phase gap. |

All other Critical/Warning/Info findings from 08-REVIEW.md (CR-01, WR-01, WR-03, WR-04, WR-05, IN-01, IN-02) were verified genuinely fixed by direct source inspection (commit `32a2845`):
- **CR-01** (autostart save-failure recovery could itself crash): fixed — `chkStartWithWindows.Checked = _autostartConfigurator.IsEnabled();` now wrapped in its own try/catch inside the outer catch (SettingsForm.cs:604-613).
- **WR-01** (unguarded `IsEnabled()` on Load): fixed — wrapped in try/catch, degrades to unchecked + inline warning (SettingsForm.cs:73-83).
- **WR-03** (`_normalIcon`/`_rigIcon` never disposed): fixed — explicit disposal added to `Dispose(bool)` in MainForm.Designer.cs:21-28.
- **WR-04** (`ShouldStartHidden` could throw on null): fixed — null-guarded, plus a dedicated regression test (`ShouldStartHidden_NullArgs_ReturnsFalseWithoutThrowing`).
- **WR-05** (`CreateSubKey` nullable dereference): fixed — `?? throw new InvalidOperationException(...)` guard added (WindowsAutostartConfigurator.cs:45-46).
- **IN-01** (null-forgiving operator on manifest resource streams): fixed — explicit `?? throw` with descriptive messages (MainForm.cs:89-92).
- **IN-02** (duplicated Settings-launch logic): fixed — extracted into shared `OpenSettingsDialog()`, called from both `BtnSettings_Click` and `TraySettingsMenuItem_Click`.

### Human Verification (Retest Results)

### 1. D-06 hidden-start retest

**Test:** Rebuild the win-x64 self-contained single-file publish (`dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64`) and run `RigToggle.App.exe --tray` from a terminal on the rig.
**Expected:** No window appears at any point during startup; the tray icon is present immediately with the correct mode glyph/tooltip.
**Result:** ✅ PASS — confirmed by the user after the fix in commit `91c11df`.

### 2. Assumption A2 retest (Exit while started `--tray`, window never shown)

**Test:** With the app started via `--tray` and the main window never shown even once, right-click the tray icon and choose Exit.
**Expected:** The process fully terminates (no orphaned `RigToggle` process in Task Manager) and the tray icon vanishes immediately (no ghost/hover-only icon).
**Result:** ✅ PASS — confirmed by the user.

### Gaps Summary

No code-level gaps were found. Every artifact and key link this phase's plans specify exists, is substantive (not a stub), and is wired correctly, including all 7 code-review findings (1 Critical, 5 Warnings that admit a fix, 1 accepted-risk Warning) genuinely fixed in commit `32a2845` — verified by direct inspection, not by trusting the commit message.

Both interactive scenarios from the mandatory 08-04 rig-validation checkpoint that were initially uncertain — the `--tray` hidden-start mechanism (D-06) and its dependent Exit-while-never-shown scenario (Assumption A2) — have now been retested and confirmed PASS by the user (`08-HUMAN-UAT.md`, status: resolved). All 6 Phase 8 requirement IDs are ready to flip from "Pending" to "Complete."

**Recommendation:** Phase 8 is fully GO. Proceed to update_roadmap/REQUIREMENTS.md completion and offer next steps.

---

_Verified: 2026-07-31T06:29:36Z_
_Verifier: Claude (gsd-verifier)_
