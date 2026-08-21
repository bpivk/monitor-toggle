---
phase: 25-single-instance-guard
plan: 04
subsystem: infra
tags: [dotnet, mutex, threading, single-instance, defect-fix]

# Dependency graph
requires:
  - phase: 25-single-instance-guard (25-01/25-02/25-03)
    provides: "SingleInstanceGuard, ActivationSignal, the --apply-update bypass, and the InstanceId test-escape-hatch this plan builds on"
provides:
  - "AbandonedMutexException caught inside WaitForInstanceReady, closing CR-01 (25-REVIEW.md Critical / 25-VERIFICATION.md's one blocking gap)"
  - "Guarded readiness-mutex construction in Acquire() (WR-02) degrading to a logged null handle instead of an unhandled startup crash"
  - "Corrected class doc comments (abandoned-mutex reality, WR-01 namespace-divergence accepted-limitation note) and UpdateApplyEntryPoint's IN-01 ordering reword"
affects: [26-auto-update]

# Actuals (#2632)
actuals:
  tokens: 3617
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Abandonment-by-owning-thread-exit as the only construction that produces a genuine AbandonedMutexException in a regression test (Dispose()-based simulation is explicitly banned — it releases cleanly and would go green either way)"

key-files:
  created: []
  modified:
    - src/RigToggle.Core/SingleInstanceGuard.cs
    - src/RigToggle.Tests/SingleInstanceGuardTests.cs
    - src/RigToggle.App/UpdateApplyEntryPoint.cs

key-decisions:
  - "Abandoned wait treated as acquired=true (per .NET's documented contract that an abandoned wait still transfers ownership), routing into the existing release branch rather than returning false, which would re-abandon the mutex for the next waiter"
  - "WR-02's readiness-mutex construction guarded with a broad catch (Exception), deliberately asymmetric to the main mutex's narrow UnauthorizedAccessException catch two lines above it, because the main mutex is correctness-critical (decides primary-vs-duplicate) and the readiness mutex is a best-effort latency optimisation layered over an already-tolerant broadcast path"
  - "WR-01's cross-namespace probe explicitly NOT implemented — accepted as a known limitation at ASVS L1 for this single-user, single-session personal rig; the stronger fix is named in the source but deferred"

requirements-completed: [INSTANCE-01, INSTANCE-02, UPDATE-07]  # Closed on operator authorization (2026-08-21) with PARTIAL hardware evidence -- see Task 3 section and the D1/D4 verification entries below for exactly what was and was not confirmed.

coverage:
  - id: D1
    description: "WaitForInstanceReady catches AbandonedMutexException and treats an abandoned-but-acquired wait as a successful wait, closing CR-01"
    requirement: "INSTANCE-01"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/SingleInstanceGuardTests.cs#WaitForInstanceReady_PrimaryAbandonedReadinessMutex_ReturnsTrueWithoutThrowing"
        status: pass
      - kind: manual_procedural
        ref: "Operator confirms the solution compiles and the app runs/behaves normally on the real Windows rig at this commit"
        status: pass
      - kind: manual_procedural
        ref: "Live Task-Manager-kill reproduction of the CR-01 abandoned-mutex race (Task 3 Check 2)"
        status: not_performed
    human_judgment: true
    rationale: "The automated regression net is green (Linux sandbox). On real Windows hardware, the operator confirmed only that the build compiles and the app functions normally in ordinary use -- NOT the specific CR-01 live repro (kill primary between Acquire()/MarkReady(), confirm no crash dialog) that this plan's Task 3 originally asked for. The operator explicitly declined to run that check ('doesn't work on my pc') and approved closing the plan on the evidence that does exist. See Task 3 section for the operator's verbatim response and the accepted-override rationale."
  - id: D2
    description: "Acquire()'s readiness-mutex construction degrades to a logged null handle instead of escaping the method (WR-02)"
    verification: []
    human_judgment: true
    rationale: "No injection seam exists to force a construction failure without widening the public API; the guard is verified by code review and by the pre-existing facts covering the null-readiness-mutex downstream paths, not by a dedicated new test."
  - id: D3
    description: "Class doc comments corrected to state the true abandoned-mutex picture and record WR-01 as a known accepted limitation; UpdateApplyEntryPoint's ordering claim reworded to match Program.cs (IN-01)"
    verification: []
    human_judgment: false
  - id: D4
    description: "Three consecutive dotnet test RigToggle.sln runs on real Windows hardware, all seven SingleInstanceProcessTests facts green including the three ApplyUpdateBypass_* facts, and no crash dialog on a real CR-01 reproduction"
    requirement: "INSTANCE-02, UPDATE-07"
    verification:
      - kind: manual_procedural
        ref: "Operator: solution builds successfully on real Windows hardware at this commit"
        status: pass
      - kind: manual_procedural
        ref: "Operator: general app functionality confirmed correct by direct use (informal smoke check, not targeted at CR-01/UPDATE-07 specifically)"
        status: pass
      - kind: e2e
        ref: "src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs -- three consecutive `dotnet test RigToggle.sln -c Release --no-build` runs"
        status: not_performed
      - kind: e2e
        ref: "src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs#ApplyUpdateBypass_RunsWhileGuardIsHeld / ApplyUpdateBypass_IsIdempotentAndSideEffectFree / ApplyUpdateBypass_ConcurrentInvocationsDoNotInterfere"
        status: unknown
      - kind: manual_procedural
        ref: "Live CR-01 Task-Manager-kill reproduction (Task 3 Check 2)"
        status: not_performed
    human_judgment: true
    rationale: "Requires the Microsoft.WindowsDesktop.App runtime and real Windows hardware -- cannot run in this Linux sandbox, and Task 3's specific checks (three-run flakiness check, ApplyUpdateBypass_* confirmation, live CR-01 repro) were NOT performed by the operator on this occasion ('doesn't work on my pc'). The operator explicitly authorized closing this plan on the strength of a successful build plus normal app functionality, accepting the remaining evidence gap as an open follow-up rather than a blocker. See Task 3 section below for the verbatim exchange."

duration: ~40min (Tasks 1-2); Task 3 closed via operator override, not the originally-specified verification
completed: 2026-08-21
status: complete
---

# Phase 25 Plan 04: CR-01 Abandoned Readiness Mutex + WR-02/WR-01/IN-01 Hardening Summary

**`WaitForInstanceReady` now catches `AbandonedMutexException` and treats an abandoned-but-acquired wait as success, closing the phase's one blocking crash gap; `Acquire()`'s readiness-mutex construction can no longer escape as an unhandled exception. Task 3's blocking checkpoint was closed by explicit operator authorization on PARTIAL evidence — build succeeds and the app functions normally on real Windows hardware, but the specific flakiness check and live CR-01 repro this task originally asked for were not run. See "Task 3: Operator Verification" below for the exact evidence and what remains open.**

## Performance

- **Duration:** ~40 min for Tasks 1-2; Task 3 closed via operator override rather than the originally-specified verification steps
- **Started:** 2026-08-21
- **Tasks:** 3 of 3 closed (Task 3 closed by operator authorization on partial evidence, not full completion of its `how-to-verify` steps)
- **Files modified:** 3

## Accomplishments

- **CR-01 closed.** `WaitForInstanceReady`'s `readyMutex.WaitOne(remaining)` is now wrapped in a `try`/`catch (AbandonedMutexException)` that sets `acquired = true` and logs the case distinctly, routing control into the existing release branch that clears the abandoned state. A duplicate launch whose primary died between `Acquire()` and `MarkReady()` now completes `WaitForInstanceReady` and returns `true` instead of crashing with an unhandled exception.
- **Regression net proven RED-then-GREEN.** A new xUnit fact, `WaitForInstanceReady_PrimaryAbandonedReadinessMutex_ReturnsTrueWithoutThrowing`, reproduces abandonment by letting a dedicated owner thread exit while still holding the readiness mutex (never via `Dispose()`, which this plan's prohibitions explicitly ban as a no-op simulation). It was observed failing against unfixed code, naming `AbandonedMutexException` in its failure output, then passing after the fix. It uses a locally-generated instance id (not the shared `TestInstanceId`) so its deliberately-unreleased guard cannot poison sibling facts.
- **WR-02 closed.** `Acquire()`'s readiness-mutex construction is now guarded by `if (createdNew) { try { ... } catch (Exception ex) { TryLog(...); } }`, degrading to a logged null readiness handle instead of escaping the method. Every downstream path this produces (`MarkReady()`'s no-op, `Dispose()`'s null-guard, the loser's fail-fast wait, `Program.cs`'s unconditional broadcast) already existed and is already covered by pre-existing facts.
- **Doc comments corrected (CR-01/WR-01/IN-01).** The class doc comment no longer claims the guard is categorically free of abandoned-mutex risk — it now names the readiness mutex as the one call site that genuinely carries the risk and names the fact that proves the fix. The `Global\`/`Local\` namespace-divergence risk (WR-01) is recorded as a known, accepted limitation with its trigger and its deferred stronger fix named. `UpdateApplyEntryPoint`'s doc comment now names `SetColorMode`/`ApplicationConfiguration.Initialize` explicitly, matching `Program.cs`'s own precise ordering claim (IN-01).
- **Task 3 closed by operator authorization, on partial evidence.** The phase's remaining evidence gap — three consecutive `dotnet test RigToggle.sln -c Release --no-build` runs, the three `ApplyUpdateBypass_*` facts confirmed green, and a real-hardware CR-01 reproduction — requires the `Microsoft.WindowsDesktop.App` runtime and real Windows hardware, neither available in this Linux sandbox. The operator ran this checkpoint on their own rig and reported that the specific automated-suite run "doesn't work on my pc" (reason unspecified), but that the solution compiles and the app works correctly in direct use. The operator explicitly authorized closing the plan on that basis. This is recorded honestly below, not inferred or fabricated as a pass of the original ask.

## Task Commits

Each task was committed atomically:

1. **Task 1, Step 2 (RED):** `1dc54bd` — `test(25-04): add failing abandoned-readiness-mutex regression test for CR-01`
2. **Task 1, Step 4 (GREEN):** `eb30b78` — `fix(25-04): catch AbandonedMutexException in WaitForInstanceReady (CR-01)`
3. **Task 2:** `9d555f8` — `fix(25-04): guard readiness-mutex construction; correct doc comments (WR-02, WR-01, IN-01)`

**Task 3 (blocking human-verify checkpoint): closed by operator authorization, not by completing its `how-to-verify` steps.** See "Task 3: Operator Verification" section below for the verbatim exchange and exact evidence.

_Task 1 is `tdd="true"`, so it produced the mandatory RED-then-GREEN commit pair rather than a single commit; no REFACTOR commit was needed._

## Files Created/Modified

- `src/RigToggle.Core/SingleInstanceGuard.cs` — `WaitForInstanceReady` gains the `catch (AbandonedMutexException)`; `Acquire()`'s readiness-mutex construction is now guarded; class doc comment's abandoned-mutex and namespace-fallback paragraphs corrected.
- `src/RigToggle.Tests/SingleInstanceGuardTests.cs` — new fact `WaitForInstanceReady_PrimaryAbandonedReadinessMutex_ReturnsTrueWithoutThrowing`; `using System.Threading;` added.
- `src/RigToggle.App/UpdateApplyEntryPoint.cs` — doc comment reworded to name the two position-sensitive bootstrap calls that precede it (IN-01). `Run(string[])`'s body is unchanged.

## RED Test Output (Task 1, Step 2 — mandatory, verbatim)

Command: `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -c Release --filter "FullyQualifiedName~WaitForInstanceReady_PrimaryAbandonedReadinessMutex"`, run against the test-only commit (`1dc54bd`), before the production fix existed:

```
[xUnit.net 00:00:00.27]     RigToggle.Tests.SingleInstanceGuardTests.WaitForInstanceReady_PrimaryAbandonedReadinessMutex_ReturnsTrueWithoutThrowing [FAIL]
  Failed RigToggle.Tests.SingleInstanceGuardTests.WaitForInstanceReady_PrimaryAbandonedReadinessMutex_ReturnsTrueWithoutThrowing [12 ms]
  Error Message:
   CR-01: an abandoned wait must be caught inside WaitForInstanceReady and treated as a successful acquisition, not left to escape as AbandonedMutexException.
  Stack Trace:
     at RigToggle.Tests.SingleInstanceGuardTests.WaitForInstanceReady_PrimaryAbandonedReadinessMutex_ReturnsTrueWithoutThrowing() in /home/bpivk/moza/src/RigToggle.Tests/SingleInstanceGuardTests.cs:line 264
   at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(Object obj, IntPtr* args)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)

Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1, Duration: 12 ms - RigToggle.Tests.dll (net10.0)
```

The assertion is `Assert.False(exception is AbandonedMutexException, "CR-01: ...")` — it fails precisely because the captured exception genuinely *was* an `AbandonedMutexException` thrown by the unguarded `readyMutex.WaitOne(remaining)` call, and the custom assertion message names that type verbatim in the failure output, satisfying this task's mandatory RED-observation requirement.

## GREEN Output (Task 1, Step 4 — verbatim)

Filtered run immediately after the production fix (commit `eb30b78`):

```
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 11 ms - RigToggle.Tests.dll (net10.0)
```

Full unfiltered suite, run twice back to back:

```
Passed!  - Failed:     0, Passed:   121, Skipped:     0, Total:   121, Duration: 428 ms - RigToggle.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:   121, Skipped:     0, Total:   121, Duration: 426 ms - RigToggle.Tests.dll (net10.0)
```

Baseline captured before any change (precondition check): `Passed!  - Failed:     0, Passed:   120, ... Duration: 424 ms` — confirms exactly one more passing fact than baseline, `Failed: 0` both times, identical result on rerun (the new fact's deliberately-unreleased guard leaks into nothing).

## Explicit Note: No Test for WR-02's Construction-Failure Path

No test was added for the guarded readiness-mutex construction in `Acquire()`. There is no seam to inject a `Mutex` construction failure through — `new Mutex(initiallyOwned: true, readyEventName, out _)` fails only on OS-level conditions (e.g. a name collision with an incompatible existing kernel object, or a security-descriptor mismatch) that cannot be triggered deterministically from a unit test without either mocking the `Mutex` constructor (not possible — it is a sealed BCL type) or widening `SingleInstanceGuard`'s public API specifically to allow fault injection. Both options are a larger, riskier change than the two-line guard they would cover. The degraded (null-readiness-mutex) state this guard produces on failure is not a new code path — it is the exact state a non-primary guard already produces every time, and is already exercised by `MarkReady_OnNonPrimaryGuard_DoesNotThrow` and `WaitForInstanceReady_NoInstancePublished_ReturnsFalseQuickly`, both pre-existing and still green.

## Explicit Note: WR-01's Structural Fix Deliberately Deferred

WR-01 (the `Global\`/`Local\` cross-process namespace-divergence risk — two processes whose tokens disagree about `Global\` access can each resolve a different namespace and each believe themselves primary) is NOT fixed in this plan. The class doc comment now records it as a known, accepted limitation: its trigger (mismatched security contexts — one elevated launch and one not, or two login/RDP sessions), why it is accepted (ASVS level 1 for this single-user personal rig, where every launch is the same non-elevated user in one session, and the failure has never been observed), and the deferred stronger fix (probe the opposite namespace via `Mutex.TryOpenExisting` before concluding primary, so a scope mismatch degrades to "assume duplicate and broadcast" instead of "assume primary"). It is deferred because it changes single-instance detection semantics and carries more regression risk than the failure it prevents. This is this plan's one deliberately-not-covered backstop truth (see `25-04-PLAN.md`'s `must_haves.truths[7]`).

## Task 3: Operator Verification — Closed on Partial Evidence

Task 3 was authored as a `type="checkpoint:human-verify" gate="blocking"` task with two specific checks: (1) three consecutive `dotnet test RigToggle.sln -c Release --no-build` runs confirming `Failed: 0` every time and the three `ApplyUpdateBypass_*` facts green, and (2) a live reproduction of the CR-01 abandoned-mutex race by killing the primary process from Task Manager mid-startup while a second copy launches, confirming no crash dialog appears.

**Operator's verbatim response:**

> "This as discussed doesn't work on my pc but compiling works and the app works fine so approve it"

**What this confirms, precisely:**
- The solution builds successfully on real Windows hardware at this commit (`dotnet build` / equivalent IDE build succeeds).
- The app functions correctly in general, informal, direct use on the rig — not a targeted exercise of the CR-01 scenario or the `RigToggle.Windows.Tests` suite specifically.

**What this does NOT confirm — explicitly not performed:**
- The three-consecutive-clean-runs flakiness check of `dotnet test RigToggle.sln -c Release --no-build` was not run. The operator reported the automated test-suite run itself "doesn't work on my pc" — the specific cause (test host / environment issue vs. an actual test failure) is unreported and unknown.
- The three `ApplyUpdateBypass_*` facts (UPDATE-07's outstanding evidence, recorded as "unknown" since 25-03-SUMMARY.md) remain unconfirmed. Their status is still `unknown`, not `pass`.
- The live CR-01 reproduction (killing the primary via Task Manager between `Acquire()` and `MarkReady()`, confirming the duplicate exits quietly with no crash dialog, and checking `debug.log` for the abandoned-case log line) was not attempted. No confirmation exists — positive or negative — that the fix behaves correctly under the exact real-hardware race condition it was written to close.

**Disposition:** the operator, working on their own rig, made the call that a successful build plus confirmed normal app operation is sufficient evidence to close this plan, and explicitly authorized doing so rather than running the originally-requested checks. This is treated the same way this project has previously handled an operator override of a planned verification step — see `.planning/STATE.md`'s Phase 16/DISPLAY-13 precedent (formally waived by the user rather than tested, recorded as a documented override rather than a fabricated pass). The three-run flakiness check, the `ApplyUpdateBypass_*` confirmation, and the live CR-01 repro remain **open, unperformed follow-up items** — not closed, not failed, just not done. If flakiness or a real crash surfaces later in ordinary use, that is new information this SUMMARY does not rule out.

## Decisions Made

- Abandoned wait treated as `acquired = true` rather than `false` — the .NET contract for `AbandonedMutexException` is that the wait succeeded and ownership transferred; returning `false` would skip the release that clears the abandoned state and re-abandon the mutex for the next waiter, converting one dead primary into an unbounded exception chain.
- WR-02's catch is deliberately broad (`catch (Exception ex)`), asymmetric to the main mutex's narrow `catch (UnauthorizedAccessException)` two lines above — the main mutex decides primary-vs-duplicate and is correctness-critical, while the readiness mutex is a best-effort latency optimisation layered over an already-tolerant broadcast path.
- Abandonment in the regression test is produced by owning-THREAD exit (a dedicated `Thread` that acquires the guard and returns immediately), not by `guard.Dispose()` — `Dispose()` releases the readiness mutex cleanly and would produce a permanently-green test that proves nothing, per this plan's explicit prohibition.
- WR-01's structural fix (cross-namespace probe) is deliberately not implemented — see the dedicated note above.

## Deviations from Plan

None beyond what the plan itself anticipated and explicitly permitted (the documented deviation from 25-VERIFICATION.md's/25-REVIEW.md's literal `Dispose()`-based simulation wording, which this plan's own prohibitions call out and supersede with the owning-thread-exit construction). No unplanned auto-fixes were needed — Tasks 1 and 2 executed exactly as specified, and every acceptance-criteria grep gate and build/test gate passed on the first attempt.

## Issues Encountered

- Pre-existing condition, out of scope: `dotnet build src/RigToggle.Tests/RigToggle.Tests.csproj -c Release` reports 6 `xUnit1031` warnings (blocking-task-in-test-method), all pre-existing before this plan began (4 in `ToggleOrchestratorTests.cs`, 2 on the pre-existing `WaitForInstanceReady_ReadinessPublishedWhileWaitInProgress_ReturnsTrue` fact in `SingleInstanceGuardTests.cs`, confirmed present in the baseline test run captured before any edit in this plan). Task 2's acceptance criteria state the Tests build should show `0 Warning(s)`, which was already untrue at baseline and is not caused by this plan's changes — per this workflow's scope-boundary rule, pre-existing warnings in unrelated code are out of scope for this plan and were left untouched rather than "fixed" to satisfy a criterion this plan's own changes did not break.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

**Closed by operator authorization; the underlying evidence gap is not fully closed.** This plan is marked `complete` because the operator, on their own real Windows hardware, explicitly approved closing it on the strength of a successful build and confirmed normal app operation — see "Task 3: Operator Verification" above for the verbatim exchange. The following remain genuinely open follow-up items, not fabricated as done:

1. Three consecutive `dotnet test RigToggle.sln -c Release --no-build` runs confirming `Failed: 0` every time, including the three `ApplyUpdateBypass_*` facts (UPDATE-07) explicitly green — never run. 25-03-SUMMARY.md already recorded these as "unknown"; that status is unchanged by this plan.
2. A real reproduction of the CR-01 race (kill the primary between launch and window-appearing while a second copy is launching), confirming the second process exits quietly with no crash dialog, and a check of `%LOCALAPPDATA%\RigToggle\debug.log` for the new abandoned-case log line — not attempted.

Phase 25's requirements (INSTANCE-01, INSTANCE-02, UPDATE-07) are marked complete on this plan's authority per the operator's explicit sign-off, but the strongest available evidence for the two items above is still the code-level regression test (Task 1) and code review (Task 2), not a real-hardware confirmation of the exact scenarios they were meant to prove. If either item is later run and finds a problem, that supersedes this closure.

---
*Phase: 25-single-instance-guard*
*Completed: 2026-08-21 (Tasks 1-2 executed in full; Task 3 closed by operator authorization on partial evidence — see Task 3 section)*
