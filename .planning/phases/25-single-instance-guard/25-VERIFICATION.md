---
phase: 25-single-instance-guard
verified: 2026-08-21T00:00:00Z
status: gaps_found
score: 2/5 must-haves verified
behavior_unverified: 2 # SC1 (rapid-relaunch/tight-race) and SC3 (apply-update bypass) automated e2e tests exist, are wired, but have not been observed to pass (RigToggle.Windows.Tests cannot execute in this Linux sandbox, and the operator's own SUMMARY records their status as "unknown" — manual/log evidence substitutes only for SC2)
overrides_applied: 0
gaps:
  - truth: "The single-instance guard mechanism never crashes a process due to its own kernel-object handling (Security-Domain-V5 never-crash-on-startup contract, T-25-06, and 25-01 must_haves: 'Startup never crashes because of the guard')"
    status: failed
    reason: >
      SingleInstanceGuard.WaitForInstanceReady's readyMutex.WaitOne(remaining) call
      (src/RigToggle.Core/SingleInstanceGuard.cs, ~lines 270-286) has no try/catch for
      System.Threading.AbandonedMutexException. If the primary instance terminates
      abnormally after Acquire() but before MarkReady() — a window that spans
      ApplicationConfiguration.Initialize(), StartupRecoveryChecker.Run, MainForm
      construction, InitializeTrayState(), and RegisterHotkeyAtStartup() in Program.cs —
      while a duplicate ("loser") process is concurrently blocked in
      WaitForInstanceReady (exactly the "Pitfall 8 tight race" scenario this phase built
      dedicated tests for), the OS marks the readiness mutex abandoned and the loser's
      WaitOne throws an uncaught AbandonedMutexException, crashing the duplicate process
      with an unhandled exception instead of gracefully broadcasting and exiting. There
      is no try/catch at the only production call site (src/RigToggle.App/Program.cs:247)
      and no AppDomain.UnhandledException/Application.ThreadException handler anywhere in
      RigToggle.App as a last resort. This is 25-REVIEW.md's sole Critical/blocker
      finding (CR-01), dated 2026-08-21, and remains unfixed as of the phase's latest
      commit (5954add docs(25): add code review report) — no subsequent commit touches
      SingleInstanceGuard.cs. Confirmed independently: `grep -n "AbandonedMutexException"
      src/RigToggle.Core/SingleInstanceGuard.cs` matches only inside doc-comment prose
      (the class doc comment's blanket "no abandoned-mutex risk" claim, which covers the
      main mutex's `new Mutex(...)` constructor path but does NOT cover the separate
      readiness mutex's `WaitOne` call), never inside a catch clause. Neither
      SingleInstanceGuardTests nor SingleInstanceProcessTests exercises this path, so
      nothing in CI would catch a regression here either.
    artifacts:
      - path: "src/RigToggle.Core/SingleInstanceGuard.cs"
        issue: "WaitForInstanceReady's `bool acquired = readyMutex.WaitOne(remaining);` (inside the `using (readyMutex) { ... }` block) is unguarded against AbandonedMutexException."
      - path: "src/RigToggle.App/Program.cs"
        issue: "Line 247, the only production call site (`bool becameReady = SingleInstanceGuard.WaitForInstanceReady(...)`), has no try/catch around it, unlike the TryLog calls immediately around it which are individually try/caught."
    missing:
      - "Wrap readyMutex.WaitOne(remaining) in try/catch (AbandonedMutexException) and treat an abandoned-but-acquired wait as a successful wait — the fix is already specified verbatim in 25-REVIEW.md CR-01."
      - "Add a SingleInstanceGuardTests case that acquires the readiness mutex, disposes the guard without calling MarkReady() first (simulating the abandoned-mutex scenario), and asserts WaitForInstanceReady still returns true rather than throwing, so this path is covered by the same CI regression net that already covers the rest of Pitfall 8."
deferred: []
behavior_unverified_items:
  - truth: "ROADMAP SC1 — after 10 rapid duplicate launches and a 3-round tight-race launch against a running instance, exactly one RigToggle.App process remains alive every time (SingleInstanceProcessTests.RapidRelaunch_ExactlyOneProcessSurvives / TightRaceLaunch_ExactlyOneProcessSurvives)"
    test: "On Windows: `dotnet test RigToggle.sln -c Release --no-build`, specifically the RigToggle.Windows.Tests project, run three times consecutively"
    expected: "Both facts pass identically across all three runs, with zero flakiness"
    why_human: "RigToggle.Windows.Tests requires the Microsoft.WindowsDesktop.App runtime, absent in this Linux sandbox — the tests cannot be executed here at all, only read and grep-verified for presence/wiring/no-skip-attribute."
  - truth: "ROADMAP SC3 — --apply-update exits with StartupArgs.ApplyUpdateBypassExitCode while the single-instance mutex is held, is idempotent on repeat, and three concurrent bypass launches do not interfere with each other or a live primary (ApplyUpdateBypass_RunsWhileGuardIsHeld / _IsIdempotentAndSideEffectFree / _ConcurrentInvocationsDoNotInterfere)"
    test: "Same Windows-only RigToggle.Windows.Tests run as above"
    expected: "All three facts pass identically across all three runs"
    why_human: "Same execution-environment constraint as SC1. Additionally, 25-03-SUMMARY.md's own coverage table records these three facts' status as 'unknown' — the operator's Task 3 investigation verified INSTANCE-01/INSTANCE-02 manually via a PowerShell repro script and direct log/visual inspection, but did not report running these specific bypass facts to a confirmed green result."
human_verification:
  - test: "Run `dotnet test RigToggle.sln -c Release --no-build` three times consecutively on the operator's Windows rig (the flakiness check 25-03-PLAN.md's Task 3 originally requested and 25-03-SUMMARY.md's own 'Open follow-up' section records as not yet completed)"
    expected: "Identical pass/fail results across all three runs, all 7 SingleInstanceProcessTests facts green every time"
    why_human: "Requires real Windows hardware with the .NET 10 SDK; cannot run in this Linux verification sandbox. This is the single evidence gap the phase's own SUMMARY explicitly flags as open, distinct from the CR-01 gap above."
  - test: "After CR-01 is fixed, reproduce the abandoned-readiness-mutex scenario (kill the primary process between guard acquisition and MarkReady() while a duplicate is blocked in WaitForInstanceReady) and confirm the duplicate no longer crashes"
    expected: "The duplicate process gracefully returns (broadcasts and exits, or falls through some other non-crashing path) instead of terminating with an unhandled AbandonedMutexException"
    why_human: "Requires deliberately killing a real Windows process at a precise point in a race window — not reproducible via static analysis, and blocked on the CR-01 code fix landing first."
---

# Phase 25: Single-Instance Guard Verification Report

**Phase Goal:** Users can never end up with two Rig Toggle processes running side by side; a duplicate launch attempt surfaces the existing instance instead, and the guard exposes a deliberate bypass for the app's own future internal relaunches.
**Verified:** 2026-08-21
**Status:** gaps_found
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | SC1 — exactly one process survives rapid duplicate launches (scripted rapid-relaunch + tight-race tests) | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | `SingleInstanceGuard`, `Program.cs` guard-acquisition ordering, and `SingleInstanceProcessTests.RapidRelaunch_ExactlyOneProcessSurvives`/`TightRaceLaunch_ExactlyOneProcessSurvives` all exist and are wired (verified via grep, line-ordering, and build). Cannot execute in this Linux sandbox (`RigToggle.Windows.Tests` needs `Microsoft.WindowsDesktop.App`). Operator manually reproduced a 2-launch scenario multiple times post-fix with no crash, but the automated facts themselves and the requested 3-consecutive-run flakiness check are not yet confirmed. |
| 2 | SC2 — duplicate launch restores and focuses the already-running (possibly tray-hidden/minimized) instance | ✓ VERIFIED | `MainForm.RestoreAndFocus()` wired from both the tray-click handler and the `WndProc` activation branch; `base.WndProc` stays unconditional and last. Operator confirmed on real hardware via corrected debug.log capture (`IsForegroundWindow=True`, `ContainsFocus=True` after every `RestoreAndFocus()` call — ground-truth `GetForegroundWindow()==Handle`, not the misleading `Control.Focused`) plus direct visual observation that the window jumps to foreground on a real duplicate launch. This is strong, direct hardware evidence exceeding what a single automated test run would provide. |
| 3 | SC3 — `--apply-update` bypass exits distinguishably from a normal duplicate-launch exit, is idempotent, and is safe under concurrent invocation alongside a live primary | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Bypass branch is the first branch in `Main()`, strictly above guard acquisition (line-ordering assertions pass); `UpdateApplyEntryPoint.Run` is side-effect-free (comment-filtered negative grep passes); `SingleInstanceProcessTests.ApplyUpdateBypass_*` (3 facts) exist and are wired to the real `StartupArgs.ApplyUpdateFlag`/`ApplyUpdateBypassExitCode` constants. Cannot execute here; 25-03-SUMMARY.md's own coverage table records these three facts' status as "unknown" — not reported run to green by the operator. |
| 4 | The guard mechanism never crashes a process due to its own kernel-object handling (never-crash-on-startup contract, T-25-06) | ✗ FAILED | `SingleInstanceGuard.WaitForInstanceReady`'s `readyMutex.WaitOne(remaining)` has no catch for `AbandonedMutexException`. Confirmed unfixed via direct source read and `grep -n "AbandonedMutexException" src/RigToggle.Core/SingleInstanceGuard.cs` (only doc-comment matches, no catch clause). This is 25-REVIEW.md's sole Critical finding (CR-01), unaddressed as of the latest commit. See Gaps Summary. |
| 5 | No fail-open, single duplicate-launch branch (D-02), silent restore (D-01), zero new NuGet packages, clean build/regression | ✓ VERIFIED | All Task 3 (25-01) and equivalent gates re-confirmed: `grep -c 'IsPrimaryInstance' src/RigToggle.App/Program.cs` → 1; no `ShowBalloonTip`/`MessageBox`/`ToolTipIcon` in the touched files; `RigToggle.Core.csproj` has zero actual `<PackageReference>` elements; `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -c Release` independently re-run here → 120/120 passing, 0 failures. |

**Score:** 2/5 truths verified (2 present, behavior-unverified; 1 failed)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/RigToggle.Core/SingleInstanceGuard.cs` | Named cross-process mutex primitive, readiness handshake | ✓ VERIFIED (with gap) | Exists, 358 lines, compiles, wired into `Program.cs`. Contains the CR-01 crash gap (see Truth 4). |
| `src/RigToggle.Windows/ActivationSignal.cs` | Public facade over broadcast P/Invoke | ✓ VERIFIED | Exists, 181 lines, wired into `MainForm.WndProc`. |
| `src/RigToggle.Windows/NativeMethods.cs` | `HWND_BROADCAST`, `RegisterWindowMessage`, `PostMessage`, `AllowSetForegroundWindow`, `GetForegroundWindow` | ✓ VERIFIED | Exists, 194 lines. |
| `src/RigToggle.App/Program.cs` | Guard acquisition above bootstrap, one duplicate-launch branch, apply-update bypass branch above the guard | ✓ VERIFIED | Exists, 403 lines. `TryGetApplyUpdateArgs` check precedes `using var guard = SingleInstanceGuard.Acquire();` (line 116 vs 226); `IsPrimaryInstance` branch appears exactly once (line 237). |
| `src/RigToggle.App/MainForm.cs` | `RestoreAndFocus()` helper, `WndProc` activation branch | ✓ VERIFIED | Exists, 1995 lines. `RestoreAndFocus()` called from tray click (line 1738) and `WndProc` (line 317); `base.WndProc` unconditional and last (line 320). |
| `src/RigToggle.Core/StartupArgs.cs` | `ApplyUpdateFlag`, `TryGetApplyUpdateArgs`, `ApplyUpdateBypassExitCode` | ✓ VERIFIED | Exists, 76 lines. |
| `src/RigToggle.App/UpdateApplyEntryPoint.cs` | Side-effect-free placeholder relaunch entry point | ✓ VERIFIED | Exists, 39 lines, `Run(string[])` returns the bypass exit code only. |
| `src/RigToggle.Tests/SingleInstanceGuardTests.cs` | In-process xUnit coverage | ✓ VERIFIED | Exists, 200 lines, part of the 120/120 passing cross-platform suite. Does NOT cover the CR-01 abandoned-mutex path. |
| `src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs` | Real child-process e2e coverage, 6+ facts | ✓ VERIFIED (present, unexecuted here) | Exists, 615 lines, 7 `[Fact]`s (6 planned + 1 added during the Task 3 investigation), no `Skip`/`Trait` exclusions. Cannot execute in this sandbox. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `Program.cs` bypass branch | `UpdateApplyEntryPoint.Run` | direct call, above guard acquisition | ✓ WIRED | Line-ordering confirmed: `StartupArgs.TryGetApplyUpdateArgs` (116) < `using var guard` (226). |
| `Program.cs` duplicate-launch branch | `ActivationSignal.BroadcastActivation` | via `WaitForInstanceReady` then broadcast | ✓ WIRED | Lines 247-251. |
| `MainForm.WndProc` | `RestoreAndFocus()` | activation-message branch | ✓ WIRED | Line 317, guarded by `MessageId != 0` (line 303). |
| `SingleInstanceGuard.Acquire()` | readiness mutex (`WaitForInstanceReady`) | shared `IsGlobalScope`-derived name prefix | ✓ WIRED (but see Truth 4) | Both names built from one resolved prefix; the readiness mutex's `WaitOne` call is the unguarded gap. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/RigToggle.Core/SingleInstanceGuard.cs` | ~186-192 (WR-02, 25-REVIEW.md) | Readiness-mutex construction has no try/catch fallback of its own | ⚠️ Warning | If construction throws for any reason, the exception propagates out of `Acquire()` before any log line, and the already-acquired main mutex is never released. Documented in code review, not fixed. Not blocking per this verification (lower likelihood than CR-01, no confirmed real-world trigger), but should be tracked. |
| `src/RigToggle.Core/SingleInstanceGuard.cs` | ~162-197 (WR-01, 25-REVIEW.md) | `Global\`/`Local\` namespace resolution can diverge silently between two processes running under different security contexts, defeating the single-instance guarantee | ⚠️ Warning | Low likelihood for this single-user rig's normal usage (would require e.g. one launch "Run as Administrator" and another not); accepted at ASVS L1 in the phase's own threat model but the class doc comment overstates the fallback's safety. Not blocking. |
| `src/RigToggle.App/UpdateApplyEntryPoint.cs` | 8-9 (IN-01, 25-REVIEW.md) | Doc comment overstates ordering relative to `Program.Main()` (says "before any other bootstrap step" when `SetColorMode`/`ApplicationConfiguration.Initialize()` actually run first) | ℹ️ Info | Documentation-only, load-bearing property (before the guard) still holds. Not blocking. |

No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` markers found in any of the 9 phase-touched files.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Cross-platform suite regression | `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -c Release --no-restore` (run independently by this verifier) | `Passed: 120, Failed: 0, Skipped: 0` | ✓ PASS |
| CR-01 abandoned-mutex path has no test coverage | `grep -n "AbandonedMutex" src/RigToggle.Tests/SingleInstanceGuardTests.cs src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs` | no matches (exit 1) | ✓ CONFIRMED GAP (no coverage exists) |
| Windows-only e2e suite (`SingleInstanceProcessTests`, 7 facts) | N/A | N/A | ? SKIP — requires `Microsoft.WindowsDesktop.App`, absent in this Linux sandbox |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| INSTANCE-01 | 25-01, 25-03 | Launching while an instance is already running does not start a second instance | ✗ BLOCKED | Wiring and in-process mutex semantics verified; real-process end-to-end tests present but unexecuted here and unconfirmed by the operator; the CR-01 crash gap directly threatens the reliability of this exact code path (the readiness wait every duplicate launch goes through) |
| INSTANCE-02 | 25-01, 25-03 | Already-running instance is brought to focus | ✓ SATISFIED | Strong direct-hardware evidence (log ground truth + visual confirmation) from the operator's Task 3 investigation |
| UPDATE-07 | 25-02, 25-03 | Update-apply relaunch always succeeds despite the guard | ? NEEDS HUMAN | Bypass branch runs before the guard is ever touched (verified structurally); the bypass path is entirely unaffected by CR-01 (it never reaches `WaitForInstanceReady`); real-process bypass tests exist but their pass/fail status is "unknown" per 25-03-SUMMARY.md's own coverage table |
| PERF-03 | Phase 24 | (out of this phase's scope — listed complete in REQUIREMENTS.md) | N/A | Not this phase's concern |

REQUIREMENTS.md's traceability table still lists INSTANCE-01/INSTANCE-02/UPDATE-07 as "Pending" rather than "Complete" — consistent with this verification's `gaps_found` outcome; no discrepancy to flag beyond the gap itself.

No orphaned requirements: all three IDs declared in this phase's plans (`25-01: [INSTANCE-01, INSTANCE-02]`, `25-02: [UPDATE-07]`, `25-03: [INSTANCE-01, INSTANCE-02, UPDATE-07]`) match REQUIREMENTS.md's Phase 25 mapping exactly.

### Human Verification Required

### 1. Three consecutive clean Windows test runs (flakiness check)

**Test:** Run `dotnet test RigToggle.sln -c Release --no-build` three times in a row on the operator's Windows rig.
**Expected:** Identical results across all three runs, all 7 `SingleInstanceProcessTests` facts green every time.
**Why human:** Requires real Windows hardware; cannot execute in this Linux sandbox. This is the exact check 25-03-PLAN.md's Task 3 originally requested and 25-03-SUMMARY.md's own "Open follow-up, NOT closed out by this SUMMARY" section records as still outstanding — the operator's actual Task 3 verification pivoted into fixing two real production crash/focus bugs found along the way (both confirmed fixed via log/visual evidence) but did not complete the specific 3-run flakiness pass.

### 2. Confirm the CR-01 fix once applied

**Test:** After the AbandonedMutexException catch is added, deliberately kill the primary process between `SingleInstanceGuard.Acquire()` and `guard.MarkReady()` while a duplicate is concurrently blocked in `WaitForInstanceReady`, and observe the duplicate's behavior.
**Expected:** The duplicate process no longer terminates with an unhandled `AbandonedMutexException` — it should gracefully complete its `WaitForInstanceReady` call (treating the abandoned mutex as "acquired") and proceed to broadcast/exit as normal.
**Why human:** Requires precisely timed real-process termination on real Windows; not reproducible via static analysis or in this sandbox, and blocked on the code fix landing first.

### Gaps Summary

One Critical, unresolved code-review finding (CR-01, 25-REVIEW.md) sits directly in the single-instance guard's readiness-wait path: `SingleInstanceGuard.WaitForInstanceReady`'s `readyMutex.WaitOne(remaining)` call has no catch for `AbandonedMutexException`. If the primary process dies between winning the mutex and calling `MarkReady()` — a startup window that spans several bootstrap steps — while a duplicate ("loser") process is concurrently waiting for readiness (precisely the "tight race" scenario this phase built dedicated tests for), the duplicate crashes with an unhandled exception instead of gracefully broadcasting and exiting. This directly contradicts the phase's own stated "never crash on startup" (Security-Domain-V5) design posture, is untested by either of this phase's test suites, and was reviewed and flagged as the review's sole blocker over 24 hours before this verification with no follow-up commit addressing it.

Two further Warning-level findings from the same review (WR-01: `Global\`/`Local\` namespace resolution can diverge between processes with different security contexts, silently producing two "primary" instances; WR-02: the readiness mutex's own construction has no fallback/guard) remain unresolved as well, but are lower-likelihood for this single-user personal-rig deployment and are recorded here as warnings rather than blockers.

Separately from the code gap, the phase's own SUMMARY.md documents an explicitly incomplete verification step: the three-consecutive-clean-run flakiness check that 25-03-PLAN.md's Task 3 checkpoint originally required has not been completed. The operator's actual Windows-hardware investigation was extensive and found/fixed two real production bugs (a `SetColorMode`/`SystemEvents` startup crash and a silently-ineffective foreground-activation grant) with strong log/visual evidence for INSTANCE-01 and INSTANCE-02, but did not report running the automated `RigToggle.Windows.Tests` suite to a confirmed stable green result, nor specifically exercising the three `--apply-update` bypass facts.

**What would move this to passed:** (1) fix CR-01 per the review's own specified patch and add the abandoned-mutex regression test; (2) the operator completes the three-consecutive-clean-run check on real Windows hardware and reports the result, including the bypass facts' pass/fail status.

---

_Verified: 2026-08-21_
_Verifier: Claude (gsd-verifier)_
