---
phase: 21-accent-color-reading-live-update
plan: 02
subsystem: theme
tags: [winforms, accent-color, theme, live-update]

# Dependency graph
requires:
  - phase: 21-accent-color-reading-live-update (Plan 01)
    provides: "IThemeProvider.AccentColor / AccentColorChanged contract and WindowsThemeProvider's live registry/DWM read"
provides:
  - "All five D-04 accent consumers (MonitorTile.AccentColor, MonitorTile.FocusRingColor, ToggleSwitch.OnColor, ToggleSwitch.FocusRingColor, MainForm.AccentColor) sourced from IThemeProvider.AccentColor instead of the hardcoded dark/light placeholder pair"
  - "A live accent-color change repaints every accent-tinted control through the existing OnThemeChanged -> ApplyDashboardTheming() funnel, with no new handler and no new call site"
affects: [22-manual-light-dark-override]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Second event subscribed into an existing handler (OnThemeChanged), not a new handler -- reuses the handler's InvokeRequired/BeginInvoke marshalling and its single repaint funnel for a second, independent OS signal"
    - "Theme-independent value threaded as a method parameter (accentColor) alongside a theme-dependent bool (dark), rather than re-deriving it inside the callee from a light/dark branch"

key-files:
  created: []
  modified:
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/ThemeApplier.cs

key-decisions:
  - "AccentColorChanged wired into the existing OnThemeChanged handler, not a new OnAccentColorChanged -- inherits both the UI-thread marshalling guard and both theming entry points (OnThemeChanged, InitializeTrayState()) for free, per the plan's two-call-site rule"
  - "ThemeMonitorTile and ThemeToggleSwitch take accentColor as an explicit parameter rather than reading IThemeProvider directly, keeping ThemeApplier a pure static helper with no provider dependency"

patterns-established: []

requirements-completed: [THEME-07]

# Metrics
duration: 5min
completed: 2026-08-11
---

# Phase 21 Plan 02: Accent Color Consumer Swap Summary

**Repointed all five D-04 accent consumers (two MonitorTile properties, two ToggleSwitch properties, MainForm's focus-ring color) from the hardcoded `Color.FromArgb(0, 90, 158)`/`SystemColors.Highlight` dark/light pair to the live `IThemeProvider.AccentColor`, with a live accent flip now repainting through the existing `OnThemeChanged` -> `ApplyDashboardTheming()` funnel.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-08-11T07:13:40Z (worktree base commit `8264caf`)
- **Completed:** 2026-08-11T07:17:52Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- `MainForm` subscribes `_themeProvider.AccentColorChanged` into the same `OnThemeChanged` handler `ThemeChanged` already uses (no new handler, no new subscription target)
- `MainForm.AccentColor` is now `_themeProvider.AccentColor` -- a live pass-through, placeholder literal removed
- `ThemeApplier.ThemeMonitorTile` and `ThemeApplier.ThemeToggleSwitch` each gained a `Color accentColor` parameter; all four accent assignments (`tile.AccentColor`, `tile.FocusRingColor`, `toggleSwitch.OnColor`, `toggleSwitch.FocusRingColor`) now assign it directly instead of a dark/light ternary
- `MainForm.ApplyDashboardTheming()`'s two call sites forward the live `AccentColor` property
- The two out-of-scope `ThemeApplier.cs` literals (SettingsForm grid-selection, hotkey-recording box) are untouched -- `Color.FromArgb(0, 90, 158)` still appears exactly twice, both on those lines
- Build green (0 Errors, 4 pre-existing Warnings), 82/82 tests pass -- unchanged from Plan 01's baseline

## Task Commits

Each task was committed atomically:

1. **Task 1: MainForm subscribes to AccentColorChanged and sources AccentColor live** - `d715b34` (feat)
2. **Task 2: ThemeApplier takes the accent color as a parameter for the tile and the switch** - `a93e457` (feat)

## Files Created/Modified
- `src/RigToggle.App/MainForm.cs` - Added `_themeProvider.AccentColorChanged += OnThemeChanged;` (constructor, directly below the existing `ThemeChanged` subscription); replaced the `AccentColor` property getter and its stale comment with a live `_themeProvider.AccentColor` pass-through; updated `ApplyDashboardTheming()`'s two `ThemeApplier` call sites to forward `AccentColor`
- `src/RigToggle.App/ThemeApplier.cs` - `ThemeMonitorTile(MonitorTile tile, bool dark, Color accentColor)` and `ThemeToggleSwitch(ToggleSwitch toggleSwitch, bool dark, Color accentColor)` new signatures; four assignments switched from a dark/light ternary to `accentColor`; retired the stale "Phase 21/THEME-07 replaces this pair" forward-reference marker comment in favor of present-tense documentation; XML doc comments on both methods updated to describe the new parameter

## Decisions Made
- `AccentColorChanged` reuses `OnThemeChanged` rather than getting its own handler -- both the marshalling guard and the single repaint funnel (`ApplyDashboardTheming()`, reached by both `OnThemeChanged` and `InitializeTrayState()`) are inherited for free, per the plan's hard constraint 4 and the two-call-site rule from `19-RESEARCH.md` Pitfall 1
- `ThemeApplier`'s two methods take the color as a parameter instead of querying `IThemeProvider` themselves, keeping `ThemeApplier` a pure static helper with no provider dependency -- `MainForm` is the single place that reads `IThemeProvider.AccentColor` and forwards it

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

**Worktree base drift (pre-execution, not a plan deviation):** On spawn, this worktree's `HEAD` was at an older commit (`ec29345`) rather than the expected base commit `8264cafdc68d7237cd177e0baf1ed48aea3075de` (`docs(phase-21): update tracking after wave 1`). The mandatory `<worktree_branch_check>` merge-base assertion caught this before any file edits (the branch namespace check passed; the stale commit was confirmed to be an ancestor of the expected base); `git reset --hard 8264cafdc68d7237cd177e0baf1ed48aea3075de` was run per the documented recovery procedure before Task 1 began. No task work or commits were affected.

## Acceptance Criteria - Recorded Command Output

**Task 1 (all passed):**
```
$ dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
Build succeeded. 0 Error(s), 4 Warning(s) (pre-existing xUnit1031 only)

$ dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true
Passed! - Failed: 0, Passed: 82, Skipped: 0, Total: 82

$ grep -c '_themeProvider.AccentColorChanged += OnThemeChanged;' src/RigToggle.App/MainForm.cs
1
$ grep -c '_themeProvider.ThemeChanged += OnThemeChanged;' src/RigToggle.App/MainForm.cs
1
$ grep -c 'OnAccentColorChanged' src/RigToggle.App/MainForm.cs
0
$ grep -c 'private Color AccentColor => _themeProvider.AccentColor;' src/RigToggle.App/MainForm.cs
1
$ grep -c 'Color.FromArgb(0, 90, 158)' src/RigToggle.App/MainForm.cs
0
$ grep -c 'private bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark;' src/RigToggle.App/MainForm.cs
1
$ grep -n 'AccentColor' src/RigToggle.App/MainForm.cs
127:            _themeProvider.AccentColorChanged += OnThemeChanged;
190:        // 21/THEME-07/D-04: sourced live from IThemeProvider.AccentColor
192:        // not branch on light/dark -- AccentColor is theme-independent. It is the
196:        private Color AccentColor => _themeProvider.AccentColor;
1105:        /// language and color source (ThemeApplier.ThemeMonitorTile's AccentColor)
1191:                    using var ringPen = new Pen(AccentColor, penWidth);
1254:                    DrawButtonFocusRing(e.Graphics, btnSettings.ClientRectangle, AccentColor);
$ grep -rc 'AccentColorChanged' src/RigToggle.App/MonitorConfirmDialog.cs src/RigToggle.App/SettingsForm.cs
src/RigToggle.App/MonitorConfirmDialog.cs:0
src/RigToggle.App/SettingsForm.cs:0
$ git status --porcelain src/RigToggle.App/
 M src/RigToggle.App/MainForm.cs
```

**Task 2 (all passed):**
```
$ dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
Build succeeded. 0 Error(s), 4 Warning(s) -- no new warning

$ dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true
Passed! - Failed: 0, Passed: 82, Skipped: 0, Total: 82

$ grep -c 'public static void ThemeMonitorTile(MonitorTile tile, bool dark, Color accentColor)' src/RigToggle.App/ThemeApplier.cs
1
$ grep -c 'public static void ThemeToggleSwitch(ToggleSwitch toggleSwitch, bool dark, Color accentColor)' src/RigToggle.App/ThemeApplier.cs
1
$ grep -c 'tile.AccentColor = accentColor;' src/RigToggle.App/ThemeApplier.cs
1
$ grep -c 'tile.FocusRingColor = accentColor;' src/RigToggle.App/ThemeApplier.cs
1
$ grep -c 'toggleSwitch.OnColor = accentColor;' src/RigToggle.App/ThemeApplier.cs
1
$ grep -c 'toggleSwitch.FocusRingColor = accentColor;' src/RigToggle.App/ThemeApplier.cs
1
$ grep -c 'Color.FromArgb(0, 90, 158)' src/RigToggle.App/ThemeApplier.cs
2
$ grep -n 'Color.FromArgb(0, 90, 158)' src/RigToggle.App/ThemeApplier.cs
41:                grid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;
98:                textBox.BackColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Info;
$ awk '/public static void ThemeMonitorTile/,/^        }$/' src/RigToggle.App/ThemeApplier.cs | grep -c 'SystemColors.Highlight'
0
$ awk '/public static void ThemeToggleSwitch/,/^        }$/' src/RigToggle.App/ThemeApplier.cs | grep -c 'SystemColors.Highlight'
0
$ grep -rc 'this is the single line that changes then\|Phase 21/THEME-07 replaces' src/RigToggle.App/ThemeApplier.cs
0
$ grep -c 'ThemeApplier.ThemeMonitorTile(tile, IsDark, AccentColor);' src/RigToggle.App/MainForm.cs
1
$ grep -c 'ThemeApplier.ThemeToggleSwitch(toggleSwitch, IsDark, AccentColor);' src/RigToggle.App/MainForm.cs
1
$ grep -rc 'ThemeApplier.ThemeMonitorTile(' src/ --include=*.cs | grep -v ':0'
src/RigToggle.App/MainForm.cs:1
$ grep -rc 'ThemeApplier.ThemeToggleSwitch(' src/ --include=*.cs | grep -v ':0'
src/RigToggle.App/MainForm.cs:1
$ awk '/private void ApplyDashboardTheming/,/^        }$/' src/RigToggle.App/MainForm.cs
        private void ApplyDashboardTheming()
        {
            foreach (MonitorTile tile in _tiles)
            {
                ThemeApplier.ThemeMonitorTile(tile, IsDark, AccentColor);
            }

            ThemeApplier.ThemeButton(btnIdentify, IsDark);
            ThemeApplier.ThemeButton(btnSettings, IsDark);
            ThemeApplier.ThemeToggleSwitch(toggleSwitch, IsDark, AccentColor);
            // The gear glyph is painted from btnSettings.ForeColor, which
            // ThemeButton just changed -- force a repaint so it doesn't wait for the
            // next incidental invalidation.
            btnSettings.Invalidate();

            lblNoMonitors.ForeColor = IsDark ? Color.FromArgb(160, 160, 160) : SystemColors.GrayText;
        }
$ git status --porcelain src/
 M src/RigToggle.App/MainForm.cs
 M src/RigToggle.App/ThemeApplier.cs
$ grep -rc 'DWMWA_CAPTION_COLOR' src/ --include=*.cs | grep -v ':0' | wc -l
0
$ git diff --stat -- '*.csproj'
(empty)
```

**Plan-level verification (all passed):**
```
$ dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
Build succeeded. 0 Error(s), 4 Warning(s)
$ dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true
Passed! Failed: 0, Total: 82
$ git status --porcelain
(empty -- both task commits landed cleanly)
```

## Final Source of the Changed ThemeApplier Accent Lines

```csharp
public static void ThemeMonitorTile(MonitorTile tile, bool dark, Color accentColor)
{
    try
    {
        tile.BackColor = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
        tile.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
        tile.AccentColor = accentColor;
        tile.FocusRingColor = accentColor;
        tile.IconOffColor = dark ? Color.FromArgb(160, 160, 160) : SystemColors.GrayText;
        tile.HoverBackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.ControlLight;
        tile.Invalidate();
    }
    catch { /* Cosmetic-only */ }
}

public static void ThemeToggleSwitch(ToggleSwitch toggleSwitch, bool dark, Color accentColor)
{
    try
    {
        toggleSwitch.BackColor = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
        toggleSwitch.OnColor = accentColor;
        toggleSwitch.FocusRingColor = accentColor;
        // ... remaining grayscale/theme-derived assignments unchanged ...
        toggleSwitch.Invalidate();
    }
    catch { /* Cosmetic-only */ }
}
```

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All five D-04 accent consumers are wired to the live `IThemeProvider.AccentColor`; behavior visibly changes for the first time in Phase 21 (pending Plan 03's rig verification of both the byte-order tiebreak flagged in Plan 01 and this plan's live repaint).
- Plan 03's rig checklist should visually confirm: (1) the tile ON-state icon fill, tile/switch focus rings, and switch ON-state track fill all show the user's actual Windows accent color; (2) a live accent change (via Settings > Personalization > Colors) repaints all of them with no restart; (3) the two out-of-scope SettingsForm surfaces (grid selection, hotkey box) are unaffected.
- No blockers.

---
*Phase: 21-accent-color-reading-live-update*
*Completed: 2026-08-11*

## Self-Check: PASSED

Both modified source files (`src/RigToggle.App/MainForm.cs`, `src/RigToggle.App/ThemeApplier.cs`) and this SUMMARY.md confirmed present on disk. All 3 commits (`d715b34`, `a93e457`, `641e2cb`) confirmed present in `git log`.
