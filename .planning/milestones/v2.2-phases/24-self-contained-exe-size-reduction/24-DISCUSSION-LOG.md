# Phase 24: Self-Contained Exe Size Reduction - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-18
**Phase:** 24-self-contained-exe-size-reduction
**Areas discussed:** Lever scope, Startup-latency tradeoff, UseSystemResourceKeys, Minimum bar for a lever

---

## Lever scope

| Option | Description | Selected |
|--------|-------------|----------|
| Include swaps if found | Same standard as v2.0: a zero-behavior-change package-reference swap counts as a safe lever if it saves real space, matching established precedent | ✓ |
| Property flags only | Keep this phase to pure MSBuild property changes; log any found package-swap opportunity as a backlog note instead of acting on it now | |
| Let me explain | Freeform | |

**User's choice:** Include swaps if found (Recommended)
**Notes:** None.

---

## Startup-latency tradeoff

This area arose as freeform input via "Other" when a follow-up gray-area set was offered (not from a pre-defined option list).

**User's input (verbatim):** "I don't know if we can save some space but if possible we could also compress the app and make it load a bit slower"

**Claude's clarification:** Noted that `EnableCompressionInSingleFile` is already on (the largest v2.0 lever, binary on/off, no adjustable "level"), and confirmed the user is relaxing the implicit "all levers must be startup-neutral" assumption, not asking for a compression level that doesn't exist.

**User's follow-up (verbatim):** "yes, if it meaningfully shrinks it further and it does not take a lot of extra time"

**Resolution:** Recorded as D-02 in CONTEXT.md — a real startup-latency cost is acceptable if the size win is meaningful and the added latency stays small. Not an open-ended tradeoff.

---

## UseSystemResourceKeys

| Option | Description | Selected |
|--------|-------------|----------|
| Apply it | Take the size win — debug.log is off-by-default and this milestone prioritizes size | |
| Skip it | Keep exception messages fully readable in debug.log; not worth the diagnostic downside for a small saving | ✓ |

**User's choice:** Skip it
**Notes:** None.

---

## Minimum bar for a lever

| Option | Description | Selected |
|--------|-------------|----------|
| Apply every safe lever found | No lever is too small — stack every safe, zero/low-cost win the research/publish-audit finds | ✓ |
| Only meaningful wins | Skip levers that save a trivial amount if they add any real complexity or risk | |

**User's choice:** Apply every safe lever found (Recommended)
**Notes:** None.

---

## Claude's Discretion

- Exact `dotnet publish` measurement methodology and reporting format — follow the established v2.0/Phase 18 pattern.
- Judgment on whether a given lever counts as "safe" — same standard as CLAUDE.md's existing "What NOT to Use" exclusions (no IL trimming, no Native AOT, no `PublishReadyToRun`).

## Deferred Ideas

None — discussion stayed within phase scope.
