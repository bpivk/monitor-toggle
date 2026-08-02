# Architecture Research

**Domain:** Live OS-theme-aware re-theming for an existing 4-project WinForms desktop app (Core/Windows/App/Tests split), plus a drop-in tray-icon asset swap
**Researched:** 2026-08-02
**Confidence:** HIGH (integration points verified directly against the real source tree); MEDIUM on a couple of WinForms-runtime specifics flagged inline (SystemEvents threading, UserPreferenceCategory filtering) where no single official Microsoft Learn page was fetched but the pattern is broadly corroborated

## Standard Architecture

### System Overview

This is **not a new subsystem** — it is two new capabilities threaded through the existing four-project solution (`RigToggle.Core` / `RigToggle.Windows` / `RigToggle.App` / `RigToggle.Tests`) using the exact composition-root + interface-adapter pattern already established by `IAutostartConfigurator` / `WindowsAutostartConfigurator` and `GlobalHotkey`.

```
┌─────────────────────────────────────────────────────────────────────────┐
│ RigToggle.App  (net10.0-windows, UseWindowsForms, composition root)     │
│                                                                           │
│  Program.cs ── constructs WindowsThemeProvider (singleton) ──┐          │
│                                                                 │          │
│  ┌───────────────┐   ┌──────────────────┐   ┌───────────────────────┐  │
│  │ MainForm       │   │ SettingsForm      │   │ MonitorConfirmDialog  │  │
│  │ (long-lived,   │   │ (transient modal) │   │ (transient modal)     │  │
│  │  tray-resident)│   │                   │   │                       │  │
│  │                │   │                   │   │                       │  │
│  │ subscribes to  │   │ subscribes to     │   │ subscribes to         │  │
│  │ ThemeChanged   │   │ ThemeChanged      │   │ ThemeChanged          │  │
│  │ on Load,       │   │ on Load,          │   │ on Load,              │  │
│  │ unsubscribes   │   │ unsubscribes on   │   │ unsubscribes on       │  │
│  │ on Dispose     │   │ FormClosed        │   │ FormClosed            │  │
│  └───────┬────────┘   └─────────┬─────────┘   └──────────┬────────────┘  │
│          │  both call ThemeApplier.Apply(this, theme)     │             │
│          │  and DwmTitleBar.ApplyTheme(Handle, isDark)    │             │
│          ▼                       ▼                        ▼             │
│  ┌────────────────────────────────────────────────────────────────┐    │
│  │ ThemeApplier (NEW, static, App-layer only — knows about the    │    │
│  │ specific control tree/types of THIS app's Designer-generated   │    │
│  │ forms: Label/Button/TextBox/ComboBox/CheckBox/GroupBox/         │    │
│  │ DataGridView/ErrorProvider)                                     │    │
│  └────────────────────────────────────────────────────────────────┘    │
│                                                                           │
│  Resources\normal.ico, Resources\rig.ico ── REPLACED IN PLACE           │
│  (MainForm.LoadTrayIconsIfNeeded/EmbeddedResource wiring UNCHANGED)     │
└──────────────────────────────┬────────────────────────────────────────┘
                                │ ProjectReference
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ RigToggle.Windows  (net10.0-windows, UseWindowsForms, real adapters)   │
│                                                                           │
│  WindowsThemeProvider : IThemeProvider  (NEW)                          │
│    - reads HKCU\...\Personalize\AppsUseLightTheme (registry)           │
│    - subscribes to Microsoft.Win32.SystemEvents.UserPreferenceChanged  │
│    - raises public C# event ThemeChanged                               │
│                                                                           │
│  DwmTitleBar (NEW, public static façade — mirrors GlobalHotkey.cs)     │
│    - wraps NativeMethods.DwmSetWindowAttribute (dwmapi.dll)            │
│                                                                           │
│  NativeMethods.cs (MODIFIED — new DllImport added, existing ones untouched)│
└──────────────────────────────┬────────────────────────────────────────┘
                                │ ProjectReference
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ RigToggle.Core  (net10.0, ZERO Windows API references — enforced)      │
│                                                                           │
│  Abstractions\IThemeProvider.cs (NEW) — theme contract only, no P/Invoke,│
│  no registry, no Microsoft.Win32 dependency of any kind                 │
│  Models\AppTheme.cs (NEW) — enum { Light, Dark }                       │
└─────────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Layer / Status |
|-----------|----------------|-----------------|
| `IThemeProvider` | Pure contract: "what theme is active right now" + "notify me when it changes." No knowledge of registry paths, DWM, or SystemEvents. | `RigToggle.Core.Abstractions` — **NEW** |
| `AppTheme` (enum: `Light`, `Dark`) | Shared value type both Core and downstream layers can reason about without a `bool`'s ambiguous polarity. | `RigToggle.Core.Models` — **NEW** |
| `WindowsThemeProvider` | Implements `IThemeProvider`: reads `AppsUseLightTheme` DWORD from the registry for the initial value; subscribes to `Microsoft.Win32.SystemEvents.UserPreferenceChanged` for live updates; re-reads the registry on each relevant event and raises `ThemeChanged` only if the resolved theme actually changed (dedupe — `UserPreferenceChanged` fires for many unrelated preference categories, not just theme). | `RigToggle.Windows` — **NEW** |
| `DwmTitleBar` | Public static façade (same shape as the existing `GlobalHotkey` class) around one P/Invoke: `DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int))`. Takes a raw `IntPtr` handle + bool, returns nothing, never throws (best-effort — see Anti-Patterns). | `RigToggle.Windows` — **NEW** |
| `NativeMethods` | Gains one new `[DllImport("dwmapi.dll")]` signature for `DwmSetWindowAttribute`, alongside a `DWMWA_USE_IMMERSIVE_DARK_MODE = 20` constant. Everything else in this file is untouched. | `RigToggle.Windows` — **MODIFIED** |
| `ThemeApplier` | App-layer-only static helper. Walks a `Form`'s `Controls` tree recursively and applies a light/dark `Color` palette per concrete control type (see Patterns below). This is where Designer-generated-form-specific knowledge lives — it is deliberately *not* pushed down into `RigToggle.Windows`, matching the existing rule that all WinForms/Designer code stays in `RigToggle.App`. | `RigToggle.App` — **NEW** |
| `MainForm` | Constructor gains an `IThemeProvider` parameter (composition-root-injected, same pattern as every other dependency). Subscribes to `ThemeChanged` in `OnLoad`/`InitializeTrayState`, unsubscribes in `Dispose(bool)`. Applies theme once on load/`InitializeTrayState` (covers the `--tray` hidden-startup path, mirroring the existing `InitializeTrayState` Pitfall-6 precedent) and again on every `ThemeChanged` event while running. | `RigToggle.App` — **MODIFIED** |
| `SettingsForm` | Same `IThemeProvider` injection via constructor (added to the existing `SettingsFormFactory` closure in `Program.cs`). Subscribes on `Load`, unsubscribes on `FormClosed` (it is transient — `using var settingsForm = ...` in `MainForm.OpenSettingsDialog` — an un-unsubscribed handler on a singleton `WindowsThemeProvider` would otherwise leak the disposed form and later touch disposed controls). | `RigToggle.App` — **MODIFIED** |
| `MonitorConfirmDialog` | Same transient-subscription treatment as `SettingsForm`. Currently constructed with no injected dependencies at all ("Pure display data" per its own doc comment) — this is the one place where adding `IThemeProvider` is a real, if small, precedent break; still the right call for visual consistency (DISPLAY-07's confirm dialog is shown on literally every first toggle). | `RigToggle.App` — **MODIFIED** |
| `Program.cs` | Composition root gains: construct `WindowsThemeProvider` once, alongside `monitorController`/`audioController`/etc.; pass it into `MainForm`'s constructor and into the `SettingsFormFactory`/`MonitorConfirmDialog` construction sites. No other startup-sequencing change — it slots in next to the existing adapter-construction block. | `RigToggle.App` — **MODIFIED** |
| `RigToggle.App.csproj` `Resources\normal.ico` / `rig.ico` | Binary asset swap only. `EmbeddedResource`/`LogicalName` wiring (`normal.ico`, `rig.ico`) is untouched — `MainForm.LoadTrayIconsIfNeeded()`'s `GetManifestResourceStream("normal.ico")` / `("rig.ico")` calls need zero code changes as long as the replacement files keep the same filenames and are valid multi-resolution `.ico` files. | `RigToggle.App\Resources\` — **MODIFIED (binary only)** |

## Recommended Project Structure

```
src/
├── RigToggle.Core/
│   ├── Abstractions/
│   │   ├── IThemeProvider.cs          # NEW — AppTheme CurrentTheme { get; }; event EventHandler<EventArgs>? ThemeChanged;
│   │   └── ... (existing interfaces, untouched)
│   └── Models/
│       ├── AppTheme.cs                # NEW — public enum AppTheme { Light, Dark }
│       └── ... (existing models, untouched)
│
├── RigToggle.Windows/
│   ├── WindowsThemeProvider.cs        # NEW — IThemeProvider impl: registry read + SystemEvents subscription
│   ├── DwmTitleBar.cs                 # NEW — public static façade over DwmSetWindowAttribute (mirrors GlobalHotkey.cs)
│   ├── NativeMethods.cs               # MODIFIED — + DwmSetWindowAttribute DllImport, + DWMWA_USE_IMMERSIVE_DARK_MODE const
│   └── ... (existing adapters, untouched)
│
├── RigToggle.App/
│   ├── ThemeApplier.cs                # NEW — static recursive control-tree recolor pass, App-specific control knowledge
│   ├── Program.cs                     # MODIFIED — construct WindowsThemeProvider, inject into MainForm/SettingsFormFactory/MonitorConfirmDialog
│   ├── MainForm.cs                    # MODIFIED — IThemeProvider ctor param, subscribe/unsubscribe, apply-on-load + apply-on-change
│   ├── SettingsForm.cs                # MODIFIED — same treatment, transient-subscription lifecycle
│   ├── MonitorConfirmDialog.cs        # MODIFIED — same treatment (new dependency, previously had none)
│   └── Resources/
│       ├── normal.ico                 # MODIFIED (binary swap only — same filename/LogicalName)
│       └── rig.ico                    # MODIFIED (binary swap only — same filename/LogicalName)
│
└── RigToggle.Tests/
    └── (new unit tests against a fake IThemeProvider, following the existing
        Core-tested-against-fakes convention — WindowsThemeProvider itself,
        like every other Windows* adapter, is not unit-testable and is verified
        on the rig instead)
```

### Structure Rationale

- **`IThemeProvider`/`AppTheme` in Core:** Keeps `RigToggle.Core` at zero Windows-API references — a structural invariant the project already enforces with an explicit comment in `RigToggle.Core.csproj` ("D-08 structural enforcement... Do NOT add a PackageReference... any such addition is a regression"). The interface's shape (a current-value query + a change event) is intentionally the *smallest possible* Windows-agnostic contract — it says nothing about registries, DWM, or WM_SETTINGCHANGE, exactly as `IAutostartConfigurator` says nothing about the `Run` registry key internally.
- **`WindowsThemeProvider`/`DwmTitleBar` in `RigToggle.Windows`:** This project already targets `net10.0-windows` with `UseWindowsForms=true` (confirmed in `RigToggle.Windows.csproj`), so `Microsoft.Win32.SystemEvents` is already available with **zero new package references** — it ships as part of the Windows Desktop shared framework that `UseWindowsForms` pulls in. `DwmTitleBar` deliberately mirrors the existing `GlobalHotkey.cs` shape (public static façade over an `internal` `NativeMethods` P/Invoke, because `RigToggle.App` has no `InternalsVisibleTo` grant to `RigToggle.Windows` — only `RigToggle.Windows.Tests` has that, per `AssemblyInfo.cs`). Do not add a new `InternalsVisibleTo` grant to route around this; follow the established public-façade pattern instead.
- **`ThemeApplier` in `RigToggle.App`, not `RigToggle.Windows`:** This is the one placement decision worth stating explicitly, since it would be *technically possible* to put a generic "recolor any WinForms control tree" helper in `RigToggle.Windows` (which already has `UseWindowsForms=true`). It stays in `RigToggle.App` because it isn't a generic capability — it encodes knowledge specific to *this app's* Designer-generated control layout (which `GroupBox`es nest which controls, that `dgvMonitors` needs `DefaultCellStyle`/`ColumnHeadersDefaultCellStyle` set separately from its own `BackColor`, that `btnToggle`/`btnSettings` need `FlatStyle = Flat` before a custom `BackColor` will actually render). `RigToggle.Windows` is reserved for OS-facing *mechanism* (Win32/COM/CCD calls); WinForms *composition* — which is what a control-tree walk over `MainForm.Controls` fundamentally is — has never lived there, and every other GUI-composition concern in this codebase (all three `.Designer.cs` files, all event wiring) lives in `RigToggle.App` only.
- **Injected via constructor, composed in `Program.cs`:** Follows the existing, explicitly-documented rule verbatim from `Program.cs`'s own doc comment: "MainForm/SettingsForm never `new` a concrete adapter or store themselves." `WindowsThemeProvider` is a concrete Windows adapter exactly like `WindowsAutostartConfigurator`; it must be constructed once in `Program.cs` and passed down, not `new`'d inside a form.

## Architectural Patterns

### Pattern 1: Core-interface + Windows-adapter (existing pattern, reused verbatim)

**What:** A dependency-free contract in `RigToggle.Core.Abstractions`, one concrete Windows implementation in `RigToggle.Windows`, wired together only in `Program.cs`. This is not a new pattern for this milestone — it's the same shape as `IAutostartConfigurator`/`WindowsAutostartConfigurator`, `IMonitorController`/`WindowsMonitorController`, etc.
**When to use:** Any time App-layer code needs an OS capability. Theme detection is architecturally identical to "is autostart enabled" — a query plus a change notification, backed by the registry.
**Trade-offs:** `WindowsThemeProvider` itself is not unit-testable (same as every other `Windows*` class in this codebase) — verification is rig-only, exactly like `WindowsMonitorController`/`WindowsAudioController`. `IThemeProvider` as an interface *is* independently testable with a fake, which is the entire point of the split.

**Example:**
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

### Pattern 2: Registry read + `SystemEvents.UserPreferenceChanged` for live updates (NEW infrastructure, standard Win32 idiom)

**What:** `WindowsThemeProvider` reads `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` (`0` = dark, `1` = light — this specific key controls *app* theme; the sibling `SystemUsesLightTheme` key controls shell/taskbar chrome and is not what you want here) for the current value, then subscribes to `Microsoft.Win32.SystemEvents.UserPreferenceChanged` to be notified of *any* system preference change and re-reads the registry key on each notification, raising `ThemeChanged` only when the resolved value differs from the last-known one.
**When to use:** This is the mechanism `IThemeProvider`'s single implementation uses internally — App-layer code never touches the registry or `SystemEvents` directly.
**Trade-offs / confidence notes:**
- `UserPreferenceChanged` fires for many unrelated preference categories (mouse, keyboard, power, etc.), not just theme — filtering on `e.Category == UserPreferenceCategory.General` before re-reading the registry is the widely-used community pattern for this, though I did not find one single official Microsoft Learn page pinning down that exact category for *this specific* key (MEDIUM confidence — corroborated across multiple independent sources, not an official doc citation). Re-reading and comparing against the last-known value regardless of category is the safer fallback if `General` proves too narrow or too noisy on the rig.
- `SystemEvents` raises its events on the thread that first touched the `SystemEvents` static class — for a WinForms app this is *usually*, but not guaranteed to be, the UI thread. Any `ThemeChanged` handler in `MainForm`/`SettingsForm`/`MonitorConfirmDialog` **must marshal onto the form's own UI thread** (`InvokeRequired` / `BeginInvoke`) before touching any control, exactly the same defensive pattern this codebase already applies elsewhere for cross-thread safety. Skipping this is the single most likely source of an intermittent, hard-to-repro crash in this feature.

**Example:**
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
            return AppTheme.Light; // best-effort — matches this codebase's existing "never throw from a Load-time read" convention
        }
    }

    public void Dispose() => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}
```

### Pattern 3: DWM title-bar façade, best-effort, applied per-form on `HandleCreated` (NEW infrastructure)

**What:** `DwmTitleBar.ApplyTheme(IntPtr handle, bool useDarkMode)` wraps `DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE /* = 20 */, ref value, sizeof(int))`. Called from each form once its `Handle` exists (`OnHandleCreated` override, or `OnLoad` — `Handle` is guaranteed created by the time `OnLoad` fires) and again every time `ThemeChanged` fires while the form is open.
**When to use:** Any top-level `Form` that should have a themed non-client title bar — that's `MainForm`, `SettingsForm`, and `MonitorConfirmDialog` (all three are currently plain `SystemColors`-styled top-level forms per the milestone context).
**Trade-offs:**
- `DWMWA_USE_IMMERSIVE_DARK_MODE = 20` is the modern (Windows 10 20H1+/Windows 11) constant value. Calling it on an unsupported build simply fails the `HRESULT` — treat this exactly like every other best-effort OS call already in this codebase (`RegisterHotkeyAtStartup`, autostart writes): catch/ignore a non-success result, never throw, never block startup or the recolor pass.
- Must be re-applied on every `ThemeChanged` event, not just once at startup — the whole point of wiring `SystemEvents` instead of relying on .NET 10's built-in `Application.SetColorMode(SystemColorMode.System)` is live updates without a restart (see Anti-Pattern 1 below for why the built-in API alone doesn't satisfy this tray-resident app's requirements).

**Example:**
```csharp
// RigToggle.Windows/DwmTitleBar.cs
public static class DwmTitleBar
{
    public static void ApplyTheme(IntPtr handle, bool useDarkMode)
    {
        try
        {
            int value = useDarkMode ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
        catch
        {
            // Best-effort — an unsupported Windows build or a transient DWM failure
            // must never prevent the window from opening (same posture as every
            // other OS-facing call in this codebase).
        }
    }
}
```

### Pattern 4: Recursive control-tree recolor pass, keyed by concrete `Control` type (NEW, App-layer only)

**What:** `ThemeApplier.Apply(Control root, AppTheme theme)` walks `root.Controls` recursively (needed because `grpMonitor`/`grpAudioDevices`/`grpAppPath` are `GroupBox`es that nest their own child controls — a shallow, non-recursive pass over `Form.Controls` alone would miss everything inside them), pattern-matching on the concrete runtime type of each control to apply a small per-type palette.
**When to use:** Called once per form on load (after `InitializeComponent()`), and again on every `ThemeChanged` event while that form is open.
**Trade-offs:**
- `Button` needs `FlatStyle = FlatStyle.Flat` (plus `FlatAppearance.BorderColor`) before a custom `BackColor` renders at all — the default `FlatStyle.Standard` delegates painting to the OS visual-styles renderer, which ignores `BackColor`. This is a well-known WinForms gotcha and the most common reason a naive "just set BackColor everywhere" recolor pass looks broken on buttons specifically.
- `DataGridView` needs its `BackgroundColor`, `DefaultCellStyle` (`BackColor`/`ForeColor`/`SelectionBackColor`/`SelectionForeColor`), `ColumnHeadersDefaultCellStyle`, `GridColor`, and `EnableHeadersVisualStyles = false` all set explicitly — `dgvMonitors`'s three-column grid (name / disable-checkbox / enable-checkbox) is the single most theming-intensive control in this app.
- `ComboBox` fully honors `BackColor`/`ForeColor` for the edit portion but the dropdown button glyph area can remain visually inconsistent depending on `FlatStyle`; test `cboAudioNormal`/`cboAudioRig` specifically on the rig in dark mode.
- Native/system chrome — `MessageBox.Show(...)` (used extensively in `MainForm.BtnToggle_Click`/`OpenSettingsDialog` error paths) and `OpenFileDialog` (`dlgOpenExe` in `SettingsForm`) — is **out of scope for `ThemeApplier`** and already follows the OS theme automatically on Windows 11 without any app-side work; do not attempt to reimplement these as custom-drawn dialogs to "theme" them (see Anti-Pattern 3).
- `ErrorProvider`'s red warning-icon glyphs (`errMonitor`, `errAudioNormal`, etc.) are not `Control`s and are not touched by a `Controls`-tree walk at all — leave them as-is; their small red icon reads fine on both light and dark backgrounds.

**Example:**
```csharp
// RigToggle.App/ThemeApplier.cs
internal static class ThemeApplier
{
    private static readonly Color DarkBack = Color.FromArgb(32, 32, 32);
    private static readonly Color DarkFore = Color.FromArgb(240, 240, 240);
    private static readonly Color DarkControlBack = Color.FromArgb(45, 45, 48);

    public static void Apply(Control root, AppTheme theme)
    {
        bool dark = theme == AppTheme.Dark;
        root.BackColor = dark ? DarkBack : SystemColors.Control;
        root.ForeColor = dark ? DarkFore : SystemColors.ControlText;

        foreach (Control child in root.Controls)
        {
            ApplyToControl(child, dark);
            if (child.HasChildren)
            {
                Apply(child, theme); // recurse into GroupBox etc.
            }
        }
    }

    private static void ApplyToControl(Control c, bool dark)
    {
        switch (c)
        {
            case Button b:
                b.FlatStyle = FlatStyle.Flat;
                b.BackColor = dark ? DarkControlBack : SystemColors.ButtonFace;
                b.ForeColor = dark ? DarkFore : SystemColors.ControlText;
                b.FlatAppearance.BorderColor = dark ? Color.FromArgb(70, 70, 70) : SystemColors.ControlDark;
                break;
            case DataGridView g:
                g.BackgroundColor = dark ? DarkBack : SystemColors.Window;
                g.EnableHeadersVisualStyles = false;
                g.DefaultCellStyle.BackColor = dark ? DarkControlBack : SystemColors.Window;
                g.DefaultCellStyle.ForeColor = dark ? DarkFore : SystemColors.ControlText;
                g.ColumnHeadersDefaultCellStyle.BackColor = dark ? DarkBack : SystemColors.Control;
                g.ColumnHeadersDefaultCellStyle.ForeColor = dark ? DarkFore : SystemColors.ControlText;
                g.GridColor = dark ? Color.FromArgb(70, 70, 70) : SystemColors.ControlLight;
                break;
            case TextBox or ComboBox or Label or CheckBox or GroupBox:
                c.BackColor = dark ? DarkControlBack : SystemColors.Window;
                c.ForeColor = dark ? DarkFore : SystemColors.ControlText;
                break;
        }
    }
}
```

## Data Flow

### Startup Flow (per form, first paint)

```
Program.cs Main()
    │
    ├─ new WindowsThemeProvider()  ─── reads AppsUseLightTheme registry value once,
    │                                   subscribes to SystemEvents.UserPreferenceChanged
    │
    ├─ new MainForm(..., themeProvider)
    │       │
    │       └─ InitializeTrayState() [existing Pitfall-6 entry point, runs even under --tray]
    │               │
    │               ├─ ThemeApplier.Apply(this, themeProvider.CurrentTheme)
    │               ├─ DwmTitleBar.ApplyTheme(this.Handle, themeProvider.CurrentTheme == Dark)
    │               └─ themeProvider.ThemeChanged += OnThemeChanged  (subscribe once, long-lived form)
    │
    └─ Application.Run(...)  [SettingsForm/MonitorConfirmDialog constructed+shown later, on demand]
            │
            └─ each: Load handler applies theme once, subscribes to ThemeChanged,
                      FormClosed handler unsubscribes (transient-form lifecycle)
```

### Live Theme-Change Flow (app already running, user flips Windows Settings > light/dark)

```
Windows OS theme toggled (Settings app writes AppsUseLightTheme + broadcasts WM_SETTINGCHANGE)
    │
    ▼
Microsoft.Win32.SystemEvents.UserPreferenceChanged  (fires on SystemEvents' own thread —
    │                                                  not guaranteed to be the UI thread)
    ▼
WindowsThemeProvider.OnUserPreferenceChanged
    │  re-reads registry, compares to CurrentTheme
    ▼
WindowsThemeProvider.ThemeChanged event raised (only if value actually changed)
    │
    ├──────────────► MainForm.OnThemeChanged  (marshal via BeginInvoke if InvokeRequired)
    │                     │
    │                     ├─ ThemeApplier.Apply(this, e.CurrentTheme)
    │                     └─ DwmTitleBar.ApplyTheme(this.Handle, isDark)
    │
    └──────────────► SettingsForm.OnThemeChanged / MonitorConfirmDialog.OnThemeChanged
                          (same, only if currently open — subscribed while open, unsubscribed on close)
```

### Key Data Flows

1. **Cold start (visible or `--tray`):** `WindowsThemeProvider` resolves the current theme from the registry exactly once at construction, synchronously, before any form is shown — this is what lets `MainForm.InitializeTrayState()` apply the correct theme even under `--tray` hidden startup, mirroring the existing tray-icon-priming precedent for the exact same reason (`Form.Load` never fires when the form is never shown).
2. **Live update while running:** A single app-wide `SystemEvents.UserPreferenceChanged` subscription (owned by the one `WindowsThemeProvider` instance) fans out to however many forms are currently open via the `ThemeChanged` event — each open form re-applies independently; there is no central "re-theme everything" broadcaster beyond the event itself.
3. **Icon asset flow (independent of the above):** `MainForm.LoadTrayIconsIfNeeded()` → `Assembly.GetManifestResourceStream("normal.ico"/"rig.ico")` → `new Icon(stream)`. This path is entirely unaffected by the theme work — replacing the two `.ico` files under `RigToggle.App\Resources\` with better-designed art requires zero changes to this flow, as long as filenames and the `LogicalName` mapping in `RigToggle.App.csproj` stay the same.

## Scaling Considerations

Not applicable in the traditional user-scale sense (single-user personal tool). The relevant "growth" axis here is **number of top-level forms** and **control-tree size per form**:

| Scale | Approach |
|-------|----------|
| Current: 3 forms (`MainForm`, `SettingsForm`, `MonitorConfirmDialog`), ~20 controls total | `ThemeApplier.Apply` recursive walk is O(controls) per call, called on load + on each theme change (a rare, human-triggered event) — no performance concern at this size. |
| If a 4th/5th form is added later (e.g. a future toggle-history/log viewer, LOG-01, currently deferred) | Same pattern extends directly: inject `IThemeProvider`, call `ThemeApplier.Apply` on load, subscribe/unsubscribe symmetrically with the form's lifecycle. No architectural change needed — this is precisely why the recolor pass is a generic type-switch rather than per-form hardcoded control references. |
| If `ThemeApplier`'s per-type switch grows unwieldy (many more control types) | Consider extracting a small `IThemeable`/palette-record abstraction at that point — premature for the current ~6 distinct control types in play (`Label`, `Button`, `TextBox`, `ComboBox`, `CheckBox`, `GroupBox`, `DataGridView`). Not worth building now. |

## Anti-Patterns

### Anti-Pattern 1: Relying solely on .NET 10's `Application.SetColorMode(SystemColorMode.System)` for this app

**What people do:** .NET 10 made WinForms' built-in dark-mode support (`Application.SetColorMode`) non-experimental (it was gated behind compiler error WFO5001 in .NET 9; that gate is removed in .NET 10 — confirmed directly against the official "What's new in WinForms for .NET 10" Learn page). `SystemColorMode.System` "detects the current Windows system theme and applies it," which sounds like it could replace all of the above.
**Why it's wrong for this app specifically:** `SystemColorMode.System` is applied once, at startup, before `Application.Run` — it does **not** react live if the user changes the Windows theme while the app keeps running (confirmed via a secondary source describing this exact limitation; the app would need a restart to pick up the change). `RigToggle` is explicitly designed to be tray-resident for long stretches (autostart + minimize/close-to-tray, shipped in v1.1) — a theme mechanism that only takes effect on restart directly contradicts that usage pattern. This is exactly why the milestone context already calls out "requires manual DWM API calls for the title bar plus re-coloring every control by hand" as the accepted approach — this research confirms that decision was well-founded, not merely un-researched.
**Instead:** Use `SystemEvents.UserPreferenceChanged` + manual `ThemeApplier`/`DwmTitleBar` (Patterns 2–4 above) specifically because they support live updates without a restart. `Application.SetColorMode` is not used at all in this design — do not add it alongside the manual approach; running both simultaneously risks fighting over control colors with no clear precedence.

### Anti-Pattern 2: Subscribing to `ThemeChanged` from a transient dialog without unsubscribing

**What people do:** `SettingsForm`/`MonitorConfirmDialog` subscribe to the singleton `WindowsThemeProvider.ThemeChanged` event on `Load` and never unsubscribe.
**Why it's wrong:** The event source (`WindowsThemeProvider`, constructed once in `Program.cs`) outlives every transient dialog. An un-unsubscribed `+=` keeps the disposed form reachable (classic .NET event-leak) and — worse — the next OS theme change would invoke a handler that touches disposed `Control`s, throwing `ObjectDisposedException` from inside an event handler with no visible call site, a genuinely nasty bug to diagnose later.
**Instead:** Unsubscribe in `FormClosed` (not just `Dispose(bool)` — `FormClosed` fires reliably for both `ShowDialog()` returns and the `X`/Escape/Cancel paths already wired via `AcceptButton`/`CancelButton` in both dialogs). This mirrors the existing disposal fastidiousness already present in this codebase (`MainForm.Designer.cs`'s explicit `_normalIcon?.Dispose(); _rigIcon?.Dispose();` alongside the `components?.Dispose()` backstop).

### Anti-Pattern 3: Trying to re-theme `MessageBox.Show(...)` or `OpenFileDialog`

**What people do:** Notice that `MessageBox.Show` (used throughout `MainForm`'s toggle-result/error paths) still looks like stock light-mode chrome even after `ThemeApplier` runs, and reach for a custom-drawn replacement `MessageBox`/`TaskDialog`.
**Why it's wrong:** These are native OS common-dialog windows, not WinForms `Control`s — they are outside `ThemeApplier`'s `Controls`-tree walk by construction, and on Windows 11 they already follow the OS theme automatically at the OS level with zero app-side code (this is a Windows-shell-level behavior, not something this app's `ThemeApplier` needs to reproduce). Replacing them with a custom-drawn dialog is a large scope increase (new form, new layout, new icon/button semantics to match `MessageBoxIcon.Warning`/`Information`, accessibility work) for a milestone explicitly scoped as "visual polish," not a UI framework rewrite.
**Instead:** Leave `MessageBox.Show` and `OpenFileDialog` exactly as they are. If they visually clash with a dark-themed `MainForm`/`SettingsForm` on the actual rig hardware, that is expected native-dialog behavior, not a bug in `ThemeApplier`.

### Anti-Pattern 4: Coupling the tray-icon redesign to the theme-detection work

**What people do:** Assume the new `normal.ico`/`rig.ico` pair needs to be theme-aware (e.g., swapped for a "dark-taskbar" variant) because the rest of this milestone is about theme-following.
**Why it's wrong:** `NotifyIcon` tray glyphs are a single static asset rendered against the taskbar, not the app's own window chrome — `MainForm.LoadTrayIconsIfNeeded`/`RefreshUi` already select between exactly two icons (`normal.ico` vs `rig.ico`) based on **toggle mode**, not OS theme, and that axis is unrelated to light/dark. Introducing a second theme-driven axis (4 icon variants: normal-light, normal-dark, rig-light, rig-dark) would be a real scope increase to both the icon-design work and `LoadTrayIconsIfNeeded`/`RefreshUi`, and is not what the milestone context asks for ("Redesigned tray icon pair: visually distinct... for rig mode vs. normal mode").
**Instead:** Design the two new icons with enough internal contrast/outline to read clearly against both a light and a dark Windows 11 taskbar (standard tray-icon design practice — most Windows tray icons ship as one asset that works on both), but keep the icon-loading code path (`LoadTrayIconsIfNeeded`, the two `EmbeddedResource`/`LogicalName` entries in `RigToggle.App.csproj`) completely untouched. The icon swap and the theme work are architecturally independent — see Integration Points below.

## Integration Points

### External Services (OS-level)

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` | `Microsoft.Win32.Registry.CurrentUser.OpenSubKey(...)` inside `WindowsThemeProvider`, read-only | `0` = dark, `1` = light. This is the *app*-theme key; do not confuse with the sibling `SystemUsesLightTheme` key (shell/taskbar chrome — not relevant here). Key/value name confirmed stable across current Windows 10/11 builds via multiple corroborating sources (HIGH confidence — this is one of the most widely-used undocumented-but-stable registry reads in the WinForms/WPF ecosystem, functionally analogous in stability to this project's own existing `IPolicyConfig` COM interop precedent). |
| `Microsoft.Win32.SystemEvents.UserPreferenceChanged` | Static event subscription inside `WindowsThemeProvider`'s constructor; unsubscribe in `Dispose` | Already available with zero new package references (`RigToggle.Windows` already has `UseWindowsForms=true`). Fires on a non-guaranteed thread — every downstream handler (in `RigToggle.App`) must marshal to the UI thread before touching controls (see Pattern 2). |
| `dwmapi.dll` `DwmSetWindowAttribute` (`DWMWA_USE_IMMERSIVE_DARK_MODE = 20`) | New `[DllImport]` in `NativeMethods.cs`, wrapped by the new public `DwmTitleBar` façade | Best-effort — matches this codebase's existing convention of never letting an OS-facing call block startup or crash the app (`RegisterHotkeyAtStartup`, autostart writes, etc. all follow the same try/catch/trace posture). |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `RigToggle.Core` ↔ `RigToggle.Windows` | `IThemeProvider` interface implemented by `WindowsThemeProvider` | Identical shape to every other Core-interface/Windows-adapter pair already in this codebase (`IAutostartConfigurator`/`WindowsAutostartConfigurator`, `IMonitorController`/`WindowsMonitorController`, etc.) — no new pattern introduced. |
| `RigToggle.Windows` ↔ `RigToggle.App` | Public façade classes only (`WindowsThemeProvider`, `DwmTitleBar`) — `NativeMethods` stays `internal`, no new `InternalsVisibleTo` grant | Mirrors the existing `GlobalHotkey` public-façade-over-internal-P/Invoke pattern exactly. |
| `RigToggle.App`'s composition root (`Program.cs`) ↔ its forms | Constructor injection, same as every existing dependency (`ToggleOrchestrator`, `ISettingsStore`, `IMonitorController`, etc.) | `WindowsThemeProvider` is constructed once in `Program.cs` and threaded into `MainForm`'s constructor and the `SettingsFormFactory`/`MonitorConfirmDialog` construction sites — never `new`'d inside a form. |
| Theme work ↔ icon-redesign work | None — fully independent | The icon swap touches only `RigToggle.App\Resources\*.ico` (binary files) and, if replacement `.ico`s use different filenames, the two `EmbeddedResource`/`LogicalName` lines in `RigToggle.App.csproj`. It shares no files, no interfaces, and no runtime data flow with `IThemeProvider`/`ThemeApplier`/`DwmTitleBar`. See "Suggested Build Order" below. |

### Suggested Build Order

1. **Theme infrastructure first** (`IThemeProvider`/`AppTheme` in Core → `WindowsThemeProvider`/`DwmTitleBar`/`NativeMethods` in Windows → `ThemeApplier` + the three forms' wiring + `Program.cs` composition in App). This is the higher-risk, more novel piece of this milestone (cross-thread event marshaling, per-control-type recolor correctness, DWM best-effort handling) — sequencing it first means any rig-verification cycles needed for these specifics happen before the simpler work lands, and any per-control recoloring bugs get discovered and fixed while the forms are already being actively touched, rather than requiring a second pass later.
2. **Icon redesign second, but truly independent — can run in parallel or even land first.** There is zero technical coupling: no shared files, no shared interfaces, no ordering dependency in either direction. If the new icon art is ready before the theme infrastructure, it can be dropped in and verified immediately (does it still load via `LoadTrayIconsIfNeeded`, does it read clearly in both taskbar themes) without waiting on anything else in this milestone.
3. **README.md work (third milestone deliverable, not covered by this research)** naturally comes last since it documents the finished visual result (screenshots) — not an architectural dependency, just a practical sequencing note.

## Sources

- Direct source-tree reads (HIGH confidence — this is the actual codebase, not documentation): `src/RigToggle.App/Program.cs`, `MainForm.cs`, `MainForm.Designer.cs`, `SettingsForm.cs`, `SettingsForm.Designer.cs`, `MonitorConfirmDialog.cs`, `RigToggle.App.csproj`; `src/RigToggle.Windows/NativeMethods.cs`, `GlobalHotkey.cs`, `RigToggle.Windows.csproj`; `src/RigToggle.Core/Abstractions/IAutostartConfigurator.cs`, `Models/AppSettings.cs`, `RigToggle.Core.csproj`
- `.planning/PROJECT.md` — milestone context, prior architectural decisions, explicit "no built-in support" framing for theme-following
- https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/net100 — official, fetched directly — confirmed `Application.SetColorMode` is no longer experimental in .NET 10 (WFO5001 gate removed), confirmed `ControlStyles.ApplyThemingImplicitly` opt-in mechanism for custom controls — HIGH confidence
- https://ironsoftware.com/academy/csharp-framework/dotnet10-dark-mode-winforms/ — third-party, fetched directly — source for "`SystemColorMode.System` does not live-update; requires app restart to pick up a Windows theme change" and "Windows 11 only" claims — MEDIUM confidence (single non-official source for these two specific claims; corroborates and explains the milestone's own stated decision to hand-roll rather than use the built-in API, but not independently re-verified against a second source)
- https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-windows-themes and multiple corroborating community sources (codestudy.net, quinnscomputing.com, HMG forum) — `DWMWA_USE_IMMERSIVE_DARK_MODE = 20`, `dwmapi.dll` `DwmSetWindowAttribute` usage, Windows 10 20H1+/Windows 11 support window — HIGH confidence (value and usage pattern corroborated across many independent sources, consistent with prior knowledge)
- Multiple corroborating community sources (no single official Learn page fetched for this specific combination) — `HKCU\...\Personalize\AppsUseLightTheme` registry key semantics (`0`=dark/`1`=light) and `Microsoft.Win32.SystemEvents.UserPreferenceChanged` as the live-update mechanism — HIGH confidence for the registry key itself (extremely widely corroborated, functionally stable for years), MEDIUM confidence specifically for "filter on `UserPreferenceCategory.General`" (widely used pattern, not independently confirmed against one official doc)

---
*Architecture research for: WinForms live theme-following (registry + DWM) integration into an existing Core/Windows/App/Tests solution, plus an independent tray-icon asset swap*
*Researched: 2026-08-02*
