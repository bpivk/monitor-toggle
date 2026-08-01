# Phase 11: Configurable Tray Close/Minimize Behavior - Pattern Map

**Mapped:** 2026-08-01
**Files analyzed:** 3 (all modified, no new files)
**Analogs found:** 3 / 3 (all self-referential — this phase revises existing files in place, using sibling code in the same files as the pattern)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.Core/Models/AppSettings.cs` | model | CRUD (settings persistence) | same file — `SkipMonitorConfirmation`/`EnableDebugLogging` bool fields (lines 27-28) | exact |
| `src/RigToggle.App/MainForm.cs` (`MainForm_FormClosing`, new minimize handler, new derived-visibility helper) | controller (WinForms code-behind, event-driven) | event-driven | same file — `MainForm_FormClosing` (lines 314-327), `NotifyIcon_MouseClick` (335-343), `TryRegisterConfiguredHotkey`/`RegisterHotkeyAtStartup` (helper-method + XML-doc-rationale pattern) | exact |
| `src/RigToggle.App/SettingsForm.cs` (two new checkboxes) + `SettingsForm.Designer.cs` (control declarations) | component (WinForms code-behind, request-response Load/Save round-trip) | CRUD (Load populates UI from `AppSettings`, Save writes UI back to `AppSettings`) | same file — `chkEnableDebugLogging` (Load line 88, Save line 721, Designer lines 268-274) is the **direct-AppSettings-round-trip** analog; `chkStartWithWindows` (Load 104-114, Save 740-776, Designer 312-327) is the **layout/positioning + "same section" grouping** analog only | exact (split: two distinct sub-patterns from two existing checkboxes) |

**Correction to CONTEXT.md's stated precedent:** CONTEXT.md (D-03, D-06, Reusable Assets) describes the target pattern as "plain `CheckBox`, read/written directly to/from `AppSettings` on Load/Save, no separate confirmation step" and names `chkStartWithWindows` as that exact template. Having read the actual code, `chkStartWithWindows` does **not** round-trip through `AppSettings` — it reads/writes via `_autostartConfigurator.IsEnabled()/Enable()/Disable()` (a registry-backed service), wrapped in try/catch with an inline `errAutostart`/`lblAutostartWarning` failure path (D-05, "Start with Windows" has no `AppSettings` mirror field by design). The checkbox that actually matches "plain, direct `AppSettings` field round-trip, no separate service, no failure handling" is `chkEnableDebugLogging`. Use `chkEnableDebugLogging` as the **behavioral** template (Load/Save wiring) for both new checkboxes, and `chkStartWithWindows` only for its **visual/layout** precedent (same-section placement, spacing, plain unwrapped `CheckBox` control with no `GroupBox` — these three checkboxes sit directly on the form, not inside a group box).

## Pattern Assignments

### `src/RigToggle.Core/Models/AppSettings.cs` (model, CRUD)

**Analog:** same file, existing `bool` fields

**Core pattern** (lines 16-31, full class — new fields are simple additions):
```csharp
public sealed class AppSettings
{
    public string? MonitorDevicePath { get; set; }
    public string? MonitorFriendlyName { get; set; }   // display-cache only, not used for matching
    public List<string>? MonitorsToDisable { get; set; }
    public List<string>? MonitorsToEnable { get; set; }
    public string? NormalAudioDeviceId { get; set; }
    public string? NormalAudioDeviceName { get; set; }
    public string? RigAudioDeviceId { get; set; }
    public string? RigAudioDeviceName { get; set; }
    public string? CompanionAppPath { get; set; }
    public bool SkipMonitorConfirmation { get; set; }
    public bool EnableDebugLogging { get; set; }
    public int? HotkeyModifiers { get; set; }
    public int? HotkeyKey { get; set; }
}
```
Add `CloseMinimizesToTray`/`MinimizeToTray` (or planner-chosen names) as two more `public bool ... { get; set; }` fields, same style as `SkipMonitorConfirmation`/`EnableDebugLogging` — no `[JsonPropertyName]` attribute, no default-value initializer (C# `bool` already defaults to `false`, matching D-02/D-05's required default-off behavior on upgrade with zero extra code). Update the class XML-doc summary (lines 3-15) to mention the two new fields' semantics, following the existing convention of documenting each field group's meaning inline in the class doc rather than per-property (there are no per-property `///` comments on any existing field — do not introduce that inconsistency).

---

### `src/RigToggle.App/MainForm.cs` (controller, event-driven)

**Analog:** same file, three existing regions

**Close-handler pattern to make conditional** (lines 301-327):
```csharp
/// <summary>
/// TRAY-01/D-03: only the window's own Close (X button, Alt+F4, or a plain
/// this.Close() call) is intercepted and redirected to hide-to-tray —
/// CloseReason.UserClosing is the specific, documented enum value raised for
/// exactly that case (08-RESEARCH.md Pattern 1). ...
/// </summary>
private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
{
    if (e.CloseReason == CloseReason.UserClosing)
    {
        e.Cancel = true;
        Hide();
        return;
    }

    // ApplicationExitCall (tray Exit), WindowsShutDown, TaskManagerClosing, etc.
    // T-08-GHOST: belt-and-suspenders ghost-icon prevention alongside the
    // explicit notifyIcon.Visible = false already set in TrayExitMenuItem_Click.
    notifyIcon.Visible = false;
}
```
D-01 makes the `e.CloseReason == CloseReason.UserClosing` branch's `Hide()` conditional on `AppSettings.CloseMinimizesToTray` (settings must be loaded via `_settingsStore.Load()`, mirroring how `BtnToggle_Click` already does `var settings = _settingsStore.Load();` at line 178 for the same purpose). When the flag is false, do not cancel/hide — let the close proceed exactly like the existing `else` fall-through, including the `notifyIcon.Visible = false` ghost-icon guard.

**Left-click-restore pattern (unaffected, reachability-gated only)** (lines 329-343):
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
No code change required here per D-09 — only reachable when `notifyIcon.Visible` is true, which is exactly the derived-visibility invariant D-08 establishes elsewhere.

**Helper-method + XML-doc-rationale pattern to follow for the new minimize interception and the new derived-visibility helper** (lines 481-514, `TryRegisterConfiguredHotkey`/`UnregisterConfiguredHotkey`):
```csharp
/// <summary>
/// TRIG-01/D-04: (re)registers the configured global hotkey on this window's
/// handle, unregister-first so it is safe to call repeatedly -- ...
/// </summary>
public bool TryRegisterConfiguredHotkey()
{
    GlobalHotkey.Unregister(Handle, GlobalHotkeyId);
    _hotkeyRegistered = false;

    var settings = _settingsStore.Load();
    if (settings.HotkeyModifiers is not int modifiers || settings.HotkeyKey is not int key)
    {
        return true; // nothing configured -- nothing to register, not a failure
    }
    ...
}

/// <summary>
/// TRIG-01/D-07: unregisters the global hotkey if currently registered; no-op
/// otherwise, so callers ... never need to track registration state themselves.
/// </summary>
public void UnregisterConfiguredHotkey()
{
    if (_hotkeyRegistered)
    {
        GlobalHotkey.Unregister(Handle, GlobalHotkeyId);
        _hotkeyRegistered = false;
    }
}
```
Model the new "send to tray" shared helper (Reusable Assets note: both Close-to-tray and Minimize-to-tray must call one shared method, not duplicate `Hide()`) and the D-08 derived-visibility helper (e.g. `RefreshTrayIconVisibility()`) on this shape: small `public`/`private` method on `MainForm`, settings loaded via `_settingsStore.Load()` at the top, XML-doc explaining the *why* referencing the relevant D-numbers, called from multiple sites (constructor/`InitializeTrayState()`, `OpenSettingsDialog()`'s post-Save point per Integration Points, and the new minimize handler).

**Startup wiring precedent for where derived-visibility must also run** (lines 91-95, `InitializeTrayState`):
```csharp
public void InitializeTrayState()
{
    LoadTrayIconsIfNeeded();
    RefreshUi();
}
```
D-08 requires the same derived rule applied at startup — add the new visibility-recompute call here (or inside `RefreshUi()`) alongside the existing calls, matching the "always idempotent, safe to call repeatedly" convention already documented on this method.

**Minimize interception:** no existing analog in this file (Phase 8 deliberately left minimize as standard OS behavior — see D-05/08-CONTEXT.md D-03). CONTEXT.md's Claude's Discretion section suggests a `Resize`/`SizeChanged` handler checking `WindowState == FormWindowState.Minimized`; wire it the same way `FormClosing` is wired in `MainForm.Designer.cs` (`this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);`, line 149) — add a matching `this.Resize += new System.EventHandler(this.MainForm_Resize);` (or `SizeChanged`) line in the same Designer.cs region, and a handler in `MainForm.cs` that calls the same shared "send to tray" helper as the close path when `AppSettings.MinimizeToTray` is true.

**Constructor injection note:** `MainForm`'s constructor (lines 42-54) already takes `ISettingsStore _settingsStore` — no new constructor parameter is needed for the new fields; both new handlers can call `_settingsStore.Load()` exactly like `MainForm_FormClosing`'s sibling `BtnToggle_Click` already does.

---

### `src/RigToggle.App/SettingsForm.cs` + `SettingsForm.Designer.cs` (component, CRUD Load/Save)

**Analog A — direct `AppSettings` round-trip (behavior template):** `chkEnableDebugLogging`

Load (line 88):
```csharp
chkEnableDebugLogging.Checked = _settings.EnableDebugLogging;
```

Save (line 721, inside the `AppSettings` object-initializer at `BtnSaveSettings_Click`):
```csharp
var settingsToSave = new AppSettings
{
    ...
    EnableDebugLogging = chkEnableDebugLogging.Checked,
    ...
};
```

Designer declaration (lines 268-274):
```csharp
// chkEnableDebugLogging
this.chkEnableDebugLogging.Text = "Enable debug logging (writes to %LOCALAPPDATA%\\RigToggle\\debug.log)";
this.chkEnableDebugLogging.Location = new System.Drawing.Point(12, 484);
this.chkEnableDebugLogging.Size = new System.Drawing.Size(396, 40);
this.chkEnableDebugLogging.AutoSize = false;
this.chkEnableDebugLogging.Name = "chkEnableDebugLogging";
```

This is the exact pattern for both new checkboxes: `Checked = _settings.<Field>` on Load, `<Field> = chk<Name>.Checked` in the `AppSettings` initializer on Save — no try/catch, no external service call, no `ErrorProvider`.

**Analog B — layout/section placement (visual template only):** `chkStartWithWindows`

Designer declaration (lines 312-327):
```csharp
// chkStartWithWindows
this.chkStartWithWindows.Text = "Start with Windows";
this.chkStartWithWindows.Location = new System.Drawing.Point(12, 600);
this.chkStartWithWindows.Size = new System.Drawing.Size(396, 24);
this.chkStartWithWindows.AutoSize = false;
this.chkStartWithWindows.Name = "chkStartWithWindows";

// lblAutostartWarning
this.lblAutostartWarning.Location = new System.Drawing.Point(12, 624);
this.lblAutostartWarning.Size = new System.Drawing.Size(396, 20);
this.lblAutostartWarning.AutoSize = false;
this.lblAutostartWarning.Visible = false;
this.lblAutostartWarning.Name = "lblAutostartWarning";
```

Note: `chkStartWithWindows` is **not** wrapped in a `GroupBox` — it, `lblAutostartWarning`, and `chkEnableDebugLogging` all sit as plain sibling controls directly on the form (added individually via `this.Controls.Add(...)`, lines 382/386-387), each with an explicit `Point`/`Size` (no `AutoSize`, no anchor/dock layout, no `FlowLayoutPanel`). D-07 ("same section as `chkStartWithWindows`") means: place the two new checkboxes as additional plain sibling `CheckBox` controls at the next available `Y` offset after `chkStartWithWindows`/`lblAutostartWarning` (i.e., below `Location.Y = 624 + 20 = ~644`), same `X = 12`, same `Size.Width = 396`, no new `GroupBox`. This is a UI-SPEC-level layout decision per D-07 ("UI hint: yes") but the control-declaration mechanics (Designer.cs field + `InitializeComponent()` block + `Controls.Add`) should mirror `chkEnableDebugLogging`/`chkStartWithWindows` exactly.

**Save-flow integration point — where D-08's live tray-visibility recompute must be triggered:**

`BtnSaveSettings_Click` (lines 664-799) already ends with a post-persist side-effect pattern for another cross-cutting concern (hotkey re-registration, lines 778-798):
```csharp
if (!_tryRegisterConfiguredHotkey())
{
    string message = "Could not register hotkey — ...";
    ...
    this.DialogResult = DialogResult.None;
}
```
Note `_tryRegisterConfiguredHotkey` is a `Func<bool>` injected via the constructor (line 19: `private readonly Func<bool> _tryRegisterConfiguredHotkey;`), NOT a direct `MainForm` reference — `SettingsForm` has no dependency on `MainForm`. CONTEXT.md's Claude's Discretion section suggests "a small `MainForm` helper mirroring the existing `TryRegisterConfiguredHotkey`-style pattern" — follow this exact DI shape: add a second `Func<bool>`-or-`Action`-typed constructor parameter (e.g. `Action _refreshTrayVisibility`) injected the same way, called near the end of `BtnSaveSettings_Click` after `_settingsStore.Save(settingsToSave);` (line 733), so the tray icon updates live without `SettingsForm` taking a direct `MainForm` dependency. Check `Program.cs`'s `SettingsFormFactory` (referenced at `MainForm.cs` line 46, `Func<SettingsForm> _settingsFormFactory`) for the composition-root wiring site where the new delegate parameter must be supplied — `Program.cs:116` constructs `MainForm`, and the `SettingsFormFactory` closure (not read in full here, but same file) is where `SettingsForm`'s constructor args are assembled.

---

## Shared Patterns

### Settings-load-in-event-handler pattern
**Source:** `src/RigToggle.App/MainForm.cs` line 178 (`BtnToggle_Click`) and lines 486 (`TryRegisterConfiguredHotkey`)
**Apply to:** `MainForm_FormClosing` (now needs settings) and the new minimize handler
```csharp
var settings = _settingsStore.Load();
```
`MainForm` already holds `_settingsStore` as a constructor-injected field (line 26) — no new dependency needed.

### XML-doc rationale-comment convention
**Source:** every handler in `MainForm.cs` (e.g. lines 301-313, 421-429, 468-480)
**Apply to:** all modified/new handlers in this phase
Every existing handler carries a `///` doc block citing the relevant REQ-ID/D-number and explaining *why*, not just *what* — CONTEXT.md's Established Patterns section explicitly calls this out as required for D-01's now-conditional close and D-08's derived-visibility rule. New code must follow this (see `MainForm_FormClosing`'s existing doc as the direct template for what the revised version's doc should look like — it must now explain that D-01 makes the branch conditional rather than unconditional).

### Plain-CheckBox-to-AppSettings round trip
**Source:** `src/RigToggle.App/SettingsForm.cs` line 88 (Load) + line 721 (Save) + Designer.cs lines 268-274 (`chkEnableDebugLogging`)
**Apply to:** both new checkboxes (`chkCloseMinimizesToTray`, `chkMinimizeToTray` or planner-chosen names)
No try/catch, no `ErrorProvider`, no external service — directly mirrors the simplest existing settings checkbox, not the registry-backed `chkStartWithWindows`.

### Derived/computed state recomputed at multiple call sites
**Source:** `src/RigToggle.App/MainForm.cs` — `RefreshUi()` (lines 127-143) is called from `OnLoad`, `InitializeTrayState()`, `BtnToggle_Click`, `OpenSettingsDialog()`, `TrayToggleMenuItem_Click`, and `HandleHotkeyToggle()` — i.e., every state-changing entry point re-derives and re-applies UI state rather than mutating it incrementally.
**Apply to:** the new D-08 tray-icon-visibility helper — call it from the same set of entry points (startup/`InitializeTrayState`, post-Settings-Save) rather than trying to track visibility incrementally/diffed.

## No Analog Found

| File/Region | Role | Data Flow | Reason |
|---|---|---|---|
| Minimize-to-tray interception (`MainForm.Resize`/`SizeChanged` handler) | controller, event-driven | No existing WinForms `Resize`/`SizeChanged` handler exists anywhere in the codebase — Phase 8 deliberately left minimize as standard OS behavior (08-CONTEXT.md D-03). This is genuinely new wiring; follow the `FormClosing` event-wiring mechanics in `MainForm.Designer.cs` line 149 as the closest structural precedent (event subscription in `InitializeComponent()`, handler method in `MainForm.cs`), not a behavioral analog. |

## Metadata

**Analog search scope:** `src/RigToggle.App/MainForm.cs`, `src/RigToggle.App/MainForm.Designer.cs`, `src/RigToggle.App/SettingsForm.cs`, `src/RigToggle.App/SettingsForm.Designer.cs`, `src/RigToggle.Core/Models/AppSettings.cs`, `src/RigToggle.App/Program.cs` (composition root, grepped only)
**Files scanned:** 6 read/grepped, 3 are the direct modification targets named in CONTEXT.md; no directory-wide analog search was needed since CONTEXT.md already pinpointed exact files/lines and this phase's closest analogs are sibling code within the same three files
**Pattern extraction date:** 2026-08-01
