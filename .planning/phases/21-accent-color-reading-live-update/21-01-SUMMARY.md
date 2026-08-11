---
phase: 21-accent-color-reading-live-update
plan: 01
subsystem: theme
tags: [winforms, dwm, registry, p-invoke, accent-color, theme]

# Dependency graph
requires:
  - phase: 20-custom-toggle-switch-control
    provides: "The existing IThemeProvider CurrentTheme/ThemeChanged contract and WindowsThemeProvider's registry-read + SystemEvents.UserPreferenceChanged conventions this plan extends"
provides:
  - "IThemeProvider.AccentColor (Color, get-only) and IThemeProvider.AccentColorChanged event"
  - "WindowsThemeProvider live accent-color resolution: registry-primary (HKCU\\Software\\Microsoft\\Windows\\DWM\\AccentColor) with DwmGetColorizationColor fallback, never throwing"
  - "FakeThemeProvider.AccentColor / AccentColorChanged / RaiseAccentColorChanged(Color) test double"
  - "Contract test proving AccentColorChanged fires exactly once with AccentColor already updated"
affects: [21-02-accent-color-consumer-swap, 22-manual-light-dark-override]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Second independent lock (_accentLock) alongside an existing per-value lock (_themeLock) inside the same provider class, to keep two diffed OS reads from serializing behind each other"
    - "Registry-primary / native-API-fallback OS read pattern (ReadAccentColorFromRegistry() ?? ReadAccentColorFromDwm()), each branch never-throwing with an explicit safe default"

key-files:
  created: []
  modified:
    - src/RigToggle.Core/Abstractions/IThemeProvider.cs
    - src/RigToggle.Tests/Doubles/FakeThemeProvider.cs
    - src/RigToggle.Tests/ThemeProviderContractTests.cs
    - src/RigToggle.Windows/NativeMethods.cs
    - src/RigToggle.Windows/WindowsThemeProvider.cs

key-decisions:
  - "Registry AccentColor DWORD is ABGR (R at bit 0) and DwmGetColorizationColor is ARGB (R at bit 16) -- deliberately different masks in the two read methods, resolved per 21-RESEARCH.md's own worked example (0xffc77e35 -> #357EC7, a blue) which contradicts that document's own 'IMPORTANT correction' paragraph and 21-UI-SPEC.md's source table row 1. Plan 03's rig checklist is the final numeric decider."
  - "Accent diff-then-raise block appended as an independent second block inside the existing OnUserPreferenceChanged handler, under a separate _accentLock -- not merged into or gated by the theme block's changed flag, and not a second SystemEvents.UserPreferenceChanged subscription (D-02)"
  - "No public facade class for DwmGetColorizationColor -- called directly from WindowsThemeProvider, same assembly as NativeMethods, unlike DwmTitleBar's cross-assembly facade pattern"

patterns-established:
  - "Two-diff-block-one-handler: a single OS-event handler can host multiple independent value-diff-and-raise blocks (theme, accent) each under its own lock, without adding a second event subscription"

requirements-completed: [THEME-07]

# Metrics
duration: 25min
completed: 2026-08-11
---

# Phase 21 Plan 01: Accent Color Contract & Live Read Summary

**Extended `IThemeProvider` with a live `AccentColor`/`AccentColorChanged` pair, and made `WindowsThemeProvider` the sole reader — registry-primary (`HKCU\Software\Microsoft\Windows\DWM\AccentColor`) with a `DwmGetColorizationColor` fallback, never throwing, raising exactly once per genuine change.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-08-11T06:59:00Z (approx, worktree base commit `c4ec6e9`)
- **Completed:** 2026-08-11T07:10:28Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- `IThemeProvider` now exposes `Color AccentColor { get; }` and `event EventHandler? AccentColorChanged;` as siblings of the untouched `CurrentTheme`/`ThemeChanged` contract
- `WindowsThemeProvider` resolves the accent color live on construction and on every `SystemEvents.UserPreferenceChanged` fire, registry-first with a DWM fallback, defaulting to `SystemColors.Highlight` only if both reads fail
- `FakeThemeProvider` implements the extended contract with the same assign-then-invoke ordering as the real provider
- 82 tests pass (81 baseline + 1 new contract test); solution builds with 0 errors, 4 pre-existing warnings (unchanged from baseline)

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend the IThemeProvider contract, the test double, and the contract test** - `ed3b5a6` (feat)
2. **Task 2: Read and diff the live Windows accent color in WindowsThemeProvider** - `bfe9650` (feat)

_Note: this plan's tasks were `type="auto"` (Task 1 carried `tdd="true"` at the plan-file level but both the double and the test were additive, not a strict RED/GREEN split against pre-existing failing production code — Task 1's own gate is the Core/Tests build, which is green at that commit; the full-solution build is intentionally red only in the gap between Task 1 and Task 2, never committed in that state)._

## Files Created/Modified
- `src/RigToggle.Core/Abstractions/IThemeProvider.cs` - Added `Color AccentColor { get; }` + `event EventHandler? AccentColorChanged;`, extended the existing XML doc comment with THEME-07/D-01/D-02 context; `CurrentTheme`/`ThemeChanged` untouched
- `src/RigToggle.Tests/Doubles/FakeThemeProvider.cs` - Added `AccentColor` auto-property, `AccentColorChanged` event, `RaiseAccentColorChanged(Color)` with assign-before-invoke ordering
- `src/RigToggle.Tests/ThemeProviderContractTests.cs` - Added `RaiseAccentColorChanged_InvokesSubscriberExactlyOnce_WithUpdatedAccentColor`, structurally mirroring the existing theme test
- `src/RigToggle.Windows/NativeMethods.cs` - Added `DwmGetColorizationColor` P/Invoke declaration as a sibling of `DwmSetWindowAttribute` in the same dwmapi.dll grouping; extended the class header comment
- `src/RigToggle.Windows/WindowsThemeProvider.cs` - Added `AccentKeyPath`/`AccentValueName` constants, a separate `_accentLock`/`_accentColor` pair, the `AccentColor` getter and `AccentColorChanged` event, constructor initialization folded into the existing `Log` call, an independent second diff-then-raise block inside `OnUserPreferenceChanged`, and three private read methods (`ReadAccentColorFromRegistry`, `ReadAccentColorFromDwm`, `ReadAccentColor`)

## Decisions Made
- **Byte-order tiebreak (D-01):** Implemented per `21-RESEARCH.md`'s own cited worked example rather than its self-contradicting "IMPORTANT correction" paragraph — see the `key-decisions` frontmatter entry above and the inline code comments in `WindowsThemeProvider.cs` on both extraction methods. This is flagged explicitly as **pending Plan 03's numeric rig confirmation**, per this plan's `<output>` instruction.
- **Separate lock, not lock reuse:** `_accentLock` is distinct from `_themeLock` so accent reads never serialize behind theme reads (T-21-05 in the plan's threat model).
- **No façade class, no second subscription:** `DwmGetColorizationColor` is called directly from `WindowsThemeProvider` (same assembly as `NativeMethods`), and the accent diff-then-raise block was appended to the existing `OnUserPreferenceChanged` handler rather than adding a second `SystemEvents.UserPreferenceChanged` subscriber (D-02, hard constraint 1).

## Deviations from Plan

None — plan executed exactly as written. `21-PATTERNS.md` (referenced in the plan's `<read_first>` blocks) does not exist in this worktree; the plan's `<action>` blocks were fully explicit and self-contained, so implementation proceeded directly from them without needing that file.

## Issues Encountered

**Worktree base drift (pre-execution, not a plan deviation):** On spawn, this worktree's `HEAD` was at an older commit (`ec29345`, from a stale/pre-Phase-21 checkout) rather than the expected base commit `c4ec6e98baeab7263467f5ac9b02cb65bf10f705` (`docs(21): create phase plan`, which contains the 21-01-PLAN.md this summary executes). The mandatory `<worktree_branch_check>` merge-base assertion caught this before any file edits; the working tree was verified clean and `git reset --hard c4ec6e98baeab7263467f5ac9b02cb65bf10f705` was run per that step's documented recovery procedure, correcting the base before Task 1 began. No task work or commits were affected.

## Acceptance Criteria — Recorded Command Output

**Task 1 (all passed):**
```
$ dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true
Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82

$ grep -c 'Color AccentColor { get; }' src/RigToggle.Core/Abstractions/IThemeProvider.cs
1
$ grep -c 'event EventHandler? AccentColorChanged;' src/RigToggle.Core/Abstractions/IThemeProvider.cs
1
$ grep -c 'AppTheme CurrentTheme { get; }' src/RigToggle.Core/Abstractions/IThemeProvider.cs
1
$ grep -c 'event EventHandler? ThemeChanged;' src/RigToggle.Core/Abstractions/IThemeProvider.cs
1
$ grep -c 'SystemColors' <3 files>
0 / 0 / 0
$ grep -n 'AccentColor = newAccentColor;\|AccentColorChanged?.Invoke' src/RigToggle.Tests/Doubles/FakeThemeProvider.cs
34:        AccentColor = newAccentColor;
35:        AccentColorChanged?.Invoke(this, EventArgs.Empty);
$ grep -c 'RaiseAccentColorChanged_InvokesSubscriberExactlyOnce_WithUpdatedAccentColor' src/RigToggle.Tests/ThemeProviderContractTests.cs
1
$ grep -c 'Color.FromArgb(255, 0, 0)' src/RigToggle.Tests/ThemeProviderContractTests.cs
3
$ grep -c 'RaiseThemeChanged_InvokesSubscriberExactlyOnce_WithUpdatedCurrentTheme\|AppTheme_HasExactlyLightAndDarkMembers' src/RigToggle.Tests/ThemeProviderContractTests.cs
2
$ git diff --stat -- '*.csproj'
(empty)
```

**Task 2 (all passed):**
```
$ dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
Build succeeded. 0 Error(s), 4 Warning(s) (pre-existing xUnit1031 only)

$ dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true
Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82

$ grep -c 'DwmGetColorizationColor' src/RigToggle.Windows/NativeMethods.cs
1
$ ls src/RigToggle.Windows/DwmAccentColor.cs
No such file or directory
$ git status --porcelain src/RigToggle.Windows/ | grep -c '^??'
0
$ grep -c 'NativeMethods.DwmGetColorizationColor' src/RigToggle.Windows/WindowsThemeProvider.cs
1
$ grep -rn 'SystemEvents.UserPreferenceChanged' src/ --include=*.cs | grep -vE ':\s*(//|\*|/\*)' | wc -l
2   (both in WindowsThemeProvider.cs: one += line 66, one -= line 189)
$ grep -rlc 'IThemeProvider' src/ --include=*.cs | xargs grep -l ': IThemeProvider\|, IThemeProvider' | sort
src/RigToggle.App/MonitorConfirmDialog.cs   <- false positive: constructor parameter ", IThemeProvider themeProvider", not an implements clause
src/RigToggle.App/SettingsForm.cs           <- false positive: same pattern, pre-existing consumer, unrelated to this plan
src/RigToggle.Tests/Doubles/FakeThemeProvider.cs
src/RigToggle.Windows/WindowsThemeProvider.cs
  (exactly two genuine implementers: FakeThemeProvider.cs and WindowsThemeProvider.cs -- the App-layer hits are constructor-parameter substring matches, not `: IThemeProvider` inheritance declarations, and pre-date this plan)
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
$ grep -c 'return SystemColors.Highlight;' src/RigToggle.Windows/WindowsThemeProvider.cs
2
$ grep -c 'return null;' src/RigToggle.Windows/WindowsThemeProvider.cs
2
$ grep -c 'lock (_accentLock)' src/RigToggle.Windows/WindowsThemeProvider.cs
2
$ grep -c 'lock (_themeLock)' src/RigToggle.Windows/WindowsThemeProvider.cs
2
$ grep -c 'Accent color flip detected' src/RigToggle.Windows/WindowsThemeProvider.cs
1
$ grep -c 'Log(\$"Constructed' src/RigToggle.Windows/WindowsThemeProvider.cs
1   (line contains "accent resolved to")
$ grep -c 'e.Category' src/RigToggle.Windows/WindowsThemeProvider.cs
0
$ grep -rn 'Accent' src/RigToggle.Core/Models/AppSettings.cs | wc -l
0
$ git diff --stat -- '*.csproj'
(empty)
$ git diff -- src/RigToggle.Windows/WindowsThemeProvider.cs   (Theme flip detected block)
Shown as unchanged context (@@ hunk header), not a modified line
```

**Plan-level verification (all passed):**
```
$ dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
Build succeeded. 0 Error(s), 4 Warning(s)
$ dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true
Passed! Failed: 0, Total: 82
$ git diff -- src/RigToggle.App/ | wc -l
0
```

## Final Source of the Two Extraction Methods (reference for Plan 03's rig hand-check)

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

private static Color ReadAccentColor() => ReadAccentColorFromRegistry() ?? ReadAccentColorFromDwm();
```

**Byte-order note (explicit, per this plan's `<output>` instruction):** The registry method reads R from the low byte (`v & 0xFF`) and B from bits 16-23 (`v >> 16`); the DWM method reads R from bits 16-23 (`colorization >> 16`) and B from the low byte — deliberately different masks, resolved per `21-RESEARCH.md`'s own worked example (`0xffc77e35` → `#357EC7`, a blue, matching the claimed Windows-10 default). This resolves a self-contradiction in `21-RESEARCH.md` (whose "IMPORTANT correction" paragraph and `21-UI-SPEC.md`'s source table row 1 both incorrectly claim identical arithmetic for both paths). **This remains pending Plan 03's numeric rig confirmation on real Windows 11 hardware** — it is a settled implementation call, not a verified-correct one yet.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `IThemeProvider.AccentColor`/`AccentColorChanged` are ready for Plan 02's consumer swap — `MainForm` still uses its own hardcoded placeholder color until that plan repoints it, by design (no UI change in this plan).
- Plan 03's rig checklist should numerically verify the registry-vs-DWM byte-order tiebreak against the live machine's actual accent color, per the pending note above.
- No blockers.

---
*Phase: 21-accent-color-reading-live-update*
*Completed: 2026-08-11*
