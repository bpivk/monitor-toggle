---
phase: 20-custom-toggle-switch-control
verified: 2026-08-10T19:05:00Z
status: passed
score: 7/7 must-haves verified
overrides_applied: 0
---

# Phase 20: Custom Toggle-Switch Control Verification Report

**Phase Goal:** The Rig/Normal mode action reads as a modern toggle switch instead of a plain button.
**Verified:** 2026-08-10T19:05:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | The Rig/Normal action on `MainForm` is a custom-drawn track+thumb `ToggleSwitch`, not a standard `Button` | VERIFIED | `src/RigToggle.App/Controls/ToggleSwitch.cs` defines `public sealed class ToggleSwitch : UserControl` with owner-drawn `OnPaint`. `MainForm.Designer.cs:278` — `this.Controls.Add(this.toggleSwitch);` occupies `btnToggle`'s former slot; zero `btnToggle`/stock-Button-toggle references remain anywhere in `src/` (`grep -rn 'btnToggle\|lblMode\|TogglePx\|ModeLabelHeightPx\|BtnToggle_' src/ --include=*.cs --include=*.csproj` → 0 hits, confirmed independently). Rig check #1 (Round 2): PASS. |
| 2 | Off/On/Indeterminate states differ by both fill-presence and thumb position, not color alone | VERIFIED | `ToggleSwitch.cs` `OnPaint`: Off is unfilled/hollow-outlined (track fill only on hover/press), On/Indeterminate are solid-filled; thumb position computed per-state via a `switch` expression (`Off` → left, `On` → right, `Indeterminate` → exact mid-track). Track and thumb are separate draw calls (`FillEllipse`×1, `DrawEllipse`×1, `AddEllipse`×0 — confirmed by grep). Rig check #2 (Round 2): PASS. |
| 3 | Track size derives from `ClientSize.Height`, never `ClientSize.Width` | VERIFIED | `trackH = h * TrackHeightFraction; trackW = trackH * TrackAspectRatio;` — confirmed via `grep -c 'trackW = trackH'` = 1, and audit greps for `TrackAspectRatio * w`/`w * TrackHeightFraction` return 0. |
| 4 | Switch is keyboard-reachable (tab stop, pill focus ring, Space/Enter raises the same `ActionRequested` as click) | VERIFIED | `TabStop = true`, `ProcessCmdKey` handles `Keys.Space`/`Keys.Return`, pill-shaped focus ring drawn only `if (Focused)` using a radius-matched `GraphicsPath`. Post-review fix (WR-02, commit `c75329f`) adds an `_actionKeyDown` guard so autorepeat no longer re-fires the event on every `WM_KEYDOWN` while held — confirmed present in the code (`OnKeyUp` clears the flag, `OnLeave` clears it defensively). Rig check #4 (Round 2): PASS. |
| 5 | The full row (label + track) is one clickable/focusable surface | VERIFIED | `OnClick` raises `ActionRequested` for the whole `UserControl`; `Cursor = Cursors.Hand` set control-wide in the constructor. `GetPreferredSize` sizes the control to its own content (label + gap + track + ring margin), added during the Round 1 rig fix. |
| 6 | `ToggleSwitch` is presentational-only — no controller/store/theme-provider/`SystemEvents` reference | VERIFIED | `grep -nE 'IMonitorController|ToggleOrchestrator|ISettingsStore|IThemeProvider|MonitorConfirmDialog|MessageBox|SystemEvents' src/RigToggle.App/Controls/ToggleSwitch.cs` excluding comment lines → 0 hits (re-confirmed independently). |
| 7 | `ThemeApplier.ThemeToggleSwitch` sets every color the switch paints with, for both themes, reached from both `MainForm` theming call sites through one funnel | VERIFIED | `ThemeApplier.cs` has `public static void ThemeToggleSwitch(...)` setting `BackColor` + nine color properties. `ApplyDashboardTheming()` calls it exactly once; both `OnThemeChanged` and `InitializeTrayState()` reach `ApplyDashboardTheming()` — confirmed via `awk`-scoped greps (all = 1), matching Pitfall 1's two-call-site rule. |

**Score:** 7/7 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/RigToggle.App/Controls/ToggleSwitch.cs` | `ToggleSwitchState` enum + owner-drawn `ToggleSwitch` UserControl, keyboard activation, nine theme properties | VERIFIED | Exists, compiles, all structural greps (class declaration, enum, `ActionRequested?.Invoke` count=2, nine `[DesignerSerializationVisibility(Hidden)]` properties) pass. |
| `src/RigToggle.App/ThemeApplier.cs` | `ThemeToggleSwitch(ToggleSwitch, bool dark)` | VERIFIED | Method present, one call site, all locked color literals present. |
| `src/RigToggle.App/MainForm.cs` | `ToggleSwitch_ActionRequested` (verbatim port of `BtnToggle_Click` + CR-01 lease fix), switch-driven `RefreshUi()`, `ToggleRowHeightPx` layout | VERIFIED | Handler present; all four user-protection gates (unknown-mode refusal, WR-01/Settings-unconfigured refusal, DISPLAY-07 confirmation, CORE-04 checklist, `ToggleInProgressException`) confirmed present in the handler body. `RefreshUi()` calls `toggleSwitch.SetState()` exactly twice (Indeterminate branch + On/Off ternary) — no third render path. |
| `src/RigToggle.App/MainForm.Designer.cs` | `toggleSwitch` field/instantiation/`Controls.Add`/`ActionRequested` wiring in `btnToggle`'s former slot | VERIFIED | `Controls.Add` sequence confirmed in exact expected order: `tileStrip`, `lblNoMonitors`, `btnIdentify`, `toggleSwitch`, `btnSettings`. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `ThemeApplier.cs` | `Controls/ToggleSwitch.cs` | `ThemeToggleSwitch` writes every color property the control paints with | WIRED | `toggleSwitch.OnColor` and 8 other properties assigned; `Invalidate()` called. |
| `MainForm.Designer.cs` | `MainForm.cs` | `toggleSwitch.ActionRequested +=` wired to `ToggleSwitch_ActionRequested` | WIRED | Confirmed via grep, and handler exists in `MainForm.cs`. |
| `MainForm.cs` | `ThemeApplier.cs` | `ApplyDashboardTheming()` — single helper both theming call sites invoke | WIRED | `OnThemeChanged` → `ApplyDashboardTheming` (1 hit); `InitializeTrayState()` → `ApplyDashboardTheming` (1 hit); `ApplyDashboardTheming` → `ThemeApplier.ThemeToggleSwitch` (1 hit, sole call site solution-wide). |
| `MainForm.cs` | `RigToggle.App.Controls.ToggleSwitchState` | `RefreshUi` maps mode onto the switch | WIRED | `SetState(ToggleSwitchState.Indeterminate)` and `SetState(isInRigMode ? On : Off)` both present, count=2 (no drift path). |
| `MainForm.cs` | `RigToggle.Core.ToggleOrchestrator` | `ToggleSwitch_ActionRequested` is the sole GUI mutation path, now lease-guarded around its confirm dialog | WIRED | CR-01 fix confirmed in diff: `TryAcquireMonitorAccess()` lease acquired before `MonitorConfirmDialog.ShowDialog`, released (via `using`) before `ToggleToRigMode()`/`ToggleToNormalMode()` are called — matches `OnTileAction`'s established pattern. `DeactivateMonitors`/`ActivateMonitors` call-site count unchanged from Phase 19 (declaration + 3 known callers). |

### Behavioral Spot-Checks

Not applicable in the traditional sense — this is a Windows GUI app that cannot run headless in this Linux build environment. The project's own verification strategy substitutes a blocking rig-hardware checkpoint (Plan 03 Task 2) for runtime behavioral checks. That checkpoint was executed by the human developer, not the executor or this verifier, and returned 11/11 PASS on Round 2 (after 2 fix rounds). This verifier independently confirmed: (a) build exits 0 with 0 errors, (b) `dotnet test` reports 81/81 passing, (c) the code-review fix commit (`c75329f`) applied after Round 2's rig approval is present in the working tree and matches its stated intent by direct diff inspection.

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Solution builds clean | `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` | `Build succeeded. 0 Warning(s), 0 Error(s)` | PASS |
| Existing test suite has no regressions | `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` | `Failed: 0, Passed: 81, Total: 81` | PASS |
| Retirement is complete (independent re-check, not trusting SUMMARY) | `grep -rn 'btnToggle\|lblMode\|TogglePx\|ModeLabelHeightPx\|BtnToggle_' src/ --include=*.cs --include=*.csproj \| wc -l` | `0` | PASS |
| CR-01 lease-then-release ordering (independent re-check) | `awk` scope of `ToggleSwitch_ActionRequested` inspected line-by-line | lease acquired at line 95, `using (lease)` wraps only the dialog/DontAskAgain block, `ToggleToRigMode()` called outside that block at line 117 | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| THEME-08 | 20-01, 20-02, 20-03 | The Rig/Normal toggle is a custom-drawn toggle-switch control (track + thumb), remaining distinguishable by shape/position, not color alone | SATISFIED | REQUIREMENTS.md marks THEME-08 `[x]` and maps it solely to Phase 20. All supporting truths above (1, 2, 3, 6) directly satisfy the requirement text; rig checks #1 and #2 (Round 2) independently confirm the visual/behavioral claim on real hardware. No orphaned requirements found for Phase 20 in REQUIREMENTS.md. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | No `TBD`/`FIXME`/`XXX`/unreferenced debt markers found in any Phase 20-touched file | — | None — clean |

No blocker or warning-level anti-patterns found in the files this phase touched. `ToggleSwitch.OnPaint` and `LayoutDashboard()` both remain free of bare multi-digit pixel literals (re-confirmed independently via the same `awk`+`grep` audits Plan 03 ran). No placeholder/stub copy, no empty handlers, no hardcoded-empty render paths.

### Human Verification Required

None outstanding. The phase's one blocking human-verify checkpoint (Plan 03 Task 2, rig-hardware verification of all 11 items) was already executed by the developer and returned **APPROVED — 11/11 PASS** on Round 2, after two fix rounds (layout sizing/focus-ring clipping, then action-row merge/Identify corner rounding). This satisfies the phase's `checkpoint:human-verify` gate.

**Documented risk-accepted note (not blocking):** After Round 2's rig approval, a code review (`20-REVIEW.md`) found 1 Critical (CR-01: missing exclusive-access lease around the switch's confirm dialog, closing a hotkey/tray-toggle race) + 3 Warning findings (WR-01: focus-ring clipping regression, WR-02: keyboard-autorepeat re-firing, WR-03: unguarded `_settingsStore.Load()`). All four were fixed directly in commit `c75329f` (verified present in the code by this verifier, matching the stated intent by direct diff inspection — build 0 errors/warnings, 81/81 tests passing after the fix). These four fixes were **not** re-verified against real rig hardware a third time; the user explicitly opted to close the phase without a third rig round, judging the fixes as behaviorally invisible in normal use (CR-01 only matters under a specific hotkey-during-dialog race timing window; WR-02 only matters if Space/Enter is held rather than tapped rather than released promptly). This is an explicit, already-made user risk-acceptance decision, not a phase gap — recorded here for traceability, not as a blocking item.

### Gaps Summary

None. All 7 must-have truths verified, all artifacts exist/are substantive/are wired, all key links verified, the phase's requirement (THEME-08) is satisfied and marked complete in REQUIREMENTS.md with no orphaned requirements, the blocking rig checkpoint returned 11/11 PASS, and the post-rig code-review fixes are present and match their stated intent by independent code inspection. The one open item — a third rig round for the four post-review fixes — is a user-accepted, documented, non-blocking risk (see note above), not a verification gap.

---

_Verified: 2026-08-10T19:05:00Z_
_Verifier: Claude (gsd-verifier)_
