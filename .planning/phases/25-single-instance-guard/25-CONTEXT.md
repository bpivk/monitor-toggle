# Phase 25: Single-Instance Guard - Context

**Gathered:** 2026-08-20
**Status:** Ready for planning

<domain>
## Phase Boundary

A mutex-based single-instance guard prevents two Rig Toggle processes from ever running side by side. A blocked duplicate launch brings the already-running instance to front (restored from minimized/tray-hidden if needed) instead of silently exiting, erroring, or starting a second process. The guard also exposes a deliberate, explicitly-built bypass path — a real `--apply-update` CLI flag, checked before the mutex — that Phase 26's auto-update relaunch will reuse; Phase 25 does not implement update-apply logic itself, only proves the bypass mechanism works via a scripted simulated relaunch. No new Settings/UI surface is in scope — this is an always-on background behavior, not a user-configurable toggle.

</domain>

<decisions>
## Implementation Decisions

### Duplicate-launch feedback
- **D-01:** A blocked duplicate launch produces no toast/notification — the existing instance is silently focused/restored (`Show()`, `WindowState = Normal`, `Activate()`), matching the existing tray-icon left-click restore path (`MainForm.cs` ~1520-1531). The visible window coming to front IS the confirmation; nothing else is needed. — **Reversibility:** reversible — a toast call can be added later without touching the guard's core logic.
- **D-02:** One universal rule for all duplicate-launch scenarios — no special-casing based on why the second launch happened (accidental double-click, autostart Run-key racing a manual launch, etc.). Every blocked launch is treated identically: mutex acquisition fails → signal existing instance → existing instance focuses/restores → second process exits.

### Internal-relaunch bypass contract
- **D-03:** The bypass is a dedicated CLI flag, not a generic "skip the mutex check" switch. Checked as the very first branch in `Program.cs Main()` — before `SingleInstanceGuard` is touched at all, before settings/mode-store bootstrap, before any Form is constructed — mirroring how `StartupArgs.ShouldStartHidden(args)` already gates the visible-vs-hidden startup branch. — **Reversibility:** one-way — Phase 26 (UPDATE-07) directly consumes this exact flag name and parse contract; renaming it after Phase 26 ships means touching both phases' code.
- **D-04:** The flag is the real, final name Phase 26 will use: `--apply-update <args>`, parsed by a new `StartupArgs.TryGetApplyUpdateArgs(args)` helper (same shape/location as the existing `ShouldStartHidden` helper). Phase 25 builds this now with a placeholder body (e.g. a minimal entry point that proves control transferred and the mutex/tray/hotkey path was skipped) — Phase 26 replaces the placeholder body with real update-apply logic; the flag name and parsing contract do not change between phases. — **Reversibility:** one-way — same rationale as D-03, this IS the cross-phase contract.

### Verification approach
- **D-05:** Both the rapid-relaunch test (ROADMAP SC1: exactly one process survives N rapid launches) and the bypass simulation test (ROADMAP SC3: `--apply-update` skips the mutex/tray/hotkey path without racing it) are automated xUnit tests in `RigToggle.Tests`, launching the actual built/published exe as a real child process — not a standalone manual script. This matches the project's existing automated-verification discipline (the 4 deterministic `ToggleOrchestrator` reentrancy tests from Phase 7).
- **D-06:** These process-launching tests run in the normal CI build (`build.yml`), not tagged to skip CI. A single-instance regression should be caught automatically on every push, consistent with how the rest of the codebase is held to an automated-test bar rather than relying on manual/rig-only verification for logic that doesn't require real display/audio hardware.

### Claude's Discretion
- The activation-signal mechanism used to wake the existing instance once the mutex check fails (`RegisterWindowMessage`/`PostMessage(HWND_BROADCAST,...)` per STACK.md's recommendation vs. a named pipe per ARCHITECTURE.md's data-flow sketch — the two research docs are not fully consistent on this point). Resolve during research/planning; either satisfies the requirements and neither was raised as a user-facing concern during discussion. `MainForm.WndProc` already intercepts one custom message (`WM_HOTKEY`, Phase 9's global hotkey) so a `RegisterWindowMessage`-based approach fits the codebase's existing convention if there's no other reason to prefer a named pipe.
- Named-mutex naming (`Global\RigToggle-{GUID}` per STACK.md) — exact GUID value, and whether the receiver-ready race described in PITFALLS.md Pitfall 8 (existing instance's signal-receiver must be listening before the loser signals) is handled via retry-with-backoff on the loser side, early receiver setup on the winner side, or both.
- Whether `SingleInstanceGuard` is a new standalone class/composition-root object (following the codebase's established preference — see Pitfall 7's note on `ToggleOrchestrator` being a new wrapper, not logic folded into `ToggleService`) — expected to be, but exact shape is an implementation detail.
- Test harness specifics for D-05/D-06: whether the child-process tests target the `dotnet publish` single-file output or a `dotnet build` output, how many rapid-launch iterations constitute a reliable rapid-relaunch test per PITFALLS.md Pitfall 8's warning that manual/single-shot testing misses this race, and how CI timing/flakiness risk is mitigated if it appears.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### This milestone's research (v2.2) — directly covers this phase
- `.planning/research/STACK.md` §"2. Single-Instance Guard + Focus-Existing-Window" — recommends `System.Threading.Mutex` named `Global\RigToggle-{GUID}`, held for process lifetime, released in `finally` around `Application.Run`; `RegisterWindowMessage`/`PostMessage(HWND_BROADCAST,...)` for the wake-existing-instance signal; explicitly rules out named pipes/IPC-with-payload as unneeded (pure "wake up and show yourself" signal, no data channel)
- `.planning/research/ARCHITECTURE.md` §"Pattern 1: Startup-gate bypass for the internal relaunch helper" — the exact `Program.cs Main()` branch ordering this phase must follow (bypass check first, before mutex, before any Form); §"Startup Sequencing" data-flow diagram — full four-path `Main()` flow this phase's changes fit into (note: this diagram's mention of "named pipe" for signaling is the STACK.md/ARCHITECTURE.md inconsistency flagged under Claude's Discretion above)
- `.planning/research/PITFALLS.md` Pitfall 4 (relaunch races the guard — deferred to whichever of Phase 25/26 ships last; since Phase 25 ships first, Phase 26 owns closing this out), Pitfall 7 (do not reuse `ToggleOrchestrator._busy` — this is a new, separate cross-process primitive), Pitfall 8 (mutex-win alone doesn't guarantee the loser can signal the winner — early receiver setup + retry-with-backoff on the loser side, verified via scripted rapid repeated launches per D-05/D-06, not manual double-clicks)
- `.planning/research/FEATURES.md`, `.planning/research/SUMMARY.md` — milestone-level framing confirming zero new NuGet packages, hand-rolled BCL-only implementation is the deliberate recommendation for this feature, consistent with the project's existing `IPolicyConfig`/`RegisterHotKey` precedent

### Constraints (locked, do not re-litigate)
- `.planning/REQUIREMENTS.md` INSTANCE-01, INSTANCE-02, UPDATE-07 — exact requirement text this phase satisfies
- `.planning/ROADMAP.md` Phase 25 section — success criteria (exactly one process at any time via scripted rapid-relaunch test; existing instance focused/restored from minimized/tray-hidden; explicit bypass verified by scripted simulated relaunch)

### Current code this phase touches or must follow the pattern of
- `src/RigToggle.App/Program.cs` — composition root; exact insertion point for the bypass check (before `SetColorMode`/`ApplicationConfiguration.Initialize()`? — no, per Pattern 1, the bypass check comes after those two position-sensitive calls but before mutex/settings/Form construction) and the mutex acquisition point
- `src/RigToggle.Core/StartupArgs.cs` — existing `ShouldStartHidden(args)` helper; `TryGetApplyUpdateArgs(args)` (D-04) follows its exact pattern/location
- `src/RigToggle.App/MainForm.cs` — existing `WndProc` override (~line 247, already handles `WM_HOTKEY`) is the natural home for a new custom-message case if `RegisterWindowMessage` is chosen; existing tray-restore sequence (~lines 1520-1531: `Show()`; `WindowState = FormWindowState.Normal`; `Activate()`) is the exact restore call to reuse for D-01/D-02
- `src/RigToggle.Core/ToggleOrchestrator.cs` — existing `_busy` `Interlocked.CompareExchange` reentrancy guard; explicitly NOT the mechanism for this phase (Pitfall 7) — kept as a reference for "how this codebase structures a dedicated guard class," not as code to extend
- `src/RigToggle.Tests/` — existing test project; home for the new D-05/D-06 process-launching tests, following the pattern of the existing 4 `ToggleOrchestrator` reentrancy tests from Phase 7
- `.github/workflows/build.yml` — CI build this phase's new tests must run in per D-06

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MainForm`'s tray-icon left-click restore sequence (`Show()` / `WindowState = FormWindowState.Normal` / `Activate()`) — the exact restore behavior D-01/D-02 need; already proven and rig-verified for bringing the app's own window to front from a hidden/minimized/tray state
- `StartupArgs.ShouldStartHidden(args)` — the existing CLI-arg-parsing convention `TryGetApplyUpdateArgs(args)` (D-04) should follow structurally
- `ToggleOrchestrator`'s `Interlocked.CompareExchange` busy-flag — not reused directly (Pitfall 7), but its existence as a small, dedicated, single-purpose guard class is the structural precedent for how `SingleInstanceGuard` should likely be shaped (a new class, not logic folded into `Program.cs` or an existing service)

### Established Patterns
- Composition-root-only construction: `Program.cs` is the one place real adapters/stores are wired together; MainForm/SettingsForm never `new` a concrete adapter themselves. Any new `SingleInstanceGuard` object follows this same rule.
- Best-effort, non-blocking startup idiom: most of `Program.cs`'s startup steps (trace listener, hotkey registration) are wrapped to never throw/block startup on failure. `StartupRecoveryChecker.Run(...)` is the one deliberate exception (not wrapped). The mutex acquisition and bypass-flag check are more likely to belong with the deliberate-exception category (a failure here has real correctness consequences, not just a degraded diagnostic), but this is a planning-time judgment call, not locked here.
- `MainForm.WndProc` already special-cases one custom message (`WM_HOTKEY`) with a documented comment about base.WndProc ordering — precedent for adding a second case cleanly if the signal mechanism ends up being `RegisterWindowMessage`.

### Integration Points
- `Program.cs Main()` — the bypass check and mutex acquisition both insert here, in that order, before any of the existing bootstrap (settingsStore, modeStore, markerStore, StartupRecoveryChecker, controllers, mainForm construction)
- `MainForm` — either gains a new `WndProc` case (message-based signaling) or nothing at all (named-pipe signaling would live in a separate listener object instead) — resolved by the Claude's-Discretion signal-mechanism choice above
- `RigToggle.Tests` — new test class(es) alongside existing `ToggleOrchestrator` reentrancy tests

</code_context>

<specifics>
## Specific Ideas

No specific UI/UX phrasing or reference examples were given beyond the decisions above — the user consistently picked the option that reused existing codebase patterns (silent tray-restore behavior, `ShouldStartHidden`-style flag parsing, automated-test discipline matching Phase 7's reentrancy tests) over introducing new patterns.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. No scope-creep suggestions came up during discussion.

### Reviewed Todos (not folded)
None — `todo.match-phase` returned zero matches for this phase.

</deferred>

---

*Phase: 25-single-instance-guard*
*Context gathered: 2026-08-20*
