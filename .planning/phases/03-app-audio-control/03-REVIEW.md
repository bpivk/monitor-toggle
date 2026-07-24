---
phase: 03-app-audio-control
reviewed: 2026-07-24T17:21:05Z
depth: standard
files_reviewed: 11
files_reviewed_list:
  - src/RigToggle.Core/Models/AudioRoleState.cs
  - src/RigToggle.Core/Models/AudioState.cs
  - src/RigToggle.Core/Persistence/JsonSnapshotStore.cs
  - src/RigToggle.Core/ToggleService.cs
  - src/RigToggle.Tests/Doubles/FakeControllers.cs
  - src/RigToggle.Tests/JsonStoreTests.cs
  - src/RigToggle.Tests/ToggleServiceTests.cs
  - src/RigToggle.Windows/Audio/IPolicyConfig.cs
  - src/RigToggle.Windows/NativeMethods.cs
  - src/RigToggle.Windows/WindowsAppController.cs
  - src/RigToggle.Windows/WindowsAudioController.cs
findings:
  critical: 1
  warning: 5
  info: 1
  total: 7
status: issues_found
---

# Phase 3: Code Review Report

**Reviewed:** 2026-07-24T17:21:05Z
**Depth:** standard
**Files Reviewed:** 11
**Status:** issues_found

## Summary

The IPolicyConfig COM interop vtable layout, GUIDs, and `Marshal.ReleaseComObject` lifecycle were checked carefully (this project's own CLAUDE.md flags them as highest-risk) and are correct: the 12-slot vtable order matches the community-verified layout (SetDefaultEndpoint at 0-indexed slot 10 / "slot 11"), the CLSID/IID match documented values, `PolicyConfigClient` instances are created and released exactly once per call with no double-release or cross-cycle caching, and NAudio's `MMDeviceEnumerator`/`MMDevice` are disposed correctly (including the per-device `using` inside the enumeration loop in `GetPlaybackDevices`). `WindowsAppController`'s `Process.GetProcessesByName` results are disposed in every code path via `finally`, and the "don't `FindWindow`-by-title, don't cache defaults" constraints from CLAUDE.md are respected.

However, `WindowsAudioController.Restore` has a real correctness gap: it only falls back to friendly-name matching when the captured `DeviceId` is null/empty, never when the ID is present but stale (the actual "device unplugged since last capture" scenario the method's own doc comment claims to handle). When that happens, `ApplyAndVerify` throws, the exception is uncaught anywhere in the call chain, and it aborts the *entire* `ToggleToNormalMode` sequence — the remaining two audio roles, `MinimizeIfRunning`, and (critically) `_snapshotStore.Clear()` never run, leaving the app permanently stuck reporting "Rig" mode with no automatic recovery. This directly undermines the project's core value proposition ("just as reliably restores everything to exactly how it was before"). Several lower-severity issues (a blocking 10-second poll on what is confirmed to be the UI thread, null-forgiving operator misuse, narrow exception handling in `JsonSnapshotStore.Load`, silent broad catches with no diagnostics, and a missing reentrancy guard in `ToggleService.ToggleToRigMode`) are also documented below.

## Critical Issues

### CR-01: WindowsAudioController.Restore aborts the entire toggle-back sequence when a captured device ID is stale

**File:** `src/RigToggle.Windows/WindowsAudioController.cs:118-147` (root cause shared with `ApplyAndVerify` at 154-180)

**Issue:** `Restore`'s doc comment (lines 113-117) explicitly claims it handles "a device may have been unplugged/replaced since capture" by falling back to a friendly-name match, and that "a role with neither a usable ID nor a resolvable name is skipped rather than failing the whole restore." The implementation does not actually do this:

```csharp
string? deviceId = snapshot.DeviceId;

if (string.IsNullOrEmpty(deviceId) && !string.IsNullOrEmpty(snapshot.DeviceName))
{
    deviceId = GetPlaybackDevices()
        .FirstOrDefault(d => string.Equals(d.FriendlyName, snapshot.DeviceName, StringComparison.OrdinalIgnoreCase))
        ?.Id;
}

if (string.IsNullOrEmpty(deviceId))
{
    continue;
}

ApplyAndVerify(nativeRole, managedRole, deviceId);
```

The name-fallback branch only triggers when `DeviceId` is *null/empty* — i.e. only when `CaptureState` itself failed to read that role. It never triggers for the actual "Pitfall 4" scenario the comment describes: a role captured successfully (non-null, non-stale-at-capture-time `DeviceId`) whose device is later unplugged/replaced before restore. In that case the stale ID is passed straight to `ApplyAndVerify`, which calls `IPolicyConfig.SetDefaultEndpoint` with a device ID Windows no longer recognizes. That call is expected to return a non-zero HRESULT for an unknown/removed endpoint ID, which `ApplyAndVerify` turns into a thrown `InvalidOperationException` (line 162-164). Nothing in `Restore`, and nothing in `ToggleService.ToggleToNormalMode` (which calls `_audioController.Restore(snapshot.Audio)` with no try/catch, `src/RigToggle.Core/ToggleService.cs:107`), catches this exception. Consequences:

- The `foreach` loop over the three roles aborts on the first failing role — the other two roles (even if their devices are perfectly fine) are never restored either, contradicting "skipped rather than failing the whole restore."
- `ToggleToNormalMode` never reaches `_appController.MinimizeIfRunning(...)` or `_snapshotStore.Clear()` (`src/RigToggle.Core/ToggleService.cs:110-112`), so `IsInRigMode()` (which derives purely from snapshot-file presence, D-14) stays `true` forever for that snapshot.
- The only caller in this codebase, `MainForm.BtnToggle_Click`, wraps the call in a generic `catch (Exception)` that shows "Something went wrong while toggling. **No changes were applied.**" — which is false in this scenario: the monitor may already have been restored, and 0-2 of the 3 audio roles may already have been switched, before the exception fired. The user is left with no working "undo" path short of manually deleting `state.json`.

This is the exact class of failure ("device unplugged/replaced since capture") that the code's own documentation says is handled — it is not.

**Fix:** Use the already-existing `TryResolveDevice` helper (or equivalent) to verify the captured ID still resolves *before* trusting it, and isolate each role's failure so one bad role doesn't block the other two or the rest of `ToggleToNormalMode`:

```csharp
public void Restore(AudioState previousState)
{
    var snapshots = new (ERole Native, Role Managed, AudioRoleState Snapshot)[]
    {
        (ERole.eConsole, Role.Console, previousState.Console),
        (ERole.eMultimedia, Role.Multimedia, previousState.Multimedia),
        (ERole.eCommunications, Role.Communications, previousState.Communications),
    };

    foreach (var (nativeRole, managedRole, snapshot) in snapshots)
    {
        string? deviceId = snapshot.DeviceId;

        // Stale-ID case (device unplugged/replaced since capture) as well as the
        // never-captured case both fall through to the same name-based fallback.
        if (!string.IsNullOrEmpty(deviceId) && TryResolveDevice(deviceId) is null)
        {
            deviceId = null;
        }

        if (string.IsNullOrEmpty(deviceId) && !string.IsNullOrEmpty(snapshot.DeviceName))
        {
            deviceId = GetPlaybackDevices()
                .FirstOrDefault(d => string.Equals(d.FriendlyName, snapshot.DeviceName, StringComparison.OrdinalIgnoreCase))
                ?.Id;
        }

        if (string.IsNullOrEmpty(deviceId))
        {
            continue;
        }

        try
        {
            ApplyAndVerify(nativeRole, managedRole, deviceId);
        }
        catch (InvalidOperationException)
        {
            // Don't let one role's failure abort restore of the other two, or abort
            // the rest of ToggleToNormalMode (MinimizeIfRunning / snapshot Clear).
        }
    }
}
```
(Also consider surfacing a partial-failure signal to the caller so the UI message isn't misleadingly "no changes were applied," but that is tracked separately under CORE-04/Phase 5 per the existing docstrings.)

## Warnings

### WR-01: LaunchOrFocus blocks the calling thread for up to 10 seconds, which is confirmed to be the WinForms UI thread

**File:** `src/RigToggle.Windows/WindowsAppController.cs:63-77`
**Issue:** `LaunchOrFocus` polls `process.Refresh()`/`MainWindowHandle` with a synchronous `Thread.Sleep(250ms)` loop for up to 10 seconds:

```csharp
var deadline = DateTime.UtcNow + LaunchPollTimeout;
while (DateTime.UtcNow < deadline)
{
    process.Refresh();
    if (process.MainWindowHandle != IntPtr.Zero) { ... return; }
    Thread.Sleep(LaunchPollInterval);
}
```

`ToggleService.ToggleToRigMode` calls this synchronously, and `MainForm.BtnToggle_Click` (`src/RigToggle.App/MainForm.cs:60-88`) calls `ToggleToRigMode()` directly from a button-click event handler with no `Task.Run`/background thread. That means the entire UI freezes ("Not Responding") for up to 10 seconds every time the companion app is slow to create its main window on launch — a poor experience for a "single reliable action... one click" utility.
**Fix:** Run the poll loop off the UI thread (e.g. have `MainForm` invoke `ToggleService` methods via `Task.Run(...)` with the button disabled/showing a busy state until it completes), or make `LaunchOrFocus` itself asynchronous (`async Task` with `await Task.Delay`) and have callers await it.

### WR-02: Null-forgiving operator passes a potentially-null value into a non-nullable parameter

**File:** `src/RigToggle.Core/ToggleService.cs:110`
**Issue:** `ToggleToNormalMode` does `_appController.MinimizeIfRunning(settings.CompanionAppPath!)`. Unlike `ToggleToRigMode`, this method never calls `IsFullyConfigured`/checks `CompanionAppPath` for null/empty before use — it only loads `settings` and asserts non-null via `!`. If `ToggleToNormalMode` is ever reached with an unconfigured or partially-cleared `AppSettings` (e.g. a snapshot exists from a prior session but settings.json was since reset), `CompanionAppPath` will genuinely be `null`, and the `!` operator suppresses the compiler's nullable-reference warning for a value that is not actually guaranteed non-null. It happens to be harmless today only because `WindowsAppController.IsRunning`/`MinimizeIfRunning` independently re-check `string.IsNullOrWhiteSpace` — but the type contract (`string companionAppPath`, non-nullable) is being violated at the call site, and a future edit to either side could silently reintroduce a `NullReferenceException` (e.g. `Path.GetFileNameWithoutExtension(null)` throws `ArgumentNullException`, not caught anywhere in that call chain).
**Fix:** Guard explicitly instead of suppressing:
```csharp
if (!string.IsNullOrEmpty(settings.CompanionAppPath))
{
    _appController.MinimizeIfRunning(settings.CompanionAppPath);
}
```

### WR-03: JsonSnapshotStore.Load only guards against JsonException, not other I/O failures

**File:** `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs:46-56`
**Issue:** The documented intent (and the matching test `SnapshotStore_Load_OnMalformedFile_ReturnsNullInsteadOfThrowing`) is that a bad `state.json` should be treated as "no snapshot" rather than crash the app. The `catch` clause only catches `JsonException`:
```csharp
try
{
    return JsonSerializer.Deserialize<StateSnapshot>(File.ReadAllText(_path));
}
catch (JsonException)
{
    return null;
}
```
`File.ReadAllText` can throw `IOException` (file locked/in use, e.g. by antivirus or a concurrent write) or `UnauthorizedAccessException` (permissions), neither of which is a `JsonException`. Given that `ToggleToNormalMode` calls `Load()` with no surrounding try/catch, a transient sharing-violation on `state.json` would propagate all the way up as an unhandled exception rather than being treated the same graceful way a malformed file is.
**Fix:** Broaden the catch to cover I/O-level failures, e.g. `catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)`.

### WR-04: Broad `catch (Exception)` with zero diagnostics in CaptureState and TryResolveDevice

**File:** `src/RigToggle.Windows/WindowsAudioController.cs:66-93` (three blocks), `203-206`
**Issue:** All four `catch (Exception)` blocks in this file silently swallow every exception type (including ones indicating genuine bugs, e.g. an `ArgumentNullException` from a future refactor) and convert them into "no device"/`null` results with no logging, `Debug.WriteLine`, or any other trace. For a personal utility with no logging infrastructure at all, this means a real audio-capture failure (permissions, driver issue, or a code defect) is indistinguishable from "no default assigned to this role yet," and the user/developer has zero way to diagnose why a restore silently no-op'd.
**Fix:** At minimum, narrow the catches to the specific COM/NAudio exception types actually expected (e.g. `COMException`), and add a `Trace.WriteLine`/`Debug.WriteLine` (or a lightweight file logger) so unexpected failures leave a breadcrumb instead of vanishing entirely.

### WR-05: ToggleService.ToggleToRigMode has no internal guard against being invoked while already in Rig mode

**File:** `src/RigToggle.Core/ToggleService.cs:42-75`
**Issue:** Calling `ToggleToRigMode()` a second time while `IsInRigMode()` is already `true` would call `_monitorController.CaptureState`/`_audioController.CaptureState` against the *already-modified* (rig) state, then `_snapshotStore.Save(...)` would overwrite the previously-saved true "normal" snapshot with that already-modified state — permanently discarding the original desktop/audio configuration the user actually wants restored later. The only current caller, `MainForm.BtnToggle_Click`, happens to branch on `IsInRigMode()` first and therefore never reaches this path today, but `ToggleService` — the class whose own docstring emphasizes the "snapshot-before-mutate" guarantee (CORE-03) — provides no defense-in-depth against a different caller (a future tray-icon menu item, global hotkey handler, or a bug in the UI branch) triggering this same destructive overwrite.
**Fix:** Add an explicit guard at the top of `ToggleToRigMode`:
```csharp
if (_snapshotStore.Exists())
{
    throw new InvalidOperationException("Already in Rig Mode; switch back to Normal Mode first.");
}
```

## Info

### IN-01: CaptureState duplicates per-role logic instead of reusing the existing `Roles` table

**File:** `src/RigToggle.Windows/WindowsAudioController.cs:57-96`
**Issue:** `SetDefaultForAllRoles` and `Restore` both iterate the static `Roles`/`snapshots` arrays to avoid repeating per-role logic three times. `CaptureState` instead hand-writes three near-identical `try { using enumerator... } catch { ... }` blocks (console/multimedia/communications), each independently typed out. This is currently consistent, but the duplication means a future bug fix or behavior change (e.g. adding logging, changing the catch filter) is easy to apply to one or two blocks and forget the third.
**Fix:** Loop over the existing `Roles` array the same way `SetDefaultForAllRoles` does:
```csharp
public AudioState CaptureState()
{
    var states = new AudioRoleState[Roles.Length];
    for (int i = 0; i < Roles.Length; i++)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Roles[i].Managed);
            states[i] = new AudioRoleState(device.ID, device.FriendlyName);
        }
        catch (Exception)
        {
            states[i] = new AudioRoleState(null, null);
        }
    }
    return new AudioState(states[0], states[1], states[2]);
}
```

---

_Reviewed: 2026-07-24T17:21:05Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
