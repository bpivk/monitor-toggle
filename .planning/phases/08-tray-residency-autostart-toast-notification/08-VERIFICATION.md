---
phase: 08-tray-residency-autostart-toast-notification
verified: 2026-07-31T06:29:36Z
status: human_needed
score: 5/7 must-haves verified (2 uncertain, pending rig retest)
overrides_applied: 0
human_verification:
  - test: "D-06 hidden-start retest: rebuild the win-x64 self-contained publish (`dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64`) and run `RigToggle.App.exe --tray` from a terminal."
    expected: "No window appears at any point; the tray icon is present immediately and already shows the correct mode glyph/tooltip (InitializeTrayState primes it before Application.Run)."
    why_human: "This is the exact scenario that FAILED on the first rig pass (08-04-SUMMARY.md scenario #7) before being root-caused and fixed in commit 91c11df (`Application.Run(new ApplicationContext())` with no MainForm). The fix is code-reviewed and looks sound (08-REVIEW.md explicitly re-examined this mechanism and found no regression), but it has not been re-run on real Windows since the fix — WinForms message-loop/startup behavior in this codebase has already proven to diverge from documented/researched theory once in this exact spot, so only a live rig run counts as proof."
  - test: "Assumption A2 retest: with the app started via `--tray` and the window NEVER shown even once, right-click the tray icon and choose Exit."
    expected: "The process fully terminates (no orphaned RigToggle process in Task Manager) and the tray icon vanishes immediately (no ghost/hover-only icon lingering)."
    why_human: "Blocked/untested in the first rig session because scenario #7 (D-06) failed before this dependent scenario could be exercised (08-04-SUMMARY.md scenario #8, 08-HUMAN-UAT.md item #2). Requires the D-06 fix to be in a working state first, then live process/tray-icon observation on Windows — not something a static code read can confirm (Application.Exit()'s interaction with a MainForm-less ApplicationContext plus NotifyIcon lifecycle is exactly the kind of runtime behavior this project has already been burned by trusting on paper)."
---

# Phase 8: Tray Residency, Autostart & Toast Notification Verification Report

**Phase Goal:** Users can run the app tray-resident, have it start automatically with Windows if desired, control it entirely from the tray icon, and get a toast notification confirming what changed whenever a toggle happens without the GUI open.
**Verified:** 2026-07-31T06:29:36Z
**Status:** human_needed
**Re-verification:** No — initial verification

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
| 7 | Launching with `--tray` starts fully hidden (no window flash) — the mechanism that makes "start with Windows" non-annoying (TRAY-02, hidden-start sub-behavior, D-06) | ? UNCERTAIN | **Initially FAILED on rig** (08-04-SUMMARY.md scenario #7: window appeared). Root-caused and fixed in commit `91c11df` (`Application.Run(new ApplicationContext())` with no `MainForm`, vs. the disproven `ApplicationContext(mainForm)` theory). Fix re-examined during code review (08-REVIEW.md) and judged mechanically sound with no regression found. **Not yet re-run on real Windows** — user was away from the rig (08-HUMAN-UAT.md item #1, still `pending`). |
| 8 | Exit-while-never-shown terminates cleanly with no orphan process / ghost tray icon (Assumption A2, dependent on #7) | ? UNCERTAIN | Blocked in the first rig session by #7's failure (08-04-SUMMARY.md scenario #8: BLOCKED). Cannot be exercised until #7 is confirmed working. 08-HUMAN-UAT.md item #2, still `pending`. |

**Score:** 6/8 truths VERIFIED, 2/8 UNCERTAIN pending rig retest (5/7 if #1+#2 and #6 are grouped as the roadmap's TRAY-01/05 and TRAY-02 success-criteria lines — either count nets the same two open items).

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
| TRAY-01 | 08-02 | Hide-to-tray on close instead of exit | ✓ SATISFIED | Rig-confirmed + code verified. REQUIREMENTS.md still shows "Pending" — consistent with the project's rig-validation-before-completion discipline; should flip to Complete only once the 2 outstanding retest items close. |
| TRAY-02 | 08-01, 08-03 | Settings checkbox for start-with-Windows, off by default | ⚠ PARTIALLY SATISFIED | Checkbox + registry read/write rig-confirmed working. The `--tray` hidden-start sub-behavior (what makes autostart non-annoying) had a real bug, is now fixed in code and code-reviewed sound, but is UNCONFIRMED on hardware. |
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

### Human Verification Required

### 1. D-06 hidden-start retest

**Test:** Rebuild the win-x64 self-contained single-file publish (`dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64`) and run `RigToggle.App.exe --tray` from a terminal on the rig.
**Expected:** No window appears at any point during startup; the tray icon is present immediately with the correct mode glyph/tooltip.
**Why human:** This exact scenario FAILED on the first rig pass (08-04-SUMMARY.md #7) despite being backed by cited Microsoft-docs research — proving that source-level confidence alone was insufficient here once already. The fix (commit `91c11df`) is code-reviewed and judged sound, but "sound on paper" was also true of the original, disproven approach. Only a live rig run counts as proof for this specific WinForms message-loop behavior.

### 2. Assumption A2 retest (Exit while started `--tray`, window never shown)

**Test:** With the app started via `--tray` and the main window never shown even once, right-click the tray icon and choose Exit.
**Expected:** The process fully terminates (no orphaned `RigToggle` process in Task Manager) and the tray icon vanishes immediately (no ghost/hover-only icon).
**Why human:** This scenario was BLOCKED in the first rig session by scenario #7's failure (08-04-SUMMARY.md #8) and has never actually been exercised. It depends on live process-list and tray-icon observation on Windows, which cannot be inspected from source alone.

### Gaps Summary

No code-level gaps were found. Every artifact and key link this phase's plans specify exists, is substantive (not a stub), and is wired correctly, including all 7 code-review findings (1 Critical, 5 Warnings that admit a fix, 1 accepted-risk Warning) genuinely fixed in commit `32a2845` — verified by direct inspection, not by trusting the commit message.

The phase is NOT yet fully closeable, however: two interactive scenarios from the mandatory 08-04 rig-validation checkpoint — the `--tray` hidden-start mechanism (D-06) and its dependent Exit-while-never-shown scenario (Assumption A2) — remain in `pending` status in `08-HUMAN-UAT.md`. The underlying bug that caused the first failure has a code fix that looks correct on inspection and survived a subsequent code review's scrutiny of that exact mechanism, but per this project's own established rig-validation-before-completion discipline (explicitly invoked in 08-04-SUMMARY.md, citing Phase 1 and Phase 6 precedent), an unconfirmed fix to a mechanism that has already once diverged from documented behavior on this runtime cannot be marked "passed." REQUIREMENTS.md correctly still shows all 6 Phase 8 requirement IDs as "Pending" rather than "Complete," consistent with this stance.

**Recommendation:** Do not advance past Phase 8 (or block Phase 9/10 work that assumes tray-resident single-instance behavior) until the user retests the two pending items and reports the result via `08-HUMAN-UAT.md`. If both pass, this phase's status can be flipped to `passed` without any further code changes. If either fails, treat it as a new gap for `/gsd:plan-phase 8 --gaps` (not a regression of already-fixed work, per 08-HUMAN-UAT.md's own framing).

---

_Verified: 2026-07-31T06:29:36Z_
_Verifier: Claude (gsd-verifier)_
