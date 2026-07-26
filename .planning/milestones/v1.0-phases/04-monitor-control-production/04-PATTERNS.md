# Phase 4: Monitor Control (Production) - Pattern Map

**Mapped:** 2026-07-24
**Files analyzed:** 12 (2 new, 10 modified)
**Analogs found:** 12 / 12

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.Windows/WindowsMonitorController.cs` | service (Windows adapter) | CRUD + verify-and-throw mutation | `src/RigToggle.Windows/WindowsAudioController.cs` | exact (same role, same Phase-3 verify-and-throw pattern) |
| `src/RigToggle.Core/Abstractions/IMonitorController.cs` | interface (abstraction) | request-response | `src/RigToggle.Core/Abstractions/IAudioController.cs` | exact (mirrors `CaptureState()` no-param precedent) |
| `src/RigToggle.Core/Models/MonitorState.cs` | model | transform / JSON-serializable | `src/RigToggle.Core/Models/AudioState.cs` | exact (composed-of-per-item-snapshots record shape) |
| `src/RigToggle.Core/Models/MonitorPathSnapshot.cs` (NEW) | model | transform / JSON-serializable | `src/RigToggle.Core/Models/AudioRoleState.cs` | role-match (per-unit primitive snapshot record) |
| `src/RigToggle.App/MonitorConfirmDialog.cs` (NEW) | component (WinForms Form) | request-response (modal dialog) | `src/RigToggle.App/SettingsForm.cs` | exact (modal dialog wiring: AcceptButton/CancelButton, constructor-injected data, Load-time populate) |
| `src/RigToggle.App/MonitorConfirmDialog.Designer.cs` (NEW) | component (WinForms Designer) | N/A (declarative layout) | `src/RigToggle.App/SettingsForm.Designer.cs` | exact (Designer-file convention: `InitializeComponent`, `DialogResult` buttons, `Dispose`) |
| `src/RigToggle.App/MainForm.cs` | controller (WinForms code-behind) | event-driven (button click) | itself (`BtnToggle_Click`, existing) | exact — self-modification, extend existing handler |
| `src/RigToggle.Core/Models/AppSettings.cs` | model | CRUD (settings persistence) | itself (existing) | exact — self-modification, add one field |
| `src/RigToggle.App/SettingsForm.cs` | component (WinForms code-behind) | CRUD (settings save) | itself (`BtnSaveSettings_Click`, existing) | exact — self-modification, extend existing save logic |
| `src/RigToggle.Core/ToggleService.cs` | service (orchestrator) | CRUD (snapshot-before-mutate sequence) | itself (`ToggleToRigMode`, existing) | exact — self-modification, one call-site signature change |
| `src/RigToggle.Tests/Doubles/FakeControllers.cs` | test double | request-response (recording fake) | `FakeAudioController` in same file (existing) | exact — sibling fake in the same file, same recording-fake convention |
| `src/RigToggle.Tests/JsonStoreTests.cs` | test | CRUD (round-trip persistence) | itself (existing `MonitorState`/`AudioState` construction calls) | exact — self-modification, update construction calls for new shape |

## Pattern Assignments

### `src/RigToggle.Windows/WindowsMonitorController.cs` (service, CRUD + verify-and-throw)

**Analog:** `src/RigToggle.Windows/WindowsAudioController.cs`

**Imports pattern** (lines 1-7 of analog; current monitor file's imports at lines 1-4 already match minus `System.Runtime.InteropServices`):
```csharp
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;
```

**Verify-and-throw core pattern** (`WindowsAudioController.cs` lines 183-209, `ApplyAndVerify`):
```csharp
private static void ApplyAndVerify(ERole nativeRole, Role managedRole, string deviceId)
{
    var client = (IPolicyConfig)new PolicyConfigClient();
    try
    {
        int hr = client.SetDefaultEndpoint(deviceId, nativeRole);
        if (hr != 0) // S_OK
        {
            throw new InvalidOperationException(
                $"SetDefaultEndpoint failed for role {nativeRole} (HRESULT 0x{hr:X8}).");
        }
    }
    finally
    {
        Marshal.ReleaseComObject(client); // release every cycle — Pitfall 5/C COM leak
    }

    // D-03/D-04: verify-and-throw, not trust-the-HRESULT (Pitfall 6)
    using var enumerator = new MMDeviceEnumerator();
    using var actual = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, managedRole);
    if (!string.Equals(actual.ID, deviceId, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Audio default for role {managedRole} did not change to the requested " +
            $"device after SetDefaultEndpoint (expected '{deviceId}', got '{actual.ID}').");
    }
}
```
**How to translate for Monitor:** Same shape — mutate via `PathInfo.ApplyPathInfos(...)`, then immediately re-query via `PathInfo.GetActivePaths()` (never `Screen.AllScreens`, per D-04) and throw `InvalidOperationException` on mismatch, exactly mirroring the "apply, then trust only a fresh re-query" two-step. RESEARCH.md Pattern 1/3 (`04-RESEARCH.md` lines 172-292) already contains the CCD-specific version of this same shape — use those as the literal implementation, this analog is for the *verify-and-throw idiom and exception-message style* (parametrized `$"... (expected 'x', got 'y')."` message format), not the CCD math itself.

**Per-item independent-failure loop pattern** (`WindowsAudioController.cs` lines 57-96, `CaptureState`'s three independent try/catch blocks — one per audio role):
```csharp
AudioRoleState consoleState;
try
{
    using var enumerator = new MMDeviceEnumerator();
    using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
    consoleState = new AudioRoleState(device.ID, device.FriendlyName);
}
catch (Exception)
{
    consoleState = new AudioRoleState(null, null);
}
```
**How to translate:** `WindowsMonitorController.CaptureState()` (new, no-param per Pattern 2) should capture the **full active topology** in one `PathInfo.GetActivePaths()` call and `.Select(...)` it into `MonitorPathSnapshot` records — there is no per-item independent-failure need here (unlike audio's three separate role queries), so this pattern only informs the *defensive posture*, not a literal per-monitor try/catch. Reference `04-RESEARCH.md` lines 249-269 (Pattern 2 code example) for the exact `CaptureState` shape to implement.

**Existing doc-comment convention to preserve** (`WindowsMonitorController.cs` lines 8-14 — update, don't delete, this style of file-header comment referencing the responsible spike/research/context artifacts):
```csharp
/// <summary>
/// Real monitor enumeration via WindowsDisplayAPI's CCD wrapper (proven non-elevated
/// on this rig's AMD/DisplayPort hardware by the Phase 1 spike,
/// spike/MonitorDetachSpike/Program.cs RunList()). Disable/Restore are documented
/// no-op stubs until Phase 4 fills in the real CCD topology-removal mutation
/// (02-RESEARCH.md Pattern 1).
/// </summary>
```

**Source for the actual CCD mechanism (not this analog):** `04-RESEARCH.md` Patterns 1, 2, 3, 4 (lines 167-360) — repositioning-aware primary removal, full-topology capture, verify-and-throw, live-identity restore. `spike/MonitorDetachSpike/Program.cs` `RunDisable`/`VerifyOnce` (lines 72-153) shows the *non-primary* mechanism and the "capture before mutate, snapshot is audit-only" shape but is explicitly NOT sufficient as-is (no repositioning, no cross-restart-safe shape) — do not copy its `Screen.AllScreens` verification (lines 129-152) at all (D-04 forbids it).

---

### `src/RigToggle.Core/Abstractions/IMonitorController.cs` (interface, request-response)

**Analog:** `src/RigToggle.Core/Abstractions/IAudioController.cs`

**Full analog file** (lines 1-18):
```csharp
using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Audio playback endpoint enumeration and default-device switch/restore contract.
/// Implemented by RigToggle.Windows.WindowsAudioController. Read methods
/// (GetPlaybackDevices, CaptureState) are real starting Phase 2; SetDefault/Restore
/// are no-op stubs until Phase 3 (02-RESEARCH.md Pattern 1).
/// </summary>
public interface IAudioController
{
    IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();
    AudioState CaptureState();
    void SetDefault(string deviceId);
    void Restore(AudioState previousState);
}
```
**How to translate:** Change `IMonitorController.CaptureState(string monitorDevicePath)` to `CaptureState()` — no parameter — exactly matching `IAudioController.CaptureState()`'s shape (RESEARCH.md Pattern 2, `04-RESEARCH.md` line 222). Update the doc comment to say "real starting Phase 2; Disable/Restore are real starting Phase 4" (mirrors the audio interface's own phase-progression comment style). `GetActiveMonitors`/`Disable`/`Restore` method signatures stay unchanged.

---

### `src/RigToggle.Core/Models/MonitorState.cs` (model, transform)

**Analog:** `src/RigToggle.Core/Models/AudioState.cs` (composition) + `src/RigToggle.Core/Models/AudioRoleState.cs` (per-unit primitive record)

**AudioState.cs, full file** (lines 1-9):
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Captured default audio playback device state at toggle-time, used to restore it later.
/// Holds one AudioRoleState snapshot per Windows audio role (eConsole, eMultimedia,
/// eCommunications) per D-02 / AUDIO-02, since Windows tracks a separate default render
/// endpoint for each role and restore must be exact across all three.
/// </summary>
public sealed record AudioState(AudioRoleState Console, AudioRoleState Multimedia, AudioRoleState Communications);
```

**AudioRoleState.cs, full file** (lines 1-7):
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Captured default audio playback device for a single Windows audio role (eConsole,
/// eMultimedia, or eCommunications), used to restore that role's default later.
/// </summary>
public sealed record AudioRoleState(string? DeviceId, string? DeviceName);
```
**How to translate:** `MonitorState` becomes a composition record holding `IReadOnlyList<MonitorPathSnapshot> Paths` plus `string TargetDevicePath` (which entry is the configured monitor to disable) — same "container record + per-unit primitive record" shape as `AudioState`/`AudioRoleState`, but a list instead of three fixed named fields (topology size is variable, unlike the fixed three audio roles). Exact target shape is specified in `04-RESEARCH.md` Pattern 2 (lines 230-245):
```csharp
public sealed record MonitorPathSnapshot(
    string DevicePath,
    string FriendlyName,
    int PositionX,
    int PositionY,
    int ResolutionWidth,
    int ResolutionHeight,
    DisplayConfigPixelFormat PixelFormat,
    DisplayConfigRotation Rotation,
    DisplayConfigScaling Scaling,
    DisplayConfigVideoOutputTechnology OutputTechnology,
    ulong FrequencyInMillihertz,
    DisplayConfigScanLineOrdering ScanLineOrdering,
    bool IsPrimary);

public sealed record MonitorState(IReadOnlyList<MonitorPathSnapshot> Paths, string TargetDevicePath);
```
**Breaking-change ripple to track (mirrors how `AudioState`'s three-role shape already ripples through `JsonStoreTests.cs`, `FakeControllers.cs`, `ToggleService.cs`):** every call site constructing `new MonitorState(devicePath)` (single-string constructor) must be updated to the new shape — see `src/RigToggle.Tests/JsonStoreTests.cs` lines 96, 106, 121, 126 and `src/RigToggle.Tests/Doubles/FakeControllers.cs` lines 27, 37 below.

---

### `src/RigToggle.App/MonitorConfirmDialog.cs` + `.Designer.cs` (NEW — component, request-response)

**Analog:** `src/RigToggle.App/SettingsForm.cs` + `src/RigToggle.App/SettingsForm.Designer.cs`

**Constructor + AcceptButton/CancelButton wiring pattern** (`SettingsForm.cs` lines 26-44):
```csharp
public SettingsForm(IMonitorController monitorController, IAudioController audioController, ISettingsStore settingsStore)
{
    _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
    _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
    _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));

    InitializeComponent();

    // Esc / system close box (X) both produce DialogResult.Cancel via the
    // declarative Discard button — no extra FormClosing handler needed
    // (02-RESEARCH.md Pattern 5).
    this.AcceptButton = btnSaveSettings;
    this.CancelButton = btnDiscardChanges;
    ...
}
```
**How to translate:** `MonitorConfirmDialog`'s constructor takes the monitor's friendly name as a plain string argument (no Core interface needed — it's pure display data, per `04-RESEARCH.md` Pattern 5 line 368-381) rather than injected interfaces:
```csharp
public partial class MonitorConfirmDialog : Form
{
    public bool DontAskAgain => chkDontAskAgain.Checked;

    public MonitorConfirmDialog(string monitorFriendlyName)
    {
        InitializeComponent();
        lblMessage.Text = $"This will disable \"{monitorFriendlyName}\" (primary). Continue?";
        AcceptButton = btnContinue;
        CancelButton = btnCancel;
    }
}
```

**Designer-file declarative-button + DialogResult convention** (`SettingsForm.Designer.cs` lines 198-215, the `btnSaveSettings`/`btnDiscardChanges` pair — declarative `DialogResult` property means no explicit `Close()` call is needed in code-behind):
```csharp
//
// btnSaveSettings
//
this.btnSaveSettings.Text = "Save Settings";
this.btnSaveSettings.Location = new System.Drawing.Point(180, 332);
this.btnSaveSettings.Size = new System.Drawing.Size(110, 32);
this.btnSaveSettings.DialogResult = System.Windows.Forms.DialogResult.OK;
this.btnSaveSettings.Name = "btnSaveSettings";
this.btnSaveSettings.Click += new System.EventHandler(this.BtnSaveSettings_Click);

//
// btnDiscardChanges
//
this.btnDiscardChanges.Text = "Discard Changes";
this.btnDiscardChanges.Location = new System.Drawing.Point(298, 332);
this.btnDiscardChanges.Size = new System.Drawing.Size(110, 32);
this.btnDiscardChanges.DialogResult = System.Windows.Forms.DialogResult.Cancel;
this.btnDiscardChanges.Name = "btnDiscardChanges";
```
**Whole-Designer-file skeleton to copy** (`Dispose`/`InitializeComponent`/`#region` shape, `SettingsForm.Designer.cs` lines 1-30 and 259-289 — the boilerplate top/bottom that every `*.Designer.cs` in this project uses verbatim):
```csharp
namespace RigToggle.App
{
    partial class MonitorConfirmDialog
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            // ... lblMessage, chkDontAskAgain, btnContinue, btnCancel construction,
            // matching SettingsForm.Designer.cs's per-control comment-block style
            // ("// \n // controlName \n //") and FixedDialog/CenterParent/ShowInTaskbar=false
            // form-level properties (SettingsForm.Designer.cs lines 234-243).
        }
        #endregion

        private System.Windows.Forms.Label lblMessage;
        private System.Windows.Forms.CheckBox chkDontAskAgain;
        private System.Windows.Forms.Button btnContinue;
        private System.Windows.Forms.Button btnCancel;
    }
}
```
**Form-level properties to copy** (`SettingsForm.Designer.cs` lines 234-243 — `FixedDialog`, no maximize/minimize, `CenterParent`, hidden from taskbar):
```csharp
this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
this.MaximizeBox = false;
this.MinimizeBox = false;
this.ShowInTaskbar = false;
this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
```

---

### `src/RigToggle.App/MainForm.cs` (controller, event-driven — modify existing)

**Analog:** itself, `BtnToggle_Click` (lines 60-100) — the confirmation dialog call is inserted into the existing rig-mode branch.

**Current structure to extend:**
```csharp
private void BtnToggle_Click(object? sender, EventArgs e)
{
    try
    {
        if (_toggleService.IsInRigMode())
        {
            _toggleService.ToggleToNormalMode();
        }
        else
        {
            if (!_toggleService.IsSettingsConfigured())
            {
                MessageBox.Show(this, "...", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _toggleService.ToggleToRigMode();
        }

        RefreshUi();
    }
    catch (Exception)
    {
        MessageBox.Show(this, "Something went wrong...", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
```
**Insertion point (per `04-RESEARCH.md` Pattern 5, lines 383-403):** the confirmation-dialog + "don't ask again" persistence block goes between the `IsSettingsConfigured()` guard and the `_toggleService.ToggleToRigMode()` call, inside the same `else` branch, using the same early-`return` idiom already established by the `IsSettingsConfigured` guard above it (return with nothing mutated on Cancel):
```csharp
var settings = _settingsStore.Load();
if (!settings.SkipMonitorConfirmation)
{
    var monitor = _monitorController.GetActiveMonitors()
        .FirstOrDefault(m => m.DevicePath == settings.MonitorDevicePath);
    string name = monitor?.FriendlyName ?? "the configured monitor";

    using var confirmDialog = new MonitorConfirmDialog(name);
    if (confirmDialog.ShowDialog(this) != DialogResult.OK)
    {
        return; // user cancelled — nothing mutated
    }

    if (confirmDialog.DontAskAgain)
    {
        settings.SkipMonitorConfirmation = true;
        _settingsStore.Save(settings);
    }
}
```
**Constructor-injection note:** `MainForm` does not currently hold an `IMonitorController` field (only `ToggleService`, `IAppController`, `ISettingsStore`, `Func<SettingsForm>` — lines 16-19). This new code needs `IMonitorController` added as a constructor parameter, following the exact same null-guard convention already used for the other three:
```csharp
_monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
```
and `Program.cs` (composition root, lines 49-51) must pass `monitorController` (already constructed at line 36) into `new MainForm(...)`.

---

### `src/RigToggle.Core/Models/AppSettings.cs` (model — modify existing)

**Analog:** itself (existing field-addition convention, lines 9-18):
```csharp
public sealed class AppSettings
{
    public string? MonitorDevicePath { get; set; }
    public string? MonitorFriendlyName { get; set; }   // display-cache only, not used for matching
    public string? NormalAudioDeviceId { get; set; }
    public string? NormalAudioDeviceName { get; set; }
    public string? RigAudioDeviceId { get; set; }
    public string? RigAudioDeviceName { get; set; }
    public string? CompanionAppPath { get; set; }
}
```
**How to translate:** add `public bool SkipMonitorConfirmation { get; set; }` — a plain auto-property, same style as every existing field. No `ISettingsStore`/`JsonSettingsStore` change is needed: `JsonSettingsStore.Save`/`Load` (`src/RigToggle.Core/Persistence/JsonSettingsStore.cs` lines 27-64) serialize/deserialize the whole `AppSettings` object via `System.Text.Json` with no per-field hardcoding, so a new property round-trips automatically (confirmed by reading the store — it never enumerates fields by name).

---

### `src/RigToggle.App/SettingsForm.cs` (component — modify existing, D-02 reset)

**Analog:** itself, `BtnSaveSettings_Click` (lines 237-265):
```csharp
private void BtnSaveSettings_Click(object? sender, EventArgs e)
{
    var monitorItem = cboMonitor.SelectedItem as PickerItem;
    var audioNormalItem = cboAudioNormal.SelectedItem as PickerItem;
    var audioRigItem = cboAudioRig.SelectedItem as PickerItem;

    if (monitorItem is null || audioNormalItem is null || audioRigItem is null || !IsValidExePath(txtAppPath.Text))
    {
        return;
    }

    var settingsToSave = new AppSettings
    {
        MonitorDevicePath = monitorItem.Id,
        MonitorFriendlyName = monitorItem.DisplayLabel,
        NormalAudioDeviceId = audioNormalItem.Id,
        NormalAudioDeviceName = audioNormalItem.DisplayLabel,
        RigAudioDeviceId = audioRigItem.Id,
        RigAudioDeviceName = audioRigItem.DisplayLabel,
        CompanionAppPath = txtAppPath.Text,
    };

    _settingsStore.Save(settingsToSave);
}
```
**How to translate (per `04-RESEARCH.md` Pattern 5, lines 406-416):** compare `_settings.MonitorDevicePath` (the value loaded in `SettingsForm_Load`, line 49, still held in the `_settings` field at Save time) against the newly selected `monitorItem.Id` before constructing `settingsToSave`:
```csharp
bool monitorChanged = _settings.MonitorDevicePath != monitorItem.Id;
var settingsToSave = new AppSettings
{
    MonitorDevicePath = monitorItem.Id,
    MonitorFriendlyName = monitorItem.DisplayLabel,
    NormalAudioDeviceId = audioNormalItem.Id,
    NormalAudioDeviceName = audioNormalItem.DisplayLabel,
    RigAudioDeviceId = audioRigItem.Id,
    RigAudioDeviceName = audioRigItem.DisplayLabel,
    CompanionAppPath = txtAppPath.Text,
    SkipMonitorConfirmation = monitorChanged ? false : _settings.SkipMonitorConfirmation,
};
```
This is the same "compare previously-loaded field against the about-to-be-saved value" idiom already used implicitly by the stale-warning logic in `PopulateMonitorPicker`/`PopulateAudioCombo` (lines 98-112, 161-172) — those compare saved-vs-live at *load* time; D-02's reset compares loaded-vs-new at *save* time, same field (`_settings.MonitorDevicePath`), opposite direction.

---

### `src/RigToggle.Core/ToggleService.cs` (service — modify existing call site)

**Analog:** itself, `ToggleToRigMode` (line 66):
```csharp
var monitorState = _monitorController.CaptureState(settings.MonitorDevicePath!);
var audioState = _audioController.CaptureState();
```
**How to translate:** drop the parameter, matching `_audioController.CaptureState()`'s existing no-param call directly beneath it on line 67 (already the pattern to mirror — no new pattern to learn, just delete the argument):
```csharp
var monitorState = _monitorController.CaptureState();
var audioState = _audioController.CaptureState();
```
No other line in `ToggleService.cs` needs to change — `Disable(settings.MonitorDevicePath!)` (line 72) and `Restore(snapshot.Monitor)` (line 118) already pass the right shape of argument for the new `IMonitorController` signature (device path string / `MonitorState` object respectively).

---

### `src/RigToggle.Tests/Doubles/FakeControllers.cs` (test double — modify existing)

**Analog:** `FakeAudioController` in the same file (lines 41-87), specifically its `CaptureState()` no-param shape (lines 63-68):
```csharp
public AudioState CaptureState()
{
    _callLog.Add("audio.CaptureState");
    var roleState = new AudioRoleState(_capturedDefaultDeviceId, null);
    return new AudioState(roleState, roleState, roleState);
}
```
**Current `FakeMonitorController` to update** (lines 12-39 — `CaptureState` signature and `MonitorState` construction both need the reshape):
```csharp
public MonitorState CaptureState(string monitorDevicePath)
{
    _callLog.Add($"monitor.CaptureState:{monitorDevicePath}");
    return new MonitorState(monitorDevicePath);
}

public void Restore(MonitorState previousState)
{
    _callLog.Add($"monitor.Restore:{previousState.MonitorDevicePath}");
}
```
**How to translate:** drop the parameter on `CaptureState()` (mirroring `FakeAudioController.CaptureState()` immediately below it in the same file), and construct `MonitorState` with the new `(IReadOnlyList<MonitorPathSnapshot> Paths, string TargetDevicePath)` shape — a single-entry fake list is sufficient, same "minimal fake data, just enough to exercise call-log assertions" philosophy already used for `GetActiveMonitors()`'s single hardcoded `MonitorInfo` (line 21) and `FakeAudioController.GetPlaybackDevices()`'s single hardcoded device (line 60). Update `Restore`'s call-log message to read from the new shape (e.g. `previousState.TargetDevicePath` instead of `previousState.MonitorDevicePath`).

---

### `src/RigToggle.Tests/JsonStoreTests.cs` (test — modify existing)

**Analog:** itself — every `new MonitorState("\\\\?\\DISPLAY#PRIMARY")` call site (lines 96, 106, 121) and the corresponding assertion (line 126: `snapshot.Monitor.MonitorDevicePath`) must be updated to the new record shape. Same file already demonstrates the project's "construct inline, assert field-by-field" test style to follow for the new fields — see `SettingsStore_Save_ThenLoad_RoundTripsAllFields` (lines 46-72) for the full round-trip-and-assert-every-field convention to extend to `MonitorState`/`MonitorPathSnapshot` if a dedicated new test is added (per `04-RESEARCH.md` Pitfall 4, lines 457-461, which explicitly calls out verifying `JsonStoreTests.cs` still covers "old shape → treated as no snapshot" after the reshape).

---

## Shared Patterns

### Verify-and-throw (D-03/D-04)
**Source:** `src/RigToggle.Windows/WindowsAudioController.cs` lines 183-209 (`ApplyAndVerify`)
**Apply to:** `WindowsMonitorController.Disable`/`Restore` — every mutating `PathInfo.ApplyPathInfos(...)` call must be immediately followed by a fresh `PathInfo.GetActivePaths()` re-query and an `InvalidOperationException` throw on any mismatch. Never trust the non-throwing return of `ApplyPathInfos` alone, and never use `Screen.AllScreens` (spike Finding 2 staleness). Concrete CCD-specific version: `04-RESEARCH.md` Pattern 3 (lines 271-292).
```csharp
if (targetStillActive || !exactlyOnePrimary)
{
    throw new InvalidOperationException(
        $"Monitor disable did not take effect as expected (targetStillActive={targetStillActive}, " +
        $"exactlyOnePrimary={exactlyOnePrimary}). No further automatic recovery is attempted (D-05).");
}
```

### Fail-loudly, no automatic rollback (D-05)
**Source:** `src/RigToggle.App/MainForm.cs` lines 89-99 (existing generic `catch (Exception)` around the whole toggle click) + `src/RigToggle.Core/ToggleService.cs` lines 109-132 (`ToggleToNormalMode`'s intentionally-scoped restore try/catch — swallowed ONLY in the restore-on-toggle-back path, never in the forward `ToggleToRigMode`/`Disable` path)
**Apply to:** New `WindowsMonitorController.Disable`/`Restore` verification throws bubble all the way up to `MainForm.BtnToggle_Click`'s existing generic catch — no new exception handling needed in `MainForm`. Do NOT add a monitor-specific try/catch inside `ToggleService.ToggleToRigMode` (the forward path has no swallow-and-continue precedent; only `ToggleToNormalMode`'s restore path does, and even there each controller type is isolated so one failure doesn't block the others — see `WindowsAudioController.Restore`'s per-role `try { ApplyAndVerify(...) } catch (InvalidOperationException) { }` at lines 162-174 for the isolation-without-forward-swallow idiom, if `WindowsMonitorController.Restore` ever needs a similar per-entry isolation for multi-monitor restore).

### Modal dialog with declarative DialogResult buttons
**Source:** `src/RigToggle.App/SettingsForm.cs` lines 37-38 + `SettingsForm.Designer.cs` lines 198-215
**Apply to:** `MonitorConfirmDialog` — set `AcceptButton`/`CancelButton` in the constructor (not `FormClosing`), and give each button its `DialogResult` property in the Designer so Esc/X/click all resolve without extra code-behind.

### Composition-root wiring (no `new` of concrete adapters in Forms)
**Source:** `src/RigToggle.App/Program.cs` lines 33-51
**Apply to:** `MainForm`'s new `IMonitorController` dependency must be threaded through the existing composition root, not `new WindowsMonitorController()`'d inside `MainForm` itself — `monitorController` is already constructed at line 36 and passed to `SettingsFormFactory`; add it to the `new MainForm(...)` call at line 49 too.

## No Analog Found

None — every file in this phase's scope has a strong same-role, same-data-flow analog already in the codebase (mostly Phase 3's audio-control precedent, which this phase's own CONTEXT.md/RESEARCH.md explicitly designed to mirror).

## Metadata

**Analog search scope:** `src/RigToggle.Core/`, `src/RigToggle.Windows/`, `src/RigToggle.App/`, `src/RigToggle.Tests/`, `spike/MonitorDetachSpike/`
**Files scanned:** 18 (all `.cs` files under `src/` and `spike/`)
**Pattern extraction date:** 2026-07-24
