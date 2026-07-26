---
phase: 04-monitor-control-production
reviewed: 2026-07-24T21:11:15Z
depth: standard
files_reviewed: 13
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/MonitorConfirmDialog.Designer.cs
  - src/RigToggle.App/MonitorConfirmDialog.cs
  - src/RigToggle.App/Program.cs
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.Core/Abstractions/IMonitorController.cs
  - src/RigToggle.Core/Models/AppSettings.cs
  - src/RigToggle.Core/Models/MonitorPathSnapshot.cs
  - src/RigToggle.Core/Models/MonitorState.cs
  - src/RigToggle.Core/ToggleService.cs
  - src/RigToggle.Tests/Doubles/FakeControllers.cs
  - src/RigToggle.Tests/JsonStoreTests.cs
  - src/RigToggle.Windows/WindowsMonitorController.cs
findings:
  critical: 2
  warning: 6
  info: 2
  total: 10
status: issues_found
---

# Phase 4: Code Review Report

**Reviewed:** 2026-07-24T21:11:15Z
**Depth:** standard
**Files Reviewed:** 13
**Status:** issues_found

## Summary

Reviewed the monitor-control production implementation (WindowsMonitorController's CCD
disable/restore, the reflection-based `OutputTechnology` patch, ToggleService's
snapshot-before-mutate orchestration and its restore-path exception asymmetry) plus the
surrounding App-layer wiring (MainForm, SettingsForm, MonitorConfirmDialog, Program.cs)
and persistence tests.

I cross-checked the `WindowsMonitorController` logic against the actual `WindowsDisplayAPI`
1.3.0.13 source (`PathInfo.cs`, `PathTargetInfo.cs`) rather than trusting the in-repo
comments at face value. Two things I initially suspected as blockers turned out to be
non-issues once checked against the library: `PathInfo` has no `Equals`/`==` override (so
the `p != targetPath` reference-equality filter in `Disable()` is safe), and
`ApplyPathInfos()` validates via `ValidatePathInfos()` before ever calling
`SetDisplayConfig`, so an empty `pathsToApply` array (the "only one monitor connected"
edge case) fails validation and throws before any native mutation — it does **not** blank
the screen. That edge case is still a real defect, but a "confusing generic exception"
defect rather than a "blank screen" defect (see WR-01).

The most serious findings are in `ToggleService.ToggleToNormalMode()`: two divergent-file
corruption scenarios (state.json corrupted-but-present, settings.json corrupted while in
rig mode) each lead to either silent, unrecoverable data loss while reporting success, or
an unhandled null-reference crash that leaves the app permanently believing it is still in
rig mode. Both are directly reachable through code paths already demonstrated by this
phase's own tests (`JsonStoreTests.SnapshotStore_Load_OnMalformedFile_ReturnsNullInsteadOfThrowing`
plus `JsonSettingsStore.Load()`'s catch-and-degrade-to-blank behavior on malformed JSON).

The class-level remarks in `ToggleService.cs` justify swallowing audio-restore exceptions
("a genuinely-gone audio device can never succeed on retry") but the implementation is
broader than the justification: it's a bare `catch (Exception)` with no logging, and the
snapshot is unconditionally cleared afterward even for transient/retriable failures — see
WR-03/WR-04 for the detailed asymmetry analysis requested for this review.

`RigToggle.Windows` (the entire `WindowsMonitorController`, including the reflection patch)
has zero automated test coverage — `RigToggle.Tests.csproj` only references
`RigToggle.Core`, never `RigToggle.Windows` — despite this being the exact code that was
"extensively debugged live against real hardware" per this phase's history.

## Narrative Findings (AI reviewer)

## Critical Issues

### CR-01: Corrupted state.json causes silent, unrecoverable "successful" restore while monitor/audio remain unchanged

**File:** `src/RigToggle.Core/ToggleService.cs:113-133`
**Issue:**
`IsInRigMode()` (line 139) is derived purely from `_snapshotStore.Exists()` (file
presence). But `JsonSnapshotStore.Load()` (`src/RigToggle.Core/Persistence/JsonSnapshotStore.cs:39-57`)
independently catches `JsonException` and returns `null` for a malformed/truncated
`state.json` **while `Exists()` still returns `true`** for that same file (this exact
divergence is proven by this phase's own test,
`JsonStoreTests.SnapshotStore_Load_OnMalformedFile_ReturnsNullInsteadOfThrowing`).

In `ToggleToNormalMode()`:
```csharp
var snapshot = _snapshotStore.Load();

if (snapshot is not null)
{
    _monitorController.Restore(snapshot.Monitor);
    try { _audioController.Restore(snapshot.Audio); } catch (Exception) { }
}

_appController.MinimizeIfRunning(settings.CompanionAppPath!);
_snapshotStore.Clear();
```
If `snapshot` is `null` (corrupted file, `Exists()==true`), the entire `if` block —
monitor restore and audio restore — is skipped. Execution falls straight through to
`MinimizeIfRunning` and `_snapshotStore.Clear()`, which **deletes the only file that could
ever be used to recover the pre-rig-mode display/audio state** and flips
`IsInRigMode()` to `false`. The user sees "Mode: Normal" with no error, but the monitor is
still physically disabled and audio is still routed to the rig device, with no data left
to auto-recover from. This is reachable from a plain interrupted write (crash/power-loss
mid-`File.WriteAllText`/`File.Move`) or manual edit of `state.json` while in rig mode —
exactly the class of corruption `JsonSnapshotStore`'s own doc comment anticipates for
`Load()`, but `ToggleToNormalMode()` doesn't distinguish "no snapshot ever existed" from
"snapshot exists but is unreadable."

**Fix:** Distinguish the two cases and fail loudly instead of silently discarding the
snapshot:
```csharp
bool wasInRigMode = _snapshotStore.Exists();
var snapshot = _snapshotStore.Load();

if (snapshot is not null)
{
    _monitorController.Restore(snapshot.Monitor);
    try { _audioController.Restore(snapshot.Audio); } catch (Exception) { }
}
else if (wasInRigMode)
{
    // Do NOT Clear() here — preserve the corrupted file for diagnosis/manual recovery.
    throw new InvalidOperationException(
        "The saved rig-mode state file exists but could not be read (corrupted). " +
        "Your monitor and audio device were NOT restored automatically. Please fix " +
        "your display/audio settings manually, then contact support before retrying.");
}

_appController.MinimizeIfRunning(settings.CompanionAppPath!);
_snapshotStore.Clear();
```

### CR-02: Corrupted settings.json while in rig mode causes a null-reference crash that permanently strands the app in "Rig" mode

**File:** `src/RigToggle.Core/ToggleService.cs:130`
**Issue:**
`ToggleToNormalMode()` unconditionally does:
```csharp
_appController.MinimizeIfRunning(settings.CompanionAppPath!);
```
`settings` is loaded fresh from disk at the top of the method (line 113), independent of
the snapshot. `JsonSettingsStore.Load()` degrades to a fresh, all-`null`-field
`AppSettings()` on **any** `JsonException` or `IOException` (malformed JSON, 0-byte file
from an interrupted write, antivirus lock — `src/RigToggle.Core/Persistence/JsonSettingsStore.cs:34-50`,
explicitly documented there as expected/handled behavior for `settings.json`). If this
happens to `settings.json` while the app is in rig mode (snapshot present and valid), the
null-forgiving `!` on `settings.CompanionAppPath` doesn't make it non-null — it just
suppresses the compiler warning — so `MinimizeIfRunning(null)` throws.

By that point, `_monitorController.Restore(...)` and `_audioController.Restore(...)` on
the preceding lines have **already run and likely already succeeded** — but the exception
from `MinimizeIfRunning` prevents `_snapshotStore.Clear()` (line 132) from ever executing.
The physical state is now "Normal" but `IsInRigMode()` still reports `true` (file still
present), permanently showing "Mode: Rig" / "Switch to Normal Mode" in the UI. The next
toggle attempt calls `Restore()` again against an already-restored state, which may itself
fail verification (the code's own `Restore()` verify-and-throw would find the monitor
already in the expected position but through a stale/re-applied path set — behavior in that
case is unexercised and untested). The user has no way back to a consistent UI state short
of manually deleting `state.json`.

**Fix:** Don't propagate a null companion-app path into a Win32 call; degrade gracefully
instead, and always clear the (now-restored) snapshot:
```csharp
if (!string.IsNullOrEmpty(settings.CompanionAppPath))
{
    _appController.MinimizeIfRunning(settings.CompanionAppPath);
}

_snapshotStore.Clear();
```

## Warnings

### WR-01: `Disable()` gives an unhelpful generic exception instead of validating "only one active display" up front

**File:** `src/RigToggle.Windows/WindowsMonitorController.cs:117-142`
**Issue:** When the configured monitor is the GDI primary and it is the *only* active
path (`survivors.Length == 0`), the code falls through to `pathsToApply = survivors`
(an empty array) at line 139, then calls `PathInfo.ApplyPathInfos(pathsToApply, ...)` at
line 142. I verified against the `WindowsDisplayAPI` 1.3.0.13 source: `ApplyPathInfos`
calls `ValidatePathInfos`, which returns `false` for an empty path array before any native
`SetDisplayConfig` call is made — so this does *not* blank the screen — but it does throw
a generic, uninformative `PathChangeException("Invalid paths information.")` that bubbles
straight to `MainForm`'s catch-all as `"PathChangeException: Invalid paths information."`
with zero indication of the actual cause (trying to disable the system's only display).
This is a real, reachable scenario for this product (rig monitor unplugged/off, or a
laptop with only its built-in display, when "Switch to Rig Mode" is pressed).

**Fix:** Validate explicitly before attempting the mutation:
```csharp
PathInfo[] survivors = currentPaths.Where(p => p != targetPath).ToArray();

if (survivors.Length == 0)
{
    throw new InvalidOperationException(
        $"Cannot disable '{monitorDevicePath}' — it is currently the only active " +
        "display. Connect and enable another display before switching to Rig Mode.");
}
```

### WR-02: Restore() fallback's source-reservation logic doesn't account for currently-active displays outside the snapshot — can reproduce the exact source-collision bug it was written to fix

**File:** `src/RigToggle.Windows/WindowsMonitorController.cs:262-264`
**Issue:**
```csharp
var usedSources = new HashSet<PathDisplaySource>(
    resolved.Where(r => r.LiveTarget.IsPathActive).Select(r => r.LiveMatch.DisplaySource));
```
`usedSources` is built only from entries in `resolved` — i.e., only from targets that are
part of `previousState.Paths` (the snapshot being restored). It does not reserve the
`DisplaySource` of any currently-active path that isn't part of the snapshot (e.g., a
monitor plugged in *after* the snapshot was captured, while the rig was already in "Rig
mode"). The subsequent free-source pick:
```csharp
sourceToUse = allSources.FirstOrDefault(s => !usedSources.Contains(s))
    ?? throw new InvalidOperationException(...);
```
can therefore select a source that is actually in use by that unlisted-but-live monitor,
producing the exact `PathChangeException` ("Invalid paths information.") /
source-collision failure this method's own comments describe having been root-caused and
fixed for the *original* two-monitor case. The fix only closes the gap for sources used by
paths that are part of the restore set, not the general "any currently active source"
case.

**Fix:** Build `usedSources` from all currently active paths on the system, not just the
ones being restored:
```csharp
var usedSources = new HashSet<PathDisplaySource>(
    PathInfo.GetActivePaths(virtualModeAware: false).Select(p => p.DisplaySource));
```

### WR-03: Monitor-restore failure unnecessarily blocks an independent, potentially-successful audio restore

**File:** `src/RigToggle.Core/ToggleService.cs:116-127`
**Issue:** This review was specifically asked to evaluate the monitor-restore-propagates
vs. audio-restore-swallowed asymmetry. The class doc comment justifies *why* the monitor
exception must propagate (preserve the snapshot for retry) and *why* audio swallows
(a truly-gone device can't succeed on retry) — but doesn't address a side effect of the
current sequencing: because `_monitorController.Restore(snapshot.Monitor)` (line 118) is a
plain call with no `try`/`catch`, an exception there aborts the method immediately and
`_audioController.Restore(snapshot.Audio)` (line 122) — a logically independent subsystem —
never even runs. A monitor-driver hiccup that has nothing to do with audio now also leaves
the rig audio device selected as default, when the audio restore might well have succeeded
on its own. The user is left in a worse partial state than necessary.

**Fix:** Attempt audio restore regardless of monitor outcome, then re-throw the monitor
failure (preserving the original stack) so callers/tests still see it:
```csharp
Exception? monitorFailure = null;
try
{
    _monitorController.Restore(snapshot.Monitor);
}
catch (Exception ex)
{
    monitorFailure = ex; // preserve for re-throw below; snapshot NOT cleared yet
}

try
{
    _audioController.Restore(snapshot.Audio);
}
catch (Exception)
{
    // Intentionally swallowed (gap-closure 03-04): see class-level remarks.
}

if (monitorFailure is not null)
{
    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(monitorFailure).Throw();
}
```
(Adjust `MinimizeIfRunning`/`Clear()` placement to match — they should probably still be
skipped when the monitor restore failed, per the existing design intent.)

### WR-04: Audio-restore `catch (Exception)` is broader than its stated justification and has no logging

**File:** `src/RigToggle.Core/ToggleService.cs:120-127`
**Issue:** The comment justifies swallowing on the basis that "a genuinely-gone audio
device (unplugged) can never succeed on retry." But the actual `catch (Exception)` catches
*everything* — a transient COM/audio-service hiccup, a bug in `WindowsAudioController`
unrelated to device availability, etc. — not just a narrower "device not found"-shaped
exception. Two consequences: (1) genuine implementation bugs in the audio adapter are
silently hidden with zero diagnostic trail (no logging exists anywhere in this codebase),
making them effectively undebuggable for a single-user tool with no telemetry; (2) because
`_snapshotStore.Clear()` runs unconditionally right after (line 132), even a *transient*,
retriable audio failure permanently destroys the only state that would let a future retry
succeed — the "can't succeed on retry" premise is assumed for all exceptions, not verified
per-exception.

**Fix:** At minimum, catch a narrower/more specific exception type if
`WindowsAudioController` can distinguish "device gone" from other failures, and add a
minimal diagnostic trace (e.g., write to a rolling debug log or `Trace.WriteLine`) so a
silent audio-restore failure is at least forensically visible after the fact:
```csharp
catch (Exception ex)
{
    // Intentionally swallowed (gap-closure 03-04) — but leave a trace.
    System.Diagnostics.Trace.WriteLine($"Audio restore failed, continuing: {ex}");
}
```

### WR-05: Reflection patch for `PathTargetInfo.OutputTechnology` has no regression test guarding it

**File:** `src/RigToggle.Windows/WindowsMonitorController.cs:351-364`
**Issue:** `CopyOutputTechnology` reaches into `WindowsDisplayAPI`'s compiler-generated
backing field `<OutputTechnology>k__BackingField` via reflection because the library
exposes no public constructor parameter for it. The package version *is* pinned exactly
(`WindowsDisplayAPI` `Version="1.3.0.13"` in `RigToggle.Windows.csproj`, no floating
range), which mitigates an unnoticed transitive bump, but nothing in the automated test
suite exercises `CopyOutputTechnology` itself. This method doesn't require real display
hardware to test — it operates on a `PathTargetInfo` built via the library's public
constructor — so the current zero-coverage state means a routine, deliberate package
upgrade (e.g. picking up a driver-compatibility fix) could silently reintroduce the exact
`OutputTechnology=Other` bug this code was built to fix, and nothing in CI would catch it;
it would only surface again on the rig hardware, the same way it did originally.

**Fix:** Add a focused unit/reflection test (in a Windows-targeted test project — see
WR-06) that constructs a `PathTargetInfo`, calls `CopyOutputTechnology`, and asserts
`target.OutputTechnology` equals the value passed in — this fails immediately on `dotnet
restore` if the library ever changes its backing-field shape, instead of failing silently
weeks later on real hardware.

### WR-06: `RigToggle.Windows` (including `WindowsMonitorController`) has zero automated test coverage

**File:** `src/RigToggle.Tests/RigToggle.Tests.csproj` (missing `ProjectReference` to
`RigToggle.Windows`); `src/RigToggle.Windows/WindowsMonitorController.cs` (entire file)
**Issue:** `RigToggle.Tests.csproj` only has a `ProjectReference` to `RigToggle.Core` — the
entire `RigToggle.Windows` assembly, including `WindowsMonitorController`'s `Disable()`,
`Restore()` (both fast-path and fallback-reconstruction branches), and the
`CopyOutputTechnology` reflection patch, has no automated tests whatsoever. This is
precisely the code this phase's own history describes as having shipped with "several real
bugs" that were only found through live debugging on real hardware
(namespace import bug, silently-swallowed exceptions destroying recoverable state,
`TargetNotAvailableException`, source collision, the `OutputTechnology` constructor gap).
Portions of this logic (pure data transformation / reflection, like `CopyOutputTechnology`,
and the source-reservation set-building in `Restore()`'s fallback path) do not require
actual display hardware or even a Windows CI runner with attached monitors, and could be
unit-tested today with fake/constructed `PathInfo`/`PathTargetInfo` instances.

**Fix:** Add a `net10.0-windows` test target (or a dedicated test project referencing
`RigToggle.Windows`) covering at minimum: `CopyOutputTechnology`'s reflection round-trip
(WR-05), and the `Restore()` fallback's source-assignment logic given a constructed set of
active/inactive `PathInfo`s that includes a monitor outside the snapshot (to pin WR-02's
fix once applied).

## Info

### IN-01: `AppSettings.MonitorFriendlyName` persists the UI-formatted "(Primary)" suffix, not the raw friendly name

**File:** `src/RigToggle.App/SettingsForm.cs:258`
**Issue:** `AppSettings.MonitorFriendlyName` is documented as "display-cache only"
(`src/RigToggle.Core/Models/AppSettings.cs:12`), but `BtnSaveSettings_Click` sets it to
`monitorItem.DisplayLabel`, which is the ComboBox's rendered label —
`$"{FriendlyName} (Primary)"` when the monitor happened to be primary at save-time
(`PopulateMonitorPicker`, line 75). If the monitor later stops being primary (or the user
swaps roles), the cached name permanently reads "... (Primary)" until the next Settings
save. Currently inert (grep confirms `MonitorFriendlyName` is never read back for display
anywhere in the app — write-only aside from round-trip tests), but it's a latent
data-quality bug waiting for the first future feature that reads this field back.
**Fix:** Store the raw `FriendlyName` (not the formatted `DisplayLabel`) when populating
`MonitorFriendlyName`, e.g. carry the raw name separately in `PickerItem` or re-resolve it
from `_monitorController.GetActiveMonitors()` at save time.

### IN-02: Inconsistent `SelectedItem` assignment pattern between monitor and audio pickers

**File:** `src/RigToggle.App/SettingsForm.cs:104` vs `src/RigToggle.App/SettingsForm.cs:166`
**Issue:** `PopulateMonitorPicker` assigns `cboMonitor.SelectedItem = match;` (reusing the
bound instance), while `PopulateAudioCombo` assigns
`combo.SelectedItem = new PickerItem(match.Id, match.DisplayLabel);` (a freshly allocated,
value-equal instance). Functionally equivalent since `PickerItem` is a `record` with
value equality, but the inconsistency is confusing for future maintainers reasoning about
whether instance identity matters here.
**Fix:** Use `combo.SelectedItem = match;` in `PopulateAudioCombo` for consistency with
`PopulateMonitorPicker`.

---

_Reviewed: 2026-07-24T21:11:15Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
