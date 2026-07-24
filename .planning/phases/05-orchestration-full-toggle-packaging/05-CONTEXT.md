# Phase 5: Orchestration, Full Toggle & Packaging - Context

**Gathered:** 2026-07-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Harden the already-real toggle flow with honest partial-failure reporting (CORE-04), and ship a standalone .exe (PACKAGING-01). Most of this phase's other success criteria are **already structurally satisfied** by prior phases and need verification, not new implementation:

- CORE-01/CORE-02 (one-action toggle in both directions): already wired — `MainForm.BtnToggle_Click` already calls `ToggleService.ToggleToRigMode()`/`ToggleToNormalMode()` against fully-real adapters (monitor real since Phase 4, audio/app real since Phase 3).
- CORE-03 (snapshot before mutate): already implemented — `ToggleService.ToggleToRigMode()` captures and persists monitor+audio state via `_snapshotStore.Save()` before any mutating call.
- CORE-05 (correct mode detection after a crash): already implemented — `ToggleService.IsInRigMode()` derives purely from snapshot-file presence (D-14, Phase 2), which is crash-safe by construction; `MainForm.RefreshUi()` already reflects it correctly on `OnLoad`.

**What's actually new in this phase:**
1. **CORE-04** — `ToggleToRigMode` currently has *zero* per-step failure isolation (fully linear, unconditional); `ToggleToNormalMode` already has partial isolation (monitor/audio restore each independently try/caught) but reports failure via a single generic exception message in `MainForm`'s catch block. Neither direction currently tells the user "which steps succeeded/failed" as REQUIREMENTS.md demands — this phase builds that.
2. **PACKAGING-01** — no publish profile or self-contained/single-file config exists yet in any `.csproj`. This is genuinely greenfield for this phase.

</domain>

<decisions>
## Implementation Decisions

### Partial-Failure Reporting (CORE-04)
- **D-01:** On any toggle failure, the user sees a step checklist — e.g. "Monitor: disabled OK / Audio: FAILED (reason) / App: not attempted" — not a generic exception message. This is the literal reading of REQUIREMENTS.md CORE-04 ("reports which steps succeeded/failed").
- **D-02:** `ToggleToRigMode`/`ToggleToNormalMode` change from void-returning-and-throwing to returning a structured result object (list of step name + outcome, e.g. succeeded/failed-with-reason/not-attempted). `MainForm` renders the checklist from that result rather than catching and formatting an exception. This touches `ToggleService`'s public API and its existing unit tests (`ToggleServiceTests.cs`) — planner should expect test updates, not just new code.
- **D-03:** The structured-result treatment applies to **both** toggle directions, including `ToggleToNormalMode`'s existing per-step restore isolation (monitor restore / audio restore / app minimize) — not just the new rig-mode path. Consistent checklist UX regardless of direction.

### Rig-Mode Failure Isolation (CORE-04, behavior — distinct from reporting)
- **D-04:** `ToggleToRigMode` stays **stop-on-first-failure** — it does NOT get `ToggleToNormalMode`'s per-step try/catch-and-continue treatment. Deliberate, reasoned choice: forward-direction steps have real dependencies the user cares about (no point switching audio or launching the companion app if the monitor didn't actually disable), and stop-on-first-failure keeps the result trivially reportable (one step failed, later steps are cleanly "not attempted" — no ambiguity to reconcile).
- **D-05:** This creates an intentional asymmetry between the two directions: `ToggleToRigMode` = stop-on-first-failure (new, this phase); `ToggleToNormalMode` = isolate-and-continue-per-step (existing since Phase 3/gap-closure 03-04, unchanged). Both report through the same structured-result/checklist shape (D-02/D-03) — only the underlying continue-vs-stop behavior differs, and that difference is deliberate, not an oversight. Document this explicitly in code comments per this project's established pattern (see e.g. `ToggleService.cs`'s existing XML-doc rationale style) so a future reader doesn't "fix" it into symmetry.
- **D-06:** No auto-revert/auto-rollback on failure in either direction — already the existing behavior and explicitly required by REQUIREMENTS.md CORE-04 ("stops rather than silently continuing or auto-reverting"). Not re-litigated, just confirmed unchanged.

### Packaging (PACKAGING-01)
- **D-07:** Publish configuration lives in a `PublishProfiles/win-x64.pubxml` (or equivalent `PropertyGroup`) added to `RigToggle.App.csproj`, capturing `SelfContained=true`, `PublishSingleFile=true`, `RuntimeIdentifier=win-x64`, `PublishTrimmed=false` (per CLAUDE.md's explicit trimming warning — do NOT trim, COM/P-Invoke code is trim-unsafe) — plus a short README documenting the one-line `dotnet publish` invocation so the exact flags don't need to be remembered each rebuild.
- **D-08:** No custom icon or version-info branding on the shipped exe — bare default icon is fine. This is a personal single-user tool, not something distributed to others; zero extra asset work.
- **D-09:** Windows-only x64 target — no need for arm64 or any other RID (matches the single known rig PC's hardware; consistent with the project's "single-user personal tool" framing throughout PROJECT.md).

### Startup Crash-Recovery UX (CORE-05 — verification only, no new code)
- **D-10:** No new startup dialog/toast when the app detects it's in rig mode on launch. The existing `MainForm.RefreshUi()` behavior (label reads "Mode: Rig", toggle button reads "Switch to Normal Mode") already IS the crash-recovery signal and is sufficient — confirmed explicitly, not just left undiscussed. This phase's job for CORE-05 is an end-to-end verification checkpoint (kill the process while in rig mode, relaunch, confirm correct state), not new implementation.

### Claude's Discretion
- Exact structured-result type shape (record vs. class, exact step-name enum/strings, how "not attempted" is represented) — left to planner, informed by D-01/D-02/D-03.
- Whether the win-x64 publish profile lives as a `.pubxml` file vs. inline `PropertyGroup` conditioned on a publish property — both satisfy D-07; planner's call based on what's cleaner to maintain.
- README location and exact wording for the publish instructions (D-07) — no README currently exists in the repo root; planner/executor's call on placement and level of detail.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level
- `.planning/PROJECT.md` — core value, requirements, evolution rules
- `.planning/REQUIREMENTS.md` — CORE-01/02/03/04/05, PACKAGING-01 (mapped to this phase)
- `CLAUDE.md` — packaging section: `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true`, explicit `PublishTrimmed=true` prohibition (COM/P-Invoke trim-safety), framework-dependent-deployment prohibition

### Prior phases
- `.planning/phases/03-app-audio-control/03-CONTEXT.md` — D-03/D-04 (audio verify-and-throw pattern), D-05 (app-path preflight ordering) — the exception-bubbling precedent this phase's structured-result work replaces
- `.planning/phases/04-monitor-control-production/04-CONTEXT.md` — D-05 (monitor-restore failure must bubble, not be swallowed — directly informs why `ToggleToNormalMode`'s monitor-restore failure still needs to surface clearly in the new checklist, not get silently absorbed into "isolate-and-continue")
- `.planning/phases/02-foundations-gui-shell/02-CONTEXT.md` — D-14 (mode derived from snapshot-file presence — the mechanism CORE-05 already relies on)

### Existing code (this phase's actual surface area)
- `src/RigToggle.Core/ToggleService.cs` — `ToggleToRigMode()`/`ToggleToNormalMode()`/`IsInRigMode()` — the class whose public API changes per D-02
- `src/RigToggle.App/MainForm.cs` — `BtnToggle_Click`'s try/catch — the UI surface that renders the new checklist per D-01
- `src/RigToggle.Tests/ToggleServiceTests.cs` — existing unit tests exercising the current void/throw contract; will need updates for the new structured-result contract

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/RigToggle.Core/ToggleService.cs` — orchestration logic (snapshot → mutate → restore sequencing) is already fully real; this phase changes its *return contract* (D-02), not its underlying step logic or step order.
- `src/RigToggle.App/MainForm.cs` — already has a working catch-all exception handler (`BtnToggle_Click`) and `RefreshUi()` pattern to build on; the new checklist replaces/extends this handler rather than introducing new UI plumbing.
- `src/RigToggle.Core/Abstractions/{IMonitorController,IAudioController,IAppController}.cs` — no signature changes anticipated; these are called the same way, just wrapped by the new result-tracking logic in `ToggleService`.

### Established Patterns
- Interface-per-concern + composition root (`Program.cs`) — unchanged by this phase; `Program.cs` already wires the fully-real adapters (no fakes remain to swap).
- Verify-and-throw pattern from Phase 3/4 (audio `SetDefault`, monitor `Disable`/`Restore`) — these lower-level verification throws become the "reason" text surfaced in the new step checklist (D-01), not replaced by it.
- XML-doc rationale comments explaining *why*, not *what* (see `ToggleService.cs`'s existing extensive remarks) — continue this convention when documenting the deliberate rig-mode-vs-normal-mode asymmetry (D-05).

### Integration Points
- `ToggleService.ToggleToRigMode()`/`ToggleToNormalMode()` signatures change from `void` to a structured result type (D-02) — ripples to `MainForm.BtnToggle_Click` (consumes the result to build the checklist) and `ToggleServiceTests.cs` (asserts on the result instead of catching exceptions).
- New `RigToggle.App.csproj` publish profile (D-07) — no code changes, but is new project-file surface area not touched since Phase 2's scaffold.
- No changes anticipated to `SettingsForm.cs`, `MonitorConfirmDialog.cs`, `JsonSettingsStore.cs`, `JsonSnapshotStore.cs`, or any `RigToggle.Windows` adapter — this phase's surface area is narrowly `ToggleService` + `MainForm` + packaging config.

</code_context>

<specifics>
## Specific Ideas

- Step checklist wording should follow the existing MessageBox tone already used in `MainForm.cs` (plain, includes real exception detail — "surfacing the real error is more useful than hiding it" per the existing D-13/T-02-FAKEFAIL comment) — e.g. "Monitor: disabled OK / Audio: FAILED (Could not resolve device X) / App: not attempted."
- The rig-mode-vs-normal-mode isolation asymmetry (D-04/D-05) is a considered decision, not an inconsistency — must be documented inline in code so it isn't "corrected" into false symmetry later.
- Packaging: self-contained, single-file, win-x64, untrimmed, no icon — a deliberately minimal, no-frills release setup matching the project's "personal tool, one user" framing throughout PROJECT.md/REQUIREMENTS.md's Out of Scope table.
- CORE-05 verification should include an explicit "kill the process while in rig mode, relaunch, confirm mode label + toggle button read correctly" checkpoint — same "Linux sandbox can't run Windows code" execution boundary noted in every prior phase (Phases 1, 2, 4) applies here too; this is gated on the user testing on the actual rig PC.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. No scope-creep items came up (no hotkey/tray/notification/logging suggestions raised during this discussion; those remain correctly deferred to v2 per REQUIREMENTS.md).

</deferred>

---

*Phase: 5-Orchestration-Full-Toggle-Packaging*
*Context gathered: 2026-07-24*
