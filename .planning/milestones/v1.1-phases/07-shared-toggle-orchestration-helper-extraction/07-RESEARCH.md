# Phase 7: Shared Toggle-Orchestration Helper Extraction - Research

**Researched:** 2026-07-29
**Domain:** In-process concurrency guard (C#/.NET single-flight pattern) wrapped around an existing pure-sequencer service in a WinForms desktop app
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Reentrancy Guard Mechanism (CORE-06)**
- **D-01:** Non-blocking busy-flag, not a blocking lock and not a queue. A second toggle request arriving while one is in flight is rejected immediately — it never waits for the first to finish and never gets silently serialized behind it. This is the literal reading of the roadmap's own success criteria ("safely rejects the second request", "exactly one toggle executing, never two overlapping") — a queue would make the second request eventually execute, which is explicitly not what's being asked for. Implementation mechanism (`Interlocked.CompareExchange` on an int, or an equivalent atomic bool) is Claude's discretion.
- **D-02:** One shared guard protects **both** `ToggleToRigMode` and `ToggleToNormalMode` — not independent per-direction flags. Both directions mutate the same underlying monitor/audio/app state, so a rig-mode toggle in flight must also reject a normal-mode request (and vice versa), not just a same-direction repeat.

**Orchestration Entry Point Shape**
- **D-03:** A new thin orchestration type wraps the existing `ToggleService` (e.g. `ToggleOrchestrator`) rather than adding the guard directly inside `ToggleService`'s own methods. `ToggleService` stays exactly what it is today — a pure, already-unit-tested step sequencer with no concurrency concerns — and the new type becomes the single call site every trigger (button today, tray/hotkey/CLI later) is required to go through. `MainForm.BtnToggle_Click` is refactored to call the orchestrator instead of `_toggleService` directly.
- **D-04:** The orchestrator's public surface mirrors `ToggleService`'s existing shape as closely as possible (e.g. `ToggleToRigMode()`/`ToggleToNormalMode()`/`IsInRigMode()`/`IsSettingsConfigured()` pass-throughs plus the new guard) — minimizes the diff at the one existing call site and keeps future trigger code (Phases 8-10) trivial to wire up.

**Rejected-Request Signaling**
- **D-05:** A busy rejection throws a dedicated exception (not a new `ToggleResult` variant) — consistent with the existing precedent in `ToggleService.ToggleToRigMode` where preflight conditions (unconfigured settings, missing companion app path) already throw `InvalidOperationException` outside the `ToggleResult` step-checklist contract. "Already in progress" is exactly this kind of preflight condition, not a mutation-step outcome. `MainForm.BtnToggle_Click`'s existing catch-and-`MessageBox` path already surfaces exception messages to the user, so success criterion 3 (existing GUI behavior unchanged) is satisfied without new UI code for today's single-trigger scenario.

### Claude's Discretion
- Exact type/method names for the orchestrator and the busy exception — left to planner.
- Whether the busy-flag is a field on the new orchestrator type or held elsewhere (e.g. a small dedicated guard class it composes) — implementation detail.
- Whether/how to add a rig-testable double-click regression test (e.g. a fake `ToggleService` with an artificial delay to prove only one execution proceeds) — left to planner/executor, but strongly encouraged given CORE-06's explicit "rapidly double-clicking" success criterion.

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope. Tray/hotkey/CLI trigger implementations (which will consume this phase's orchestration entry point) remain correctly scoped to Phases 8-10, not pulled forward here.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|--------------------|
| CORE-06 | If a toggle is triggered while another toggle is already in progress, the app safely rejects the second request rather than risking corrupted state | Standard Stack (`Interlocked.CompareExchange`), Architecture Patterns (Pattern 2: non-blocking single-flight guard; Pattern 3: dedicated rejection exception), Common Pitfalls (blocking-lock regression; flaky reentrancy tests; unreleased-flag-on-exception) together specify the exact mechanism, its correct implementation shape, and how to test it deterministically |
</phase_requirements>

## Summary

This phase is a small, self-contained concurrency problem, not a library-selection problem: the entire mechanism needed (`Interlocked.CompareExchange` on an `int` field, guarded with `try`/`finally`) is a BCL primitive with zero new dependencies. The real research value here is in three places CONTEXT.md flagged: (1) confirming when genuine thread concurrency can actually arise in this app (today: never — WinForms serializes all UI-thread event dispatch; the real target is future-proofing against Phase 10's CLI/IPC listener thread), (2) making sure the guard is implemented as reject-not-block (a plain `lock`/`Monitor.Enter` would silently violate D-01 by turning into a queue), and (3) designing a deterministic (non-timing-based) test for the "rapid double-click" scenario using a controllable blocking fake rather than `Thread.Sleep`.

`ToggleService` is sealed, has no interface, and is directly constructed in `Program.cs`'s composition root — the new `ToggleOrchestrator` type wraps it as a plain composed dependency (not via a new abstraction/interface), matching D-03's "thin wrapper" framing and minimizing the diff. The busy-rejection exception should be implemented as a subclass of `InvalidOperationException` (not a standalone `Exception`) — this is what makes D-05's claim true for free: `MainForm.BtnToggle_Click`'s existing `catch (Exception ex)` block already surfaces `ex.Message` via `MessageBox.Show`, so no MainForm changes are needed to satisfy success criterion 3 beyond swapping the call target from `_toggleService` to `_orchestrator`.

**Primary recommendation:** Implement `ToggleOrchestrator` as a sealed class composing `ToggleService`, guarding `ToggleToRigMode()`/`ToggleToNormalMode()` with a shared `private int _busy` field via `Interlocked.CompareExchange(ref _busy, 1, 0)` inside a `try`/`finally` that always resets it to `0`; throw a new `ToggleInProgressException : InvalidOperationException` on rejection; pass through `IsInRigMode()`/`IsSettingsConfigured()` unguarded; refactor `MainForm` to depend on `ToggleOrchestrator` instead of `ToggleService`; wire the wrapping in `Program.cs`'s composition root.

## Architectural Responsibility Map

This app has no web/server tiers; the relevant "tiers" are its own layered architecture (established in prior phases' `ARCHITECTURE.md`/`02-RESEARCH.md` Anti-Pattern 2: interface-per-concern + composition-root injection).

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Reentrancy/single-flight guard | New orchestration layer (`ToggleOrchestrator`) | — | D-03: must not live inside `ToggleService` (keeps it a pure, already-tested step sequencer); must not live in `MainForm` (every future trigger — tray/hotkey/CLI — needs it too, not just the button) |
| Toggle direction dispatch (`IsInRigMode() ? ToNormal : ToRig`) | Trigger/UI layer (`MainForm` today; tray/hotkey/CLI in Phases 8-10) | — | Unchanged by this phase — D-04 keeps `ToggleToRigMode`/`ToggleToNormalMode` as two separate orchestrator methods, so the caller still decides direction, exactly as `MainForm` does today |
| Toggle step sequencing (snapshot → mutate → report) | Domain/service layer (`ToggleService`) | — | Untouched by this phase (D-03) — remains the single source of truth for CORE-03/CORE-04 step semantics |
| Rejected-request signaling | New orchestration layer (`ToggleOrchestrator`, via thrown exception) | Trigger/UI layer (catches/displays it) | D-05: exception-based, outside the `ToggleResult` contract, mirroring `ToggleService`'s existing preflight-guard precedent |
| Windows API mutation (CCD, audio COM, Win32 window) | Adapter/infrastructure layer (`RigToggle.Windows.*`) | — | Untouched by this phase |

## Standard Stack

### Core
No new libraries. This phase uses only BCL primitives already available via `net10.0`/`net10.0-windows` (see `RigToggle.Core.csproj`/`RigToggle.App.csproj` — both already target `net10.0`(-windows) with `Nullable enable`).

| API | Namespace | Purpose | Why Standard |
|-----|-----------|---------|---------------|
| `Interlocked.CompareExchange(ref int, int, int)` | `System.Threading` | Atomic test-and-set for the busy flag | The documented, canonical .NET idiom for a non-blocking single-flight guard — atomically compares and conditionally swaps in one indivisible operation, so there is no window between "check" and "set" for a second caller to race into `[CITED: learn.microsoft.com/dotnet/api/system.threading.interlocked.compareexchange]` |

### Supporting
None needed. `try`/`finally` (language construct, not a library) guarantees the flag resets even when `ToggleService` throws (including its own existing preflight `InvalidOperationException`s).

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `Interlocked.CompareExchange` on `int` | `SemaphoreSlim(1,1)` with `.Wait(0)` (non-blocking check) + `.Release()` in `finally` | Functionally equivalent (both are non-blocking single-flight guards); `SemaphoreSlim` is heavier (allocates a kernel-backed wait handle lazily) and is really designed for *waiting* scenarios, not simple flag flips — `Interlocked` is the leaner, more idiomatic choice for exactly this "reject if busy" shape. Only reach for `SemaphoreSlim` if a future requirement needs actual queuing/timeout behavior (explicitly out of scope per D-01). |
| `Interlocked.CompareExchange` on `int` | `Monitor.TryEnter(gate, timeout: 0)` (non-blocking `lock`) | Also non-blocking and correct if used with `TryEnter`/`Exit` in `try`/`finally`. Slightly more verbose than a raw `int` flag and easy to mis-copy into a blocking `lock (gate) { ... }` by a future editor who doesn't realize the whole point was non-blocking — `Interlocked` makes the "must not block" property visible in the code shape itself, which is a safer default for a decision this consequential (D-01 calls this out explicitly). |
| A plain `bool _busy` field (no `Interlocked`) | — | **Not viable even for same-thread-only correctness.** A bare `if (!_busy) { _busy = true; ... }` is a check-then-act race even under a single OS thread if the two operations are ever separated by an `await` or a nested message pump (see Common Pitfalls). `Interlocked.CompareExchange` collapses check+set into one atomic operation, so it is correct regardless of whether future callers are same-thread or cross-thread — always prefer it over a manual bool even where testing suggests "it can't race today." |

**Installation:** None — `System.Threading.Interlocked` is part of the BCL, already implicitly available in every project in this solution.

## Package Legitimacy Audit

**Not applicable.** This phase introduces zero new external packages (no NuGet, no npm, no pip). All mechanisms are BCL (`System.Threading`) or the project's own hand-written test doubles. Skip the Package Legitimacy Gate protocol entirely for this phase's plan.

## Architecture Patterns

### System Architecture Diagram

```
                    ┌─────────────────────────────────────────┐
                    │         Trigger / UI layer               │
                    │  MainForm.BtnToggle_Click  (this phase)   │
                    │  [Phase 8: tray menu click]                │
                    │  [Phase 9: WM_HOTKEY handler]              │
                    │  [Phase 10: CLI/IPC listener — NEW THREAD] │
                    └───────────────┬───────────────────────────┘
                                    │ calls
                                    ▼
                    ┌─────────────────────────────────────────┐
                    │      ToggleOrchestrator (NEW, this phase) │
                    │  ┌────────────────────────────────────┐  │
                    │  │ Interlocked.CompareExchange(_busy)  │  │
                    │  │   0 → 1 : proceed                   │  │
                    │  │   already 1 : throw                 │  │
                    │  │   ToggleInProgressException          │  │
                    │  └────────────────────────────────────┘  │
                    │        try { call ToggleService }         │
                    │        finally { _busy = 0 }              │
                    │  IsInRigMode()/IsSettingsConfigured()      │
                    │    → pass through, unguarded               │
                    └───────────────┬───────────────────────────┘
                                    │ delegates to (unchanged)
                                    ▼
                    ┌─────────────────────────────────────────┐
                    │      ToggleService (UNCHANGED, Phase 5/6) │
                    │  ToggleToRigMode() / ToggleToNormalMode()  │
                    │  snapshot → mutate → ToggleResult          │
                    └───────────────┬───────────────────────────┘
                                    ▼
                    ┌─────────────────────────────────────────┐
                    │  IMonitorController / IAudioController /   │
                    │  IAppController (Windows adapters,          │
                    │  UNCHANGED)                                 │
                    └─────────────────────────────────────────┘
```

Reading the diagram: every trigger source funnels through the same `ToggleOrchestrator` instance (one instance for the app's lifetime, constructed once in `Program.cs`'s composition root — see Pattern 1). A second call arriving while `_busy == 1` never reaches `ToggleService` at all; it is rejected at the top of the orchestrator with zero side effects. `ToggleService` itself is never touched or made aware that a guard exists above it.

### Recommended Project Structure
```
src/RigToggle.Core/
├── ToggleService.cs              # UNCHANGED
├── ToggleOrchestrator.cs         # NEW — wraps ToggleService, owns the busy-guard
├── ToggleInProgressException.cs  # NEW — dedicated rejection exception (D-05)
└── Models/                       # UNCHANGED (ToggleResult, ToggleStepResult, etc.)

src/RigToggle.App/
├── MainForm.cs                   # MODIFIED — depends on ToggleOrchestrator, not ToggleService
└── Program.cs                    # MODIFIED — constructs ToggleOrchestrator, injects it into MainForm

src/RigToggle.Tests/
├── ToggleServiceTests.cs         # UNCHANGED
├── ToggleOrchestratorTests.cs    # NEW — guard behavior, pass-through behavior, deterministic double-invoke test
└── Doubles/
    ├── FakeControllers.cs        # UNCHANGED (or: add an optional blocking hook — see Pattern 3)
    └── (optionally) BlockingMonitorController.cs  # NEW, test-only — controllable block point for the reentrancy test
```

### Pattern 1: Composition-root wrapping (matches existing Anti-Pattern 2 convention)
**What:** `Program.cs` constructs the real `ToggleService` exactly as it does today, then wraps it in a `ToggleOrchestrator` and passes *that* to `MainForm` instead.
**When to use:** This is the only wiring change needed — no new interface, no service locator.
**Example:**
```csharp
// Source: pattern matches existing src/RigToggle.App/Program.cs composition root
var toggleService = new ToggleService(
    settingsStore, snapshotStore, monitorController, audioController, appController);

var toggleOrchestrator = new ToggleOrchestrator(toggleService);

var mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory);
```
`MainForm`'s constructor parameter type changes from `ToggleService` to `ToggleOrchestrator`; the field rename (`_toggleService` → `_orchestrator` or similar) plus the four call sites (`IsInRigMode()`, `IsSettingsConfigured()`, `ToggleToRigMode()`, `ToggleToNormalMode()` — MainForm.cs lines 49, 60, 62, 66, 124) are the entire `MainForm` diff.

### Pattern 2: Non-blocking single-flight guard (D-01's core mechanism)
**What:** Guard the two mutating methods with `Interlocked.CompareExchange`, never with a blocking `lock`.
**When to use:** Any time "reject the second caller immediately" (not "make the second caller wait") is the required semantic — exactly D-01's wording ("never waits... never gets silently serialized").
**Example:**
```csharp
// Source: standard .NET idiom, confirmed against
// https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked.compareexchange
public sealed class ToggleOrchestrator
{
    private readonly ToggleService _toggleService;

    // 0 = idle, 1 = a toggle is in flight. Interlocked.CompareExchange makes the
    // "is anyone in flight?" check and "claim it" set a single atomic operation —
    // this is what D-01 means by "non-blocking busy-flag": a second caller is
    // rejected immediately, it never waits and is never queued (unlike `lock`,
    // which would block the second caller until the first finishes — silently
    // turning D-01's explicitly-rejected "queue" behavior back on).
    private int _busy;

    public ToggleOrchestrator(ToggleService toggleService)
    {
        _toggleService = toggleService ?? throw new ArgumentNullException(nameof(toggleService));
    }

    public ToggleResult ToggleToRigMode() => RunGuarded(_toggleService.ToggleToRigMode);

    public ToggleResult ToggleToNormalMode() => RunGuarded(_toggleService.ToggleToNormalMode);

    // D-04 pass-throughs — pure reads, no guard. Safe to call at any time,
    // including while a toggle is in flight (mirrors how MainForm.RefreshUi()
    // already calls IsInRigMode() immediately after every toggle today).
    public bool IsInRigMode() => _toggleService.IsInRigMode();
    public bool IsSettingsConfigured() => _toggleService.IsSettingsConfigured();

    // D-02: ONE shared flag guards BOTH directions — a rig-mode toggle in flight
    // must also reject a normal-mode request, not just a same-direction repeat,
    // because both directions mutate the same monitor/audio/app state.
    private ToggleResult RunGuarded(Func<ToggleResult> pipeline)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            throw new ToggleInProgressException(
                "A toggle is already in progress. Wait for it to finish, then try again.");
        }

        try
        {
            return pipeline();
        }
        finally
        {
            // Must run even when ToggleService throws (its own preflight
            // InvalidOperationExceptions, or anything unexpected) — otherwise a
            // single failed toggle would permanently wedge the app in "busy"
            // and every future request (including a well-formed one) would be
            // rejected forever.
            Volatile.Write(ref _busy, 0);
        }
    }
}
```

### Pattern 3: The dedicated rejection exception (D-05)
**What:** Subclass `InvalidOperationException` rather than a bare `Exception` or a new `ToggleResult` variant.
**When to use:** For exactly this "preflight-style, outside-the-step-checklist" rejection, matching `ToggleService`'s own existing precedent (unconfigured settings, missing companion app path — both throw `InvalidOperationException` today, see `ToggleService.cs` lines 66-67, 76-77).
**Example:**
```csharp
// Source: matches existing ToggleService preflight-exception precedent
namespace RigToggle.Core;

/// <summary>
/// Thrown by ToggleOrchestrator when a toggle is requested while another is already
/// in flight (CORE-06). Subclasses InvalidOperationException (not a bare Exception)
/// so it is caught by MainForm.BtnToggle_Click's existing `catch (Exception ex)` block
/// with zero UI changes — the same way ToggleService's own preflight guards
/// (unconfigured settings, missing companion app path) are already surfaced today.
/// </summary>
public sealed class ToggleInProgressException : InvalidOperationException
{
    public ToggleInProgressException(string message) : base(message) { }
}
```
This is why success criterion 3 ("existing GUI toggle button's behavior... unchanged") is satisfied for free — no new `MessageBox`, no new catch clause, no new branch in `MainForm`.

### Anti-Patterns to Avoid
- **`lock (_gate) { return pipeline(); }` around the whole call:** This is the single most likely accidental regression of D-01. A plain `lock`/`Monitor.Enter` *blocks* the second caller until the first finishes, then lets it proceed — that is a queue, which is explicitly the rejected design (D-01: "never gets silently serialized behind it"). If `Monitor` is used at all, it must be the non-blocking `Monitor.TryEnter(gate, 0)` variant, mirroring `Interlocked.CompareExchange`'s semantics, not a bare `lock` statement.
- **A bare `bool` flag without `Interlocked`:** `if (!_busy) { _busy = true; ... }` is a classic check-then-act race. Even though today's only caller (`MainForm.BtnToggle_Click`) runs entirely on the single WinForms UI thread with no nested message pump during the mutation call, this pattern would silently become unsafe the moment Phase 10 adds a CLI/IPC listener thread — and would already be fragile/non-idiomatic even before that. Use `Interlocked.CompareExchange` from day one.
- **Guarding `IsInRigMode()`/`IsSettingsConfigured()`:** These are pure reads used for UI-state derivation (`RefreshUi()` calls `IsInRigMode()` after every toggle, `MainForm` calls `IsSettingsConfigured()` before offering "Switch to Rig Mode"). Guarding them would be both unnecessary (D-04 specifies plain pass-throughs) and actively harmful (it would make the UI unable to refresh its own label while a toggle is running).
- **Resetting the flag inside the `try` block instead of `finally`:** If `ToggleService.ToggleToRigMode()` throws (including its own existing preflight `InvalidOperationException`s for unconfigured settings), a reset placed after the call inside `try` never executes, permanently wedging the orchestrator in "busy" for the rest of the app's lifetime.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Non-blocking single-flight guard | A custom spin-lock class, a hand-rolled semaphore, or a `ManualResetEvent`-based gate | `Interlocked.CompareExchange` on a single `int` field | It is already the minimal, correct, allocation-free primitive for exactly this shape (atomic compare-and-swap) — anything hand-built on top of lower-level primitives to achieve the same "atomic test-and-set" semantic would just be reimplementing what `Interlocked` already does, with more surface area for bugs |
| Cross-thread marshaling for a future CLI/IPC trigger (Phase 10, not this phase) | A custom message queue between the listener thread and the UI thread | `Control.Invoke`/`BeginInvoke` (WinForms' built-in cross-thread marshaling) — **only relevant once Phase 10 actually adds that thread; not built in this phase** | Flagged here only so the orchestrator's design doesn't accidentally assume single-thread-forever; the orchestrator itself should remain thread-safe (which `Interlocked` already guarantees) so Phase 10 doesn't have to retrofit thread-safety into it — see Open Questions |

**Key insight:** The entire "don't hand-roll" surface for this phase is one BCL method call. The temptation to build something more elaborate (a queue, a semaphore wrapper, a custom `IDisposable` guard-scope helper) should be resisted — D-01 explicitly wants the simplest correct thing, and `Interlocked.CompareExchange` plus `try`/`finally` is it.

## Common Pitfalls

### Pitfall 1: Reaching for `lock` and accidentally re-introducing the rejected "queue" design
**What goes wrong:** A `lock (_gate) { return pipeline(); }` around the guarded call compiles, looks idiomatic, and even "passes" a naive double-click test (only one toggle result is ever returned to each caller — the second caller just waits and then also gets a result, instead of an exception).
**Why it happens:** `lock` is the most familiar C# concurrency primitive, and "protect this from concurrent access" reflexively suggests it. But `lock` solves mutual exclusion via blocking/waiting, not rejection.
**How to avoid:** Use `Interlocked.CompareExchange` (or `Monitor.TryEnter(gate, timeout: 0)` if `Monitor` is preferred) — both are non-blocking by construction, so there is no way to accidentally write the blocking version.
**Warning signs:** A reentrancy test that asserts "the second call also eventually succeeds" instead of "the second call throws `ToggleInProgressException`" — that assertion shape is itself evidence the implementation queues rather than rejects.

### Pitfall 2: Flaky, timing-based reentrancy tests
**What goes wrong:** A test that does something like `Task.Run(() => orchestrator.ToggleToRigMode()); Thread.Sleep(50); Assert.Throws<...>(() => orchestrator.ToggleToNormalMode());` is nondeterministic — on a slow CI machine or under load, the first call may already have completed within the 50ms window, making the second call the *only* call in flight (no exception thrown, false negative), or conversely on a fast machine the timing may happen to work every time locally but fail intermittently elsewhere.
**Why it happens:** Reentrancy is inherently about "call B while call A hasn't finished yet" — the naive way to express that in a test is a sleep, but sleeps are a race against however long the guarded work actually takes, which is not a stable quantity.
**How to avoid:** Make the guarded work block on a controllable synchronization primitive instead of taking real time. Extend (or add alongside) `FakeMonitorController` a test-only double whose `DeactivateMonitors` call: (1) signals a `ManualResetEventSlim` to tell the test "the first call has genuinely entered the guarded region," then (2) blocks on a second `ManualResetEventSlim` until the test explicitly releases it. The test then: starts the first call on a background `Task`, waits (with no timeout risk — `ManualResetEventSlim.Wait()` blocks until signaled, it does not guess a duration) on the first signal, asserts the second (synchronous, same-thread) call throws `ToggleInProgressException`, then releases the block and awaits the first `Task`'s result. This is fully deterministic — no `Thread.Sleep`, no timing assumption, no flakiness under CI load.
**Warning signs:** Any `Thread.Sleep(...)` in a reentrancy test; any assertion whose pass/fail depends on how fast the guarded operation happens to run on the current machine.

### Pitfall 3: Forgetting the guard must span the *entire* orchestrator call, not just the atomic flag flip
**What goes wrong:** Setting `_busy = 1`, then calling `pipeline()`, then setting `_busy = 0` as three separate statements (rather than the atomic flip + `try`/`finally` shown in Pattern 2) reintroduces a race window and, more importantly, loses the "always reset even on exception" guarantee if the reset line is written after (not in a `finally` following) the call.
**Why it happens:** It is easy to write the "happy path" first (flip, call, flip back) and add exception safety as an afterthought, or skip it because none of the existing hand-written fakes throw by default.
**How to avoid:** Write the guard as `Interlocked.CompareExchange(...) != 0 → throw` immediately followed by `try { return pipeline(); } finally { reset }` from the start — this is the only shape that is correct both for the happy path and for every one of `ToggleService`'s existing preflight-exception paths (unconfigured settings, missing companion app, corrupted snapshot).
**Warning signs:** A test where `ToggleToRigMode()` throws (e.g. unconfigured settings) and a subsequent, otherwise-valid `ToggleToRigMode()` call incorrectly also throws `ToggleInProgressException` — that is the wedged-flag bug from Pitfall 3 manifesting.

## Code Examples

Verified patterns already used elsewhere in this codebase, for consistency:

### Existing preflight-exception precedent (what D-05 is matching)
```csharp
// Source: src/RigToggle.Core/ToggleService.cs lines 60-68 (existing code, unchanged by this phase)
if (!IsFullyConfigured(settings))
{
    throw new InvalidOperationException(
        "Rig Toggle settings are not fully configured. Open Settings and choose at least one monitor to disable or enable, both audio devices, and the companion app path before switching to Rig Mode.");
}
```
The new `ToggleInProgressException` should read the same way — a clear, user-facing message (this is a single-user diagnostic tool per `MainForm`'s own comment at line ~151: "surfacing the real error is more useful than hiding it").

### Existing MainForm catch-and-display path (why no MainForm UI changes are needed)
```csharp
// Source: src/RigToggle.App/MainForm.cs lines 142-160 (existing code, unchanged by this phase)
catch (Exception ex)
{
    MessageBox.Show(
        this,
        $"Something went wrong while toggling:\n\n{ex.GetType().Name}: {ex.Message}\n\nTry again, or check Settings.",
        "Rig Toggle",
        MessageBoxButtons.OK,
        MessageBoxIcon.Warning);
}
```
Because `ToggleInProgressException` is an `Exception` (via `InvalidOperationException`), a rejected double-click will surface here automatically as "Something went wrong while toggling: ToggleInProgressException: A toggle is already in progress..." — satisfying success criterion 1 ("clear 'toggle already in progress' response") without touching this method.

### Existing hand-written-fake test convention (to extend for orchestrator tests)
```csharp
// Source: src/RigToggle.Tests/Doubles/FakeControllers.cs (existing pattern, no mocking framework)
public sealed class FakeMonitorController : IMonitorController
{
    private readonly List<string> _callLog;
    // ... existing throw-on-disable / mutates-before-throwing constructor flags ...
}
```
Recommend adding a new, narrowly-scoped test double (not bloating `FakeMonitorController`'s already multi-purpose constructor further) purely for the deterministic reentrancy test, e.g. `Doubles/BlockingMonitorController.cs`, whose `DeactivateMonitors` signals one `ManualResetEventSlim` and waits on another, as described in Pitfall 2.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| N/A — this is new code in this codebase (no prior concurrency guard existed anywhere; `grep -rn "Interlocked\|volatile\|lock ("` across `src/` returns zero matches) | `Interlocked.CompareExchange`-based single-flight guard | This phase | First introduction of any explicit thread-safety primitive in the codebase — sets the pattern future phases (8-10) will extend if they ever need similar guards |

**Deprecated/outdated:** Nothing to deprecate — greenfield within this codebase.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | No genuine cross-thread concurrency exists in the app today — all current and Phase 8/9 trigger sources (button, tray menu, `WM_HOTKEY`) dispatch through the single WinForms UI-thread message loop, so they cannot literally execute `BtnToggle_Click`-equivalent handlers in parallel with each other today. This is based on general WinForms/Win32 message-loop semantics (training knowledge), not verified against this specific app's future Phase 8/9 code (which doesn't exist yet). | Summary, Open Questions | Low — even if wrong, the `Interlocked`-based guard recommended here is correct regardless of whether real parallelism exists, so no design change would be needed; this assumption only affects how strongly the research argues the guard is "future-proofing" vs. "needed today." |
| A2 | Phase 10's CLI/IPC listener (out of scope for this phase, mentioned only as forward context) will run on a thread distinct from the UI thread, and any UI-visible side effect from that path will need explicit `Control.Invoke`/`BeginInvoke` marshaling. This is inferred from the "CLI trigger... must signal the resident instance via IPC" note in REQUIREMENTS.md's Out-of-Scope table, not from any Phase 10 design that exists yet. | Open Questions | Low for this phase specifically (Phase 10 is 3 phases away and will do its own research/context gathering) — flagged only so Phase 7's orchestrator design doesn't need revisiting later purely for thread-safety reasons. |

## Open Questions (RESOLVED)

1. **RESOLVED — Does the reentrancy guard need to be verified thread-safe for a genuinely cross-thread caller in THIS phase, or is UI-thread-only sufficient for now?**
   - What we know: Today's only trigger (`MainForm.BtnToggle_Click`) and Phases 8-9's planned triggers (tray menu, hotkey) all appear to be UI-thread-dispatched per standard WinForms/Win32 semantics.
   - What's unclear: Whether Phase 10's CLI/IPC listener (the one plausible source of genuine cross-thread calls) will call this same orchestrator instance directly from a background thread, or will marshal onto the UI thread before calling it — that design doesn't exist yet.
   - Recommendation: Build the guard with `Interlocked.CompareExchange` regardless (it costs nothing extra and is correct either way) so this question never needs to be revisited later — the orchestrator is thread-safe by construction whether or not Phase 10 ends up needing that property.
   - Resolution: Adopted in full — `07-01-PLAN.md`'s Task 1 builds the guard with `Interlocked.CompareExchange`.

2. **RESOLVED — Should the reentrancy test double live in the shared `Doubles/` folder or be scoped to the orchestrator's own test file?**
   - What we know: The existing convention (`FakeControllers.cs`) centralizes fakes in `Doubles/` and reuses them across `ToggleServiceTests.cs`.
   - What's unclear: Whether a blocking/event-driven double (needed only for this one reentrancy test) belongs in the shared file (bloating its already-multi-parameter constructors) or as a small dedicated class.
   - Recommendation: Left to planner/executor per CONTEXT.md's Claude's Discretion — this research recommends a small dedicated double to avoid coupling the widely-reused `FakeMonitorController` to a concern only one test needs, but either placement is workable.
   - Resolution: Adopted the recommendation — `07-01-PLAN.md` creates a small dedicated `BlockingMonitorController.cs` rather than extending `FakeMonitorController`.

## Environment Availability

Skipped — this phase has no external tool/service/runtime dependencies beyond the .NET 10 SDK already established and verified working by every prior phase in this repository (Phase 2-6 all built and tested successfully against it).

## Validation Architecture

Skipped — `.planning/config.json` sets `workflow.nyquist_validation: false` explicitly.

## Security Domain

`security_enforcement` is absent from `.planning/config.json` (workflow block has no such key), so per the instructions this section is included, though this phase has almost no ASVS-relevant surface: it is a purely local, single-user, no-network, no-auth desktop refactor.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V1 Architecture / Business Logic | Marginally | The reentrancy guard is itself a business-logic-integrity control (preventing a race condition from corrupting persisted monitor/audio state) — this phase's entire purpose *is* closing a V1.11-style "race condition in business flow" gap, just not one exposed to a network attacker |
| V2 Authentication | No | Single local user, no authentication surface anywhere in this app |
| V3 Session Management | No | No sessions — this is a stateless-per-invocation desktop utility |
| V4 Access Control | No | No multi-user/permission model exists or is planned |
| V5 Input Validation | No | No new external input is introduced by this phase (no new file, network, or user-input parsing) |
| V6 Cryptography | No | Not touched by this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|-----------------------|
| Check-then-act race on a shared mutable flag (TOCTOU at the code level, not a security-attacker scenario here but the identical bug class) | Tampering (state corruption) / mild Denial of Service (a wedged busy-flag would make the app permanently reject all future toggles) | `Interlocked.CompareExchange` for the atomic check+set; `try`/`finally` to guarantee the flag is always released, closing the "wedged forever after one exception" failure mode described in Pitfall 3 |

This is a reliability property being achieved through a mechanism that happens to also be the standard mitigation for a security-relevant bug class (race conditions) — worth documenting for completeness per the instructions, but this phase does not introduce or close any actual attacker-facing vulnerability.

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked.compareexchange — official BCL API reference, confirms atomic compare-and-swap semantics for the exact primitive recommended above
- Direct reading of `src/RigToggle.Core/ToggleService.cs`, `src/RigToggle.App/MainForm.cs`, `src/RigToggle.App/Program.cs`, `src/RigToggle.Tests/ToggleServiceTests.cs`, `src/RigToggle.Tests/Doubles/FakeControllers.cs` — this phase's actual integration surface, read in full
- `grep -rn "Interlocked\|volatile\|lock ("` across `src/` (zero matches) — confirms no prior concurrency-guard pattern exists in this codebase to be consistent with; this phase establishes the first one

### Secondary (MEDIUM confidence)
- https://dotnettutorials.net/lesson/interlocked-vs-lock-in-csharp/ — corroborates the standard `Interlocked` vs. `lock` tradeoff framing (non-blocking atomic operation vs. blocking mutual exclusion), consistent with official docs

### Tertiary (LOW confidence)
None — no unverified claims were needed for this phase's scope.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — single BCL primitive, confirmed against official Microsoft docs, zero new dependencies to evaluate
- Architecture: HIGH — directly derived from reading the actual existing `ToggleService.cs`/`MainForm.cs`/`Program.cs` code and CONTEXT.md's locked D-01 through D-05 decisions, not from external research
- Pitfalls: HIGH — all three pitfalls (blocking-lock regression, flaky timing tests, unreleased-flag-on-exception) are well-established, mechanically verifiable failure modes of this exact primitive, not speculative

**Research date:** 2026-07-29
**Valid until:** No expiry concern — this is a stable BCL primitive with no version-sensitivity; re-research only if .NET's `Interlocked` API surface itself changes (extremely unlikely) or if CONTEXT.md's locked D-01/D-02/D-03/D-05 decisions are revisited.
