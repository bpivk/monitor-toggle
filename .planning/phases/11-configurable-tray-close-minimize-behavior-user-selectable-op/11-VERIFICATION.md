---
phase: 11-configurable-tray-close-minimize-behavior-user-selectable-op
verified: 2026-08-01T21:00:00Z
status: human_needed
score: 11/11 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 10/11
  gaps_closed:
    - "The CR-01 lockout-guard fix (MainForm.ApplyTrayVisibility) prevents the app from becoming unreachable via any UI surface when both tray preferences are disabled — including the --tray-autostart + tray-Settings-disable-both path that commit e317323 missed"
  gaps_remaining: []
  regressions: []
---

# Phase 11: Configurable Tray Close/Minimize Behavior Verification Report

**Phase Goal:** User can independently configure whether closing the main window (X) minimizes to tray or exits the app, and whether the minimize button also minimizes to tray, instead of the current fixed always-minimize-to-tray behavior from Phase 8.
**Verified:** 2026-08-01
**Status:** human_needed
**Re-verification:** Yes — after gap closure (commit `a2f5c48`, following prior gap report on commit `e317323`)

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `AppSettings` exposes `CloseMinimizesToTray`/`MinimizeToTray` as plain bool fields, default false on upgrade | VERIFIED (regression check) | `src/RigToggle.Core/Models/AppSettings.cs:32-33` — unchanged since prior verification |
| 2 | Both fields round-trip through `JsonSettingsStore` Save/Load, including a genuine field-less legacy JSON | VERIFIED (regression check) | `src/RigToggle.Tests/JsonStoreTests.cs:112-165` — unchanged, tests still present |
| 3 | D-01: X (`CloseReason.UserClosing`) hides to tray only when `CloseMinimizesToTray` is true; otherwise the app exits | VERIFIED (regression check) | `src/RigToggle.App/MainForm.cs:400-405` — unchanged conditional |
| 4 | D-04/D-05: Minimize hides to tray only when `MinimizeToTray` is true (via the shared `SendToTray()` helper); standard OS minimize otherwise | VERIFIED (regression check) | `src/RigToggle.App/MainForm.cs:427-446` — unchanged |
| 5 | Close and Minimize share exactly one `Hide()` call site (`SendToTray()`), not duplicated | VERIFIED (regression check) | `src/RigToggle.App/MainForm.cs:363-366` — unchanged single call site |
| 6 | D-08/D-09: `notifyIcon.Visible` is derived as `CloseMinimizesToTray \|\| MinimizeToTray`, applied at startup and on demand | VERIFIED (regression check) | `src/RigToggle.App/MainForm.cs:129-151` (`ApplyTrayVisibility`), called from `InitializeTrayState()` (line 95) and `SettingsForm.cs:745` |
| 7 | D-11: `NotifyIcon` is always instantiated; only `.Visible` is derived | VERIFIED (regression check) | `MainForm.Designer.cs:50,138` — unchanged, `Visible` defaults `false`, set live |
| 8 | D-03/D-06/D-07: Settings shows the two checkboxes with locked wording, grouped directly above "Start with Windows" | VERIFIED (regression check) | `SettingsForm.Designer.cs:315-330` — unchanged |
| 9 | D-03/D-06: Settings Load/Save round-trips both checkboxes with no error/confirmation path | VERIFIED (regression check) | `SettingsForm.cs:91-92,726-727` — unchanged |
| 10 | D-08: Saving Settings applies the derived tray-icon visibility live via an injected `Action` | VERIFIED (regression check) | `SettingsForm.cs:745` (`_applyTrayVisibility()` invoked right after `_settingsStore.Save`, before the autostart/hotkey blocks), `Program.cs:114` |
| 11 | The CR-01 lockout-guard fix (`MainForm.ApplyTrayVisibility`) prevents the app from becoming unreachable via any UI surface when both tray preferences are disabled, in ALL reachable startup/interaction paths — including the `--tray` autostart + tray-only-Settings path the prior gap identified | **VERIFIED** (full re-trace, source-level only — see Human Verification below) | See trace below |

**Score:** 11/11 truths verified

### Full Re-Trace of Truth #11 (the closed gap)

**Fix commit:** `a2f5c48` — removes the `_windowEverShown` field entirely (was set only in `OnLoad`, which never fires under `--tray`) and replaces the guard condition with `wasVisible` — `notifyIcon.Visible` read into a local **before** this call overwrites it:

```csharp
bool wasVisible = notifyIcon.Visible;
bool shouldBeVisible = settings.CloseMinimizesToTray || settings.MinimizeToTray;

if (wasVisible && !shouldBeVisible && !Visible)
{
    Show();
    WindowState = FormWindowState.Normal;
}

notifyIcon.Visible = shouldBeVisible;
```
(`src/RigToggle.App/MainForm.cs:129-151`)

**Scenario 1 — the exact gap reproduction (`--tray` autostart, `MinimizeToTray=true`, `CloseMinimizesToTray=false`, window never shown, Settings opened via tray menu, both prefs disabled, Save):**
1. `Program.cs:121` calls `mainForm.InitializeTrayState()` before either `Application.Run` branch → `ApplyTrayVisibility()` runs with `notifyIcon.Visible` at its Designer default (`false`, confirmed `MainForm.Designer.cs:138`). `wasVisible = false`, `shouldBeVisible = true` (MinimizeToTray on). Guard requires `wasVisible` → skipped (correctly; nothing to protect yet). `notifyIcon.Visible` set to `true`.
2. `Application.Run(new ApplicationContext())` (no main form, `Program.cs:146`, gated by `StartupArgs.ShouldStartHidden`) — `MainForm.Show()` is never called. `Visible` stays `false`.
3. User right-clicks the now-visible tray icon → Settings (`TraySettingsMenuItem_Click` → `OpenSettingsDialog()`, `MainForm.cs:348-355,464`) — never calls `Show()`.
4. User unchecks `MinimizeToTray`, clicks Save. `SettingsForm.cs:745` calls `_applyTrayVisibility()` → `MainForm.ApplyTrayVisibility()` runs again: `wasVisible = notifyIcon.Visible = true` (set in step 1), `shouldBeVisible = false || false = false`, `Visible = false` (window still never shown). Guard `wasVisible && !shouldBeVisible && !Visible` = `true && true && true` = **true** → `Show(); WindowState = Normal;` fires. Window is forced into view before `notifyIcon.Visible` is set to `false`.

Result: the window becomes the reachable UI surface at the exact moment the tray icon disappears. **Gap closed for this exact reproduction.**

**Scenario 2 — original CR-01 case (visible session, hidden via Close/Minimize, then both prefs disabled from tray Settings) — must still work (no regression):**
`wasVisible = true` (tray icon was showing), `Visible = false` (window `Hide()`'d via `SendToTray()`), `shouldBeVisible` going to `false` → guard fires identically. **Still correct.**

**Scenario 3 — accepted D-10 case (`--tray` autostart, BOTH prefs false at startup, no live Settings change) — must remain untouched (no forced Show where none is warranted):**
At `InitializeTrayState()`, `notifyIcon.Visible` starts at Designer default `false`, `shouldBeVisible = false` → `wasVisible = false` → guard condition false regardless of `!shouldBeVisible && !Visible` → no forced `Show()`. Tray icon and window both stay absent, matching 11-CONTEXT.md's explicit D-10 acceptance ("no special handling or warning" for this combination). **Correctly untouched.**

**Scenario 4 — visible session, tray disabled from Settings opened via the main window's own Settings button (not tray menu) — must not force an unwanted `Show()`/focus-steal:**
`Visible = true` at the time of Save (window already on-screen) → guard's `!Visible` term is false → guard skipped, no redundant `Show()`/`WindowState` reset. **Correct, no regression.**

No other call site of `ApplyTrayVisibility()` exists (`grep` confirms exactly two callers: `InitializeTrayState()` and `SettingsForm.cs:745`), and `_windowEverShown` has been fully removed from the codebase (`grep -rn "_windowEverShown" src/` returns no matches) — no dead/orphaned state left behind by the fix.

**Caveat (explicitly flagged per task instruction):** This trace is source-level only (no `dotnet` SDK in this sandbox, consistent with how this phase has been executed throughout). WinForms `Show()`/`WindowState`/message-loop timing — especially interaction with `ApplicationContext()` (no main form) during `--tray` startup — carries residual risk that only manifests on a live Windows session. The original 11-04 human-verify checkpoint approval predates both `e317323` and `a2f5c48`; this exact path has never been exercised on real Windows. See Human Verification Required below.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/RigToggle.Core/Models/AppSettings.cs` | `CloseMinimizesToTray`/`MinimizeToTray` bool fields | VERIFIED | Unchanged since prior verification |
| `src/RigToggle.Tests/JsonStoreTests.cs` | Round-trip + legacy-default tests | VERIFIED | Unchanged, 3 tests present |
| `src/RigToggle.App/MainForm.cs` | `SendToTray`, conditional `FormClosing`, `ApplyTrayVisibility` (broadened guard), `MainForm_Resize` | VERIFIED | `_windowEverShown` removed; guard now keyed on `wasVisible` (`notifyIcon.Visible` snapshot) — commit `a2f5c48` |
| `src/RigToggle.App/MainForm.Designer.cs` | `notifyIcon.Visible` default off, `Resize` wiring | VERIFIED | Unchanged |
| `src/RigToggle.App/SettingsForm.Designer.cs` | Two checkboxes + relayout | VERIFIED | Unchanged |
| `src/RigToggle.App/SettingsForm.cs` | Load/Save wiring + injected Action | VERIFIED | Unchanged |
| `src/RigToggle.App/Program.cs` | `mainForm.ApplyTrayVisibility` wired into `SettingsFormFactory` | VERIFIED | Unchanged |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `MainForm_FormClosing` | `AppSettings.CloseMinimizesToTray` | `_settingsStore.Load()` gate | WIRED | `MainForm.cs:400-405`, unchanged |
| `MainForm_Resize` | `SendToTray` | `MinimizeToTray` gate | WIRED | `MainForm.cs:442-445`, unchanged |
| `ApplyTrayVisibility` | `notifyIcon.Visible` | `CloseMinimizesToTray \|\| MinimizeToTray`, lockout-guarded on prior `notifyIcon.Visible` state | WIRED | `MainForm.cs:141-150` — re-verified fully with the new `wasVisible` guard, all four traced scenarios above |
| `SettingsForm.BtnSaveSettings_Click` | `MainForm.ApplyTrayVisibility` | injected `Action _applyTrayVisibility` | WIRED | `SettingsForm.cs:745`, unchanged |
| `Program.cs SettingsFormFactory` | `mainForm.ApplyTrayVisibility` | constructor argument | WIRED | `Program.cs:114`, unchanged |

### Data-Flow Trace (Level 4)

Not applicable in the classic sense (no dynamic list/API rendering) — the relevant "data flow" is the settings.json round-trip (Truths #1-#2) plus the live-apply guard logic (Truth #11), both confirmed by direct code read rather than a running app (no `dotnet` SDK in this sandbox).

### Behavioral Spot-Checks

SKIPPED — this phase's runtime behavior is WinForms message-loop/window-chrome interaction, not automatable without a Windows GUI session and the .NET SDK (neither available in this sandbox). Same constraint as the initial verification.

### Probe Execution

SKIPPED (no runnable entry points / no `scripts/*/tests/probe-*.sh` declared for this phase).

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|--------------|--------|----------|
| TRAY-01 | 11-01, 11-02, 11-03, 11-04 | Revises Phase 8's fixed close-to-tray behavior into an independent user preference (plus the new minimize-to-tray preference) | SATISFIED | All observable truths verified, including the previously-failing lockout-guard completeness (Truth #11), now closed |

No orphaned requirements: `REQUIREMENTS.md` lists TRAY-01 under Phase 8 (`67:| TRAY-01 | Phase 8 | Complete |`) with no separate Phase-11 entry, matching 11-CONTEXT.md's explicit statement that this phase revises TRAY-01 rather than adding a net-new REQ-ID. `grep -n "Phase 11" .planning/REQUIREMENTS.md` returns nothing, which is expected, not a gap.

### Anti-Patterns Found

None. Re-scanned `MainForm.cs` (the only file touched by the gap-closing commit) for `TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER` — zero matches. `_windowEverShown` fully removed with no leftover dead references anywhere in `src/`.

### Human Verification Required

The source-level trace closes the gap with high confidence, but WinForms window-chrome/message-loop behavior during `--tray` autostart genuinely cannot be fully confirmed without running on Windows. Flagging per task instruction:

### 1. Re-verify the CR-01 lockout-guard fix on real Windows

**Test:** Enable "Start with Windows" and `MinimizeToTray` only (leave `CloseMinimizesToTray` off), restart the machine (or launch with `--tray` manually) so the app autostarts hidden with the tray icon visible. Without ever left-clicking the tray icon to restore the window, right-click the tray icon → Settings → uncheck `MinimizeToTray` → Save.
**Expected:** The window is forced back into view (`Show()` + `WindowState = Normal`) rather than the app going fully invisible with no tray icon and no taskbar entry.
**Why human:** WinForms `Show()`/`WindowState`/message-loop timing during `--tray` autostart (particularly interaction with the parameterless `ApplicationContext()` used in that path) is not fully verifiable via static source read. This exact path — and the fix's actual on-screen behavior — has never been exercised on real Windows; the original 11-04 checkpoint approval predates both fix commits (`e317323` and `a2f5c48`).

### 2. Spot-check no regression in the original (already-approved) tray scenarios

**Test:** Re-run the original 11-04 checklist items (fresh-upgrade default, close-to-tray, minimize-to-tray, live icon appear/disappear on Settings-Save, tray-menu regression) on Windows, since the guard's internal logic changed even though its intended behavior for these cases is unchanged per the source trace above.
**Expected:** All behave identically to the 11-04-approved baseline (no forced-Show side effects in scenarios where the window was already visible or where no tray icon was ever shown).
**Why human:** Same as above — confirms the refactor from `_windowEverShown` to `wasVisible` didn't introduce a subtle timing regression in cases the source trace considers safe.

### Gaps Summary

No gaps remain. The one gap from the prior verification run — the CR-01 lockout-guard's `_windowEverShown` condition failing to cover a `--tray` autostart session where the user interacts exclusively through the tray context menu before disabling both tray preferences — is closed by commit `a2f5c48`, which replaces `_windowEverShown` with a `wasVisible` snapshot of `notifyIcon.Visible` taken immediately before the derived-visibility assignment. Re-tracing all four relevant scenarios (the exact gap reproduction, the original CR-01 case, the accepted D-10 case, and a visible-session Settings-Save) confirms the new guard fires exactly when it should and stays silent exactly when it should, with no dead references to the removed field.

Status is `human_needed` rather than `passed` solely because this sandbox has no Windows/`.NET` runtime to execute the fix and its neighbors live — the fix has been re-traced at full source-code fidelity but not re-tested on the target OS since being changed. Recommend running the two human-verification items above on the actual rig machine before considering Phase 11 fully closed.

---

_Verified: 2026-08-01_
_Verifier: Claude (gsd-verifier)_
