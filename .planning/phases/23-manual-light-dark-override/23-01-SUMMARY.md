---
phase: 23-manual-light-dark-override
plan: 01
subsystem: ui
tags: [winforms, theme, decorator-pattern, dotnet10]

requires:
  - phase: 21-accent-color-reading-live-update
    provides: IThemeProvider extended with AccentColor/AccentColorChanged, the interface this plan decorates
provides:
  - AppSettings.ThemeOverride nullable AppTheme? field (null = System/live-follow)
  - RigToggle.Core.OverridableThemeProvider — the single shared effective-theme resolver (preview ?? persisted override ?? live OS signal)
  - Composition-root swap in Program.cs so all three IsDark/IsDarkTheme copies (MainForm, SettingsForm, MonitorConfirmDialog) resolve through the decorator with zero per-form edits
  - ThemeApplier.ApplyEffectiveColorMode/ThemeFormSurface — application color mode now derived from the effective theme, not hardcoded to follow the OS
affects: [23-02-manual-light-dark-override-ui, 23-03-manual-light-dark-override-verification]

actuals:
  tokens: 6735
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "IThemeProvider decorator resolving preview ?? persisted-override ?? live-signal, memoizing only the persisted read (never per-CurrentTheme-call) to avoid a settings-file load per repaint"
    - "Application-wide color mode derived from the effective theme at every theming call site plus one composition-root priming call, instead of a single fire-and-forget System mode"

key-files:
  created:
    - src/RigToggle.Core/OverridableThemeProvider.cs
    - src/RigToggle.Tests/OverridableThemeProviderTests.cs
  modified:
    - src/RigToggle.Core/Models/AppSettings.cs
    - src/RigToggle.App/Program.cs
    - src/RigToggle.App/ThemeApplier.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/MonitorConfirmDialog.cs
    - src/RigToggle.Tests/Doubles/InMemoryStores.cs

key-decisions:
  - "ReadPersistedOverride() is the sole _settingsStore.Load() call site, invoked only from the constructor and RefreshOverride() (never per CurrentTheme read) — asserted by the plan's own grep-count acceptance criteria (exactly 1 Load() call, exactly 3 ReadPersistedOverride occurrences)"
  - "ThemeChanged is re-raised unconditionally on every inner OS flip, deliberately NOT diffed against the effective theme — an explicit deviation from ARCHITECTURE.md's suggested refinement, recorded in the plan's <interfaces> section and mirrored in the class doc comment"
  - "themeProvider declared via var (concrete OverridableThemeProvider) at the Program.cs composition root, not IThemeProvider, so Plan 23-02 can pass SetPreviewOverride/RefreshOverride as method-group arguments from the same local"
  - "Application.SetColorMode is now called from ThemeApplier.ApplyEffectiveColorMode plus the composition-root priming call — no in-form call site was left calling the raw API directly; grep confirms exactly two non-comment SetColorMode call sites project-wide (Program.cs, ThemeApplier.cs)"

patterns-established:
  - "OverridableThemeProvider decorator: constructed once at the composition root wrapping WindowsThemeProvider, threaded everywhere IThemeProvider was already injected — zero consumer-side code changes needed for override awareness"
  - "ApplyEffectiveColorMode/ThemeFormSurface: two new ThemeApplier helpers, each wrapped in the class's existing try/catch fail-silent shape"

requirements-completed: [THEME-09]

coverage:
  - id: D1
    description: "AppSettings.ThemeOverride persists a nullable AppTheme? (null = System/live-follow); OverridableThemeProvider.CurrentTheme resolves preview ?? persisted-override ?? live-OS-signal so a Dark/Light override locks the whole app's effective theme regardless of the live Windows signal"
    requirement: "THEME-09"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#CurrentTheme_PersistedDarkOverride_WinsOverLiveLightSignal"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#CurrentTheme_PersistedLightOverride_WinsOverLiveDarkSignal"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#CurrentTheme_NullOverride_FallsThroughToLiveLightSignal"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#CurrentTheme_NullOverride_FallsThroughToLiveDarkSignal"
        status: pass
    human_judgment: false
  - id: D2
    description: "A live OS theme flip while an override is set does not change what any surface resolves; with no override set, live flips still propagate exactly as before (THEME-01..06 no-regression)"
    requirement: "THEME-09"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#CurrentTheme_LiveFlip_DoesNotChangeWhileOverrideIsSet"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#CurrentTheme_LiveFlip_ChangesWhenOverrideIsNull"
        status: pass
    human_judgment: false
  - id: D3
    description: "SetPreviewOverride/RefreshOverride give SettingsForm (Plan 23-02) a live-preview-then-persist-then-revert lifecycle: preview takes precedence over the persisted value including previewing null, and RefreshOverride drops an active preview and re-reads the persisted value, each raising ThemeChanged exactly once"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#SetPreviewOverride_TakesPrecedenceOverPersistedValue"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#SetPreviewOverride_Null_PreviewsSystemWhileDarkIsPersisted"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#RefreshOverride_DropsActivePreview_ReturnsToPersistedValue"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#SetPreviewOverride_RaisesThemeChangedExactlyOnce"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#RefreshOverride_RaisesThemeChangedExactlyOnce"
        status: pass
    human_judgment: false
  - id: D4
    description: "A store failure or an out-of-range persisted enum value degrades silently to System (live-follow) — no exception, no log, no error UI"
    requirement: "THEME-09"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#CurrentTheme_ThrowingStore_ResolvesToLiveSignalInsteadOfThrowing"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#CurrentTheme_OutOfRangeOverride_ResolvesToLiveSignal"
        status: pass
    human_judgment: false
  - id: D5
    description: "AccentColor/AccentColorChanged pass through the inner provider unchanged — THEME-09 is scoped to the light/dark axis only"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#AccentColor_PassesThroughInnerProviderUnchanged"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/OverridableThemeProviderTests.cs#AccentColorChanged_PassesThroughInnerProviderUnchanged"
        status: pass
    human_judgment: false
  - id: D6
    description: "Application color mode is derived from the effective theme at every theming call site (MainForm's ApplyDashboardTheming, SettingsForm's OnThemeChanged/Load, MonitorConfirmDialog's OnThemeChanged/constructor) and primed once at the composition root before any Form exists, so native controls cannot follow the OS behind a locked override — this is a visual/runtime claim only rig hardware can confirm, deferred to 23-03"
    requirement: "THEME-09"
    verification: []
    human_judgment: true
    rationale: "Native-control recoloring and the absence of a startup theme flash are visual/runtime behaviors this Linux build host cannot render or observe — deferred to Plan 23-03's blocking rig checkpoint per the plan's own <verification> section."
  - id: D7
    description: "WindowsThemeProvider, all three IsDark/IsDarkTheme property bodies, and every .csproj are unchanged; build is green and the test suite grew by 15 passing tests with zero failures (97 total vs 82 baseline)"
    verification:
      - kind: unit
        ref: "dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj --nologo (Passed: 97, Failed: 0)"
        status: pass
      - kind: other
        ref: "git diff --stat bbbfebc -- src/RigToggle.Windows/ '*.csproj' (empty)"
        status: pass
    human_judgment: false

duration: ~35min
completed: 2026-08-16
status: complete
---

# Phase 23 Plan 1: OverridableThemeProvider & Effective-Theme Resolver Summary

**One shared `OverridableThemeProvider` decorator (preview ?? persisted `ThemeOverride` ?? live OS signal) wired into every `IThemeProvider` consumer via a single composition-root swap, plus an application-wide color mode now derived from that effective theme instead of hardcoded OS-follow.**

## Performance

- **Duration:** ~35 min
- **Completed:** 2026-08-16T20:58:11Z
- **Tasks:** 2
- **Files modified:** 9 (2 created, 7 modified)

## Accomplishments

- `AppSettings.ThemeOverride` — new nullable `AppTheme?` field (`null` = System/live-follow), following the class's existing "null = unset" convention
- `RigToggle.Core.OverridableThemeProvider` — the single shared effective-theme resolver: `CurrentTheme` resolves `preview ?? persisted-override ?? live-OS-signal`; `SetPreviewOverride`/`RefreshOverride` give 23-02's Save/Discard/live-preview UI exactly the two hooks it needs; `AccentColor`/`AccentColorChanged` pass through unchanged; the persisted read is memoized and re-read only in the constructor and `RefreshOverride()` (never per `CurrentTheme` call, since `IsDark` is read from `OnPaint` handlers)
- Composition-root swap in `Program.cs`: `WindowsThemeProvider` is now wrapped by `OverridableThemeProvider` before being handed to `MainForm`/`SettingsFormFactory` — `MainForm.IsDark`, `SettingsForm.IsDarkTheme`, and `MonitorConfirmDialog.IsDark` all gained override awareness with **zero** edits to any of the three property bodies (D-04/Pitfall 6)
- `ThemeApplier.ApplyEffectiveColorMode`/`ThemeApplier.ThemeFormSurface` — two new helpers; the application-wide `Application.SetColorMode` call is now derived from the effective theme at every theming call site (`MainForm.ApplyDashboardTheming`, `SettingsForm.OnThemeChanged`/`SettingsForm_Load`, `MonitorConfirmDialog.OnThemeChanged`/constructor) plus one priming call in `Program.cs` before any `Form` is constructed
- 15 new unit tests (`OverridableThemeProviderTests.cs`) covering override precedence, preview precedence, live-flip suppression while overridden, `RefreshOverride`'s dual role, `ThemeChanged` raise counts, fail-silent degradation on a throwing store and an out-of-range persisted enum value, and `AccentColor`/`AccentColorChanged` pass-through

## Task Commits

Each task was committed atomically:

1. **Task 1: End-to-end effective-theme resolution — one persisted value locks the whole app** - `ab738ed` (feat)
2. **Task 2: Stop the application color mode from following the OS while an override is set** - `c4ff4c4` (feat)

_No separate TDD RED/GREEN commits — this plan's tasks are `type="tracer"` and `type="auto"`, not `tdd="true"`._

## Files Created/Modified

- `src/RigToggle.Core/OverridableThemeProvider.cs` - new decorator, the single shared effective-theme resolver
- `src/RigToggle.Tests/OverridableThemeProviderTests.cs` - 15 new unit tests
- `src/RigToggle.Core/Models/AppSettings.cs` - added `ThemeOverride` nullable field + doc comment
- `src/RigToggle.Tests/Doubles/InMemoryStores.cs` - added `ThrowingSettingsStore` test double
- `src/RigToggle.App/Program.cs` - composition-root swap (`OverridableThemeProvider` wraps `WindowsThemeProvider`) + one priming `ApplyEffectiveColorMode` call
- `src/RigToggle.App/ThemeApplier.cs` - added `ApplyEffectiveColorMode`/`ThemeFormSurface`
- `src/RigToggle.App/MainForm.cs` - color mode + form surface now applied from inside `ApplyDashboardTheming()`, reaching both the `OnThemeChanged` and `InitializeTrayState()` call sites structurally
- `src/RigToggle.App/SettingsForm.cs` - color mode applied at `OnThemeChanged` and `SettingsForm_Load`
- `src/RigToggle.App/MonitorConfirmDialog.cs` - color mode applied at `OnThemeChanged` and the constructor

## Resolution Order & Public Surface (for 23-02/23-03)

`OverridableThemeProvider.CurrentTheme` resolution order (exact expression):
```csharp
_hasPreview ? (_previewOverride ?? _inner.CurrentTheme) : (_persistedOverride ?? _inner.CurrentTheme)
```

Public surface:
```csharp
public sealed class OverridableThemeProvider : IThemeProvider
{
    public OverridableThemeProvider(IThemeProvider inner, ISettingsStore settingsStore);
    public AppTheme CurrentTheme { get; }
    public event EventHandler? ThemeChanged;
    public Color AccentColor { get; }
    public event EventHandler? AccentColorChanged; // add/remove pass-through to inner
    public void SetPreviewOverride(AppTheme? previewOverride);
    public void RefreshOverride();
}
```

Persisted read helper: `ReadPersistedOverride()` — the sole `_settingsStore.Load()` call site, invoked exactly twice (constructor, `RefreshOverride()`).

Persisted JSON representation of `ThemeOverride` (integer, no string-enum converter): `0` = Light, `1` = Dark, absent/`null` = System.

`Application.SetColorMode` call sites (comment-filtered, project-wide): `src/RigToggle.App/Program.cs:42` (unconditional `System` priming before settings/theme provider exist) and `src/RigToggle.App/ThemeApplier.cs` (inside `ApplyEffectiveColorMode`, the one place that now maps `dark` to `SystemColorMode.Dark`/`Classic`).

`ThemeApplier` new helper signatures:
```csharp
public static void ApplyEffectiveColorMode(bool dark);
public static void ThemeFormSurface(Form form, bool dark);
```

## Verbatim Build/Test Output

```
dotnet build RigToggle.sln --nologo
...
Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj --nologo
...
Passed!  - Failed:     0, Passed:    97, Skipped:     0, Total:    97, Duration: 95 ms - RigToggle.Tests.dll (net10.0)
```

## Decisions Made

- `ReadPersistedOverride()` is the single named helper wrapping `_settingsStore.Load()`, satisfying the plan's exact grep-count acceptance criteria (1 `Load()` call site, 3 `ReadPersistedOverride` occurrences: declaration + 2 call sites).
- `ThemeChanged` is re-raised unconditionally on every inner OS flip rather than diffed against the effective value — a documented, deliberate deviation from `ARCHITECTURE.md`'s suggested refinement (see the class's own doc comment and the plan's `<interfaces>` "diff-before-raise is deliberately NOT implemented" note).
- `themeProvider` in `Program.cs` is declared via `var` (concrete `OverridableThemeProvider`), not `IThemeProvider`, so Plan 23-02 can pass `SetPreviewOverride`/`RefreshOverride` as method-group arguments from the same local without a downcast.
- `ThrowingSettingsStore` was added to `InMemoryStores.cs` (not `files_modified` in the plan's frontmatter, but explicitly named in Task 1's `<files>` list) — a minimal `ISettingsStore` double whose `Load()`/`Save()` both throw, mirroring the existing `ThrowingClearToggleInProgressStore` precedent.

## Deviations from Plan

None — plan executed exactly as written. One inline self-correction during Task 2: the first draft of `MainForm.OnThemeChanged`'s comment restated the literal method name `ApplyDashboardTheming()`, which caused the acceptance criterion's `awk`+`grep` count for that method to read 2 instead of the required 1 (comment text plus the real call). Reworded the comment to describe the helper without repeating its exact name; verified the grep count dropped to 1 before proceeding — not a Rule 1-4 deviation, just an acceptance-criterion-driven wording fix caught before commit.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `OverridableThemeProvider.SetPreviewOverride`/`RefreshOverride` and the `themeProvider` local (concrete type, not interface) are ready for Plan 23-02 to wire into `SettingsForm`'s new radio group (immediate-apply preview on click, `RefreshOverride()` after Save and on Discard/close).
- `ThemeApplier.ApplyEffectiveColorMode`/`ThemeFormSurface` are in place for 23-02's live-preview repaint path to reuse (no new theming helpers should be needed).
- All visual/runtime claims (native-control recoloring under an override, absence of a startup theme flash, live-OS-flip suppression on real hardware) remain unverified on this Linux build host — deferred to 23-03's blocking rig checkpoint per this plan's own `<verification>` section.
- No blockers.

---
*Phase: 23-manual-light-dark-override*
*Completed: 2026-08-16*
