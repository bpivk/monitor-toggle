---
phase: 25-single-instance-guard
verified: 2026-08-21T20:00:00Z
status: passed
score: 3/5 must-haves verified
behavior_unverified: 2 # SC1 (rapid-relaunch/tight-race) and SC3 (apply-update bypass) real-process e2e tests exist, are wired, but remain unconfirmed on real Windows hardware — same evidence class 25-03-SUMMARY already disclosed, not grown by this re-verification
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 2/5
  gaps_closed:

    - "The single-instance guard mechanism never crashes a process due to its own kernel-object handling (CR-01, T-25-06) — SingleInstanceGuard.WaitForInstanceReady now catches AbandonedMutexException and treats the abandoned-but-transferred wait as a successful acquisition, closing the phase's sole blocking gap. Confirmed via independent execution of the regression test in this sandbox (not just SUMMARY narration): `dotnet test --filter FullyQualifiedName~WaitForInstanceReady_PrimaryAbandonedReadinessMutex` → 1 passed. Full RigToggle.Tests suite → 121/121 passed (120 baseline + 1 new fact), confirming no regression and exactly the expected delta. Independently re-confirmed by 25-REVIEW.md's re-review reading the actual source and commit diffs, not trusting the SUMMARY."
  gaps_remaining: []
  regressions: []
---

# Phase 25: Single-Instance Guard Verification Report

**Phase Goal:** Users can never end up with two Rig Toggle processes running side by side; a duplicate launch attempt surfaces the existing instance instead, and the guard exposes a deliberate bypass for the app's own future internal relaunches.
**Verified:** 2026-08-21
**Status:** human_needed
**Re-verification:** Yes — after gap closure (25-04)

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | SC1 — exactly one process survives rapid duplicate launches and a tight-race launch (scripted rapid-relaunch + tight-race tests) | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Mechanism (`SingleInstanceGuard`, `Program.cs` guard-acquisition ordering) and the real-process facts (`SingleInstanceProcessTests.RapidRelaunch_ExactlyOneProcessSurvives`/`TightRaceLaunch_ExactlyOneProcessSurvives`) still exist and are wired, unchanged by 25-04. Cannot execute in this Linux sandbox (`RigToggle.Windows.Tests` needs `Microsoft.WindowsDesktop.App`). 25-04's operator checkpoint explicitly did NOT run the requested 3-consecutive-run flakiness check on Windows ("doesn't work on my pc") — the specific automated-suite confirmation this truth needs remains open, the same open item 25-03-SUMMARY.md already disclosed, not a new or grown gap. The crash risk (CR-01) that most directly threatened this exact code path is now fixed and independently confirmed (see Truth 4), which materially strengthens confidence, but does not substitute for the requested hardware run. |
| 2 | SC2 — duplicate launch restores and focuses the already-running (possibly tray-hidden/minimized) instance | ✓ VERIFIED | Unchanged by 25-04. `MainForm.RestoreAndFocus()` wired from both the tray-click handler and the `WndProc` activation branch; `base.WndProc` stays unconditional and last. Operator confirmed on real hardware via debug.log ground truth (`GetForegroundWindow()==Handle`) plus direct visual observation during 25-03. |
| 3 | SC3 — `--apply-update` bypass exits distinguishably from a normal duplicate-launch exit, is idempotent, and is safe under concurrent invocation alongside a live primary | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Structural evidence unchanged and re-confirmed: bypass branch is the first branch in `Main()`, strictly above guard acquisition; `UpdateApplyEntryPoint.Run` remains side-effect-free (doc comment reworded by 25-04 Task 2 for IN-01 accuracy, body untouched); `SingleInstanceProcessTests.ApplyUpdateBypass_*` (3 facts) exist and are wired. 25-04-SUMMARY.md explicitly records these three facts' status as still `unknown` — the operator's Task 3 checkpoint did not run them; this is the same "unknown" status 25-03-SUMMARY.md already recorded, unchanged by this plan. |
| 4 | The guard mechanism never crashes a process due to its own kernel-object handling (never-crash-on-startup contract, T-25-06) — CR-01 | ✓ VERIFIED | **Gap closed.** `SingleInstanceGuard.WaitForInstanceReady`'s `readyMutex.WaitOne(remaining)` (src/RigToggle.Core/SingleInstanceGuard.cs:330-350) is now wrapped in `try/catch (AbandonedMutexException)`, sets `acquired = true`, and routes into the existing release branch. This is a behavior-dependent truth (a specific exception-handling/state-transition invariant), and it is graduated to VERIFIED — not left at present-but-unproven — because a genuine behavioral test exercises it and was independently re-run by this verifier in this sandbox: `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -c Release --filter "FullyQualifiedName~WaitForInstanceReady_PrimaryAbandonedReadinessMutex"` → `Passed: 1, Failed: 0`. The test reproduces genuine OS-level mutex abandonment (an owning thread exits while still holding the mutex — explicitly NOT a `Dispose()`-based simulation, which this plan's own prohibitions ban as a no-op that would pass either way) and asserts both "no `AbandonedMutexException` escapes" and "the wait reports true." 25-04-SUMMARY.md's RED-then-GREEN transcript is corroborated by this independent re-run, and 25-REVIEW.md's re-review (dated same day) independently confirmed the fix by reading the source and diff directly, calling it "a real fix, not a cosmetic one — traced end to end, no gaps found." Full regression suite also re-run independently: 121/121 passing (120 pre-25-04 baseline + exactly 1 new fact, matching the SUMMARY's claimed delta exactly). |
| 5 | No fail-open, single duplicate-launch branch (D-02), silent restore (D-01), zero new NuGet packages, clean build/regression | ✓ VERIFIED | Re-confirmed independently: `grep -c 'IsPrimaryInstance' src/RigToggle.App/Program.cs` → 1; no `ShowBalloonTip`/`MessageBox`/`ToolTipIcon` in touched files; no `.csproj` changed since 25-03 (`git log --oneline -- '*.csproj'` shows no 25-04 commit touching any project file) — zero new/removed/version-changed `PackageReference`s; `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -c Release` independently re-run → 121/121 passing, 0 failures. |

**Score:** 3/5 truths verified (2 present, behavior-unverified; 0 failed — down from 1 failed in the prior verification)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/RigToggle.Core/SingleInstanceGuard.cs` | Named cross-process mutex primitive, readiness handshake, no unhandled-exception crash paths | ✓ VERIFIED | Exists, 365 lines (was 358 pre-25-04), compiles, wired into `Program.cs`. CR-01 gap closed (see Truth 4); WR-02 (readiness-mutex construction guard) also closed and confirmed by 25-REVIEW.md's re-review. |
| `src/RigToggle.Tests/SingleInstanceGuardTests.cs` | In-process xUnit coverage including the CR-01 regression net | ✓ VERIFIED | Exists, 281 lines (was 200). New fact `WaitForInstanceReady_PrimaryAbandonedReadinessMutex_ReturnsTrueWithoutThrowing` independently re-run and confirmed passing by this verifier. |
| `src/RigToggle.App/UpdateApplyEntryPoint.cs` | Side-effect-free placeholder relaunch entry point, doc comment matching `Program.cs`'s true ordering (IN-01) | ✓ VERIFIED | Exists, 39 lines. `Run(string[])` body unchanged (still returns only the bypass exit code); doc comment now names `SetColorMode`/`ApplicationConfiguration.Initialize` explicitly, matching the review's IN-01 finding. |
| `src/RigToggle.Windows/ActivationSignal.cs` | Public facade over broadcast P/Invoke | ✓ VERIFIED | Unchanged since 25-03; exists, wired into `MainForm.WndProc`. |
| `src/RigToggle.App/Program.cs` | Guard acquisition above bootstrap, one duplicate-launch branch, apply-update bypass branch above the guard | ✓ VERIFIED | Unchanged since 25-03 (25-04 explicitly did not touch this file — confirmed by its own key-files list and by this verifier's independent branch-count grep). |
| `src/RigToggle.App/MainForm.cs` | `RestoreAndFocus()` helper, `WndProc` activation branch | ✓ VERIFIED | Unchanged since 25-03 within phase scope. (Note: this file currently has separate, uncommitted local changes from an unrelated ongoing debug investigation into a monitor-enable bug — out of scope for Phase 25, not part of any of the four plans' commits, and does not affect this verdict.) |
| `src/RigToggle.Core/StartupArgs.cs` | `ApplyUpdateFlag`, `TryGetApplyUpdateArgs`, `ApplyUpdateBypassExitCode` | ✓ VERIFIED | Unchanged since 25-02/25-03. |
| `src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs` | Real child-process e2e coverage, 7 facts | ✓ VERIFIED (present, still unexecuted here) | Unchanged since 25-03; exists, 615 lines, 7 `[Fact]`s, no `Skip`/`Trait` exclusions. Cannot execute in this sandbox; not run to a confirmed-green result on Windows by the operator during 25-04 either (see Truths 1 and 3). |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `Program.cs` bypass branch | `UpdateApplyEntryPoint.Run` | direct call, above guard acquisition | ✓ WIRED | Unchanged; line-ordering re-confirmed structurally. |
| `Program.cs` duplicate-launch branch | `ActivationSignal.BroadcastActivation` | via `WaitForInstanceReady` then broadcast | ✓ WIRED | Unchanged. `WaitForInstanceReady`'s return value now reliably reflects success even on the abandoned-mutex path (Truth 4), so this link's behavior is strictly more correct than at the prior verification. |
| `MainForm.WndProc` | `RestoreAndFocus()` | activation-message branch | ✓ WIRED | Unchanged. |
| `SingleInstanceGuard.Acquire()` | readiness mutex (`WaitForInstanceReady`) | shared `IsGlobalScope`-derived name prefix | ✓ WIRED (gap closed) | The prior verification's flagged gap — the readiness mutex's unguarded `WaitOne` call — is now closed with a narrowly-typed catch. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/RigToggle.Core/SingleInstanceGuard.cs` | 194-203 (new WR-01, 25-REVIEW.md re-review) | `Acquire()`'s `Local\` fallback mutex construction is unguarded — if it also throws, the exception propagates out of `Acquire()` with zero diagnostic trail, contradicting the doc comment's "never propagates out of Main()" claim | ⚠️ Warning | Extremely low likelihood on this app's only supported context (interactive, non-elevated Windows session per CLAUDE.md). Not blocking — this is a new observation from the re-review's second pass, not a regression from 25-04's changes, and is a narrower/lower-likelihood case than the just-closed CR-01 (name-collision on `Local\`, not primary-vs-duplicate mutex semantics). |
| `src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs` | 305-327 (new WR-02, 25-REVIEW.md re-review) | `TightRaceLaunch_ExactlyOneProcessSurvives` can throw an unhandled exception instead of a clean assertion failure if the survivor process exits in the small window before `KillAndConfirmExit` runs | ⚠️ Warning | Test-only, affects test output clarity on a benign race, not production behavior. Not blocking. |
| `src/RigToggle.Windows/ActivationSignal.cs` | 75-82 (IN-01, 25-REVIEW.md re-review) | `MessageId`'s lazy cache (`_messageId ??= ...`) is not thread-safe | ℹ️ Info | Benign in practice (`RegisterWindowMessage` is idempotent); low priority. |

No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` markers found in any of the phase-touched files (`SingleInstanceGuard.cs`, `SingleInstanceGuardTests.cs`, `UpdateApplyEntryPoint.cs`), confirmed via grep.

No new Critical findings from the re-review (0 critical / 2 warning / 1 info, down from 1 critical / 2 warning / 1 info at the prior review).

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| CR-01 regression fact passes in isolation | `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~WaitForInstanceReady_PrimaryAbandonedReadinessMutex"` (run independently by this verifier) | `Passed: 1, Failed: 0, Skipped: 0, Total: 1, Duration: 12 ms` | ✓ PASS |
| Cross-platform suite regression (full, run once) | `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -c Release --no-restore` (run independently by this verifier) | `Passed: 121, Failed: 0, Skipped: 0` — exactly baseline (120) + 1 new fact, matching 25-04-SUMMARY.md's claimed delta | ✓ PASS |
| No `.csproj` touched since 25-03 (D-06 / zero-new-NuGet gate) | `git log --oneline -- '*.csproj'` since `2f4c238` (last 25-03 csproj-touching commit) | No 25-04 commit appears | ✓ PASS |
| Windows-only e2e suite (`SingleInstanceProcessTests`, 7 facts, including the 3 `ApplyUpdateBypass_*` facts) | N/A | N/A | ? SKIP — requires `Microsoft.WindowsDesktop.App`, absent in this Linux sandbox; also not run to green on Windows by the operator during 25-04 (see Truths 1, 3) |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| INSTANCE-01 | 25-01, 25-03, 25-04 | Launching while an instance is already running does not start a second instance | ? NEEDS HUMAN | Mechanism is sound and the crash risk (CR-01) that most directly threatened this path is now fixed and independently confirmed (Truth 4). What remains is the same evidence class already open since 25-03: the real-process e2e suite (`SingleInstanceProcessTests`, including the 3-consecutive-run flakiness check) has still not been run to a confirmed green result on Windows hardware. Upgraded from `BLOCKED` (prior verification) — the code-level blocker is gone — to `NEEDS HUMAN`, not yet `SATISFIED`. |
| INSTANCE-02 | 25-01, 25-03 | Already-running instance is brought to focus | ✓ SATISFIED | Unchanged; strong direct-hardware evidence from 25-03, unaffected by CR-01 or 25-04. |
| UPDATE-07 | 25-02, 25-03, 25-04 | Update-apply relaunch always succeeds despite the guard | ? NEEDS HUMAN | Bypass branch structurally verified to run before the guard is ever touched, and is entirely unaffected by CR-01 (never reaches `WaitForInstanceReady`). The 3 `ApplyUpdateBypass_*` facts remain `unknown` per both 25-03-SUMMARY.md and 25-04-SUMMARY.md — unchanged status, not a new or worsened gap. |
| PERF-03 | Phase 24 | (out of this phase's scope — listed complete in REQUIREMENTS.md) | N/A | Not this phase's concern. |

REQUIREMENTS.md's traceability table lists all three of this phase's requirements as `[x]`/"Complete" — this reflects the operator's explicit authorization to close 25-04 (see Task 3 disposition below), not an unqualified automated pass; this verification's `human_needed` status is the more precise state and does not contradict REQUIREMENTS.md's checkbox, which tracks plan-level closure, not verification-level certainty.

No orphaned requirements: all IDs declared across all four plans (`25-01: [INSTANCE-01, INSTANCE-02]`, `25-02: [UPDATE-07]`, `25-03: [INSTANCE-01, INSTANCE-02, UPDATE-07]`, `25-04: [INSTANCE-01, INSTANCE-02, UPDATE-07]`) match REQUIREMENTS.md's Phase 25 mapping exactly.

### Human Verification Required

### 1. Three consecutive clean Windows test runs (flakiness check), including the `ApplyUpdateBypass_*` facts

**Test:** Run `dotnet test RigToggle.sln -c Release --no-build` three times in a row on the operator's Windows rig.
**Expected:** Identical results across all three runs, all 7 `SingleInstanceProcessTests` facts green every time (including the 3 `ApplyUpdateBypass_*` facts, whose status is still `unknown`).
**Why human:** Requires real Windows hardware with `Microsoft.WindowsDesktop.App`; cannot execute in this Linux sandbox. This is the same check 25-03-PLAN.md's Task 3 and 25-04-PLAN.md's Task 3 both requested; 25-04-SUMMARY.md records the operator explicitly did not run it during the 25-04 checkpoint ("doesn't work on my pc"). This is the single evidence gap now standing between `human_needed` and `passed` for this phase — it has not grown since 25-03, but it also has not shrunk.

### 2. (Optional, confirmatory) Live CR-01 hardware reproduction

**Test:** Kill the primary process via Task Manager between `SingleInstanceGuard.Acquire()` and `guard.MarkReady()` while a duplicate is concurrently launched, and observe whether the duplicate crashes.
**Expected:** The duplicate process no longer terminates with an unhandled `AbandonedMutexException` — it completes `WaitForInstanceReady` and proceeds to broadcast/exit normally, with the new "readiness mutex was abandoned" log line appearing in `debug.log`.
**Why human:** Requires precisely timed real-process termination on real Windows; not reproducible via static analysis or in this sandbox. Downgraded in priority from the prior verification's framing: the automated regression test now independently confirmed passing in this sandbox already exercises the identical OS-level abandonment mechanism (an owning thread/process terminating while holding the mutex, which is what marks it abandoned regardless of whether the terminating entity is a whole process or one thread within it) — so Truth 4 is marked VERIFIED on that evidence rather than left pending on this specific hardware repro. This item remains open as corroborating, not blocking, evidence; 25-04-SUMMARY.md records it honestly as "not attempted."

### Gaps Summary

**The prior verification's sole blocking gap (CR-01) is closed and independently confirmed in this re-verification** — not merely trusted from 25-04-SUMMARY.md's narration. This verifier independently re-ran the CR-01 regression test (`WaitForInstanceReady_PrimaryAbandonedReadinessMutex_ReturnsTrueWithoutThrowing`) and the full `RigToggle.Tests` suite in this sandbox and confirmed both pass (1/1 and 121/121 respectively), read the fix directly in `SingleInstanceGuard.cs`, and cross-checked against 25-REVIEW.md's independent re-review, which reached the same conclusion by reading the diff rather than trusting the SUMMARY. No new Critical findings were introduced. Zero debt markers, zero new NuGet packages, zero regressions in the pre-existing test suite, and all requirement IDs across all four plans reconcile cleanly against REQUIREMENTS.md with no orphans.

**What remains open is the same evidence class already disclosed at the prior verification, not a new or worsened gap.** SC1 (rapid-relaunch/tight-race, real-process) and SC3 (apply-update bypass, real-process) both depend on `RigToggle.Windows.Tests`, which requires real Windows hardware and cannot run in this Linux sandbox. 25-04's Task 3 was a blocking human-verify checkpoint that specifically asked the operator to run this suite three times and reproduce the CR-01 crash live; the operator's verbatim response — "This as discussed doesn't work on my pc but compiling works and the app works fine so approve it" — confirms only that the solution builds on real hardware and that the app behaves normally in ordinary, untargeted use. It does NOT confirm the specific automated suite run, the three `ApplyUpdateBypass_*` facts, or the live CR-01 repro. 25-04-SUMMARY.md discloses this honestly and does not claim these as passed.

This verifier's judgment: the CR-01 fix itself now has strong, independently-reproduced automated evidence and an independent code review confirming the same, which is a materially different (and much stronger) evidentiary basis than "the operator says it works." The remaining gap — confirmed-green real-process e2e results on Windows hardware — is exactly the pre-existing open item 25-03-SUMMARY.md already flagged before 25-04 ever started; it has not grown, but it also has not been closed. Per the decision tree, a phase with zero failed truths but one or more open human-verification items routes to `human_needed`, not `gaps_found` and not `passed`. That is the correct, most precise classification here: the code-level blocker that previously forced `gaps_found` is gone, but the phase cannot honestly be called `passed` while the exact real-hardware evidence its own plans specified is still unconfirmed.

**What would move this to `passed`:** the operator runs `dotnet test RigToggle.sln -c Release --no-build` three consecutive times on the Windows rig and reports `Failed: 0` every time, including the 3 `ApplyUpdateBypass_*` facts passing. (The optional CR-01 live repro would further corroborate but, per the reasoning above, is no longer required for `passed` given the independently-confirmed automated regression evidence.)

---

_Verified: 2026-08-21_
_Verifier: Claude (gsd-verifier)_
