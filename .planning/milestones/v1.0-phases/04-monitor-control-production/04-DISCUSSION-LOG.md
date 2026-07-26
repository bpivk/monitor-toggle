# Phase 4: Monitor Control (Production) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-24
**Phase:** 4-Monitor-Control-Production
**Areas discussed:** Confirmation dialog UX, remembered-consent scope, verification strictness, failure-path handling

---

## Confirmation Dialog Frequency

| Option | Description | Selected |
|--------|-------------|----------|
| Always confirm, every toggle | Simple Yes/No MessageBox naming the monitor, shown every time | |
| Confirm once, remember choice | Dialog on first use with a "don't ask again" checkbox that persists | ✓ |

**User's choice:** Confirm once, remember choice
**Notes:** User preferred fewer repeat clicks over the belt-and-suspenders "always confirm" default.

---

## Remembered-Consent Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Persisted setting, resets on monitor change | "Don't ask again" clears automatically if the configured monitor device path changes | ✓ |
| Persisted setting, never resets | Stays checked regardless of later monitor changes | |

**User's choice:** Persisted setting, resets on monitor change
**Notes:** Prevents a stale consent silently applying to a newly-configured, different display.

---

## Verification Strictness

| Option | Description | Selected |
|--------|-------------|----------|
| Verify and throw on mismatch | Re-query WindowsDisplayAPI after Disable/Restore, throw if actual topology doesn't match expected | ✓ |
| Trust the API call result | Treat a non-throwing ApplyPathInfos call as success, no re-query | |

**User's choice:** Verify and throw on mismatch
**Notes:** Directly motivated by the spike's Finding 2 (Screen.AllScreens staleness) and Finding 3 (primary-removal validation failures) — trusting the return value alone risks exactly the "looks successful but isn't" failure mode this project exists to avoid.

---

## Failure Path

| Option | Description | Selected |
|--------|-------------|----------|
| Bubble up, same as audio | Exception surfaces through existing MainForm exception handling, no auto-rollback | ✓ |
| Attempt automatic rollback first | Re-apply pre-toggle topology automatically before reporting the error | |

**User's choice:** Bubble up, same as audio
**Notes:** Matches Phase 3's precedent; automatic rollback right after a failed mutation risks compounding the problem with a second risky topology call. Comprehensive failure recovery stays Phase 5 (CORE-04) scope.

---

## Claude's Discretion

- Exact mechanism for repositioning the remaining display to (0,0) before removing the primary monitor's path (lower-level mode reconstruction vs raw P/Invoke) — implementation risk for research/planner, not a user preference.
- `MonitorState`'s enriched snapshot shape for exact restore (position/primary/orientation) — left to planner, following the spike's proven "keep full original array, re-apply wholesale" pattern.

## Deferred Ideas

None — discussion stayed within phase scope. Automatic rollback and comprehensive failure reporting remain correctly scoped to Phase 5.
