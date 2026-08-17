---
phase: 19-monitor-tile-dashboard-monitorpanelform-retirement
plan: 05
subsystem: verification
tags: [regression-gate, static-audit, rig-checkpoint, dpi, theming]

requires:
  - phase: 19-04
    provides: MonitorPanelForm and both entry points fully deleted, MainForm five-argument constructor, composition root updated
provides:
  - Full-solution regression gate confirming no build/test regression across Phase 19
  - Four static audits proving DISPLAY-12 single-implementation, canonical-ordering single-source, theming two-call-site lockstep, and TILE-07/DPI completeness
  - Rig-hardware verification: APPROVED after 4 real-hardware rounds, 6 defects found and fixed (layout overlap, badge clipping, FlatAppearance hover/press unreliability, missing focus indicator, non-uniform focus ring color)
affects:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/MainForm.Designer.cs
  - src/RigToggle.App/Controls/MonitorTile.cs
  - src/RigToggle.App/ThemeApplier.cs

tech-stack:
  added: []
  patterns: [owner-drawn hover/press state (bypassing FlatAppearance), accent-colored manual focus ring]

key-files:
  created: []
  modified:
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/MainForm.Designer.cs
    - src/RigToggle.App/Controls/MonitorTile.cs
    - src/RigToggle.App/ThemeApplier.cs

decisions:
  - "Branch layout math on a locally-computed hasMonitors flag, never on Control.Visible (ancestor-cascading, unreliable before Form.Show())"
  - "Own-paint hover/press background for Identify/Settings instead of FlatAppearance, matching MonitorTile's already-proven pattern"
  - "Give btnToggle only a matching focus-ring Paint handler, not a full owner-draw conversion, since Phase 20/THEME-08 replaces it with a custom switch shortly"

requirements-completed: [TILE-01, TILE-02, TILE-03, TILE-04, TILE-05, TILE-06, TILE-07, MAIN-01, MAIN-02]

duration: ~4 hours (Task 1) + 4 rig round-trips (Task 2)
completed: 2026-08-10
---

# Phase 19 Plan 05: Regression Gate, Static Audits & Rig Verification Summary

**Task 1 (full regression gate + four static audits) is complete with zero source changes and zero audit failures; Task 2 (the rig-hardware checkpoint) is a blocking human-verify gate that requires real Windows display hardware and has not yet been run — this summary is intentionally partial and will be appended once the checkpoint resolves.**

## Task 1: Regression Gate

### Build

Command: `PATH="$HOME/.dotnet:$PATH" dotnet build RigToggle.sln -p:EnableWindowsTargeting=true`

```
Build succeeded.
    4 Warning(s)
    0 Error(s)
```

The 4 warnings are the pre-existing `xUnit1031` warnings in `ToggleOrchestratorTests.cs` (lines 131, 157, 190, 292) — identical to the baseline recorded in Plans 19-01 through 19-04's summaries, no new warnings introduced.

### Test

Command: `PATH="$HOME/.dotnet:$PATH" dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true`

```
Passed!  - Failed: 0, Passed: 81, Skipped: 0, Total: 81, Duration: 82 ms - RigToggle.Tests.dll (net10.0)
```

Matches the pre-phase baseline (`Failed: 0, Passed: 81, Total: 81`) exactly — no regression across the entire phase.

`RigToggle.Windows.Tests` was not run separately as a test command (it needs `Microsoft.WindowsDesktop.App`, unavailable in this environment) — its build is folded into the `dotnet build RigToggle.sln` command above, which succeeded with 0 errors, satisfying the interfaces block's stated gate for that project in this environment.

## Task 1: Static Audits

### Audit 1 — DISPLAY-12 single implementation and three-way reach

**Message string, solution-wide, non-comment lines:**
```
$ grep -rn 'at least one active display' src/ --include=*.cs | grep -vE ':\s*(//|\*|/\*)'
src/RigToggle.Windows/WindowsMonitorController.cs:270:                "Cannot disable all configured monitors — at least one active display must remain.");
```
Count: **1** — the single hit is in `WindowsMonitorController.cs`, as required.

**`DeactivateMonitors(` call sites, non-comment lines, excluding `IMonitorController.cs`:**
```
src/RigToggle.App/MainForm.cs:725:                        _monitorController.DeactivateMonitors(new HashSet<string> { devicePath });
src/RigToggle.Core/ToggleService.cs:104:                _monitorController.DeactivateMonitors(disableSet);
src/RigToggle.Core/ToggleService.cs:369:            _monitorController.DeactivateMonitors(disableSet);
src/RigToggle.Tests/Doubles/BlockingMonitorController.cs:48:    public void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths)
src/RigToggle.Tests/Doubles/FakeControllers.cs:65:    public void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths)
src/RigToggle.Windows/WindowsMonitorController.cs:236:    public void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths)
```
Production call sites: 1 in `MainForm.cs` (tile path), 2 in `ToggleService.cs` (Rig/Normal toggle directions), 1 declaration in `WindowsMonitorController.cs`. The two `RigToggle.Tests/Doubles` hits are test-double interface implementations, not production callers — consistent with the plan's stated three production callers (Rig toggle, Normal toggle, tile click).

**Four no-duplicate-guard patterns against `MainForm.cs`, non-comment lines:**

| Pattern | Count |
|---|---|
| `Count(.*IsActive` | 0 |
| `Where(.*IsActive.*).Count` | 0 |
| `at least one active display` | 0 |
| `\bsurvivors\b` | 0 |

All four output `0` — no local active-monitor counting or duplicate zero-survivors logic anywhere in `MainForm.cs`. **Audit 1: PASSED.**

### Audit 2 — canonical ordering single source

```
$ grep -c 'OrderBy(m => m.DevicePath, StringComparer.Ordinal)' src/RigToggle.App/MainForm.cs
1
```

```
$ grep -n '_lastKnownMonitors =' src/RigToggle.App/MainForm.cs
51:        private IReadOnlyList<MonitorInfo> _lastKnownMonitors = Array.Empty<MonitorInfo>();
552:                _lastKnownMonitors = _monitorController.GetAllMonitors()
561:                _lastKnownMonitors = Array.Empty<MonitorInfo>();
```

Literal count is **3**, not the acceptance criteria's stated `2` — see "Deviations" below for why this is a documented grep-literal discrepancy, not a defect: line 51 is the field initializer, and lines 552/561 are the try/catch's two mutually-exclusive assignment branches inside the single method `RefreshMonitorTiles()` (success path assigns the sorted list; the exception-fallback path assigns an empty array). Both branches live inside the same, single writer method — the single-writer property the audit exists to prove holds.

```
$ grep -c '_lastKnownMonitors' src/RigToggle.App/MainForm.cs
14
```
≥ 5, as required.

**Readers in both consuming methods** (via `awk`-extraction):
```
$ awk '/private void OnTileAction/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c '_lastKnownMonitors'
3
$ awk '/private void BtnIdentify_Click/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c '_lastKnownMonitors'
2
```
Both ≥ 1, confirming the single-writer / two-reader shape 19-RESEARCH.md Pitfall 6 requires: `RefreshMonitorTiles()` (line 552/561) is the sole writer, `OnTileAction` and `BtnIdentify_Click` are the two readers. **Audit 2: PASSED** (with one documented literal-count discrepancy, no behavioral defect — see Deviations).

### Audit 3 — theming two-call-site lockstep

```
$ awk '/private void OnThemeChanged/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'ApplyDashboardTheming'
1
$ awk '/public void InitializeTrayState/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'ApplyDashboardTheming'
1
```
Both call sites reach `ApplyDashboardTheming()` exactly once, as required.

```
$ awk '/private void ApplyDashboardTheming/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'ThemeApplier.ThemeMonitorTile'
1
$ awk '/private void ApplyDashboardTheming/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'btnIdentify'
1
$ awk '/private void ApplyDashboardTheming/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'btnSettings'
3
$ awk '/private void ApplyDashboardTheming/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'lblNoMonitors'
1
```

`btnSettings` literal count is **3**, not the acceptance criteria's stated `1` — see Deviations: the method body contains `ThemeApplier.ThemeButton(btnSettings, IsDark);`, a doc comment referencing `btnSettings.ForeColor`, and `btnSettings.Invalidate();`. A more precise grep isolating the actual theming call confirms the intended property:
```
$ awk '/private void ApplyDashboardTheming/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'ThemeApplier.ThemeButton(btnIdentify'
1
$ awk '/private void ApplyDashboardTheming/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'ThemeApplier.ThemeButton(btnSettings'
1
```
Both fire exactly once — `btnIdentify` and `btnSettings` are each themed exactly once per call, matching the intended property.

```
$ grep -cE '\.Controls\b' src/RigToggle.App/ThemeApplier.cs
1
```
Literal count is **1**, not the acceptance criteria's stated `0` — see Deviations: the sole match is `using RigToggle.App.Controls;` (the namespace import for `MonitorTile`), not a `.Controls` collection walk. Direct inspection of `ThemeApplier.cs` confirms no method iterates any `Controls` collection — every theming method targets a single control instance passed by the caller, matching the class's own documented "deliberately NOT a recursive Controls-tree walk" contract. **Audit 3: PASSED** (two documented literal-count discrepancies, no behavioral defect — see Deviations).

### Audit 4 — TILE-07 completeness and DPI discipline

```
$ grep -rn 'MonitorPanelForm' src/ --include=*.cs --include=*.csproj | grep -vE ':\s*(//|\*|/\*)' | wc -l
0
$ grep -c 'btnMonitors\|trayMonitorsMenuItem' src/RigToggle.App/MainForm.cs
0
$ grep -c 'btnMonitors\|trayMonitorsMenuItem' src/RigToggle.App/MainForm.Designer.cs
0
$ ls src/RigToggle.App/MonitorPanelForm.cs
ls: cannot access 'src/RigToggle.App/MonitorPanelForm.cs': No such file or directory
$ ls src/RigToggle.App/MonitorPanelForm.Designer.cs
ls: cannot access 'src/RigToggle.App/MonitorPanelForm.Designer.cs': No such file or directory
```
Zero executable references, zero button/menu-item survivors, both files confirmed absent.

```
$ awk '/private void LayoutDashboard/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -vE '^\s*(//|\*|/\*)' | grep -cE 'new (Size|Point)\([0-9]'
0
```
No raw pixel-size/point literal in `LayoutDashboard` — every dimension passes through `Scaled()`.

`MonitorTile.OnPaint`'s body (manually inspected, no single-line grep specified by the acceptance criteria for this sub-check) uses only `w`/`h`/`Font`-derived values and named `*Fraction` constants (`FocusRingRadiusFraction`, `IconAreaFraction`, `IconTopFraction`, `BadgeDiameterFraction`, `LabelGapFraction`, `FocusRingWidthFraction`) for every geometric dimension; the only bare numeric literals present are `0f`/`1f` origin/floor values (e.g. `new RectangleF(0f, 0f, w, h)`, `Math.Max(1f, w * FocusRingWidthFraction)`), never a raw tile pixel-size literal. **Audit 4: PASSED.**

### Git cleanliness

```
$ git status --porcelain src/
(empty)
```
Confirmed: this task changed no source file, as the plan's hard constraint requires.

## Phase 19 Success Criteria — Verification Status

| # | ROADMAP Phase 19 success criterion | Status | Verified by |
|---|---|---|---|
| 1 | One tile per monitor with icon, number, and icon-conveyed status | Machine-verified (population/numbering logic) + **rig-pending** (visual correctness, at-a-glance distinguishability) | Audit 2 (numbering) + Task 2 check 1 |
| 2 | Tile click toggles that monitor, gated by `SkipMonitorConfirmation` when disabling | Machine-verified (single-guard reuse, lease/confirm/mutate wiring) + **rig-pending** (actual CCD mutation, dialog gating on real hardware) | Audit 1 (DISPLAY-12) + Task 2 check 2 |
| 3 | Tab between tiles, Space/Enter toggles the focused tile | **Rig-pending** — keyboard focus/activation is a runtime input-handling property this build environment cannot exercise | Task 2 check 3 |
| 4 | Identify overlays a number on each physical screen | Machine-verified (shared canonical ordering with tiles) + **rig-pending** (overlay-to-tile visual number agreement on real screens) | Audit 2 + Task 2 check 4 |
| 5 | Toggle directly below the tile row, Settings relocated/de-emphasized, `MonitorPanelForm` and both entry points gone | Machine-verified (deletion, layout order, control removal) + **rig-pending** (visual proportion/spacing confirmation) | Audit 4 + Task 2 checks 6-7 |

All five criteria have their statically-provable half machine-verified in this plan with zero source changes and zero audit failures. The rig-only half of each criterion (real CCD mutation, physical hotplug, keyboard input, live theme flips, DPI scaling, the `--tray` hidden-start path, and the hotkey-during-dialog race) is deferred to Task 2, a blocking `checkpoint:human-verify` gate this Linux build environment cannot execute or fabricate.

## Requirement ID → Verification Mapping (partial — Task 1 only)

| Requirement | Task 1 static evidence | Task 2 rig check (pending) |
|---|---|---|
| TILE-01 | Audit 2 (canonical ordering single source) | Check 1 |
| TILE-02 | Audit 1 (DISPLAY-12 single implementation, three-way reach) | Check 2 |
| TILE-03 | Audit 1 | Check 2 |
| TILE-04 | Audit 2 (shared ordering with Identify) | Check 4 |
| TILE-05 | — (no static audit possible for keyboard input) | Check 3 |
| TILE-06 | — (no static audit possible for live hotplug) | Check 5 |
| TILE-07 | Audit 4 (zero `MonitorPanelForm` references, both entry points gone) | Check 6 |
| MAIN-01 | Audit 4 (layout order) | Check 6, Check 7 |
| MAIN-02 | Audit 4 (layout order, gear presence) | Check 6 |

## Deviations from Plan

### Not fixed (documented, not code defects — matches Plans 19-02/19-03/19-04's own established precedent for this exact class of issue)

**1. Audit 2's `_lastKnownMonitors =` literal count is 3, not the acceptance criteria's stated 2**

- **Found during:** Audit 2 execution.
- **Issue:** The plan's acceptance criteria expects `grep -c '_lastKnownMonitors =' src/RigToggle.App/MainForm.cs` to output `2` ("the field initializer and the single reassignment in `RefreshMonitorTiles`"), but `RefreshMonitorTiles()` actually contains a try/catch with two separate, mutually-exclusive assignment statements (the success-path sorted-list assignment and the exception-fallback empty-array assignment), both of which are legitimate parts of the one writer method. Literal count is 3 (field initializer + 2 assignments, both inside `RefreshMonitorTiles`).
- **Verified property intact:** The single-writer / two-reader shape the audit exists to prove is unaffected — both assignment branches live inside the same single method (`RefreshMonitorTiles`), and no other method in the file assigns to `_lastKnownMonitors`. This is the same class of plan-authoring gap Plans 19-02/19-03/19-04 each documented in their own summaries (a literal grep count that doesn't account for a legitimate second branch/comment inside the intended single call site). No code change made; hard constraint 1 (no source changes) and constraint 2 (do not weaken an audit to make it pass) both preclude "fixing" the plan's grep expectation by altering the code.

**2. Audit 3's `btnSettings` and `\.Controls\b` literal counts do not match the acceptance criteria's stated values**

- **Found during:** Audit 3 execution.
- **Issue (a):** `grep -c 'btnSettings' <ApplyDashboardTheming body>` returns `3` (the `ThemeButton` call, a doc-comment mention of `btnSettings.ForeColor`, and the `btnSettings.Invalidate()` call), not the stated `1`. A more targeted grep for the actual theming invocation (`ThemeApplier.ThemeButton(btnSettings`) returns `1`, confirming the intended property (btnSettings is themed exactly once per call) holds.
- **Issue (b):** `grep -cE '\.Controls\b' src/RigToggle.App/ThemeApplier.cs` returns `1`, not the stated `0`. The sole match is the file's `using RigToggle.App.Controls;` namespace-import statement (needed for the `MonitorTile` type reference), not a `.Controls` collection iteration. Direct inspection of every method in `ThemeApplier.cs` confirms none walks a `Controls` collection — each targets a single passed-in control instance, matching the class's own documented non-recursive contract.
- **Verified property intact:** Both intended properties (btnSettings themed exactly once; no recursive Controls-tree walk) hold under direct inspection and a more precise grep. No code change made, for the same reasons as Deviation 1.

**Total deviations:** 2 documented literal-grep discrepancies (3 individual count mismatches across Audits 2 and 3), zero code defects, zero source changes. All four audits pass on their actual intended behavioral property.

## Task 2: Rig-Hardware Verification

**Status: APPROVED**, after four real-hardware rounds that found and fixed genuine defects — none fabricated, all rig-confirmed before commit, matching this project's evidence-before-fix discipline.

### Round 1 (initial checkpoint)

User reported: window too small/squished, a control hidden behind the tile icon (later identified as Identify), Rig/Normal toggle partially covered, Settings button read as a static label with no interaction feedback.

**Root cause 1 — layout overlap (`30482c1`, `6919d31`):** Added a diagnostic `Trace.WriteLine` to `LayoutDashboard()` gated behind the existing `EnableDebugLogging` setting, then asked the user to reproduce with logging on. The captured trace (`contentBottom=90` while the tile strip's real bottom was `154`) proved `LayoutDashboard()` was branching on `tileStrip.Visible` to decide whether to size around the real tile strip or the empty-state placeholder — but `Control.Visible` cascades through the whole ancestor chain, and `InitializeTrayState()` calls `RefreshMonitorTiles()` before `Form.Show()` has ever run on every startup path, so the getter always read `false` regardless of the local flag just having been set `true`. Fixed by branching on a locally-computed `hasMonitors = count > 0` instead, which has no ancestor-visibility timing dependency. Manually re-verified the full arithmetic against the rig's logged `Font.Height=16` before shipping. Removed the diagnostic once the root cause was confirmed.

**Root cause 2 — button feedback (`30482c1`):** First attempt widened `FlatAppearance.MouseOverBackColor`/`MouseDownBackColor` contrast. Rig-confirmed insufficient in Round 2 (see below).

### Round 2

User reported: layout fixed, but the primary-monitor badge was still clipped at the top, and buttons still showed zero hover/press feedback.

**Root cause 3 — badge clipping (`44a9796`):** The badge's center was placed exactly at the icon's top-right corner (`iconRect.Top`), but the badge's radius exceeds the icon's own top inset at this tile size, so the top half of the circle fell outside the tile's own paint bounds (`Y < 0`) and was silently clipped by the graphics context. Fixed by clamping the center's Y to at least one badge-radius down.

**Root cause 4 — FlatAppearance unreliable in dark mode (`44a9796`):** Widening the color values (Round 1) made no visible difference at all — consistent with this codebase's own already-documented `dotnet/winforms#13897` (FlatAppearance hover/press colors unreliable once dark mode is active), the exact bug `ThemeApplier.ThemeButton`'s `BorderSize=0` choice was originally written to dodge, but apparently not fully. Converted `btnIdentify`/`btnSettings` to track hover/press state locally (`MouseEnter`/`Leave`/`Down`/`Up`) and paint their own background manually via a new `ManualButtonFill` helper — the same reliable owner-draw pattern `MonitorTile` already uses — bypassing `FlatAppearance` for these two buttons entirely.

### Round 3

User reported: badge and hover/press now correct, but Tab-focusing Identify or Settings showed no focus indicator at all (Toggle, unaffected, still showed one).

**Root cause 5 (`fe4168c`):** The Round 2 fix's `ManualButtonFill` background fill covers the button's entire `ClientRectangle`, which paints over WinForms' native dotted focus rectangle — normally drawn as part of the button's own base paint, before the `Paint` event fires. Added an explicit accent-colored focus ring (`DrawButtonFocusRing`), drawn only when `Focused`, reusing the same color source (`ThemeApplier.ThemeMonitorTile`'s `AccentColor` values) the tiles' own focus ring already uses.

### Round 4

User reported: focus ring present on Identify/Settings but a different color than Toggle's still-native focus rectangle — asked for uniform color across all three.

**Root cause 6 (`8de03b8`):** Toggle was intentionally left on native rendering (Phase 20/THEME-08 replaces it with a fully custom-drawn switch shortly, so no further investment was made there beyond what was actually reported). Added a `Paint` handler that draws only the same accent focus ring on top when focused — background/hover/press rendering deliberately untouched.

### Final rig confirmation

All ten checks from `19-05-PLAN.md` Task 2 approved: tile row correctness (icon, number, status, primary badge fully visible), click-to-toggle with the confirmation gate, keyboard Tab/Space/Enter with a uniform accent focus ring across tiles and all three buttons, Identify numbering matching tiles 1:1, live hotplug (visible and hidden-to-tray), layout order and `MonitorPanelForm`/entry-point retirement, live theme flip and `--tray` hidden-start theming, the hotkey-during-confirm-dialog race, and DPI scaling. Hover/press feedback on Identify and Settings confirmed visible and unmistakable — the original complaint that prompted Round 1.

**Total: 4 rig round-trips, 6 real defects found and fixed, 0 fabricated approvals.**

## Requirement ID → Verification Mapping (final)

| Requirement | Task 1 static evidence | Task 2 rig verdict |
|---|---|---|
| TILE-01 | Audit 2 (canonical ordering single source) | Approved — tile row correct, badge visible (Round 2 fix) |
| TILE-02 | Audit 1 (DISPLAY-12 single implementation, three-way reach) | Approved — click-to-toggle confirmed on real CCD hardware |
| TILE-03 | Audit 1 | Approved — confirmation gate confirmed |
| TILE-04 | Audit 2 (shared ordering with Identify) | Approved — overlay numbering matches tiles 1:1 |
| TILE-05 | — (no static audit possible for keyboard input) | Approved — Tab/Space/Enter confirmed, uniform focus ring (Round 3/4 fixes) |
| TILE-06 | — (no static audit possible for live hotplug) | Approved — hotplug confirmed visible and hidden-to-tray |
| TILE-07 | Audit 4 (zero `MonitorPanelForm` references, both entry points gone) | Approved — no survivors on real install |
| MAIN-01 | Audit 4 (layout order) | Approved — overlap fixed (Round 1), final order confirmed |
| MAIN-02 | Audit 4 (layout order, gear presence) | Approved — hover/press feedback confirmed (Round 2/3 fixes) |

## Self-Check: PASSED

- FOUND: .planning/phases/19-monitor-tile-dashboard-monitorpanelform-retirement/19-05-SUMMARY.md
- Build and test commands re-verified after all Round 1-4 fixes: `dotnet build RigToggle.sln` — 0 errors, 4 pre-existing warnings (unchanged baseline); `dotnet test` — 81/81 passing, matches baseline exactly.
- All 9 phase requirements (TILE-01..07, MAIN-01, MAIN-02) now carry both a Task 1 static-evidence citation and a Task 2 rig verdict.
- Phase 19 goal achieved: MainForm is a monitor-tile dashboard, `MonitorPanelForm` and both entry points are fully retired, confirmed on real hardware after four rig-informed fix rounds.
