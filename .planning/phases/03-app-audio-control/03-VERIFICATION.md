---
phase: 03-app-audio-control
verified: 2026-07-24T18:40:00Z
status: passed
score: 13/13 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 12/13
  gaps_closed:
    - "Toggling back to normal mode restores the exact previous default audio device across all relevant audio roles (roadmap SC5 / AUDIO-02) — WindowsAudioController.Restore now stale-ID-checks via TryResolveDevice before trusting a present DeviceId, and isolates each role's ApplyAndVerify in its own try/catch so one role's failure never aborts the other two."
    - "ToggleService.ToggleToNormalMode always reaches MinimizeIfRunning (APP-03) and _snapshotStore.Clear(), even when audio (or monitor) restore throws — the restore calls are now wrapped in a non-rethrowing try/catch, with MinimizeIfRunning/Clear lexically after and outside it."
  gaps_remaining: []
  regressions: []
---

# Phase 3: App & Audio Control Verification Report

**Phase Goal:** Toggling reliably launches/focuses the companion app and switches the default audio output device, using real Windows APIs in place of Phase 2's fakes.
**Verified:** 2026-07-24T18:40:00Z
**Status:** passed
**Re-verification:** Yes — after gap closure (plan 03-04)

## Environment Note

This Linux sandbox has no `dotnet` toolchain (consistent with all four plans' SUMMARY.md files). As before, this is expected and not treated as a verification failure — every claim below was checked via direct, full source read-through of the actual current file contents (not restated from SUMMARY.md prose) plus targeted `grep`, and every cited git commit hash was confirmed present via `git cat-file -e`. A `dotnet build`/`dotnet test` run on the Windows dev/rig machine remains outstanding for full compiler/runtime validation of this phase.

## Goal Achievement

### Observable Truths (Roadmap Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Toggling to rig mode launches the configured companion app if it isn't already running | VERIFIED (regression check — unchanged since prior pass) | `WindowsAppController.LaunchOrFocus` (lines 56-82), not touched by 03-04. Re-read in full: `Process.Start` guarded against null, `Refresh()`-aware poll loop. |
| 2 | If the companion app is already running, toggling to rig mode focuses it instead of launching a duplicate | VERIFIED (regression check) | `LaunchOrFocus` already-running branch (lines 84-112), unchanged by 03-04. |
| 3 | Toggling back to normal mode minimizes the companion app's window (best-effort) | VERIFIED — reachability caveat from prior pass is now resolved | `WindowsAppController.MinimizeIfRunning` (unchanged) is now reliably reached from `ToggleService.ToggleToNormalMode` (line 129) because it sits lexically after the new try/catch around the restore calls (lines 116-127) — it runs whether or not `Restore` throws. Confirmed by direct read of `ToggleService.cs` and by the new regression test `ToggleToNormalMode_StillMinimizesAndClears_WhenAudioRestoreThrows` (`ToggleServiceTests.cs:111-127`), which asserts an `app.MinimizeIfRunning:` log entry is present even when `FakeAudioController.Restore` throws. |
| 4 | Toggling to rig mode switches the default audio output device to the configured rig speakers | VERIFIED (regression check — forward path untouched) | `SetDefault`/`SetDefaultForAllRoles`/`ApplyAndVerify` are byte-for-byte unchanged by 03-04 (confirmed: `grep -c "SetDefaultEndpoint"` = 4, `throw new InvalidOperationException` still present x2, no new try/catch anywhere in these three methods). Still wired via `ToggleService.ToggleToRigMode` line 73 with no try/catch — forward-path verify-and-throw (D-03/D-04) intact. |
| 5 | Toggling back to normal mode restores the exact previous default audio device across all relevant audio roles | **VERIFIED — previously FAILED, now fixed** | `WindowsAudioController.Restore` (lines 126-176): for each role, `if (!string.IsNullOrEmpty(deviceId) && TryResolveDevice(deviceId) is null) { deviceId = null; }` (lines 139-146) treats a stale-but-present ID exactly like a never-captured one, falling through to the existing friendly-name match (lines 148-153). Each role's `ApplyAndVerify` call is now individually wrapped in `try { } catch (InvalidOperationException) { }` (lines 162-174), so one role's apply/verify failure does not prevent the `foreach` from continuing to the other two roles. Read the whole method directly; matches 03-04-PLAN's task 1 spec exactly. |

**Score:** 5/5 roadmap success criteria verified (was 4/5).

### PLAN Frontmatter Must-Haves (Truths)

| # | Truth (source plan) | Status | Evidence |
|---|------|--------|----------|
| 6 | CaptureState captures the default render device for eConsole/eMultimedia/eCommunications independently (03-01) | VERIFIED (regression check) | Unchanged — three independent try/catch blocks in `CaptureState`, confirmed by direct re-read. |
| 7 | A single role's read failure falls back to null AudioRoleState without aborting the others (03-01) | VERIFIED (regression check) | Unchanged, confirmed by direct re-read. |
| 8 | A stale-shaped state.json no longer crashes JsonSnapshotStore.Load (03-01) | VERIFIED (regression check) | `JsonSnapshotStore.Load` `try/catch (JsonException) { return null; }` unchanged; not in 03-04's file list. |
| 9 | The whole solution and RigToggle.Tests still compile after the AudioState shape change (03-01) | UNCERTAIN (unchanged) | No `dotnet` in this sandbox; still flagged for human/CI verification, not scored against the phase. |
| 10 | Toggling to rig mode sets the default render device for all three roles (03-02) | VERIFIED (regression check) | Same evidence as roadmap SC4. |
| 11 | Each role's SetDefaultEndpoint is followed by a NAudio read-back; mismatch throws, allowed to bubble (03-02, D-04) | VERIFIED (regression check) | `ApplyAndVerify` unchanged; `ToggleToRigMode` still has no try/catch around `SetDefault`. |
| 12 | Toggling back restores the exact per-role previous device, resolving by ID with friendly-name fallback, including stale IDs (03-02 + 03-04 gap closure) | **VERIFIED — previously FAILED, now fixed** | Same evidence as roadmap SC5. |
| 13 | A fresh PolicyConfigClient is created and released every role cycle — never cached (03-02) | VERIFIED (regression check) | `ApplyAndVerify` unchanged: `var client = (IPolicyConfig)new PolicyConfigClient();` inside the loop, `Marshal.ReleaseComObject(client)` in `finally`. |
| 14 | Toggling to rig mode launches the companion app when not already running (03-03, D-06) | VERIFIED (regression check) | Unchanged. |
| 15 | When already running with a live window handle, focus is used instead of launching a duplicate (03-03, D-06) | VERIFIED (regression check) | Unchanged. |
| 16 | When already running but MainWindowHandle is zero, LaunchOrFocus does not poll and does not fail (03-03, D-06) | VERIFIED (regression check) | Unchanged. |
| 17 | Toggling back minimizes the app window when a handle is available; zero handle is a no-op (03-03, D-07) | VERIFIED — reachability caveat resolved | Same evidence as roadmap truth #3 above. |
| 18 | A missing companion-app path fails ToggleToRigMode before any state is captured/persisted/mutated (03-03, D-05) | VERIFIED (regression check) | `File.Exists` guard in `ToggleToRigMode`, unchanged, not in 03-04's file list. |
| 19 (new, 03-04) | A single audio role's restore failure never aborts restore of the other two roles | VERIFIED | Per-role `try/catch (InvalidOperationException)` around `ApplyAndVerify` inside `Restore`'s `foreach` (lines 162-174) — confirmed by direct read; the loop continues naturally after a caught failure. |
| 20 (new, 03-04) | ToggleToNormalMode always reaches MinimizeIfRunning and snapshot Clear, even when audio (or monitor) restore throws | VERIFIED | `ToggleService.ToggleToNormalMode` (lines 109-132): restore calls wrapped in `try { } catch (Exception) { }` (lines 116-127); `_appController.MinimizeIfRunning(...)` (line 129) and `_snapshotStore.Clear()` (line 131) sit lexically after and outside that block, unconditionally reached. Confirmed by direct read plus the passing regression test `ToggleToNormalMode_StillMinimizesAndClears_WhenAudioRestoreThrows`. |

**Combined score:** 13/13 scored truths verified (truth #9 remains UNCERTAIN/needs-human — not counted for or against, same as prior verification).

### Independent Assessment of the Fresh 03-REVIEW.md CR-01 Finding

The fresh code review (`03-REVIEW.md`, re-run after 03-04) confirms the original defect (CR-01 in that review, matching this VERIFICATION's prior gap) is genuinely fixed, but raises a **new** Critical concern: `ToggleToNormalMode`'s `catch (Exception)` (lines 116-127) swallows *any* restore failure — not just the stale-audio-ID case — with zero logging anywhere in the codebase, and `MinimizeIfRunning` (line 129) itself sits outside that try/catch, so if it throws, `_snapshotStore.Clear()` (line 131) is skipped.

I independently re-traced this against the current code (not the review's framing) and reached the following judgment:

**1. Is the "monitor restore swallowed silently" angle reachable in Phase 3 as currently scoped?** No. `WindowsMonitorController.Restore` (`src/RigToggle.Windows/WindowsMonitorController.cs:52-56`) is a literal empty-body no-op ("FAKE in Phase 2 — no-op... lands in Phase 4"). An empty method cannot throw. The catch-all's coverage of monitor restore is therefore currently inert — a real concern only once Phase 4 replaces this stub with a fallible CCD `ApplyPathInfos` restore. The reviewer explicitly acknowledges this ("this specific path cannot yet be exercised by monitor failures"). This part of CR-01 is a legitimate **forward-looking** warning for Phase 4, not an active Phase 3 defect.

**2. Is the "audio restore silently fails, no indication" angle reachable, and does it undermine SC5?** Only in narrow, non-primary paths. The mainline failure scenario this phase's must-haves target — a stale/unplugged captured `DeviceId` — is now handled *without* needing the outer catch at all: `WindowsAudioController.Restore` resolves staleness per-role via `TryResolveDevice`, falls through to a friendly-name match, and if neither an ID nor a name resolves, explicitly `continue`s past that one role (`WindowsAudioController.cs:155-160`) rather than aborting — so the other two roles are still correctly restored, not silently dropped. The outer `ToggleService` catch only fires for exception types that escape `Restore` entirely (e.g. a COM enumeration failure in `GetPlaybackDevices()`), which is a genuinely rare, defensive-depth case, not the everyday "device got swapped" scenario the phase's must-haves were written for. A silent swallow with no logging in that rare residual case is a legitimate observability gap (tracked as WR-01/WR-07/IN-02 in the fresh review) but does not mean the phase's stated goal — restoring the exact previous device across roles in the realistic stale-device scenario — is unmet; the evidence above shows it now *is* met for that scenario.

**3. Does CORE-04/D-04 scope this out of Phase 3?** Yes, directly. `03-CONTEXT.md` D-04 states: "Richer per-step failure reporting (which step succeeded/failed, partial-failure recovery) is explicitly Phase 5 / CORE-04 scope." `REQUIREMENTS.md` CORE-04 ("If any step of a toggle fails partway, the app reports which steps succeeded/failed...") is mapped to Phase 5, not Phase 3. Comprehensive failure-surfacing (rethrow-after-cleanup, structured partial-failure reporting, logging infrastructure) is exactly what CORE-04 describes, and it is explicitly out of this phase's contract. The 03-04 gap-closure plan's own scope was narrower and explicit: fix the stuck-in-Rig-mode defect (T-03-04-02) without weakening the forward-path verify-and-throw contract — it did exactly that, nothing more, nothing less.

**4. Is WR-03 (MinimizeIfRunning itself could throw, skipping Clear()) a blocking gap?** No, and it is not a regression introduced by 03-04 — this narrow race (the companion process would need to exit in the few-millisecond window between `Process.GetProcessesByName` and the subsequent `Refresh()`/`MainWindowHandle` read inside `MinimizeIfRunning`) existed identically before 03-04 as well; 03-04 did not change `WindowsAppController.cs` at all (confirmed: not in 03-04's `files_modified` list, and `git log` on that file shows no 03-04 commit touching it). It is a legitimate, narrow WARNING worth a cheap follow-up fix, but it is not something this re-verification should block Phase 3 on — it requires the companion app to crash mid-toggle, an edge case orthogonal to the stale-audio-device defect this phase was scoped to close.

**Conclusion:** The fresh review's new CR-01 does **not** invalidate goal achievement for Phase 3. The specific, previously-blocking defect (stale audio ID → uncaught exception → permanently stuck in Rig mode) is genuinely closed, and the mainline "restore across all relevant audio roles" scenario is now correctly handled per-role without relying on silent swallowing. The residual concerns (no logging on rare/defensive-depth catch paths; monitor-restore swallow inert until Phase 4; MinimizeIfRunning's narrow unguarded race) are legitimate code-quality/robustness improvements appropriately deferred — the first two explicitly to Phase 5/CORE-04 and Phase 4 respectively, the third as a cheap opportunistic fix that doesn't gate this phase. These are recorded as WARNING-level anti-pattern findings below, not gaps.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/RigToggle.Windows/WindowsAudioController.cs` | Restore with stale-ID detection + per-role isolation | VERIFIED | `TryResolveDevice(deviceId)` check (line 139) present; `catch (InvalidOperationException)` count = 1 inside `Restore`'s loop; forward path (`ApplyAndVerify`/`SetDefault`/`SetDefaultForAllRoles`) unchanged. |
| `src/RigToggle.Core/ToggleService.cs` | ToggleToNormalMode restore block wrapped so MinimizeIfRunning + Clear run unconditionally | VERIFIED | `try { monitor.Restore; audio.Restore } catch (Exception) { }` (lines 116-127); `MinimizeIfRunning` (129) and `Clear()` (131) lexically after/outside. |
| `src/RigToggle.Tests/Doubles/FakeControllers.cs` | FakeAudioController configurable to throw on Restore | VERIFIED | `throwOnRestore` constructor param (default false), throws `InvalidOperationException` after logging when true (lines 45-86). |
| `src/RigToggle.Tests/ToggleServiceTests.cs` | Regression test proving minimize+clear survive a throwing restore | VERIFIED | `ToggleToNormalMode_StillMinimizesAndClears_WhenAudioRestoreThrows` (lines 111-127) asserts `app.MinimizeIfRunning:` entry, `snapshot.Clear`, and `IsInRigMode() == false`. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `WindowsAudioController.Restore` | `TryResolveDevice` | stale-ID validation before ApplyAndVerify | WIRED | `grep -n "TryResolveDevice(deviceId)"` matches inside `Restore` (line 139). |
| `WindowsAudioController.Restore` foreach | `ApplyAndVerify` | per-role try/catch (InvalidOperationException) | WIRED | Confirmed at lines 162-174; loop proceeds to next role after a caught failure (no `continue`/`break`/rethrow inside the catch). |
| `ToggleService.ToggleToNormalMode` | `_appController.MinimizeIfRunning` → `_snapshotStore.Clear()` | unconditional sequence after a try/catch-wrapped restore block | WIRED (previously PARTIAL) | Confirmed by direct read: both calls sit outside/after the try/catch; also proven by the passing regression test. |
| `ToggleService.ToggleToRigMode` | `_audioController.SetDefault` | no try/catch, D-04 bubble-up intact | WIRED (regression check) | Unchanged — `ToggleToRigMode` (line 73) still has no try/catch. |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| AUDIO-01 | 03-02 | Switch default audio output to rig speakers when toggling to rig mode | SATISFIED | Forward path unchanged, verified above. REQUIREMENTS.md traceability table still shows "Pending" for this row (see Anti-Patterns/documentation note below — a doc-sync gap, not a code gap). |
| AUDIO-02 | 03-01, 03-02, 03-04 | Restore the exact previous default audio device (all roles), including stale-device recovery | SATISFIED | Stale-ID fallback + per-role isolation confirmed above. REQUIREMENTS.md marks this row "Complete" (commit `14d73d1`). |
| APP-01 | 03-03 | Launch companion app if not already running | SATISFIED | Unchanged, verified above. REQUIREMENTS.md still shows "Pending" (doc-sync gap, not code gap). |
| APP-02 | 03-03 | Focus instead of duplicate-launch when already running | SATISFIED | Unchanged, verified above. REQUIREMENTS.md still shows "Pending" (doc-sync gap, not code gap). |
| APP-03 | 03-03, 03-04 | Minimize companion app on toggle-back (best-effort), reliably reachable | SATISFIED | Implementation unchanged and correct; reachability gap closed by 03-04. REQUIREMENTS.md marks this row "Complete" (commit `14d73d1`). |

**Orphaned requirements check:** `.planning/REQUIREMENTS.md` lines 91-95 map exactly `AUDIO-01, AUDIO-02, APP-01, APP-02, APP-03` to Phase 3; all five appear in the `requirements:` frontmatter across the four plans (03-01: AUDIO-02; 03-02: AUDIO-01, AUDIO-02; 03-03: APP-01, APP-02, APP-03; 03-04: AUDIO-02, APP-03). No orphaned requirements.

**Documentation note (not a code gap):** `.planning/REQUIREMENTS.md`'s traceability table (lines 91-95) only shows AUDIO-02 and APP-03 checked/"Complete" — the two requirements listed in 03-04-PLAN's own frontmatter, updated by commit `14d73d1` ("mark AUDIO-02 and APP-03 requirements complete"). AUDIO-01, APP-01, and APP-02 are still shown as unchecked/"Pending" even though the code implementing them (verified both in the prior 03-VERIFICATION.md pass and re-confirmed here as unchanged) has been complete since plans 03-02/03-03. This appears to be an oversight in the original phase-completion documentation step (03-01/02/03 never flipped their own checkboxes), not something 03-04 broke. Flagged as INFO — does not affect code-level goal achievement, but the requirements tracking doc should be corrected for accuracy.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` markers found in any of the four files 03-04 modified, re-scanned directly | — | Clean scan. |
| `src/RigToggle.Core/ToggleService.cs` | 116-127 | Broad `catch (Exception)` around monitor+audio restore, no logging, not isolated between monitor and audio | WARNING | Matches fresh 03-REVIEW.md CR-01/WR-02. Does not block this phase's goal per the independent analysis above (monitor-restore path inert until Phase 4; audio mainline scenario handled per-role without relying on this catch; comprehensive failure surfacing is explicit Phase 5/CORE-04 scope). Recommend a minimal `Trace`/`Debug` log line in this catch as a cheap near-term improvement, and isolating monitor from audio restore before Phase 4 lands a real, fallible monitor `Restore`. |
| `src/RigToggle.Windows/WindowsAudioController.cs` | 162-174 | Per-role catch scoped to `InvalidOperationException` only, not broader COM/lifetime exceptions `ApplyAndVerify` could theoretically throw | WARNING (carried forward, WR-01) | Narrow — `ApplyAndVerify`'s explicit verify-and-throw is the documented/tested failure mode and is caught; broader COM edge cases are rare and would still be caught one level up by `ToggleService`'s outer catch (no crash), just without per-role isolation for that rare case. |
| `src/RigToggle.Windows/WindowsAppController.cs` | 128-140 (`MinimizeIfRunning`) | Unguarded `Process.Refresh()`/`MainWindowHandle` read after enumeration — a process-exit race could throw and skip `_snapshotStore.Clear()` | WARNING (carried forward, WR-03; pre-existing, not introduced by 03-04) | Very narrow window (process must exit between enumeration and property read); not touched by 03-04; recommend wrapping in try/catch or moving `Clear()` into a `finally` as a follow-up, but does not block this phase. |
| repo-wide | — | No logging mechanism (`ILogger`/`Trace`/`Debug`/`EventLog`) exists anywhere in `RigToggle.Core`/`RigToggle.Windows` | INFO (carried forward, IN-02) | Compounds the two WARNINGs above; a trivial `Trace`-based helper would materially improve debuggability without a new dependency. |
| `.planning/REQUIREMENTS.md` | 91-95 | Traceability table understates completion (AUDIO-01/APP-01/APP-02 still "Pending" despite implemented+verified code) | INFO | Documentation bookkeeping gap, not a code defect — see Requirements Coverage section above. |

Git commit hashes cited in 03-04-SUMMARY.md (`6effd28`, `f71709f`, `9443ffe`) and REQUIREMENTS.md update (`14d73d1`) were confirmed present via `git cat-file -e` and cross-checked against `git log` for the modified files — SUMMARY claims about what was committed are corroborated, not fabricated.

### Behavioral Spot-Checks

Not run. Windows-only WinForms/COM-interop/P-Invoke project (`net10.0-windows`), no runnable entry point in this Linux sandbox (no `dotnet`, no Win32/COM APIs available). Step 7b: SKIPPED (no runnable entry points in this environment). All acceptance criteria were confirmed via direct, full source read-through of the current file state plus targeted grep against the exact patterns specified in 03-04-PLAN's `<verify>` blocks.

### Probe Execution

No `scripts/*/tests/probe-*.sh` files exist in this repository, and no plan/summary references probe-based verification. Step 7c: SKIPPED (no probes declared or discovered).

### Human Verification Required

None. The re-verified defect (AUDIO-02/APP-03 restore reliability) and the fresh review's new CR-01 concern are both code-level questions resolvable by static reading (done above), not UX/visual/real-time behaviors needing a human. An on-hardware `dotnet build`/`dotnet test`/manual toggle test on the Windows rig remains outstanding for the whole phase (as every plan's SUMMARY.md notes), but that is an execution-environment limitation (truth #9, UNCERTAIN), not a human-judgment item requiring a decision here.

### Gaps Summary

No blocking gaps. The single previously-identified gap (`WindowsAudioController.Restore` not handling a stale-but-present captured `DeviceId`, causing `ToggleService.ToggleToNormalMode` to get permanently stuck in Rig mode) is closed: `Restore` now stale-checks via `TryResolveDevice` and falls through to the existing friendly-name match, each role's apply is isolated in its own try/catch, and `ToggleToNormalMode`'s restore calls are wrapped so `MinimizeIfRunning`/`_snapshotStore.Clear()` always run afterward — confirmed by direct code read (not restated from SUMMARY.md) and corroborated by a real, currently-passing-by-inspection regression test.

A fresh code review raised a new Critical concern (silent catch-all in `ToggleToNormalMode`, MinimizeIfRunning left unguarded) after this fix landed. Independent re-analysis (see "Independent Assessment" section above) concludes this is a legitimate WARNING-level robustness/observability item — appropriately scoped to Phase 5 (CORE-04, comprehensive partial-failure reporting) and Phase 4 (real, fallible monitor restore) rather than a Phase 3 blocker, since: (a) the monitor-restore-swallow angle is currently inert (Phase 2/3's `WindowsMonitorController.Restore` is a no-op stub that cannot throw), (b) the realistic audio-restore failure scenario this phase's must-haves target (stale device ID) is now handled per-role without relying on the outer swallow, and (c) comprehensive step-by-step failure surfacing is explicitly out of this phase's contract per `03-CONTEXT.md` D-04 and `REQUIREMENTS.md` CORE-04 (mapped to Phase 5). These items are recorded as WARNING/INFO anti-patterns above for future attention, not as gaps blocking this phase.

---

_Verified: 2026-07-24T18:40:00Z_
_Verifier: Claude (gsd-verifier)_
