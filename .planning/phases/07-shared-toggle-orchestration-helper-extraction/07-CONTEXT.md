# Phase 7: Shared Toggle-Orchestration Helper Extraction - Context

**Gathered:** 2026-07-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Extract a single, reentrancy-safe orchestration entry point that every toggle trigger (today's GUI button; tray menu / hotkey / CLI in Phases 8-10) calls through, so a toggle already in progress can never be corrupted by a second concurrent request. Covers: the reentrancy guard design and its scope (which of `ToggleToRigMode`/`ToggleToNormalMode` it protects), the new orchestration entry point's shape and location, how a rejected ("already in progress") request is signaled back to a caller, and refactoring `MainForm.BtnToggle_Click` to go through it instead of calling `ToggleService` directly. Does not include tray/hotkey/CLI trigger implementation themselves (Phases 8-10) — this phase only builds the shared pipeline they will later plug into.

</domain>

<decisions>
## Implementation Decisions

### Reentrancy Guard Mechanism (CORE-06)
- **D-01:** Non-blocking busy-flag, not a blocking lock and not a queue. A second toggle request arriving while one is in flight is rejected immediately — it never waits for the first to finish and never gets silently serialized behind it. This is the literal reading of the roadmap's own success criteria ("safely rejects the second request", "exactly one toggle executing, never two overlapping") — a queue would make the second request eventually execute, which is explicitly not what's being asked for. Implementation mechanism (`Interlocked.CompareExchange` on an int, or an equivalent atomic bool) is Claude's discretion.
- **D-02:** One shared guard protects **both** `ToggleToRigMode` and `ToggleToNormalMode` — not independent per-direction flags. Both directions mutate the same underlying monitor/audio/app state, so a rig-mode toggle in flight must also reject a normal-mode request (and vice versa), not just a same-direction repeat.

### Orchestration Entry Point Shape
- **D-03:** A new thin orchestration type wraps the existing `ToggleService` (e.g. `ToggleOrchestrator`) rather than adding the guard directly inside `ToggleService`'s own methods. `ToggleService` stays exactly what it is today — a pure, already-unit-tested step sequencer with no concurrency concerns — and the new type becomes the single call site every trigger (button today, tray/hotkey/CLI later) is required to go through. `MainForm.BtnToggle_Click` is refactored to call the orchestrator instead of `_toggleService` directly.
- **D-04:** The orchestrator's public surface mirrors `ToggleService`'s existing shape as closely as possible (e.g. `ToggleToRigMode()`/`ToggleToNormalMode()`/`IsInRigMode()`/`IsSettingsConfigured()` pass-throughs plus the new guard) — minimizes the diff at the one existing call site and keeps future trigger code (Phases 8-10) trivial to wire up.

### Rejected-Request Signaling
- **D-05:** A busy rejection throws a dedicated exception (not a new `ToggleResult` variant) — consistent with the existing precedent in `ToggleService.ToggleToRigMode` where preflight conditions (unconfigured settings, missing companion app path) already throw `InvalidOperationException` outside the `ToggleResult` step-checklist contract. "Already in progress" is exactly this kind of preflight condition, not a mutation-step outcome. `MainForm.BtnToggle_Click`'s existing catch-and-`MessageBox` path already surfaces exception messages to the user, so success criterion 3 (existing GUI behavior unchanged) is satisfied without new UI code for today's single-trigger scenario.

### Claude's Discretion
- Exact type/method names for the orchestrator and the busy exception — left to planner.
- Whether the busy-flag is a field on the new orchestrator type or held elsewhere (e.g. a small dedicated guard class it composes) — implementation detail.
- Whether/how to add a rig-testable double-click regression test (e.g. a fake `ToggleService` with an artificial delay to prove only one execution proceeds) — left to planner/executor, but strongly encouraged given CORE-06's explicit "rapidly double-clicking" success criterion.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level
- `.planning/PROJECT.md` — core value, v1.1 milestone goal, Key Decisions table (stop-on-first-failure vs. isolate-and-continue asymmetry that any new orchestration layer must not disturb)
- `.planning/REQUIREMENTS.md` — CORE-06 (mapped to this phase)
- `.planning/ROADMAP.md` — Phase 7 section: goal, success criteria, and the explicit note that the reentrancy guard design (lock vs. busy-flag vs. queue) is this phase's own deliverable, not deferrable

### Prior phases (orchestration & reporting precedent)
- `.planning/milestones/v1.0-phases/05-orchestration-full-toggle-packaging/05-CONTEXT.md` — D-02/D-03/D-04/D-05 established the structured `ToggleResult` step-checklist contract and the deliberate stop-on-first-failure (rig mode) vs. isolate-and-continue (normal mode) asymmetry; this phase's guard must sit outside that contract (D-05 above), the same way existing preflight guards already do, and must not touch the asymmetry itself
- `.planning/phases/06-multi-monitor-data-model-controller-generalization/06-CONTEXT.md` — D-07 (`IsFullyConfigured` OR-semantics for disable/enable sets) and D-08 (silent settings migration) — context for `IsSettingsConfigured()`'s current contract, which the orchestrator's D-04 pass-through must preserve unchanged

### Existing code (this phase's actual surface area)
- `src/RigToggle.Core/ToggleService.cs` — `ToggleToRigMode()`/`ToggleToNormalMode()`/`IsInRigMode()`/`IsSettingsConfigured()` (the full existing public surface the new orchestrator wraps); existing preflight-exception pattern (`InvalidOperationException` for unconfigured settings / missing companion app) that D-05's busy-exception follows
- `src/RigToggle.App/MainForm.cs` — `BtnToggle_Click` (lines 54-155ish): the one existing call site that must be refactored to call the orchestrator instead of `_toggleService` directly, and whose confirmation-dialog + per-step-checklist behavior (success criterion 3) must remain unchanged after the refactor
- `src/RigToggle.App/Program.cs` — composition root; needs to wire up the new orchestrator type alongside/in place of the existing `ToggleService` registration
- `src/RigToggle.Tests/ToggleServiceTests.cs` — existing test suite and `FakeControllers.cs`/`InMemorySettingsStore`/`InMemorySnapshotStore` doubles this phase's new orchestrator tests should follow the same hand-written-fake convention as, rather than introducing a mocking framework

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ToggleService`'s existing preflight-exception pattern (`IsFullyConfigured` check → `InvalidOperationException`, missing companion app path → `InvalidOperationException`) in `ToggleToRigMode()` — directly reusable shape for the new busy-rejection exception (D-05).
- `RigToggle.Tests/Doubles/FakeControllers.cs`'s hand-written recording-fake convention (no mocking framework) — the orchestrator's reentrancy tests should use a fake `ToggleService`-shaped dependency (or a real `ToggleService` wired to fakes with an artificially slow controller) following this same style.

### Established Patterns
- Interface-per-concern + constructor injection via the composition root (`Program.cs`) — the orchestrator should be introduced the same way (its own type, injected into `MainForm`, no service-locator or static state).
- XML-doc rationale comments explaining *why*, not *what* — continue this convention for the reentrancy guard's design (why busy-flag not lock/queue, why one shared flag not per-direction), matching how Phase 5's stop-vs-continue asymmetry and Phase 6's enable-set-always-redisables asymmetry are both documented inline so they aren't "corrected" into something else later.

### Integration Points
- `MainForm.BtnToggle_Click` is the only current caller of `ToggleService.ToggleToRigMode()`/`ToggleToNormalMode()`/`IsSettingsConfigured()` — the sole refactor site for this phase.
- `Program.cs`'s composition root is where the new orchestrator type gets constructed and injected in place of (or wrapping) today's direct `ToggleService` registration into `MainForm`.
- Future trigger sources (tray menu, hotkey, CLI — Phases 8-10) will each become new callers of this same orchestration entry point; this phase's public API shape (D-04) is effectively locking the integration contract those phases will build against.

</code_context>

<specifics>
## Specific Ideas

No specific UI/UX ideas — this phase is purely an internal refactor/extraction with no user-visible surface beyond "the button still works exactly the same, and rapid double-clicking no longer risks corrupting state." Success is invisible under normal use and only observable via the rig double-click test and unit tests.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. Tray/hotkey/CLI trigger implementations (which will consume this phase's orchestration entry point) remain correctly scoped to Phases 8-10, not pulled forward here.

</deferred>

---

*Phase: 7-Shared-Toggle-Orchestration-Helper-Extraction*
*Context gathered: 2026-07-29*
