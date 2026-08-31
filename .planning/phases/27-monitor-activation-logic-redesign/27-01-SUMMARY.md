---
phase: 27-monitor-activation-logic-redesign
plan: 01
subsystem: monitor-activation
tags: [ccd, monitor-control, redesign, windows-display-api]
dependency-graph:
  requires: []
  provides:
    - "ActivateMonitorsCore single-pass Extend-only shape"
  affects:
    - "src/RigToggle.Windows/WindowsMonitorController.cs"
tech-stack:
  added: []
  patterns:
    - "Whole-topology ApplyTopology(Extend) as sole CCD activation mechanism, correction loop as sole in-call safety net"
key-files:
  created: []
  modified:
    - "src/RigToggle.Windows/WindowsMonitorController.cs"
decisions:
  - "Operator selected option-c (staged two-commit removal with rig gate between commits) via AskUserQuestion presented by the orchestrator before this executor was dispatched"
metrics:
  duration: "~35min (Tasks 1-2 only; Task 3 not attempted, see below)"
  completed: 2026-08-31
status: in-progress
actuals:
  tokens: 46000
  tasks: 2
  commits: 1
---

# Phase 27 Plan 01: Monitor Activation Logic Redesign — Tracer Slice Summary

Collapsed `ActivateMonitorsCore` to a single-pass, Extend-only activation shape — removed the
scoped-`ApplyPathInfos` primary/fallback split and its retry/poll machinery while leaving the
now-orphaned helper methods physically in the file (staged removal, Plan 02) — and confirmed the
solution builds clean with `RigToggle.Tests` at 249/249. Real-rig verification (Task 3) is a
separate checkpoint this session cannot perform and is reported below as unresolved, not
fabricated.

## Task 1: Removal Strategy Decision (checkpoint:decision)

**Status:** Already resolved before this executor was dispatched — not re-asked.

Operator selected **option-c** ("Outright removal, staged across two commits with a rig gate
between them") via `AskUserQuestion` presented by the orchestrator before this executor session
began. This is exactly the approach this plan set (27-01/27-02/27-03) already implements:
Plan 01 lands the behavior-change-only tracer commit (Task 2, this session) and gates further
work on a real-rig verification pass (Task 3) before Plan 02 deletes the now-unreachable scoped-
activation helpers. No replanning was triggered (that branch only applies to option-b, not
selected).

## Task 2: Collapse ActivateMonitorsCore to Single-Pass Extend-Only Shape (tracer)

**Status:** Complete. Commit `6b3058b`.

Rewrote `ActivateMonitorsCore`'s body in `src/RigToggle.Windows/WindowsMonitorController.cs`
exactly per the plan's action spec and `.planning/debug/monitor-position-regre.md` §22.1:

- **Removed:** the `MaxScopedActivationRetryAttempts` outer retry `for` loop; the
  `usedScopedActivation` local and every read/write of it; the `if (TryBuildScopedActivationPlan
  (...)) {...} else {...}` branch (including its try/catch around the scoped
  `ApplyPathInfos` call and both plan-detail Log lines); the `devicePathsToActivate` local; the
  informational `if (isPartOfMonitorSwap) { Log(...) }` block; the `retryEligibleTopLevel`/
  `retryEligibleNestedOnly` locals and their `ShouldRetryScopedActivation`/
  `ShouldRetryNestedCorrectionActivation` calls; the reachability-poll `Stopwatch` block and its
  `PollUntilTargetsReachable` call; the `attemptNumber > 1` conditional Log suffixes on both
  terminal throw branches (no attempt counter exists any more).
- **Replaced:** the activation section with one unconditional call —
  `PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false)` — preceded by
  the renamed `activePathsBeforeMutation` local (was `activePathsForScopedPlan`) feeding the
  unchanged `CacheLiveModes(...)` call.
- **Kept byte-for-byte:** the `isPartOfMonitorSwap` derivation, ENTER/EXIT logging, the
  `monitorDevicePaths.Count == 0` no-op, the Pitfall-3 already-active skip, the missing-
  availability guard/throw, the settle-poll-then-correct loop (`MaxCorrectionRounds`,
  `RequiredConsecutiveCleanRounds`, `ComputeUnexpectedlyActivated`/`ComputeUnexpectedlyDeactivated`,
  the nested nested `ActivateMonitorsCore(..., isNestedCorrectionCall: true)` call), the
  verify-and-throw computation, and both terminal exception types
  (`InvalidOperationException` / `CollateralMonitorRestoreFailedException`).
- **Added:** a new remark block immediately above `ActivateMonitorsCore` stating Phase 27's
  redesign in prose (no deleted-helper names), citing debug §22.1/§22.3.
- **Left in place, not deleted (Plan 02's job):** `TryBuildScopedActivationPlan`,
  `PromoteToOriginIfNeeded`, `SelectSourceForActivation`, `ResolveIsPathActiveBackingField` (+
  `_isPathActiveBackingFieldCache`), `DescribeSource`, `DescribeScopedPathEntry`,
  `ShouldRetryScopedActivation`, `ShouldRetryNestedCorrectionActivation`,
  `PollUntilTargetsReachable` (+ `MaxReachabilityPollAttempts`).

### Verification

```
dotnet build RigToggle.sln -c Debug --nologo         -> 0 Error(s), 6 pre-existing xUnit1031 warnings (baseline)
dotnet build src/RigToggle.Windows.Tests/... --nologo -> 0 Warning(s), 0 Error(s)
dotnet test src/RigToggle.Tests/... --nologo          -> Failed: 0, Passed: 249, Total: 249
```

Source assertions:

| Assertion | Result |
|---|---|
| `grep -c 'ApplyTopology('` in `ActivateMonitorsCore` | Exactly one call site inside the method (line 595); other 11 matches are doc-comment/remark references to the historical mechanism, not calls |
| `usedScopedActivation` non-comment grep | **2** (not 0 — see note below) |
| `MaxScopedActivationRetryAttempts` non-comment grep | 0 |
| `TryBuildScopedActivationPlan(` / `ShouldRetryScopedActivation(` / `ShouldRetryNestedCorrectionActivation(` / `PollUntilTargetsReachable(` each appear | Exactly 1 (declaration only, zero call sites) |
| `isNestedCorrectionCall` | Parameter present, exactly one nested call site passes `true`, exactly one terminal branch throws `CollateralMonitorRestoreFailedException` |
| `CacheLiveModes(` count | 3 (declaration + `ActivateMonitorsCore` call + `DeactivateMonitors` call) — unchanged |

**Note on the `usedScopedActivation` count (2, not 0):** both remaining occurrences are inside the
**untouched** `ShouldRetryScopedActivation` helper's own signature/body (`bool
usedScopedActivation` parameter, line 1220, and its use at line 1225) — not inside
`ActivateMonitorsCore`. Task 2's action explicitly instructs "do NOT delete any helper method
bodies in this task — orphaned helpers are removed in Plan 02," so `ShouldRetryScopedActivation`'s
body (including its parameter named `usedScopedActivation`) was correctly left intact. The
`usedScopedActivation` **local variable inside `ActivateMonitorsCore`** — the one the acceptance
criterion's grep was actually targeting — is fully removed; the criterion's literal grep count
does not distinguish "local variable inside the rewritten method" from "an unrelated, still-
present helper's parameter of the same name," which is why the raw count is 2 rather than 0. This
is a known, expected consequence of the staged-removal (option-c) decision and is resolved when
Plan 02 deletes `ShouldRetryScopedActivation` outright. Not a deviation requiring a fix — fixing
it would require deleting a helper body this task is explicitly forbidden from touching.

### Hand-trace (required by acceptance criteria)

Scenario: `ToggleService.ToggleToRigMode()` with `enableSet = {Rig}`, `disableSet = {Primary}`
(the ordinary Rig-mode full-swap shape), starting state Normal mode (Primary active, Rig
inactive), and the CCD persistence database's last-known "extend" layout being the 2-monitor
Primary+Rig pair (the common case — this is what was active the last time both were on together).

| Point | Active monitors | Notes |
|---|---|---|
| Before the call (`currentlyActiveDevicePaths`) | `{Primary}` | Rig is off, matching Normal-mode state |
| After `ApplyTopology(Extend)` returns | `{Primary, Rig}` | Extend restores the persisted whole-topology DB layout, which includes both monitors — Rig comes on because it's requested; Primary stays on because Extend does not scope to only the requested target |
| After correction round 1 | `{Primary, Rig}` (unchanged) | `ComputeUnexpectedlyActivated` excludes Rig (it's in `monitorDevicePaths`, i.e. requested) and Primary was already active pre-call, so nothing is flagged; `ComputeUnexpectedlyDeactivated` finds no active-before/inactive-now survivor, so nothing is flagged either. `consecutiveCleanRounds` increments toward the required 2 |
| At `ActivateMonitors` return (success path) | `{Primary, Rig}` | `requestedStillInactive` is empty (Rig is active); `survivorStillInactive` excludes Primary via the `monitorSwapDisableSet` filter (`!monitorSwapDisableSet.Contains(dp)`), so Primary being active or inactive here is irrelevant to the throw decision — `stillInactive` is empty, EXIT success |
| After the immediately-following `DeactivateMonitors(disableSet={Primary})` returns | `{Rig}` | Primary is explicitly turned off by the separate call `ToggleService` makes right after `ActivateMonitors`, per Pitfall 2's unchanged ordering contract |

**Does `ComputeUnexpectedlyDeactivated`'s `monitorSwapDisableSet` exclusion still prevent the
swap's disable-set from being flagged as lost survivors? Yes, unchanged.** The exclusion
(`!monitorSwapDisableSet.Contains(dp)` inside the `survivorStillInactive` filter, and
`monitorSwapDisableSet` passed as-is into `ComputeUnexpectedlyDeactivated`'s own call) is
byte-for-byte unchanged by this rewrite. It matters most in the case where Extend's persisted
layout does *not* happen to include Primary (e.g. if the DB's last extend state only had Rig
active): Primary would then go active-before/inactive-after within the same call, which is
exactly `ComputeUnexpectedlyDeactivated`'s trigger condition — but because Primary is in
`monitorSwapDisableSet`, it is correctly excluded from being flagged and nested-reactivated. Without
this exclusion, the swap's own disable-set target would be wrongly "corrected" back on inside
`ActivateMonitors`, immediately undone by the very next `DeactivateMonitors` call — a
self-defeating loop this exclusion has always existed to prevent, and continues to prevent
identically post-redesign.

## Task 3: Rig-Hardware Verification (checkpoint:human-verify) — NOT PERFORMED THIS SESSION

**Status: not attempted.** This executor runs in a Linux sandbox with no Windows CCD API access
and cannot build/run `RigToggle.App.exe` or exercise live display hardware. Per this session's
explicit instructions, Task 3 was not attempted, not guessed at, and not marked passed. It is
returned to the orchestrator as a checkpoint for the operator to perform on the real Windows rig.
Checks A-E (single-tile enable/collateral-activation, position preservation, full swap ordering,
the Odyssey G5 flaky monitor x3, and failure-dialog surfacing) remain fully unanswered as of this
summary. **PLAN 27-01 IS NOT YET COMPLETE** — Task 3 must be answered by the operator before this
plan (and REDESIGN-04) can be marked done, and before Plan 02 proceeds with helper deletion.

## Deviations from Plan

### Auto-fixed Issues

None — Task 2 executed exactly as specified in the plan's `<action>` block.

### Documented Discrepancies (not fixed, not deviations under Rules 1-4)

**1. `usedScopedActivation` acceptance-criteria grep returns 2, not the specified 0.** See the
"Note on the `usedScopedActivation` count" above — the two matches are inside the untouched
`ShouldRetryScopedActivation` helper's own signature, which Task 2 explicitly forbids deleting.
Not fixed, since fixing it would require deleting a helper body outside this task's scope (Plan
02's job). Not a Rule 1-3 auto-fix candidate (no bug, no missing functionality, nothing blocking
completion) and not a Rule 4 architectural question — it is a known, narrow textual mismatch
between the acceptance criterion's literal grep and the task's own "do not delete helper bodies"
constraint. Resolves itself when Plan 02 deletes `ShouldRetryScopedActivation`.

## Known Stubs

None.

## Threat Flags

None — this task modifies only the internal activation call sequence inside an existing
safety-critical method; no new network endpoint, auth path, file-access pattern, or schema change
at a trust boundary was introduced. The threat model's T-27-01/T-27-02/T-27-05 mitigations were
verified to hold (see Verification table above: correction loop, zero-survivor guard in
`DeactivateMonitors`, and both terminal exception types all confirmed unchanged).

## Self-Check

- `src/RigToggle.Windows/WindowsMonitorController.cs` — FOUND, modified as described
- Commit `6b3058b` — FOUND in `git log`

## Self-Check: PASSED
