# Phase 12: Theme Infrastructure & Live Theme-Following - Pattern Map

**Mapped:** 2026-08-02
**Files analyzed:** 14 (5 new, 9 modified)
**Analogs found:** 14 / 14

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `src/RigToggle.Core/Abstractions/IThemeProvider.cs` (NEW) | interface/abstraction | event-driven + request-response | `src/RigToggle.Core/Abstractions/IAutostartConfigurator.cs` | role-match (adds event, autostart has none) |
| `src/RigToggle.Core/Models/AppTheme.cs` (NEW) | model (enum) | N/A (value type) | `src/RigToggle.Core/Models/ToggleStepOutcome.cs` | exact |
| `src/RigToggle.Windows/WindowsThemeProvider.cs` (NEW) | service (Windows adapter) | event-driven (registry read + `SystemEvents` subscription) | `src/RigToggle.Windows/WindowsAutostartConfigurator.cs` | exact (same HKCU registry-adapter shape) |
| `src/RigToggle.Windows/DwmTitleBar.cs` (NEW) | utility (P/Invoke façade) | request-response (fire-and-forget best-effort call) | `src/RigToggle.Windows/GlobalHotkey.cs` | exact (public façade over internal `NativeMethods`) |
| `src/RigToggle.Windows/NativeMethods.cs` (MODIFIED — add DWM constants + `DllImport`) | utility (P/Invoke signatures) | N/A | itself (existing `RegisterHotKey`/`UnregisterHotKey` block) | exact — same file, same convention |
| `src/RigToggle.App/ThemeApplier.cs` (NEW) | utility (targeted per-control recolor pass) | transform (control-tree mutation) | `src/RigToggle.App/SettingsForm.cs` (`PopulateMonitorGrid`/`RenderHotkeyIdleDisplay` — control-styling methods) | role-match (no dedicated "applier" file exists yet; closest is the form's own imperative control-styling code) |
| `src/RigToggle.App/Program.cs` (MODIFIED — composition root) | config/composition | event-driven (startup sequencing) | itself (existing `Main()` adapter-construction block) | exact |
| `src/RigToggle.App/MainForm.cs` (MODIFIED — ctor param, `OnHandleCreated`, theme-change handler) | controller/form code-behind | event-driven | itself (existing `InitializeTrayState()`/hotkey-registration pattern) | exact |
| `src/RigToggle.App/MainForm.Designer.cs` (MODIFIED — `FlatStyle.System` on buttons) | component (Designer-generated) | N/A | itself | exact |
| `src/RigToggle.App/SettingsForm.cs` (MODIFIED — ctor param, `txtHotkey` color fix, `FormClosed` unsubscribe) | controller/form code-behind | event-driven + CRUD (settings load/save) | itself (existing `_tryRegisterConfiguredHotkey`/`_applyTrayVisibility` injected-delegate pattern) | exact |
| `src/RigToggle.App/SettingsForm.Designer.cs` (MODIFIED — `GroupBox`→`Panel` x3, `DataGridView` styling hooks, `FlatStyle.System`) | component (Designer-generated) | N/A | itself | exact |
| `src/RigToggle.App/MonitorConfirmDialog.cs` (MODIFIED — first-ever injected dependency, `FlatStyle`) | controller/form code-behind | request-response | `src/RigToggle.App/SettingsForm.cs` constructor (injected-dependency pattern) | role-match (first time this form gains a dependency) |
| `src/RigToggle.App/MonitorConfirmDialog.Designer.cs` (MODIFIED — `FlatStyle.System`) | component (Designer-generated) | N/A | itself | exact |
| `src/RigToggle.Tests/Doubles/FakeThemeProvider.cs` (NEW, if Core-level tests are added) | test double | N/A | `src/RigToggle.Tests/Doubles/FakeControllers.cs` | role-match (hand-written recording fake, no mocking framework) |

## Pattern Assignments

### `src/RigToggle.Core/Abstractions/IThemeProvider.cs` (interface, Core)

**Analog:** `src/RigToggle.Core/Abstractions/IAutostartConfigurator.cs` (full file, 16 lines)

**Full pattern to copy** (interface shape + XML-doc rationale-comment convention):
```csharp
namespace RigToggle.Core.Abstractions;

/// <summary>
/// HKCU "start with Windows" registration contract (TRAY-02). Implemented by
/// RigToggle.Windows.WindowsAutostartConfigurator this phase. The current-user Run
/// registry key's existence is the single source of truth for whether autostart is
/// enabled -- there is deliberately no mirrored AppSettings boolean to drift out of
/// sync with the actual registry state.
/// </summary>
public interface IAutostartConfigurator
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
```
**Apply as:**
```csharp
namespace RigToggle.Core.Abstractions;

public interface IThemeProvider
{
    AppTheme CurrentTheme { get; }
    event EventHandler? ThemeChanged;
}
```
Match the same one-paragraph "why this exists / what the source of truth is" doc-comment style — for `IThemeProvider`, the source-of-truth note is `HKCU\...\Personalize\AppsUseLightTheme` (D-06), analogous to `IAutostartConfigurator`'s Run-key-existence note.

**Zero-Windows-reference constraint:** `RigToggle.Core.csproj` (read directly) has a structural comment enforcing this — `IThemeProvider`/`AppTheme` must be pure C# (interface + enum), no `Microsoft.Win32`, no P/Invoke, no `UseWindowsForms`. Same constraint `IAutostartConfigurator` already satisfies.

---

### `src/RigToggle.Core/Models/AppTheme.cs` (model/enum, Core)

**Analog:** `src/RigToggle.Core/Models/ToggleStepOutcome.cs` (full file, 13 lines)

```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Outcome of a single toggle step (Monitor / Audio / App). NotAttempted covers steps
/// skipped because an earlier step in a stop-on-first-failure sequence (ToggleToRigMode,
/// D-04) already failed.
/// </summary>
public enum ToggleStepOutcome
{
    Succeeded,
    Failed,
    NotAttempted,
}
```
**Apply as:**
```csharp
namespace RigToggle.Core.Models;

public enum AppTheme { Light, Dark }
```
Same bare-enum + one-paragraph rationale-comment shape (no comment strictly needed here since `Light`/`Dark` are self-explanatory, but keep the file-header convention consistent with every other `Models/*.cs` file — a one-line summary is fine).

---

### `src/RigToggle.Windows/WindowsThemeProvider.cs` (service, Windows adapter)

**Analog:** `src/RigToggle.Windows/WindowsAutostartConfigurator.cs` (full file, 73 lines)

**Imports pattern** (lines 1-3):
```csharp
using System.Diagnostics;
using Microsoft.Win32;
using RigToggle.Core.Abstractions;

namespace RigToggle.Windows;
```

**HKCU registry-read pattern** (lines 25-29, `IsEnabled()`):
```csharp
public bool IsEnabled()
{
    using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
    return key?.GetValue(ValueName) is not null;
}
```
Apply directly to `ReadThemeFromRegistry()` — same `Registry.CurrentUser.OpenSubKey(..., writable: false)` idiom, same null-coalescing safety. Key path differs: `Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`, value name `AppsUseLightTheme` (D-06 — NOT `SystemUsesLightTheme`).

**Best-effort diagnostic logging pattern** (lines 58-71, kept verbatim as a template):
```csharp
// Best-effort diagnostic logging, matching WindowsAppController's convention --
// routed through Trace.WriteLine so RigToggle.App's TextWriterTraceListener
// persists it to the same opt-in debug.log. Never throws.
private static void Log(string message)
{
    try
    {
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] WindowsAutostartConfigurator: {message}");
    }
    catch
    {
        // Logging is diagnostic-only; never let it affect autostart behavior.
    }
}
```
Reuse this exact shape for `WindowsThemeProvider`, renamed in the trace prefix. Log at least: theme resolved at construction, and every time `ThemeChanged` actually fires (old → new).

**Class-level rationale-comment convention** (lines 7-19) — write an equivalent block explaining: why `AppsUseLightTheme` not `SystemUsesLightTheme` (D-06), why registry-read failures default to `AppTheme.Light` rather than throwing (matches this codebase's "never throw from a Load-time read" convention, same posture as `IsEnabled()`'s null-safe `?.` chain).

**Full target implementation** — RESEARCH.md Pattern 2 (verified against this codebase's conventions, already includes the `IDisposable`/`SystemEvents.UserPreferenceChanged` unsubscribe pattern that has no existing analog elsewhere in this repo since `WindowsAutostartConfigurator` is not `IDisposable`):
```csharp
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
            return raw is int i && i == 0 ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            return AppTheme.Light;
        }
    }

    public void Dispose() => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}
```

---

### `src/RigToggle.Windows/DwmTitleBar.cs` (utility, Windows P/Invoke façade)

**Analog:** `src/RigToggle.Windows/GlobalHotkey.cs` (full file, 39 lines)

**Full façade pattern to copy** (public wrapper over `internal NativeMethods`):
```csharp
namespace RigToggle.Windows;

/// <summary>
/// Public cross-assembly façade over the internal RegisterHotKey/UnregisterHotKey
/// P/Invoke in NativeMethods (TRIG-01). Exists because NativeMethods is `internal` to
/// RigToggle.Windows and RigToggle.App has no InternalsVisibleTo grant to this assembly
/// ... MainForm calls GlobalHotkey.*, never NativeMethods.* -- do not "simplify"
/// this away by re-exposing NativeMethods or adding an InternalsVisibleTo grant to
/// RigToggle.App; the public wrapper is the deliberate encapsulation boundary.
/// </summary>
public static class GlobalHotkey
{
    public const int WmHotkey = 0x0312;
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    public static bool Register(IntPtr hWnd, int id, uint fsModifiers, uint vk) =>
        NativeMethods.RegisterHotKey(hWnd, id, fsModifiers, vk);

    public static bool Unregister(IntPtr hWnd, int id) =>
        NativeMethods.UnregisterHotKey(hWnd, id);
}
```
**Apply as** (RESEARCH.md Pattern 4, verbatim target shape, same public-façade-over-internal-P/Invoke boundary):
```csharp
namespace RigToggle.Windows;

public static class DwmTitleBar
{
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_MAINWINDOW = 2; // D-02: standard Mica, NOT Mica Alt/Acrylic

    public static void ApplyRoundedCornersAndMica(IntPtr handle)
    {
        int corner = DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        int backdrop = DWMSBT_MAINWINDOW;
        NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
    }
}
```
Same rationale-comment convention as `GlobalHotkey.cs`'s header — explain why this façade exists (encapsulation boundary over `internal NativeMethods`) and why every call here is silently best-effort (D-07 — non-throwing posture, matches `RegisterHotKey`'s own bool-return convention).

---

### `src/RigToggle.Windows/NativeMethods.cs` (MODIFIED — additions only)

**Analog:** itself — existing `RegisterHotKey`/`UnregisterHotKey` block (lines 109-121)

**Existing convention to match** (P/Invoke declaration style, `internal` visibility, grouped comment above each related block):
```csharp
// TRIG-01 global-hotkey P/Invoke surface: registers/unregisters a system-wide hotkey
// on a caller-owned message-pump window handle (MainForm's, in this project). Kept
// internal like the rest of this class -- RigToggle.App has no InternalsVisibleTo
// grant to this assembly (see AssemblyInfo.cs), so it cannot call these directly.
// Exposed to RigToggle.App only through the public GlobalHotkey wrapper (GlobalHotkey.cs
// in this same namespace), never through a new InternalsVisibleTo grant.
[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
```
**Additions to append** (RESEARCH.md verified constants — `learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute`):
```csharp
internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;   // owned by Application.SetColorMode — do not call manually (Pitfall 1)
internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
internal const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

[DllImport("dwmapi.dll")]
internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
```
Note the class-level header comment at the top of `NativeMethods.cs` (lines 6-16) currently says "this is the project's only P/Invoke surface" and lists what it deliberately excludes (`FindWindow`/`FindWindowEx`) — update this comment when adding the DWM block so it doesn't go stale (it should now say "the project's only P/Invoke *surfaces*" or similarly acknowledge the new `dwmapi.dll` import alongside the existing `user32.dll` ones).

**Namespace/visibility note:** the P/Invoke import stays `internal` — do not grant `RigToggle.App` a new `InternalsVisibleTo`; `DwmTitleBar.cs`'s public façade is the only sanctioned cross-assembly entry point (mirrors the `GlobalHotkey`/`NativeMethods` split exactly).

---

### `src/RigToggle.App/ThemeApplier.cs` (NEW, utility — targeted control-recolor pass)

**No direct analog exists** — this is the first "static helper that mutates a Designer-generated form's controls" file in `RigToggle.App`. The closest structural precedent is `SettingsForm.cs`'s own imperative control-styling methods (`RenderHotkeyIdleDisplay`, `PopulateMonitorGrid`'s empty-state styling) — same "small private/static method per control-family, explicit `Color`/`SystemColors` assignment, no generic tree-walk" shape, just extracted into a shared class per RESEARCH.md's Recommended Project Structure and CONTEXT.md's discretion note ("research recommends `RigToggle.App`... consistent with the rule that WinForms composition code stays in the App layer").

**`RenderHotkeyIdleDisplay` as the closest in-file precedent** (`SettingsForm.cs` lines 130-144 — to be REPLACED, not copied, per the required fix):
```csharp
private void RenderHotkeyIdleDisplay()
{
    if (_pendingHotkeyModifiers is int modifiers && _pendingHotkeyKey is int key)
    {
        txtHotkey.Text = HotkeyFormatter.ToDisplayString(modifiers, key);
        txtHotkey.BackColor = SystemColors.Window;
        txtHotkey.ForeColor = SystemColors.WindowText;
    }
    else
    {
        txtHotkey.Text = "(No hotkey set — click to configure)";
        txtHotkey.BackColor = SystemColors.Window;
        txtHotkey.ForeColor = SystemColors.GrayText;
    }
}
```
**Critical finding (RESEARCH.md, verified via direct source read):** this method, plus `TxtHotkey_MouseDown` (lines 150-156) and `TxtHotkey_KeyDown` (lines 228-234), hardcode `System.Drawing.SystemColors` — a color system that does NOT follow `Application.SetColorMode`. Replace all three `SystemColors.*` assignments with theme-aware values sourced from `ThemeApplier`/`IThemeProvider.CurrentTheme`, using the exact literal palette specified in `12-UI-SPEC.md`'s Color section (dark: `#2D2D30` idle/configured, `#A0A0A0` unconfigured text, `#005A9E` recording background, `#FFFFFF` recording text; light: keep existing `SystemColors.*` values unchanged).

**`DataGridView` theming target** (RESEARCH.md Pattern 5 code example, verified against `dgvMonitors`'s actual 3-column shape in `SettingsForm.Designer.cs`):
```csharp
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
Target control: `dgvMonitors` (declared `SettingsForm.Designer.cs` line 430, 3 columns `colMonitorName`/`colDisable`/`colEnable`, confirmed `dotnet/winforms#11893` gap for exactly this mixed text+checkbox shape).

**Error handling / non-throwing posture:** every `ThemeApplier` method must be safe to call repeatedly (idempotent, matching `LoadTrayIconsIfNeeded`'s/`ApplyTrayVisibility`'s guard-and-reapply convention in `MainForm.cs`) and must never throw — this is cosmetic-only code; an exception here must not crash the toggle/settings flow. No existing file in this codebase demonstrates a "recolor pass," so follow the general project convention (seen throughout `MainForm.cs`/`SettingsForm.cs`) of wrapping any OS/registry-adjacent call in `try/catch` and defaulting to leaving the control unchanged on failure.

---

### `src/RigToggle.App/Program.cs` (MODIFIED — composition root additions)

**Analog:** itself (existing `Main()`, full file read, 154 lines)

**Existing adapter-construction + injection pattern** (lines 87-97, 104-116) — the pattern every new Windows adapter in this project follows:
```csharp
var monitorController = new WindowsMonitorController();
var audioController = new WindowsAudioController();
var appController = new WindowsAppController();
var autostartConfigurator = new WindowsAutostartConfigurator();

var toggleService = new ToggleService(
    settingsStore,
    snapshotStore,
    monitorController,
    audioController,
    appController);
```
```csharp
MainForm mainForm = null!;
SettingsForm SettingsFormFactory() => new SettingsForm(monitorController, audioController, settingsStore, autostartConfigurator, mainForm.TryRegisterConfiguredHotkey, mainForm.ApplyTrayVisibility);

mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory);
```
**Apply as:** construct `var themeProvider = new WindowsThemeProvider();` alongside the other adapters (same block, same "composition root, nobody else `new`s a concrete adapter" rule — Anti-Pattern 2 referenced in `MainForm.cs`'s own header comment), then thread it into `MainForm`'s constructor and the `SettingsFormFactory`/`MonitorConfirmDialog` construction sites exactly like `autostartConfigurator` is threaded today.

**`Application.SetColorMode` placement — first line of `Main()`, per RESEARCH.md Pattern 3 (verified against this exact file's line numbers):**
```csharp
[STAThread]
static void Main(string[] args)
{
    System.Windows.Forms.Application.SetColorMode(System.Windows.Forms.SystemColorMode.System);

    ApplicationConfiguration.Initialize();
    // ... existing settings/store/adapter construction, unchanged ...
```
Must run before line 38 (`ApplicationConfiguration.Initialize()`), before any `Form`/control is constructed (Pitfall 1 — double-DWM-attribute-set flash if this ordering is violated).

**Unconditional-priming-before-either-`Application.Run`-branch pattern** (lines 118-129, the exact precedent D-08 requires theme application to mirror):
```csharp
// Pitfall 6: prime the tray icon/menu BEFORE either Run branch — under
// --tray the form's own Load event never fires since the form is never
// shown, so tray state must not depend on it.
mainForm.InitializeTrayState();

mainForm.RegisterHotkeyAtStartup();
```
Any Mica/rounded-corner DWM call for `MainForm` must run from inside (or right after) `InitializeTrayState()` — NOT from a new standalone call sequenced only in the visible-startup branch — since `Handle` is already forced into existence by the existing `RegisterHotkeyAtStartup()` → `TryRegisterConfiguredHotkey()` → `GlobalHotkey.Unregister(Handle, ...)` read (confirmed via direct source read, RESEARCH.md Pattern 3).

---

### `src/RigToggle.App/MainForm.cs` (MODIFIED — ctor param, theme-change wiring)

**Analog:** itself (existing constructor injection + `InitializeTrayState`/hotkey lifecycle patterns, lines 42-96, 600-666)

**Constructor injection pattern to extend** (lines 42-54):
```csharp
public MainForm(
    ToggleOrchestrator orchestrator,
    ISettingsStore settingsStore,
    IMonitorController monitorController,
    Func<SettingsForm> settingsFormFactory)
{
    _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
    _settingsFormFactory = settingsFormFactory ?? throw new ArgumentNullException(nameof(settingsFormFactory));

    InitializeComponent();
}
```
Add `IThemeProvider themeProvider` as a new constructor parameter, same null-guard convention, store as `_themeProvider`, subscribe `_themeProvider.ThemeChanged += OnThemeChanged;` after `InitializeComponent()`.

**"Runs regardless of startup path" unconditional-priming pattern** (lines 91-96, `InitializeTrayState()` — the exact D-08 precedent):
```csharp
public void InitializeTrayState()
{
    LoadTrayIconsIfNeeded();
    RefreshUi();
    ApplyTrayVisibility();
}
```
Add a call to a new method (e.g. `ApplyDwmChrome()`, calling `DwmTitleBar.ApplyRoundedCornersAndMica(Handle)`) inside this same method — do NOT put it in `OnLoad`/`OnShown` (Pitfall 5 — `--tray` never fires those).

**Idempotent-reapply convention** (`ApplyTrayVisibility`, lines 129-151) — the theme-change handler (`OnThemeChanged`) should follow the same "safe to call repeatedly, re-derives everything from current state" shape rather than tracking incremental deltas.

**Best-effort try/catch-around-registry-or-OS-call convention** (repeated throughout, e.g. lines 131-139, 390-398, 432-440):
```csharp
AppSettings settings;
try
{
    settings = _settingsStore.Load();
}
catch
{
    settings = new AppSettings();
}
```
Apply the same shape anywhere `OnThemeChanged` touches the DWM P/Invoke or `ThemeApplier` (never let a theming failure propagate).

**Cross-thread marshaling — no existing analog in this codebase** (this project has no prior `InvokeRequired`/`BeginInvoke` usage; `SystemEvents.UserPreferenceChanged` may fire off the UI thread per RESEARCH.md Assumption A3). Use the standard WinForms pattern:
```csharp
private void OnThemeChanged(object? sender, EventArgs e)
{
    if (InvokeRequired)
    {
        BeginInvoke(new Action(() => OnThemeChanged(sender, e)));
        return;
    }
    // ... re-apply SetColorMode + DWM Mica/corner + ThemeApplier passes + Refresh() ...
}
```

---

### `src/RigToggle.App/MainForm.Designer.cs` (MODIFIED — `FlatStyle.System`)

**Analog:** itself (existing `btnToggle`/`btnSettings` declarations, lines 70-85)
```csharp
// btnToggle
this.btnToggle.Text = "Switch to Rig Mode";
this.btnToggle.Location = new System.Drawing.Point(16, 60);
this.btnToggle.Size = new System.Drawing.Size(288, 40);
this.btnToggle.Name = "btnToggle";
this.btnToggle.Click += new System.EventHandler(this.BtnToggle_Click);
```
Add `this.btnToggle.FlatStyle = System.Windows.Forms.FlatStyle.System;` (and same for `btnSettings`) inside each control's property-assignment block, immediately after `.Name`/before the event-handler wiring line, matching this file's existing per-control property-then-event ordering convention. Per D-08/UI-SPEC's "Scope Notes," use `FlatStyle.System`, never `.Flat` (`dotnet/winforms#13897`).

---

### `src/RigToggle.App/SettingsForm.cs` (MODIFIED — ctor param, `txtHotkey` fix, subscribe/unsubscribe)

**Analog:** itself (existing multi-delegate constructor injection, lines 51-81)
```csharp
public SettingsForm(IMonitorController monitorController, IAudioController audioController, ISettingsStore settingsStore, IAutostartConfigurator autostartConfigurator, Func<bool> tryRegisterConfiguredHotkey, Action applyTrayVisibility)
{
    _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
    _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
    _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    _autostartConfigurator = autostartConfigurator ?? throw new ArgumentNullException(nameof(autostartConfigurator));
    _tryRegisterConfiguredHotkey = tryRegisterConfiguredHotkey ?? throw new ArgumentNullException(nameof(tryRegisterConfiguredHotkey));
    _applyTrayVisibility = applyTrayVisibility ?? throw new ArgumentNullException(nameof(applyTrayVisibility));

    InitializeComponent();
    ...
}
```
Add `IThemeProvider themeProvider` following the exact same null-guard-then-store pattern, positioned alongside `autostartConfigurator` (both are Windows-adapter-backed Core interfaces injected the same way).

**Subscribe-on-construct/unsubscribe-on-close** — no existing analog in this file (every current event subscription in the constructor, e.g. lines 68-80, is wired once for the form's lifetime and never explicitly unsubscribed, because none of them reference an object that outlives the form). `IThemeProvider.ThemeChanged` is different: `WindowsThemeProvider` is a long-lived singleton constructed once in `Program.cs` and outlives every transient `SettingsForm` instance (confirmed fresh-per-open via `Program.cs`'s `SettingsFormFactory`/`MainForm.OpenSettingsDialog`'s `using var settingsForm = ...` pattern, RESEARCH.md Open Question 1). Subscribe in the constructor; unsubscribe in a `FormClosed` handler:
```csharp
this.FormClosed += (_, _) => _themeProvider.ThemeChanged -= OnThemeChanged;
```
This is a REQUIRED addition, not a copy of an existing pattern — flagged explicitly in RESEARCH.md/CONTEXT.md as a pitfall ("a transient dialog leaking a subscription to a `WindowsThemeProvider.ThemeChanged` event").

**`txtHotkey` `SystemColors.*` replacement — three exact call sites to modify** (already excerpted in full under `ThemeApplier.cs` above): `RenderHotkeyIdleDisplay()` (lines 130-144), `TxtHotkey_MouseDown` (lines 150-156), `TxtHotkey_KeyDown` (lines 228-234, specifically lines 232-233's `txtHotkey.BackColor = SystemColors.Window; txtHotkey.ForeColor = SystemColors.WindowText;`). Replace each `SystemColors.*` reference with a call into `ThemeApplier`/`_themeProvider.CurrentTheme`-driven `Color` values per the UI-SPEC palette table.

---

### `src/RigToggle.App/SettingsForm.Designer.cs` (MODIFIED — `GroupBox`→`Panel` x3, `FlatStyle.System`)

**Analog:** itself — existing `GroupBox` declaration/child-reparenting pattern (lines 90-98, `grpMonitor`):
```csharp
// grpMonitor
this.grpMonitor.Location = new System.Drawing.Point(12, 12);
this.grpMonitor.Size = new System.Drawing.Size(396, 234);
this.grpMonitor.TabStop = false;
this.grpMonitor.Text = "Monitor";
this.grpMonitor.Controls.Add(this.lblMonitorExplain);
this.grpMonitor.Controls.Add(this.dgvMonitors);
this.grpMonitor.Controls.Add(this.lblMonitorWarning);
```
Repeated identically for `grpAudioDevices` (lines 163-174, 6 children) and `grpAppPath` (lines 227-238, 3 children + `DragEnter`/`DragDrop` handlers attached directly to the `GroupBox` — note these drag handlers must move to the replacement `Panel`, not be dropped).

**Refactor target (THEME-05, per UI-SPEC Spacing section — preserve exact bounding box):** replace `System.Windows.Forms.GroupBox` field declarations with `System.Windows.Forms.Panel` + a new `System.Windows.Forms.Label` per group (caption), same `Location`/`Size` values, same `Controls.Add(...)` child list, same event-handler wiring (`AppPath_DragEnter`/`AppPath_DragDrop` move from `grpAppPath` to the new `pnlAppPath`). Caption `Label` positioned at the ~9px inset the `GroupBox`'s native caption currently renders at (UI-SPEC explicit pixel-parity requirement — not a new spacing token). Set `Panel.BorderStyle = FixedSingle` (UI-SPEC Color section, both themes; dark-mode fallback override via `Panel.Paint` only if rig verification shows insufficient contrast).

**`FlatStyle.System` targets in this file:** `btnBrowse`, `btnSaveSettings`, `btnDiscardChanges` — same one-line addition pattern as `MainForm.Designer.cs` above.

---

### `src/RigToggle.App/MonitorConfirmDialog.cs` (MODIFIED — first injected dependency)

**Analog:** `src/RigToggle.App/SettingsForm.cs` constructor (injected-dependency null-guard convention, see excerpt above) — `MonitorConfirmDialog`'s own current constructor (full file, lines 19-30) takes plain data only:
```csharp
public MonitorConfirmDialog(IReadOnlyList<string> disableNames, IReadOnlyList<string> enableNames)
{
    InitializeComponent();

    var clauses = new List<string>();
    if (disableNames.Count > 0) clauses.Add($"disable {FormatNames(disableNames)}");
    if (enableNames.Count > 0) clauses.Add($"enable {FormatNames(enableNames)}");
    lblMessage.Text = $"This will {string.Join(" and ", clauses)}. Continue?";

    this.AcceptButton = btnContinue;
    this.CancelButton = btnCancel;
}
```
Add `IThemeProvider themeProvider` as a new parameter using `SettingsForm`'s null-guard-then-store idiom (`_themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));`), update `MainForm.BtnToggle_Click`'s `using var confirmDialog = new MonitorConfirmDialog(disableNames, enableNames);` call site (and `Program.cs`, if `MainForm` constructs the dependency, or thread it through from `MainForm`'s own injected `_themeProvider`). Per UI-SPEC scope note: this dialog IS in scope. Per RESEARCH.md Open Question 2 note: this is the first time this form's constructor gains an interface dependency — document that precedent break in a comment, matching this codebase's established practice of flagging "first of its kind" changes.

---

### `src/RigToggle.App/MonitorConfirmDialog.Designer.cs` (MODIFIED — `FlatStyle.System`)

**Analog:** itself — `btnContinue`/`btnCancel` declarations (not read in full this pass; same one-line `FlatStyle.System` addition pattern as `MainForm.Designer.cs`/`SettingsForm.Designer.cs` above — apply identically).

---

### `src/RigToggle.Tests/Doubles/FakeThemeProvider.cs` (NEW, if Core-level unit tests are added)

**Analog:** `src/RigToggle.Tests/Doubles/FakeControllers.cs` (excerpt, lines 1-27 — hand-written recording-fake convention, no mocking framework):
```csharp
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Tests.Doubles;

/// <summary>
/// Hand-written recording fakes for the three mutation-adapter interfaces (no mocking
/// framework — matches the project's no-unnecessary-dependency posture). ...
/// </summary>
public sealed class FakeMonitorController : IMonitorController
{
    private readonly List<string> _callLog;
    ...
    public FakeMonitorController(List<string> callLog, ...) { ... }
    public IReadOnlyList<MonitorInfo> GetActiveMonitors()
    {
        _callLog.Add("monitor.GetActiveMonitors");
        return new List<MonitorInfo> { new(...) };
    }
    ...
}
```
Apply the same shape for a `FakeThemeProvider : IThemeProvider` with a settable `CurrentTheme` and a public method to raise `ThemeChanged` on demand for tests — only needed if the planner scopes in Core-level unit tests for anything that consumes `IThemeProvider` (e.g. a future Core-side theme-dependent helper). `WindowsThemeProvider` itself is NOT unit-testable (rig-only, same as every other `Windows*` adapter — RESEARCH.md Recommended Project Structure note).

---

## Shared Patterns

### Composition-root-only adapter construction (Anti-Pattern 2)
**Source:** `src/RigToggle.App/Program.cs` lines 87-97, 104-116; reinforced in `src/RigToggle.App/MainForm.cs` header doc-comment (lines 9-22)
**Apply to:** `WindowsThemeProvider` construction — exactly one `new WindowsThemeProvider()` call, in `Program.cs`, injected everywhere else. Never `new` it inside `MainForm`/`SettingsForm`/`MonitorConfirmDialog`.

### Unconditional priming before either `Application.Run` branch (D-08 / Pitfall 5/6)
**Source:** `src/RigToggle.App/Program.cs` lines 118-129 (`mainForm.InitializeTrayState()` / `mainForm.RegisterHotkeyAtStartup()`); `src/RigToggle.App/MainForm.cs` lines 79-96 (`InitializeTrayState()` doc-comment)
**Apply to:** every theme-application call for `MainForm` (DWM Mica/corner-preference) — must live inside `InitializeTrayState()` or an equivalent unconditional call site reached by BOTH the `--tray` and visible startup paths, never in `OnLoad`/`OnShown`.

### Best-effort, never-throw wrapping around OS/registry calls
**Source:** repeated throughout `MainForm.cs` (lines 131-139, 390-398, 432-440), `WindowsAutostartConfigurator.cs` (lines 58-71 `Log()`), `Program.cs` (lines 46-57, 68-83)
```csharp
AppSettings settings;
try { settings = _settingsStore.Load(); }
catch { settings = new AppSettings(); }
```
**Apply to:** all DWM P/Invoke calls (already non-throwing by HRESULT-return design per D-07), `WindowsThemeProvider`'s registry read, and any `ThemeApplier` method — none of this phase's new code may throw out to a caller in a way that blocks startup, toggle, or Settings-save.

### `internal NativeMethods` + `public` façade encapsulation boundary
**Source:** `src/RigToggle.Windows/NativeMethods.cs` header comment (lines 6-16, 109-114); `src/RigToggle.Windows/GlobalHotkey.cs` full file
**Apply to:** `DwmTitleBar.cs` must be `public static class`, wrapping `internal static class NativeMethods`'s new `DwmSetWindowAttribute` — do not grant `RigToggle.App` a new `InternalsVisibleTo`.

### Idempotent, safe-to-call-repeatedly refresh methods
**Source:** `src/RigToggle.App/MainForm.cs` `ApplyTrayVisibility()` (lines 129-151), `LoadTrayIconsIfNeeded()` (lines 159-177, guarded on already-loaded state)
**Apply to:** `OnThemeChanged` handlers on `MainForm`/`SettingsForm`/`MonitorConfirmDialog`, and every `ThemeApplier` method — re-derive the full themed state from current `IThemeProvider.CurrentTheme` each call rather than tracking incremental deltas, since they may be invoked multiple times (startup + every live flip).

### XML-doc rationale-comment convention ("why", not "what")
**Source:** pervasive throughout this codebase (every method excerpted above has one) — e.g. `WindowsAutostartConfigurator.cs` lines 7-19, `MainForm.cs` lines 80-90, 416-425
**Apply to:** D-01 (why `MessageBox` stays native), D-03 (why the `ToolStrip` stale-color bug isn't worked around), D-08 (why theme application can't live in `Form_Load`) — each of these must get an explicit comment at its relevant code site per CONTEXT.md's own instruction, matching this established convention.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `src/RigToggle.App/ThemeApplier.cs` | utility | transform | No prior "shared control-recolor helper" file exists in this codebase — closest precedent is inline imperative styling code inside `SettingsForm.cs` itself (see Pattern Assignments above), not a dedicated reusable class. Use RESEARCH.md's verified code examples (Pattern 5, `ThemeMonitorGrid`) as the primary source instead of a codebase analog. |
| Cross-thread `SystemEvents` marshaling (`InvokeRequired`/`BeginInvoke`) | N/A — cross-cutting concern, not a file | event-driven | This project has never previously needed UI-thread marshaling (no prior background-thread-to-UI event exists) — no analog anywhere in the repo. Use the standard, well-documented WinForms `InvokeRequired`/`BeginInvoke` idiom (shown under `MainForm.cs` Pattern Assignments above) rather than a codebase precedent. |

## Metadata

**Analog search scope:** `src/RigToggle.Core/Abstractions/`, `src/RigToggle.Core/Models/`, `src/RigToggle.Windows/` (all adapter + P/Invoke files), `src/RigToggle.App/` (all forms/Designer files), `src/RigToggle.Tests/Doubles/`
**Files scanned:** `IAutostartConfigurator.cs`, `WindowsAutostartConfigurator.cs`, `NativeMethods.cs`, `GlobalHotkey.cs`, `Program.cs`, `MainForm.cs`, `MainForm.Designer.cs`, `SettingsForm.cs`, `SettingsForm.Designer.cs`, `MonitorConfirmDialog.cs`, `AudioRoleState.cs`, `ToggleStepOutcome.cs`, `FakeControllers.cs`, `RigToggle.Core.csproj`, `RigToggle.Windows.csproj`, `RigToggle.App.csproj` (16 files read directly)
**Pattern extraction date:** 2026-08-02
