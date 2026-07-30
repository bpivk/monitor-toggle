# Phase 8: Tray Residency, Autostart & Toast Notification - Pattern Map

**Mapped:** 2026-07-30
**Files analyzed:** 12 (7 modified, 5 new; icon assets excluded from code-pattern analysis)
**Analogs found:** 12 / 12

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|---------------|
| `src/RigToggle.Core/ToggleResultFormatter.cs` | utility | transform | `src/RigToggle.App/MainForm.cs` (`FormatChecklist`, being relocated verbatim) | exact (literal relocation) |
| `src/RigToggle.Core/StartupArgs.cs` | utility | transform | `src/RigToggle.Core/ToggleService.cs` (`IsFullyConfigured`, private static pure predicate) | role-match |
| `src/RigToggle.Core/Abstractions/IAutostartConfigurator.cs` | model/interface | CRUD (contract only) | `src/RigToggle.Core/Abstractions/IAppController.cs` | exact |
| `src/RigToggle.Windows/WindowsAutostartConfigurator.cs` | service (adapter) | CRUD | `src/RigToggle.Windows/WindowsAppController.cs` | role-match |
| `src/RigToggle.App/MainForm.cs` | controller (UI) | event-driven + request-response | itself (existing file, extended) | exact (same file) |
| `src/RigToggle.App/MainForm.Designer.cs` | component/config | — (declarative wiring) | itself (existing file, extended) | exact (same file) |
| `src/RigToggle.App/SettingsForm.cs` | controller (UI) | CRUD | itself (existing file, `chkEnableDebugLogging` block) | exact (same file, direct precedent) |
| `src/RigToggle.App/SettingsForm.Designer.cs` | component/config | — (declarative wiring) | itself (`chkEnableDebugLogging` designer block, lines 254-264) | exact (same file) |
| `src/RigToggle.App/Program.cs` | config (composition root) | request-response (startup branch) | itself (existing file, extended) | exact (same file) |
| `src/RigToggle.Tests/ToggleResultFormatterTests.cs` | test | transform | `src/RigToggle.Tests/ToggleOrchestratorTests.cs` | role-match |
| `src/RigToggle.Tests/StartupArgsTests.cs` | test | transform | `src/RigToggle.Tests/ToggleOrchestratorTests.cs` | role-match |
| `src/RigToggle.Windows.Tests/WindowsAutostartConfiguratorTests.cs` (optional) | test | CRUD (real registry) | `src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs` | role-match |

## Pattern Assignments

### `src/RigToggle.Core/ToggleResultFormatter.cs` (utility, transform — NEW)

**Analog:** `src/RigToggle.App/MainForm.cs` lines 187-202 (the exact method being relocated)

**Imports pattern** (top of `RigToggle.Core` files — see `ToggleOrchestrator.cs` lines 1-4):
```csharp
using RigToggle.Core.Models;

namespace RigToggle.Core;
```
Note the file-scoped namespace style (`namespace RigToggle.Core;`) used everywhere in `RigToggle.Core` (e.g. `ToggleOrchestrator.cs` line 4, `Abstractions/IAppController.cs` line 1) — do NOT use the block-scoped `namespace X { }` style that `RigToggle.App`'s forms use.

**Core transform pattern to relocate verbatim** (`MainForm.cs` lines 191-202):
```csharp
private static string FormatChecklist(ToggleResult result)
{
    return string.Join(
        Environment.NewLine,
        result.Steps.Select(step => step.Outcome switch
        {
            ToggleStepOutcome.Succeeded => $"{step.StepName}: OK",
            ToggleStepOutcome.Failed => $"{step.StepName}: FAILED ({step.Reason})",
            ToggleStepOutcome.NotAttempted => $"{step.StepName}: not attempted",
            _ => $"{step.StepName}: unknown",
        }));
}
```
Widen from `private static` to `public static` on a new `public static class ToggleResultFormatter` in `RigToggle.Core`. Add the companion `FormatModeTitle(bool isInRigMode)` (D-09) next to it, matching the existing `btnToggle.Text`/`lblMode.Text` phrasing convention from `MainForm.RefreshUi()` (lines 48-53):
```csharp
private void RefreshUi()
{
    bool isInRigMode = _orchestrator.IsInRigMode();
    lblMode.Text = isInRigMode ? "Mode: Rig" : "Mode: Normal";
    btnToggle.Text = isInRigMode ? "Switch to Normal Mode" : "Switch to Rig Mode";
}
```

**Callers to update:**
- `MainForm.BtnToggle_Click` (line 142): `$"...\n\n{FormatChecklist(result)}"` becomes `$"...\n\n{ToggleResultFormatter.FormatChecklist(result)}"`; delete the now-relocated private method from `MainForm.cs`.
- New tray toggle handler calls the same `ToggleResultFormatter.FormatChecklist(result)` for `ShowBalloonTip` text, and `ToggleResultFormatter.FormatModeTitle(...)` for the balloon title — this is the entire point of the relocation (D-09, avoid duplicating wording).

**XML-doc rationale-comment convention to follow** (matches every `RigToggle.Core` type, e.g. `ToggleOrchestrator.cs` lines 6-30, `ToggleStepResult.cs` lines 3-8): explain *why* the type exists and *why* it moved, not just what it does.

---

### `src/RigToggle.Core/StartupArgs.cs` (utility, transform — NEW)

**Analog:** `src/RigToggle.Core/ToggleService.cs` line 195-205, `IsFullyConfigured` — a small private static pure predicate over primitive/collection state, same "tiny testable pure helper in Core" shape:
```csharp
public bool IsSettingsConfigured() => IsFullyConfigured(_settingsStore.Load());

private static bool IsFullyConfigured(Models.AppSettings settings) =>
    (settings.MonitorsToDisable?.Count > 0 || settings.MonitorsToEnable?.Count > 0)
    && !string.IsNullOrEmpty(settings.NormalAudioDeviceId)
    && !string.IsNullOrEmpty(settings.RigAudioDeviceId)
    && !string.IsNullOrEmpty(settings.CompanionAppPath);
```

**Pattern to write** (per RESEARCH.md Pattern 4, defensive per Security Domain V5 — must not throw on null/empty/malformed `args`):
```csharp
namespace RigToggle.Core;

public static class StartupArgs
{
    private const string TrayFlag = "--tray";

    public static bool ShouldStartHidden(string[] args) =>
        args.Contains(TrayFlag, StringComparer.OrdinalIgnoreCase);
}
```
`args.Contains` on a `string[]` (even empty) never throws — matches the "don't crash the composition root over malformed input" threat mitigation in RESEARCH.md's Security Domain table.

---

### `src/RigToggle.Core/Abstractions/IAutostartConfigurator.cs` (interface — NEW)

**Analog:** `src/RigToggle.Core/Abstractions/IAppController.cs` (full file, 15 lines) — exact structural match: a small interface with an XML-doc header naming the concrete `RigToggle.Windows` implementer and phase provenance.
```csharp
namespace RigToggle.Core.Abstractions;

/// <summary>
/// Companion-app running-detection and launch/focus/minimize contract. Implemented
/// by RigToggle.Windows.WindowsAppController. IsRunning is real starting Phase 2
/// (D-07); LaunchOrFocus/MinimizeIfRunning are no-op stubs until Phase 3
/// (02-RESEARCH.md Pattern 1).
/// </summary>
public interface IAppController
{
    bool IsRunning(string companionAppPath);
    void LaunchOrFocus(string companionAppPath);
    void MinimizeIfRunning(string companionAppPath);
}
```
Write the new interface with the same shape/doc convention:
```csharp
namespace RigToggle.Core.Abstractions;

/// <summary>
/// HKCU "start with Windows" registration contract (TRAY-02). Implemented by
/// RigToggle.Windows.WindowsAutostartConfigurator, this phase. Registry existence is
/// the source of truth (no mirrored AppSettings boolean) — see Shared Patterns below.
/// </summary>
public interface IAutostartConfigurator
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
```

---

### `src/RigToggle.Windows/WindowsAutostartConfigurator.cs` (service/adapter, CRUD — NEW)

**Analog:** `src/RigToggle.Windows/WindowsAppController.cs` (full file) — establishes this codebase's "sealed class implementing a Core interface, one real API call per method, best-effort/defensive posture around BCL/Win32 calls, `Trace.WriteLine`-based diagnostic logging convention" shape.

**Imports pattern** (`WindowsAppController.cs` lines 1-4):
```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using RigToggle.Core.Abstractions;

namespace RigToggle.Windows;
```
For the new file, only `RigToggle.Core.Abstractions` and `Microsoft.Win32` are needed (no P/Invoke — `Microsoft.Win32.Registry` is BCL on `net10.0-windows`, no new `PackageReference`).

**Class declaration pattern** (`WindowsAppController.cs` line 39):
```csharp
public sealed class WindowsAppController : IAppController
```
becomes:
```csharp
public sealed class WindowsAutostartConfigurator : IAutostartConfigurator
```

**Core CRUD pattern (from RESEARCH.md Pattern 3, verified against this codebase's guard-clause and doc-comment conventions seen in `WindowsAppController.IsRunning`, lines 41-54, and `LaunchOrFocus`, lines 73-93):**
```csharp
public sealed class WindowsAutostartConfigurator : IAutostartConfigurator
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "RigToggle";

    public bool IsEnabled()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    public void Enable()
    {
        // Environment.ProcessPath (NOT Assembly.Location — RESEARCH.md Pitfall 5) is the
        // correct way to resolve this app's own exe path when running from inside a
        // PublishSingleFile=true bundle (RigToggle.App.csproj).
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            throw new InvalidOperationException("Could not resolve the running executable's path.");
        }

        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, $"\"{exePath}\" --tray");
    }

    public void Disable()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
```

**Error handling pattern:** unlike `WindowsAppController.LaunchOrFocus`, which lets `Process.Start` exceptions propagate to `ToggleService`'s step wrapper, there is no equivalent step-wrapper for Settings-form actions — `SettingsForm`'s existing convention (see `PopulateMonitorGrid`/`PopulateAudioPickers`, `SettingsForm.cs` lines 78-86, 249-256) is "catch broadly, degrade to empty/false state, never crash Settings open/save." Follow that same posture in `SettingsForm`'s new checkbox Load/Save wiring around `IAutostartConfigurator` calls (see Shared Patterns below) rather than adding new try/catch inside the adapter itself — `Enable()`/`Disable()` should throw on genuine failure (matches `WindowsAppController`'s "let it propagate" posture for actual mutation calls), and the UI layer decides how to degrade.

**Logging pattern (optional, matches `WindowsAppController.Log`, lines 102-112):**
```csharp
private static void Log(string message)
{
    try
    {
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] WindowsAutostartConfigurator: {message}");
    }
    catch
    {
        // Logging is diagnostic-only; never let it affect behavior.
    }
}
```

---

### `src/RigToggle.App/MainForm.cs` (controller, event-driven + request-response — MODIFIED)

**Analog:** itself — extend the existing constructor/DI pattern and `BtnToggle_Click`/`RefreshUi` call shapes already in the file.

**Constructor/DI pattern to extend** (lines 17-36):
```csharp
public partial class MainForm : Form
{
    private readonly ToggleOrchestrator _orchestrator;
    private readonly ISettingsStore _settingsStore;
    private readonly IMonitorController _monitorController;
    private readonly Func<SettingsForm> _settingsFormFactory;

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
Add `IAutostartConfigurator` as a new constructor parameter only if `MainForm` itself needs it (per CONTEXT.md, autostart config lives in `SettingsForm`, not `MainForm` — `MainForm` likely does NOT need this dependency; `Program.cs` wires `IAutostartConfigurator` directly into the `SettingsForm` factory instead, matching how `IAudioController` is already wired only into `SettingsForm`, never `MainForm`).

**Toggle call pattern to replicate for the tray menu handler** (lines 55-146, condensed to the shape the new handler must copy):
```csharp
private void BtnToggle_Click(object? sender, EventArgs e)
{
    ToggleResult? result = null;
    try
    {
        if (_orchestrator.IsInRigMode())
        {
            result = _orchestrator.ToggleToNormalMode();
        }
        else
        {
            // ... WR-01 settings-configured guard, DISPLAY-07 confirm dialog ...
            result = _orchestrator.ToggleToRigMode();
        }

        RefreshUi();

        if (result is not null && !result.Success)
        {
            MessageBox.Show(this, $"...\n\n{FormatChecklist(result)}", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    catch (ToggleInProgressException ex)
    {
        MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show(this, $"Something went wrong while toggling:\n\n{ex.GetType().Name}: {ex.Message}\n\nTry again, or check Settings.", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
```
The new tray-menu toggle handler (`TrayToggleMenuItem_Click`) must call the **same** `_orchestrator.ToggleToRigMode()`/`ToggleToNormalMode()` pair (skip the WR-01/DISPLAY-07 GUI-only confirmation dialogs — CONTEXT.md doesn't call for them on the tray path, and `MonitorConfirmDialog`/`MessageBox` prompts are GUI-affordances inappropriate for a background tray trigger) then, per D-08/D-09, format via `ToggleResultFormatter` and call `notifyIcon.ShowBalloonTip(...)` instead of `MessageBox.Show(...)`. Keep the same `try/catch (ToggleInProgressException)` guard shape — the tray path is the second-ever caller Phase 7 built the guard for.

**`RefreshUi` extension point** (lines 44-53) — add tray icon/tooltip sync here, matching the existing `lblMode`/`btnToggle.Text` update style:
```csharp
private void RefreshUi()
{
    bool isInRigMode = _orchestrator.IsInRigMode();
    lblMode.Text = isInRigMode ? "Mode: Rig" : "Mode: Normal";
    btnToggle.Text = isInRigMode ? "Switch to Normal Mode" : "Switch to Rig Mode";
    // NEW: notifyIcon.Icon = isInRigMode ? _rigIcon : _normalIcon;
    // NEW: notifyIcon.Text = isInRigMode ? "Rig Toggle — Rig Mode" : "Rig Toggle — Normal Mode";
    // NEW: trayToggleMenuItem.Text = btnToggle.Text; // D-04: one shared source of truth
}
```
Per RESEARCH.md Pitfall 6, also add an explicit `InitializeTrayState()` (or fold into the constructor after `InitializeComponent()`) that calls this same icon/tooltip-setting logic unconditionally — `OnLoad` (line 38-42) is NOT sufficient because it never fires under `--tray` startup.

**New `FormClosing` handler** (RESEARCH.md Pattern 1 — no existing analog in this file since no close-interception exists yet; write per the research-provided pattern, follow the file's existing XML-doc-rationale-comment density):
```csharp
private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
{
    if (e.CloseReason == CloseReason.UserClosing)
    {
        e.Cancel = true;
        Hide();
        return;
    }
    notifyIcon.Visible = false;
}
```

**Exit menu handler:**
```csharp
private void TrayExitMenuItem_Click(object? sender, EventArgs e)
{
    notifyIcon.Visible = false;
    Application.Exit();
}
```

**Left-click restore handler (Pitfall 2 — must be `MouseClick`, not `Click`):**
```csharp
private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
{
    if (e.Button == MouseButtons.Left)
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }
}
```

---

### `src/RigToggle.App/MainForm.Designer.cs` (component/config — MODIFIED)

**Analog:** itself — extend `InitializeComponent()` and the `components` field exactly as this file's existing `lblMode`/`btnToggle`/`btnSettings` declarations are built.

**`components` field — currently dead code, becomes load-bearing** (lines 8, 14-21):
```csharp
private System.ComponentModel.IContainer components = null;

protected override void Dispose(bool disposing)
{
    if (disposing && (components != null))
    {
        components.Dispose();
    }
    base.Dispose(disposing);
}
```
Change to `this.components = new System.ComponentModel.Container();` inside `InitializeComponent()` and construct `NotifyIcon` with it: `this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);` — this makes the existing (already-correct) `Dispose(bool)` block a genuine defensive backstop against the ghost-tray-icon bug (D-04), on top of the explicit `notifyIcon.Visible = false` in `FormClosing`/Exit.

**Existing control-declaration pattern to replicate** (lines 29-89, one control = one comment-delimited block + a trailing private field declaration at the bottom):
```csharp
private void InitializeComponent()
{
    this.lblMode = new System.Windows.Forms.Label();
    this.btnToggle = new System.Windows.Forms.Button();
    this.btnSettings = new System.Windows.Forms.Button();

    this.SuspendLayout();

    //
    // btnToggle
    //
    this.btnToggle.Text = "Switch to Rig Mode";
    this.btnToggle.Location = new System.Drawing.Point(16, 60);
    this.btnToggle.Size = new System.Drawing.Size(288, 40);
    this.btnToggle.Name = "btnToggle";
    this.btnToggle.Click += new System.EventHandler(this.BtnToggle_Click);

    // ... MainForm-level properties, this.Controls.Add(...) calls ...

    this.ResumeLayout(false);
}

#endregion

private System.Windows.Forms.Label lblMode;
private System.Windows.Forms.Button btnToggle;
private System.Windows.Forms.Button btnSettings;
```
Add, following the identical block style: `notifyIcon` (`System.Windows.Forms.NotifyIcon`, constructed with `this.components`), `trayContextMenu` (`System.Windows.Forms.ContextMenuStrip`), three `ToolStripMenuItem`s (`trayToggleMenuItem`, `traySettingsMenuItem`, `trayExitMenuItem` — plus a `ToolStripSeparator` before Exit per the specified menu order), and wire `this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);` and `this.notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(this.NotifyIcon_MouseClick);` in the same `InitializeComponent()` method, at the `MainForm`-level event-wiring position (near where the file currently only wires child-control events, since `MainForm` itself has no existing event subscriptions to pattern-match against directly — closest precedent is the `btnToggle.Click += ...`/`btnSettings.Click += ...` lines 54, 63).

---

### `src/RigToggle.App/SettingsForm.cs` (controller, CRUD — MODIFIED)

**Analog:** itself — `chkEnableDebugLogging`'s exact Load/Save round trip is the direct, explicitly-named template (CONTEXT.md D-05, canonical_refs).

**Constructor DI pattern to extend** (lines 12-40):
```csharp
public partial class SettingsForm : Form
{
    private readonly IMonitorController _monitorController;
    private readonly IAudioController _audioController;
    private readonly ISettingsStore _settingsStore;

    public SettingsForm(IMonitorController monitorController, IAudioController audioController, ISettingsStore settingsStore)
    {
        _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
        _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));

        InitializeComponent();
        // ...
    }
```
Add `IAutostartConfigurator autostartConfigurator` as a fourth constructor parameter with the identical null-guard pattern, and update `Program.cs`'s `SettingsFormFactory` (see below) to pass it.

**Load pattern (`SettingsForm_Load`, lines 57-66) — checkbox read is the direct analog for D-05:**
```csharp
private void SettingsForm_Load(object? sender, EventArgs e)
{
    _settings = _settingsStore.Load();
    PopulateMonitorGrid();
    PopulateAudioPickers();
    PopulateAppPathField();
    chkEnableDebugLogging.Checked = _settings.EnableDebugLogging;
    ValidateSettingsForm();
}
```
Add, following the exact same one-line-per-field style — but reading from the registry via the injected interface, NOT from `_settings` (RESEARCH.md's explicit recommendation: registry existence is the source of truth, no mirrored `AppSettings` boolean, to avoid drift — see Shared Patterns below):
```csharp
chkStartWithWindows.Checked = _autostartConfigurator.IsEnabled();
```

**Save pattern (`BtnSaveSettings_Click`, lines 488-552) — `chkEnableDebugLogging.Checked` is read directly into the `AppSettings` POCO at line 545:**
```csharp
var settingsToSave = new AppSettings
{
    // ...
    EnableDebugLogging = chkEnableDebugLogging.Checked,
};

_settingsStore.Save(settingsToSave);
```
The new "Start with Windows" checkbox does NOT get a matching `AppSettings` field (per the registry-is-source-of-truth recommendation) — instead, add a direct side-effecting call alongside the `_settingsStore.Save(...)` call:
```csharp
if (chkStartWithWindows.Checked)
{
    _autostartConfigurator.Enable();
}
else
{
    _autostartConfigurator.Disable();
}
```
Match this codebase's existing "no separate Save step beyond the existing Save button" requirement (D-05) by placing this directly in `BtnSaveSettings_Click`, in the same method, right alongside (before or after) `_settingsStore.Save(settingsToSave);` — never a second dialog/button.

---

### `src/RigToggle.App/SettingsForm.Designer.cs` (component/config — MODIFIED)

**Analog:** itself — the `chkEnableDebugLogging` declaration block is the literal, explicitly-named template (CONTEXT.md canonical_refs: "lines ~60-64, 545").

**Field declaration** (line 54, and bottom-of-file field list line 354):
```csharp
this.chkEnableDebugLogging = new System.Windows.Forms.CheckBox();
// ...
private System.Windows.Forms.CheckBox chkEnableDebugLogging;
```

**Full designer block to replicate** (lines 256-264):
```csharp
//
// chkEnableDebugLogging
//
this.chkEnableDebugLogging.Text = "Enable debug logging (writes to %LOCALAPPDATA%\\RigToggle\\debug.log)";
this.chkEnableDebugLogging.Location = new System.Drawing.Point(12, 484);
this.chkEnableDebugLogging.Size = new System.Drawing.Size(396, 40);
this.chkEnableDebugLogging.AutoSize = false;
this.chkEnableDebugLogging.Name = "chkEnableDebugLogging";
```
Add an identical block for `chkStartWithWindows` (text: "Start with Windows"), positioned adjacent to `chkEnableDebugLogging` in both the field-construction order and the `this.Controls.Add(...)` list (line 316: `this.Controls.Add(this.chkEnableDebugLogging);`) — append `this.Controls.Add(this.chkStartWithWindows);` right after it. Exact Y-coordinate placement (avoiding overlap with the existing 484pt-Y checkbox and any dialog resize) is a UI-SPEC concern, not a pattern-mapping one.

---

### `src/RigToggle.App/Program.cs` (composition root — MODIFIED)

**Analog:** itself — the existing three-controller construction pattern is the direct template for adding a fourth (`WindowsAutostartConfigurator`).

**Existing controller-construction + factory pattern** (lines 78-96):
```csharp
var monitorController = new WindowsMonitorController();
var audioController = new WindowsAudioController();
var appController = new WindowsAppController();

var toggleService = new ToggleService(settingsStore, snapshotStore, monitorController, audioController, appController);
var toggleOrchestrator = new ToggleOrchestrator(toggleService);

SettingsForm SettingsFormFactory() => new SettingsForm(monitorController, audioController, settingsStore);

var mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory);

Application.Run(mainForm);
```
Extend to:
```csharp
var monitorController = new WindowsMonitorController();
var audioController = new WindowsAudioController();
var appController = new WindowsAppController();
var autostartConfigurator = new WindowsAutostartConfigurator(); // NEW

var toggleService = new ToggleService(settingsStore, snapshotStore, monitorController, audioController, appController);
var toggleOrchestrator = new ToggleOrchestrator(toggleService);

SettingsForm SettingsFormFactory() => new SettingsForm(monitorController, audioController, settingsStore, autostartConfigurator); // extended

var mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory);
mainForm.InitializeTrayState(); // Pitfall 6 — must run before either Run() branch below

bool startHidden = StartupArgs.ShouldStartHidden(args); // NEW — RigToggle.Core, unit-testable
if (startHidden)
{
    Application.Run(new ApplicationContext(mainForm)); // D-06 fix — does NOT call Show()
}
else
{
    Application.Run(mainForm); // existing behavior, unchanged
}
```

**`Main` signature change** (line 24-25):
```csharp
[STAThread]
static void Main()
```
becomes:
```csharp
[STAThread]
static void Main(string[] args)
```
This is the one place in the file where the existing "composition root, never instantiates a form/adapter anywhere else" doc-comment convention (lines 14-23) should be extended with a note about the new `--tray`/`ApplicationContext` branch, matching the file's existing density of rationale comments (see the `EnableDebugLogging`-gated `Trace` listener block, lines 50-74, as the density benchmark).

---

### `src/RigToggle.Tests/ToggleResultFormatterTests.cs` and `StartupArgsTests.cs` (test — NEW)

**Analog:** `src/RigToggle.Tests/ToggleOrchestratorTests.cs` (imports, namespace, xunit convention, `AppSettings`/`ToggleResult` construction style)

**Imports/namespace pattern** (lines 1-9):
```csharp
using System.Threading;
using System.Threading.Tasks;
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using RigToggle.Tests.Doubles;
using Xunit;

namespace RigToggle.Tests;
```
For `ToggleResultFormatterTests`/`StartupArgsTests`, drop the unused `Doubles`/`Threading` imports (no fakes or concurrency needed — both are pure functions) and keep `using RigToggle.Core;`, `using RigToggle.Core.Models;`, `using Xunit;`, `namespace RigToggle.Tests;`.

**Test class shape** (lines 19-26, `IDisposable` pattern only needed if a temp file/fixture is involved — neither new test class needs it):
```csharp
public class ToggleOrchestratorTests : IDisposable
{
    // ... fixture setup in constructor ...
    public void Dispose() => File.Delete(ExistingCompanionAppPath);
```
`ToggleResultFormatterTests`/`StartupArgsTests` are plain `public class` (no `IDisposable`) with `[Fact]`/`[Theory]` methods constructing `ToggleResult`/`ToggleStepResult` records directly (see `ToggleResult.cs`/`ToggleStepResult.cs` record shapes above) or plain `string[]` arrays for `StartupArgs.ShouldStartHidden`.

---

### `src/RigToggle.Windows.Tests/WindowsAutostartConfiguratorTests.cs` (test, optional — NEW)

**Analog:** `src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs` and the project's own csproj-header rationale comment (RigToggle.Windows.Tests.csproj lines 5-10) explaining why this is a SEPARATE `net10.0-windows`-only project from `RigToggle.Tests`.

Per RESEARCH.md Open Question 2, this test would mutate a real `HKCU\...\Run` value — the planner should decide (rig-verified vs. CI-run) rather than assume; the referenced planner recommendation is to keep `WindowsAutostartConfigurator` a thin, obviously-correct wrapper and rely on rig-testing for its actual registry behavior, mirroring the existing convention that `RigToggle.App`'s forms have zero unit test coverage and are verified manually/on-rig instead.

## Shared Patterns

### Source-of-truth for a UI checkbox backed by external state (not `AppSettings`)
**Source:** `SettingsForm.PopulateAppPathField`/`PopulateAudioPickers` (lines 246-262, 306-328) — this codebase's existing convention of reading live external state (enumerated monitors/audio devices) fresh on every `SettingsForm_Load`, rather than trusting only what's cached in `_settings`.
**Apply to:** `chkStartWithWindows.Checked = _autostartConfigurator.IsEnabled()` on Load — read the registry directly, do not add an `AppSettings.StartWithWindows` mirror field. This is a locked recommendation from RESEARCH.md's Anti-Patterns section, reinforcing an already-established codebase convention (stale-detection pattern), not a new one.

### "UI never instantiates a concrete Windows adapter directly"
**Source:** `Program.cs` lines 78-96 (composition root owns all `new WindowsXController()` calls); `02-RESEARCH.md` Anti-Pattern 2 (referenced throughout `MainForm.cs`/`SettingsForm.cs` doc comments, e.g. `MainForm.cs` lines 9-11, `SettingsForm.cs` lines 9-10).
**Apply to:** `WindowsAutostartConfigurator` must be constructed only in `Program.cs` and injected into `SettingsForm` via its constructor — never `new`'d inside `SettingsForm.cs` or `MainForm.cs`.

### `CloseReason`-gated `FormClosing`, no custom boolean flag
**Source:** RESEARCH.md Pattern 1 / Anti-Patterns ("A custom `_isExiting` boolean... unnecessary").
**Apply to:** `MainForm.MainForm_FormClosing` — gate exclusively on `e.CloseReason == CloseReason.UserClosing`; `Application.Exit()` from the tray Exit item is a distinct `CloseReason.ApplicationExitCall` and needs no additional flag.

### Two static pre-made `.ico` resources, never a `Bitmap`-to-`Icon` conversion
**Source:** RESEARCH.md Pitfall 3 / Common Pitfalls — `Icon.FromHandle(bitmap.GetHicon())` leaks GDI handles.
**Apply to:** `MainForm`'s tray-icon-state logic must load two `EmbeddedResource` `.ico` files once at startup (`new Icon(assembly.GetManifestResourceStream(resourceName))`), swap only the already-constructed `Icon` references on mode change — never re-derive an `Icon` from a `Bitmap` per toggle.

### `Trace.WriteLine`-based best-effort diagnostic logging
**Source:** `WindowsAppController.Log` (lines 102-112), gated end-to-end by `Program.cs`'s `EnableDebugLogging`-conditioned `TextWriterTraceListener` (lines 59-74).
**Apply to:** Any new diagnostic logging in `WindowsAutostartConfigurator` or the new tray handlers should route through `Trace.WriteLine`, wrapped in a `try { } catch { }` that never lets a logging failure affect real behavior — do not introduce a new logging mechanism.

### XML-doc rationale comments explaining *why*, not just *what*
**Source:** Pervasive throughout `RigToggle.Core`/`RigToggle.Windows`/`RigToggle.App` (e.g. `ToggleOrchestrator.cs` lines 6-30, `WindowsAppController.cs` lines 8-38, `MainForm.cs` lines 8-16).
**Apply to:** All new files this phase — especially D-03's close-vs-minimize distinction and D-08's toast-always-fires-unconditionally rule (per CONTEXT.md's own explicit instruction to document these so a future reader doesn't "fix" either into unintended symmetry).

## No Analog Found

None. Every file in this phase's scope has at least a role-match analog in the existing codebase; several (the `chkEnableDebugLogging`/`FormatChecklist` relocations) have exact, explicitly-named analogs per CONTEXT.md's own canonical_refs.

## Metadata

**Analog search scope:** `src/RigToggle.App/` (MainForm.cs, MainForm.Designer.cs, SettingsForm.cs, SettingsForm.Designer.cs, Program.cs, MonitorConfirmDialog.cs), `src/RigToggle.Core/` (ToggleOrchestrator.cs, ToggleService.cs, Abstractions/*.cs, Models/*.cs), `src/RigToggle.Windows/` (WindowsAppController.cs, WindowsMonitorController.cs, WindowsAudioController.cs), `src/RigToggle.Tests/` (ToggleOrchestratorTests.cs, Doubles/*.cs), `src/RigToggle.Windows.Tests/` (WindowsMonitorControllerTests.cs), all `.csproj` files.
**Files scanned:** 20 source files + 5 project files read in full or targeted excerpt.
**Pattern extraction date:** 2026-07-30
