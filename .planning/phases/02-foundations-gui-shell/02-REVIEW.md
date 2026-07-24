---
phase: 02-foundations-gui-shell
reviewed: 2026-07-24T15:59:15Z
depth: standard
files_reviewed: 25
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/MainForm.Designer.cs
  - src/RigToggle.App/Program.cs
  - src/RigToggle.App/RigToggle.App.csproj
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/SettingsForm.Designer.cs
  - src/RigToggle.Core/Abstractions/IAppController.cs
  - src/RigToggle.Core/Abstractions/IAudioController.cs
  - src/RigToggle.Core/Abstractions/IMonitorController.cs
  - src/RigToggle.Core/Abstractions/ISettingsStore.cs
  - src/RigToggle.Core/Abstractions/ISnapshotStore.cs
  - src/RigToggle.Core/Models/AppSettings.cs
  - src/RigToggle.Core/Models/AudioDeviceInfo.cs
  - src/RigToggle.Core/Models/AudioState.cs
  - src/RigToggle.Core/Models/MonitorInfo.cs
  - src/RigToggle.Core/Models/MonitorState.cs
  - src/RigToggle.Core/Models/StateSnapshot.cs
  - src/RigToggle.Core/Persistence/JsonSettingsStore.cs
  - src/RigToggle.Core/Persistence/JsonSnapshotStore.cs
  - src/RigToggle.Core/RigToggle.Core.csproj
  - src/RigToggle.Core/ToggleService.cs
  - src/RigToggle.Tests/Doubles/FakeControllers.cs
  - src/RigToggle.Tests/Doubles/InMemoryStores.cs
  - src/RigToggle.Tests/JsonStoreTests.cs
  - src/RigToggle.Tests/ToggleServiceTests.cs
  - src/RigToggle.Windows/RigToggle.Windows.csproj
  - src/RigToggle.Windows/WindowsAppController.cs
  - src/RigToggle.Windows/WindowsAudioController.cs
  - src/RigToggle.Windows/WindowsMonitorController.cs
findings:
  critical: 1
  warning: 3
  info: 1
  total: 5
status: issues_found
---

# Phase 02: Code Review Report

**Reviewed:** 2026-07-24T15:59:15Z
**Depth:** standard
**Files Reviewed:** 25 (of 29 listed; RigToggle.Tests.csproj and RigToggle.App.csproj/RigToggle.Windows.csproj are build manifests reviewed for correctness but not separately itemized as findings sources)
**Status:** issues_found

## Summary

Reviewed the full Phase 2 slice: GUI shell (MainForm/SettingsForm), the Core persistence + orchestration layer (ToggleService, JsonSettingsStore, JsonSnapshotStore), and the real (non-stub) portions of the Windows adapters (monitor/audio enumeration, companion-process detection). Per the phase scope, `Disable`/`Restore`/`SetDefault`/`LaunchOrFocus`/`MinimizeIfRunning` no-op stub bodies were **not** flagged — that is the deliberate Phase 3/4 boundary.

The architecture (interfaces-only Core, real Windows adapters isolated in RigToggle.Windows, composition root in Program.cs) is clean and matches the documented conventions closely, and the hand-written test doubles / xUnit tests correctly exercise the snapshot-before-mutate sequencing.

One genuine crash-class bug survived: neither `JsonSettingsStore.Load()` nor its two unguarded call sites (`MainForm.OnLoad` and `MainForm.BtnSettings_Click`) handle malformed JSON, and the project's own stack notes explicitly invite the user to hand-edit `settings.json` — a plausible typo there takes down the whole app with no recovery path. Three further robustness/quality gaps are documented below (missing settings-completeness guard before toggling, and two un-disposed native/COM resource leaks in the Windows adapters that fire on every Settings-open / status-refresh).

## Critical Issues

### CR-01: Malformed settings.json crashes the app with no recovery path

**File:** `src/RigToggle.Core/Persistence/JsonSettingsStore.cs:35`
**Issue:**
```csharp
var json = File.ReadAllText(_path);
return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
```
`Load()` does not guard against `JsonException` (malformed/truncated/empty JSON). This method's doc comment explicitly cites `SETTINGS-04`/`T-02-CORRUPT` as the concern it's meant to address, and the project's own STACK notes justify choosing plain JSON specifically because "the user should be able to hand-edit" it — so a hand-edit typo, an antivirus interrupting a write, or a 0-byte file (all realistic on a personal utility) throws `JsonException` here.

That exception is **unguarded at both of its production call sites**:
- `src/RigToggle.App/MainForm.cs:52` — `RefreshUi()` calls `_settingsStore.Load()`, and is itself called from `OnLoad` (`MainForm.cs:35-39`) with no surrounding try/catch. A corrupt settings file crashes the app before the main window even renders.
- `src/RigToggle.App/SettingsForm.cs:49` — `SettingsForm_Load` calls `_settingsStore.Load()` directly, and is invoked via `settingsForm.ShowDialog(this)` from `MainForm.BtnSettings_Click` (`MainForm.cs:88-96`), which has **no try/catch at all** (unlike `BtnToggle_Click`, which does wrap its `ToggleService` calls). Opening Settings with a corrupt settings.json crashes/terminates the app.

Contrast this with the rest of the codebase, which is otherwise careful about this exact failure mode: `WindowsAudioController.CaptureState()` wraps its read in try/catch, and `SettingsForm.PopulateMonitorPicker`/`PopulateAudioPickers` both explicitly defend enumeration failures with "Defensive: ... should not crash Settings open; degrade to empty-state." `JsonSettingsStore.Load()` and its two callers are the one place this pattern was dropped.

**Fix:** Catch `JsonException` (and ideally `IOException`) in `JsonSettingsStore.Load()` and degrade to a fresh `AppSettings()` (mirroring the existing "missing file" branch), e.g.:
```csharp
public AppSettings Load()
{
    if (!File.Exists(_path))
    {
        return new AppSettings();
    }

    try
    {
        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
    }
    catch (JsonException)
    {
        return new AppSettings();
    }
}
```
This is the minimal fix; consider also wrapping `RefreshUi()`/`BtnSettings_Click` defensively at the UI layer as defense-in-depth, consistent with the try/catch already present around `BtnToggle_Click`.

## Warnings

### WR-01: No settings-completeness guard before ToggleToRigMode — silently persists a null-valued snapshot and flips mode to "Rig"

**File:** `src/RigToggle.Core/ToggleService.cs:44,50-52` and `src/RigToggle.App/MainForm.cs:60-86`
**Issue:** `ToggleToRigMode()` reads settings and immediately does:
```csharp
var monitorState = _monitorController.CaptureState(settings.MonitorDevicePath!);
...
_monitorController.Disable(settings.MonitorDevicePath!);
_audioController.SetDefault(settings.RigAudioDeviceId!);
_appController.LaunchOrFocus(settings.CompanionAppPath!);
```
The `!` null-forgiving operators silence the compiler but there is no runtime check that these fields are actually populated. `MainForm.BtnToggle_Click` (lines 60-86) enables/calls the toggle unconditionally — there is no check anywhere that Settings has ever been saved with valid values before allowing "Switch to Rig Mode."

Concretely: on first run (before the user ever opens Settings), clicking "Switch to Rig Mode" today does not crash (all mutation calls are Phase-2 no-ops), but it **does** call the real `_snapshotStore.Save(...)` with a `StateSnapshot` containing a `null` `MonitorDevicePath`, and `IsInRigMode()` (derived from snapshot-file presence, D-14) flips to `true` — so the UI now claims "Mode: Rig" even though nothing was configured or done, and that garbage state is durably persisted to `state.json` across app restarts. `ToggleServiceTests` only exercises the fully-configured `ConfiguredSettings` case, so this path is untested. Once Phase 3/4 replace the no-op stubs with real Win32/COM calls, the same unconfigured-settings path will throw from inside real mutation code — but only *after* the snapshot has already been durably saved, leaving `IsInRigMode()==true` with no monitor/audio actually changed.

**Fix:** Add an explicit "is settings fully configured" check (mirroring the same four fields `SettingsForm.ValidateSettingsForm` already validates) before allowing `ToggleToRigMode()` to run — either disable `btnToggle` in `MainForm` when settings are incomplete, or have `BtnToggle_Click` check and redirect the user to Settings with a friendly message instead of proceeding.

### WR-02: `Process` objects from `Process.GetProcessesByName` are never disposed

**File:** `src/RigToggle.Windows/WindowsAppController.cs:31`
**Issue:**
```csharp
return Process.GetProcessesByName(processName).Length > 0;
```
`Process.GetProcessesByName` returns an array of `IDisposable` `Process` objects, each wrapping a native process handle. None of them are disposed here — Microsoft's own docs call out that these should be disposed by the caller. This method is called from `MainForm.RefreshUi()` (`MainForm.cs:54`), which itself runs on every `OnLoad`, after every toggle, and after every Settings-dialog close — so handles accumulate for the lifetime of the app until finalized by the GC.

**Fix:**
```csharp
var processes = Process.GetProcessesByName(processName);
try
{
    return processes.Length > 0;
}
finally
{
    foreach (var p in processes)
    {
        p.Dispose();
    }
}
```

### WR-03: `MMDevice` COM wrappers are never disposed in `WindowsAudioController`

**File:** `src/RigToggle.Windows/WindowsAudioController.cs:21-24, 37, 75`
**Issue:** All three real methods obtain NAudio `MMDevice` instances (each wrapping a native/COM `IMMDevice` reference) and read only `.ID`/`.FriendlyName` off them, without disposing:
- `GetPlaybackDevices()` — the `foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(...))` loop never disposes `device`.
- `CaptureState()` — `defaultDevice` is read and returned from, never disposed.
- `TryResolveDevice()` — `device` is never disposed.

`MMDeviceEnumerator` is correctly scoped with `using` in all three methods, but that does not dispose the individual `MMDevice` objects it hands out. Per `SettingsForm`'s own doc comment (D-11), the Settings dialog "re-enumerate[s] on every open — no manual Refresh control exists," so `GetPlaybackDevices()` runs (and leaks one `MMDevice` per active render endpoint) every single time the user opens Settings; `CaptureState()` similarly runs on every rig-mode toggle.

**Fix:** Wrap each device usage in its own `using`/dispose, e.g.:
```csharp
foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
{
    using (device)
    {
        result.Add(new AudioDeviceInfo(device.ID, device.FriendlyName));
    }
}
```
and similarly for `defaultDevice` in `CaptureState()` and `device` in `TryResolveDevice()`.

## Info

### IN-01: `JsonSnapshotStore.Load()` has the same unguarded-deserialize shape as CR-01, but is reachable only through an already-guarded path

**File:** `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs:40`
**Issue:** `Load()` (`Exists() ? JsonSerializer.Deserialize<StateSnapshot>(File.ReadAllText(_path)) : null`) will throw `JsonException` on a corrupted `state.json`, exactly like CR-01. Today this is only reached via `ToggleService.ToggleToNormalMode()`, which is called from `MainForm.BtnToggle_Click` inside its try/catch — so it won't crash the app. However, if it throws, `_snapshotStore.Clear()` (the last line of `ToggleToNormalMode`) never runs, permanently sticking the app in "Mode: Rig" with every subsequent "Switch to Normal Mode" attempt failing identically, and no way to recover short of manually deleting `state.json`.
**Fix:** Apply the same `catch (JsonException)` treatment as suggested for CR-01, likely falling back to `null` (i.e., "no snapshot to restore, but still allow `Clear()` to proceed") rather than swallowing the mode-stuck side effect.

---

_Reviewed: 2026-07-24T15:59:15Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
