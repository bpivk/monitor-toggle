---
phase: 21-accent-color-reading-live-update
plan: 03
subsystem: theme
tags: [verification, static-audit, regression, winforms, dwm, registry, accent-color]

# Dependency graph
requires:
  - phase: 21-accent-color-reading-live-update (Plan 01)
    provides: "IThemeProvider.AccentColor / AccentColorChanged contract and WindowsThemeProvider's live registry/DWM read"
  - phase: 21-accent-color-reading-live-update (Plan 02)
    provides: "All five D-04 accent consumers wired to the live AccentColor through the existing ApplyDashboardTheming funnel"
provides:
  - "Full-solution regression proof (build + 82-test suite) that Phase 21's two source plans introduced no defect"
  - "Five recorded static audits proving source-swap completeness, D-02 single-subscription/single-provider discipline, the repaint-funnel/two-call-site lockstep, the exact five-consumer D-04 set with no scope creep, and read-path safety/byte-order discipline"
  - "Task 2 rig-verification checkpoint reached and awaiting the user's personally-run PASS/FAIL report (D-05) -- NOT YET RECORDED"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created:
    - .planning/phases/21-accent-color-reading-live-update/21-03-SUMMARY.md
  modified: []

key-decisions:
  - "No source changes made or needed -- all five audits passed on the first pass with no defect found, consistent with hard constraint 1 (this plan makes no source changes)"

patterns-established: []

requirements-completed: []  # THEME-07 is NOT yet complete -- D-05's rig PASS (Task 2) is still outstanding. Do not mark complete until the checkpoint resolves.

# Metrics
duration: (Task 1 only; Task 2 checkpoint pending)
completed: 2026-08-11
---

# Phase 21 Plan 03: Full Regression Gate & Static Audit Summary (Task 1 of 2 -- Task 2 rig checkpoint pending)

**Full-solution regression gate confirmed green at baseline (0 Errors, 4 pre-existing Warnings, 82/82 tests) and all five static audits of Phase 21's structural/safety properties passed with recorded command evidence; no source file was touched. The phase's three ROADMAP success criteria remain rig-pending per D-05 -- Task 2 (blocking human-verify checkpoint) has NOT been executed and this plan is NOT complete.**

## Performance

- **Duration:** Task 1 only (build/test + 5 audits); Task 2 (rig checkpoint) duration not yet started
- **Started:** 2026-08-11 (worktree base commit `af8d10a`, after correcting a stale worktree base drift -- see Issues Encountered)
- **Completed:** Task 1 complete; Task 2 pending
- **Tasks:** 1 of 2 complete
- **Files modified:** 0 (this SUMMARY.md is the only file this plan creates)

## Accomplishments

- Regression gate: `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` exits 0 with `0 Error(s)`, `4 Warning(s)` (all four pre-existing `xUnit1031` warnings in `ToggleOrchestratorTests.cs`, unrelated to Phase 21)
- Regression gate: `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true` reports `Failed: 0, Passed: 82, Skipped: 0, Total: 82` -- exact baseline match
- Audit 1 (source-swap completeness): confirmed and classified every occurrence of the placeholder literal and `SystemColors.Highlight` across `src/` -- no unclassified violation found
- Audit 2 (D-02 single notification/single provider): confirmed exactly one `+=`/one `-=` subscription to `SystemEvents.UserPreferenceChanged`, exactly two genuine `IThemeProvider` implementers, no polling timer, no `e.Category` filtering
- Audit 3 (repaint funnel / two-call-site lockstep): confirmed both `OnThemeChanged` and `InitializeTrayState()` reach `ApplyDashboardTheming`, which forwards `AccentColor` to both `ThemeMonitorTile` and `ThemeToggleSwitch`; confirmed `AccentColorChanged` has exactly one subscriber (`OnThemeChanged`, no dedicated accent handler) and that the `InvokeRequired` marshalling guard is still in place
- Audit 4 (D-04 consumer set exactness): confirmed `MonitorConfirmDialog.cs` and `SettingsForm.cs` have zero `AccentColorChanged` references, confirmed `DwmTitleBar.cs`/`MonitorTile.cs`/`ToggleSwitch.cs` are byte-for-byte unmodified across the whole phase (`git diff --stat` against phase-base commit `9836311` is empty for all three), confirmed `DWMWA_CAPTION_COLOR` appears nowhere in `src/`, confirmed no new file and no `.csproj` change
- Audit 5 (read-path safety / byte-order discipline): confirmed the registry and DWM extractions use the deliberately different R/B masks with their worked-example justification comments, confirmed zero four-argument `Color.FromArgb` calls, confirmed both read paths have non-throwing exits (2x `return null;`, 2x `return SystemColors.Highlight;`), confirmed `NativeMethods.DwmGetColorizationColor` is declared once, `internal`, called directly with no façade, confirmed accent color is never written to `AppSettings`
- `git status --porcelain src/` is empty -- this task made no source changes, satisfying hard constraint 1

## Task Commits

Each task was committed atomically:

1. **Task 1: Full regression gate and five static audits** - (this SUMMARY.md commit; no source files changed, `docs(21-03)` type)

Task 2 (rig-hardware verification, blocking checkpoint) has not started.

## Files Created/Modified

- `.planning/phases/21-accent-color-reading-live-update/21-03-SUMMARY.md` - This summary, recording Task 1's regression gate and audit evidence

## Decisions Made

- None beyond what Plans 01/02 already decided -- this plan is verification-only per its hard constraints and made no implementation decisions.

## Deviations from Plan

None - Task 1 executed exactly as written; every audit passed on the first attempt with no defect requiring remediation.

## Issues Encountered

**Worktree base drift (pre-execution, not a plan deviation):** On spawn, this worktree's `HEAD` was at a stale commit (`ec29345`, "docs: update retrospective for v2.0" -- a point in `master`'s history that predates all of Phase 21's work) rather than the expected base commit `af8d10ad8682126ec40b969338ca086d26648628` ("docs(phase-21): update tracking after wave 2", the tip after Plans 01 and 02 landed). The mandatory worktree branch check's merge-base assertion caught this before any file edits (branch namespace check passed; the working tree was verified clean via `git status --short`); `git reset --hard af8d10ad8682126ec40b969338ca086d26648628` was run per the documented recovery procedure, correcting the base before Task 1 began. No task work or commits were affected -- this mirrors the identical drift pattern documented in both 21-01-SUMMARY.md and 21-02-SUMMARY.md's "Issues Encountered" sections.

## Acceptance Criteria — Recorded Command Output

**Regression gate:**
```
$ dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
Build succeeded.
    4 Warning(s)
    0 Error(s)
(all 4 warnings: pre-existing xUnit1031 in ToggleOrchestratorTests.cs, lines 131/157/190/292 -- unrelated to Phase 21)

$ dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true
Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82, Duration: 76 ms
```

**Audit 1 — source-swap completeness and out-of-scope preservation:**
```
$ grep -rn 'Color.FromArgb(0, 90, 158)' src/ --include=*.cs
src/RigToggle.IconGen/IconGeometry.cs:62:    private static readonly Color AppGlassColor = Color.FromArgb(0, 90, 158);  // #005A9E
src/RigToggle.App/ThemeApplier.cs:41:  grid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;
src/RigToggle.App/ThemeApplier.cs:98:  textBox.BackColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Info;
-> Classification: IconGeometry.cs line is icon artwork (AppGlassColor), out of scope, unrelated to D-04.
   ThemeApplier.cs:41 is the SettingsForm grid-selection out-of-scope survivor.
   ThemeApplier.cs:98 is the SettingsForm hotkey-textbox out-of-scope survivor.
   Exactly 3 occurrences total across src/, matching the plan's stated baseline exactly.

$ grep -c 'Color.FromArgb(0, 90, 158)' src/RigToggle.App/MainForm.cs
0

$ grep -c 'accentColor;' src/RigToggle.App/ThemeApplier.cs
4   (tile.AccentColor, tile.FocusRingColor, toggleSwitch.OnColor, toggleSwitch.FocusRingColor -- all 4 D-04 assignments now read the parameter)

$ grep -rn 'SystemColors.Highlight' src/ --include=*.cs
src/RigToggle.App/ThemeApplier.cs:41    grid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;
src/RigToggle.App/Controls/MonitorTile.cs:41    private Color _accentColor = SystemColors.Highlight;
src/RigToggle.App/Controls/MonitorTile.cs:44    private Color _focusRingColor = SystemColors.Highlight;
src/RigToggle.App/Controls/ToggleSwitch.cs:86   private Color _onColor = SystemColors.Highlight;
src/RigToggle.App/Controls/ToggleSwitch.cs:94   private Color _focusRingColor = SystemColors.Highlight;
src/RigToggle.App/Controls/ToggleSwitch.cs:436  // comment only: "SystemColors.Highlight, and will keep working"
src/RigToggle.Windows/WindowsThemeProvider.cs:171   return SystemColors.Highlight;   (DWM fallback safe-default, catch branch)
src/RigToggle.Windows/WindowsThemeProvider.cs:181   return SystemColors.Highlight;   (DWM fallback safe-default, non-zero HRESULT branch)
-> Classification: ThemeApplier.cs:41 is the SettingsForm grid-selection out-of-scope survivor (light-theme branch of the same line as the FromArgb hit above).
   MonitorTile.cs:41/44 and ToggleSwitch.cs:86/94 are each control's own default field initializer -- pre-theming defaults overwritten by ThemeApplier's first theming pass, correctly left alone per the plan's explicit exemption.
   ToggleSwitch.cs:436 is a comment, not code.
   WindowsThemeProvider.cs:171/181 are the provider's safe-default return path (audit 5's own subject) -- not a violation.
   No unclassified hit found; zero violations.
```

**Audit 2 — single notification source and single provider (D-02):**
```
$ grep -rn 'SystemEvents.UserPreferenceChanged' src/ --include=*.cs | grep -vE ':\s*(//|\*|/\*)'
src/RigToggle.Windows/WindowsThemeProvider.cs:66:        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
src/RigToggle.Windows/WindowsThemeProvider.cs:189:    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
-> 2 total, 1 += and 1 -=, both in WindowsThemeProvider.cs. PASS.

$ grep -rl ': IThemeProvider\|, IThemeProvider' src/ --include=*.cs | sort
src/RigToggle.App/MonitorConfirmDialog.cs   <- false positive: constructor parameter ", IThemeProvider themeProvider", not an implements clause (pre-dates Phase 21)
src/RigToggle.App/SettingsForm.cs           <- false positive: same pattern, pre-existing consumer, unrelated to this plan
src/RigToggle.Tests/Doubles/FakeThemeProvider.cs
src/RigToggle.Windows/WindowsThemeProvider.cs
-> Exactly two genuine implementers after classification (FakeThemeProvider, WindowsThemeProvider); the two App-layer hits are constructor-parameter substring matches, identical false-positive pattern already documented in 21-01-SUMMARY.md's Task 2 evidence. PASS.

$ grep -cE '\bTimer\b|Task.Delay|Thread.Sleep' src/RigToggle.Windows/WindowsThemeProvider.cs
0

$ grep -c 'e.Category' src/RigToggle.Windows/WindowsThemeProvider.cs
0
```

**Audit 3 — repaint funnel and two-call-site lockstep:**
```
$ awk '/private void OnThemeChanged/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'ApplyDashboardTheming'
1
$ awk '/public void InitializeTrayState/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'ApplyDashboardTheming'
1
$ awk '/private void OnThemeChanged/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'InvokeRequired'
1
$ awk '/private void ApplyDashboardTheming/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -c 'AccentColor'
2   (ThemeApplier.ThemeMonitorTile(tile, IsDark, AccentColor) and ThemeApplier.ThemeToggleSwitch(toggleSwitch, IsDark, AccentColor))
$ grep -rc 'AccentColorChanged +=' src/ --include=*.cs | grep -v ':0'
src/RigToggle.App/MainForm.cs:1
src/RigToggle.Tests/ThemeProviderContractTests.cs:1   (contract test's own subscription -- not a second application subscriber)
$ grep -rc 'OnAccentColorChanged' src/ --include=*.cs | grep -v ':0' | wc -l
0   (no dedicated accent handler exists -- AccentColorChanged is wired to the existing OnThemeChanged)
```
All Audit 3 acceptance criteria satisfied. `ApplyDashboardTheming` still themes `btnIdentify`, `btnSettings`, and `lblNoMonitors` (confirmed by reading the full method body during read_first).

**Audit 4 — D-04 consumer set is exactly five, and the deferred item stayed deferred:**
```
$ grep -rc 'AccentColorChanged' src/RigToggle.App/MonitorConfirmDialog.cs src/RigToggle.App/SettingsForm.cs
src/RigToggle.App/MonitorConfirmDialog.cs:0
src/RigToggle.App/SettingsForm.cs:0

$ git diff --stat 9836311 -- src/RigToggle.Windows/DwmTitleBar.cs src/RigToggle.App/Controls/MonitorTile.cs src/RigToggle.App/Controls/ToggleSwitch.cs
(empty -- no output)

$ grep -rc 'DWMWA_CAPTION_COLOR' src/ --include=*.cs | grep -v ':0' | wc -l
0

$ git status --porcelain src/
(empty)

$ git diff --stat 9836311 -- '*.csproj'
(empty -- no output)
```
All Audit 4 acceptance criteria satisfied.

**Audit 5 — read-path safety and byte-order discipline:**
```
$ awk '/private static Color\? ReadAccentColorFromRegistry/,/^    }$/' src/RigToggle.Windows/WindowsThemeProvider.cs | grep -c 'byte r = (byte)(v & 0x000000FF);'
1
$ awk '/private static Color\? ReadAccentColorFromRegistry/,/^    }$/' src/RigToggle.Windows/WindowsThemeProvider.cs | grep -c 'byte b = (byte)((v >> 16) & 0x000000FF);'
1
$ awk '/private static Color ReadAccentColorFromDwm/,/^    }$/' src/RigToggle.Windows/WindowsThemeProvider.cs | grep -c 'byte r = (byte)((colorization >> 16) & 0x000000FF);'
1
$ awk '/private static Color ReadAccentColorFromDwm/,/^    }$/' src/RigToggle.Windows/WindowsThemeProvider.cs | grep -c 'byte b = (byte)(colorization & 0x000000FF);'
1
$ grep -cE 'Color\.FromArgb\([^,()]+,[^,()]+,[^,()]+,[^,()]+\)' src/RigToggle.Windows/WindowsThemeProvider.cs
0
$ grep -c 'return null;' src/RigToggle.Windows/WindowsThemeProvider.cs
2
$ grep -c 'return SystemColors.Highlight;' src/RigToggle.Windows/WindowsThemeProvider.cs
2
$ grep -c 'DwmGetColorizationColor' src/RigToggle.Windows/NativeMethods.cs
1
$ ls src/RigToggle.Windows/DwmAccentColor.cs
ls: cannot access 'src/RigToggle.Windows/DwmAccentColor.cs': No such file or directory
$ grep -rc 'Accent' src/RigToggle.Core/Models/AppSettings.cs
0
$ grep -n 'internal static extern int DwmGetColorizationColor' src/RigToggle.Windows/NativeMethods.cs
148:    internal static extern int DwmGetColorizationColor(out uint pcrColorization, [MarshalAs(UnmanagedType.Bool)] out bool pfOpaqueBlend);
$ grep -c 'NativeMethods.DwmGetColorizationColor' src/RigToggle.Windows/WindowsThemeProvider.cs
1
```
All Audit 5 acceptance criteria satisfied.

**Plan-level verification:**
```
$ git status --porcelain src/
(empty -- this task changed no source file)
```

## Three Phase 21 Success Criteria — Verification Status

| # | Success Criterion (ROADMAP.md Phase 21) | Machine-verified this task | Rig-pending (Task 2) |
|---|---|---|---|
| 1 | The toggle switch's ON state (and other designated interactive elements) visibly uses the current Windows accent color | Structural prerequisite confirmed: Audit 1 proves the placeholder literal is gone from all five D-04 consumers; Audit 3 proves the funnel reaches both `ThemeMonitorTile`/`ThemeToggleSwitch` with `AccentColor` forwarded. **Visual confirmation on real hardware is NOT YET DONE.** | Task 2 checks 4, 9, 11 |
| 2 | Changing the Windows accent color while running updates accent-tinted elements live, without restart | Structural prerequisite confirmed: Audit 2 proves exactly one `SystemEvents.UserPreferenceChanged` subscription with no timer/polling; Audit 3 proves `AccentColorChanged` has exactly one subscriber (`OnThemeChanged`) with the `InvokeRequired` marshalling guard intact. **Whether the OS event actually fires for an accent-only change (Assumption A3), and whether the repaint actually happens live, is NOT YET DONE.** | Task 2 checks 6, 7, 8 |
| 3 | The accent color shown in the app matches Settings > Colors exactly, including for a custom accent | Audit 5 confirms the two extraction methods use deliberately different, individually-justified R/B masks and never throw. **The byte-order question itself (which extraction is numerically correct) is explicitly NOT resolvable by static analysis** -- Plan 01's own summary flags this as "a settled implementation call, not a verified-correct one yet," and this plan's hard constraints assign the numeric resolution to the rig pass alone. | Task 2 checks 2, 3, 4 (check 3 is the decisive one) |

**All three criteria remain rig-pending.** No criterion can be marked fully verified until Task 2's checkpoint returns a user-reported PASS per D-05.

## Reference Evidence for Task 2 (verbatim from source, per this plan's `<output>` instruction)

**Registry extraction (`ReadAccentColorFromRegistry`):**
```csharp
private static Color? ReadAccentColorFromRegistry()
{
    try
    {
        using var key = Registry.CurrentUser.OpenSubKey(AccentKeyPath, writable: false);
        var raw = key?.GetValue(AccentValueName);
        if (raw is not int i)
        {
            return null;
        }

        uint v = unchecked((uint)i);
        byte r = (byte)(v & 0x000000FF);
        byte g = (byte)((v >> 8) & 0x000000FF);
        byte b = (byte)((v >> 16) & 0x000000FF);
        return Color.FromArgb(r, g, b);
    }
    catch
    {
        return null;
    }
}
```
ABGR (R in the low byte, bits 0-7; B in bits 16-23).

**DWM extraction (`ReadAccentColorFromDwm`):**
```csharp
private static Color ReadAccentColorFromDwm()
{
    try
    {
        int hr = NativeMethods.DwmGetColorizationColor(out uint colorization, out _);
        if (hr != 0)
        {
            return SystemColors.Highlight;
        }

        byte r = (byte)((colorization >> 16) & 0x000000FF);
        byte g = (byte)((colorization >> 8) & 0x000000FF);
        byte b = (byte)(colorization & 0x000000FF);
        return Color.FromArgb(r, g, b);
    }
    catch
    {
        return SystemColors.Highlight;
    }
}
```
Documented Microsoft `0xAARRGGBB` (R in bits 16-23; B in the low byte, bits 0-7) -- the opposite byte order from the registry extraction above, deliberately.

**Log-line format strings (constructor and flip-detection, for matching against `debug.log`):**
```csharp
Log($"Constructed: initial theme resolved to {CurrentTheme}, accent resolved to {_accentColor}");
```
```csharp
Log($"Accent color flip detected: {previousAccent} -> {resolvedAccent}");
```

## User Setup Required

None for Task 1. **Task 2 requires the user to personally run the rig-verification checklist on real Windows 11 hardware** -- see Next Steps below. This execution environment is Linux and cannot run the Windows GUI, read the real Windows registry, call `DwmGetColorizationColor`, or sample real screen pixels, so Task 2 cannot be automated or inferred.

## Next Phase Readiness

**This plan is NOT complete.** Task 1 (regression gate + five static audits) is done and all evidence is recorded above with no defect found. Task 2 -- a blocking `checkpoint:human-verify` requiring the user to personally run all twelve numbered rig checks from `21-03-PLAN.md` on real Windows 11 hardware with a live accent color -- has not started. Per this plan's hard constraint 4 and D-05, **Phase 21 must not be marked done until the user reports a PASS/FAIL rig verdict.**

No blockers to starting Task 2 -- the app is ready to publish and rig-test (`dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`), and all reference evidence (extraction methods, log-line formats) Task 2 needs is recorded above.

---
*Phase: 21-accent-color-reading-live-update*
*Completed: Task 1 only -- 2026-08-11. Task 2 (rig checkpoint) still pending.*

## Self-Check: PASSED

This SUMMARY.md confirmed present on disk. `git status --porcelain src/` confirmed empty (no source file changed by Task 1). Build and test commands re-verified against the recorded output above.
