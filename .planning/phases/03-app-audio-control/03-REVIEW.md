---
phase: 03-app-audio-control
reviewed: 2026-07-24T00:00:00Z
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
  warning: 8
  info: 3
  total: 12
status: issues_found
---

# Phase 3: Code Review Report (Re-Review After Gap-Closure 03-04)

**Reviewed:** 2026-07-24
**Depth:** standard
**Files Reviewed:** 11
**Status:** issues_found

## Summary

This is a fresh, complete re-review of Phase 3 after gap-closure plan 03-04, which was scoped only to fix CR-01 (`WindowsAudioController.Restore` never falling back to friendly-name matching for a *stale* `DeviceId`, causing an uncaught exception that aborted the rest of `ToggleToNormalMode`).

**CR-01 verification: the original defect is genuinely fixed.** `WindowsAudioController.Restore` (`src/RigToggle.Windows/WindowsAudioController.cs:126-176`) now calls `TryResolveDevice(deviceId)` before trusting a captured ID (line 139); if it resolves to nothing, `deviceId` is nulled out and control falls through to the same friendly-name lookup used for a never-captured ID (line 148). `ApplyAndVerify` for each role is now wrapped in its own `try { } catch (InvalidOperationException) { }` (lines 162-174), so one role's verify-mismatch no longer aborts the other two. `ToggleService.ToggleToNormalMode` (`src/RigToggle.Core/ToggleService.cs:109-132`) wraps both `_monitorController.Restore(...)` and `_audioController.Restore(...)` in a `try/catch (Exception)` that does not rethrow, and both `_appController.MinimizeIfRunning(...)` (line 129) and `_snapshotStore.Clear()` (line 131) sit after that block, unconditionally reachable when the try/catch swallows. `ToggleServiceTests.ToggleToNormalMode_StillMinimizesAndClears_WhenAudioRestoreThrows` exercises exactly this path with `FakeAudioController(throwOnRestore: true)` and passes. Verdict: the specific scenario in the original CR-01 (stale ID -> uncaught exception -> stuck in Rig mode forever) is resolved.

However, the fix introduces a new problem the review was specifically asked to check for, and it also leaves two related isolation gaps that weaken the guarantee the fix's own doc comment claims to provide. These are documented below as a new Critical and two new Warnings, in addition to the five previously-known, out-of-scope Warnings (WR-01 through WR-05 from the prior review), which remain present and unchanged.

## Critical Issues

### CR-01: ToggleToNormalMode's catch-all swallow hides *any* restore failure (not just audio) behind a false "success" UI, with zero logging

**File:** `src/RigToggle.Core/ToggleService.cs:109-132`, consumed by `src/RigToggle.App/MainForm.cs:60-99`

**Issue:** The gap-closure fix wraps *both* `_monitorController.Restore(snapshot.Monitor)` and `_audioController.Restore(snapshot.Audio)` in a single `try { } catch (Exception) { /* intentionally swallowed */ }` block (lines 116-126). Before this change, an exception from either call propagated out of `ToggleToNormalMode`, up through `MainForm.BtnToggle_Click`'s own `catch (Exception)` (`src/RigToggle.App/MainForm.cs:89-99`), which at least showed the user a MessageBox ("Something went wrong while toggling..."). After this change, that exception never reaches `MainForm` at all: `ToggleToNormalMode` returns normally, `RefreshUi()` runs, `lblMode.Text` flips to "Mode: Normal", and the user is given **no indication whatsoever** that the monitor and/or 1-3 audio roles were not actually restored to their prior state. There is no logging call anywhere in this catch block (confirmed: `grep` for `ILogger|Log\.|Console.WriteLine|Trace\.|EventLog` across `RigToggle.Core`/`RigToggle.Windows` returns zero matches), so the failure is not even observable to a developer after the fact -- it simply vanishes.

`WindowsMonitorController.Restore` (`src/RigToggle.Windows/WindowsMonitorController.cs:52-56`) is currently a documented no-op stub pending the real Phase 4 CCD-restore implementation, so this specific path cannot yet be exercised by monitor failures -- but the try/catch as written already covers it, so the moment Phase 4 lands a real, fallible `Restore`, any failure there will be silently and permanently invisible in exactly the same way audio failures are today. Given the product's stated core value is restoring "exactly how it was before" -- and the *monitor* restore is the single most important part of that promise -- shipping the pattern that will silently swallow its failures is a defect that should be fixed now, not discovered after Phase 4 ships on top of it.

This is a regression relative to the pre-gap-closure behavior in terms of user-observability: the prior code was *less resilient* (it got stuck in Rig mode) but at least *visibly* failed. The new code is *more resilient* (self-recovers to Normal-mode bookkeeping) but does so by converting a visible failure into an invisible one.

**Fix:** Capture the caught exception and surface it (not swallow it silently) after the recovery steps run, so the "never gets stuck" guarantee and "user finds out something didn't restore" are both satisfied:

```csharp
public void ToggleToNormalMode()
{
    var settings = _settingsStore.Load();
    var snapshot = _snapshotStore.Load();
    Exception? restoreFailure = null;

    if (snapshot is not null)
    {
        try
        {
            _monitorController.Restore(snapshot.Monitor);
        }
        catch (Exception ex)
        {
            restoreFailure = ex; // monitor and audio isolated from each other too (see WR-02)
        }

        try
        {
            _audioController.Restore(snapshot.Audio);
        }
        catch (Exception ex)
        {
            restoreFailure ??= ex;
        }
    }

    _appController.MinimizeIfRunning(settings.CompanionAppPath!);
    _snapshotStore.Clear();

    if (restoreFailure is not null)
    {
        // Rethrow (or raise an event / return a result) so MainForm can tell the user
        // "switched back to Normal mode, but restore was incomplete" instead of nothing.
        throw new ToggleRestorePartiallyFailedException(
            "Switched to Normal mode, but the previous monitor/audio state could not be fully restored.",
            restoreFailure);
    }
}
```
At minimum, add a diagnostic log call (`Trace.WriteLine`/`Debug.WriteLine`/file log) inside the existing catch even if a full UI-surfacing mechanism is deferred.

## Warnings

### WR-01: Restore's per-role isolation only catches InvalidOperationException, not any exception ApplyAndVerify can actually throw

**File:** `src/RigToggle.Windows/WindowsAudioController.cs:162-174`, `183-209`

**Issue:** The per-role loop in `Restore` isolates failures with `catch (InvalidOperationException)` only (line 166). `ApplyAndVerify` (lines 183-209), however, can also throw other exception types before it ever reaches its own explicit `throw new InvalidOperationException(...)` (line 191 or 205): `new PolicyConfigClient()` (line 185) and `Marshal.ReleaseComObject(client)` (line 197) can throw `COMException`/`ArgumentException` in COM-registration or lifetime-edge-cases, and `enumerator.GetDefaultAudioEndpoint(...)` (line 202) can throw `COMException` (e.g. `E_NOTFOUND`) if no default endpoint exists for that role at verify-time (a plausible race given the device was just resolved a moment earlier via `TryResolveDevice`/`GetPlaybackDevices`, not atomically locked). Any of these exception types escapes the per-role `catch` and aborts the `foreach` loop immediately -- the remaining, perfectly-restorable roles are never attempted. `ToggleService`'s outer catch (see CR-01) prevents this from getting the app stuck, but it silently defeats the documented "one role's restore failure never aborts restore of the other two roles" contract (doc comment at lines 119-125) for anything other than the specific verify-mismatch case.

**Fix:** Broaden the per-role catch to match the isolation guarantee actually documented:
```csharp
catch (Exception)
{
    // Per-role isolation: any failure applying/verifying this role must not prevent
    // the other two roles from being attempted.
}
```

### WR-02: ToggleToNormalMode's try/catch does not isolate monitor restore from audio restore

**File:** `src/RigToggle.Core/ToggleService.cs:116-126`

**Issue:** `_monitorController.Restore(snapshot.Monitor)` and `_audioController.Restore(snapshot.Audio)` share a single try block. If `_monitorController.Restore` throws, `_audioController.Restore` is never even attempted -- even though the audio state may be entirely independent and fully restorable. This mirrors the isolation gap in WR-01 but one level up: `WindowsAudioController.Restore` isolates failures *between its own three roles*, but `ToggleService` does not isolate failures *between the monitor controller and the audio controller*. Today this is latent (monitor `Restore` is a no-op stub), but it will become a real gap the moment Phase 4's real CCD restore can throw.

**Fix:** Give monitor and audio restore independent try/catch blocks (as shown in the CR-01 fix snippet above), so a monitor-restore failure doesn't also suppress an otherwise-successful audio restore.

### WR-03: ToggleToNormalMode's recovery guarantee stops at MinimizeIfRunning -- that call is unguarded

**File:** `src/RigToggle.Core/ToggleService.cs:129-131`; `src/RigToggle.Windows/WindowsAppController.cs:115-148`

**Issue:** The doc comment on `ToggleToNormalMode` (lines 98-107) states the try/catch exists so "`MinimizeIfRunning` and the snapshot Clear below MUST still run." But `_appController.MinimizeIfRunning(settings.CompanionAppPath!)` (line 129) is called *after* the try/catch, itself unguarded -- if it throws, `_snapshotStore.Clear()` (line 131) never runs, reproducing the exact "stuck in Rig mode" failure class gap-closure 03-04 was meant to eliminate, just relocated to the app-controller step. Today `WindowsAppController.MinimizeIfRunning` (lines 115-148) appears to avoid throwing in the common case, but it re-enumerates processes with `Process.GetProcessesByName` (line 128) and then calls `p.Refresh()` / reads `p.MainWindowHandle` (lines 133-134) without a try/catch around that specific access -- if the target process exits in the narrow window between the enumeration and the property read, `Process.MainWindowHandle`/`Refresh()` can throw `InvalidOperationException` ("process has exited"), which would propagate out of `MinimizeIfRunning` uncaught and skip `Clear()`.

**Fix:** Either move `MinimizeIfRunning` + `Clear` under their own best-effort try/catch (or a `finally`), or guarantee `_snapshotStore.Clear()` always runs via a `finally` block regardless of what happens in the steps before it:
```csharp
try
{
    if (snapshot is not null) { /* restore monitor + audio, isolated per WR-01/WR-02 */ }
    _appController.MinimizeIfRunning(settings.CompanionAppPath!);
}
finally
{
    _snapshotStore.Clear();
}
```

### WR-04: LaunchOrFocus blocks the UI thread with a synchronous Thread.Sleep poll loop up to 10s (carried forward, unchanged)

**File:** `src/RigToggle.Windows/WindowsAppController.cs:63-77`

**Issue:** `LaunchOrFocus` polls `process.MainWindowHandle` via `Thread.Sleep(LaunchPollInterval)` in a loop with up to a 10-second timeout (`LaunchPollTimeout`). Since `ToggleService.ToggleToRigMode` calls this synchronously from `MainForm.BtnToggle_Click`, the UI thread is frozen (unresponsive, "Not Responding" in Task Manager) for up to 10 seconds on every switch to Rig mode when the companion app is slow to create its main window. Unchanged from the prior review.

**Fix:** Move the poll to a background thread/`Task.Run`, or use an async `LaunchOrFocusAsync` with `await Task.Delay`, and update the UI (or disable the toggle button) while waiting instead of blocking the message pump.

### WR-05: ToggleToNormalMode uses `settings.CompanionAppPath!` on a value the type system says can be null (carried forward, unchanged)

**File:** `src/RigToggle.Core/ToggleService.cs:129`

**Issue:** `AppSettings`'s string properties are nullable, and `ToggleToNormalMode` calls `_settingsStore.Load()` and uses `settings.CompanionAppPath!` (line 129) with no equivalent to `ToggleToRigMode`'s `IsFullyConfigured` preflight guard. If settings were altered/cleared while a snapshot exists (e.g., user opens Settings and clears the companion path while nominally "in Rig mode" per the snapshot-presence flag), this passes `null!` through. In practice `WindowsAppController.IsRunning`/`MinimizeIfRunning` guard against a null/whitespace path internally (`string.IsNullOrWhiteSpace` checks), so this does not currently crash the real implementation -- but the null-forgiving operator is still unsound at the type level, and a future `IAppController` implementation (or the `FakeAppController` test double, which does not null-check its parameter) would not be protected the same way.

**Fix:** Validate/guard the loaded settings' nullability explicitly in `ToggleToNormalMode` (e.g. `settings.CompanionAppPath ?? string.Empty`) rather than relying on an unrelated class's defensive coding to make the null-forgiving operator safe in practice.

### WR-06: JsonSnapshotStore.Load only catches JsonException, not IOException/UnauthorizedAccessException (carried forward, unchanged)

**File:** `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs:39-57`

**Issue:** `Load()` only guards against malformed JSON (line 50). A locked file, a permissions error, or the file being deleted between the `Exists()` check (line 41) and `File.ReadAllText` (line 48) by another process/thread throws an uncaught `IOException`/`UnauthorizedAccessException`/`FileNotFoundException` out of `Load`, with no caller in `ToggleService` (`ToggleToNormalMode`, line 112) protecting against it. Unchanged from the prior review.

**Fix:** Broaden the catch to cover I/O failures the same way it already covers JSON-shape failures:
```csharp
catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
{
    return null;
}
```

### WR-07: Broad, unlogged `catch (Exception)` blocks in WindowsAudioController.CaptureState/TryResolveDevice (carried forward, compounded)

**File:** `src/RigToggle.Windows/WindowsAudioController.cs:60-93, 226-235`

**Issue:** `CaptureState`'s three per-role try/catch blocks and `TryResolveDevice` all catch `Exception` broadly with no logging, silently downgrading any failure (including unexpected ones, not just "device has no default") to a null/empty result. This was already flagged previously; it is now compounded by the fact that `ToggleService.ToggleToNormalMode`'s own new catch (CR-01) is equally silent, meaning there is *no* point in the restore pipeline, from device enumeration up through the UI, where a developer or user can observe that something failed.

**Fix:** At minimum add a diagnostic log line per catch (see CR-01 fix) so failures are discoverable during support/debugging, even if the UI continues to degrade gracefully.

### WR-08: No re-entrancy guard on ToggleToRigMode/ToggleToNormalMode; UI does not disable the toggle button during the operation (carried forward, unchanged)

**File:** `src/RigToggle.Core/ToggleService.cs` (whole class); `src/RigToggle.App/MainForm.cs:60-99`

**Issue:** Nothing prevents a double-click (or a click during the up-to-10-second `LaunchOrFocus` poll from WR-04) from re-entering `ToggleToRigMode`/`ToggleToNormalMode` concurrently -- `MainForm.BtnToggle_Click` does not disable `btnToggle` before calling `_toggleService.ToggleToRigMode()`/`ToggleToNormalMode()` (`src/RigToggle.App/MainForm.cs:60-99`), and `ToggleService` itself has no lock/semaphore/in-flight flag. A rapid double-click while transitioning to Rig mode could, e.g., trigger a second `_snapshotStore.Save(...)` overwriting the first snapshot with a partially-mutated state, or interleave two `LaunchOrFocus` calls. Unchanged from the prior review.

**Fix:** Disable `btnToggle` for the duration of the toggle call in `MainForm.BtnToggle_Click` (simplest fix, given WinForms is single-threaded for UI events) and/or add a `SemaphoreSlim`/boolean in-flight guard inside `ToggleService` for defense in depth.

## Info

### IN-01: JsonSnapshotStore.Save can leave an orphaned `.tmp` file if File.Move fails after File.WriteAllText succeeds

**File:** `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs:26-37`

**Issue:** `Save` writes to `tempPath` (line 35) then calls `File.Move(tempPath, _path, overwrite: true)` (line 36) with no try/catch. If the move fails (e.g. destination locked by another process, disk full mid-move, permissions change between the two calls), the `.tmp` file is left behind indefinitely with no cleanup path -- a minor litter/diagnostic-confusion issue, not correctness-critical since `_path` itself remains whatever it was before the failed `Save`.

**Fix:** Wrap the move in a try/finally that deletes the temp file on failure, or catch-cleanup-then-rethrow.

### IN-02: No logging mechanism exists anywhere in RigToggle.Core/RigToggle.Windows

**File:** repo-wide (confirmed via `grep -rn "ILogger\|Log\.|Console.WriteLine|Trace\.|EventLog"` across both projects -- zero matches)

**Issue:** Every "intentionally swallowed" exception in this phase's files (CR-01, WR-01, WR-07, and the pre-existing `JsonSnapshotStore.Load` JSON-exception swallow) relies entirely on comments to explain intent, with no runtime trace of when/how often these paths actually fire. For a personal utility this may be an acceptable trade-off, but given how many silent-catch paths this phase has accumulated, even a minimal `Trace.WriteLine`/rolling text-file logger would materially improve debuggability without adding a real dependency.

**Fix:** Add a trivial static `Trace`/`Debug`-based logging helper and call it from each swallowed-exception catch block.

### IN-03: No test exercises a throwing monitor Restore or a throwing MinimizeIfRunning in ToggleServiceTests

**File:** `src/RigToggle.Tests/ToggleServiceTests.cs:111-127`; `src/RigToggle.Tests/Doubles/FakeControllers.cs:12-39, 89-115`

**Issue:** `ToggleToNormalMode_StillMinimizesAndClears_WhenAudioRestoreThrows` (lines 111-127) proves the audio-throws case only. `FakeMonitorController` has no `throwOnRestore` equivalent, and `FakeAppController.MinimizeIfRunning` cannot be configured to throw either -- so neither WR-02 nor WR-03's failure modes (monitor restore throwing; MinimizeIfRunning throwing) are covered by any existing test, despite this being precisely the class of scenario gap-closure 03-04 targeted.

**Fix:** Add a `throwOnRestore` constructor parameter to `FakeMonitorController` and a `throwOnMinimize` parameter to `FakeAppController`, mirroring `FakeAudioController`'s existing pattern, with tests asserting `MinimizeIfRunning`/`snapshot.Clear` still run (or don't, if WR-03 is fixed to guarantee they do) in each case.

---

_Reviewed: 2026-07-24_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
