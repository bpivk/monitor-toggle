---
phase: 12-theme-infrastructure-live-theme-following
plan: 02
subsystem: ui
tags: [winforms, dwm, theming, composition-root, live-theme-follow]

# Dependency graph
requires: [12-01]
provides:
  - Application.SetColorMode(SystemColorMode.System) as the first executable statement of Main(), before any Form/control construction
  - Single composition-root WindowsThemeProvider instance, injected into SettingsForm, MainForm, MonitorConfirmDialog
  - MainForm live theme-follow (marshaled OnThemeChanged: SetColorMode + DwmTitleBar.ApplyRoundedCornersAndMica + Refresh), DWM chrome applied from InitializeTrayState (covers --tray hidden-start), FlatStyle.System buttons, D-03 ToolStrip stale-color rationale comment
  - MonitorConfirmDialog live theme-follow (same marshaled pattern, FormClosed-based unsubscribe since it's a transient dialog), DWM chrome applied post-InitializeComponent, FlatStyle.System buttons
  - SettingsForm IThemeProvider ctor parameter + field only (per-control theming deferred to 12-03)
affects: [12-03, 12-04, ui, theme-application]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Composition-root-only construction: WindowsThemeProvider is `new`'d exactly once in Program.cs and threaded into every form constructor — no form ever constructs its own adapter"
    - "Constructor-then-callsite discipline: each form's IThemeProvider ctor param is added by the same task that updates its construction call-site, keeping every task-boundary build green"
    - "Marshal-then-try/catch OnThemeChanged pattern: InvokeRequired/BeginInvoke re-dispatch to the UI thread, then SetColorMode + DwmTitleBar.ApplyRoundedCornersAndMica + Refresh wrapped in try/catch so a theming failure never crashes the toggle/confirm flow"
    - "Long-lived-provider vs transient-dialog unsubscribe split: MainForm unsubscribes ThemeChanged in Dispose(bool) (app-lifetime form); MonitorConfirmDialog unsubscribes on FormClosed (per-show transient dialog, must not leak a handler on the outliving provider)"

key-files:
  created: []
  modified:
    - src/RigToggle.App/Program.cs
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/MainForm.Designer.cs
    - src/RigToggle.App/MonitorConfirmDialog.cs
    - src/RigToggle.App/MonitorConfirmDialog.Designer.cs

key-decisions:
  - "SetColorMode is the literal first executable statement of Main(), before ApplicationConfiguration.Initialize() and before any Form/control construction — avoids the double-DWM-attribute title-bar flash pitfall"
  - "DWM chrome (Mica/rounded corners) is applied from MainForm.InitializeTrayState(), never OnLoad/OnShown, because the --tray hidden-start path never fires those events but InitializeTrayState() runs unconditionally on both startup paths with Handle already forced"
  - "MonitorConfirmDialog gets its first-ever interface dependency (IThemeProvider) in this plan — previously pure display data with zero Core interface injection"
  - "The dotnet/winforms#12027 ToolStrip stale-color bug is explicitly documented as an accepted limitation via a code comment, not rebuilt/worked around — avoids unplanned scope creep into ContextMenuStrip reconstruction"

requirements-completed: [THEME-01, THEME-02, THEME-03, THEME-05, THEME-06]

# Metrics
duration: 45min
completed: 2026-08-03
---

# Phase 12 Plan 02: Composition Root Wiring & MainForm/MonitorConfirmDialog Live Theming Summary

**Application.SetColorMode wired as the very first Main() statement, one composition-root WindowsThemeProvider threaded into every form, and MainForm + MonitorConfirmDialog get full launch-time + SystemEvents-driven live theme-follow (title bar, controls, flat buttons, Windows-11 Mica/rounded corners) — SettingsForm's own per-control theming is deferred to plan 12-03.**

## Performance

- **Duration:** 45 min
- **Started:** 2026-08-02T22:10:00Z (interrupted mid-Task-2 by a session usage limit, resumed same session)
- **Completed:** 2026-08-03T00:05:00Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- `Application.SetColorMode(SystemColorMode.System)` is the literal first executable statement of `Main()`, before `ApplicationConfiguration.Initialize()` and any UI construction
- `WindowsThemeProvider` is constructed exactly once (composition-root-only rule, grep-verified) and threaded into `SettingsFormFactory`, `MainForm`, and (via `MainForm`) `MonitorConfirmDialog`
- `MainForm` themes at startup on BOTH the visible and `--tray` hidden paths (DWM chrome applied from `InitializeTrayState()`, not `OnLoad`/`OnShown`), live-follows OS theme flips via a marshaled `OnThemeChanged` handler, gets Windows-11 Mica/rounded corners with a silent Windows-10 no-op, flat (`FlatStyle.System`) buttons, and unsubscribes from the app-lifetime provider on `Dispose(bool)`
- `MonitorConfirmDialog` gains its first-ever injected Core interface dependency, applies DWM chrome immediately post-`InitializeComponent` (always shown via `ShowDialog`, no `--tray` timing concern), live-follows theme flips, and unsubscribes on `FormClosed` (transient per-show lifecycle, distinct from `MainForm`'s `Dispose`-time unsubscribe)
- The `dotnet/winforms#12027` ToolStrip stale-color limitation is documented in-code near the tray context menu as an accepted, non-actionable limitation
- `SettingsForm` carries the `IThemeProvider` constructor parameter (ctor + field only) so the composition root compiles; its per-control theming work is explicitly deferred to plan 12-03
- Full solution builds (`dotnet build RigToggle.sln`) and the full 70-test suite passes with no regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Program.cs base color mode + provider construction + SettingsForm injection** - `2c5fd09` (feat)
2. **Task 2: MainForm — ctor injection, live-follow, DWM chrome, flat buttons, D-03 comment** - `ce4f343` (feat)
3. **Task 3: MonitorConfirmDialog — ctor injection, live-follow, DWM chrome, flat buttons** - `0f813b9` (feat)

**Plan metadata:** committed alongside this SUMMARY (docs)

## Files Created/Modified
- `src/RigToggle.App/Program.cs` - `SetColorMode` as first `Main()` statement; single `WindowsThemeProvider` construction; threaded into `SettingsFormFactory` and `new MainForm(...)`
- `src/RigToggle.App/SettingsForm.cs` - `IThemeProvider` ctor parameter + `_themeProvider` field (ctor-only this plan)
- `src/RigToggle.App/MainForm.cs` - `IThemeProvider` ctor parameter, `ThemeChanged` subscription, marshaled `OnThemeChanged`, `ApplyDwmChrome()` called from `InitializeTrayState()`, themed `MonitorConfirmDialog` construction
- `src/RigToggle.App/MainForm.Designer.cs` - `FlatStyle.System` on `btnToggle`/`btnSettings`, D-03 rationale comment near `trayContextMenu`/`traySeparator`, `ThemeChanged` unsubscribe in `Dispose(bool)`
- `src/RigToggle.App/MonitorConfirmDialog.cs` - `IThemeProvider` ctor parameter (first-ever interface dependency for this form), subscribe/`FormClosed`-unsubscribe, marshaled `OnThemeChanged`, DWM chrome applied post-`InitializeComponent`
- `src/RigToggle.App/MonitorConfirmDialog.Designer.cs` - `FlatStyle.System` on `btnContinue`/`btnCancel`

## Decisions Made
None beyond what the plan already specified. All constructor-then-callsite sequencing, marshal/try-catch patterns, and the D-03/ToolStrip rationale were taken directly from the plan's task actions and 12-PATTERNS.md.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking, self-caught] Reworded a Program.cs comment to avoid a literal `new WindowsThemeProvider()` grep double-match**
- **Found during:** Task 1, acceptance-criteria verification
- **Issue:** Task 1's acceptance criteria requires `grep -c "new WindowsThemeProvider()" src/RigToggle.App/Program.cs` to return exactly 1. My first draft of the rationale comment above the construction line literally spelled out `` `new WindowsThemeProvider()` `` in backticks to explain the composition-root-only rule, which itself matched the grep pattern and pushed the count to 2.
- **Fix:** Reworded the comment to describe the construction generically ("the ONE and only place the theme provider adapter is constructed") without repeating the literal `new WindowsThemeProvider()` expression, preserving the rationale without tripping the gate.
- **Files modified:** `src/RigToggle.App/Program.cs`
- **Verification:** `grep -c "new WindowsThemeProvider()" ...` returns 1; `dotnet build` still succeeds.
- **Committed in:** `2c5fd09` (Task 1 commit — caught before commit, no separate fix commit needed)

---

**Total deviations:** 1 auto-fixed (1 self-caught grep-gate wording fix, no functional change)
**Impact on plan:** Cosmetic-only; no scope creep, no behavior change.

## Issues Encountered

Execution was interrupted mid-Task-2 by a session usage limit after the D-03 rationale comment was added but before the diff had been verified against acceptance criteria or committed. On resume, the uncommitted diff was re-verified in full against Task 2's acceptance criteria (all grep gates + `dotnet build`) before committing — no rework was needed, the pre-interruption code was already correct.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 12-03 can now add `SettingsForm`'s own `ThemeChanged` subscription and per-control theming (the ctor param + field it needs already exist)
- Plan 12-04's rig checkpoint can verify the full launch-time + live-flip visual behavior for MainForm/MonitorConfirmDialog built in this plan
- Full solution builds and all 70 tests pass with no regressions

## Self-Check: PASSED

- FOUND: src/RigToggle.App/Program.cs (modified)
- FOUND: src/RigToggle.App/SettingsForm.cs (modified)
- FOUND: src/RigToggle.App/MainForm.cs (modified)
- FOUND: src/RigToggle.App/MainForm.Designer.cs (modified)
- FOUND: src/RigToggle.App/MonitorConfirmDialog.cs (modified)
- FOUND: src/RigToggle.App/MonitorConfirmDialog.Designer.cs (modified)
- FOUND commit 2c5fd09
- FOUND commit ce4f343
- FOUND commit 0f813b9

---
*Phase: 12-theme-infrastructure-live-theme-following*
*Completed: 2026-08-03*
