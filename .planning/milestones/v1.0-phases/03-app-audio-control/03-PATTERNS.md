# Phase 3: App & Audio Control - Pattern Map

**Mapped:** 2026-07-24
**Files analyzed:** 8 (2 new interop files, 1 new/expanded model, 3 modified production files, 2 modified test files)
**Analogs found:** 6 / 8 (2 files — the COM interop wrapper and the P/Invoke wrapper — have no in-repo functional analog; closest precedent is the disposal/lifecycle convention in sibling files, noted below)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.Windows/WindowsAppController.cs` (modify `LaunchOrFocus`/`MinimizeIfRunning`) | controller (adapter) | event-driven (poll for window-handle state change) | itself — existing `IsRunning` method in the same file | exact (in-file precedent) |
| `src/RigToggle.Windows/WindowsAudioController.cs` (modify `CaptureState`, `SetDefault`, `Restore`) | controller (adapter) | CRUD (write-then-read-back-verify) | itself — existing `GetPlaybackDevices`/`TryResolveDevice` in the same file | exact (in-file precedent) |
| `src/RigToggle.Windows/Audio/IPolicyConfig.cs` (**new**) | utility (COM interop wrapper) | request-response | none in-repo — see "No Analog Found" | no-analog (external reference: RESEARCH.md Pattern 1) |
| `src/RigToggle.Windows/NativeMethods.cs` (**new**, P/Invoke `user32.dll`) | utility (P/Invoke wrapper) | request-response | none in-repo — see "No Analog Found" | no-analog (external reference: RESEARCH.md Pattern 5) |
| `src/RigToggle.Core/Models/AudioState.cs` (modify — breaking shape change) | model | transform | `src/RigToggle.Core/Models/MonitorState.cs` + `StateSnapshot.cs` | role-match |
| `src/RigToggle.Core/Models/AudioRoleState.cs` (**new**, per-role sub-record) | model | transform | `src/RigToggle.Core/Models/AudioDeviceInfo.cs` | role-match |
| `src/RigToggle.Core/ToggleService.cs` (modify — insert D-05 preflight) | service (orchestrator) | CRUD | itself — existing `IsFullyConfigured` guard in `ToggleToRigMode` | exact (in-file precedent) |
| `src/RigToggle.Tests/Doubles/FakeControllers.cs` (modify `FakeAudioController`) | test double | CRUD | itself — same file's `FakeMonitorController`/`FakeAppController` | exact (in-file precedent) |
| `src/RigToggle.Tests/ToggleServiceTests.cs` (modify + add preflight test) | test | CRUD | itself — existing fact-method structure | exact (in-file precedent) |

## Pattern Assignments

### `src/RigToggle.Windows/WindowsAppController.cs` (controller, event-driven)

**Analog:** itself — `src/RigToggle.Windows/WindowsAppController.cs` lines 1-59 (the file this phase modifies in place)

**Imports pattern** (lines 1-2):
```csharp
using System.Diagnostics;
using RigToggle.Core.Abstractions;
```
Add `using RigToggle.Windows.Interop;` (or wherever `NativeMethods` lands) when it's split into a separate file — keep the same flat, no-alias import style already used here.

**Existing real-method disposal pattern to mirror** (lines 16-46, `IsRunning`):
```csharp
var processes = Process.GetProcessesByName(processName);
try
{
    return processes.Length > 0;
}
finally
{
    // Process.GetProcessesByName hands back IDisposable Process objects wrapping
    // native handles (WR-02) — dispose every one, not just the ones we happened
    // to read .Length from.
    foreach (var p in processes)
    {
        p.Dispose();
    }
}
```
`LaunchOrFocus`/`MinimizeIfRunning` must reuse this same "get processes, use in try, dispose all in finally" shape whenever they re-enumerate by name — do not introduce a different disposal idiom.

**Current stub bodies being replaced** (lines 48-58):
```csharp
public void LaunchOrFocus(string companionAppPath)
{
    // FAKE in Phase 2 — no-op. Real Process.Start (if not running) or
    // SetForegroundWindow (if running) via user32.dll P/Invoke lands in Phase 3.
}

public void MinimizeIfRunning(string companionAppPath)
{
    // FAKE in Phase 2 — no-op. Real ShowWindow(hWnd, SW_MINIMIZE) via user32.dll
    // P/Invoke lands in Phase 3.
}
```
**Real implementation to substitute:** RESEARCH.md Pattern 4 (`Process.Refresh()`-aware poll loop, 250ms/10s, D-06 branch: only poll on fresh launch, never on already-running-with-zero-handle) + Pattern 5 (`ShowWindow`/`SetForegroundWindow`/`IsIconic` P/Invoke signatures). Both are copy-ready code blocks in 03-RESEARCH.md lines 267-284 and 294-316.

**Doc-comment convention to preserve** (lines 6-13, class-level XML doc): every controller class opens with a `<summary>` that (a) states what's real vs. stubbed, (b) cites the research pattern/decision number that will fill the stub, and (c) calls out an explicit anti-pattern avoided (e.g. "Deliberately does NOT use FindWindow/FindWindowEx"). Update this doc comment once `LaunchOrFocus`/`MinimizeIfRunning` become real — do not leave "FAKE in Phase 2" language after the swap.

---

### `src/RigToggle.Windows/WindowsAudioController.cs` (controller, CRUD)

**Analog:** itself — `src/RigToggle.Windows/WindowsAudioController.cs` lines 1-88 (the file this phase modifies in place)

**Imports pattern** (lines 1-3):
```csharp
using NAudio.CoreAudioApi;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
```
Add `using System.Runtime.InteropServices;` (for `Marshal.ReleaseComObject`) and `using RigToggle.Windows.Audio;` (for the new `IPolicyConfig`/`PolicyConfigClient`/`ERole` types) alongside these.

**"Never cache across calls" disposal convention to mirror** (lines 37-49, current `CaptureState`, and lines 70-87, `TryResolveDevice`):
```csharp
public AudioState CaptureState()
{
    try
    {
        using var enumerator = new MMDeviceEnumerator();
        using MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return new AudioState(defaultDevice.ID);
    }
    catch (Exception)
    {
        return new AudioState(null);
    }
}
```
```csharp
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
```
`CaptureState`'s expansion to per-role (D-02) must repeat this same `using var enumerator = new MMDeviceEnumerator()` + defensive `try/catch` shape three times (once per `Role`), not introduce a single long-lived enumerator field. This is the direct in-repo precedent for the "fresh enumerator per read" rule that RESEARCH.md Pattern 2 also applies to the COM write side (`Marshal.ReleaseComObject` per cycle).

**Current stub bodies being replaced** (lines 51-60):
```csharp
public void SetDefault(string deviceId)
{
    // FAKE in Phase 2 — no-op. Real IPolicyConfig COM interop
    // (IPolicyConfig::SetDefaultEndpoint) lands in Phase 3.
}

public void Restore(AudioState previousState)
{
    // FAKE in Phase 2 — no-op. Real IPolicyConfig COM interop restore lands in Phase 3.
}
```
**Real implementation to substitute:** RESEARCH.md Pattern 2 (`SetDefaultForAllRoles` — set-all-3-roles + verify-and-throw + `Marshal.ReleaseComObject` per cycle, 03-RESEARCH.md lines 203-233), reused by both `SetDefault` (new device) and `Restore` (per-role snapshot, with `TryResolveDevice`-style ID + friendly-name fallback per Pitfall 4).

**Doc-comment convention to preserve** (lines 7-13): same "what's real vs. stubbed + cites decision" opening `<summary>` pattern as `WindowsAppController`.

---

### `src/RigToggle.Windows/Audio/IPolicyConfig.cs` (new — COM interop wrapper)

**Analog:** none in-repo (first COM-interop file in this codebase). Closest structural precedent for "wrap an external native/managed API behind a small internal type" is `src/RigToggle.Windows/WindowsMonitorController.cs` lines 1-6 (imports a third-party wrapper library, `WindowsDisplayAPI`, and maps its types into project models) — same *spirit* (isolate OS-interop risk inside `RigToggle.Windows`, per ARCHITECTURE.md's Adapter pattern already enforced by the `RigToggle.Core.csproj` "zero Windows API references" guard, `RigToggle.Core.csproj` lines 8-10), but no existing code shows raw COM vtable declarations to copy from.

**Source to copy verbatim (already verified/final in RESEARCH.md, not a draft to redesign):** 03-RESEARCH.md Pattern 1, lines 151-182 — the 12-method `IPolicyConfig` interface (`[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]`, `[PreserveSig] int` stubs 1-10 and 12, `SetDefaultEndpoint` at slot 11) plus the `PolicyConfigClient` `[ComImport]` class (`Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")`). Do not substitute a different `IPolicyConfig.cs` found elsewhere without re-counting against this 12-method list (Pitfall A in RESEARCH.md — at least one popular copy is missing `ResetDeviceFormat` and silently calls the wrong vtable slot).

**Namespace convention to follow:** `RigToggle.Windows.Audio` (per RESEARCH.md's own example, line 153) — sits alongside but separate from the flat `RigToggle.Windows` namespace used by `WindowsAppController`/`WindowsAudioController`/`WindowsMonitorController`, mirroring how those three controller classes are flat-namespaced today.

---

### `src/RigToggle.Windows/NativeMethods.cs` (new — P/Invoke wrapper)

**Analog:** none in-repo (no prior `DllImport` usage anywhere in `src/`). CLAUDE.md's explicit stack guidance (Alternatives Considered table, "hand-rolled `DllImport` signatures... not worth taking a dependency like `PInvoke.User32`") is the load-bearing constraint here, not an in-repo code analog.

**Source to copy verbatim:** 03-RESEARCH.md Pattern 5, lines 294-316 — `internal static class NativeMethods` with `ShowWindow`, `SetForegroundWindow`, `IsIconic` `[DllImport("user32.dll")]` signatures plus the `Minimize`/`RestoreIfMinimized` convenience wrappers. Only `ShowWindow`/`SetForegroundWindow` are actually called this phase (`IsIconic`/`RestoreIfMinimized` are unused by APP-01/02/03 but harmless to include per the research file, or can be trimmed to just the two calls this phase needs — planner's call).

**Placement convention:** internal, non-public class (`internal static class`) — matches this project's general preference for keeping OS-interop plumbing invisible outside `RigToggle.Windows` (the public surface is the `IAppController`/`IAudioController` interfaces in `RigToggle.Core.Abstractions`, not the interop details).

---

### `src/RigToggle.Core/Models/AudioState.cs` (model, transform — breaking shape change)

**Analog:** `src/RigToggle.Core/Models/MonitorState.cs` (same directory, same simplicity level) and `src/RigToggle.Core/Models/StateSnapshot.cs` (shows the "combine two per-domain records into one" idiom)

**Current shape being replaced** (full file, `src/RigToggle.Core/Models/AudioState.cs` lines 1-8):
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Captured default audio playback device at toggle-time, used to restore it later.
/// DefaultDeviceId is nullable to represent "no default device could be determined"
/// at capture time.
/// </summary>
public sealed record AudioState(string? DefaultDeviceId);
```

**Sibling doc-comment/record style to mirror** (`MonitorState.cs`, full file, lines 1-9):
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Captured monitor state at toggle-time, used to restore the exact prior configuration.
/// Phase-2-minimal: stores only the target monitor's device path. Phase 4 may enrich this
/// with the full DISPLAYCONFIG_PATH_INFO/MODE_INFO arrays needed for a true CCD restore
/// (02-RESEARCH.md Pitfall 7 / Pattern 1).
/// </summary>
public sealed record MonitorState(string MonitorDevicePath);
```
The one-line `sealed record` declaration + a `<summary>` that states current scope and forward-references the next phase's likely enrichment is the house style for these tiny model files — replicate it for the new `AudioState`/`AudioRoleState` shape (RESEARCH.md Pattern 3, lines 242-251):
```csharp
public sealed record AudioRoleState(string? DeviceId, string? DeviceName);

public sealed record AudioState(
    AudioRoleState Console,
    AudioRoleState Multimedia,
    AudioRoleState Communications);
```

**Downstream ripple — every call site constructing `AudioState` positionally must be updated:**
- `src/RigToggle.Windows/WindowsAudioController.cs` lines 43, 47 (`return new AudioState(defaultDevice.ID);` / `return new AudioState(null);`)
- `src/RigToggle.Tests/Doubles/FakeControllers.cs` line 61 (`return new AudioState(_capturedDefaultDeviceId);`)
- `src/RigToggle.Tests/ToggleServiceTests.cs` line 101 (`entry.StartsWith("audio.Restore:")` string assertion) and the `FakeAudioController` constructor's `_capturedDefaultDeviceId` field/parameter (line 44-50 of `FakeControllers.cs`)

**Serialization note:** `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs` (full file read, lines 1-49) needs no code change — `System.Text.Json.JsonSerializer.Serialize`/`Deserialize` picks up the new record shape automatically (line 35, 40). Per RESEARCH.md's Open Question 1, decide whether `Load()` (line 39-40) gets a defensive `try/catch` returning `null` for a stale-shaped on-disk `state.json`, or whether a manual delete is acceptable — either is fine pre-v1, planner's call.

---

### `src/RigToggle.Core/Models/AudioRoleState.cs` (new model)

**Analog:** `src/RigToggle.Core/Models/AudioDeviceInfo.cs` (full file, lines 1-8) — same "two-field id+name record" shape:
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// A single enumerated audio render (playback) endpoint, as returned by
/// IAudioController.GetPlaybackDevices(). Id is the stable identifier persisted
/// in AppSettings.NormalAudioDeviceId / RigAudioDeviceId.
/// </summary>
public sealed record AudioDeviceInfo(string Id, string FriendlyName);
```
`AudioRoleState(string? DeviceId, string? DeviceName)` should follow this exact "record with a one-line class doc + two positional fields" convention, differing only in nullability (a role may have had no default device captured) — see RESEARCH.md Pattern 3.

---

### `src/RigToggle.Core/ToggleService.cs` (service/orchestrator, CRUD)

**Analog:** itself — `src/RigToggle.Core/ToggleService.cs` lines 1-108 (the file this phase modifies in place)

**Imports pattern** (lines 1-3):
```csharp
using RigToggle.Core.Abstractions;
```
The D-05 preflight only needs `System.IO.File.Exists`, which is implicitly available via `ImplicitUsings=enable` (no new `using` needed) — confirmed by `RigToggle.Core.csproj`'s `<ImplicitUsings>enable</ImplicitUsings>`.

**Existing in-file guard-clause precedent to mirror exactly** (lines 40-52, `ToggleToRigMode`'s current first check):
```csharp
public void ToggleToRigMode()
{
    var settings = _settingsStore.Load();

    if (!IsFullyConfigured(settings))
    {
        // Guard against WR-01: without this check, an unconfigured (null-field)
        // AppSettings would still make it through to _snapshotStore.Save() below,
        // durably persisting a garbage snapshot and flipping IsInRigMode() to true
        // (D-14) even though nothing was actually captured or changed.
        throw new InvalidOperationException(
            "Rig Toggle settings are not fully configured. Open Settings and choose a monitor, both audio devices, and the companion app path before switching to Rig Mode.");
    }

    var monitorState = _monitorController.CaptureState(settings.MonitorDevicePath!);
    var audioState = _audioController.CaptureState();

    // Snapshot MUST be persisted before any mutation call (D-08/CORE-03 guarantee).
    _snapshotStore.Save(new Models.StateSnapshot(monitorState, audioState));

    _monitorController.Disable(settings.MonitorDevicePath!);
    _audioController.SetDefault(settings.RigAudioDeviceId!);
    _appController.LaunchOrFocus(settings.CompanionAppPath!);
}
```
**Insertion point for D-05:** the new `File.Exists(settings.CompanionAppPath)` preflight throw must land *after* `IsFullyConfigured` (there's no path to check until settings are known to be non-null) but *before* `_monitorController.CaptureState(...)` on the line above — i.e. as a second guard clause, same `throw new InvalidOperationException("...")` idiom, same "explain what the user should do" message style. This exactly matches RESEARCH.md's architecture diagram (03-RESEARCH.md lines 86-89) and Open Question 2's recommendation (plain inline `File.Exists`, no new interface method).

**Doc-comment convention to preserve** (lines 35-39, method-level `<summary>` above `ToggleToRigMode`): update to mention the new preflight step and its ordering guarantee, following the same "what happens, in what order, and why" style already used.

---

### `src/RigToggle.Tests/Doubles/FakeControllers.cs` (test double, CRUD)

**Analog:** itself — `src/RigToggle.Tests/Doubles/FakeControllers.cs` lines 41-73 (`FakeAudioController`)

**Current shape to update for the new `AudioState`:**
```csharp
public sealed class FakeAudioController : IAudioController
{
    private readonly List<string> _callLog;
    private readonly string? _capturedDefaultDeviceId;

    public FakeAudioController(List<string> callLog, string? capturedDefaultDeviceId = "fake-normal-device")
    {
        _callLog = callLog;
        _capturedDefaultDeviceId = capturedDefaultDeviceId;
    }
    ...
    public AudioState CaptureState()
    {
        _callLog.Add("audio.CaptureState");
        return new AudioState(_capturedDefaultDeviceId);
    }
    ...
    public void Restore(AudioState previousState)
    {
        _callLog.Add($"audio.Restore:{previousState.DefaultDeviceId}");
    }
}
```
Update the constructor param and `CaptureState`/`Restore` bodies to build/read the new `AudioState(Console, Multimedia, Communications)` shape — keep the exact same call-log-recording idiom (`_callLog.Add($"audio.Restore:{...}")`) used by every other fake in this file (`FakeMonitorController` lines 12-39, `FakeAppController` lines 75-101), just adjust what's interpolated (e.g. `previousState.Multimedia.DeviceId` instead of `previousState.DefaultDeviceId`, or log all three roles if the planner wants finer test assertions).

---

### `src/RigToggle.Tests/ToggleServiceTests.cs` (test, CRUD)

**Analog:** itself — `src/RigToggle.Tests/ToggleServiceTests.cs` lines 1-104

**Existing fixture/setup pattern to extend** (lines 16-39):
```csharp
private static readonly AppSettings ConfiguredSettings = new()
{
    MonitorDevicePath = "\\\\?\\DISPLAY#PRIMARY",
    ...
    CompanionAppPath = @"C:\Program Files\Moza\MozaCompanion.exe",
};

private static (ToggleService Service, List<string> CallLog, InMemorySnapshotStore SnapshotStore) CreateService(
    AppSettings? settings = null)
{
    var callLog = new List<string>();
    var settingsStore = new InMemorySettingsStore(settings ?? ConfiguredSettings);
    var snapshotStore = new InMemorySnapshotStore(callLog);
    var monitorController = new FakeMonitorController(callLog);
    var audioController = new FakeAudioController(callLog);
    var appController = new FakeAppController(callLog);

    var service = new ToggleService(settingsStore, snapshotStore, monitorController, audioController, appController);
    return (service, callLog, snapshotStore);
}
```
**New D-05 preflight test to add**, following the existing `[Fact]` naming/body convention (e.g. lines 41-55's `ToggleToRigMode_SavesSnapshotBeforeAnyMutationCall`):
```csharp
[Fact]
public void ToggleToRigMode_Throws_WhenCompanionAppPathDoesNotExist()
{
    var settings = ConfiguredSettings with { CompanionAppPath = @"C:\nonexistent\MozaCompanion.exe" };
    var (service, callLog, _) = CreateService(settings);

    Assert.Throws<InvalidOperationException>(() => service.ToggleToRigMode());
    Assert.DoesNotContain(callLog, entry => entry.StartsWith("snapshot.Save"));
}
```
Note `ConfiguredSettings` is a `class` (`AppSettings`), not a `record`, per `src/RigToggle.Core/Models/AppSettings.cs` (lines 9-18) — the `with` expression above will NOT compile as-is; the test must construct a fresh `AppSettings { ... }` object-initializer copy instead (or the planner adds a small clone helper). Flagging this precisely so the plan doesn't silently inherit a compile error.

**Existing `AudioState`-shaped assertion to update** (line 101):
```csharp
Assert.Contains(callLog, entry => entry.StartsWith("audio.Restore:"));
```
This assertion is shape-agnostic (string prefix match) and needs no change even after `AudioState` is restructured — but the `FakeAudioController.Restore` call-log line it depends on (see above) must still emit a string starting with `"audio.Restore:"` after the refactor.

## Shared Patterns

### COM/native resource lifecycle (never cache across calls)
**Source:** `src/RigToggle.Windows/WindowsAudioController.cs` lines 18, 41-42, 79-80 (`using var enumerator = new MMDeviceEnumerator()` created fresh per call, never as a field)
**Apply to:** `WindowsAudioController.SetDefault`/`Restore` (new `PolicyConfigClient` instances via `Marshal.ReleaseComObject` in `finally`, RESEARCH.md Pattern 2) and any COM/native handle used in `WindowsAppController` (`Process` objects disposed in `finally`, lines 32-45)
```csharp
using var enumerator = new MMDeviceEnumerator();
using MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
```

### Defensive try/catch returning a graceful "miss" value, never propagating
**Source:** `src/RigToggle.Windows/WindowsAudioController.cs` lines 39-48 (`CaptureState`) and 77-86 (`TryResolveDevice`)
**Apply to:** the per-role expansion of `CaptureState` (D-02) — each role's read should independently catch and fall back to a null-ID `AudioRoleState`, not let one role's failure abort capturing the other two.
```csharp
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
```
Contrast this with the D-03 verification path (RESEARCH.md Pattern 2), which deliberately does the opposite — throws instead of swallowing — because a mismatched *mutation* result is a real, visible failure the user must be told about, whereas a *capture*-time miss is expected/recoverable (device may be unplugged).

### Guard-clause-then-throw ordering in `ToggleService`
**Source:** `src/RigToggle.Core/ToggleService.cs` lines 44-52 (`IsFullyConfigured` check)
**Apply to:** D-05's new app-path-exists preflight — same `throw new InvalidOperationException("<user-actionable message>")` idiom, placed before any state-mutating or state-persisting call.

### Doc-comment style: state what's real vs. stubbed, cite the deciding phase/decision
**Source:** `src/RigToggle.Windows/WindowsAppController.cs` lines 6-13, `WindowsAudioController.cs` lines 7-13, `WindowsMonitorController.cs` lines 8-14
**Apply to:** all three controller classes once their stubs become real this phase — the "FAKE in Phase 2 — no-op. Real X lands in Phase 3" comments (e.g. `WindowsAppController.cs` lines 50-51, 56-57; `WindowsAudioController.cs` lines 53-54, 59) must be replaced with a description of the real mechanism and a citation of the governing decision (D-01/D-02/D-03/D-06/D-07), not simply deleted.

## No Analog Found

Files with no close in-repo match — planner should rely on 03-RESEARCH.md's own verified code blocks (these are already cross-verified, near-final code, not merely inspirational sketches):

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `src/RigToggle.Windows/Audio/IPolicyConfig.cs` | utility (COM interop) | request-response | First COM-interop file in this codebase — no prior `[ComImport]`/`[Guid]`/vtable declaration exists to pattern-match against. Use RESEARCH.md Pattern 1 (lines 151-182) verbatim; it is already cross-verified against 3 independent external sources and should not be redesigned. |
| `src/RigToggle.Windows/NativeMethods.cs` | utility (P/Invoke) | request-response | First `DllImport`/P-Invoke file in this codebase — no prior `user32.dll` signature exists. Use RESEARCH.md Pattern 5 (lines 294-316) verbatim. |

## Metadata

**Analog search scope:** `src/RigToggle.Windows/`, `src/RigToggle.Core/` (Models, Abstractions, Persistence, root), `src/RigToggle.Tests/` (Doubles, root), `spike/MonitorDetachSpike/`, plus all four `.csproj` files for dependency/constraint confirmation.
**Files scanned:** 19 (`WindowsAppController.cs`, `WindowsAudioController.cs`, `WindowsMonitorController.cs`, `ToggleService.cs`, `AudioState.cs`, `StateSnapshot.cs`, `MonitorState.cs`, `AudioDeviceInfo.cs`, `AppSettings.cs`, `IAudioController.cs`, `IAppController.cs`, `IMonitorController.cs`, `ISnapshotStore.cs`, `JsonSnapshotStore.cs`, `FakeControllers.cs`, `InMemoryStores.cs`, `ToggleServiceTests.cs`, `Program.cs` (spike), 4x `.csproj`)
**Pattern extraction date:** 2026-07-24
