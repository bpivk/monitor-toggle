# Phase 7: Shared Toggle-Orchestration Helper Extraction - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-29
**Phase:** 7-shared-toggle-orchestration-helper-extraction
**Areas discussed:** Reentrancy guard mechanism, Orchestration entry point shape, Rejected-request signaling

**Mode:** `--auto` — Claude selected the recommended option for each question without interactive prompts (fully autonomous discuss-phase, per user's continued chain from Phase 6 closure).

---

## Reentrancy Guard Mechanism (CORE-06)

| Option | Description | Selected |
|--------|-------------|----------|
| Non-blocking busy-flag (immediate reject) | Second request rejected instantly if one is already in flight; never waits, never queues | ✓ |
| Blocking lock (`lock`/`Monitor`/`SemaphoreSlim.Wait()`) | Second request blocks until the first finishes, then proceeds | |
| Queue | Second (and further) requests are queued and executed in order once the first completes | |

**Selected:** Busy-flag, immediate reject.
**Notes:** [auto] Selected because the roadmap's own success criteria explicitly say "safely rejects the second request" and "exactly one toggle executing, never two overlapping" — both a blocking lock and a queue would eventually execute the second request, which contradicts "rejects." One shared flag protects both `ToggleToRigMode` and `ToggleToNormalMode` (not per-direction flags), since they mutate the same hardware state.

---

## Orchestration Entry Point Shape

| Option | Description | Selected |
|--------|-------------|----------|
| New wrapper type (e.g. `ToggleOrchestrator`) around existing `ToggleService` | `ToggleService` stays a pure step-sequencer; new type adds the guard and becomes the single trigger call site | ✓ |
| Add the guard directly inside `ToggleService`'s own methods | No new type; guard logic lives inline in the existing class | |

**Selected:** New wrapper type.
**Notes:** [auto] Keeps `ToggleService` exactly as-is (already unit-tested, no concurrency concerns) and gives Phases 8-10's future trigger sources (tray/hotkey/CLI) one obvious, already-guarded entry point to call instead of duplicating guard logic per trigger. The wrapper's public surface mirrors `ToggleService`'s existing methods to minimize the diff at `MainForm.BtnToggle_Click`, the one existing call site.

---

## Rejected-Request Signaling

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated exception (e.g. a busy/`InvalidOperationException`-style throw) | Matches existing preflight-guard precedent in `ToggleService` (unconfigured settings, missing companion app path) | ✓ |
| New `ToggleResult` variant (e.g. a "Rejected" step outcome) | Extends the existing per-step checklist contract to represent "never started" | |

**Selected:** Dedicated exception.
**Notes:** [auto] "Already in progress" is a preflight condition, not a mutation-step outcome — it belongs in the same family as `ToggleService`'s existing exception-based guards, not the `ToggleResult` step-checklist. `MainForm.BtnToggle_Click`'s existing catch-and-`MessageBox` handling already surfaces exception messages, so success criterion 3 (existing GUI behavior unchanged) holds without new UI code.

---

## Claude's Discretion

- Exact type/method/exception names for the orchestrator — left to planner.
- Whether the busy-flag lives directly on the orchestrator or in a small composed guard class — implementation detail.
- Exact shape of the rig double-click regression test (fake with artificial delay, etc.) — left to planner/executor.

## Deferred Ideas

None — discussion stayed within phase scope. Tray/hotkey/CLI trigger implementations remain correctly scoped to Phases 8-10.
