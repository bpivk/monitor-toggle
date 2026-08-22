---
phase: 26-auto-update
reviewed: 2026-08-22T00:00:00Z
depth: standard
files_reviewed: 32
files_reviewed_list:
  - .github/workflows/release.yml
  - src/RigToggle.App/MainForm.Designer.cs
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/Program.cs
  - src/RigToggle.App/ReleaseNotesRenderer.cs
  - src/RigToggle.App/RigToggle.App.csproj
  - src/RigToggle.App/SettingsForm.Designer.cs
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/ThemeApplier.cs
  - src/RigToggle.App/UpdateApplyEntryPoint.cs
  - src/RigToggle.App/UpdatePromptDialog.Designer.cs
  - src/RigToggle.App/UpdatePromptDialog.cs
  - src/RigToggle.App/UpdateRollbackChecker.cs
  - src/RigToggle.Core/Abstractions/IReleaseFeed.cs
  - src/RigToggle.Core/Abstractions/IUpdateAppliedMarkerStore.cs
  - src/RigToggle.Core/Abstractions/IUpdateApplier.cs
  - src/RigToggle.Core/GitHubReleaseFeed.cs
  - src/RigToggle.Core/Models/AppSettings.cs
  - src/RigToggle.Core/Models/ReleaseInfo.cs
  - src/RigToggle.Core/Models/UpdateAppliedMarker.cs
  - src/RigToggle.Core/Persistence/JsonUpdateAppliedMarkerStore.cs
  - src/RigToggle.Core/ReleaseNotesFormatter.cs
  - src/RigToggle.Core/UpdateChecksum.cs
  - src/RigToggle.Core/UpdateOrchestrator.cs
  - src/RigToggle.Core/UpdateVersionComparer.cs
  - src/RigToggle.Tests/GitHubReleaseFeedTests.cs
  - src/RigToggle.Tests/JsonUpdateAppliedMarkerStoreTests.cs
  - src/RigToggle.Tests/ReleaseNotesFormatterTests.cs
  - src/RigToggle.Tests/UpdateChecksumTests.cs
  - src/RigToggle.Tests/UpdateOrchestratorTests.cs
  - src/RigToggle.Tests/UpdateVersionComparerTests.cs
  - src/RigToggle.Windows.Tests/UpdateApplyProcessTests.cs
  - src/RigToggle.Windows/WindowsUpdateApplier.cs
findings:
  critical: 2
  warning: 3
  info: 1
  total: 6
status: issues_found
---

# Phase 26: Code Review Report

**Reviewed:** 2026-08-22T00:00:00Z
**Depth:** standard
**Files Reviewed:** 32
**Status:** issues_found

## Summary

Reviewed the GitHub-releases auto-update feature (Phase 26): the fetch/compare/
confirm/apply sequencer (`UpdateOrchestrator`), the GitHub feed reader
(`GitHubReleaseFeed`), the checksum-verification/self-replace applier
(`WindowsUpdateApplier`/`UpdateApplyEntryPoint`), the crash-detection auto-rollback
state machine (`UpdateRollbackChecker`/`UpdateAppliedMarker`), the release-notes
renderer, the new App-layer entry points (tray menu item, Settings button, on-launch
check) wired into `MainForm.cs`/`SettingsForm.cs`/`Program.cs`, and the release CI
workflow. Most of this code is careful and well-documented, and the SSRF/host-
allowlist and checksum fail-closed logic are both sound in isolation. However, two
issues undermine the feature's core reliability promise: the two independent
update-check entry points (automatic on-launch and manual tray/Settings) share no
mutual-exclusion guard, unlike every other concurrent-mutation path in this codebase
(`ToggleOrchestrator`'s `_busy` flag, `BeginExclusiveMonitorAccess`); and the
auto-rollback "confirmed healthy" signal is a bare 10-second UI timer with no
other path to clear it, so an ordinary quick exit after an auto-applied update is
indistinguishable from a crash and silently triggers a downgrade-and-revert with a
misleading "update failed to start" message on the next launch.

## Critical Issues

### CR-01: No mutual exclusion between automatic and manual update checks — concurrent invocation can double-apply

**File:** `src/RigToggle.App/MainForm.cs:2037` (`RunAutomaticUpdateCheckAsync`) and `src/RigToggle.App/MainForm.cs:2149` (`PerformManualUpdateCheckAsync`); shared sequencer at `src/RigToggle.Core/UpdateOrchestrator.cs:131` (`CheckAsync`)

**Issue:** `RunAutomaticUpdateCheckAsync` is fired via `mainForm.BeginInvoke` right after
startup (`Program.cs:405`), and independently, the user can trigger
`PerformManualUpdateCheck()` at any time from the tray menu
(`TrayCheckUpdatesMenuItem_Click`) or Settings (`BtnCheckForUpdates_Click`). Both
paths call into the same `UpdateOrchestrator` instance with no shared "busy" flag,
lock, or `Interlocked` guard of any kind — a stark contrast with this codebase's own
established convention for exactly this class of hazard
(`ToggleOrchestrator.BeginExclusiveMonitorAccess`/`_busy`, and `MainForm`'s own
`TryAcquireMonitorAccess` comments explicitly noting that `ShowDialog()` runs a
nested message loop that dispatches queued messages, including tray-menu commands).

`ShowUpdatePromptDialog` (`MainForm.cs:2092`) calls `dialog.ShowDialog(this)`, which
pumps a nested message loop. While the automatic check's confirm dialog is open, a
user can click the tray's "Check for Updates" item — the nested pump will dispatch
that click, invoking `PerformManualUpdateCheckAsync` reentrantly from inside the
first dialog's own pump. This can construct and show a second `UpdatePromptDialog`
stacked on the first, and if either resolves to `UpdateNow` first, that call proceeds
to `DownloadAndStageAsync`/`ApplyAndRelaunch`/`Application.Exit()` while the other
check's async chain is still suspended awaiting its own (now orphaned) dialog. Two
concurrent `DownloadAndStageAsync` calls would race on the exact same
`FileShare.None`-locked staging path (`WindowsUpdateApplier.cs:75`,
`RigToggle.App.update.exe`), and two `ApplyAndRelaunch` calls could spawn two helper
processes and two `Application.Exit()` calls against a single process. Best case this
produces a confusing double-dialog UX; worst case it produces a failed/corrupted
apply or duplicate relaunch.

**Fix:** Add a shared, `Interlocked`-guarded "update check in progress" flag (mirroring
`ToggleOrchestrator`'s `_busy` pattern) inside `UpdateOrchestrator` or at the
`MainForm` call sites, and reject/no-op a second concurrent
`CheckOnLaunchAsync`/`CheckOnDemandAsync` call while one is already in flight —
e.g.:

```csharp
private int _updateCheckInProgress;

public async Task PerformManualUpdateCheckAsync()
{
    if (Interlocked.CompareExchange(ref _updateCheckInProgress, 1, 0) != 0)
    {
        // Already checking (automatic or another manual check) — no-op.
        return;
    }
    try { /* existing body */ }
    finally { Interlocked.Exchange(ref _updateCheckInProgress, 0); }
}
```

Apply the same guard to `RunAutomaticUpdateCheckAsync`.

### CR-02: Auto-rollback "confirmed healthy" signal is a bare 10-second timer — a normal quick exit is indistinguishable from a crash and triggers a false revert

**File:** `src/RigToggle.App/MainForm.cs:2227` (`BeginUpdateHealthWatch`), `src/RigToggle.App/UpdateRollbackChecker.cs:37` (`Run`) and `:214` (`ConfirmHealthy`)

**Issue:** `ConfirmHealthy` — the only code path that clears the
`UpdateMarkerStage.FirstLaunchAttempted` marker short of a full revert — is called
from exactly one place: a one-shot `System.Windows.Forms.Timer` with a 10-second
interval, started from `Program.cs` before `Application.Run`. There is no other route
to "confirmed healthy" (verified: `ConfirmHealthy` has a single caller in the
codebase). If the user closes the app (tray "Exit", or Windows shutdown/logoff) within
10 seconds of an auto-applied update's first launch — a plausible scenario, since the
user just clicked "Update Now" and may reasonably expect to be done — the timer never
fires, and the marker is left at `FirstLaunchAttempted`.

On the *next* launch, `UpdateRollbackChecker.Run`'s `FirstLaunchAttempted` branch
(`:71`) unconditionally treats this as "the previous boot of the new version never
reached confirmed-healthy" and executes `RunRestoreBranch`, which renames the
(perfectly healthy) new exe to `.failed`, restores the `.bak` (the old version) into
its place, and relaunches it. The user sees (via `MainForm.ShowUpdateRevertNotice`,
`UpdateRollbackChecker.cs:88`) `"Update to v{new} failed to start — reverted to
v{previous}."` — a false and misleading message, since the update did not fail; the
user simply exited quickly. This silently downgrades a working install and
misattributes the cause.

**Fix:** Decouple "confirmed healthy" from wall-clock UI-thread survival. Options:
persist a lower-cost "reached OnLoad/tray-ready" signal immediately (already true by
the time `BeginUpdateHealthWatch` is armed) combined with recording a graceful-exit
flag from `TrayExitMenuItem_Click`/`FormClosing` that also calls `ConfirmHealthy`
(a clean exit is strong evidence the new build is not crash-looping); or shorten the
window and/or also confirm on `Application.Idle`/first successful toggle rather than
solely a fixed timer that a normal exit can race. At minimum, a graceful
`Application.Exit()` path should call `UpdateRollbackChecker.ConfirmHealthy` (or
equivalent) so intentionally closing the app is never conflated with a crash.

## Warnings

### WR-01: Copied update-relaunch helper exe in %TEMP% is never cleaned up

**File:** `src/RigToggle.Windows/WindowsUpdateApplier.cs:123-151` (`ApplyAndRelaunch`)

**Issue:** Every applied update copies the entire running self-contained single-file
exe (per `CLAUDE.md`, on the order of ~150 MB) to
`Path.Combine(Path.GetTempPath(), $"RigToggle-updater-{Environment.ProcessId}.exe")`
and launches it as the swap helper. Nothing in this class, `UpdateApplyEntryPoint`, or
`UpdateRollbackChecker` ever deletes this file afterward (confirmed via repo-wide
search — the only other reference to this path is where it is created). Across the
app's update lifetime, this leaves one orphaned ~150 MB file per applied update in the
user's `%TEMP%` directory indefinitely.

**Fix:** Have `UpdateApplyEntryPoint.Run` delete its own helper exe (itself) as the
last step before/after relaunching — e.g. schedule a delayed self-delete (a common
Windows pattern: spawn `cmd /c timeout ... & del "%~f0"` or use
`FileOptions.DeleteOnClose` semantics via a wrapper), or have the next app launch
sweep `%TEMP%\RigToggle-updater-*.exe` for files older than a short grace period,
mirroring the existing orphaned-`.bak`/`.failed` cleanup already done in
`UpdateRollbackChecker.Run`'s `marker is null` branch.

### WR-02: Staged update exe is not cleaned up on a partial-download or checksum-fetch failure

**File:** `src/RigToggle.Windows/WindowsUpdateApplier.cs:62-107` (`DownloadAndStageAsync`)

**Issue:** The staged file at `stagedPath` is only explicitly deleted on the two
checksum-specific failure branches (missing checksum URL, checksum mismatch,
`:88` and `:98`). If `source.CopyToAsync` throws partway through the initial download
(network drop, cancellation) or `_httpClient.GetStringAsync(release.ChecksumDownloadUrl, ...)`
throws (network error fetching the checksum), the exception propagates unwrapped (by
design, per the class doc comment) but the partially-downloaded or fully-downloaded-
but-unverified exe is left on disk at `stagedPath`. It is silently overwritten by
`FileMode.Create` on the next attempt, so this is not user-visible, but it is an
inconsistent resource-cleanup contract (two failure branches clean up, two others do
not) and leaves a corrupt/unverified binary on disk in the interim.

**Fix:** Wrap the download+verify body in a `try`/`catch` that deletes `stagedPath` on
any exception before rethrowing, rather than only on the two checksum branches:

```csharp
try
{
    // download + checksum fetch/compare
}
catch
{
    try { File.Delete(stagedPath); } catch { /* best effort */ }
    throw;
}
```

### WR-03: `UpdateChecksum.Matches`'s documented "bare digest" support breaks on a trailing newline

**File:** `src/RigToggle.Core/UpdateChecksum.cs:42-68`

**Issue:** The class doc comment and `Matches`'s own doc comment both claim support
for "a bare 64-hex-character digest" in addition to the `sha256sum`-style
`"digest  filename"` line. The implementation only `TrimStart()`s the published text
(`:49`) before searching for a delimiting space/tab (`:50`); it never trims trailing
whitespace. A bare-digest checksum file with a trailing newline — the typical shape
of a hash written by `echo`, most text editors, or any tool other than this project's
own `-NoNewline` PowerShell step in `release.yml` — will have `digestToken.Length`
equal to 65 (64 hex chars + `\n`), fail the `!= 64` check at `:53`, and be rejected as
unverifiable, even though the digest itself is correct. Because `Matches` fails
closed, this is not a security hole, but it silently breaks update verification (and
therefore every future update) for any checksum-file format other than the exact one
`release.yml` currently produces — and the codebase's own test suite
(`UpdateChecksumTests.cs`) has no test covering a trailing-newline bare digest,
so this gap is untested.

**Fix:** Trim trailing whitespace/newline from the published text (or from the
extracted token) before the length/hex checks:

```csharp
ReadOnlySpan<char> trimmed = publishedText.AsSpan().Trim();
```

## Info

### IN-01: `ShowUpdatingBalloon` takes an unused `ReleaseInfo` parameter

**File:** `src/RigToggle.App/MainForm.cs:2112`

**Issue:** `ShowUpdatingBalloon(ReleaseInfo release)` never references `release` — the
balloon text is a fixed literal. This is likely required only to satisfy the
`Action<ReleaseInfo>` delegate shape shared with `onApplyStarting`, so it's a minor,
low-priority nit rather than dead code.

**Fix:** No action required if the delegate signature is intentionally shared; if
desired, document why the parameter is unused (e.g., `_ = release;` with a short
comment) so a future reader doesn't wonder whether a per-release detail was meant to
be shown.

---

_Reviewed: 2026-08-22T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
