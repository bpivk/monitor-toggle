# Phase 12: Theme Infrastructure & Live Theme-Following - Research

**Researched:** 2026-08-02
**Domain:** WinForms system-theme-following (dark/light mode) for an existing shipped .NET 10 tray-resident desktop app
**Confidence:** HIGH (API contracts verified directly against official Microsoft Learn docs and the real source tree); MEDIUM on two specific runtime-behavior questions flagged explicitly in Open Questions (live-update scope for transient dialogs, `SystemEvents` threading)

## Summary

This phase adds two things to an already-shipped 4-project WinForms solution: (1) launch-time light/dark theming of `MainForm` and `SettingsForm` that matches Windows' current theme, including title bar, controls, and Windows-11 Mica/rounded-corner chrome, and (2) live re-theming when the user flips the Windows theme setting while the app keeps running (including while hidden in the tray). Both capabilities build on **`Application.SetColorMode(SystemColorMode.System)`**, a fully non-experimental, GA WinForms API as of .NET 10 (confirmed directly against the official "What's new in WinForms for .NET 10" doc and the `Application.SetColorMode` API reference — the `WFO5001` experimental gate that applied in .NET 9 is removed in .NET 10). This single call, made once before any control is constructed, themes the title bar (via WinForms' own internal `DWMWA_USE_IMMERSIVE_DARK_MODE` call) and most standard controls (`Label`, `TextBox`, `ComboBox`, `CheckBox`, `Button`, `ToolStrip`) automatically, on Windows 11 only (Windows 10 silently stays light/classic; High Contrast mode disables dark mode automatically — both confirmed in the official API remarks).

This built-in mechanism has two confirmed, documented gaps that this phase's hand-rolled code must close: (a) **no live-update** — `Application.SetColorMode`'s own docs state verbatim "If the system setting is changed, the application will not automatically adapt to the new setting," corroborated independently by the open, unresolved `dotnet/winforms#13935`; and (b) **known-incomplete control coverage** for `DataGridView` with mixed column types (including checkbox columns — exactly this app's `dgvMonitors` grid) per `dotnet/winforms#11893` and corroborating sources, and `GroupBox`'s bevel border, which has no flat variant regardless of color mode. `MessageBox` also stays permanently light-themed by design (native Win32 dialog, outside WinForms' rendering control) — this phase's `CONTEXT.md` explicitly accepts this as a deliberate tradeoff (D-01), not a gap to patch.

**Primary recommendation:** Use `Application.SetColorMode(SystemColorMode.System)` as the base layer for THEME-01/02/03 (call once, at the very top of `Program.cs`'s `Main()`, before `new MainForm(...)`); patch the live-update gap with a `Microsoft.Win32.SystemEvents.UserPreferenceChanged`-driven `IThemeProvider`/`WindowsThemeProvider` (Core/Windows split mirroring `IAutostartConfigurator`) that re-invokes `SetColorMode` and reapplies the DWM Mica/corner calls on every open form when the theme actually changes; and hand-write targeted per-control overrides only for the two confirmed gaps — `DataGridView` cell/header styling (THEME-04) and `GroupBox`→`Panel`+`Label` replacement (THEME-05) — rather than a full recursive recolor pass over every control on the form.

**Important reconciliation:** This milestone's own `ARCHITECTURE.md` (research artifact, written earlier the same day as this phase's other research) recommends a fully hand-rolled approach that explicitly avoids `Application.SetColorMode` entirely (its "Anti-Pattern 1"). This phase's `CONTEXT.md` (user-approved, written after `ARCHITECTURE.md`, decisions D-04/D-05) **overrides** that recommendation and locks in the `SetColorMode`-as-base-layer approach instead — which is also what this milestone's own `PITFALLS.md` independently arrived at ("Important Correction to Milestone Framing"). **Follow `CONTEXT.md` D-04/D-05, not `ARCHITECTURE.md`'s Anti-Pattern 1.** `ARCHITECTURE.md`'s Core/Windows/App project-placement guidance (`IThemeProvider` in Core, `WindowsThemeProvider`/DWM P/Invoke in Windows) and its transient-dialog-subscription-lifecycle guidance remain valid and should still be followed — only the "hand-roll every control's colors yourself" framing is superseded.

## User Constraints (from CONTEXT.md)

<user_constraints>

### Locked Decisions

- **D-01 (MessageBox):** MainForm's 4 existing `MessageBox.Show()` call sites (toggle-result checklist, warnings) stay native/unthemed — deliberate, accepted tradeoff. Do not build a themed MessageBox replacement or apply DWM theming to MessageBox's window handle.
- **D-02 (Windows 11 rounded corners / Mica):** Use `DWMWA_SYSTEMBACKDROP_TYPE` with standard **Mica** (not Mica Alt, not Acrylic — `DWMSBT_MAINWINDOW`, value 2). Apply alongside `DWMWA_WINDOW_CORNER_PREFERENCE` (rounded — `DWMWCP_ROUND`, value 2) on both MainForm and SettingsForm. Same best-effort/non-throwing posture as the dark-title-bar call.
- **D-03 (ToolStrip stale-color live-flip bug):** Accept the known, unfixed `dotnet/winforms#12027` bug (tray context menu separators/dropdown arrows keep pre-flip color after a live theme change) as a documented limitation. Do NOT build a rebuild-the-menu-on-theme-change workaround. Note it explicitly in code comments.
- **D-04 (base layer):** Base layer is .NET 10's built-in `Application.SetColorMode(SystemColorMode.System)`, called once at startup in `Program.cs` — do NOT hand-roll the full recolor pass PROJECT.md originally assumed was necessary; that assumption is known-outdated. `SetColorMode` handles launch-time control coloring and the title bar for free.
- **D-05 (live-following patch):** The one confirmed gap is live theme-following — `SetColorMode` does not react to the user flipping Windows' theme setting mid-session. Close this gap with a small hand-rolled patch: `Microsoft.Win32.SystemEvents.UserPreferenceChanged` subscription (BCL, zero new packages) that diffs old-vs-new theme state (the event fires on many unrelated preference changes too) and re-applies theming when it actually changed.
- **D-06 (registry key scoping):** Use `HKCU\...\Personalize\AppsUseLightTheme` for app/control chrome (title bar, controls) — different, independently-settable value from `SystemUsesLightTheme` (taskbar, Phase 13's concern). Do not conflate the two.
- **D-07 (Windows 10 / API-unavailable fallback):** Every DWM attribute call (dark title bar, rounded corners, Mica) must be wrapped so a failed/no-op `DwmSetWindowAttribute` call never throws or crashes the app — best-effort visual enhancement only. Flat control styling and control recoloring (THEME-04/05) are NOT Windows-11-gated and should still apply on Windows 10. Confirming the DWM dark-mode attribute value and whether it actually applies on the target Windows 10 build is left as a rig-verification item — carry a `checkpoint:human-verify`.
- **D-08 (`--tray` hidden-start timing):** Because `Program.cs`'s `--tray` startup path never calls `Form.Show()`/`Form.Load()` (Phase 8 pattern), theme-application code for MainForm must NOT live in `Form_Load`/`OnShown` — it must run in a place that fires regardless of startup path (e.g. `OnHandleCreated`, or the existing `InitializeTrayState()`-style unconditional-priming call). This mirrors a bug class this project has hit twice already (Phase 8, Phase 11) — do not repeat it a third time.

### Claude's Discretion

- Exact shape of the theme-provider abstraction (`IThemeProvider`/`WindowsThemeProvider` in Core/Windows per architecture research, vs. a simpler static helper) — research recommends the Core/Windows split mirroring `IAutostartConfigurator`/`WindowsAutostartConfigurator`.
- Where the per-control recolor pass (`ThemeApplier` or similar) lives — research recommends `RigToggle.App` (Designer-generated-form-specific control knowledge, consistent with the rule that WinForms composition code stays in the App layer).
- `UserPreferenceCategory` filter used for the `SystemEvents.UserPreferenceChanged` subscription, and whether `SystemEvents` events need explicit UI-thread marshaling — flagged MEDIUM confidence in research, not independently re-verified.
- Exact `FlatStyle` value per control (`Flat` vs `System`) — research flagged an open `dotnet/winforms#13897` bug where `FlatStyle.Flat` buttons don't color correctly in dark mode; planner should use `FlatStyle.System` for buttons specifically to route around this, `Flat` elsewhere is fine.
- Whether `SettingsForm` is instantiated fresh per open or reused/hidden across opens — **resolved by this research, see Code Insights below: confirmed fresh-per-open.**

### Deferred Ideas (OUT OF SCOPE)

None raised during discussion. Manual theme override (THEME-09), accent-color-aware highlight (THEME-07), and a custom-drawn toggle-switch control (THEME-08) remain correctly deferred to the v2 backlog per REQUIREMENTS.md — not in-scope asks for this phase.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| THEME-01 | App detects the current Windows light/dark theme on startup and applies it to MainForm and SettingsForm | `Application.SetColorMode(SystemColorMode.System)` called before `new MainForm(...)` in `Program.cs`; verified the call's effect is captured at each control's/form's handle-creation time, and MainForm's handle is already forced into existence during `Program.cs`'s existing `RegisterHotkeyAtStartup()` call (which reads `.Handle`) — before `Application.Run` on both the visible and `--tray` paths. See "Startup ordering" in Architecture Patterns. |
| THEME-02 | Theme changes while running are picked up live, without app restart | `WindowsThemeProvider` (`IThemeProvider` impl) subscribes to `Microsoft.Win32.SystemEvents.UserPreferenceChanged`, diffs `AppsUseLightTheme` registry value, raises `ThemeChanged`; MainForm/SettingsForm/MonitorConfirmDialog re-invoke `SetColorMode` + DWM calls + `Refresh()` on that event. See Pattern 2/3, Pitfall 2. |
| THEME-03 | Title bar reflects active theme (dark in dark mode) | `Application.SetColorMode` manages `DWMWA_USE_IMMERSIVE_DARK_MODE` internally for the base/startup case — do NOT also hand-roll this same attribute (Pitfall 1: double-set causes a visible flash). Only the live-update re-application path needs to touch DWM directly for the title bar, and it should do so by re-invoking `SetColorMode`, not a separate manual `DwmSetWindowAttribute(20,...)` call. |
| THEME-04 | All controls, including the monitor grid, legibly recolored — no stock white/gray left over | `SetColorMode` covers `Label`/`TextBox`/`ComboBox`/`CheckBox` automatically (verify visually); `DataGridView` (`dgvMonitors`) needs explicit `DefaultCellStyle`/`ColumnHeadersDefaultCellStyle`/`GridColor`/`EnableHeadersVisualStyles=false` overrides (confirmed known gap, `dotnet/winforms#11893`); **`txtHotkey`'s hand-rolled `SystemColors.*` state-machine colors (found via direct source read, not in prior milestone research) must be replaced with theme-aware colors** — see Critical Codebase-Specific Finding below. |
| THEME-05 | Flat, modern button/panel styling instead of legacy 3D bevel/gradient | `Button.FlatStyle = FlatStyle.System` (not `.Flat`, routes around `dotnet/winforms#13897`); `GroupBox` (`grpMonitor`/`grpAudioDevices`/`grpAppPath`) has no flat variant at any color mode — must be replaced with `Panel` (flat 1px border) + `Label` caption, a real Designer-file layout change, not a property tweak. |
| THEME-06 | Windows 11: rounded corners + Mica backdrop; Windows 10/unavailable: graceful no-op | `DwmSetWindowAttribute` with `DWMWA_WINDOW_CORNER_PREFERENCE` (33, value `DWMWCP_ROUND`=2) and `DWMWA_SYSTEMBACKDROP_TYPE` (38, value `DWMSBT_MAINWINDOW`=2 for standard Mica) — both Windows-11-only attributes, confirmed via official `DWMWINDOWATTRIBUTE`/`DWM_SYSTEMBACKDROP_TYPE` enum docs. Note: standard WinForms top-level windows are **already** corner-rounded automatically on Windows 11 with zero code — this call is defensive/explicit insurance, not filling an actual visual gap for this app's plain `FixedDialog` forms. Declare the P/Invoke to return `int` (HRESULT) rather than relying on exceptions, so an unsupported-OS failure is a silently-ignored non-zero return, matching this codebase's non-throwing convention. |

</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Theme detection (registry read, current value) | API/Backend-equivalent (`RigToggle.Windows`, OS adapter layer) | Core (`IThemeProvider` contract) | Same shape as every existing OS-facing adapter (`WindowsAutostartConfigurator`, `WindowsMonitorController`) — a Windows-specific implementation behind a dependency-free Core interface. |
| Live theme-change notification | `RigToggle.Windows` (`SystemEvents` subscription) | — | OS-level event plumbing; no cross-tier concern beyond the `ThemeChanged` event surface. |
| Title-bar dark mode (base/startup) | Browser/Client-equivalent (`RigToggle.App`, WinForms composition) | — | Delegated to `Application.SetColorMode`'s internal DWM call — App-layer composition root responsibility (call site in `Program.cs`), not a new abstraction. |
| Title-bar dark mode (live re-apply) | `RigToggle.App` (form-level event handlers) | `RigToggle.Windows` (re-invocation of the same `SetColorMode`/DWM mechanism) | Each open form reacts to `IThemeProvider.ThemeChanged`; the mechanism itself stays in Windows/BCL. |
| Mica backdrop / rounded corners | `RigToggle.Windows` (new `DwmTitleBar`-style façade over `NativeMethods`) | `RigToggle.App` (calls it once per form, on handle-created + on theme-change) | Win32/DWM P/Invoke is exactly what `RigToggle.Windows` already owns (mirrors `GlobalHotkey`'s public-façade-over-internal-P/Invoke pattern). |
| Per-control recolor overrides (`DataGridView`, `GroupBox`→`Panel`, `txtHotkey` state colors) | `RigToggle.App` (Designer-generated-form-specific knowledge) | — | Not a generic capability — encodes exactly which controls this app's own Designer files contain; stays in App per the existing "WinForms composition lives in App only" rule. |
| Settings persistence for theme | N/A — no new setting | — | THEME-09 (manual override) is explicitly deferred; this phase adds zero new `AppSettings` fields. |

## Project Constraints (from CLAUDE.md)

- **Windows-only, no cross-platform requirement** — no constraint conflict; all APIs used here (`Application.SetColorMode`, DWM P/Invoke, `SystemEvents`) are Windows/WinForms-specific already.
- **Standalone self-contained single-file `.exe`, untrimmed (`PublishTrimmed=false` implied by "Accept the larger exe")** — no new packages are introduced by this phase (see Package Legitimacy Audit), so this constraint is unaffected. Do not introduce `PublishTrimmed=true` as part of this phase's work; it remains explicitly forbidden project-wide (IL trimming risk for P/Invoke/COM paths).
- **Hand-rolled P/Invoke preferred over third-party wrapper packages** — directly applicable: the new DWM P/Invoke (`DwmSetWindowAttribute`) must be hand-written in `RigToggle.Windows/NativeMethods.cs`, following the exact existing pattern (plain `DllImport`, no `PInvoke.User32`/similar package).
- **No elevation manifest of any kind** — the theme/registry/DWM APIs used here (`HKCU` registry read, `SystemEvents`, `DwmSetWindowAttribute`) all work at standard (asInvoker) integrity level; no elevation is required or should be added.
- **`RigToggle.Core` has zero Windows API references (structural, enforced via `.csproj` comment)** — `IThemeProvider`/`AppTheme` must be pure C# (interface + enum), no `Microsoft.Win32`, no P/Invoke, no `UseWindowsForms`.
- **GUI framework fixed as WinForms** (`UseWindowsForms=true`, `net10.0-windows`) — already the case for `RigToggle.App`/`RigToggle.Windows`; this phase adds no new TFM or GUI-framework dependency.

## Standard Stack

### Core

| API / Mechanism | Source | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Application.SetColorMode(SystemColorMode)` | `System.Windows.Forms` (BCL, ships with `net10.0-windows` + `UseWindowsForms=true`) — **`[VERIFIED: learn.microsoft.com]`** | Base-layer light/dark theming for controls + title bar | Non-experimental (`WFO5001` gate removed) as of .NET 10 GA, per the official method signature page (fetched directly — the page's `windowsdesktop-9.0` code sample still shows `[Experimental("WFO5001")]`, the `windowsdesktop-10.0`/`11.0` sample does not). `SystemColorMode` enum: `Classic = 0`, `System = 1`, `Dark = 2` — namespace `System.Windows.Forms`. |
| `Microsoft.Win32.SystemEvents.UserPreferenceChanged` | BCL, part of the Windows Desktop shared framework already referenced (`UseWindowsForms=true`) — **`[VERIFIED: RigToggle.Windows.csproj already has UseWindowsForms=true]`** | Live OS theme-change detection | Zero new package references. Fires for many unrelated preference categories — filter/dedupe by re-reading the registry value and comparing to last-known (do not rely solely on `UserPreferenceCategory.General`, which is `[ASSUMED]` — see Assumptions Log). |
| `Microsoft.Win32.Registry.CurrentUser` → `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` | BCL — **`[CITED: multiple independent sources, widely corroborated, functionally stable for years]`** | Current app-theme value (`0`=dark, `1`=light) | Already used elsewhere in this project (autostart uses `HKCU\...\Run`) — same access pattern, same `Registry.CurrentUser.OpenSubKey` idiom. |
| Hand-rolled `DwmSetWindowAttribute` P/Invoke (`dwmapi.dll`) | N/A — ~15-line `DllImport`, no package — **`[VERIFIED: learn.microsoft.com DWMWINDOWATTRIBUTE/DWM_WINDOW_CORNER_PREFERENCE/DWM_SYSTEMBACKDROP_TYPE enum pages, fetched directly]`** | Mica backdrop + rounded corners (THEME-06) | Not covered by `Application.SetColorMode` at all — a separate DWM attribute family. Constants confirmed directly against the official `DWMWINDOWATTRIBUTE` enum page: `DWMWA_USE_IMMERSIVE_DARK_MODE = 20`, `DWMWA_WINDOW_CORNER_PREFERENCE = 33`, `DWMWA_SYSTEMBACKDROP_TYPE = 38`. |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| None | — | — | This phase requires **zero new NuGet packages**. `WindowsDisplayAPI`/`NAudio` (already referenced) are untouched; theming uses only BCL + WinForms built-ins. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled `SystemEvents`-based live-following | [DarkNet](https://github.com/Aldaviva/DarkNet) (NuGet, MIT) | Purpose-built for exactly this gap; worth reaching for only if the hand-rolled approach proves flakier than expected on the rig. Not recommended as a starting point — matches this project's established "no heavy third-party dependency" bias. |
| Full hand-rolled per-control recolor pass (`ARCHITECTURE.md`'s original framing) | `Application.SetColorMode` as base + targeted overrides | **Superseded by CONTEXT.md D-04/D-05** — the full hand-roll approach is not the locked design for this phase. Do not implement it as originally sketched in `ARCHITECTURE.md`'s Pattern 4/Anti-Pattern 1. |

**Installation:** None — no `npm install`/`dotnet add package` needed. All APIs are already available in the existing `net10.0-windows` + `UseWindowsForms=true` projects.

**Version verification:** `Application.SetColorMode` — confirmed non-experimental directly against `learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setcolormode?view=windowsdesktop-10.0` (fetched 2026-08-02, `ms.date: 2025-07-01`, `updated_at: 2026-07-01`). `DWMWINDOWATTRIBUTE`/`DWM_WINDOW_CORNER_PREFERENCE`/`DWM_SYSTEMBACKDROP_TYPE` — confirmed directly against their respective `learn.microsoft.com/en-us/windows/win32/api/dwmapi/...` pages (fetched 2026-08-02).

## Package Legitimacy Audit

**No new external packages are introduced by this phase.** Every API used (`Application.SetColorMode`, `SystemEvents`, `Registry`, hand-rolled `DwmSetWindowAttribute` P/Invoke) is either BCL or already part of the `UseWindowsForms=true` shared framework already referenced by `RigToggle.Windows`/`RigToggle.App`. The Package Legitimacy Gate protocol is therefore skipped — there is nothing to run `slopcheck`/registry verification against. If a future plan revision decides to take a dependency on [DarkNet] (see Alternatives Considered) instead of hand-rolling, that decision must re-trigger this audit before the plan proceeds.

## Architecture Patterns

### System Architecture Diagram

```
Windows OS theme setting (Settings > Personalization > Colors)
        │
        │ read once at startup                  │ broadcasts WM_SETTINGCHANGE on live change
        ▼                                        ▼
┌─────────────────────────┐         Microsoft.Win32.SystemEvents.UserPreferenceChanged
│ Program.cs Main()        │                     │ (fires on SystemEvents' own thread)
│                          │                     ▼
│ 1. Application.SetColorMode(SystemColorMode.System)   ◄── must run before any Form/control
│    (before ANY UI element is constructed)                  is constructed — first line of Main()
│                          │
│ 2. new WindowsThemeProvider()  ── reads AppsUseLightTheme once, subscribes to SystemEvents
│                          │
│ 3. new MainForm(..., themeProvider)  ── subscribes to ThemeChanged during construction
│                          │
│ 4. mainForm.InitializeTrayState()  ── existing Pitfall-6 entry point; also where the
│                          │            DWM Mica/corner calls run (handle already forced by
│                          │            the existing RegisterHotkeyAtStartup() .Handle read)
│                          │
│ 5. Application.Run(...)  ── SettingsForm/MonitorConfirmDialog constructed later, on demand
└──────────────┬───────────┘
               │
               ▼
      ┌────────────────────────────────────────────────────────┐
      │ Live theme-change flow (app already running)             │
      │                                                            │
      │ WindowsThemeProvider.OnUserPreferenceChanged              │
      │   re-reads AppsUseLightTheme, compares to CurrentTheme    │
      │   → raises ThemeChanged only if it actually changed       │
      │                                                            │
      │ ├──► MainForm.OnThemeChanged (marshal to UI thread)       │
      │ │      SetColorMode(System) again + DWM Mica/corner        │
      │ │      re-apply + DataGridView/GroupBox-replacement        │
      │ │      overrides re-applied + Refresh()                    │
      │ │                                                          │
      │ └──► SettingsForm/MonitorConfirmDialog.OnThemeChanged      │
      │        (same, only while open — subscribed on construct,  │
      │         unsubscribed on FormClosed)                        │
      └────────────────────────────────────────────────────────┘
```

### Recommended Project Structure

```
src/
├── RigToggle.Core/
│   ├── Abstractions/IThemeProvider.cs      # NEW — AppTheme CurrentTheme { get; }; event EventHandler? ThemeChanged;
│   └── Models/AppTheme.cs                  # NEW — public enum AppTheme { Light, Dark }
│
├── RigToggle.Windows/
│   ├── WindowsThemeProvider.cs             # NEW — IThemeProvider impl: registry read + SystemEvents subscription
│   ├── DwmTitleBar.cs                      # NEW — public static façade over Mica/corner-preference P/Invoke
│   ├── NativeMethods.cs                    # MODIFIED — + DwmSetWindowAttribute DllImport + 3 DWMWA_* constants
│   └── ... (existing adapters, untouched)
│
├── RigToggle.App/
│   ├── ThemeApplier.cs                     # NEW — TARGETED overrides only: DataGridView cell styling,
│   │                                       #        GroupBox→Panel+Label replacement, txtHotkey state colors.
│   │                                       #        NOT a full recursive Controls-tree walk (superseded design).
│   ├── Program.cs                          # MODIFIED — SetColorMode call (first line of Main), construct
│   │                                       #             WindowsThemeProvider, inject into forms
│   ├── MainForm.cs / .Designer.cs          # MODIFIED — IThemeProvider ctor param, subscribe/unsubscribe,
│   │                                       #             Button.FlatStyle = System
│   ├── SettingsForm.cs / .Designer.cs      # MODIFIED — same + GroupBox→Panel replacement (3x) +
│   │                                       #             DataGridView styling + txtHotkey color fix
│   └── MonitorConfirmDialog.cs             # DISCRETION — see Open Questions (in/out of literal requirement scope)
│
└── RigToggle.Tests/
    └── (new unit tests against a fake IThemeProvider for anything Core-level; WindowsThemeProvider
        itself is not unit-testable — rig-only, same as every other Windows* adapter)
```

### Pattern 1: Core-interface + Windows-adapter (existing pattern, reused verbatim)

**What:** `IThemeProvider` (Core, zero Windows references) + `WindowsThemeProvider` (Windows, registry + `SystemEvents`), wired together in `Program.cs`. Identical shape to `IAutostartConfigurator`/`WindowsAutostartConfigurator`.

```csharp
// RigToggle.Core/Abstractions/IThemeProvider.cs
namespace RigToggle.Core.Abstractions;

public interface IThemeProvider
{
    AppTheme CurrentTheme { get; }
    event EventHandler? ThemeChanged;
}

// RigToggle.Core/Models/AppTheme.cs
namespace RigToggle.Core.Models;

public enum AppTheme { Light, Dark }
```

### Pattern 2: Registry read + `SystemEvents.UserPreferenceChanged` for live updates

```csharp
// RigToggle.Windows/WindowsThemeProvider.cs
public sealed class WindowsThemeProvider : IThemeProvider, IDisposable
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValueName = "AppsUseLightTheme";

    public AppTheme CurrentTheme { get; private set; }
    public event EventHandler? ThemeChanged;

    public WindowsThemeProvider()
    {
        CurrentTheme = ReadThemeFromRegistry();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        // [ASSUMED] filtering on e.Category == UserPreferenceCategory.General is the widely-used
        // community pattern but not confirmed against one official Learn page — re-reading and
        // diffing on every category (not just General) is the safer fallback if General proves
        // too narrow. Verify on rig; adjust filter if theme changes are missed or over-fire.
        var resolved = ReadThemeFromRegistry();
        if (resolved != CurrentTheme)
        {
            CurrentTheme = resolved;
            ThemeChanged?.Invoke(this, EventArgs.Empty); // may fire off the UI thread — callers must marshal
        }
    }

    private static AppTheme ReadThemeFromRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(KeyPath);
            var raw = key?.GetValue(ValueName);
            return raw is int i && i == 0 ? AppTheme.Dark : AppTheme.Light; // best-effort default: Light
        }
        catch
        {
            return AppTheme.Light; // matches this codebase's existing "never throw from a Load-time read" convention
        }
    }

    public void Dispose() => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}
```

**Trade-offs / confidence notes:**
- `SystemEvents` raises events on the thread that first touched the static `SystemEvents` class — for a WinForms app this is *usually*, but not guaranteed to be, the UI thread. Every handler in `MainForm`/`SettingsForm`/`MonitorConfirmDialog` **must marshal onto the form's own UI thread** (`InvokeRequired`/`BeginInvoke`) before touching any control. `[ASSUMED — MEDIUM confidence, corroborated across multiple community sources, not one official Learn page]`.

### Pattern 3: `Application.SetColorMode` as the base layer — startup ordering (VERIFIED against real `Program.cs`)

**What:** Call `Application.SetColorMode(SystemColorMode.System)` as literally the first executable statement in `Main()`, before `ApplicationConfiguration.Initialize()` and certainly before `new MainForm(...)` (line 116 of the current `Program.cs`).

**Why this satisfies THEME-01 even under `--tray` (verified, not assumed):** `Program.cs`'s existing `--tray` path already forces `MainForm`'s window handle into existence *before* `Application.Run`, regardless of which branch runs — `mainForm.RegisterHotkeyAtStartup()` (called unconditionally at line 129, before the `if (StartupArgs.ShouldStartHidden(...))` branch at line 144) calls `TryRegisterConfiguredHotkey()`, which reads `Handle` (`GlobalHotkey.Unregister(Handle, GlobalHotkeyId)`). Since `.Handle` is a lazy-create property, this read forces `CreateHandle` right there, before either `Application.Run` branch. Because `Application.SetColorMode` was already called earlier in `Main()`, WinForms' internal theming logic (which resolves and applies the color mode around each control's/form's handle-creation time) has already captured the correct mode by the time this forced handle-creation happens — **no `OnHandleCreated` override is required for the base `SetColorMode` layer to satisfy THEME-01 on the `--tray` path.** `OnHandleCreated`/an explicit priming call *is* still needed for the separate DWM Mica/corner-preference calls (Pattern 4 below), since those are not part of `SetColorMode` at all.

```csharp
// Program.cs — first line of Main(), before ApplicationConfiguration.Initialize()
[STAThread]
static void Main(string[] args)
{
    System.Windows.Forms.Application.SetColorMode(System.Windows.Forms.SystemColorMode.System);

    ApplicationConfiguration.Initialize();
    // ... existing settings/store/adapter construction, unchanged ...
}
```

### Pattern 4: DWM Mica + rounded-corner façade, best-effort, Windows-11-only (VERIFIED against official example)

```csharp
// RigToggle.Windows/NativeMethods.cs — additions
internal static class NativeMethods
{
    // ... existing members unchanged ...

    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;   // Win11 Build 22000+ (WinForms' own SetColorMode owns this one)
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;  // Win11 Build 22000+
    internal const int DWMWA_SYSTEMBACKDROP_TYPE = 38;       // Win11 Build 22621+ (22H2)

    // Declared to return int (HRESULT) rather than void+PreserveSig=false, so an
    // unsupported-OS/attribute failure is a silently-ignorable non-zero return —
    // matches this codebase's non-throwing P/Invoke convention (see ShowWindow/
    // RegisterHotKey above), no try/catch required at the call site.
    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}

// RigToggle.Windows/DwmTitleBar.cs — mirrors GlobalHotkey.cs's public-façade-over-internal-P/Invoke shape
public static class DwmTitleBar
{
    private const int DWMWCP_ROUND = 2;        // DWM_WINDOW_CORNER_PREFERENCE
    private const int DWMSBT_MAINWINDOW = 2;   // DWM_SYSTEMBACKDROP_TYPE — standard Mica (D-02: NOT Mica Alt/Acrylic)

    /// <summary>
    /// Best-effort. Windows 11 only (Build 22000+ for corners, 22621+/22H2 for Mica) — a failed
    /// call on Windows 10 or an unsupported build is a silently-ignored non-zero HRESULT, never
    /// an exception, matching every other OS-facing call in this codebase (D-07).
    /// </summary>
    public static void ApplyRoundedCornersAndMica(IntPtr handle)
    {
        int corner = DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        int backdrop = DWMSBT_MAINWINDOW;
        NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
    }
}
```

**Call sites:** `MainForm` — inside `InitializeTrayState()` (already the unconditional, both-startup-paths priming point; `Handle` is already forced by the time this runs, per Pattern 3 above). `SettingsForm`/`MonitorConfirmDialog` — in the constructor after `InitializeComponent()`, or in `Load` (both are always `ShowDialog()`-displayed immediately after construction — no hidden-tray complication applies to these two forms, so `OnHandleCreated` is not strictly required here, though using it is equally valid and slightly more consistent).

**Note on corner rounding actually mattering:** confirmed directly against Microsoft's "Apply rounded corners in desktop apps for Windows 11" guide — *"All standard WinForms and WPF apps are rounded automatically"* on Windows 11. `MainForm`/`SettingsForm` are plain `FormBorderStyle.FixedDialog` forms with standard frame styles, so they very likely already render with rounded corners on Windows 11 with **zero code**. The explicit `DWMWA_WINDOW_CORNER_PREFERENCE` call is defensive insurance (per D-02), not filling an actual visible gap — do not be surprised if a before/after screenshot shows no visible difference for the corner-rounding half of THEME-06; the Mica backdrop half is the part that visibly changes the window.

### Pattern 5: Targeted control overrides — concrete THEME-04/05 breakdown (verified against actual `.Designer.cs` files)

The following table is a full inventory of `MainForm.Designer.cs` + `SettingsForm.Designer.cs` + `MonitorConfirmDialog.Designer.cs` controls, verified by direct source read (not the milestone research's earlier estimate — that estimate was off by a few controls, corrected here), with an explicit disposition per control:

| Control(s) | Count | Expected to theme automatically via `SetColorMode`? | Action needed |
|---|---|---|---|
| `Label` (`lblMode`, `lblMonitorWarning`, `lblMonitorExplain`, `lblAudioNormalCaption`, `lblAudioRigCaption`, `lblAudioNormalWarning`, `lblAudioRigWarning`, `lblAppWarning`, `lblHotkeyCaption`, `lblHotkeyWarning`, `lblAutostartWarning`, `lblMessage`) | 12 | Yes | None — verify visually only. |
| `TextBox` (`txtAppPath`, readonly) | 1 | Yes | None — verify visually only. |
| `ComboBox` (`cboAudioNormal`, `cboAudioRig`) | 2 | Yes (edit portion); dropdown-button glyph area "can remain visually inconsistent" per STACK.md | Verify visually in both themes; no code change expected unless verification finds a gap. |
| `CheckBox` (`chkEnableDebugLogging`, `chkCloseMinimizesToTray`, `chkMinimizeToTray`, `chkStartWithWindows`, `chkDontAskAgain`) | 5 | Yes | None — verify visually only. |
| `Button` (`btnToggle`, `btnSettings`, `btnBrowse`, `btnSaveSettings`, `btnDiscardChanges`, `btnContinue`, `btnCancel`) | 7 | Base color: yes. Flat modern look (THEME-05): needs `FlatStyle` set. | Set `FlatStyle = FlatStyle.System` (NOT `.Flat` — `dotnet/winforms#13897`) on every button. |
| `GroupBox` (`grpMonitor`, `grpAudioDevices`, `grpAppPath`) | 3 | Background/text: yes. Bevel border: **no flat variant exists at any color mode.** | **Real Designer-file refactor for THEME-05:** replace each with a `Panel` (1px flat themed border, drawn or `BorderStyle.FixedSingle` with an explicit color) + a `Label` acting as the caption. All child controls currently parented to each `GroupBox` must be re-parented to the new `Panel`. This is the single largest layout-affecting task in this phase — budget real Designer-file time, not a one-line property change. |
| `DataGridView` (`dgvMonitors`) — 3 columns: `colMonitorName` (text), `colDisable`/`colEnable` (checkbox) | 1 grid, 3 columns | **No — confirmed known gap.** `dotnet/winforms#11893`: mixed-column-type `DataGridView` (exactly this shape — text + checkbox columns) has confirmed incorrect-color issues under dark mode. | Explicit overrides required: `BackgroundColor`, `EnableHeadersVisualStyles = false` (must be false before `ColumnHeadersDefaultCellStyle` takes effect at all — well-known WinForms gotcha), `DefaultCellStyle.BackColor`/`ForeColor`/`SelectionBackColor`/`SelectionForeColor`, `ColumnHeadersDefaultCellStyle.BackColor`/`ForeColor`, `GridColor`. Verify checkbox-cell rendering specifically (the confirmed-buggy column type) on the rig in both themes. |
| `TextBox` (`txtHotkey`) — **CRITICAL, codebase-specific finding, not flagged by prior milestone research** | 1 | **No — actively hostile to theming.** | See dedicated finding below. |
| `ErrorProvider` (`errMonitor`, `errAudioNormal`, `errAudioRig`, `errApp`, `errAutostart`, `errHotkey`) | 6 | N/A — not a `Control`, unaffected by any `Controls`-tree-based approach | None. Small red warning-icon glyph reads fine on both light and dark backgrounds — leave untouched. |
| `ToolStripMenuItem`/`ToolStripSeparator` (tray context menu) | 4 items | Themed at startup; live-flip has the confirmed stale-cache bug | None (D-03: accepted known limitation, comment only). |
| `MessageBox.Show(...)` call sites (`MainForm.cs`) | **4** (confirmed exact count via direct source read: WR-01 config-incomplete Info dialog, CORE-04 partial-failure Warning, `ToggleInProgressException` Info dialog, generic-`Exception` Warning) | No — native Win32 dialog | None (D-01: deliberately left native/unthemed). |
| `OpenFileDialog` (`dlgOpenExe`) | 1 | Yes — native common dialog already follows OS theme on Windows 11 automatically | None. |

#### Critical codebase-specific finding: `txtHotkey`'s hand-rolled `SystemColors.*` state machine actively fights theming

**`[VERIFIED: direct source read, SettingsForm.cs]`** — not identified by any prior milestone research artifact (STACK.md/ARCHITECTURE.md/PITFALLS.md all predate this direct source inspection). `SettingsForm.cs`'s hotkey-capture UI (TRIG-01/D-01) hardcodes **`System.Drawing.SystemColors`** — the classic Win32 control-panel color table, which is a *completely separate* color system from WinForms' internal dark-mode palette and does **not** follow `Application.SetColorMode` — in three places:

1. `RenderHotkeyIdleDisplay()` (lines 130-144): sets `txtHotkey.BackColor = SystemColors.Window` / `.ForeColor = SystemColors.WindowText` (Configured state) or `SystemColors.GrayText` (Unconfigured state).
2. `TxtHotkey_MouseDown` (lines 150-156): on entering Recording state, sets `txtHotkey.BackColor = SystemColors.Info` (the classic yellow-ish "ToolTip" system color) / `.ForeColor = SystemColors.WindowText`.
3. `TxtHotkey_KeyDown` (lines 228-234): on a successful capture, sets `txtHotkey.BackColor = SystemColors.Window` / `.ForeColor = SystemColors.WindowText` again.

Because these are explicit, imperative color assignments fired on every state transition (not passive ambient inheritance), **even if `SetColorMode` correctly themes `txtHotkey` at startup, the very first user interaction (a mouse click to start Recording) forcibly resets it back to light-mode `SystemColors.Info`/`SystemColors.Window`**, breaking the dark theme on this one control every time it's touched. This is a concrete, load-bearing THEME-04 gap that must be fixed by replacing these three `SystemColors.*` references with theme-aware `Color` values (sourced from `IThemeProvider.CurrentTheme` or a small shared palette constant set), not left to `SetColorMode` to handle — it structurally cannot, since the app's own code overwrites the color on every state change.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Detecting light/dark and repainting standard controls at launch | A full recursive `Controls`-tree walk that manually sets `BackColor`/`ForeColor` on every `Label`/`TextBox`/`ComboBox`/`CheckBox`/`Button` | `Application.SetColorMode(SystemColorMode.System)` | This is now a first-class, non-experimental .NET 10 API that does exactly this — hand-rolling it duplicates framework behavior and risks the double-DWM-attribute-set flash (Pitfall 1) if a manual title-bar call is also added. |
| Dark-mode title bar | Manual `DwmSetWindowAttribute(hwnd, 20, ...)` for the base/startup case | `Application.SetColorMode`'s internal handling (base layer); only re-invoke `SetColorMode` itself (not a separate raw DWM call) for the live-update re-apply | Calling the same attribute from two independent code paths is a confirmed community-reported flicker bug. |
| Determining if dark mode is active | Custom heuristics / theme-detection libraries | `HKCU\...\Personalize\AppsUseLightTheme` (already the registry key `Application.SetColorMode` effectively tracks internally) | Single, stable, widely-corroborated source of truth; matches what `SetColorMode` itself resolves against. |
| Common-dialog theming (`MessageBox`, `OpenFileDialog`) | Custom-drawn replacement dialogs | Leave native — Windows 11 themes these automatically at the shell level | Explicitly accepted per D-01; a themed-`MessageBox` replacement is a scope increase this milestone rejected. |

**Key insight:** The temptation in this domain is to over-build — either a full hand-rolled recolor pass (superseded by `SetColorMode`) or a themed-`MessageBox` replacement (explicitly rejected by CONTEXT.md D-01). The actual required custom work is narrow: live-update plumbing, two confirmed-broken controls (`DataGridView`, `GroupBox`), one actively-hostile codebase-specific control (`txtHotkey`), and two Windows-11-only DWM calls (Mica, corner preference) that `SetColorMode` never touches at all.

## Common Pitfalls

### Pitfall 1: Manual DWM title-bar call fights `Application.SetColorMode`'s own internal call
**What goes wrong:** Setting `DWMWA_USE_IMMERSIVE_DARK_MODE` from both WinForms' internal `SetColorMode` handling and a separate hand-rolled call causes the title bar to visibly flash/animate between colors on every form show.
**How to avoid:** Never add a manual `DwmSetWindowAttribute(hwnd, 20, ...)` call anywhere in this codebase for THEME-03's base case. The live-update re-apply path should re-invoke `Application.SetColorMode(SystemColorMode.System)` itself, not a raw DWM call for that specific attribute. (The Mica/corner-preference DWM calls, attributes 33/38, are unrelated attributes and do not conflict.)
**Phase to address:** Theme-infrastructure — architecture decision, not a review-time catch.

### Pitfall 2: `SetColorMode` alone does not live-follow
**What goes wrong:** `Application.SetColorMode(SystemColorMode.System)` is applied once, at the call site. Confirmed via the official API remarks *and* independently via `dotnet/winforms#13935` (open, unresolved) that it does not react to a live OS theme change.
**How to avoid:** `WindowsThemeProvider`'s `SystemEvents.UserPreferenceChanged` subscription (Pattern 2) is not optional — it is the entire mechanism THEME-02 depends on.
**Warning signs:** Testing only ever restarts the app to "test dark mode" instead of flipping Windows Settings > Personalization > Colors while the app is already running.
**Phase to address:** Theme-infrastructure for the mechanism; verification must include an explicit live-flip rig test, both visible and `--tray`-hidden.

### Pitfall 3: Wrong registry key for icon-theme selection
**Not this phase's concern** (Phase 13 owns tray icon theming, keyed off `SystemUsesLightTheme`) — but `WindowsThemeProvider` must read `AppsUseLightTheme` specifically, not `SystemUsesLightTheme`, per D-06. Confirmed both keys live at the same `HKCU\...\Personalize` path but govern independently-settable surfaces.

### Pitfall 4: Static system-brush/pen caches don't repaint tray context menu on live flip
**What goes wrong:** `dotnet/winforms#12027` (confirmed, open) — `ToolStripSeparator` lines and dropdown arrows keep the pre-flip color after a live theme change, because `SystemBrushes`/`SystemPens` cache by `KnownColor` and aren't purged on a runtime switch.
**How to avoid:** Accept as documented known limitation per D-03. Do not build a rebuild-the-`ContextMenuStrip`-on-theme-change workaround.
**Phase to address:** Note in code comments during theme-infrastructure; verify (and accept) during polish/verification.

### Pitfall 5: DWM calls issued before the window handle exists silently no-op on `--tray`
**What goes wrong:** Calling any DWM attribute function before a form's `HWND` exists is a documented no-op failure mode. This project has hit exactly this class of startup-path-divergence bug twice already (Phase 8's `--tray` `Show()` suppression, Phase 11's lockout bug).
**How to avoid:** As established in Pattern 3/4 above — the base `SetColorMode` layer is unaffected (verified: `Program.cs` already forces `MainForm.Handle` into existence before either `Application.Run` branch via the existing `RegisterHotkeyAtStartup()` call). The Mica/corner-preference calls must run from `InitializeTrayState()` (or later), never from a constructor.
**Phase to address:** Theme-infrastructure; must be verified specifically on the `--tray` hidden-start-then-restore sequence, not just normal launch.

### Pitfall 6: `Button.FlatStyle = FlatStyle.Flat` breaks under dark mode
**What goes wrong:** `dotnet/winforms#13897` (open) — `FlatAppearance` border/hover/pressed colors don't reliably apply once dark mode is active when `FlatStyle.Flat` is used, producing visually broken buttons.
**How to avoid:** Use `FlatStyle.System` for every `Button` in this phase's scope (per CONTEXT.md's discretion note, already resolved).
**Phase to address:** Theme-infrastructure, THEME-05 work.

### Pitfall 7: `MessageBox` and other native dialogs stay light — don't try to fix this
**What goes wrong:** `MessageBox` is a native Win32 dialog outside WinForms' rendering control; `SetColorMode` cannot and does not reach it. Attempting to CBT-hook or theme its window handle is disproportionate effort explicitly rejected by D-01.
**How to avoid:** Leave `MessageBox.Show` and `OpenFileDialog` exactly as-is; do not add any code touching them in this phase.
**Phase to address:** N/A — already resolved by CONTEXT.md D-01; listed here only so a future reviewer doesn't "fix" it into unplanned scope.

### Pitfall 8: `SystemColors.*` hardcoded in application code silently defeats dark mode on that one control
**What goes wrong:** Any explicit `Control.BackColor = SystemColors.X` assignment in event-handler code (not just Designer-generated defaults) overwrites whatever `SetColorMode` applied, every time that code path runs — this is exactly `txtHotkey`'s situation (see Pattern 5's dedicated finding above), and is easy to miss because it's imperative code in `.cs` files, not a static Designer property that a quick visual Designer-preview pass would catch.
**How to avoid:** Grep the App-layer `.cs` files (not just `.Designer.cs`) for `SystemColors.` during the theme-infrastructure phase — do not assume all hardcoded colors live in Designer-generated files.
**Warning signs:** A control looks correctly dark-themed at Settings-open time but reverts to a light color the moment the user interacts with it.
**Phase to address:** Theme-infrastructure — this is a required audit step, not optional polish.

## Code Examples

### `NativeMethods.cs` additions (verified constants)

```csharp
// Source: learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute (fetched directly)
internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;   // owned by Application.SetColorMode — do not call manually
internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
internal const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

[DllImport("dwmapi.dll")]
internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
```

### `DataGridView` theming (targeted override, not a generic pass)

```csharp
// Source: pattern corroborated against dotnet/winforms#11893's description of the exact
// mixed-column-type (text + checkbox) failure mode this app's dgvMonitors grid has.
private static void ThemeMonitorGrid(DataGridView grid, bool dark)
{
    grid.EnableHeadersVisualStyles = false; // MUST be false before ColumnHeadersDefaultCellStyle applies at all
    grid.BackgroundColor = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Window;
    grid.GridColor = dark ? Color.FromArgb(70, 70, 70) : SystemColors.ControlLight;
    grid.DefaultCellStyle.BackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Window;
    grid.DefaultCellStyle.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
    grid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;
    grid.DefaultCellStyle.SelectionForeColor = dark ? Color.White : SystemColors.HighlightText;
    grid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
    grid.ColumnHeadersDefaultCellStyle.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
}
```

### `txtHotkey` fix — replace hardcoded `SystemColors` with theme-aware values

```csharp
// BEFORE (SettingsForm.cs, three call sites — RenderHotkeyIdleDisplay, TxtHotkey_MouseDown,
// TxtHotkey_KeyDown) hardcode SystemColors.Window/WindowText/GrayText/Info, which do NOT
// follow Application.SetColorMode and silently break the theme on every user interaction.
//
// AFTER — source colors from the current theme (e.g. via _themeProvider.CurrentTheme or a
// small shared palette helper), not System.Drawing.SystemColors, in all three locations.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| "WinForms has no built-in dark mode — hand-roll every control's colors + manual DWM calls" (this milestone's own original `PROJECT.md` framing) | `Application.SetColorMode(SystemColorMode.System)` as base layer, non-experimental | .NET 9 (experimental, `WFO5001`) → .NET 10 GA (Nov 2025) | This project already targets .NET 10 — the outdated framing should not drive planning; CONTEXT.md D-04/D-05 already corrects it. |
| `FlatStyle.Flat` for a "modern" button look | `FlatStyle.System` (routes around a live dark-mode color bug) | Confirmed still-open as of this research (`dotnet/winforms#13897`) | Directly affects THEME-05 button styling choice. |

**Deprecated/outdated:** The idea that this phase needs a full recursive `Controls`-tree recolor pass — superseded by `Application.SetColorMode`. Only `DataGridView`, `GroupBox`, and `txtHotkey`'s hardcoded `SystemColors` need targeted, hand-written overrides.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Filtering `SystemEvents.UserPreferenceChanged` on `UserPreferenceCategory.General` is sufficient to catch theme changes without excessive noise from unrelated settings | Pattern 2 | If wrong (too narrow), live theme-following could silently miss changes — mitigated by the safer fallback of re-reading/diffing on every category, already noted inline. If wrong (too broad/never fires on General), same mitigation applies. Low risk either way since the fallback is already documented. |
| A2 | `Application.SetColorMode`'s effect on a control is resolved at that control's own handle-creation time (not cached/fixed at the moment `SetColorMode` was first called) | THEME-01 requirement support, Pattern 3 | If wrong, transient dialogs (`SettingsForm`, `MonitorConfirmDialog`) constructed after a live theme flip would NOT automatically pick up the new theme even without any live-update code touching them — meaning the live-update patch (Pattern 2/D-05) would need to explicitly re-theme these transient dialogs too, not just `MainForm`. See Open Questions — this changes scope, not correctness, since the plan already covers subscribing all three forms defensively. |
| A3 | `SystemEvents` events for this app fire off the UI thread often enough that explicit marshaling is required, not merely theoretically possible | Pattern 2 | If wrong (events always land on UI thread for this app's specific startup shape), the marshaling code is defensive-but-harmless. If right and marshaling is skipped, risk is an intermittent `InvalidOperationException`/cross-thread-access crash — moderate risk, mitigate by always marshaling regardless of which way this resolves. |

**If this table is empty:** N/A — see entries above. All three are LOW-to-MODERATE risk with an already-documented safe fallback; none block planning.

## Open Questions

1. **Does a freshly-constructed `SettingsForm`/`MonitorConfirmDialog` (built after a live Windows theme flip, without restarting the app) automatically render in the new theme via `Application.SetColorMode(System)` alone, or does it need the same live-update patch as `MainForm`?**
   - What we know: `SettingsForm` is confirmed instantiated fresh per open (verified: `Program.cs`'s `SettingsFormFactory` local function calls `new SettingsForm(...)`, and `MainForm.OpenSettingsDialog()` uses `using var settingsForm = _settingsFormFactory(); settingsForm.ShowDialog(this);` — never reused/cached across opens). Same for `MonitorConfirmDialog` (`using var confirmDialog = new MonitorConfirmDialog(...)` in `MainForm.BtnToggle_Click`).
   - What's unclear: whether `SystemColorMode.System`'s resolution happens once (cached) or per-control (live-queried at creation time) — the official docs' wording ("will not automatically adapt") is ambiguous on this specific point.
   - Recommendation: Implement the subscribe-on-construct/unsubscribe-on-close pattern for all three forms regardless (per ARCHITECTURE.md's transient-dialog guidance, which remains valid) — this is correct either way. Treat the answer to this question as an optimization/verification note, not a blocking design decision: during rig verification, flip the Windows theme live, then open Settings fresh (without an app restart) and confirm it renders correctly — this tells you whether the subscription is strictly load-bearing for transient dialogs or a defensive no-op.

2. **Is `MonitorConfirmDialog` in scope for this phase?**
   - What we know: REQUIREMENTS.md's THEME-01 through THEME-06 text explicitly names only "MainForm and SettingsForm" for every criterion. `CONTEXT.md`'s own `<domain>` boundary section also only mentions MainForm/SettingsForm/the DataGridView — it does not mention `MonitorConfirmDialog`. `ARCHITECTURE.md` (research, not a locked decision) recommends including it "for visual consistency... shown on every first toggle."
   - What's unclear: whether extending scope to a third form is within this phase's literal requirement wording or a nice-to-have the planner should explicitly scope in or out.
   - Recommendation: Treat as planner discretion, but recommend including it — it is a small dialog (4 controls: `Label`, `CheckBox`, 2 `Button`s, all in the "themes automatically" bucket per Pattern 5's table) and the marginal cost of adding `IThemeProvider` injection to it is low, while leaving it unthemed would create a jarring light-mode popup in an otherwise dark-themed toggle flow (DISPLAY-07's dialog appears on literally every first toggle). If included, note the one real precedent break: this is the first time `MonitorConfirmDialog`'s constructor gains an injected dependency (currently "pure display data," no interfaces).

3. **What Windows version does the actual rig PC run?**
   - What we know: `Application.SetColorMode`'s dark mode and the `DWMWA_WINDOW_CORNER_PREFERENCE`/`DWMWA_SYSTEMBACKDROP_TYPE` attributes are all Windows-11-only by official documentation (Build 22000+ / 22621+ respectively). `STATE.md`'s Blockers/Concerns section already flags this as unconfirmed.
   - What's unclear: whether THEME-06's Windows-10 fallback path will ever be exercised for real vs. only defensively coded.
   - Recommendation: This is exactly the kind of "only catchable on real Windows" item this project has flagged before (Phase 8/9/11 precedent) — plan an explicit `checkpoint:human-verify` early in the phase to confirm the rig's Windows version before deciding how much defensive Windows-10-path code/testing effort to invest (per D-07, code must exist regardless, but its priority/depth of testing depends on this answer).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK / `net10.0-windows` TFM | All of this phase's work | Not verifiable from this sandbox (Linux, no `dotnet` CLI present) — already the project's pinned TFM per every `.csproj` read directly | `net10.0-windows`, `UseWindowsForms=true` (confirmed in `RigToggle.App.csproj`/`RigToggle.Windows.csproj`) | N/A — this is the existing, already-shipped target; no change needed. |
| Windows 11 (rig PC) | THEME-06 (Mica/rounded corners), full dark-mode control coverage | **Unconfirmed** — see Open Question 3 | — | D-07 already mandates a non-throwing fallback path; Windows 10 users get flat control theming (THEME-04/05, not Windows-11-gated) but no dark title bar/Mica/rounding. |
| `dwmapi.dll` (Windows built-in) | THEME-06 | Present on every Windows Vista+ system per official `DWMWINDOWATTRIBUTE` requirements table | — | N/A — universally present; only specific attribute values are version-gated, not the DLL itself. |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** Windows 11-specific DWM attributes (Mica, rounded corners, and — per official docs — dark mode itself) already have D-07's documented best-effort/no-op fallback for Windows 10.

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setcolormode?view=windowsdesktop-10.0 — fetched directly 2026-08-02; confirmed method signature, namespace (`System.Windows.Forms`), non-experimental status in .NET 10 vs. `WFO5001`-gated in .NET 9, Windows-11-only scope, High Contrast interaction, explicit "will not automatically adapt" live-update gap wording
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.systemcolormode?view=windowsdesktop-10.0 — fetched directly 2026-08-02; confirmed enum values `Classic=0`, `System=1`, `Dark=2`
- https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/net100 — fetched directly 2026-08-02; confirmed `SetColorMode`/`SystemColorMode` non-experimental in .NET 10, `ControlStyles.ApplyThemingImplicitly` opt-in mechanism, no mention of live-update support (silence corroborates the gap)
- https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute — fetched directly 2026-08-02; confirmed `DWMWA_USE_IMMERSIVE_DARK_MODE=20`, `DWMWA_WINDOW_CORNER_PREFERENCE=33`, `DWMWA_SYSTEMBACKDROP_TYPE=38`, and each attribute's exact Windows-build support floor
- https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_window_corner_preference — fetched directly 2026-08-02; confirmed `DWMWCP_DEFAULT=0`, `DWMWCP_DONOTROUND=1`, `DWMWCP_ROUND=2`, `DWMWCP_ROUNDSMALL=3`
- https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type — fetched directly 2026-08-02; confirmed `DWMSBT_AUTO=0`, `DWMSBT_NONE=1`, `DWMSBT_MAINWINDOW=2` (standard Mica — matches D-02's requirement exactly), `DWMSBT_TRANSIENTWINDOW=3` (Acrylic), `DWMSBT_TABBEDWINDOW=4` (Mica Alt)
- https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-rounded-corners — fetched directly 2026-08-02; confirmed "All standard WinForms and WPF apps are rounded automatically" on Windows 11, and provided the exact official C# WinForms `DwmSetWindowAttribute`/`DWM_WINDOW_CORNER_PREFERENCE` P/Invoke example
- https://github.com/dotnet/winforms/issues/13935 — "Does WinForms react to Dark Mode settings changes... WM_SETTINGCHANGE... ImmersiveColorSet?" — open/unresolved, confirms no built-in live theme-following (independently corroborates the official docs' own stated limitation)
- https://github.com/dotnet/winforms/issues/12027 — confirmed open, static `SystemBrushes`/`SystemPens` cache bug affecting `ToolStripSeparator`/dropdown arrows on live color-mode switch
- https://github.com/dotnet/winforms/issues/13897 — confirmed open, `FlatStyle.Flat` + dark mode `FlatAppearance` color bug
- https://github.com/dotnet/winforms/issues/11893 — confirmed open, `DataGridView` row/column/header incorrect-color issues under dark mode
- Direct source-tree reads (this repository, 2026-08-02): `src/RigToggle.App/Program.cs`, `MainForm.cs`, `MainForm.Designer.cs`, `SettingsForm.cs`, `SettingsForm.Designer.cs`, `MonitorConfirmDialog.cs`, `MonitorConfirmDialog.Designer.cs`, `RigToggle.App.csproj`; `src/RigToggle.Windows/NativeMethods.cs`, `GlobalHotkey.cs`, `RigToggle.Windows.csproj`; `src/RigToggle.Core/RigToggle.Core.csproj` — ground truth for the control inventory, startup-ordering analysis, and the `txtHotkey`/`SystemColors` finding, none of which were re-derived from the milestone-level research artifacts alone

### Secondary (MEDIUM confidence)
- https://ironsoftware.com/academy/csharp-framework/dotnet10-dark-mode-winforms/ — practical caveats (call before control creation, `MessageBox` stays light, VS Designer doesn't preview dark mode) — corroborates official docs, not independently verified beyond that
- WebSearch aggregation on `SystemEvents.UserPreferenceChanged` category filtering, `AppsUseLightTheme` vs `SystemUsesLightTheme` key scope, and `DataGridView`/`GroupBox` dark-mode limitations — consistent across multiple independent sources, no single authoritative Microsoft page found for each specific claim (see Assumptions Log A1)

### Tertiary (LOW confidence)
- None used as load-bearing claims in this document — all WebSearch findings that lacked corroboration were either dropped, flagged `[ASSUMED]` in the Assumptions Log, or surfaced as Open Questions rather than stated as fact.

## Metadata

**Confidence breakdown:**
- Standard stack (`SetColorMode`, DWM constants): HIGH — every API surface and constant fetched directly from current official Microsoft Learn pages, not training-data recall
- Architecture (Core/Windows/App placement, startup ordering): HIGH — verified directly against the real `Program.cs`/`MainForm.cs` source, not inferred
- Control-by-control THEME-04/05 breakdown: HIGH for the control inventory itself (direct Designer.cs read); MEDIUM for which controls will/won't theme automatically (based on official docs + corroborated GitHub issues, not independently tested in this sandbox since it has no Windows runtime)
- Pitfalls: HIGH for the four confirmed via open `dotnet/winforms` GitHub issues; MEDIUM for `SystemEvents` threading/category-filtering specifics (Assumptions Log A1/A3)

**Research date:** 2026-08-02
**Valid until:** 30 days (stable, official-docs-backed API surface; the two open GitHub issues could close/change behavior at any .NET 10 servicing release, re-check if this phase's execution slips past a .NET 10 patch release)
