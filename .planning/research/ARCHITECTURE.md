# Architecture Research

**Domain:** Windows WinForms desktop utility — v2.1 UI redesign (MonitorPanelForm retirement into MainForm, custom toggle-switch control, accent-color theming, manual theme override)
**Researched:** 2026-08-09
**Confidence:** HIGH (based on direct reading of the current `src/` implementation — `MainForm.cs`, `MonitorPanelForm.cs`, `ToggleOrchestrator.cs`, `ThemeApplier.cs`, `WindowsThemeProvider.cs`, `IMonitorController.cs`, `IThemeProvider.cs`, `AppSettings.cs`, `SettingsForm.cs`, `MonitorIdentifyOverlay.cs`, `DwmTitleBar.cs`, `Program.cs` — plus WebSearch-verified (MEDIUM confidence) research on the Windows accent-color API for THEME-07's net-new surface)

> **Supersedes** the previous (2026-08-04, v2.0-scoped) version of this file — that content described the now-shipped configurable-monitors/optional-targets/manual-panel milestone and is no longer current. This file is scoped entirely to v2.1.

## Standard Architecture

### System Overview

This is a 4-project .NET 10 solution (`RigToggle.Core` / `RigToggle.Windows` / `RigToggle.App` / `RigToggle.Tests`, plus a dev-time `RigToggle.IconGen`). v2.1 touches only `RigToggle.App` (WinForms UI) and adds a small amount of net-new `RigToggle.Core` (a settings-driven decorator) and `RigToggle.Windows` (an accent-color reader) — it does **not** touch `ToggleService`, `WindowsMonitorController`'s mutation methods, or `WindowsThemeProvider`'s existing OS-signal behavior.

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         RigToggle.App (WinForms UI)                       │
├──────────────────────────────────────────────────────────────────────────┤
│  ┌────────────────────────────┐   ┌───────────────────┐                  │
│  │ MainForm (redesigned)       │   │ SettingsForm        │                │
│  │  - mode indicator            │   │ (layout pass only,  │                │
│  │  - ToggleSwitch (THEME-08)  │   │  logic unchanged;   │                │
│  │  - MonitorTile[] strip      │   │  + Theme Override    │                │
│  │    (folded from             │   │    radio group)      │                │
│  │    MonitorPanelForm)        │   └─────────┬────────────┘                │
│  │  - tray icon/menu (as-is)   │             │ Save() -> callback          │
│  └───────────┬──────────────────┘             │  (mirrors ApplyTrayVisibility)│
│              │ owns/subscribes                │                            │
│  ┌───────────▼───────────────┐   ┌────────────▼──────────────┐            │
│  │ MonitorTile (NEW,          │   │ MonitorConfirmDialog        │          │
│  │ UserControl, dumb/         │   │ (unchanged)                 │          │
│  │ presentational)            │   └──────────────────────────────┘        │
│  └─────────────────────────────┘                                          │
│  ┌─────────────────────────────┐  ┌──────────────────────────────┐        │
│  │ ToggleSwitch (NEW, THEME-08) │  │ MonitorIdentifyOverlay        │        │
│  │ custom-drawn Control         │  │ (unchanged; Owner retargeted  │        │
│  │ accent-aware fill (THEME-07) │  │  from MonitorPanelForm to    │        │
│  └─────────────────────────────┘  │  MainForm)                    │        │
│                                     └──────────────────────────────┘        │
│  ThemeApplier (extended: ThemeMonitorTile / ThemeToggleSwitch helpers)     │
├──────────────────────────────────────────────────────────────────────────┤
│                          RigToggle.Core (no Windows APIs)                 │
├──────────────────────────────────────────────────────────────────────────┤
│  ToggleOrchestrator (UNCHANGED) ── BeginExclusiveMonitorAccess() lease    │
│  IThemeProvider (EXTENDED: + AccentColor, + AccentColorChanged)          │
│  OverridableThemeProvider (NEW — decorator, IThemeProvider)              │
│  AppSettings (EXTENDED: + ThemeOverride : AppTheme?)                     │
├──────────────────────────────────────────────────────────────────────────┤
│                       RigToggle.Windows (real OS adapters)                │
├──────────────────────────────────────────────────────────────────────────┤
│  WindowsMonitorController (UNCHANGED — DISPLAY-12 guard lives here only) │
│  WindowsThemeProvider (EXTENDED: same SystemEvents subscription now also │
│    reads/diffs DwmGetColorizationColor for AccentColor)                  │
└──────────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | New / Modified / Unchanged |
|-----------|----------------|------------------------|
| `MainForm` | Mode indicator, Rig/Normal toggle, **and now** the monitor-tile dashboard (enumeration, per-tile disable/enable, hotplug refresh, Identify) | **Modified** — absorbs `MonitorPanelForm`'s body |
| `MonitorTile` | Renders one monitor's icon/number/name/status; raises a click event; owns no controller reference | **New** — `UserControl`, presentation-only |
| `ToggleSwitch` | Custom-drawn on/off control replacing `btnToggle`; theme- and accent-aware paint | **New** — `Control`/`UserControl` with `OnPaint` |
| `MonitorConfirmDialog` | DISPLAY-07 informed-consent dialog before any disable | **Unchanged** — same constructor, same call sites (just relocated into `MainForm`) |
| `MonitorIdentifyOverlay` | Per-monitor numbered overlay | **Unchanged** internally — `Owner` retargeted from `MonitorPanelForm` to `MainForm` |
| `ThemeApplier` | Static per-control recolor helpers | **Modified** — add `ThemeMonitorTile`/`ThemeToggleSwitch` (or the new controls self-theme via `IsDark`, see Patterns) |
| `ToggleOrchestrator` | Reentrancy guard (`RunGuarded`) + `BeginExclusiveMonitorAccess()` lease, both on one shared `_busy` flag | **Unchanged** — reused as-is by `MainForm`'s new tile handlers |
| `WindowsMonitorController` | CCD monitor enumerate/activate/deactivate; **sole** location of the "at least one monitor enabled" guard | **Unchanged** — must remain the only guard implementation |
| `IThemeProvider` / `WindowsThemeProvider` | Live OS light/dark signal (THEME-01..06) | **Extended**, not replaced — add `AccentColor` + `AccentColorChanged`; existing `CurrentTheme`/`ThemeChanged` contract and behavior untouched |
| `OverridableThemeProvider` | Resolves effective theme = override ?? OS signal | **New** — `RigToggle.Core`, wraps `IThemeProvider`, reads `ISettingsStore` |
| `AppSettings` | Persisted user settings | **Extended** — add `ThemeOverride : AppTheme?` (nullable, `null` = follow system, consistent with every other "unset" field in this class) |
| `Program.cs` (composition root) | Wires concrete adapters | **Modified** — construct `OverridableThemeProvider` around `WindowsThemeProvider`; drop `MonitorPanelFormFactory`/`Func<MonitorPanelForm>` from `MainForm`'s constructor |

## Recommended Project Structure

No new projects or top-level folders are needed — this is additive within `RigToggle.App`, `RigToggle.Core`, and `RigToggle.Windows`:

```
src/
├── RigToggle.App/
│   ├── MainForm.cs / .Designer.cs        # MODIFIED — absorbs monitor-tile logic
│   ├── MonitorTile.cs / .Designer.cs     # NEW — reusable per-monitor UserControl
│   ├── ToggleSwitch.cs                   # NEW — custom-drawn control (THEME-08)
│   ├── SettingsForm.cs / .Designer.cs    # MODIFIED — layout pass + Theme Override group
│   ├── MonitorConfirmDialog.cs           # unchanged
│   ├── MonitorIdentifyOverlay.cs         # unchanged (Owner retarget only, in MainForm)
│   ├── ThemeApplier.cs                   # MODIFIED — new helper(s) for the two new controls
│   ├── Program.cs                        # MODIFIED — composition root wiring
│   └── (MonitorPanelForm.cs/.Designer.cs DELETED)
├── RigToggle.Core/
│   ├── Abstractions/IThemeProvider.cs    # MODIFIED — + AccentColor, + AccentColorChanged
│   ├── OverridableThemeProvider.cs       # NEW — decorator, no Windows APIs
│   └── Models/AppSettings.cs             # MODIFIED — + ThemeOverride
├── RigToggle.Windows/
│   ├── WindowsThemeProvider.cs           # MODIFIED — accent-color read/diff added to existing SystemEvents handler
│   └── NativeMethods.cs                  # MODIFIED — + DwmGetColorizationColor P/Invoke
└── RigToggle.Tests/
    └── (new unit tests for OverridableThemeProvider's override-resolution logic — pure logic, no Windows APIs, fits the existing Core test pattern)
```

### Structure Rationale

- `MonitorTile` and `ToggleSwitch` live in `RigToggle.App` (not `RigToggle.Core`) because they are WinForms `Control`/`UserControl` subclasses — same placement rule the codebase already follows for every other Form/dialog.
- `OverridableThemeProvider` belongs in `RigToggle.Core`, **not** `RigToggle.Windows`, because it contains zero Windows API calls — it is pure decision logic over an injected `IThemeProvider` + `ISettingsStore` (both already `Core` abstractions). This also makes it trivially unit-testable without a Windows CI runner, matching `RigToggle.Tests`' existing scope (Core-only, no Windows APIs) versus `RigToggle.Windows.Tests` (real adapters).
- `DwmGetColorizationColor` is a genuine Win32 API call, so it belongs in `RigToggle.Windows/NativeMethods.cs` alongside the existing `DwmSetWindowAttribute` P/Invoke, consumed through `WindowsThemeProvider` exactly as `DwmTitleBar` already consumes `NativeMethods` for the title-bar attribute.

## Architectural Patterns

### Pattern 1: Dumb presentational tile, owner performs the mutation

**What:** `MonitorTile` never calls `IMonitorController` itself. It exposes read-only setters (`FriendlyName`, `IsActive`, `IsPrimary`, `DevicePath`) and a single `event EventHandler? ActionRequested` fired on click. `MainForm` is the only caller of `_monitorController.ActivateMonitors`/`DeactivateMonitors`, exactly as `MonitorPanelForm` was the only caller before.

**When to use:** Any time a reusable child control needs to represent domain state without becoming a second place that can drift out of sync with the safety-guard logic.

**Trade-offs:** Slightly more plumbing (event + handler in `MainForm`) versus letting the tile "just call the controller" — but this is the load-bearing reason DISPLAY-12 stays a single implementation. Do not shortcut this by injecting `IMonitorController` into `MonitorTile`.

**Example:**
```csharp
public partial class MonitorTile : UserControl
{
    public string? DevicePath { get; private set; }
    public event EventHandler? ActionRequested;

    public void SetState(MonitorInfo monitor)
    {
        DevicePath = monitor.DevicePath;
        // ... update labels/paint fields ...
        Invalidate();
    }

    private void Tile_Click(object? sender, EventArgs e) => ActionRequested?.Invoke(this, EventArgs.Empty);
}

// MainForm:
tile.ActionRequested += (s, e) => OnTileAction((MonitorTile)s!);
private void OnTileAction(MonitorTile tile)
{
    var lease = TryAcquireMonitorAccess(); // ToggleOrchestrator.BeginExclusiveMonitorAccess()
    if (lease is null) return;
    using (lease)
    {
        // ... confirm dialog if disabling, then:
        _monitorController.DeactivateMonitors(new HashSet<string> { tile.DevicePath! });
        // or ActivateMonitors — same two calls MonitorPanelForm used, unchanged
    }
}
```

### Pattern 2: Decorator for theme override (Decorator over IThemeProvider)

**What:** `OverridableThemeProvider : IThemeProvider` wraps the real `WindowsThemeProvider`. `CurrentTheme => settingsStore.Load().ThemeOverride ?? inner.CurrentTheme` (read fresh every call, matching the codebase's existing "never cache `IsDark`" convention in `MainForm`/`SettingsForm`/`MonitorPanelForm`). It re-raises its own `ThemeChanged` whenever the inner provider's `ThemeChanged` fires **and** the effective (post-override) theme actually changed, plus exposes a `RefreshOverride()` method that `SettingsForm` calls immediately after `Save()` — the same "explicit live-apply callback" idiom already used for `ApplyTrayVisibility()`.

**When to use:** Any time you need to intercept/override a live OS signal without touching the class that owns that signal's subscription lifecycle.

**Trade-offs:** One extra indirection layer, but it is the only design that gets THEME-09 "for free" everywhere `IThemeProvider` is already injected (`MainForm`, `SettingsForm`, `MonitorConfirmDialog`) with **zero changes** to `WindowsThemeProvider` — this is what keeps THEME-01..06 regression risk near zero. `AccentColor`/`AccentColorChanged` deliberately pass through unmodified (the milestone scopes THEME-09 to light/dark only, not accent).

**Example:**
```csharp
public sealed class OverridableThemeProvider : IThemeProvider
{
    private readonly IThemeProvider _inner;
    private readonly ISettingsStore _settingsStore;
    public event EventHandler? ThemeChanged;
    public event EventHandler? AccentColorChanged { add => _inner.AccentColorChanged += value; remove => _inner.AccentColorChanged -= value; }

    public OverridableThemeProvider(IThemeProvider inner, ISettingsStore settingsStore)
    {
        _inner = inner; _settingsStore = settingsStore;
        _inner.ThemeChanged += (_, _) => ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public AppTheme CurrentTheme => ReadOverride() ?? _inner.CurrentTheme;
    public Color AccentColor => _inner.AccentColor;

    private AppTheme? ReadOverride()
    {
        try { return _settingsStore.Load().ThemeOverride; } catch { return null; }
    }

    // Called by SettingsForm right after _settingsStore.Save(), mirroring ApplyTrayVisibility().
    public void RefreshOverride() => ThemeChanged?.Invoke(this, EventArgs.Empty);
}
```
Note: this simplified sketch always re-raises on `RefreshOverride()`; a stricter version would diff old-vs-new effective theme first, matching `WindowsThemeProvider.OnUserPreferenceChanged`'s "only fire on genuine flip" discipline — recommended for the real implementation.

### Pattern 3: One shared SystemEvents subscription, multiple diffed signals

**What:** `WindowsThemeProvider` already subscribes once to `SystemEvents.UserPreferenceChanged` and diffs the registry-read theme against its last-known value before raising `ThemeChanged`. THEME-07 should **extend this same handler**, not add a second `UserPreferenceChanged` subscription, to read + independently diff the accent color (via `DwmGetColorizationColor`, confirmed as the standard, message/registry-consistent way to read the live DWM colorization color — see Sources) and raise `AccentColorChanged` only when it actually changed.

**When to use:** Whenever a second live signal shares the same OS notification channel as an existing one.

**Trade-offs:** Slightly larger single handler, but avoids two independent `SystemEvents` registrations racing each other and avoids a second `Dispose()`/unsubscribe path to keep in sync.

## Data Flow

### Monitor tile action flow (folded from MonitorPanelForm)

```
User clicks a MonitorTile
    ↓
MonitorTile.ActionRequested event
    ↓
MainForm.OnTileAction(tile)
    ↓
ToggleOrchestrator.BeginExclusiveMonitorAccess()  ← same _busy flag as Rig/Normal toggle
    ↓ (if disabling) MonitorConfirmDialog.ShowDialog()  — SkipMonitorConfirmation gate, unchanged
    ↓
IMonitorController.DeactivateMonitors / ActivateMonitors  ← DISPLAY-12 guard lives ONLY here
    ↓
RefreshMonitorTiles() (re-enumerate GetAllMonitors(), update each MonitorTile.SetState)
    ↓
lease.Dispose() (releases _busy)
```

### Hotplug refresh flow

```
Microsoft.Win32.SystemEvents.DisplaySettingsChanged (may fire off UI thread)
    ↓
MainForm.OnDisplaySettingsChanged  — IsDisposed check, InvokeRequired/BeginInvoke marshal
    ↓
RefreshMonitorTiles()
```
Subscribed once in `MainForm`'s constructor (no unsubscribe needed — `MainForm` is an app-lifetime object that is only ever `Hide()`'d, never disposed, until process exit; this mirrors `MainForm`'s existing un-unsubscribed `_themeProvider.ThemeChanged += OnThemeChanged` wiring already in the constructor today).

### Theme resolution flow (with override)

```
SystemEvents.UserPreferenceChanged (OS theme OR accent flip)
    ↓
WindowsThemeProvider: re-read registry, diff, raise ThemeChanged / AccentColorChanged
    ↓
OverridableThemeProvider: re-raise ThemeChanged (passes through AccentColorChanged unmodified)
    ↓
MainForm/SettingsForm/MonitorConfirmDialog.OnThemeChanged (existing handlers, UNCHANGED)
    ↓
IsDark => _themeProvider.CurrentTheme == AppTheme.Dark   ← now resolves override transparently

SettingsForm Save() (user changed the override radio group)
    ↓
_settingsStore.Save(settingsToSave)   ← includes new ThemeOverride field
    ↓
_applyThemeOverride()  (new callback, called right after Save — same slot as _applyTrayVisibility())
    ↓
OverridableThemeProvider.RefreshOverride() → ThemeChanged fires → every open Form re-themes live
```

### Key Data Flows

1. **Tile mutation never bypasses the shared guard:** every path that ends in `ActivateMonitors`/`DeactivateMonitors` — Rig toggle, Normal toggle, and now the tile click — funnels through `WindowsMonitorController`'s single implementation. Folding the panel into `MainForm` does not create a second call site with its own pre-check; it reuses the exact two method calls.
2. **Theme override never touches the OS-signal path:** `WindowsThemeProvider` keeps reading the registry and raising events exactly as it does today; the override is purely a read-time (`CurrentTheme` getter) and event-relay (constructor subscription) addition in a wrapping class that every consumer already treats as "the `IThemeProvider`" via DI.

## Scaling Considerations

This is a single-user desktop utility — there is no traditional user-scale axis. The only "scale" variable is **monitor count**:

| Monitor count | Architecture Adjustments |
|-------|--------------------------|
| 1-4 (typical desk + rig) | `FlowLayoutPanel`/`TableLayoutPanel` of `MonitorTile` instances fits comfortably in a fixed-size `MainForm` |
| 5-8 (unusual but plausible multi-monitor desk) | `FlowLayoutPanel` wraps to multiple rows automatically if `MainForm` isn't resized to fit — verify tile sizing keeps the window at a reasonable default height, or make the tile strip area scrollable (`AutoScroll = true` on the host panel) rather than growing `MainForm` unboundedly |
| 9+ | Out of scope for a personal-rig tool; no special handling needed beyond the scroll fallback above |

### Scaling Priorities

1. **First (and only realistic) concern:** tile-strip layout at higher monitor counts than the 2-monitor rig this app is built for — mitigate with `AutoScroll` on the tile container, not a redesign.
2. No second-order concern exists at this project's scale (single user, local-only state, no network/database).

## Anti-Patterns

### Anti-Pattern 1: Giving `MonitorTile` its own `IMonitorController` reference

**What people do:** Inject `IMonitorController` into the tile control "for convenience" so it can call `Activate/DeactivateMonitors` directly on click, mirroring how `MonitorPanelForm` itself held the controller.
**Why it's wrong:** Creates a second call site for monitor mutation outside `MainForm`, which is exactly the kind of duplication DISPLAY-12's "single shared implementation" was designed to prevent (the guard itself is still centralized in `WindowsMonitorController`, but a second caller doubles the surface area for the exclusive-access lease and confirm-dialog logic to drift out of sync).
**Do this instead:** `MonitorTile` raises an event; `MainForm` (the sole owner of `_monitorController` and `_orchestrator`) performs the mutation, exactly as documented in Pattern 1 above.

### Anti-Pattern 2: A second `SystemEvents.UserPreferenceChanged` or `DisplaySettingsChanged` subscription

**What people do:** Add a fresh `SystemEvents` subscription inside the new `ToggleSwitch`/`MonitorTile` controls (or a new standalone accent-color class) instead of extending the existing `WindowsThemeProvider` handler / `MainForm` constructor subscription.
**Why it's wrong:** Multiple independent subscribers to the same OS notification each do their own registry read and diff, multiplying redundant work and risking inconsistent "did it actually change" conclusions between subscribers (e.g., one fires, the other doesn't, for the same OS event).
**Do this instead:** One `WindowsThemeProvider` instance remains the single owner of `SystemEvents.UserPreferenceChanged`; one `MainForm` instance remains the single owner of `SystemEvents.DisplaySettingsChanged`. Every UI control reacts via events/property reads, never via its own OS subscription.

### Anti-Pattern 3: Caching `IThemeProvider.CurrentTheme` in a field

**What people do:** Read `_themeProvider.CurrentTheme` once at construction and store it in a `bool _isDark` field for reuse in `OnPaint`.
**Why it's wrong:** Breaks live theme-follow (THEME-01..06) — the codebase's existing convention (`MainForm.IsDark`, `SettingsForm.IsDarkTheme`, `MonitorPanelForm.IsDarkTheme`, all fresh-read properties) exists specifically to keep every re-theme correct across a live OS flip or a manual override flip. `ToggleSwitch.OnPaint` and `MonitorTile.OnPaint` must call the fresh property every paint, not a cached field.
**Do this instead:** Keep a `bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark` property pattern, read at paint time, identical to every existing Form in this codebase.

### Anti-Pattern 4: Wiring the manual override directly into `WindowsThemeProvider`

**What people do:** Add an `if (settings.ThemeOverride is not null) return settings.ThemeOverride.Value;` branch inside `WindowsThemeProvider.CurrentTheme`'s getter (or its constructor), since "it's right there."
**Why it's wrong:** Couples the pure OS-signal reader to `ISettingsStore` and to Settings-Save timing, increasing the regression surface for THEME-01..06 (which are already rig-verified against the current, override-free `WindowsThemeProvider` behavior) and makes `WindowsThemeProvider` harder to unit-test in isolation (`RigToggle.Windows.Tests` currently has no settings-store dependency for this class).
**Do this instead:** The Decorator in Pattern 2 — `WindowsThemeProvider` stays exactly as it is today; `OverridableThemeProvider` is the only new/modified consumer of `ISettingsStore` for this feature.

## Integration Points

### External Services

N/A — no network/cloud services in this project. The only "external" surfaces are Win32/COM APIs, already covered by existing `RigToggle.Windows` adapters (`WindowsDisplayAPI`/CCD, `IPolicyConfig` COM, `NAudio`, DWM P/Invoke) — v2.1 adds exactly one new Win32 surface, `DwmGetColorizationColor` (dwmapi.dll), for THEME-07.

| API | Integration Pattern | Notes |
|---------|---------------------|-------|
| `DwmGetColorizationColor` (dwmapi.dll) | `[DllImport]` in `NativeMethods.cs`, called from `WindowsThemeProvider`'s existing `OnUserPreferenceChanged` handler | Returns `0xAARRGGBB`; also confirm-verify live on the rig that `SystemEvents.UserPreferenceChanged` actually fires for an accent-color-only change (WebSearch-sourced, MEDIUM confidence — community sources confirm accent changes are reported through the same `UserPreferenceChanged`/color-category channel used for theme, but this project's own established practice is to rig-verify every theming assumption before shipping, per the Phase 12 gap-closure precedent where two "should work" bets were rig-disproven) |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `MonitorTile` ↔ `MainForm` | Event (`ActionRequested`) + method calls (`SetState`) | Tile is presentation-only; `MainForm` owns all controller/orchestrator calls (Pattern 1) |
| `ToggleSwitch` ↔ `MainForm` | Standard WinForms event (`CheckedChanged`/`Click`), replaces `btnToggle.Click` | Drop-in replacement; `BtnToggle_Click` body is otherwise unchanged |
| `MainForm`/`SettingsForm`/`MonitorConfirmDialog` ↔ `IThemeProvider` | Property read (`CurrentTheme`) + event (`ThemeChanged`) | Unchanged interface usage; the injected instance becomes `OverridableThemeProvider` instead of `WindowsThemeProvider` directly, but callers cannot tell the difference |
| `SettingsForm` ↔ composition root | Constructor-injected callback delegates (`tryRegisterConfiguredHotkey`, `applyTrayVisibility`, **+ new** `applyThemeOverride`) | Follow the exact existing pattern — do not invent a new mechanism (e.g., an event on `AppSettings`) for THEME-09's live-apply |
| `MainForm` ↔ `ToggleOrchestrator` | `BeginExclusiveMonitorAccess()` lease (tile actions) + `ToggleToRigMode()`/`ToggleToNormalMode()` (switch) | Both now originate from the same class/thread; no new concurrency behavior — WinForms' single UI-thread message pump plus the pre-existing "acquire lease before `ShowDialog()`'s nested loop" discipline (ported verbatim from `MonitorPanelForm`) is sufficient, unchanged from today |

## Recommended Build Order

1. **Build `MonitorTile` as a standalone UserControl first, unwired.** Public surface: `DevicePath`, `SetState(MonitorInfo)`, `event ActionRequested`. No `IMonitorController` reference (Pattern 1/Anti-Pattern 1). This validates the custom-drawn visuals and theming hooks in isolation, with zero risk to the currently-working toggle/panel logic.
2. **Add a tile-strip container to `MainForm`'s Designer and populate it read-only** from `_monitorController.GetAllMonitors()` — no click wiring yet. Validates enumeration + layout (including the `AutoScroll` fallback from Scaling Considerations) without any mutation risk.
3. **Port the mutation logic verbatim from `MonitorPanelForm` into `MainForm`**: `BeginExclusiveMonitorAccess()` lease acquired before `MonitorConfirmDialog.ShowDialog()`, the WR-03 devicePath re-validation after the dialog's nested message loop, `DeactivateMonitors`/`ActivateMonitors` called directly (never re-implement the guard), `SkipMonitorConfirmation` gate reused as-is. Copy-and-adapt, not a rewrite — this is the step where DISPLAY-12/lease-semantics regressions would be introduced if done carelessly.
4. **Port hotplug refresh (`SystemEvents.DisplaySettingsChanged`) and the Identify action**, retargeting `MonitorIdentifyOverlay`'s `Owner` from the old panel to `MainForm`.
5. **Delete `MonitorPanelForm.cs`/`.Designer.cs` and its two entry points (`btnMonitors`, `trayMonitorsMenuItem`) last**, only once steps 3-4 are proven working — deleting the reference implementation before porting its logic risks silently dropping a Phase-17 fix (e.g., the WR-03 re-validation, or "Identify enumerates in display order, not state.Paths order").
6. **THEME-07 (`IThemeProvider`/`WindowsThemeProvider` accent-color extension)** can be built any time — it has no dependency on the monitor-tile work. Do it before step 7 if `ToggleSwitch`'s "on" fill is meant to be accent-colored.
7. **THEME-08 (`ToggleSwitch`)**: build as its own standalone control (same isolation principle as step 1), swap it in for `btnToggle` last, after the monitor-tile folding is stable, to avoid compounding two risky UI changes in one uncommitted diff.
8. **THEME-09 (`OverridableThemeProvider` + Settings UI + composition-root wiring)** last among the theme work — it decorates whatever `IThemeProvider` looks like after step 6, and per the milestone's own scoping ("a manual **light/dark** override... independent of live Windows theme-follow") it overrides `CurrentTheme` only, passing `AccentColor`/`AccentColorChanged` through untouched — so it has no ordering dependency on step 6 beyond "the interface exists."

## Sources

- Direct reading of current implementation (HIGH confidence, this repository): `src/RigToggle.App/MonitorPanelForm.cs`, `MainForm.cs`, `ThemeApplier.cs`, `MonitorIdentifyOverlay.cs`, `MonitorConfirmDialog.cs`, `SettingsForm.cs` (Save handler + constructor), `Program.cs`; `src/RigToggle.Core/ToggleOrchestrator.cs`, `Abstractions/IThemeProvider.cs`, `Abstractions/IMonitorController.cs`, `Models/AppSettings.cs`, `Models/AppTheme.cs`; `src/RigToggle.Windows/WindowsThemeProvider.cs`, `DwmTitleBar.cs`
- `.planning/PROJECT.md` — Key Decisions table, specifically: `ToggleOrchestrator.BeginExclusiveMonitorAccess()` lease sharing `_busy` (Phase 17), Manual Monitor Panel mutating through the exact same `IMonitorController` calls the toggle uses (Phase 17, DISPLAY-12 single-shared-implementation), explicit-color `FlatStyle.Flat` theming and manual `DWMWA_USE_IMMERSIVE_DARK_MODE` (Phase 12)
- [WM_DWMCOLORIZATIONCOLORCHANGED message — Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/dwm/wm-dwmcolorizationcolorchanged) — MEDIUM-HIGH confidence, official docs, confirms message constant and 0xAARRGGBB format
- [DwmGetColorizationColor function — Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmgetcolorizationcolor) — MEDIUM-HIGH confidence, official docs, confirms the API this project should P/Invoke for THEME-07
- Community sources on `SystemEvents.UserPreferenceChanged` firing for accent-color changes and the `HKCU\Software\Microsoft\Windows\DWM\AccentColor` registry location — MEDIUM confidence (forum/blog sources, not official docs); flagged in Integration Points above as needing the same rig-verification discipline this project already applies to theming bets (Phase 12 precedent: two "should just work" assumptions about `Application.SetColorMode` were rig-disproven)

---
*Architecture research for: Windows WinForms desktop utility — v2.1 UI redesign*
*Researched: 2026-08-09*
