# Phase 15: Optional App & Audio Targets - Pattern Map

**Mapped:** 2026-08-04
**Files analyzed:** 10 (all modified, none newly created)
**Analogs found:** 10 / 10 — every analog is a section of the SAME file being modified (this phase extends existing idioms, it does not introduce a new file role)

## File Classification

| File to Modify | Role | Data Flow | Closest Analog | Match Quality |
|-----------------|------|-----------|-----------------|---------------|
| `src/RigToggle.Core/Models/ToggleStepOutcome.cs` | model (enum) | transform | itself — existing `NotAttempted`/`Failed` cases | exact (self-extend) |
| `src/RigToggle.Core/Models/ToggleResult.cs` | model | transform | itself — existing `Success` predicate | exact (self-extend) |
| `src/RigToggle.Core/ToggleResultFormatter.cs` | utility (formatter) | transform | itself — existing `FormatChecklist` switch | exact (self-extend) |
| `src/RigToggle.Core/Abstractions/IAudioController.cs` | service (interface) | request-response | itself — existing `SetDefault`/`Restore` members | exact (self-extend) |
| `src/RigToggle.Core/ToggleService.cs` | service (orchestrator) | event-driven (stop-on-first-failure / isolate-and-continue) | itself — App-path optionality precedent (lines 70-78) is the analog for Audio's new optionality | exact (self-extend) |
| `src/RigToggle.Windows/WindowsAudioController.cs` | service (Win32/COM adapter) | request-response | itself — `TryResolveDevice` (lines 219-236) already implements the needed contract; only the interface needs the member added | exact, zero-logic-change |
| `src/RigToggle.App/SettingsForm.cs` | component (WinForms code-behind) | request-response (UI validation/save) | itself — `_pendingHotkeyModifiers`/`_pendingHotkeyKey` (lines 29-30) is the analog for the new `_pendingAppPath` field; `PickerItem`/`PopulateAudioCombo` (lines 51, 534-580) is the analog for the "(None...)" sentinel | exact (self-extend) |
| `src/RigToggle.App/SettingsForm.Designer.cs` | component (WinForms designer) | request-response | itself — `btnBrowse` (lines 305-318) is the analog for the new Clear button's styling/wiring | exact (self-extend) |
| `src/RigToggle.App/MainForm.cs` | controller (WinForms code-behind) | event-driven | itself — line 292's stale "both audio devices..." string is the target to reword | exact (self-extend) |
| `src/RigToggle.Tests/Doubles/FakeControllers.cs` | test double | request-response | itself — `FakeAudioController`'s existing `SetDefault`/`Restore` methods (lines 112-128) are the analog for the new `TryResolveDevice` fake | exact (self-extend) |
| `src/RigToggle.Tests/ToggleServiceTests.cs` | test | event-driven | itself — `ToggleToNormalMode_RestoresAudioViaRestore_NeverSetDefault` (lines 123-134) must be rewritten to assert the opposite; nearby tests are the shape template | exact (self-extend, one test inverted) |

**Note on analog methodology:** This phase is "extend two already-correct patterns to two more fields" (per RESEARCH.md's own framing) — the codebase already solved the exact problem shape being extended (App-path optionality, `_pendingHotkey*` nullable-field idiom, isolate-and-continue try/catch). There is no other subsystem in this codebase to borrow from; every analog below is a different section of the same file being edited.

---

## Pattern Assignments

### `src/RigToggle.Core/Models/ToggleStepOutcome.cs` (model, transform)

**Analog:** itself (add one enum member)

**Current full file** (4 members incl. doc comment):
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

**Change:** Add `Skipped` as a fourth case, and extend the doc comment to state the distinction explicitly (D-03: `NotAttempted` = blocked by an earlier failure; `Skipped` = deliberately unconfigured). Do NOT reuse or rename `NotAttempted`.

---

### `src/RigToggle.Core/Models/ToggleResult.cs` (model, transform)

**Analog:** itself — `Success` predicate (line 11)

**Current:**
```csharp
public sealed record ToggleResult(IReadOnlyList<ToggleStepResult> Steps)
{
    public bool Success => Steps.All(s => s.Outcome == ToggleStepOutcome.Succeeded);
}
```

**Change:** Widen to `Steps.All(s => s.Outcome is ToggleStepOutcome.Succeeded or ToggleStepOutcome.Skipped)`. This is Pitfall 3 in RESEARCH.md — the single easiest regression to miss (3 downstream call sites in `MainForm.cs` depend on `Success`, none of which mention "Skipped" by name).

---

### `src/RigToggle.Core/ToggleResultFormatter.cs` (utility/formatter, transform)

**Analog:** itself — `FormatChecklist`'s switch expression (lines 28-34)

**Current:**
```csharp
public static string FormatChecklist(ToggleResult result)
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

**Change:** Add one switch arm before the `_ =>` fallback:
```csharp
ToggleStepOutcome.Skipped => $"{step.StepName}: Skipped (not configured)",  // D-04 wording
```
This is the single shared formatter consumed identically by `MainForm`'s dialog checklist (line 360) AND its tray/hotkey balloon-tip paths (lines 608, 655) — fixing it here fixes both surfaces.

---

### `src/RigToggle.Core/Abstractions/IAudioController.cs` (service interface, request-response)

**Analog:** itself — existing 4-member interface

**Current full file:**
```csharp
public interface IAudioController
{
    IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();
    AudioState CaptureState();
    void SetDefault(string deviceId);
    void Restore(AudioState previousState);
}
```

**Change:** Add `AudioDeviceInfo? TryResolveDevice(string? deviceId);`. `WindowsAudioController.TryResolveDevice` (see below) already implements this exact signature — zero new production logic, header-only addition. `FakeAudioController` (Tests) needs a new implementation added to satisfy the interface (see Tests section below).

---

### `src/RigToggle.Windows/WindowsAudioController.cs` (service adapter, request-response)

**Analog:** itself — `TryResolveDevice` (lines 219-236), already correct, no change needed to its body

**Existing implementation (verbatim, do not modify):**
```csharp
public AudioDeviceInfo? TryResolveDevice(string? deviceId)
{
    if (string.IsNullOrEmpty(deviceId))
    {
        return null;
    }

    try
    {
        using var enumerator = new MMDeviceEnumerator();
        using MMDevice? device = enumerator.GetDevice(deviceId);
        return device is null ? null : new AudioDeviceInfo(device.ID, device.FriendlyName);
    }
    catch (Exception)
    {
        return null;
    }
}
```
This method is currently `public` on the concrete class but not part of `IAudioController` — the only required change here is none at the implementation site (it already satisfies the interface once the member is declared); the class declaration `public sealed class WindowsAudioController : IAudioController` will simply pick it up.

---

### `src/RigToggle.Core/ToggleService.cs` (service/orchestrator, event-driven)

**Analog:** itself — the existing App-path optionality precedent is the direct template for Audio's new optionality; the existing `TryExecuteStep` helper is the template for the new `TryExecuteOptionalStep` wrapper.

**Imports** (lines 1-2, unchanged):
```csharp
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
```

**Existing `TryExecuteStep` helper to extend, not duplicate** (lines 157-175):
```csharp
private static bool TryExecuteStep(string stepName, Action action, List<ToggleStepResult> steps)
{
    try
    {
        action();
        steps.Add(new ToggleStepResult(stepName, ToggleStepOutcome.Succeeded, null));
        return true;
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine($"{stepName} step failed: {ex}");
        steps.Add(new ToggleStepResult(stepName, ToggleStepOutcome.Failed, ex.Message));
        return false;
    }
}
```
Add a sibling `TryExecuteOptionalStep(string stepName, string? configuredValue, Action<string> action, List<ToggleStepResult> steps)` that appends a `Skipped` step and returns `true` (does-not-block, same contract as `Succeeded`) when `configuredValue` is null/empty, otherwise delegates to `TryExecuteStep`. Do not inline a second try/catch — call the existing helper.

**Existing App-path preflight to relocate INTO the App step body** (lines 70-78 — currently a top-level preflight that blocks the entire toggle):
```csharp
if (!File.Exists(settings.CompanionAppPath))
{
    throw new InvalidOperationException(
        $"The companion app could not be found at '{settings.CompanionAppPath}'. Open Settings and reselect the companion app path before switching to Rig Mode.");
}
```
This exact `File.Exists`-then-throw shape is the template for both (a) moving App's own check inside its step body, and (b) the new Audio "configured but broken" check using `_audioController.TryResolveDevice(deviceId) is null` in place of `File.Exists`. Match message tone exactly (D-07): one sentence problem, one sentence fix instruction ("Open Settings and reselect...").

**Rig-mode Audio/App step calls to replace** (lines 140-146):
```csharp
if (!TryExecuteStep("Audio", () => _audioController.SetDefault(settings.RigAudioDeviceId!), steps))
{
    steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));
    return new ToggleResult(steps);
}

TryExecuteStep("App", () => _appController.LaunchOrFocus(settings.CompanionAppPath!), steps);
```
Becomes (using the new `TryExecuteOptionalStep`, preserving the exact same stop-on-first-failure short-circuit shape — a `Failed` Audio step must still block App via `NotAttempted`, but a `Skipped` Audio step must NOT block App):
```csharp
if (!TryExecuteOptionalStep("Audio", settings.RigAudioDeviceId, deviceId =>
    {
        if (_audioController.TryResolveDevice(deviceId) is null)
        {
            throw new InvalidOperationException(
                "The configured Rig-mode audio device could not be found. Open Settings and reselect it.");
        }
        _audioController.SetDefault(deviceId);
    }, steps))
{
    steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));
    return new ToggleResult(steps);
}

TryExecuteOptionalStep("App", settings.CompanionAppPath, path =>
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"The companion app could not be found at '{path}'. Open Settings and reselect the companion app path before switching to Rig Mode.");
        }
        _appController.LaunchOrFocus(path);
    }, steps);
```

**Normal-mode isolate-and-continue Audio block to replace** (lines 303-317, the `snapshot.Audio`-based restore — this is the one genuine behavior change, AUDIO-04):
```csharp
Exception? audioFailure = null;
try
{
    _audioController.Restore(snapshot.Audio);
}
catch (Exception ex)
{
    System.Diagnostics.Trace.WriteLine($"Audio restore failed, continuing: {ex}");
    audioFailure = ex;
}
```
Becomes (Monitor's `try`/`catch` block at lines 273-301 stays completely untouched — Phase 16 territory; only the Audio block's body changes):
```csharp
Exception? audioFailure = null;
ToggleStepOutcome audioOutcome;
if (string.IsNullOrEmpty(settings.NormalAudioDeviceId))
{
    audioOutcome = ToggleStepOutcome.Skipped;
}
else
{
    try
    {
        if (_audioController.TryResolveDevice(settings.NormalAudioDeviceId) is null)
        {
            throw new InvalidOperationException(
                "The configured Normal-mode audio device could not be found. Open Settings and reselect it.");
        }
        _audioController.SetDefault(settings.NormalAudioDeviceId);
        audioOutcome = ToggleStepOutcome.Succeeded;
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine($"Audio switch failed, continuing: {ex}");
        audioFailure = ex;
        audioOutcome = ToggleStepOutcome.Failed;
    }
}
```
Then the existing `steps.Add(new ToggleStepResult("Audio", audioFailure is null ? ToggleStepOutcome.Succeeded : ToggleStepOutcome.Failed, audioFailure?.Message))` (lines 323-326) becomes `steps.Add(new ToggleStepResult("Audio", audioOutcome, audioFailure?.Message))`. `snapshot.Audio` becomes unread dead data after this change — leave `AudioState`/`_audioController.CaptureState()` untouched (Phase 18 cleanup scope).

**`IsFullyConfigured` gate to relax** (lines 201-205, D-05):
```csharp
private static bool IsFullyConfigured(Models.AppSettings settings) =>
    (settings.MonitorsToDisable?.Count > 0 || settings.MonitorsToEnable?.Count > 0)
    && !string.IsNullOrEmpty(settings.NormalAudioDeviceId)
    && !string.IsNullOrEmpty(settings.RigAudioDeviceId)
    && !string.IsNullOrEmpty(settings.CompanionAppPath);
```
Becomes:
```csharp
private static bool IsFullyConfigured(Models.AppSettings settings) =>
    settings.MonitorsToDisable?.Count > 0 || settings.MonitorsToEnable?.Count > 0;
```
Update the surrounding doc comment (lines 188-194, 197-200) to drop the "mirrors the four fields SettingsForm.ValidateSettingsForm requires" framing — it no longer does.

**Stale message string to fix** (lines 66-67, Pitfall 4):
```csharp
throw new InvalidOperationException(
    "Rig Toggle settings are not fully configured. Open Settings and choose at least one monitor to disable or enable, both audio devices, and the companion app path before switching to Rig Mode.");
```
Reword to only reference the monitor-set requirement, matching the new `IsFullyConfigured` gate.

---

### `src/RigToggle.App/SettingsForm.cs` (WinForms component, request-response)

**Analog:** itself — `_pendingHotkeyModifiers`/`_pendingHotkeyKey` (lines 29-30) is the direct template for the new `_pendingAppPath` field; `PickerItem` + `PopulateAudioCombo` is the template for the "(None...)" sentinel.

**Imports** (lines 1-4, unchanged):
```csharp
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using RigToggle.Windows;
```

**Field pattern to copy** (lines 26-30 — nullable, user-clearable, Save-time-persisted-directly, not derived from a Text property):
```csharp
// TRIG-01/D-01: the working (not-yet-saved) hotkey combo, initialized from
// _settings on Load and mutated only by the capture state machine below. Null
// means "no hotkey configured" (D-02 — no default is pre-filled).
private int? _pendingHotkeyModifiers;
private int? _pendingHotkeyKey;
```
Add `private string? _pendingAppPath;` following this exact idiom: initialized from `_settings.CompanionAppPath` on Load, set by Browse/drag-drop handlers, set to `null` by the new Clear button, read directly by `BtnSaveSettings_Click` (NOT derived from `txtAppPath.Text`, which stays a pure display concern — see Landmine below).

**`PickerItem` record to widen** (line 51):
```csharp
private sealed record PickerItem(string Id, string DisplayLabel);
```
Becomes `private sealed record PickerItem(string? Id, string DisplayLabel);` — `Id` becomes nullable so a sentinel instance (`new PickerItem(null, "(None — don't switch audio)")`) can represent "unset" as a real, always-present list entry.

**`PopulateAudioCombo` to extend** (lines 534-580) — prepend the sentinel to `items` unconditionally (even when zero real devices are enumerated) inside `PopulateAudioPickers` (line 528, where `items` is built) before calling `PopulateAudioCombo`, then select the sentinel explicitly when `savedId is null` instead of leaving `combo.SelectedIndex = -1` (the current line 557/570 behavior for the null case, which is *why* Save is currently blocked when unset):
```csharp
// current (lines 559-570): only handles "match found" vs "stale warning" — the
// "savedId is null" case falls through with SelectedIndex left at -1 (line 557)
if (savedId is not null)
{
    var match = items.FirstOrDefault(i => i.Id == savedId);
    if (match is not null)
    {
        combo.SelectedItem = match;
    }
    else
    {
        ShowStaleWarning(errProvider, combo, warningLabel, "audio device");
    }
}
```
Add an `else` branch selecting the sentinel (`combo.SelectedItem = items.First(i => i.Id is null)`) when `savedId is null`. No change needed to `ValidateSettingsForm`'s `cboAudioNormal.SelectedItem is PickerItem` check (line 636) — it already returns true for the sentinel.

**`PopulateAppPathField` — the Landmine to avoid** (lines 582-604):
```csharp
private void PopulateAppPathField()
{
    errApp.SetError(txtAppPath, string.Empty);
    lblAppWarning.Visible = false;

    string? savedPath = _settings.CompanionAppPath;
    if (savedPath is null)
    {
        txtAppPath.Text = "No app shortcut or .exe selected";
    }
    else
    {
        txtAppPath.Text = savedPath;
        if (!IsValidLaunchTarget(savedPath))
        {
            ShowStaleWarning(errApp, txtAppPath, lblAppWarning, "target app");
        }
    }
}
```
`"No app shortcut or .exe selected"` is a literal `.Text` VALUE, not a WinForms placeholder — `BtnSaveSettings_Click` reads `CompanionAppPath = txtAppPath.Text` directly today (line 819). Set `_pendingAppPath = savedPath;` at the top of this method, and change the Save site (line 819) to `CompanionAppPath = _pendingAppPath,` instead of `txtAppPath.Text`. `ValidateSettingsForm`'s `appPathOk` (line 638, currently `IsValidLaunchTarget(txtAppPath.Text)`) must become `_pendingAppPath is null || IsValidLaunchTarget(_pendingAppPath)` — true for cleanly-unset OR set-and-valid, false only for set-and-broken (D-06).

**`ShowStaleWarning` helper — reuse as-is, no change** (lines 606-612):
```csharp
private static void ShowStaleWarning(ErrorProvider errProvider, Control control, Label warningLabel, string noun)
{
    string message = $"Previously selected {noun} not found — please reselect.";
    errProvider.SetError(control, message);
    warningLabel.Text = message;
    warningLabel.Visible = true;
}
```
This is the "configured but now broken" warning pattern (D-06) — the new audio toggle-time error message (D-07) should read consistently with this Settings-time wording ("not found... reselect"), not introduce new terminology.

**`ValidateSettingsForm` to relax** (lines 634-688, D-06):
```csharp
private void ValidateSettingsForm()
{
    bool audioNormalOk = cboAudioNormal.SelectedItem is PickerItem;
    bool audioRigOk = cboAudioRig.SelectedItem is PickerItem;
    bool appPathOk = IsValidLaunchTarget(txtAppPath.Text);
    ...
    btnSaveSettings.Enabled = monitorOk && audioNormalOk && audioRigOk && appPathOk;
}
```
`audioNormalOk`/`audioRigOk` need NO change to their expression (sentinel already satisfies `is PickerItem`) — the actual relaxation already happened in `PopulateAudioCombo`. Only `appPathOk` needs its expression changed per the Landmine fix above. Final gate: `btnSaveSettings.Enabled = monitorOk && audioNormalOk && audioRigOk && appPathOk;` stays structurally the same, only what `appPathOk` measures changes.

**`BtnBrowse_Click`/`AppPath_DragDrop` — set `_pendingAppPath` alongside `.Text`** (lines 699-736):
```csharp
private void BtnBrowse_Click(object? sender, EventArgs e)
{
    if (dlgOpenExe.ShowDialog(this) == DialogResult.OK)
    {
        errApp.SetError(txtAppPath, string.Empty);
        lblAppWarning.Visible = false;
        txtAppPath.Text = dlgOpenExe.FileName;
        ValidateSettingsForm();
    }
}
```
Add `_pendingAppPath = dlgOpenExe.FileName;` (and the parallel line in `AppPath_DragDrop`, line 734) before `ValidateSettingsForm()`.

**New Clear button handler — same shape as `BtnBrowse_Click`, but clears instead of sets:**
```csharp
private void BtnClearAppPath_Click(object? sender, EventArgs e)
{
    _pendingAppPath = null;
    errApp.SetError(txtAppPath, string.Empty);
    lblAppWarning.Visible = false;
    PopulateAppPathField(); // re-renders the "not configured" display text
    ValidateSettingsForm();
}
```

**`BtnSaveSettings_Click` — Save-site changes** (lines 764-830):
```csharp
if (audioNormalItem is null || audioRigItem is null || !IsValidLaunchTarget(txtAppPath.Text)
    || (disableSelected.Count == 0 && enableSelected.Count == 0)
    || !WouldLeaveAtLeastOneMonitorActive(_allMonitors, disableSelected, enableSelected))
{
    return;
}
...
CompanionAppPath = txtAppPath.Text,
```
The defensive re-validation guard's `!IsValidLaunchTarget(txtAppPath.Text)` becomes `!(_pendingAppPath is null || IsValidLaunchTarget(_pendingAppPath))` (mirrors the `ValidateSettingsForm` fix, D-06 "defensive guard only" comment already at line 770-772 explains why this must mirror the enable-gate exactly). `CompanionAppPath = txtAppPath.Text` becomes `CompanionAppPath = _pendingAppPath`. `NormalAudioDeviceId = audioNormalItem.Id` / `RigAudioDeviceId = audioRigItem.Id` (lines 815, 817) need NO change — they already produce the right nullable value once `PickerItem.Id` is `string?` and the sentinel's `Id` is `null`.

---

### `src/RigToggle.App/SettingsForm.Designer.cs` (WinForms designer, request-response)

**Analog:** itself — `btnBrowse` (lines 305-318) is the exact styling/wiring template for the new Clear button

**Existing `btnBrowse` declaration to mirror:**
```csharp
//
// btnBrowse
//
this.btnBrowse.Text = "Browse…";
this.btnBrowse.Location = new System.Drawing.Point(306, 21);
this.btnBrowse.Size = new System.Drawing.Size(78, 25);
this.btnBrowse.Name = "btnBrowse";
// 12-05/THEME-05 (12-REVIEW.md CR-02): FlatStyle.Flat, not .System -- the
// Windows 11 rig proved FlatStyle.System buttons do NOT pick up dark-mode
// coloring on this runtime. ThemeApplier.ThemeButton (called from
// SettingsForm_Load and OnThemeChanged) re-asserts Flat + explicit palette
// colors, working around dotnet/winforms#13897's unreliable FlatAppearance
// auto-apply pipeline via BorderSize=0 + explicit hover/pressed overrides.
this.btnBrowse.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
this.btnBrowse.Click += new System.EventHandler(this.BtnBrowse_Click);
```
New `btnClearAppPath` must: (1) use `FlatStyle.Flat` (never `.System`, per the dotnet/winforms#13897 workaround comment above — this is a hard-won, non-obvious project convention), (2) get registered in `ThemeApplier.ThemeButton(...)` calls alongside `btnBrowse` wherever those appear (grep shows at least SettingsForm.cs:148, plus the `SettingsForm_Load`/`OnThemeChanged` handlers referenced in the comment), (3) be added to `pnlAppPath.Controls.Add(...)` (line 279's sibling list) and given `Enabled = false` initial state (D-01: "enabled only when a path is currently set" — toggled from `PopulateAppPathField`/`BtnClearAppPath_Click`/Browse/drag-drop, mirroring how `btnSaveSettings.Enabled` is toggled from `ValidateSettingsForm`).

**Panel/control registration to mirror** (lines 277-282):
```csharp
this.pnlAppPath.Controls.Add(this.lblAppPathCaption);
this.pnlAppPath.Controls.Add(this.txtAppPath);
this.pnlAppPath.Controls.Add(this.btnBrowse);
this.pnlAppPath.Controls.Add(this.lblAppWarning);
```
Add `this.pnlAppPath.Controls.Add(this.btnClearAppPath);` in this list, and a `private System.Windows.Forms.Button btnClearAppPath;` field declaration alongside `txtAppPath`/`btnBrowse`/`lblAppWarning` (lines 514-516).

Note: `txtAppPath.ReadOnly = true` is confirmed at line 295 — this is CONTEXT.md's stated reason (D-01) the Clear button is needed rather than allowing direct text deletion; do not flip `ReadOnly` to work around this instead.

---

### `src/RigToggle.App/MainForm.cs` (controller, event-driven)

**Analog:** itself — the stale message string at line 292 is the fix target; `FormatChecklist`/`Success` consumers (lines 360, 608, 655) are the "already correct once Core changes land" call sites — no changes needed there beyond what `ToggleResultFormatter`/`ToggleResult` already provide.

**Stale message to fix** (lines 285-297, Pitfall 4):
```csharp
if (!_orchestrator.IsSettingsConfigured())
{
    MessageBox.Show(
        this,
        "Please finish configuring Settings (at least one monitor to disable or enable, both audio devices, and the companion app) before switching to Rig Mode.",
        "Rig Toggle",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);
    return;
}
```
Reword to drop "both audio devices, and the companion app" — matching the new `IsFullyConfigured`/`IsSettingsConfigured` gate (D-05, monitor-set only). `grep -rn "both audio devices"` should return zero matches after this phase ships (RESEARCH.md Pitfall 4's own verification instruction).

**No change needed** at `MainForm.cs:360, 608, 655` (`ToggleResultFormatter.FormatChecklist`/`TruncateForBalloon` calls) — these consume `ToggleResult`/`ToggleStepOutcome` generically and will render `Skipped` correctly automatically once `ToggleResultFormatter.FormatChecklist` and `ToggleResult.Success` are updated in Core.

---

### `src/RigToggle.Tests/Doubles/FakeControllers.cs` (test double, request-response)

**Analog:** itself — `FakeAudioController`'s existing `SetDefault`/`Restore` methods are the shape template

**Existing class to extend** (lines 83-129):
```csharp
public sealed class FakeAudioController : IAudioController
{
    private readonly List<string> _callLog;
    private readonly string? _capturedDefaultDeviceId;
    private readonly bool _throwOnRestore;

    public FakeAudioController(
        List<string> callLog,
        string? capturedDefaultDeviceId = "fake-normal-device",
        bool throwOnRestore = false)
    {
        _callLog = callLog;
        _capturedDefaultDeviceId = capturedDefaultDeviceId;
        _throwOnRestore = throwOnRestore;
    }

    public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices() { ... }
    public AudioState CaptureState() { ... }
    public void SetDefault(string deviceId)
    {
        _callLog.Add($"audio.SetDefault:{deviceId}");
    }
    public void Restore(AudioState previousState) { ... }
}
```
Add a `TryResolveDevice` implementation matching this call-log idiom exactly, plus a constructor knob to drive both the AUDIO-05 "not found" path and the AUDIO-03/04 "found" path independently:
```csharp
private readonly bool _deviceExists;
// ... add `bool deviceExists = true` param to ctor, assign `_deviceExists = deviceExists;`

public AudioDeviceInfo? TryResolveDevice(string? deviceId)
{
    _callLog.Add($"audio.TryResolveDevice:{deviceId}");
    if (string.IsNullOrEmpty(deviceId) || !_deviceExists)
    {
        return null;
    }
    return new AudioDeviceInfo(deviceId, "Fake Speakers");
}
```
Follow the file's established call-log-string convention (`"audio.SetDefault:{deviceId}"`, `"audio.Restore:{...}"`) so `ToggleServiceTests` can assert on call order/presence the same way existing tests already do.

---

### `src/RigToggle.Tests/ToggleServiceTests.cs` (test, event-driven)

**Analog:** itself — the test to invert, plus the surrounding call-log assertion idiom

**Test that MUST be rewritten (not just updated) — currently encodes the OLD, now-wrong behavior** (lines 124-134):
```csharp
[Fact]
public void ToggleToNormalMode_RestoresAudioViaRestore_NeverSetDefault()
{
    var (service, callLog, _) = CreateService();
    service.ToggleToRigMode();
    callLog.Clear();

    service.ToggleToNormalMode();

    Assert.Contains(callLog, entry => entry.StartsWith("audio.Restore:"));
    Assert.DoesNotContain(callLog, entry => entry.StartsWith("audio.SetDefault"));
}
```
Per AUDIO-04, this assertion is now backwards. Rewrite to assert `audio.SetDefault:{NormalAudioDeviceId}` IS called and `audio.Restore` is NOT called for the Audio step, while `monitor.Restore` (unaffected, Phase 16 territory) still appears in the call log. New tests needed per RESEARCH.md's own Anti-Pattern warning ("a PR that only covers the null-field case is incomplete"): one `Skipped`-outcome test and one `Failed`-outcome test per optional field (Rig Audio, Normal Audio, Rig App, Normal App) — 8 new/changed test cases minimum, following this same `CreateService()`/`callLog`/`Assert.Contains` idiom already established throughout the file.

---

## Shared Patterns

### "Never collapse two different states into one" (codebase-wide discipline)
**Source:** `ToggleService.cs` comments throughout (e.g. lines 62-68's WR-01 guard, lines 234-241's snapshot-corruption-vs-never-existed distinction)
**Apply to:** `ToggleStepOutcome.Skipped` vs `NotAttempted` (D-03), and Audio/App "unset" vs "configured but broken" (D-05/D-06/D-07) — every optional-field change in this phase must preserve two structurally distinct branches, never a single `IsNullOrEmpty` check that silently absorbs the "broken" case too.

### Fail-fast preflight → moved into per-step guard body
**Source:** `ToggleService.cs` lines 70-78 (App-path `File.Exists` check, the direct precedent)
**Apply to:** `ToggleService.cs`'s new Audio `TryResolveDevice`-based check in both `ToggleToRigMode` and `ToggleToNormalMode` — same one-sentence-problem + one-sentence-fix message shape, same "throw inside the step body, not before capturing state" placement (required by D-04's "always 3 steps in the result").

### `_pending*` nullable field for "user-clearable value not yet safe to read from a Text property"
**Source:** `SettingsForm.cs` lines 29-30 (`_pendingHotkeyModifiers`/`_pendingHotkeyKey`)
**Apply to:** New `_pendingAppPath` field — same lifecycle (init from `_settings` on Load, mutated only by explicit user actions, read directly at Save time, `null` means "not configured").

### `ShowStaleWarning` — "configured but now broken" UI wording
**Source:** `SettingsForm.cs` lines 606-612, already used by both `lblAppWarning` and `lblAudioNormalWarning`/`lblAudioRigWarning`
**Apply to:** New toggle-time (`ToggleService.cs`) audio-not-found and app-not-found exception messages (D-07) — must read consistently with this Settings-time phrasing ("not found... reselect"), not invent new terminology.

### `TryExecuteStep` bool-return, append-to-`steps`-list contract
**Source:** `ToggleService.cs` lines 157-175
**Apply to:** New `TryExecuteOptionalStep` wrapper — must preserve the exact same `bool` "did this block the chain" contract so `ToggleToRigMode`'s existing `if (!TryExecuteStep(...)) { ...NotAttempted...; return; }` short-circuit shape needs no other changes at the call sites beyond the rename/argument addition.

### `FlatStyle.Flat` (never `.System`) for themed buttons
**Source:** `SettingsForm.Designer.cs` lines 311-317 (comment citing dotnet/winforms#13897)
**Apply to:** New `btnClearAppPath` — a hard-won, non-obvious project convention; using `.System` here would silently break dark-mode theming on this specific Windows 11 rig.

---

## No Analog Found

None. Every file in this phase's scope is a modification to existing, already-analogous code — this phase explicitly extends two already-correct patterns (App-path optionality, `_pendingHotkey*` idiom) rather than introducing new subsystem shapes, per RESEARCH.md's own framing ("the risk is not missing capability, it's accidentally re-deriving a slightly-different, slightly-wrong version of a pattern that already exists correctly elsewhere in the same file/class").

## Metadata

**Analog search scope:** `src/RigToggle.Core/`, `src/RigToggle.Windows/`, `src/RigToggle.App/`, `src/RigToggle.Tests/` — all files directly named in CONTEXT.md's canonical-refs and RESEARCH.md's Sources list; no broader codebase search was needed since every touched file already contains its own closest analog.
**Files scanned:** 10 (all read directly this session; line numbers verified against current source, not research-session snapshots)
**Pattern extraction date:** 2026-08-04
