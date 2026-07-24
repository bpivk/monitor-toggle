---
phase: 03-app-audio-control
verified: 2026-07-24T17:25:30Z
status: gaps_found
score: 12/13 must-haves verified
overrides_applied: 0
gaps:
  - truth: "Toggling back to normal mode restores the exact previous default audio device across all relevant audio roles (ROADMAP SC5) / 'Toggling back restores the exact per-role previous device, resolving by ID with a friendly-name fallback' (03-02-PLAN must_haves)"
    status: failed
    reason: "WindowsAudioController.Restore only falls back to friendly-name matching when the captured DeviceId is null/empty (line 131). It never re-validates an ID that is present but stale (device unplugged/replaced since capture — the exact scenario its own doc comment at lines 113-117 claims to handle). ApplyAndVerify (called with the stale ID) throws an uncaught InvalidOperationException; ToggleService.ToggleToNormalMode calls _audioController.Restore(snapshot.Audio) with no try/catch (ToggleService.cs:107), so the exception aborts the rest of the sequence — the other two (perfectly valid) audio roles are never restored, _appController.MinimizeIfRunning is never called, and _snapshotStore.Clear() never runs, leaving IsInRigMode() permanently true with no automatic recovery. This directly contradicts the phase's core value proposition (CLAUDE.md: 'just as reliably restores everything to exactly how it was before') and the roadmap's SC5 wording 'restores the exact previous default audio device.' Confirmed by direct code read, not by restating 03-REVIEW.md's CR-01 — the same defect was independently re-traced against WindowsAudioController.cs:118-180 and ToggleService.cs:99-113 during this verification."
    artifacts:
      - path: "src/RigToggle.Windows/WindowsAudioController.cs"
        issue: "Restore (lines 118-147) and ApplyAndVerify (lines 154-180): no stale-ID check via the existing TryResolveDevice helper before trusting a non-null DeviceId; no per-role try/catch isolating one role's failure from the other two or from the rest of ToggleToNormalMode"
      - path: "src/RigToggle.Core/ToggleService.cs"
        issue: "ToggleToNormalMode (line 107) calls _audioController.Restore with no surrounding try/catch, so any exception raised inside Restore prevents MinimizeIfRunning (line 110) and snapshotStore.Clear() (line 112) from ever running"
    missing:
      - "In WindowsAudioController.Restore, check TryResolveDevice(deviceId) before trusting a present-but-possibly-stale snapshot.DeviceId; if it no longer resolves, fall through to the friendly-name match exactly as the null/empty-ID branch already does"
      - "Wrap each role's ApplyAndVerify call in Restore's foreach loop in its own try/catch so one role's failure does not abort the other two roles (matches the doc comment's own claim: 'a role with neither a usable ID nor a resolvable name is skipped rather than failing the whole restore')"
      - "Ensure ToggleToNormalMode still reaches MinimizeIfRunning and snapshotStore.Clear() even when one or more audio roles fail to restore, so the app never gets permanently stuck reporting Rig mode"
---

# Phase 3: App & Audio Control Verification Report

**Phase Goal:** Toggling reliably launches/focuses the companion app and switches the default audio output device, using real Windows APIs in place of Phase 2's fakes.
**Verified:** 2026-07-24T17:25:30Z
**Status:** gaps_found
**Re-verification:** No — initial verification

## Environment Note

This Linux sandbox has no `dotnet` toolchain. All three plans' SUMMARY.md files note this explicitly. Per the task instructions, this is expected and is not treated as a verification failure on its own — acceptance criteria were instead checked via direct source read-through (every file the plans touched was read in full) plus targeted `grep` against the exact plan-specified patterns, and all referenced git commit hashes were confirmed present in `git log`/`git cat-file`. A `dotnet build`/`dotnet test` run on the Windows dev/rig machine is still required before this code should be considered fully validated at the compiler/runtime level (this applies to all truths below, not just the failed one).

## Goal Achievement

### Observable Truths (Roadmap Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Toggling to rig mode launches the configured companion app if it isn't already running | VERIFIED | `WindowsAppController.LaunchOrFocus` (lines 56-82): `if (!IsRunning(...))` branch calls `Process.Start(companionAppPath)`, guarded against a null return, then polls with `Refresh()` before reading `MainWindowHandle`. |
| 2 | If the companion app is already running, toggling to rig mode brings its window to focus instead of launching a duplicate instance | VERIFIED | `LaunchOrFocus` (lines 84-112): already-running branch re-enumerates by process name and calls `SetForegroundWindow` on a non-zero handle; `Process.Start` is only reachable from the `!IsRunning` branch — no path launches a duplicate. |
| 3 | Toggling back to normal mode minimizes the companion app's window (best-effort) | VERIFIED (see caveat) | `WindowsAppController.MinimizeIfRunning` (lines 115-148) calls `NativeMethods.ShowWindow(handle, SW_MINIMIZE)` best-effort when a live handle exists, else silent no-op; wired via `ToggleService.ToggleToNormalMode` line 110. **Caveat:** this call is only reached if `_audioController.Restore(...)` on line 107 does not throw — see Gap below, which can prevent this step from running in the stale-device-ID scenario. |
| 4 | Toggling to rig mode switches the default audio output device to the configured rig speakers | VERIFIED | `WindowsAudioController.SetDefault` → `SetDefaultForAllRoles` → `ApplyAndVerify` for each of the three `Roles` pairings; creates a fresh `PolicyConfigClient`, calls `SetDefaultEndpoint`, throws on non-zero HRESULT, then re-verifies via NAudio `GetDefaultAudioEndpoint` and throws `InvalidOperationException` on ID mismatch. Wired via `ToggleService.ToggleToRigMode` line 73, no try/catch (D-04: allowed to bubble). |
| 5 | Toggling back to normal mode restores the exact previous default audio device across all relevant audio roles | **FAILED** | `WindowsAudioController.Restore` (lines 118-147) only applies the friendly-name fallback when `DeviceId` is null/empty (line 131) — never when a captured ID is present but no longer resolves (device unplugged/replaced since capture). A stale-but-present ID is passed straight to `ApplyAndVerify`, which throws an uncaught `InvalidOperationException` that aborts the whole `Restore` loop (other two roles never restored) and the rest of `ToggleToNormalMode` (`MinimizeIfRunning`/`snapshotStore.Clear()` never reached, `ToggleService.cs:107-112`, no try/catch present anywhere in the call chain). See Gaps Summary. |

**Score:** 4/5 roadmap success criteria fully verified; 1 failed (SC5).

### PLAN Frontmatter Must-Haves (Truths)

| # | Truth (source plan) | Status | Evidence |
|---|------|--------|----------|
| 6 | CaptureState captures the default render device for eConsole, eMultimedia, and eCommunications independently — per D-02 (03-01) | VERIFIED | `WindowsAudioController.CaptureState` (lines 57-96): three independent `try { using enumerator...; GetDefaultAudioEndpoint(DataFlow.Render, Role.X) }` blocks, one per role. `grep -c "GetDefaultAudioEndpoint(DataFlow.Render, Role\."` = 3. |
| 7 | A single role's read failure falls back to a null AudioRoleState without aborting capture of the other two roles — per D-02 (03-01) | VERIFIED | Each of the three blocks in `CaptureState` has its own `catch (Exception) { ... = new AudioRoleState(null, null); }`, fully independent of the other two. |
| 8 | A stale-shaped state.json on disk no longer crashes JsonSnapshotStore.Load (03-01) | VERIFIED | `JsonSnapshotStore.Load` (lines 46-56) wraps `Deserialize` in `try/catch (JsonException) { return null; }`. Confirmed by a real (not just grepped) test: `JsonStoreTests.SnapshotStore_Load_OnMalformedFile_ReturnsNullInsteadOfThrowing` writes `"{not valid json"` to disk and asserts `Load()` returns null. |
| 9 | The whole solution and RigToggle.Tests still compile and pass after the AudioState shape change (03-01) | UNCERTAIN | No `dotnet` available in this sandbox. Manually traced every remaining `AudioState`/`DefaultDeviceId` reference across the solution (`grep -rn "DefaultDeviceId" src/` returns only local variable names in `FakeControllers.cs`, no leftover positional-record construction sites) — no obvious compile break found, but this requires an actual Windows build to confirm. Flagged for human/CI verification, not scored as failed. |
| 10 | Toggling to rig mode sets the default render device for all three roles to the configured rig device — per D-01 (03-02) | VERIFIED | Same evidence as roadmap SC4 above. |
| 11 | Each role's SetDefaultEndpoint is followed by a NAudio read-back; a mismatch throws a visible exception, allowed to bubble per D-04 (03-02) | VERIFIED | `ApplyAndVerify` (lines 154-180): re-queries `GetDefaultAudioEndpoint(DataFlow.Render, managedRole)` after the COM call and throws `InvalidOperationException` on ID mismatch (OrdinalIgnoreCase compare). `ToggleService.ToggleToRigMode` calls `SetDefault` with no try/catch, confirming the bubble-up path. |
| 12 | Toggling back restores the exact per-role previous device, resolving by ID with a friendly-name fallback (03-02) | **FAILED** | Same evidence as roadmap SC5 above — the friendly-name fallback exists but only triggers for a null/empty ID, not a stale-but-present one, which is the actual "device changed since capture" scenario the code's own doc comment claims to cover. |
| 13 | A fresh PolicyConfigClient COM object is created and released every role cycle — never cached (03-02) | VERIFIED | `ApplyAndVerify` (lines 154-169): `var client = (IPolicyConfig)new PolicyConfigClient();` inside the per-role loop body, `Marshal.ReleaseComObject(client)` in a `finally`; no static/instance field caching a client across calls. |
| 14 | Toggling to rig mode launches the companion app when not already running — per D-06 (03-03) | VERIFIED | Same evidence as roadmap SC1. |
| 15 | When already running with a live window handle, focus is used instead of launching a duplicate — per D-06 (03-03) | VERIFIED | Same evidence as roadmap SC2. |
| 16 | When already running but MainWindowHandle is zero, LaunchOrFocus does NOT poll and does NOT fail — per D-06 (03-03) | VERIFIED | `LaunchOrFocus`'s already-running branch (lines 84-112) does a single `foreach` pass over enumerated processes calling `p.Refresh()` once each with no `while`/deadline loop, and no exception is thrown on a fully zero-handle pass — the method simply returns after the `finally` disposes the processes. |
| 17 | Toggling back minimizes the app window when a handle is available; zero handle is a no-op — per D-07 (03-03) | VERIFIED | Same evidence as roadmap SC3 (with the same caveat about reachability given the Gap above). |
| 18 | A missing companion-app path fails ToggleToRigMode before any state is captured, persisted, or mutated — per D-05 (03-03) | VERIFIED | `ToggleService.ToggleToRigMode` (lines 56-64): `File.Exists` guard placed after `IsFullyConfigured` and before `CaptureState`/`Save`. Confirmed by a real test, not just a grep: `ToggleToRigMode_Throws_WhenCompanionAppPathDoesNotExist` asserts `Assert.Throws<InvalidOperationException>` AND `Assert.DoesNotContain(callLog, entry => entry.StartsWith("snapshot.Save"))`. |

**Combined score:** 12/13 scored truths verified (truth #9 is UNCERTAIN/needs-human, not counted against or for the score; truths #5/#12 are the same underlying failure, counted once in the frontmatter score summary above as 12/13).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/RigToggle.Core/Models/AudioRoleState.cs` | Per-role id+name record | VERIFIED | `public sealed record AudioRoleState(string? DeviceId, string? DeviceName)` exists exactly as specified. |
| `src/RigToggle.Core/Models/AudioState.cs` | Three-role audio snapshot | VERIFIED | `public sealed record AudioState(AudioRoleState Console, AudioRoleState Multimedia, AudioRoleState Communications)`; no leftover `DefaultDeviceId` token. |
| `src/RigToggle.Windows/WindowsAudioController.cs` | Per-role CaptureState + real SetDefault/Restore + verify-and-throw | VERIFIED (existence/substance); Restore path has the CR-01 gap noted above | Contains `Role.Communications`, `Marshal.ReleaseComObject`, `SetDefaultEndpoint`, `ApplyAndVerify`. |
| `src/RigToggle.Windows/Audio/IPolicyConfig.cs` | Verified 12-method IPolicyConfig vtable + PolicyConfigClient + ERole | VERIFIED | 12 `[PreserveSig] int` methods counted exactly; `ResetDeviceFormat` present before `SetDeviceFormat`; both GUIDs (`F8679F50-850A-41CF-9C72-430F290290C8`, `870AF99C-171D-4F9E-AF0D-E63DF40C2BC9`) present; namespace `RigToggle.Windows.Audio`. |
| `src/RigToggle.Windows/NativeMethods.cs` | user32.dll ShowWindow/SetForegroundWindow P/Invoke | VERIFIED | Both `[DllImport("user32.dll")]` declarations present; `internal static class`; `SW_MINIMIZE = 6`. |
| `src/RigToggle.Windows/WindowsAppController.cs` | Real LaunchOrFocus + MinimizeIfRunning | VERIFIED | `Process.Start` present, Refresh-aware poll loop, no-poll already-running branch, best-effort `ShowWindow`/`SetForegroundWindow`. |
| `src/RigToggle.Core/ToggleService.cs` | D-05 app-path preflight before capture/mutation | VERIFIED | `File.Exists(settings.CompanionAppPath)` positioned after `IsFullyConfigured` throw, before `CaptureState`. |
| `src/RigToggle.Tests/Doubles/FakeControllers.cs` | Fake controllers updated to new three-role shape | VERIFIED | `FakeAudioController.CaptureState`/`Restore` build/consume the three-role `AudioState`; `audio.Restore:` log prefix preserved. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `WindowsAudioController.cs` | NAudio `MMDeviceEnumerator.GetDefaultAudioEndpoint` | one call per Role in CaptureState | WIRED | `grep -c "GetDefaultAudioEndpoint(DataFlow.Render, Role\."` = 3 in `CaptureState`. |
| `WindowsAudioController.cs` | `IPolicyConfig.SetDefaultEndpoint` | once per ERole, then NAudio read-back compare | WIRED | `ApplyAndVerify` calls `client.SetDefaultEndpoint(deviceId, nativeRole)` then re-reads via `GetDefaultAudioEndpoint(DataFlow.Render, managedRole)` and compares. |
| `WindowsAppController.cs` | `Process.MainWindowHandle` | Refresh()-aware poll loop on fresh launch | WIRED | `process.Refresh()` called as first statement of each poll iteration before `MainWindowHandle` read (line 69), and again on the already-running re-enumeration path (lines 98, 133). |
| `ToggleService.cs` | `File.Exists(settings.CompanionAppPath)` | guard clause after IsFullyConfigured, before CaptureState | WIRED | Confirmed ordering: `IsFullyConfigured` throw (line 46) → `File.Exists` throw (line 56) → `CaptureState` (lines 66-67). |
| `ToggleService.ToggleToNormalMode` | `_audioController.Restore` → `_appController.MinimizeIfRunning` → `_snapshotStore.Clear()` | linear sequence, no error isolation | **PARTIAL** | Sequence exists and is correctly ordered when `Restore` does not throw, but there is no try/catch around `Restore` (line 107) — an exception there (CR-01 scenario) prevents `MinimizeIfRunning` and `Clear()` from ever executing. This is the mechanism behind the Gap above. |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| AUDIO-01 | 03-02 | User can switch the default audio output device to the configured rig speakers when toggling to rig mode | SATISFIED | `SetDefault`/`SetDefaultForAllRoles`/`ApplyAndVerify`, wired into `ToggleToRigMode`. |
| AUDIO-02 | 03-01, 03-02 | User can restore the exact previous default audio device (across all relevant audio roles) when toggling back to normal mode | **BLOCKED** | Per-role capture (03-01) is solid; restore (03-02) has the CR-01 stale-ID gap that can silently break "exact restore across all relevant audio roles" for a plausible real-world scenario (device swapped/reinstalled between capture and restore). |
| APP-01 | 03-03 | Toggling to rig mode launches the configured companion app if it isn't already running | SATISFIED | `LaunchOrFocus` not-running branch. |
| APP-02 | 03-03 | If the companion app is already running when toggling to rig mode, its window is brought to focus instead of launching a duplicate instance | SATISFIED | `LaunchOrFocus` already-running branch. |
| APP-03 | 03-03 | Toggling back to normal mode minimizes the companion app's window (best-effort) | SATISFIED (implementation), AT-RISK (reachability) | `MinimizeIfRunning` implementation is correct and best-effort as required; however it is only reached in `ToggleToNormalMode` if `Restore` does not throw — see AUDIO-02 gap. Not marking APP-03 itself as blocked since its own logic is sound, but flagging the dependency. |

**Orphaned requirements check:** `.planning/REQUIREMENTS.md` lines 91-95 map exactly `AUDIO-01, AUDIO-02, APP-01, APP-02, APP-03` to Phase 3, and all five appear in the `requirements:` frontmatter fields across the three PLAN files (03-01: AUDIO-02; 03-02: AUDIO-01, AUDIO-02; 03-03: APP-01, APP-02, APP-03). No orphaned requirements.

### Data-Flow Trace (Level 4)

Not applicable in the strict sense (this phase has no GUI data-rendering components — WinForms UI is Phase 2 scope). The relevant "data flow" here is the settings/snapshot → controller call chain, which was traced above under Key Link Verification (`ToggleService` → `IAudioController`/`IAppController` → real Windows implementations), and is WIRED with the one PARTIAL noted (Restore exception isolation).

### Behavioral Spot-Checks

Not run. This is a Windows-only WinForms/COM-interop/P-Invoke project (`net10.0-windows`) with no runnable entry point in this Linux sandbox — `dotnet` is not installed, and the code depends on Win32/COM APIs unavailable outside Windows. Step 7b: SKIPPED (no runnable entry points in this environment). All acceptance criteria were instead confirmed via direct source read-through plus targeted grep against every exact pattern specified in the three plans' `<acceptance_criteria>` blocks.

### Probe Execution

No `scripts/*/tests/probe-*.sh` files exist in this repository, and no plan/summary references probe-based verification. Step 7c: SKIPPED (no probes declared or discovered).

```
find scripts -path '*/tests/probe-*.sh' -type f  → (no output, directory does not exist)
```

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` markers found in any of the 11 files modified across the three plans | — | None — clean scan. |
| `src/RigToggle.Core/Abstractions/IAudioController.cs`, `IAppController.cs` | doc comment | Stale doc comment still says "SetDefault/Restore are no-op stubs until Phase 3" / "LaunchOrFocus/MinimizeIfRunning are no-op stubs until Phase 3" | INFO | These interface files were not in any Phase 3 plan's `files_modified` list (pre-existing from Phase 2) and are functionally unaffected — but now describe stale behavior since Phase 3 makes these methods real. Cosmetic; does not affect goal achievement. |
| `src/RigToggle.Windows/WindowsAudioController.cs` | 66, 78, 90, 203 | Broad `catch (Exception)` with no logging (WR-04 from 03-REVIEW.md) | INFO (already tracked in code review) | Not a blocker for this phase's goal; noted for completeness per the Inversion/Confirmation-Bias-Counter check, not re-litigated here. |

Git commit hashes cited in all three SUMMARY.md files (`af42813`, `42bd348`, `c753751`, `5eb9e06`, `b25ab62`, `2b9b55f`, `714d9a2`, `a688ede`) were confirmed present via `git cat-file -e` — SUMMARY claims about what was committed are corroborated, not fabricated.

### Human Verification Required

None required to resolve the status — the failed truth (AUDIO-02 restore reliability) is a code-level defect verifiable by static reading (confirmed above), not a UX/visual/real-time behavior needing a human. Actual on-hardware `dotnet build`/`dotnet test`/manual toggle testing on the Windows rig remains outstanding for all of Phase 3 (as all three SUMMARY.md files note), but that is an execution-environment limitation already flagged as informational (truth #9, UNCERTAIN), not a human-judgment item.

### Gaps Summary

One blocking gap: `WindowsAudioController.Restore` does not handle the "captured device ID is stale" case (device unplugged/replaced/reinstalled since the snapshot was taken) — it only falls back to friendly-name matching when the ID was never captured in the first place (null/empty). Because `ApplyAndVerify` throws an uncaught `InvalidOperationException` on a stale ID, and nothing in `Restore` or `ToggleService.ToggleToNormalMode` catches it, this single bad role:
1. Aborts restoring the other two (otherwise-fine) audio roles,
2. Prevents `MinimizeIfRunning` from ever running (APP-03's reachability, not its implementation, is at risk),
3. Prevents `_snapshotStore.Clear()` from running, leaving the app permanently reporting "Rig mode" with no automatic recovery path.

This directly undermines both the explicit roadmap Success Criterion 5 ("restores the exact previous default audio device... across all relevant audio roles") and the project's stated core value ("just as reliably restores everything to exactly how it was before" — CLAUDE.md). This was independently confirmed by direct source inspection of `WindowsAudioController.cs` and `ToggleService.cs` during this verification (not merely restated from `03-REVIEW.md`'s CR-01, though the review's finding and root-cause analysis are corroborated and correct).

Everything else in the phase — companion-app launch/focus/minimize (APP-01/02/03 implementation), the forward audio-switch path (AUDIO-01), the per-role capture model, and the D-05 preflight — is genuinely implemented with real Windows APIs (COM interop, P/Invoke, NAudio), matches its plan's acceptance criteria on direct source inspection, and is wired correctly end-to-end.

**This looks like an unresolved code-review finding rather than an intentional deviation** — 03-REVIEW.md already flagged this exact issue as Critical (CR-01) with a concrete fix. No override is suggested; the recommended path is to close this gap (e.g. via `/gsd:plan-phase --gaps`) using the fix already drafted in `03-REVIEW.md`.

---

_Verified: 2026-07-24T17:25:30Z_
_Verifier: Claude (gsd-verifier)_
