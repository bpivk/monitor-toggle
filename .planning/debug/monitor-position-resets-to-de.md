---
status: awaiting_human_verify
trigger: "Monitor position resets to default when a monitor is disabled and then re-enabled via Rig Toggle"
created: 2026-08-23T07:12:36Z
updated: 2026-08-28T09:27:47Z
---

## Symptoms

### Symptom 1: Monitor position resets to default
- **Expected:** A monitor's position, as configured in Windows Display Settings, is preserved when it is disabled and then re-enabled through Rig Toggle.
- **Actual:** The monitor's position resets to its default position after being disabled and re-enabled. Happens silently, no error/warning surfaced anywhere in the app.
- **Reproduction:** Disable a monitor (via either the dashboard monitor tile or the Rig/Normal mode toggle) and re-enable it (either control). User confirmed it happens via both controls.
- **Timeline:** Started recently (not "always happened") — user is unsure of the exact trigger commit but reports it as a regression, not a longstanding issue.
- **Severity:** major (user confirmed) — always needs re-setting up after the monitor wakes.
- **Status: RESOLVED** — rig-confirmed fixed by the position-mode cache described in Resolution below. Not re-litigated this round.

### Symptom 2: Rig mode switch partially fails / silently reverts
- **Expected:** Switching to Rig mode (via the Rig/Normal toggle) completes normally regardless of whether a monitor is already disabled going in, and the resulting layout stays correct (not just immediately after the toggle, but durably).
- **Actual:** Evolved across three rig tests as investigation progressed:
  - Round 1 repro: "Partial toggle" when one monitor already disabled going in — some steps happen but the monitor part doesn't complete correctly.
  - Round 2 repro (after the isPartOfMonitorSwap->Extend fix): toggle APPEARS to succeed (app's own verify passes) then silently reverts to the pre-toggle topology ~2-3 seconds later with zero app involvement; reverse direction instead threw PathChangeException outright.
  - Round 3 repro (same fix, from the original "one monitor already independently disabled" starting state): Extend never activated the requested target at all, while additionally reactivating the independently-disabled, unrequested monitor.
  - Round 3 (third rig test, on the SIMPLE single-tile enable path, previously believed unaffected): the SAME class of delayed silent revert (~2.15s) reproduced on scoped ApplyPathInfos too — proving the revert is not specific to which CCD mechanism is used.
- **No errors surfaced** in the revert cases — silent.
- **Status:** Two independent fixes now implemented and self-verified this round (see Resolution). Awaiting rig verification.

### Suspected relationship
Three distinct, now-confirmed root causes, all downstream of the round-5 scoped-activation path's introduction and its round-2/round-3 mitigations: (1) missing position/mode data (Symptom 1, resolved), (2) ApplyTopology(Extend)'s structural inability to express an explicit desired active-path-set (Symptom 2, rounds 2-3), (3) a delayed drift-back-to-previous-state that both CCD mechanisms are equally subject to, previously only ever corrected by the app's reactive watchdog in ONE direction and only on the tile path (Symptom 2, round 3 part 2).

## Current Focus

reasoning_checkpoint:
  hypothesis: "Two independent, additive fixes are both required and both now implemented. (A) ApplyTopology(Extend) is structurally incapable of reliably enacting a specific desired active-monitor set -- confirmed by decompiling WindowsDisplayAPI 1.3.0.13's PathInfo.ApplyTopology, which supplies ZERO explicit paths to SetDisplayConfig -- so the full Rig/Normal swap case must go back to explicit, scoped ApplyPathInfos (round 5's mechanism), made swap-aware by excluding the caller's disable-set from the survivors it preserves, so ONE call both activates the new target(s) and implicitly deactivates the disable-set with no transient N-active state. (B) Independently, a THIRD rig test proved the delayed silent revert (~2-3s after an apparently-successful, verified apply) is not specific to Extend OR to the swap shape -- it also happens on the already-scoped, already-swap-free single-tile enable path. Since the existing reactive watchdog (TryReactivelyCorrectAgainstLastIntent) was, by its own explicit prior design, ONLY able to correct 'unexpectedly active' drift (never 'unexpectedly inactive'), and was never armed at all on the Rig/Normal toggle path (only on tile actions), it structurally could not have caught either the round-2 swap-path revert or the round-3 tile-path revert. Making the watchdog bidirectional and arming it on the toggle path too (in addition to fix A) directly targets the newly-confirmed, mechanism-agnostic symptom without needing to first prove or fix a genuine OS/driver-level root cause, which no code-level tool in this sandbox can access."
  confirming_evidence:
    - "Decompiled WindowsDisplayAPI 1.3.0.13 (ilspycmd) -- PathInfo.ApplyTopology's entire body is `DisplayConfigApi.SetDisplayConfig(0u, null, 0u, null, ...)`; PathInfo.ApplyPathInfos builds and passes real, non-null DisplayConfigPathInfo[]/DisplayConfigModeInfo[] arrays. Direct code read, not inference -- only ApplyPathInfos can express explicit path membership."
    - "Round 3 rig log (already in file): Extend's settle-poll never observed the requested target (SAM748A) active at any point, while an unrelated, independently-disabled monitor (DELA0B8) came back active anyway -- matches 'Windows' own opaque algorithm decides the topology' exactly, not a partial/timing-window failure the correction loop could catch."
    - "Third rig test (already in file, now folded into Symptom 2's Evidence below): round-5 scoped ApplyPathInfos succeeded (finalActive includes SAM748A, verified), then ~2.15s later RefreshMonitorTiles observed SAM748A inactive again -- OS-level revert, no app-initiated call in between, no exception, FASTER than round 2's ~3.2s swap-path revert but the same class of event, and NOT on the swap/Extend path this round's fix A targets."
    - "Direct code read of TryReactivelyCorrectAgainstLastIntent (pre-fix): its ONLY corrective branch was `monitor.IsActive && !shouldBeActive` -> DeactivateMonitors; its own doc comment stated 'never the reverse direction... nothing here re-enables anything' -- structurally incapable of correcting the newly-confirmed failure direction (active -> inactive) even when armed."
    - "Direct code read of ToggleSwitch_ActionRequested/PerformBackgroundToggle (pre-fix): both call RefreshUi() (mode text/tray icon only) after a toggle, never RefreshMonitorTiles() or ArmIntentGuard() -- confirmed already in round 2's Evidence and left as an explicit deferred blind spot pending 'if any residual revert survives this fix's rig verification,' which the third rig test is exactly that trigger condition for."
  falsification_test: "If a rig trial from the round-3 repro starting state (DELA0B8 independently pre-disabled, ACI24A4 active) still fails to activate SAM748A or still reactivates DELA0B8, fix A is refuted. Separately, if a rig trial watching the layout for 10+ seconds after ANY monitor action (tile OR toggle, either direction) still shows a monitor that should be active silently going inactive with debug.log showing NO 'TryReactivelyCorrectAgainstLastIntent... wentInactive' correction attempt at all (guard never armed, or armed but budget/window already exhausted, or the drift wasn't detected), fix B is refuted -- the true mechanism is deeper than this app's watchdog can reach (e.g. Windows re-applies a persisted topology on some driver-level trigger this reactive, event-driven design cannot preempt) and a different structural approach (e.g. periodic proactive re-verification, not just event-reactive) would be needed."
  fix_rationale: "Fix A restores explicit path-membership control (which Extend structurally cannot offer) as the primary mechanism for every ActivateMonitors call, swap or not -- a no-op (byte-identical plan) for the already-rig-validated non-swap case. Fix B extends an already-existing, already rig-proven-for-one-direction mechanism (the intent-guard watchdog) to be symmetric and to cover the toggle path too, rather than inventing a new mechanism -- reusing the same bounded-window/bounded-budget safety design, the same ActivateMonitors/DeactivateMonitors primitives, and the same 'never touch a device path absent from intent' safety boundary already established and rig-validated for the other direction. Neither fix touches or weakens the position-mode cache, ComputeUnexpectedlyActivated, DeactivateMonitors' own verify-and-throw, or the settle-poll-then-correct loop inside ActivateMonitors."
  blind_spots:
    - "Cannot execute or verify on real Windows CCD hardware in this sandbox -- same structural limitation as every prior round in this file."
    - "Fix B is reactive (event-driven off OnDisplaySettingsChanged), not proactive/periodic -- if the OS-level revert ever fires without a corresponding DisplaySettingsChanged notification (unobserved in any rig log so far, but not provably impossible), fix B would never see it. The handoff note's alternative direction (a periodic re-verify+reapply timer) was considered and deliberately not chosen this round, since every rig log so far DOES show OnDisplaySettingsChanged firing for the revert -- inventing a poll-based watchdog without evidence it's needed would be scope creep beyond the confirmed mechanism."
    - "TryBuildScopedActivationPlan's mutual-source-compatibility gap for multiple simultaneous new targets (round 2) is mitigated (excluding the disable-set frees its source(s)) but not structurally eliminated -- a hardware configuration with less source headroom could still hit it."
    - "MaxReactiveCorrections (2) and IntentGuardWindow (8s) are unchanged from round 3's original tuning -- now protecting against a second failure direction and a second call path (toggle) with the SAME budget. Not increased this round for lack of rig evidence that 2 corrections / 8 seconds is insufficient once the fix is bidirectional; a rig trial showing budget exhaustion (SKIP -- correction budget exhausted logged while drift is still present) would be the falsifying signal to revisit this."
  candidate_causes:
    - "code (fix A target): WindowsMonitorController.ActivateMonitors' round-2 fix routed the full-swap case to ApplyTopology(Extend), which (confirmed by decompile) supplies no explicit path array at all."
    - "code (fix B target): MainForm's reactive watchdog was one-directional and never armed on the toggle path -- a distinct code gap from fix A's, in a different file/layer, independently sufficient to explain why NEITHER prior fix caught the revert once it was confirmed to also occur outside the swap/Extend path."
    - "environment/hardware: some genuine OS/driver-level mechanism causes an applied CCD topology to be undone a few seconds later, independent of which API applied it -- the EXTERNAL trigger both code-level gaps above left unguarded against. Not eliminated or explained by either fix; both fixes correct FOR its effect without needing to know its cause."
  and_gate: "yes for the full symptom picture (Symptom 2 required two independent code-level fixes, each alone insufficient) but no within each fix's own decision: fix A is justified purely by the decompile-confirmed Extend defect regardless of the watchdog question, and fix B is justified purely by the watchdog's confirmed one-directional/toggle-path-unarmed gaps regardless of which CCD mechanism is in play. Both were implemented because the rig evidence independently confirmed both gaps are real, not because either alone was assumed insufficient."

next_action: "Both fixes implemented and self-verified (build + full adjacent test suite: 209/209 RigToggle.Tests pass unchanged; RigToggle.Windows.Tests still cannot execute in this sandbox, pre-existing limitation). Awaiting rig verification via the human-verify checkpoint below -- this time explicitly requesting a longer observation window (10+ seconds) after EVERY monitor action tried, not just the toggle, since round 3's own history is that fixes which looked complete after a short observation window were repeatedly proven incomplete by watching longer."

## Evidence

- timestamp: 2026-08-23T00:00:00Z
  checked: .planning/debug/resolved/monitor-enable-reactivates-others-again.md (prior resolved session, same file/method) via knowledge-base semantic match
  found: Round 5 of that session (rig-confirmed 2026-08-22) introduced TryBuildScopedActivationPlan inside ActivateMonitors, which builds the newly-activated target's PathInfo via the no-mode-info constructor specifically so the driver's best-mode-logic fills in mode data instead of this code guessing -- explicitly documented as intentional, to avoid the three previously-failed manual-reconstruction attempts (ccd-topology-restore-findings).
  implication: This is very likely the introducing change for the current bug -- it is the only recent change to this method's activation code path, it was rig-confirmed to fix a different, real bug (the reactivation flash), and its own doc comments already flag that it does not supply position data, which is exactly today's symptom.

- timestamp: 2026-08-23T00:05:00Z
  checked: src/RigToggle.Windows/WindowsMonitorController.cs, full read (ActivateMonitors, TryBuildScopedActivationPlan, DeactivateMonitors)
  found: TryBuildScopedActivationPlan's newPaths.Add used a mode-less PathInfo constructor -- confirmed no Position/Resolution/PixelFormat argument. DeactivateMonitors, by contrast, already captures and reuses REAL live Position/Resolution/PixelFormat when repositioning survivors -- confirming a mode-carrying PathInfo constructor overload exists and is already used safely elsewhere in this exact file.
  implication: The fix is a small, targeted change reusing an already-proven constructor overload and an already-KB-endorsed technique (capture mode data while the path is live, ccd-topology-restore-findings finding 5b) -- not new territory.

- timestamp: 2026-08-23T00:08:00Z
  checked: src/RigToggle.Core/ToggleService.cs (ToggleToRigMode/ToggleToNormalMode Monitor steps) and src/RigToggle.App/Program.cs (composition root)
  found: Both toggle directions call ActivateMonitors immediately followed by DeactivateMonitors inside one step (Pitfall 2 ordering). WindowsMonitorController is constructed exactly once at the composition root and shared by both MainForm's tile actions and ToggleService's toggle flow.
  implication: A single instance-level cache addresses both reported repro paths without needing cross-process persistence.

- timestamp: 2026-08-23T08:30:00Z
  checked: Post-fix rig trial of the position-mode cache fix (Symptom 1) plus a Rig-mode toggle trial (Symptom 2), user-provided debug.log excerpt
  found: Symptom 1 CONFIRMED FIXED on real hardware. Symptom 2 still broken, but with DIFFERENT log signatures than the original shared-cause hypothesis predicted.
  implication: The original "same root cause, two call sites" hypothesis is PARTIALLY REFUTED -- correct for Symptom 1, wrong for Symptom 2 (see Eliminated).

- timestamp: 2026-08-23T08:31:00Z
  checked: debug.log lines around 09:29:42.641-09:29:47.008 (Rig-mode toggle trial, forward direction, round-5 scoped path still in use)
  found: ActivateMonitors(SAM748A)+DeactivateMonitors(ACI24A4,DELA0B8) both completed with DeactivateMonitors' own verify-and-throw logging a clean success. ~3.2s later, with no intervening app-initiated call, OnDisplaySettingsChanged fired and RefreshMonitorTiles observed the pre-toggle topology restored.
  implication: Not a missing-position-data problem -- a durability problem. Also confirmed the toggle-switch code path never calls ArmIntentGuard, so the reactive watchdog had no intent baseline to even attempt a correction.

- timestamp: 2026-08-23T08:32:00Z
  checked: debug.log line around 09:30:06.685 (Normal-mode toggle trial, reverse direction, round-5 scoped path)
  found: The scoped ApplyPathInfos call threw PathChangeException("Invalid paths information.") outright and fell back to ApplyTopology(Extend), which then succeeded.
  implication: The reverse direction fails a different way (hard validation failure vs. silent revert) but from the same scoped-activation call.

- timestamp: 2026-08-23T09:15:00Z
  checked: ToggleService.cs Monitor steps and TryBuildScopedActivationPlan re-read against the "full swap" failure mode
  found: TryBuildScopedActivationPlan's plan was `currentActivePaths.Concat(newPaths)` -- unconditionally includes every already-active survivor PLUS the new target(s), so the forward direction transiently held 3 simultaneously-active paths, and the reverse direction's greedy per-target source selection had no mutual-compatibility check across 2 new targets.
  implication: Sufficient, code-level explanation for both round-2 failure modes -- led to the isPartOfMonitorSwap->Extend fix (later itself superseded, see below).

- timestamp: 2026-08-23T09:25:00Z
  checked: src/RigToggle.App/MainForm.cs -- ToggleSwitch_ActionRequested, RefreshUi(), ArmIntentGuard()/TryReactivelyCorrectAgainstLastIntent() (pre-fix)
  found: ToggleSwitch_ActionRequested called RefreshUi() (mode text/tray icon only) after the toggle, never RefreshMonitorTiles() or ArmIntentGuard(). ArmIntentGuard() was only called from OnTileAction.
  implication: Confirmed a genuine secondary gap (the watchdog has no intent baseline after a toggle-driven change) -- deliberately deferred at the time in favor of fixing the scoped-activation path directly; explicitly flagged for revisit if a residual revert survived. It did (see round 3 below), triggering that revisit.

- timestamp: 2026-08-23T10:05:00Z
  checked: Third rig test (round 3, first trial) -- round-2 fix (isPartOfMonitorSwap->Extend) tested from the ORIGINAL Symptom 2 repro starting state (DELA0B8 already independently disabled, only ACI24A4 active going in)
  found: "isPartOfMonitorSwap=true -- skipping scoped activation" logged correctly. ActivateMonitors(SAM748A) called Extend; the settle-poll immediately after NEVER observed SAM748A active at any attempt, while DELA0B8 (independently pre-disabled, unrequested) came back active. The correction loop's DeactivateMonitors(DELA0B8) succeeded, but SAM748A never became active anywhere in the sequence -- final verify-and-throw correctly threw, D-05 fail-safe preserved (no partial state), but the toggle's goal was not accomplished.
  implication: Round 2's fix does not work. Extend fails in a NEW way beyond what round 5's scoped path was built to prevent.

- timestamp: 2026-08-23T10:20:00Z
  checked: Decompiled WindowsDisplayAPI 1.3.0.13's PathInfo class via ilspycmd (only the compiled NuGet DLL is available in this sandbox, not vendored source) -- ApplyTopology, ApplyPathInfos, GetDisplayConfigPathInfos
  found: ApplyTopology's entire body is `DisplayConfigApi.SetDisplayConfig(0u, null, 0u, null, SetDisplayConfigFlags.Apply | topology-flags)` -- path/mode arrays are literally `0u, null, 0u, null`. ApplyPathInfos, by contrast, builds real DisplayConfigPathInfo[]/DisplayConfigModeInfo[] arrays from the caller's supplied PathInfo objects and passes those non-null arrays with SDC_USE_SUPPLIED_DISPLAY_CONFIG/SDC_TOPOLOGY_SUPPLIED.
  implication: Definitive, direct-code-read confirmation that ApplyTopology(Extend) structurally CANNOT be given an explicit desired active-path-set. Only ApplyPathInfos-based scoped activation, made swap-aware, is structurally capable of the required guarantee.

- timestamp: 2026-08-23T10:35:00Z
  checked: DeactivateMonitors' existing, already rig-confirmed ApplyPathInfos usage -- how excluding a path from the supplied array deactivates it with no separate call
  found: DeactivateMonitors supplies only `pathsToApply` (survivors, excluding the paths being disabled); the excluded targets go inactive as a side effect of ApplyPathInfos' "supplied array replaces the whole active topology" semantics (confirmed by the decompile above). Already rig-proven mechanism.
  implication: ActivateMonitors' own scoped plan can reuse the identical mechanism for swap-exclusion -- not a new, unproven CCD behavior.

- timestamp: 2026-08-23T10:45:00Z
  checked: Third rig test (round 3, SECOND trial, single-tile enable path -- previously believed unaffected and confirmed working in round 1)
  found: CRITICAL -- the silent-revert pattern reproduced on the SIMPLE single-tile ENABLE path (monitorSwapDisableSet empty, round-5 scoped ApplyPathInfos in use, not Extend). Scoped activation succeeded (finalActive included the target, verified), then ~2.15s later RefreshMonitorTiles observed the target inactive again -- OS-level silent revert, no app-initiated call in between, no exception. Faster than round 2's ~3.2s swap-path revert but the same class of event.
  implication: The root instability is NOT "which CCD API call is used" -- both Extend and scoped ApplyPathInfos exhibit a delayed silent revert. Strongly suggests a genuine Windows/display-driver-level behavior: an apply that appears to succeed (verified via the existing settle-poll) is not durable and gets undone 2-3+ seconds later, outside the app's synchronous control window, on BOTH the swap and non-swap paths.

- timestamp: 2026-08-23T10:55:00Z
  checked: src/RigToggle.App/MainForm.cs -- TryReactivelyCorrectAgainstLastIntent (pre-fix, full body) and OnTileAction's enable branch, re-read specifically to test whether the watchdog itself could be self-inflicting the revert
  found: TryReactivelyCorrectAgainstLastIntent's only corrective branch was `monitor.IsActive && !shouldBeActive -> DeactivateMonitors(...)`, with its own doc comment stating "never the reverse direction... nothing here re-enables anything." For the third-rig-test scenario (target activated, intent recorded as should-be-active, then observed inactive), this exact code path structurally cannot fire -- shouldBeActive is true, so the `!shouldBeActive` condition is false. Confirmed the watchdog is NOT self-inflicting the revert (ruled out as a candidate cause) but is ALSO not correcting it -- a real, independent gap.
  implication: Eliminated "the watchdog itself causes the revert" as a hypothesis. Confirmed "the watchdog cannot correct this direction of drift, and was never armed on the toggle path at all" as the two-part fix-B target -- a code-level gap fully explaining why the app never caught or corrected either round-2's or round-3's revert, independent of whatever OS/driver-level mechanism actually causes the underlying drift.

- timestamp: 2026-08-23T11:00:00Z
  checked: Implemented and built both fixes (see files_changed in Resolution) -- Fix A: TryBuildScopedActivationPlan gains a monitorSwapDisableSet parameter; ActivateMonitors always attempts scoped activation (isPartOfMonitorSwap-triggers-Extend-bypass branch removed) and caches live mode for swap-excluded survivors via a new shared CacheLiveModes helper before excluding them. Fix B: TryReactivelyCorrectAgainstLastIntent made bidirectional (adds a wentInactive branch calling ActivateMonitors, symmetric with the existing reactivated/DeactivateMonitors branch); ToggleSwitch_ActionRequested and PerformBackgroundToggle now call RefreshMonitorTiles()+ArmIntentGuard() after a toggle, matching OnTileAction's existing pattern.
  found: `dotnet build RigToggle.sln --no-incremental` succeeds (0 warnings, 0 errors, all 6 projects). `dotnet test src/RigToggle.Tests` -- 209/209 passed, unchanged, including all 4 existing isPartOfMonitorSwap routing tests (log-line format deliberately preserved in FakeMonitorController). `dotnet test src/RigToggle.Windows.Tests` still cannot execute in this sandbox (missing Microsoft.WindowsDesktop.App) -- pre-existing, unrelated limitation. MainForm.cs (fix B) has no dedicated automated test coverage in this repo (WinForms UI layer, matching this codebase's existing test-layer boundaries) -- self-verified via build + code read only, same constraint as every prior MainForm-touching round in this file.
  implication: Both fixes compile cleanly and do not regress any existing automated coverage. Whether either fix durably holds on real hardware -- especially fix B's ability to actually observe and correct the revert within its 8s/2-correction budget -- is deferred to the mandatory human-verify checkpoint below.

## Eliminated

- hypothesis: "Symptom 2 shares Symptom 1's exact root cause (scoped activation's missing Position/Resolution/PixelFormat data), so the position-mode cache fix alone would resolve both symptoms via the same code path."
  evidence: "Post-fix rig trial (2026-08-23T08:30:00Z): Symptom 1 confirmed fixed, but Symptom 2 reproduced again with a debug.log signature that does not match a missing-mode-data mechanism at all."
  timestamp: 2026-08-23T08:30:00Z

- hypothesis: "ApplyTopology(Extend) is rig-proven reliable for the full Rig/Normal swap shape (rounds 1-4's own history), so routing the swap case to it exclusively (round 2's fix) is sufficient."
  evidence: "Round 3 rig test (2026-08-23T10:05:00Z): from the original Symptom-2 repro starting state (one monitor already independently disabled), Extend failed to activate the requested target at all while also reactivating the independently-disabled, unrequested monitor. Rounds 1-4's apparent reliability only held for the specific repro shapes those rounds happened to test."
  timestamp: 2026-08-23T10:05:00Z

- hypothesis: "MainForm's reactive watchdog (TryReactivelyCorrectAgainstLastIntent) is itself causing the delayed revert (self-inflicted correction bug)."
  evidence: "Direct code read (2026-08-23T10:55:00Z): the watchdog's only corrective branch requires `!shouldBeActive` (intent says should be inactive); the third-rig-test revert scenario has `shouldBeActive == true` (target was deliberately activated), so this branch structurally cannot fire. The watchdog was confirmed unable to touch this case in either direction -- neither causing it nor (pre-fix) correcting it."
  timestamp: 2026-08-23T10:55:00Z

## Resolution

root_cause: "Three distinct, independently-confirmed root causes:
(1) Symptom 1: TryBuildScopedActivationPlan originally constructed the newly-reactivated target's PathInfo via a mode-less constructor, letting the driver's best-mode-logic pick a default instead of the monitor's own prior position. RESOLVED (position-mode cache), rig-confirmed, not touched this round.
(2) Symptom 2, part A: ApplyTopology(Extend) -- the mechanism round 2's fix routed the full swap case to -- is structurally incapable of expressing an explicit desired active-path-set (confirmed by decompiling WindowsDisplayAPI 1.3.0.13: it supplies zero path/mode arrays to SetDisplayConfig). Windows' own opaque internal topology-selection algorithm decides the result, with no guarantee it includes the caller's specific requested target or excludes a monitor the caller independently, deliberately left disabled -- confirmed on real hardware (round 3): Extend simultaneously failed to activate the requested target AND reactivated an unrelated, independently-disabled one.
(3) Symptom 2, part B: independent of (2), a delayed (~2-3s) silent revert of an apparently-successful, verified CCD apply back to the prior topology occurs on BOTH ApplyTopology(Extend) and scoped ApplyPathInfos, and on BOTH the swap (toggle) and non-swap (tile) call shapes -- confirmed by a third rig test reproducing it on the simple single-tile enable path, which round 1 had previously reported as working. The app's existing reactive watchdog (MainForm.TryReactivelyCorrectAgainstLastIntent) was structurally incapable of correcting this direction of drift (its only branch handled 'unexpectedly active', never 'unexpectedly inactive', per its own prior doc comment) and was never armed at all on the Rig/Normal toggle path (only on tile actions) -- so even where the watchdog's underlying architecture (bounded-window, event-driven correction off OnDisplaySettingsChanged) was capable of catching this class of revert, it never had the coverage to actually do so for either the swap-path revert (round 2) or the tile-path revert (round 3). The genuine external trigger for why Windows/the driver undoes the apply a few seconds later remains unknown and is not resolved by this fix -- the fix corrects for its effect without depending on knowing its cause."
fix: "Fix A -- Symptom 2 part 2 (src/RigToggle.Core/Abstractions/IMonitorController.cs, src/RigToggle.Windows/WindowsMonitorController.cs, src/RigToggle.Core/ToggleService.cs, src/RigToggle.App/MainForm.cs, plus test doubles): ActivateMonitors' `bool isPartOfMonitorSwap` parameter is replaced with `IReadOnlySet<string> monitorSwapDisableSet` (the actual disable-set, or empty). TryBuildScopedActivationPlan now excludes monitorSwapDisableSet's device paths from the survivors it preserves (both from claimedSources and the final plan) -- so ONE scoped ApplyPathInfos call both activates the new target(s) and implicitly deactivates the swap's disable-set (reusing the exact 'excluded from supplied array => goes inactive' mechanism DeactivateMonitors already relies on), going directly from the pre-toggle topology to the exact post-toggle topology with no transient N-active state. The isPartOfMonitorSwap-triggers-Extend-bypass branch is removed entirely -- scoped activation is now always attempted first, for both swap and non-swap calls (a no-op/byte-identical plan when the exclusion set is empty). Because swap-excluded survivors may now go inactive without ever reaching DeactivateMonitors' own capture-before-removal code, ActivateMonitors itself caches their live mode first via a new shared CacheLiveModes helper (extracted from DeactivateMonitors' existing capture loop) -- preserving Symptom 1's fix for the swap case too. Extend remains only as a last-resort fallback when a scoped plan cannot even be constructed, documented as now-known-unreliable for correctly targeting a specific monitor.
Fix B -- Symptom 2 part 3 (src/RigToggle.App/MainForm.cs only): TryReactivelyCorrectAgainstLastIntent is made bidirectional -- alongside its existing 'active but should be inactive -> DeactivateMonitors' correction, it now also detects 'inactive but should be active -> ActivateMonitors' and corrects that too, sharing the same lease acquisition, the same bounded correction budget, and the same 'never touch a device path absent from intent' safety boundary as the existing direction. ToggleSwitch_ActionRequested and PerformBackgroundToggle (the Rig/Normal toggle's two UI entry points) now call RefreshMonitorTiles()+ArmIntentGuard() after a toggle attempt, matching OnTileAction's existing pattern -- closing the previously-deferred gap where the toggle path never armed the watchdog at all. Does not change IntentGuardWindow (8s) or MaxReactiveCorrections (2), does not touch ComputeUnexpectedlyActivated, the settle-poll-then-correct loop inside ActivateMonitors, or DeactivateMonitors' own verify-and-throw."
verification:
  target_test: { result: partial, reason: "Symptom 1: previously rig-verified (unchanged this round). Symptom 2: no automated test coverage is possible for either fix without live Windows CCD hardware (WindowsMonitorController.ActivateMonitors/TryBuildScopedActivationPlan call static native CCD APIs with no injectable seam, per this file's and WindowsMonitorControllerTests.cs's own established constraint; MainForm.cs is WinForms UI layer with no existing test project coverage in this repo). Both fixes are self-verified via full-solution build plus the existing 209-test RigToggle.Tests suite (confirms no regression to ToggleService's routing/ordering contracts) and are deferred to the mandatory human-verify checkpoint for the actual hardware-dependent behavior." }
  mutation_check: { result: skipped, reason: "No Stryker configured in this repo (matches this file's own prior debug sessions)." }
  no_op_deletion: { result: pass, deletion_justified_by_rca: true, note: "Fix A: TryBuildScopedActivationPlan gains one new exclusion parameter that is a no-op for every pre-existing non-swap call site (empty set -> unchanged survivorsToKeep -> byte-identical plan); the isPartOfMonitorSwap Extend-bypass branch is removed (RCA-justified: round 3 proved that branch itself produces wrong behavior, not just redundant code) and replaced with unconditional scoped-activation attempt, which is what round 5 always intended before round 2's regression. Fix B: purely additive (new wentInactive branch alongside the unchanged reactivated branch; two new RefreshMonitorTiles()+ArmIntentGuard() call sites) -- no existing branch, guard, exception, log line, or assertion was removed or weakened in either fix." }
  adjacent_tests: { result: pass, suites_run: ["dotnet build RigToggle.sln --no-incremental (0 warnings, 0 errors, all 6 projects)", "dotnet test src/RigToggle.Tests (209/209 passed, unchanged from before this round)"], note: "dotnet test src/RigToggle.Windows.Tests still cannot execute in this sandbox (missing Microsoft.WindowsDesktop.App) -- pre-existing sandbox limitation, unrelated to this fix, matching every prior session in this file." }
  revert_and_reconfirm: { result: deferred, reason: "Requires a live rig trial to establish the 'before' state (target not activated / unrelated reactivation / delayed revert) to compare against an 'after' trial (durable, correct, stays correct for 10+ seconds) -- cannot be executed in this sandbox. Deferred to the mandatory human-verify checkpoint." }
  guardrail_verdict: accepted
  guardrail_note: "All self-executable signals (no-op/deletion review for both fixes, full-solution build, full adjacent test suite) pass cleanly. Signals requiring real Windows CCD hardware are explicitly deferred -- not silently skipped -- to the mandatory human-verify checkpoint, matching the identical, already-established degradation path every prior debug session touching this exact file has used. Fix A is structured so every pre-existing non-swap call site is provably unaffected (empty-set no-op). Fix B is structured so its existing, already rig-validated 'unexpectedly active' correction direction is completely unchanged -- only a new, independent 'unexpectedly inactive' branch and two new call sites are added."
files_changed:
  - src/RigToggle.Windows/WindowsMonitorController.cs
  - src/RigToggle.Core/Abstractions/IMonitorController.cs
  - src/RigToggle.Core/ToggleService.cs
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.Tests/Doubles/FakeControllers.cs
  - src/RigToggle.Tests/Doubles/BlockingMonitorController.cs

## Checkpoint history

- Round 1 checkpoint (position-mode cache + isPartOfMonitorSwap->Extend): Symptom 1 rig-confirmed fixed. Symptom 2 rig-tested and found still broken (see round 2/3 evidence above) -- fix insufficient, investigation continued rather than being archived.
- Round 2/3 (this update): two additional, independent fixes implemented per the reasoning_checkpoint above. A prior in-progress note ("Session paused -- handoff note") recommended either widening the settle-poll window or building a persistent re-verify/reapply watchdog as the next direction once the "revert regardless of mechanism" finding emerged -- the bidirectional-and-toggle-path-armed intent guard (fix B) is that persistent watchdog, built by extending the mechanism this codebase already had (event-driven, bounded-budget) rather than adding a new polling timer, since every rig log available shows the revert IS accompanied by an OnDisplaySettingsChanged notification the app can react to.

## CHECKPOINT REACHED

**Type:** human-verify
**Debug Session:** .planning/debug/monitor-position-resets-to-de.md

### Investigation State

**Root causes confirmed this round:**
1. ApplyTopology(Extend) cannot express an explicit desired active-path-set (decompile-confirmed) -- fixed by making scoped ApplyPathInfos swap-aware and using it unconditionally.
2. The reactive watchdog was one-directional and never armed on the toggle path -- fixed by making it bidirectional and arming it on both the tile AND toggle paths.

### Checkpoint Details

**Need verification:** confirm both fixes hold on real rig hardware, across BOTH the previously-broken scenarios AND a longer observation window than prior rounds used (this round's own history is that a short observation window repeatedly missed a real revert).

**Self-verified checks:**
- Full solution build: 0 warnings, 0 errors.
- Full RigToggle.Tests suite: 209/209 passed, no regressions to any existing routing/ordering test.
- Code-level review confirms both fixes are additive/no-op-safe for every pre-existing call site.

**How to check (please watch each step for at least 10 seconds after it completes, not just immediately after):**
1. From the everyday desktop configuration, flip the Rig/Normal toggle to Rig mode. Confirm the rig monitor becomes active and stays active for 10+ seconds.
2. Flip back to Normal mode. Confirm both desktop monitors become active and stay active for 10+ seconds, with no PathChangeException/Extend-fallback needed in debug.log.
3. Reproduce the ORIGINAL Symptom 2 starting condition: independently disable one desktop monitor via its tile first, THEN flip to Rig mode. Confirm the rig monitor activates (not the independently-disabled monitor) and stays active for 10+ seconds.
4. Enable a single monitor via its dashboard tile (no toggle involved). Confirm it activates and stays active for 10+ seconds -- this is the third-rig-test repro shape.
5. Quick regression check: disable a monitor via its tile, re-enable it via the tile, confirm its position is preserved (Symptom 1, should be unaffected).

**Tell me:** "confirmed fixed" (all 5 steps held for 10+ seconds each) OR describe exactly which step still fails and what debug.log shows around that time (particularly whether a `TryReactivelyCorrectAgainstLastIntent... wentInactive` line appears if something goes inactive unexpectedly).

## New Evidence (2026-08-28, fourth rig test)

Round 3's fix has its own failure mode: excluding the swap's disable-set entirely from the scoped plan's path array produces an INVALID array (single-path, target-only) that the CCD API rejects outright with PathChangeException: Invalid paths information -- reproduced at app startup (a mode-restore ActivateMonitors(SAM748A, disableSet=[ACI24A4,DELA0B8]) call, before any user action). Falls back to Extend, which is imprecise (established in round 2/3 evidence), so the whole startup mode-restore failed with "Monitor enable did not take effect."

Pattern across rounds: round 1 (scoped plan includes ALL survivors + target -- too many paths, transient over-full topology, unreliable/reverts or PathChangeException on multi-target case) -> round 3 (scoped plan excludes disable-set entirely -- too few paths for single-target case, PathChangeException on invalid array shape) -> Extend fallback in both cases is imprecise (can reactivate unrelated already-disabled monitors, confirmed via decompilation it passes zero paths/modes to SetDisplayConfig).

Also observed (11:25:40.189-40.690): the bidirectional TryReactivelyCorrectAgainstLastIntent guard (round 3's second fix) DOES now fire on a wentInactive drift (this is new, correct behavior -- previously silent), but its own correction attempt then hits the SAME two path-array problems recursively (DeactivateMonitors: "every currently active display is in the requested set, none would survive" on one attempt, then ActivateMonitors failing via the same Extend-imprecision on the retry) -- so the guard fix is directionally correct but inherits the underlying scoped-plan/Extend path-array bug, it doesn't independently resolve anything.

## Current Focus (round 4)

hypothesis: "Neither 'include all survivors' (round 1) nor 'exclude disable-set entirely' (round 3) produces a valid, precise CCD ApplyPathInfos call. The correct plan likely needs an EXPLICIT full-topology array in ONE ApplyPathInfos call: the target path marked active (with real/cached mode info), the swap's disable-set paths marked EXPLICITLY INACTIVE (not omitted -- IsModeInformationAvailable=false or equivalent 'present but inactive' path state, not absent from the array), and any OTHER already-active survivors (outside the swap) included as active with their cached mode info. This treats the apply call as 'declare the desired state of every known path', which is likely what SetDisplayConfig actually requires for validation to succeed, rather than 'declare only what's changing' (round 3) or 'declare everything that should end up active, implicitly' (round 1)."
next_action: "Resume investigation (fresh session recommended -- this needs careful WindowsDisplayAPI PathInfo/DisplayConfigPathInfo struct-level research on how to represent an EXPLICITLY INACTIVE path within an ApplyPathInfos call, as distinct from 'active with real mode' and 'omitted from array entirely' -- likely requires reading WindowsDisplayAPI's PathInfo class more deeply (TargetsInfo, IsActive-equivalent flags) or falling back to raw DISPLAYCONFIG_PATH_INFO flag manipulation (DISPLAYCONFIG_PATH_ACTIVE) if the wrapper library doesn't expose this cleanly). Rig-test each iteration against BOTH repro shapes: (a) app-startup mode-restore with one monitor already disabled, (b) manual Rig<->Normal toggle mid-session, (c) single-tile enable/disable -- all three must hold for 10+ seconds, not just immediately after."
status: investigating
