---
phase: 21-accent-color-reading-live-update
verified: 2026-08-11T08:59:39Z
status: passed
score: 14/14 must-haves verified
overrides_applied: 0
---

# Phase 21: Accent-Color Reading & Live Update Verification Report

**Phase Goal:** Key interactive elements reflect the user's actual live Windows accent color instead of a fixed palette.
**Verified:** 2026-08-11T08:59:39Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `IThemeProvider` exposes a live `Color AccentColor` and `AccentColorChanged` event, `CurrentTheme`/`ThemeChanged` untouched | ✓ VERIFIED | `src/RigToggle.Core/Abstractions/IThemeProvider.cs` — both new members present, `AppTheme CurrentTheme { get; }` / `event EventHandler? ThemeChanged;` unchanged |
| 2 | D-01: `WindowsThemeProvider` resolves accent registry-primary (`HKCU\...\DWM\AccentColor`), DWM fallback only when absent/unreadable, never throws | ✓ VERIFIED | `WindowsThemeProvider.cs` `ReadAccentColorFromRegistry`/`ReadAccentColorFromDwm`/`ReadAccentColor`; both reads fully try/catch-wrapped with non-throwing exits (`return null;` x2, `return SystemColors.Highlight;` x2, independently re-grepped) |
| 3 | A genuine accent change raises `AccentColorChanged` exactly once; re-selecting the same color raises nothing | ✓ VERIFIED | Diff-then-raise block under `_accentLock` in `OnUserPreferenceChanged` (value-equality on packed ARGB int); contract test `RaiseAccentColorChanged_InvokesSubscriberExactlyOnce_WithUpdatedAccentColor` passes (re-ran: 82/82 total) |
| 4 | D-02: exactly one `SystemEvents.UserPreferenceChanged` subscription for the whole app | ✓ VERIFIED | Independently re-ran `grep -rn 'SystemEvents.UserPreferenceChanged' src/` → exactly 2 hits (`+=` line 66, `-=` line 189), both in `WindowsThemeProvider.cs` |
| 5 | `FakeThemeProvider` satisfies the extended interface; contract test proves fire-once with value already assigned | ✓ VERIFIED | `FakeThemeProvider.cs` `RaiseAccentColorChanged` assigns before invoking (verified by reading the file); test asserts `invocationCount==1` and value equality before/after |
| 6 | All five D-04 consumers (`MonitorTile.AccentColor`/`FocusRingColor`, `ToggleSwitch.OnColor`/`FocusRingColor`, `MainForm.AccentColor`) source color from `IThemeProvider.AccentColor`, hardcoded placeholder removed (ROADMAP SC1) | ✓ VERIFIED | `ThemeApplier.cs` 4 assignments read `accentColor` param; `MainForm.cs:196` `private Color AccentColor => _themeProvider.AccentColor;`; `grep -c 'Color.FromArgb(0, 90, 158)' MainForm.cs` → 0 |
| 7 | A live accent change repaints via the existing `OnThemeChanged` → `ApplyDashboardTheming()` funnel, no new handler/call site, no restart (ROADMAP SC2) | ✓ VERIFIED | `MainForm.cs:127` `_themeProvider.AccentColorChanged += OnThemeChanged;` (same handler as `ThemeChanged`); `ApplyDashboardTheming()` forwards `AccentColor` to both `ThemeMonitorTile`/`ThemeToggleSwitch`; `OnAccentColorChanged` grep → 0 hits; human rig checks 6/7/8 report PASS (see #13) |
| 8 | Accent-tinted set frozen at exactly 5 — no scope creep (title bar, dialogs, `MonitorTile.cs`/`ToggleSwitch.cs` internals untouched) | ✓ VERIFIED | `git diff --stat 9836311 -- DwmTitleBar.cs MonitorTile.cs ToggleSwitch.cs` → empty; `DWMWA_CAPTION_COLOR` grep → 0; `MonitorConfirmDialog.cs`/`SettingsForm.cs` `AccentColorChanged` refs → 0 |
| 9 | Accent color is theme-independent — dark/light ternary removed from all four `ThemeApplier` accent assignments | ✓ VERIFIED | `awk`-scoped `ThemeMonitorTile`/`ThemeToggleSwitch` bodies contain 0 `SystemColors.Highlight` ternary hits; both take `accentColor` as a plain parameter |
| 10 | Full solution builds and 82-test suite passes with no regression | ✓ VERIFIED | Independently re-ran: `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true /t:Rebuild` → `0 Error(s)`, `4 Warning(s)` (pre-existing `xUnit1031` in `ToggleOrchestratorTests.cs`); `dotnet test` → `Failed: 0, Passed: 82, Total: 82` |
| 11 | Static audits (source-swap completeness, D-02 discipline, funnel lockstep, D-04 exactness, byte-order/read-safety) all pass with recorded evidence | ✓ VERIFIED | Independently re-ran the key audit commands (subscription count, implementer count, placeholder-literal survivors, `AppSettings` accent ref, `DwmAccentColor.cs` absence) — all match SUMMARY-recorded output exactly |
| 12 | D-03: on real rig hardware, accent-tinted elements sample to the exact same hex as Settings > Personalization > Colors for the user's real custom blue accent, title-bar-accent toggle ON | ✓ VERIFIED (human-confirmed) | Rig check 4 in `21-03-SUMMARY.md`; user's blanket "everything passes" report covers this. Note: raw hex values requested by the plan were not individually transcribed — see caveat below |
| 13 | On real rig hardware, saturated primary accents (pure red, pure blue) resolve without an R/B swap, numerically settling the byte-order contradiction | ✓ VERIFIED (human-confirmed) | Rig check 3 — the user was asked to confirm this *specifically*, separate from the blanket statement, and reported "It worked correctly" (red rendered red, blue rendered blue), closing the `21-RESEARCH.md` self-contradiction in favor of the implemented ABGR-primary registry extraction |
| 14 | D-05: phase not marked done without the user personally running the rig-verification pass and reporting PASS/FAIL | ✓ VERIFIED | `21-03-SUMMARY.md` "Task 2 — Rig Verification Verdict": user personally ran the app on real Windows 11 hardware via the blocking `checkpoint:human-verify` gate and reported PASS |

**Score:** 14/14 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/RigToggle.Core/Abstractions/IThemeProvider.cs` | Extended contract: `Color AccentColor { get; }` + `AccentColorChanged` | ✓ VERIFIED | Present, doc comment extended, existing members untouched |
| `src/RigToggle.Windows/NativeMethods.cs` | `DwmGetColorizationColor` P/Invoke sibling to `DwmSetWindowAttribute` | ✓ VERIFIED | Declared once at line 148, `internal`, correct signature |
| `src/RigToggle.Windows/WindowsThemeProvider.cs` | Registry-primary/DWM-fallback read + second diff-block in existing handler | ✓ VERIFIED | Read in full; separate `_accentLock`, independent diff block, `Log` line present |
| `src/RigToggle.Tests/Doubles/FakeThemeProvider.cs` | `AccentColor` setter, event, `RaiseAccentColorChanged` with assign-then-invoke | ✓ VERIFIED | Read in full — matches |
| `src/RigToggle.Tests/ThemeProviderContractTests.cs` | Contract test for fire-once + updated value | ✓ VERIFIED | Test present and passing |
| `src/RigToggle.App/MainForm.cs` | `AccentColorChanged` wired into `OnThemeChanged`; `AccentColor` live pass-through | ✓ VERIFIED | Lines 127, 196 confirmed by direct read |
| `src/RigToggle.App/ThemeApplier.cs` | `ThemeMonitorTile`/`ThemeToggleSwitch` accept `Color accentColor` | ✓ VERIFIED | Both signatures confirmed, all 4 assignments confirmed |
| `.planning/phases/21-accent-color-reading-live-update/21-03-SUMMARY.md` | Regression + 5 static audits + rig verdict for all 3 SCs | ✓ VERIFIED (partial evidentiary detail) | Present, all sections populated; per-check hex-value transcription for checks 2/3/4 is narrative rather than raw numeric (self-disclosed by the summary itself, not concealed) |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `WindowsThemeProvider.cs` | `HKCU\...\DWM\AccentColor` | `Registry.CurrentUser.OpenSubKey` in `ReadAccentColorFromRegistry` | ✓ WIRED | Confirmed by direct read |
| `WindowsThemeProvider.cs` | `NativeMethods.cs` | `NativeMethods.DwmGetColorizationColor` direct call, same assembly | ✓ WIRED | 1 call site, no façade |
| `WindowsThemeProvider.cs` | `AccentColorChanged` subscribers | diff-then-raise inside `OnUserPreferenceChanged` | ✓ WIRED | Independent second block, own lock |
| `MainForm.cs` | `IThemeProvider.AccentColor` | `AccentColor => _themeProvider.AccentColor` pass-through | ✓ WIRED | Confirmed line 196 |
| `MainForm.cs` | `OnThemeChanged` → `ApplyDashboardTheming` | `AccentColorChanged += OnThemeChanged;` | ✓ WIRED | Confirmed line 127; same handler, no bypass path |
| `MainForm.cs` | `ThemeApplier.cs` | `ApplyDashboardTheming` forwards `AccentColor` to both accent consumers | ✓ WIRED | `ThemeApplier.ThemeMonitorTile(tile, IsDark, AccentColor)` / `ThemeToggleSwitch(toggleSwitch, IsDark, AccentColor)` confirmed lines 1035/1040 |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full solution rebuild is clean at documented baseline | `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true /t:Rebuild` | `0 Error(s)`, `4 Warning(s)` (pre-existing xUnit1031) | ✓ PASS |
| Test suite green, no regression | `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true` | `Failed: 0, Passed: 82, Total: 82` | ✓ PASS |
| Single UserPreferenceChanged subscription (D-02) | `grep -rn 'SystemEvents.UserPreferenceChanged' src/` | 2 hits, both in `WindowsThemeProvider.cs` (`+=`/`-=`) | ✓ PASS |
| No scope-creep consumer | `grep -c AccentColorChanged MonitorConfirmDialog.cs SettingsForm.cs` | `0`, `0` | ✓ PASS |
| Control files byte-for-byte unmodified vs. phase base | `git diff --stat 9836311 -- DwmTitleBar.cs MonitorTile.cs ToggleSwitch.cs` | empty | ✓ PASS |
| Windows-only DWM/registry read behavior itself (actual OS call) | N/A — this environment has no Windows registry/DWM | Not runnable here | ? SKIP (delegated to, and completed by, the rig checkpoint) |

### Probe Execution

Not applicable — this phase declares no `scripts/*/tests/probe-*.sh` probes; verification is via `dotnet build`/`dotnet test` plus a blocking human rig checkpoint, both covered above.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| THEME-07 | 21-01, 21-02, 21-03 | Interactive elements pick up the live Windows accent color, updating live if the user changes it while the app is running | ✓ SATISFIED | All 14 truths above; code independently re-verified; human rig-confirmed on real hardware. **Note:** `REQUIREMENTS.md` line 34 still shows `THEME-07` as an unchecked `[ ]` checkbox despite `ROADMAP.md` marking Phase 21 `[x]` complete — a documentation-sync gap, not a functional one (see Anti-Patterns below) |

No orphaned requirements — `REQUIREMENTS.md`'s Phase 21 mapping table (line 62) lists only `THEME-07`, and all three plans declare it in frontmatter.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `.planning/REQUIREMENTS.md` | 34 | `THEME-07` checkbox left unchecked (`[ ]`) despite Phase 21 being marked complete in `ROADMAP.md` and fully implemented in code | ℹ️ Info | Documentation-tracking gap only; does not affect the shipped behavior. Recommend flipping to `[x]` as part of phase closeout |
| `.planning/phases/21-accent-color-reading-live-update/21-03-SUMMARY.md` | Task 2 verdict section | Rig-verification evidence is a blanket "everything passes" plus one specifically-elicited confirmation (byte-order check), rather than the plan's required per-check PASS/FAIL notes and raw hex values for checks 2/3/4 and separate `debug.log` quotes for checks 6/7/8 | ⚠️ Warning | The summary discloses this gap itself rather than fabricating numbers — a positive signal, not concealment. D-05's literal requirement ("user personally runs it and reports PASS/FAIL") is satisfied; the plan's *more detailed* evidentiary bar for individual checks was not fully met. Does not block phase completion since no functional defect was found and the decisive byte-order risk was explicitly (not just implicitly) confirmed |

No `TBD`/`FIXME`/`XXX` markers, no placeholder returns, no empty handlers, and no hardcoded-empty data found in any file modified by this phase (`IThemeProvider.cs`, `WindowsThemeProvider.cs`, `NativeMethods.cs`, `FakeThemeProvider.cs`, `ThemeProviderContractTests.cs`, `MainForm.cs`, `ThemeApplier.cs`).

### Human Verification Required

None outstanding. The phase's one item that structurally requires human observation (real Windows accent-color hardware behavior, D-05) was already gated as a blocking `checkpoint:human-verify` task during phase execution and received a user-reported PASS, including an explicitly separate confirmation of the decisive byte-order check. No new human verification is needed from this pass.

### Gaps Summary

No functional gaps found. Every source-level must-have was independently re-verified against the actual codebase (not just SUMMARY claims) — the extended `IThemeProvider` contract, the registry-primary/DWM-fallback read with correct non-throwing exits, the single-subscription/single-diff-block wiring, all five D-04 consumers repointed with the dark/light ternary removed, and the full 82-test regression suite. Build and tests were re-run directly in this session rather than trusted from the summary, and every audit grep from Plan 03 was independently reproduced with matching output.

Two non-blocking observations are recorded for the developer's awareness:
1. `REQUIREMENTS.md`'s `THEME-07` checkbox was never flipped to `[x]` — a bookkeeping gap, not a functional one.
2. The rig-verification evidence in `21-03-SUMMARY.md` is less granular (blanket PASS + one specific confirmation) than the plan's acceptance criteria asked for (12 individually-transcribed checks with raw hex values). This is honestly self-disclosed in the summary rather than papered over, and does not indicate a functional defect — it indicates the phase's evidentiary record is thinner than ideal for a scenario like a future accent-color regression investigation.

---

*Verified: 2026-08-11T08:59:39Z*
*Verifier: Claude (gsd-verifier)*
