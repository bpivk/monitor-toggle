---
status: complete
phase: 25-single-instance-guard
source: [25-VERIFICATION.md]
started: 2026-08-21T19:33:45Z
updated: 2026-08-21T19:45:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Three consecutive clean Windows test runs (flakiness check), including the ApplyUpdateBypass_* facts
expected: Run `dotnet test RigToggle.sln -c Release --no-build` three times in a row on the operator's Windows rig. Identical results across all three runs, all 7 SingleInstanceProcessTests facts green every time (including the 3 ApplyUpdateBypass_* facts, whose status is still unknown). Requires real Windows hardware with Microsoft.WindowsDesktop.App — cannot execute in this Linux sandbox. This is the same check 25-03-PLAN.md's Task 3 and 25-04-PLAN.md's Task 3 both requested; 25-04-SUMMARY.md records the operator explicitly did not run it during the 25-04 checkpoint. This is the single evidence gap standing between human_needed and passed for this phase.
result: skipped
reason: "User explicitly opted to skip this verification step ('skip verify work') rather than run the Windows test suite three times."

### 2. (Optional, confirmatory) Live CR-01 hardware reproduction
expected: Kill the primary process via Task Manager between SingleInstanceGuard.Acquire() and guard.MarkReady() while a duplicate is concurrently launched. The duplicate process no longer terminates with an unhandled AbandonedMutexException — it completes WaitForInstanceReady and proceeds to broadcast/exit normally, with a new "readiness mutex was abandoned" log line appearing in debug.log. Not required for a passed verdict (the automated regression test already independently confirms the identical OS-level abandonment mechanism) — corroborating evidence only.
result: skipped
reason: "User explicitly opted to skip this verification step ('skip verify work'); already non-blocking per the verification's own reasoning."

## Summary

total: 2
passed: 0
issues: 0
pending: 0
skipped: 2
blocked: 0

## Gaps
