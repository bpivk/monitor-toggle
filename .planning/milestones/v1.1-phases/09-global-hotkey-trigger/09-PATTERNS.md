# Phase 9: Global Hotkey Trigger - Pattern Map

**Mapped:** 2026-07-31
**Files analyzed:** 6 (5 modified, 1 new-surface-in-existing-file)
**Analogs found:** 5 / 6 (P/Invoke wrapper has a structural analog but no line-for-line one — see "No Analog Found")

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.App/MainForm.cs` (modify) | controller (window message handler + trigger source) | event-driven | `src/RigToggle.App/MainForm.cs` itself — `TrayToggleMenuItem_Click` (Phase 8) | exact (same file, sibling handler) |
| `src/RigToggle.App/MainForm.Designer.cs` (modify, if a components-owned P/Invoke wrapper is added; otherwise untouched) | config (designer-generated wiring) | request-response | `src/RigToggle.App/MainForm.Designer.cs` — `notifyIcon`/`trayContextMenu` component wiring (Phase 8) | role-match |
| `src/RigToggle.App/SettingsForm.cs` (modify) | controller (dialog Load/Save + new capture-textbox event handlers) | CRUD (settings) + event-driven (key capture) | `src/RigToggle.App/SettingsForm.cs` — `chkStartWithWindows` Load/Save block + `lblAutostartWarning`/`errAutostart` pattern | exact |
| `src/RigToggle.App/SettingsForm.Designer.cs` (modify) | config (designer layout) | request-response | `src/RigToggle.App/SettingsForm.Designer.cs` — `chkStartWithWindows`/`lblAutostartWarning`/`errAutostart` block + `lblAudioNormalCaption`/`cboAudioNormal` caption+field row | exact |
| `src/RigToggle.Core/Models/AppSettings.cs` (modify) | model | CRUD | `src/RigToggle.Core/Models/AppSettings.cs` itself — existing nullable-field convention | exact |
| `src/RigToggle.App/Program.cs` (modify) | config (composition root) | request-response (startup wiring) | `src/RigToggle.App/Program.cs` — `mainForm.InitializeTrayState()` unconditional-priming call site (Phase 8, D-06) | exact |
| *(new P/Invoke surface, wherever planner places it — e.g. `RigToggle.Windows/NativeMethods.cs` additions)* | utility (Win32 interop) | request-response | `src/RigToggle.Windows/NativeMethods.cs` | role-match (see "No Analog Found") |

## Pattern Assignments

### `src/RigToggle.App/MainForm.cs` (controller, event-driven)

**Analog:** same file — `TrayToggleMenuItem_Click` (lines 346-382) is the literal template D-03 names for the new hotkey handler; `OpenSettingsDialog()` (lines 257-262) is the literal template for D-07's unregister/re-register bracketing; `MainForm_FormClosing` (lines 277-290) shows the existing convention for overriding a WinForms lifecycle method safely.

**Imports pattern** (lines 1-4):
```csharp
using System.Linq;
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
```
Add `using RigToggle.Windows;` if the new P/Invoke wrapper is added to `RigToggle.Windows.NativeMethods` (Program.cs already has this import for the same reason — see below).

**Core "third trigger caller" pattern to replicate** (`TrayToggleMenuItem_Click`, lines 346-382):
```csharp
private void TrayToggleMenuItem_Click(object? sender, EventArgs e)
{
    ToggleResult result;

    try
    {
        result = _orchestrator.IsInRigMode()
            ? _orchestrator.ToggleToNormalMode()
            : _orchestrator.ToggleToRigMode();
    }
    catch (ToggleInProgressException ex)
    {
        notifyIcon.ShowBalloonTip(
            3000,
            "Rig Toggle",
            ToggleResultFormatter.TruncateForBalloon(ex.Message),
            ToolTipIcon.Warning);
        return;
    }
    catch (Exception ex)
    {
        notifyIcon.ShowBalloonTip(
            3000,
            "Rig Toggle",
            ToggleResultFormatter.TruncateForBalloon($"Something went wrong while toggling: {ex.GetType().Name}: {ex.Message}"),
            ToolTipIcon.Warning);
        return;
    }

    RefreshUi();

    notifyIcon.ShowBalloonTip(
        3000,
        ToggleResultFormatter.FormatModeTitle(_orchestrator.IsInRigMode()),
        ToggleResultFormatter.TruncateForBalloon(ToggleResultFormatter.FormatChecklist(result)),
        result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
}
```
The new `WM_HOTKEY` handler (called from the `WndProc` override below) should be structurally identical — same try/catch shape, same `ToolTipIcon` mapping, same never-`MessageBox` rule (D-03 says "same skip-the-GUI-confirmation-dialog posture, same unconditional `ShowBalloonTip` result toast").

**`WndProc` override — new surface, no exact analog in this codebase.** The closest structural precedent for "override a WinForms lifecycle method and delegate to base for everything not handled" is `MainForm_FormClosing` (lines 277-290):
```csharp
private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
{
    if (e.CloseReason == CloseReason.UserClosing)
    {
        e.Cancel = true;
        Hide();
        return;
    }

    // ApplicationExitCall (tray Exit), WindowsShutDown, TaskManagerClosing, etc.
    notifyIcon.Visible = false;
}
```
Apply the same "handle the one case you care about, let everything else fall through unmodified" shape to `WndProc`:
```csharp
protected override void WndProc(ref Message m)
{
    const int WM_HOTKEY = 0x0312;
    if (m.Msg == WM_HOTKEY)
    {
        // dispatch to the hotkey handler (mirrors TrayToggleMenuItem_Click's body)
    }
    base.WndProc(m); // MUST run for every message, matching or not
}
```

**Bracketing pattern for D-07** — extend `OpenSettingsDialog()` (lines 257-262):
```csharp
private void OpenSettingsDialog()
{
    using var settingsForm = _settingsFormFactory();
    settingsForm.ShowDialog(this);
    RefreshUi();
}
```
D-07 requires wrapping the `ShowDialog` call with unregister-before / re-register-after, following the same "one shared helper, called from every entry point" precedent `RefreshUi()` and `OpenSettingsDialog()` already establish (both are already deliberately shared between the GUI-button and tray-menu code paths, per the existing IN-02 code-review comment at line 255).

**Error handling pattern** — the `catch (ToggleInProgressException ex)` / `catch (Exception ex)` two-branch shape above (lines 356-373) is the exact template; no `MessageBox` anywhere in this method.

---

### `src/RigToggle.App/SettingsForm.cs` (controller, CRUD + event-driven)

**Analog:** same file — `chkStartWithWindows`'s Load-time read (lines 73-83) and Save-time write with dedicated inline-warning recovery (lines 578-614) is the literal named precedent (D-05, `08-CONTEXT.md`/`08-REVIEW.md`).

**Load-time pattern to replicate** (lines 73-83, adapt "registry read" → "load persisted hotkey + attempt registration" if D-04's helper is invoked here too, or just populate the display string if registration only happens at startup/Save):
```csharp
try
{
    chkStartWithWindows.Checked = _autostartConfigurator.IsEnabled();
}
catch (Exception ex)
{
    chkStartWithWindows.Checked = false;
    lblAutostartWarning.Text = $"Could not read Start with Windows state: {ex.Message}";
    lblAutostartWarning.Visible = true;
    errAutostart.SetError(chkStartWithWindows, lblAutostartWarning.Text);
}
```

**Save-time pattern to replicate verbatim in shape** (lines 578-614) — this is the exact template for D-05's non-blocking registration-failure warning:
```csharp
try
{
    errAutostart.SetError(chkStartWithWindows, string.Empty);
    lblAutostartWarning.Visible = false;

    if (chkStartWithWindows.Checked)
    {
        _autostartConfigurator.Enable();
    }
    else
    {
        _autostartConfigurator.Disable();
    }
}
catch (Exception ex)
{
    string message = $"Could not enable Start with Windows: {ex.Message}";
    lblAutostartWarning.Text = message;
    lblAutostartWarning.Visible = true;
    errAutostart.SetError(chkStartWithWindows, message);

    // CR-01 (code review): this recovery read must never itself throw.
    try
    {
        chkStartWithWindows.Checked = _autostartConfigurator.IsEnabled();
    }
    catch
    {
        // Best-effort UI sync only.
    }
}
```
For the hotkey field: clear `errHotkey`/`lblHotkeyWarning` first, attempt registration via the shared D-04 helper, and on failure set the dedicated warning text ("Could not register hotkey — it may already be in use by another application. Choose a different combination or close the conflicting app, then click Save again." per UI-SPEC Copywriting Contract) — **but unlike autostart, Save is NOT blocked and the value IS still persisted** (D-05 explicit distinction from the autostart precedent — do not copy the "revert to actual state" recovery-read part, since there's no external device state to resync from; the user's *chosen* combination is the source of truth regardless of registration success).

**Reentrancy-guard-around-programmatic-write pattern** — for the capture textbox's `KeyDown`/`MouseDown` interaction (UI-SPEC "Recording" state machine), the closest existing precedent for "guard a control's event handler against firing during a programmatic mutation of that same control" is `_updatingMonitorGridProgrammatically` (lines 26-29, 196-207):
```csharp
// Reentrancy guard around the D-04 programmatic sibling-checkbox write — without
// this, unchecking the sibling column would itself re-fire CellValueChanged.
private bool _updatingMonitorGridProgrammatically;
...
_updatingMonitorGridProgrammatically = true;
try
{
    row.Cells[siblingIndex].Value = false;
}
finally
{
    _updatingMonitorGridProgrammatically = false;
}
```
Apply the same "boolean flag + try/finally" shape for tracking "are we currently in Recording mode" state transitions on `txtHotkey`.

**Unhook-around-bulk-populate pattern** — `PopulateAudioCombo` (lines 284-324) shows the established "unhook the event, mutate, rehook" convention:
```csharp
combo.SelectedIndexChanged -= OnPickerChanged;
...
combo.SelectedIndexChanged += OnPickerChanged;
```
Not directly needed for a single textbox, but is the established idiom if the capture logic needs to programmatically set `txtHotkey.Text` without re-triggering its own key-capture handlers.

---

### `src/RigToggle.App/SettingsForm.Designer.cs` (config, request-response)

**Analog:** same file — the `chkStartWithWindows`/`lblAutostartWarning`/`errAutostart` block (lines 271-286, 315-321, 381-393) is the direct dedicated-pair template (D-05/UI-SPEC "MUST use its own dedicated control pair"); the `lblAudioNormalCaption`/`cboAudioNormal` caption+field row (lines 168-181) is the direct template for the new `lblHotkeyCaption`/`txtHotkey` row.

**Dedicated warning-label + ErrorProvider pair pattern** (lines 271-286, 315-321):
```csharp
//
// chkStartWithWindows
//
this.chkStartWithWindows.Text = "Start with Windows";
this.chkStartWithWindows.Location = new System.Drawing.Point(12, 532);
this.chkStartWithWindows.Size = new System.Drawing.Size(396, 24);
this.chkStartWithWindows.AutoSize = false;
this.chkStartWithWindows.Name = "chkStartWithWindows";

//
// lblAutostartWarning
//
this.lblAutostartWarning.Location = new System.Drawing.Point(12, 556);
this.lblAutostartWarning.Size = new System.Drawing.Size(396, 20);
this.lblAutostartWarning.AutoSize = false;
this.lblAutostartWarning.Visible = false;
this.lblAutostartWarning.Name = "lblAutostartWarning";
...
this.errAutostart.ContainerControl = this;
```
UI-SPEC's exact prescribed placement for the new controls (`lblHotkeyCaption` at (12,532), `txtHotkey` at (76,529) size (200,23), `lblHotkeyWarning` at (12,556) size (396,36) two-line, plus a new `errHotkey` ErrorProvider) is a direct transposition of this block — same `Location`/`Size`/`AutoSize=false`/`Name`/`ContainerControl=this` shape, just with the new coordinates from `09-UI-SPEC.md`'s placement table, and every downstream control (`chkStartWithWindows`, `lblAutostartWarning`, `btnSaveSettings`, `btnDiscardChanges`) and `ClientSize` shifted per that same table.

**Caption + field row pattern** (lines 168-181):
```csharp
//
// lblAudioNormalCaption
//
this.lblAudioNormalCaption.Text = "Normal:";
this.lblAudioNormalCaption.Location = new System.Drawing.Point(12, 25);
this.lblAudioNormalCaption.Size = new System.Drawing.Size(48, 20);
this.lblAudioNormalCaption.Name = "lblAudioNormalCaption";

//
// cboAudioNormal
//
this.cboAudioNormal.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
this.cboAudioNormal.Location = new System.Drawing.Point(64, 22);
this.cboAudioNormal.Size = new System.Drawing.Size(320, 23);
this.cboAudioNormal.Name = "cboAudioNormal";
```
Use this exact caption-left/field-right shape for `lblHotkeyCaption` ("Hotkey:") + `txtHotkey`, substituting `ReadOnly = true` and `TabStop = false` per UI-SPEC's Interaction States section (load-bearing per the UI-SPEC's own rationale — do not omit `TabStop = false`).

**ErrorProvider batch-init/EndInit pattern** (lines 315-321, 346-350):
```csharp
((System.ComponentModel.ISupportInitialize)(this.errAutostart)).BeginInit();
...
this.errAutostart.ContainerControl = this;
...
((System.ComponentModel.ISupportInitialize)(this.errAutostart)).EndInit();
```
`errHotkey` must be added to both the `BeginInit`/`EndInit` sequences and the `ContainerControl = this` block alongside the four existing `ErrorProvider` instances — do not create it as a standalone instance outside this batch.

**Field declaration convention** (lines 383-384, 393):
```csharp
private System.Windows.Forms.CheckBox chkStartWithWindows;
private System.Windows.Forms.Label lblAutostartWarning;
...
private System.Windows.Forms.ErrorProvider errAutostart;
```

---

### `src/RigToggle.Core/Models/AppSettings.cs` (model, CRUD)

**Analog:** same file (entire file, 27 lines) — flat nullable-field POCO, no nested objects, no validation attributes.

**Full existing pattern:**
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
}
```
New field(s) for the hotkey combo follow the same "nullable = never configured" convention already documented in the class XML doc (lines 6-7: "All fields nullable: a null field means 'never configured' (first run)"). D-02 requires no default hotkey, matching this exact null-means-unconfigured rule already used for `CompanionAppPath`/`NormalAudioDeviceId` etc. Whichever representation the planner picks (packed `int` vs. separate `Keys`+modifiers fields — Claude's Discretion per `09-CONTEXT.md`), keep it a flat scalar/nullable field on this same class, not a nested object — no existing field in this model is a nested/complex type, and introducing one here would break the class's established flat-POCO convention.

---

### `src/RigToggle.App/Program.cs` (config, composition root)

**Analog:** same file — the unconditional `mainForm.InitializeTrayState()` priming call (lines 108-111) is the exact precedent for "a startup-time side effect that must run regardless of which `Application.Run` branch executes next."

**Pattern to replicate:**
```csharp
// Pitfall 6: prime the tray icon/menu BEFORE either Run branch — under
// --tray the form's own Load event never fires since the form is never
// shown, so tray state must not depend on it.
mainForm.InitializeTrayState();
```
D-04's startup-time hotkey registration call belongs at this same point in `Main()` — right after `mainForm` is constructed and before the `StartupArgs.ShouldStartHidden(args)` branch — so it fires unconditionally for both the visible and `--tray` startup paths, exactly mirroring how `InitializeTrayState()` already must.

**Best-effort/never-block-startup pattern** (lines 68-83, the `EnableDebugLogging`-gated trace listener setup):
```csharp
if (settings.EnableDebugLogging)
{
    try
    {
        Directory.CreateDirectory(basePath);
        var traceWriter = new StreamWriter(Path.Combine(basePath, "debug.log"), append: true)
        {
            AutoFlush = true,
        };
        Trace.Listeners.Add(new TextWriterTraceListener(traceWriter));
    }
    catch
    {
        // Diagnostic logging is best-effort only — never let it prevent startup.
    }
}
```
D-06 requires the startup-time hotkey registration failure to be traced (`Trace.WriteLine`, matching this codebase's established convention) and surfaced via a toast — NOT to throw or block `Application.Run`. Wrap the registration call in the same try/catch-and-trace shape; on failure, call `notifyIcon.ShowBalloonTip` (via a method exposed on `mainForm`, since `notifyIcon` is a private field of `MainForm`) with `ToolTipIcon.Warning` and the D-06 wording, matching `TrayToggleMenuItem_Click`'s existing balloon-toast calls.

---

## Shared Patterns

### Never-MessageBox, Always-ShowBalloonTip for non-GUI triggers
**Source:** `src/RigToggle.App/MainForm.cs`, `TrayToggleMenuItem_Click` (lines 346-382)
**Apply to:** The new hotkey `WM_HOTKEY` handler, and the startup-time registration-failure toast in `Program.cs`/`MainForm.cs`.
```csharp
notifyIcon.ShowBalloonTip(
    3000,
    "Rig Toggle",
    ToggleResultFormatter.TruncateForBalloon(ex.Message),
    ToolTipIcon.Warning);
```
Every code path in this handler family (both exception branches and the final result) routes through `ShowBalloonTip`, never `MessageBox.Show` — the explicit "CRITICAL (D-08 no-chrome guarantee)" comment at lines 336-345 states why and must not be violated by the hotkey handler either.

### `ToggleOrchestrator` as the single toggle entry point
**Source:** `src/RigToggle.Core/ToggleOrchestrator.cs` (Phase 7); called from `MainForm.BtnToggle_Click` and `MainForm.TrayToggleMenuItem_Click`
**Apply to:** The new hotkey handler — the third caller of `ToggleToRigMode()`/`ToggleToNormalMode()`, catching `ToggleInProgressException` for the busy-rejection case exactly like the tray handler does.

### Dedicated inline-warning-pair-per-concern (never share across sections)
**Source:** `src/RigToggle.App/SettingsForm.cs` lines 578-614 + `SettingsForm.Designer.cs` lines 271-286/315-321/381-393 (`chkStartWithWindows`/`lblAutostartWarning`/`errAutostart`)
**Apply to:** The new `txtHotkey`/`lblHotkeyWarning`/`errHotkey` trio — must be its own dedicated set, never reusing `errAutostart` or any other section's controls (explicitly required by both `09-CONTEXT.md` D-05 and `09-UI-SPEC.md`'s Spacing Scale section, both citing `08-REVIEW.md`'s precedent for this rule).

### `ToggleResultFormatter` reuse for toast content
**Source:** `src/RigToggle.Core/ToggleResultFormatter.cs` (`FormatChecklist`, `FormatModeTitle`, `TruncateForBalloon`)
**Apply to:** The hotkey handler's result toast — reuse verbatim, do not reformat or duplicate this logic (same as the tray handler already does).

### Best-effort trace-and-degrade for non-critical startup side effects
**Source:** `src/RigToggle.App/Program.cs` lines 68-83 (trace-listener setup) and lines 49-57 (settings-load fallback)
**Apply to:** D-06's startup-time hotkey registration — must never throw past this call site; on failure, `Trace.WriteLine` and show the warning toast, then continue exactly as if registration had been skipped.

### XML-doc rationale comments explaining *why*, not just *what*
**Source:** pervasive throughout `MainForm.cs`, `SettingsForm.cs`, `Program.cs`, `WindowsAutostartConfigurator.cs` — e.g. `MainForm.cs` lines 264-276 (`MainForm_FormClosing`), `Program.cs` lines 108-125 (D-06 rig-corrected note)
**Apply to:** D-07's unregister-during-Settings choice and D-05's non-blocking-Save decision (explicitly called out in `09-CONTEXT.md`'s "Established Patterns" section) — write the comment as a guardrail against a future "fix" that re-introduces complexity, matching this codebase's existing tone (see `MainForm.cs` lines 328-335's "ACCEPTED RISK... reviewed and knowingly kept" style).

---

## No Analog Found

| File/Surface | Role | Data Flow | Reason |
|---|---|---|---|
| `RegisterHotKey`/`UnregisterHotKey`/`WM_HOTKEY` P/Invoke wrapper (wherever added — new members on `RigToggle.Windows/NativeMethods.cs`, or a small new file alongside it) | utility (Win32 interop) | event-driven | This app's only prior P/Invoke surface (`src/RigToggle.Windows/NativeMethods.cs`) wraps window-enumeration/focus calls (`ShowWindow`, `EnumWindows`, `GetWindowPlacement`, etc.) for a completely different purpose (finding/minimizing the companion app's window) — no existing `DllImport` in this codebase registers a global hotkey or handles a custom window message. Follow `NativeMethods.cs`'s *conventions* (plain `internal`/`public static extern`, grouped `DllImport` attributes, a short rationale comment citing CLAUDE.md's "no hooking library" guidance, constants like `SW_MINIMIZE` declared alongside the calls that use them) rather than any specific existing signature. Whether this lives in `RigToggle.Windows.NativeMethods` (kept `public` so `RigToggle.App.MainForm` can call it, consistent with "hand-rolled P/Invoke lives in the Windows project") or as new `RigToggle.App`-local `DllImport`s (since `RegisterHotKey` operates on a handle `MainForm` already owns, unlike the companion-app window lookup) is a planner decision — `09-CONTEXT.md`'s Claude's Discretion section leaves the *helper class* placement open, and the same reasoning extends to where the raw P/Invoke signatures themselves live. |
| A possible `IHotkeyRegistrar`-style interface abstraction (if the planner chooses to test-isolate the registration helper) | service | event-driven | `IAutostartConfigurator`/`WindowsAutostartConfigurator` (Phase 8) is the closest interface-per-concern precedent in this codebase for "a small Windows-specific side-effecting operation behind a Core interface, implemented in `RigToggle.Windows`" — but `09-CONTEXT.md`'s Claude's Discretion note explicitly favors a simpler `MainForm`-hosted helper (no interface) instead, since `RegisterHotKey` needs `MainForm`'s own `Handle` and isn't naturally swappable the way a registry write is. If the planner does introduce an interface for testability, `IAutostartConfigurator` (3-method contract: `IsEnabled`/`Enable`/`Disable`) is the pattern to mirror. |

## Metadata

**Analog search scope:** `src/RigToggle.App/`, `src/RigToggle.Core/`, `src/RigToggle.Windows/` (all `.cs` files, excluding `obj/`/generated files)
**Files scanned:** `MainForm.cs`, `MainForm.Designer.cs`, `SettingsForm.cs`, `SettingsForm.Designer.cs`, `Program.cs`, `AppSettings.cs`, `NativeMethods.cs`, `StartupArgs.cs`, `ToggleResultFormatter.cs`, `IAutostartConfigurator.cs`, `WindowsAutostartConfigurator.cs`, plus `08-CONTEXT.md`/`09-CONTEXT.md`/`09-UI-SPEC.md` for decision cross-referencing
**Pattern extraction date:** 2026-07-31
