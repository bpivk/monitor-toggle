# Phase 17: Manual Monitor Panel & Shared Safety Guard - Pattern Map

**Mapped:** 2026-08-08
**Files analyzed:** 6 (new/modified)
**Analogs found:** 6 / 6

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/RigToggle.App/MonitorPanelForm.cs` | component (Form, code-behind) | CRUD (read grid + per-row enable/disable mutation) | `src/RigToggle.App/SettingsForm.cs` | role-match (Settings is modal/multi-section; panel is a single non-modal grid, but shares every idiom: theming, grid population, Tag-keyed rows, confirmation gate) |
| `src/RigToggle.App/MonitorPanelForm.Designer.cs` | component (Designer) | request-response (declarative control layout) | `src/RigToggle.App/SettingsForm.Designer.cs` (grid/column declarations) + `src/RigToggle.App/MainForm.Designer.cs` (form-level chrome: FixedDialog/CenterScreen/MinimizeBox) | role-match |
| `src/RigToggle.App/MonitorIdentifyOverlay.cs` | component (borderless Form) | transform (position/size → rendered overlay), event-driven (Timer auto-close) | No existing borderless/topmost overlay `Form` in this codebase — closest structural precedent is `MonitorConfirmDialog.cs` for the theming/lifecycle skeleton only, not for borderless/topmost/no-chrome behavior | no analog (see below) |
| `src/RigToggle.App/MainForm.cs` (modified) | controller/event-handler (existing Form) | request-response (new tray/button click → open panel) | itself — extend `OpenSettingsDialog`/`BtnSettings_Click`/`TraySettingsMenuItem_Click` pattern | exact (self-analog, same file) |
| `src/RigToggle.App/MainForm.Designer.cs` (modified) | component (Designer) | request-response | itself — extend `btnSettings`/`traySettingsMenuItem` declarations | exact (self-analog, same file) |
| `src/RigToggle.App/Program.cs` (modified) | config/composition-root | request-response (DI wiring, no runtime data flow) | itself — extend `SettingsFormFactory` pattern | exact (self-analog, same file) |

No changes to `RigToggle.Core` or `RigToggle.Windows` are expected — `IMonitorController`, `WindowsMonitorController`, `MonitorState`/`MonitorPathSnapshot`/`MonitorInfo` are consumed as-is (see RESEARCH.md "Recommended Project Structure").

## Pattern Assignments

### `src/RigToggle.App/MonitorPanelForm.cs` (component, CRUD)

**Primary analog:** `src/RigToggle.App/SettingsForm.cs`
**Secondary analogs:** `src/RigToggle.App/MainForm.cs` (tray-adjacent non-modal form lifecycle, live theme-follow), `src/RigToggle.App/MonitorConfirmDialog.cs` (confirmation-gate call shape), `src/RigToggle.Windows/WindowsThemeProvider.cs` (SystemEvents subscribe/unsubscribe idiom to mirror for `SystemEvents.DisplaySettingsChanged`)

**Imports pattern** (from `SettingsForm.cs:1-6`):
```csharp
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using RigToggle.Windows;
```
Add `using Microsoft.Win32;` for `SystemEvents.DisplaySettingsChanged` (PANEL-03), matching the fully-qualified `Microsoft.Win32.SystemEvents...` style already used inline in `MainForm.cs`/`SettingsForm.cs` OnThemeChanged bodies (both call `System.Windows.Forms.Application.SetColorMode` fully-qualified rather than importing `System.Windows.Forms` — follow the same "qualify BCL/System.Windows.Forms ambiguous members inline" convention already established, or add `using Microsoft.Win32;` at top since `SystemEvents` has no naming collision risk in this file).

**Constructor / DI pattern** (`SettingsForm.cs:73-123`, `MainForm.cs:45-63`):
```csharp
public SettingsForm(IMonitorController monitorController, IAudioController audioController, ISettingsStore settingsStore, IAutostartConfigurator autostartConfigurator, IThemeProvider themeProvider, Func<bool> tryRegisterConfiguredHotkey, Action applyTrayVisibility)
{
    _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
    ...
    InitializeComponent();

    _themeProvider.ThemeChanged += OnThemeChanged;
    this.FormClosed += (_, _) => _themeProvider.ThemeChanged -= OnThemeChanged;

    DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDarkTheme);
    ...
}
```
`MonitorPanelForm`'s constructor should take exactly `IMonitorController`, `ISettingsStore`, `IThemeProvider` (per RESEARCH.md Pattern 2 — no `ToggleService`/`ToggleOrchestrator`/`IAudioController`/`IAutostartConfigurator` dependency). Null-guard every param with `?? throw new ArgumentNullException(...)` exactly like both analogs.

**Live theme-follow pattern** (`SettingsForm.cs:139-183`, near-identical in `MainForm.cs:72-92`):
```csharp
private void OnThemeChanged(object? sender, EventArgs e)
{
    if (InvokeRequired)
    {
        BeginInvoke(new Action(() => OnThemeChanged(sender, e)));
        return;
    }

    try
    {
        System.Windows.Forms.Application.SetColorMode(System.Windows.Forms.SystemColorMode.System);
        DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDarkTheme);
        ThemeApplier.ThemeMonitorGrid(dgvMonitorPanel, IsDarkTheme);
        ThemeApplier.ThemeButton(btnIdentify, IsDarkTheme);
        // re-render status-dot bitmaps too (they encode dark/light via CreateStatusDot)
        Refresh();
    }
    catch
    {
        // Cosmetic-only -- a theming failure must never crash the panel.
    }
}

private bool IsDarkTheme => _themeProvider.CurrentTheme == AppTheme.Dark;
```

**Grid population pattern (PANEL-01)** — direct structural template, `SettingsForm.cs:364-438` (`PopulateMonitorGrid`):
```csharp
private void PopulateMonitorGrid()
{
    try
    {
        _allMonitors = _monitorController.GetAllMonitors();
    }
    catch (Exception)
    {
        _allMonitors = Array.Empty<MonitorInfo>();
    }

    dgvMonitorPanel.Rows.Clear();

    if (_allMonitors.Count == 0)
    {
        dgvMonitorPanel.Enabled = false;
        lblEmptyState.Text = "No monitors detected. Reconnect a display and reopen this panel.";
        lblEmptyState.Visible = true;
        return;
    }

    dgvMonitorPanel.Enabled = true;
    lblEmptyState.Visible = false;

    foreach (MonitorInfo monitor in _allMonitors)
    {
        string suffix = monitor.IsPrimary
            ? " (Primary)"
            : !monitor.IsActive
                ? " (currently OS-disabled)"
                : string.Empty;

        int rowIndex = dgvMonitorPanel.Rows.Add(
            CreateStatusDot(monitor.IsActive, IsDarkTheme),
            monitor.FriendlyName + suffix,
            monitor.IsActive ? "Disable" : "Enable"); // action button column label

        // Stable-identity precedent (06-PATTERNS.md Shared Patterns, reused every
        // grid in this app): key every row by DevicePath via Tag, NEVER by row index.
        dgvMonitorPanel.Rows[rowIndex].Tag = monitor.DevicePath;
    }
}
```
Status-dot bitmap helper — RESEARCH.md Pattern 3 (`17-RESEARCH.md:184-193`), exact literal colors also pinned in `17-UI-SPEC.md` Color section (`#2ECC71` active / `#C83C3C` inactive):
```csharp
private static Bitmap CreateStatusDot(bool isActive, bool isDark)
{
    var bmp = new Bitmap(12, 12);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    Color dotColor = isActive ? Color.FromArgb(46, 204, 113) : Color.FromArgb(200, 60, 60);
    using var brush = new SolidBrush(dotColor);
    g.FillEllipse(brush, 0, 0, 11, 11);
    return bmp;
}
```

**Row-action button click / commit pattern (PANEL-02, PANEL-04, DISPLAY-12)** — combine `DataGridView.CellClick` (button-column clicks commit immediately, unlike `SettingsForm`'s checkbox columns — 17-UI-SPEC.md Component Behavior Contract explicitly calls this out to avoid Pitfall 5) with `MainForm.BtnToggle_Click`'s confirmation-gate + try/catch shape (`MainForm.cs:337-395`):
```csharp
private void DgvMonitorPanel_CellClick(object? sender, DataGridViewCellEventArgs e)
{
    if (e.RowIndex < 0 || e.ColumnIndex != colAction.Index) return;

    DataGridViewRow row = dgvMonitorPanel.Rows[e.RowIndex];
    if (row.Tag is not string devicePath) return;

    MonitorInfo? monitor = _allMonitors.FirstOrDefault(m => m.DevicePath == devicePath);
    if (monitor is null) return;

    if (monitor.IsActive)
    {
        DisableMonitor(devicePath, monitor.FriendlyName);
    }
    else
    {
        EnableMonitor(devicePath);
    }
}

private void DisableMonitor(string devicePath, string friendlyName)
{
    var settings = _settingsStore.Load();
    if (!settings.SkipMonitorConfirmation)
    {
        using var confirmDialog = new MonitorConfirmDialog(
            disableNames: new[] { friendlyName },
            enableNames: Array.Empty<string>(),
            _themeProvider);
        if (confirmDialog.ShowDialog(this) != DialogResult.OK) return;
        if (confirmDialog.DontAskAgain)
        {
            settings.SkipMonitorConfirmation = true;
            _settingsStore.Save(settings);
        }
    }

    try
    {
        // DISPLAY-12: the exact same method both ToggleService.ToggleToRigMode()
        // and ToggleToNormalMode() already call
        // (src/RigToggle.Core/ToggleService.cs) -- no new pre-check is written here.
        _monitorController.DeactivateMonitors(new HashSet<string> { devicePath });
        PopulateMonitorGrid();
    }
    catch (InvalidOperationException ex)
    {
        // 17-UI-SPEC.md Copywriting Contract: reuse ex.Message verbatim, universal
        // "Rig Toggle" MessageBox title, Warning icon -- do not reword per call site.
        MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}

private void EnableMonitor(string devicePath)
{
    try
    {
        _monitorController.ActivateMonitors(new HashSet<string> { devicePath });
        PopulateMonitorGrid();
    }
    catch (Exception ex)
    {
        MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
```
**Critical constraint (RESEARCH.md Pattern 1/Anti-Patterns):** do NOT add a pre-check like `if (allMonitors.Count(m => m.IsActive) <= 1)` before calling `DeactivateMonitors` — the zero-survivors guard lives solely in `WindowsMonitorController.DeactivateMonitors` (`src/RigToggle.Windows/WindowsMonitorController.cs:295-308`, exact text: `"Cannot disable all configured monitors — at least one active display must remain."`). This is the one rule DISPLAY-12 exists to enforce.

**Live hotplug refresh pattern (PANEL-03)** — direct template from `src/RigToggle.Windows/WindowsThemeProvider.cs:44,51-71` (subscribe/unsubscribe idiom) + `MainForm.cs:72-92` (marshal-then-try/catch idiom):
```csharp
public MonitorPanelForm(IMonitorController monitorController, ISettingsStore settingsStore, IThemeProvider themeProvider)
{
    ...
    Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    this.FormClosed += (_, _) =>
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
}

private void OnDisplaySettingsChanged(object? sender, EventArgs e)
{
    if (InvokeRequired) { BeginInvoke(new Action(() => OnDisplaySettingsChanged(sender, e))); return; }
    try { PopulateMonitorGrid(); }
    catch { /* best-effort refresh -- never crash on a hotplug notification */ }
}
```
Also add a `Dispose(bool)` backstop unsubscribe in the Designer partial (see `MonitorConfirmDialog.Designer.cs:14-34` WR-01 pattern below) for both `ThemeChanged` and `DisplaySettingsChanged`.

**Identify action (PANEL-05)** — combines `WindowsMonitorController.CaptureState()` (`src/RigToggle.Windows/WindowsMonitorController.cs:162-186`) with the new `MonitorIdentifyOverlay` (see its own section below):
```csharp
private void BtnIdentify_Click(object? sender, EventArgs e)
{
    MonitorState state = _monitorController.CaptureState(); // active paths only
    int number = 1;
    foreach (MonitorPathSnapshot snap in state.Paths)
    {
        var overlay = new MonitorIdentifyOverlay(snap, number);
        overlay.Show();
        number++;
    }
}
```

### `src/RigToggle.App/MonitorPanelForm.Designer.cs` (component)

**Analog 1 (grid columns):** `src/RigToggle.App/SettingsForm.Designer.cs:48-60,143-184` — `DataGridView` setup:
```csharp
this.dgvMonitors.AllowUserToAddRows = false;
this.dgvMonitors.AllowUserToDeleteRows = false;
this.dgvMonitors.AllowUserToResizeRows = false;
this.dgvMonitors.AllowUserToResizeColumns = false;
this.dgvMonitors.RowHeadersVisible = false;
this.dgvMonitors.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
this.dgvMonitors.MultiSelect = false;
this.dgvMonitors.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
this.dgvMonitors.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
this.dgvMonitors.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
    this.colMonitorName, this.colDisable, this.colEnable});
```
For `MonitorPanelForm`, replace the two checkbox columns with one `DataGridViewImageColumn` (status dot, 32px per 17-UI-SPEC.md) + one Fill `DataGridViewTextBoxColumn` (name) + one `DataGridViewButtonColumn` (80px, action label per row):
```csharp
this.colStatus = new System.Windows.Forms.DataGridViewImageColumn();
this.colStatus.Width = 32;
this.colStatus.HeaderText = string.Empty;
this.colMonitorName = new System.Windows.Forms.DataGridViewTextBoxColumn();
this.colMonitorName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
this.colAction = new System.Windows.Forms.DataGridViewButtonColumn();
this.colAction.Width = 80;
this.colAction.UseColumnTextForButtonValue = false; // per-row Text set via Rows.Add
this.dgvMonitorPanel.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DgvMonitorPanel_CellClick);
```

**Analog 2 (form-level chrome):** `src/RigToggle.App/MainForm.Designer.cs:164-183` for `FixedDialog`/`CenterScreen`/`MinimizeBox` shape:
```csharp
this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
this.MaximizeBox = false;
this.MinimizeBox = true;
this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
this.Text = "Rig Toggle — Monitors"; // 17-UI-SPEC.md Copywriting Contract
this.ShowInTaskbar = true; // 17-UI-SPEC.md: must be findable when MainForm is minimized to tray
```
Per 17-UI-SPEC.md Spacing Scale: outer margin 16px (md), grid-to-Identify-button gap 24px (lg), status-icon column internal padding 4px (xs).

**Dispose(bool) backstop pattern** (`src/RigToggle.App/MonitorConfirmDialog.Designer.cs:14-34`, WR-01):
```csharp
protected override void Dispose(bool disposing)
{
    if (disposing && (components != null))
    {
        components.Dispose();
    }

    if (disposing)
    {
        _themeProvider.ThemeChanged -= OnThemeChanged;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
    base.Dispose(disposing);
}
```

### `src/RigToggle.App/MonitorIdentifyOverlay.cs` (new component — no direct in-codebase analog)

**No analog found.** This codebase has zero prior borderless/topmost/`ShowInTaskbar=false` `Form` precedent — every existing `Form` (`MainForm`, `SettingsForm`, `MonitorConfirmDialog`) is a standard chrome'd dialog/window. Use RESEARCH.md's own cited example verbatim as the starting shape (`17-RESEARCH.md:265-297`, Pattern 6), constructed from a `MonitorPathSnapshot` + display number:
```csharp
// Source: 17-RESEARCH.md Pattern 6 (PANEL-05) -- no existing codebase precedent for
// this specific shape; combines CaptureState()'s CCD-sourced position/size (never
// Screen.AllScreens, per Pitfall 2) with a fresh borderless-Form idiom.
public sealed class MonitorIdentifyOverlay : Form
{
    public MonitorIdentifyOverlay(MonitorPathSnapshot snap, int number)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(snap.PositionX, snap.PositionY);
        Size = new Size(snap.ResolutionWidth, snap.ResolutionHeight);
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Black; // 17-UI-SPEC.md Color: Identify overlay background #000000

        var lbl = new Label
        {
            Text = number.ToString(),
            Font = new Font("Segoe UI", 120, FontStyle.Bold), // 17-UI-SPEC.md Typography: Display role, the one deliberate exception
            ForeColor = Color.White, // 17-UI-SPEC.md: text #FFFFFF
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(lbl);

        var timer = new System.Windows.Forms.Timer { Interval = 2500 }; // 17-UI-SPEC.md: auto-close 2500ms
        timer.Tick += (_, _) => { timer.Stop(); Close(); };
        Shown += (_, _) => timer.Start();
    }
}
```
Note (Pitfall 3, DPI): flag on-screen accuracy (correct monitor, correct full coverage, no offset on mixed-DPI multi-monitor setups) as a required rig-verification checkpoint — `RigToggle.App.csproj` has no `<ApplicationHighDpiMode>` override, so raw CCD pixel coordinates may need verification against the SDK-default DPI mode.

### `src/RigToggle.App/MainForm.cs` (modified — new tray/panel entry point)

**Analog:** itself, extending the existing `OpenSettingsDialog`/`BtnSettings_Click`/`TraySettingsMenuItem_Click` triad (`MainForm.cs:436-458,567`).

**Pattern to copy** — a `Monitors...` button and tray entry mirroring Settings exactly, but launching a non-modal `Show()` (not `ShowDialog()`), per RESEARCH.md Assumption A1 and 17-UI-SPEC.md Component Behavior Contract:
```csharp
private void BtnMonitors_Click(object? sender, EventArgs e) => OpenMonitorPanel();
private void TrayMonitorsMenuItem_Click(object? sender, EventArgs e) => OpenMonitorPanel();

// Non-modal (RESEARCH.md A1/17-UI-SPEC.md): Show(), never ShowDialog() -- required
// for PANEL-03's "live update while panel is open" without blocking MainForm. Unlike
// OpenSettingsDialog, does NOT unregister the global hotkey for the panel's lifetime
// (the panel is deliberately independent, ad-hoc, and the hotkey's toggle path is
// unrelated to panel actions -- see RESEARCH.md Pitfall 4 Open Question before adding
// any serialization here).
private MonitorPanelForm? _monitorPanelForm;
private void OpenMonitorPanel()
{
    if (_monitorPanelForm is null || _monitorPanelForm.IsDisposed)
    {
        _monitorPanelForm = _monitorPanelFormFactory();
    }
    _monitorPanelForm.Show();
    _monitorPanelForm.Activate();
}
```
(The `_monitorPanelForm is null || IsDisposed` re-create guard is new — no exact precedent, since `SettingsForm`/`MonitorConfirmDialog` are always fresh-per-open via `using var ... ShowDialog()`. This is the one place `MonitorPanelForm`'s non-modal, potentially-long-lived nature diverges from the existing modal-dialog idiom; keep a single cached instance so a second "Monitors" click focuses the existing window rather than opening a duplicate.)

### `src/RigToggle.App/MainForm.Designer.cs` (modified)

**Analog:** itself — `btnSettings` (`MainForm.Designer.cs:92-99`) and `traySettingsMenuItem` (`:111-115`) declarations, copied verbatim with position/copy changes per 17-UI-SPEC.md:
```csharp
//
// btnMonitors
//
this.btnMonitors.Text = "Monitors…";
this.btnMonitors.Location = new System.Drawing.Point(16, 148); // 17-UI-SPEC.md: below btnSettings, 8px gap (sm token, pixel-matches existing btnToggle->btnSettings gap)
this.btnMonitors.Size = new System.Drawing.Size(288, 32);
this.btnMonitors.Name = "btnMonitors";
this.btnMonitors.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
this.btnMonitors.Click += new System.EventHandler(this.BtnMonitors_Click);

//
// trayMonitorsMenuItem
//
this.trayMonitorsMenuItem.Text = "Monitors"; // no ellipsis, matches traySettingsMenuItem convention
this.trayMonitorsMenuItem.Name = "trayMonitorsMenuItem";
this.trayMonitorsMenuItem.Click += new System.EventHandler(this.TrayMonitorsMenuItem_Click);
```
Insert `trayMonitorsMenuItem` into `trayContextMenu.Items.AddRange(...)` immediately after `traySettingsMenuItem`, before `traySeparator` (`MainForm.Designer.cs:142-146`), per 17-UI-SPEC.md's explicit final order: Switch mode → Settings → **Monitors** → separator → Exit. `ClientSize` likely needs growing from `(320, 200)` to accommodate the new button row — follow the existing 16px-margin/40px-row-height rhythm already visible in the `lblMode`(16,16)/`btnToggle`(16,60)/`btnSettings`(16,108) vertical stack.
Also call `ThemeApplier.ThemeButton(btnMonitors, IsDark)` everywhere `btnToggle`/`btnSettings` are themed (`OnThemeChanged` at `MainForm.cs:84-85`, `InitializeTrayState` at `:168-169`).

### `src/RigToggle.App/Program.cs` (modified — composition root wiring)

**Analog:** itself — the existing `SettingsFormFactory` local function pattern (`Program.cs:150,152`):
```csharp
SettingsForm SettingsFormFactory() => new SettingsForm(monitorController, audioController, settingsStore, autostartConfigurator, themeProvider, mainForm.TryRegisterConfiguredHotkey, mainForm.ApplyTrayVisibility);
```
Add a sibling factory, threaded into `MainForm`'s constructor exactly like `SettingsFormFactory` is:
```csharp
MonitorPanelForm MonitorPanelFormFactory() => new MonitorPanelForm(monitorController, settingsStore, themeProvider);

mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory, MonitorPanelFormFactory, themeProvider);
```
Note `monitorController`/`settingsStore`/`themeProvider` are already-constructed composition-root locals (`Program.cs:115,52,124`) — no new adapter construction needed, confirming RESEARCH.md's "no changes to RigToggle.Core or RigToggle.Windows" claim.

---

## Shared Patterns

### Zero-Survivors Safety Guard (DISPLAY-12)
**Source:** `src/RigToggle.Windows/WindowsMonitorController.cs:295-308` (existing, unmodified)
**Apply to:** `MonitorPanelForm`'s disable action only (enable actions can never trigger this).
```csharp
if (survivors.Length == 0)
{
    throw new InvalidOperationException(
        "Cannot disable all configured monitors — at least one active display must remain.");
}
```
The panel must call `IMonitorController.DeactivateMonitors(IReadOnlySet<string>)` directly and catch `InvalidOperationException` — never pre-check monitor counts client-side. This is the single most load-bearing rule for this phase.

### Confirmation Gate Reuse (PANEL-04)
**Source:** `src/RigToggle.App/MainForm.cs:337-377` (`BtnToggle_Click`'s `SkipMonitorConfirmation` block) + `src/RigToggle.App/MonitorConfirmDialog.cs` (constructor takes `disableNames`/`enableNames`/`IThemeProvider`)
**Apply to:** `MonitorPanelForm`'s Disable action only, single-element `disableNames` list, `enableNames: Array.Empty<string>()`.
```csharp
var settings = _settingsStore.Load();
if (!settings.SkipMonitorConfirmation)
{
    using var confirmDialog = new MonitorConfirmDialog(
        disableNames: new[] { monitorFriendlyName },
        enableNames: Array.Empty<string>(),
        _themeProvider);
    if (confirmDialog.ShowDialog(this) != DialogResult.OK) return;
    if (confirmDialog.DontAskAgain)
    {
        settings.SkipMonitorConfirmation = true;
        _settingsStore.Save(settings);
    }
}
```

### Live Theme-Follow (marshal-then-try/catch)
**Source:** `src/RigToggle.App/MainForm.cs:72-92`, `src/RigToggle.App/SettingsForm.cs:139-183`, `src/RigToggle.App/MonitorConfirmDialog.cs:69-89` — identical shape in all three existing forms.
**Apply to:** `MonitorPanelForm.OnThemeChanged` and `MonitorIdentifyOverlay` (if the overlay is themed at all — 17-UI-SPEC.md treats the overlay's black/white palette as a fixed, non-theme-following exception, so this pattern applies to `MonitorPanelForm` only, not the overlay).
```csharp
private void OnThemeChanged(object? sender, EventArgs e)
{
    if (InvokeRequired) { BeginInvoke(new Action(() => OnThemeChanged(sender, e))); return; }
    try { /* re-theme controls */ Refresh(); }
    catch { /* cosmetic-only -- must never crash */ }
}
```

### Error/MessageBox Formatting
**Source:** every `MessageBox.Show` call site in `src/RigToggle.App/MainForm.cs` (e.g. `:301-306,321-326,389-394,408-413,427-432`)
**Apply to:** every error surfaced from `MonitorPanelForm`'s row actions.
```csharp
MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
```
Title is always the literal `"Rig Toggle"` — never a bespoke per-feature title (17-UI-SPEC.md Copywriting Contract confirms this explicitly for the DISPLAY-12 rejection case).

### DataGridView Theming
**Source:** `src/RigToggle.App/ThemeApplier.cs:31-49` (`ThemeMonitorGrid`), `:124-139` (`ThemeButton`)
**Apply to:** `dgvMonitorPanel` and `btnIdentify`/row action buttons in `MonitorPanelForm` — reuse verbatim, do not invent new colors (17-UI-SPEC.md Color section is explicit that dark-mode literals must match `ThemeApplier`'s existing values, e.g. `Color.FromArgb(0, 90, 158)` for accent/selection).

### Tag-Keyed Row Identity
**Source:** `src/RigToggle.App/SettingsForm.cs:405-423,474-479` (`dgvMonitors.Rows[rowIndex].Tag = monitor.DevicePath`)
**Apply to:** `dgvMonitorPanel` rows in `MonitorPanelForm` — every row action (enable/disable/identify) must resolve the target monitor via `row.Tag as string` (the `DevicePath`), never via row index or display-name string matching.

### Stable Instance / Composition-Root Wiring
**Source:** `src/RigToggle.App/Program.cs:150,152` (`SettingsFormFactory` local function pattern)
**Apply to:** `MonitorPanelFormFactory`, threaded into `MainForm`'s constructor the same way.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `src/RigToggle.App/MonitorIdentifyOverlay.cs` | component (borderless Form) | transform + event-driven (Timer auto-close) | This codebase has zero prior borderless/topmost/`ShowInTaskbar=false` `Form` — every existing form is a standard chrome'd window/dialog. RESEARCH.md's own Pattern 6 code example (cited above, `17-RESEARCH.md:265-297`) is the closest thing to an analog and should be used near-verbatim as the starting implementation. `MonitorConfirmDialog.cs` supplies only the theming/lifecycle *skeleton* (constructor/Dispose shape), not the borderless/topmost/no-taskbar behavior itself. |

## Metadata

**Analog search scope:** `src/RigToggle.App/` (all `.cs`/`.Designer.cs` files), `src/RigToggle.Windows/` (`WindowsMonitorController.cs`, `WindowsThemeProvider.cs`, `DwmTitleBar.cs`), `src/RigToggle.Core/` (`Abstractions/IMonitorController.cs`, `Models/MonitorPathSnapshot.cs`, `Models/MonitorInfo.cs`, `Models/MonitorState.cs`, `ToggleService.cs`, `ToggleOrchestrator.cs`)
**Files scanned:** `MainForm.cs`, `MainForm.Designer.cs`, `SettingsForm.cs`, `SettingsForm.Designer.cs`, `MonitorConfirmDialog.cs`, `MonitorConfirmDialog.Designer.cs`, `ThemeApplier.cs`, `Program.cs`, `WindowsMonitorController.cs`, `WindowsThemeProvider.cs`, `IMonitorController.cs`, `MonitorPathSnapshot.cs`, `ToggleOrchestrator.cs` (13 files read directly)
**Pattern extraction date:** 2026-08-08
