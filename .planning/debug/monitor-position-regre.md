---
status: superseded-by-redesign
trigger: "The windows position bug is back"
created: 2026-08-29T00:00:00Z
updated: 2026-08-31T00:00:00Z
---

## SESSION CLOSED (2026-08-31) -- SUPERSEDED BY REDESIGN DECISION -- read this first

**This is NOT `resolved` (nothing new was confirmed-fixed this round) and NOT abandoned (a
considered decision was made and recorded).** It is a deliberate closure: round 21's holistic
architecture review (see "Round 21 -- HOLISTIC ARCHITECTURE ASSESSMENT" below, ~line 2749)
concluded the accumulated fix-upon-fix architecture around the still-unexplained `root_cause (8)`
(why the scoped `ApplyPathInfos` plan intermittently misbehaves for one specific monitor,
SAM7489/Odyssey G5) has become disproportionate to this app's stated core value ("a single
reliable action") -- 15 independently-added mechanisms, four distinct failure shapes, no
convergence across 20+ rounds. The user reviewed that assessment and chose **Option R1:
redesign, not another incremental patch**.

**What this investigation DID accomplish, and is NOT being reverted or judged wrong:** 3
confirmed, rig-evidenced, code-level bugs were found and fixed this session (round 8:
`CacheLiveModes` widening; round 9: `PollUntilStableActiveDevicePaths` exception hardening; round
14/round 20: fix A/fix B and items A/B, all self-verified and guardrail-accepted -- see the
round-by-round `## Resolution (round N addendum)` sections below for the complete, itemized
list). All of that code remains in the codebase, unquestioned by this closure.

**What changed, and why this file is closing here:** per the user's explicit instruction, this
round did **not** implement the redesign inline. Instead it turned round 21's assessment into a
concrete, itemized redesign proposal:

**-> See "## Resolution (round 22 -- concrete redesign proposal; SESSION CLOSED)" near the end of
this file for the full proposal (target architecture, per-mechanism disposition, honest
risk/migration note).**

**No code was changed this round.** This file is intentionally left **in place** at
`.planning/debug/monitor-position-regre.md` (not moved to `resolved/`) -- nothing here was
confirmed fixed end-to-end via human verification, so the `resolved/` convention this project
uses for confirmed fixes does not apply. This file is the handoff artifact for a future
`/gsd-plan-phase` redesign effort, per this repo's own GSD workflow enforcement (CLAUDE.md).

## Symptoms

### Symptom 1: Monitor position wrong immediately after disable/re-enable (regression of resolved bug)
- **Expected:** A monitor's position, as configured in Windows Display Settings, is preserved when it is disabled and then re-enabled through Rig Toggle — same guarantee as `.planning/debug/resolved/monitor-position-resets-to-de.md` (closed 2026-08-28, rig-confirmed).
- **Actual:** The monitor's position resets immediately on re-enable (not a delayed drift this time — user explicitly distinguished "wrong immediately" from "correct then drifts later" and chose immediate). Additionally and newly: monitor identification/numbering has swapped between Windows and the app — the monitor Windows now enumerates as display 3 is shown as display 2 in Rig Toggle's own UI. Silent — no error, dialog, or warning surfaced anywhere in the app, matching the original bug's silence.
- **Reproduction:** Intermittent — does NOT happen every time (user says "only sometimes"). Trigger control (dashboard tile vs. Rig/Normal toggle switch) not yet isolated — needs follow-up.
- **Timeline:** User attributes the regression to a recent Rig Toggle app update (not a Windows update or GPU driver update) — i.e. suspects a code change in this repo, not an external environment change. Needs correlation against git history/CHANGELOG for what shipped since 2026-08-28 (the resolved-bug close date).
- **Severity:** major (recurrence of a previously major, user-confirmed bug), compounded by the new numbering-swap symptom which is a NEW observation not present in the original 8-round investigation.

### Suspected relationship to prior resolved session
This may be:
(a) A regression of Symptom 1 from the resolved session (position/mode-data cache stopped being applied or got bypassed by a later change), possibly the SAME position-mode-cache code path breaking again, or
(b) A new, distinct defect where the monitor DEVICE PATH IDENTITY mapping (which this whole subsystem keys off of — see resolved file's extensive use of device paths like ACI24A4/DELA0B8/SAM748A) has become unstable/reordered, causing the app to apply cached position/settings data for the WRONG physical monitor — which would explain both "position is wrong" (right position, wrong monitor) AND "numbering switched" (Windows' own display-number assignment for a device path changed) in one root cause.
The "only sometimes" intermittency and "wrong immediately" (not delayed) both point away from Symptom 2 (delayed revert, closed as an accepted OS-level mitigation, not a fix) and toward either (a) or (b) above — but not yet confirmed by evidence.

## Current Focus

reasoning_checkpoint:
  hypothesis: "The regression is NOT new/broken code in the sense of a bad edit since the resolved session's closure -- git history confirms zero commits have touched WindowsMonitorController.cs, MainForm.cs's monitor logic, ToggleService.cs, or IMonitorController.cs since commit 4777c40 (the resolved session's own round-8 closure commit). Instead, this is a LATENT gap in fix H itself (round 7 of the resolved session, part of that same 4777c40 commit) that was never triggered during the resolved session's own rig verification, and is now firing: ActivateMonitors' correction loop (round 7's ComputeUnexpectedlyDeactivated) nested-reactivates any previously-active survivor that the Extend fallback drops as an unrequested side effect, via `ActivateMonitors(unexpectedlyDeactivated, monitorSwapDisableSet: new HashSet<string>())` (line 610). That nested call goes through TryBuildScopedActivationPlan exactly like a brand-new activation request, consulting `_lastKnownActiveModeByDevicePath` for a cached live mode (Position/Resolution/PixelFormat). But that cache is populated ONLY by (a) DeactivateMonitors, immediately before a DELIBERATE removal, and (b) ActivateMonitors' own CacheLiveModes call, but ONLY for monitorSwapDisableSet-matching survivors (the swap's deliberately-excluded set) -- see the `if (isPartOfMonitorSwap)` block, lines 494-501. A survivor dropped ACCIDENTALLY by the Extend fallback (fix H's whole reason for existing) is in neither category: it was never in monitorSwapDisableSet (fix H explicitly excludes monitorSwapDisableSet-matching paths from unexpectedlyDeactivated, since those are the INTENDED exclusions), and DeactivateMonitors is never called for it (Extend mutates the topology directly with no DeactivateMonitors call involved). So when fix H's nested call reactivates it, no cache entry exists, TryBuildScopedActivationPlan falls to its blank-mode branch (`new PathInfo(candidate.DisplaySource, new[] { targetInfo })`, no Position/Resolution/PixelFormat), and the driver's best-mode-logic picks a default position -- resurrecting Symptom 1 of the resolved session (position resets to a driver default) through a code path Symptom 1's own original fix (round 5's CacheLiveModes/round 3's swap-only extension) never anticipated needing to cover. This explains 'wrong immediately' (the nested reactivation runs synchronously inside the SAME ActivateMonitors call, before it returns -- not a delayed OS-level revert like the resolved session's still-open Symptom 2 part B) and 'only sometimes' (fix H's correction path only fires when the scoped plan happens to throw and fall back to Extend for a specific monitor pairing -- the resolved session's own Resolution.root_cause (8) already documents this specific trigger as genuinely unexplained/intermittent, carried forward unchanged, not re-investigated here). It also plausibly explains the NEW numbering-swap symptom: TryBuildScopedActivationPlan's source-claim logic (`allPaths.FirstOrDefault(p => !claimedSources.Contains(p.DisplaySource) && ...)`) picks the first UNCLAIMED PathDisplaySource for any requested path with no preference for reclaiming the SAME source the target held before -- a survivor reactivated with a driver-default mode AND a possibly-different PathDisplaySource than it had before can land on a different physical GPU output/adapter path, which Windows' own Display Settings numbering (assigned by active-path/source enumeration order, not stable per-monitor identity) can then reassign -- consistent with the user's own account that Windows' number and the app's number no longer agree, while the app's OWN tile numbering (confirmed via direct code read of MainForm.cs line 740: `.OrderBy(m => m.DevicePath, StringComparer.Ordinal)`) is stable and DevicePath-based, unaffected by any of this."
  confirming_evidence:
    - "git log 4777c40..HEAD --name-only: zero commits touch src/RigToggle.Windows/WindowsMonitorController.cs, src/RigToggle.Core/ToggleService.cs, or src/RigToggle.Core/Abstractions/IMonitorController.cs since the resolved session's own round-8 closure commit. MainForm.cs was touched by three unrelated commits (5f3f577, d1598df, 04d765b) -- all About-dialog/update-check-menu changes, confirmed by reading each commit's diff scope (About dialog wiring, MenuStrip Help>About, removing redundant tray/Settings update-check entries) with no touch to RefreshMonitorTiles, ArmIntentGuard, TryReactivelyCorrectAgainstLastIntent, or any monitor-tile-numbering code. Rules out 'a new code change introduced this' -- confirms 'a latent gap in the already-shipped fix H is now being exercised.'"
    - "Direct code read, WindowsMonitorController.cs ActivateMonitors lines 494-501: CacheLiveModes is called ONLY inside `if (isPartOfMonitorSwap)`, and only for `swapExcludedSurvivors` (paths matching monitorSwapDisableSet) -- never unconditionally for every currently-active path."
    - "Direct code read, WindowsMonitorController.cs line 610: the fix-H nested correction call `ActivateMonitors(unexpectedlyDeactivated, monitorSwapDisableSet: new HashSet<string>())` passes an EMPTY disable-set -- so when this nested call's own TryBuildScopedActivationPlan runs, monitorSwapDisableSet is empty and the outer call's isPartOfMonitorSwap-gated CacheLiveModes was never invoked for these specific paths (they were never in monitorSwapDisableSet to begin with, by ComputeUnexpectedlyDeactivated's own definition at line 965: `!monitorSwapDisableSet.Contains(dp)`)."
    - "Direct code read, WindowsMonitorController.cs lines 813-822 (TryBuildScopedActivationPlan): `_lastKnownActiveModeByDevicePath.TryGetValue(devicePath, out cachedMode)` -- if absent, falls to `new PathInfo(candidate.DisplaySource, new[] { targetInfo })`, the exact blank-mode constructor documented (class remarks, lines 47-63) as the ORIGINAL Symptom 1 defect from the resolved session."
    - "Direct code read, DeactivateMonitors (lines 1183 onward): CacheLiveModes is called only as part of a DELIBERATE removal (survivors computed, targets confirmed, THEN cached before ApplyPathInfos) -- Extend's own fallback (line 526, `PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, ...)`) never calls DeactivateMonitors at all; it mutates the whole topology directly via SetDisplayConfig with zero path/mode arrays (already decompile-confirmed in the resolved session), so no caching opportunity exists for whatever Extend's opaque internal choice happens to drop."
    - "Direct code read, MainForm.cs line 740: `.OrderBy(m => m.DevicePath, StringComparer.Ordinal)` -- confirms the app's own tile/display numbering is a stable function of DevicePath alone, never of CCD source assignment, position, or enumeration order -- ruling out 'the app's own numbering logic is broken' as an explanation for the numbering-swap symptom, and pointing the explanation at Windows' OWN numbering shifting instead (an external, CCD-source-assignment-driven effect), consistent with a PathDisplaySource reassignment during fix H's nested reactivation."
    - "Direct code read, TryBuildScopedActivationPlan lines 775-786: `allPaths.FirstOrDefault(p => !claimedSources.Contains(p.DisplaySource) && ...)` -- greedy 'first unclaimed candidate' source selection, with NO check for whether that candidate matches the target's own previously-held PathDisplaySource, even when a cachedMode (line 813) IS present and carries the original source information (PathInfo exposes DisplaySource, already used elsewhere in this same method as a HashSet key at line 763)."
  falsification_test: "If a rig trial reproducing fix H's correction path (a scoped-plan PathChangeException falling back to Extend, dropping a previously-active, non-swap-excluded survivor) STILL shows that survivor's position reset to a driver default AFTER caching is made unconditional for all currently-active paths (the fix below), this hypothesis is refuted -- either the cache is being cleared/invalidated somewhere between capture and use that this read did not find, or a genuinely different mechanism (e.g. the nested call itself not finding the cache due to a key/DevicePath mismatch) is responsible."
  fix_rationale: "Extends the ALREADY-EXISTING, already rig-proven CacheLiveModes mechanism (used by DeactivateMonitors for deliberate removal, and by ActivateMonitors' round-3 swap-exclusion) to cover EVERY currently-active path unconditionally, at the top of ActivateMonitors before any topology mutation is attempted -- not a new mechanism, just widening an existing one's coverage from 'only the deliberately-excluded subset' to 'every survivor, regardless of why it might later end up inactive.' This directly closes the gap fix H's correction path needs (a cached mode available for ANY survivor Extend might accidentally drop, not just the ones this call intentionally excludes) and is strictly additive/no-op-safe for every case that already worked: monitorSwapDisableSet's paths are always a subset of 'every currently-active path,' so the swap case is byte-for-byte unchanged; a plain non-swap call with no accidental drops simply caches data that is never consulted, exactly as harmless as the existing per-swap caching already is for the (common) case where nothing gets corrected."
  blind_spots:
    - "Cannot execute or verify on real Windows CCD hardware in this sandbox -- same structural limitation as every round of the resolved session. Self-verified via build + full adjacent test suite + direct code read/hand-trace of the modified control flow against this round's specific repro (fix H's nested-reactivation path)."
    - "Does NOT fix the source-claim greediness (TryBuildScopedActivationPlan always picks the first unclaimed PathDisplaySource, never preferring the target's own previous source) -- caching Position/Resolution/PixelFormat restores the LOGICAL position but does not guarantee the SAME physical source is reclaimed, so a residual, lower-severity numbering-swap risk could remain even after this fix if Windows' own numbering is sensitive to source identity rather than only active-path count/order. Not fixed this round because: (a) it is a distinct mechanism from the immediately-evidenced position-reset defect, (b) no direct rig evidence yet isolates whether source reassignment vs. blank-mode-position-reset is the numbering symptom's actual proximate cause, and (c) this mirrors the resolved session's own established discipline of fixing the well-evidenced defect now and flagging the less-evidenced one for the verification round rather than guessing preemptively."
    - "Does not explain, and does not attempt to explain, WHY the scoped ApplyPathInfos plan throws PathChangeException for specific monitor pairings in the first place (the trigger that makes fix H's correction path fire at all) -- this remains the resolved session's own still-open Resolution.root_cause (8) item, explicitly carried forward, not re-investigated here."
  candidate_causes:
    - "code (fix target): ActivateMonitors' live-mode caching only covers monitorSwapDisableSet's deliberately-excluded survivors, not every currently-active path -- so fix H's nested reactivation of an ACCIDENTALLY-dropped survivor finds no cache entry and falls back to a driver-default position."
    - "environment/CCD-driver (pre-existing, NOT fixed this round, carried forward from the resolved session's own still-open Resolution.root_cause (8)): the actual trigger for WHY a scoped ApplyPathInfos plan throws PathChangeException for a specific monitor pairing (causing the Extend fallback that can accidentally drop a survivor in the first place) remains genuinely unknown -- a precondition for reaching the buggy code path, not itself being fixed."
  and_gate: "SUPERSEDED by round 9 below -- a freshly-supplied debug.log excerpt (received after this hypothesis was written and its fix already applied) revealed TWO ADDITIONAL, INDEPENDENT code-level defects present in the SAME rig trial, neither of which is the CacheLiveModes gap above and neither of which requires the other to manifest. The CacheLiveModes fix documented above remains correct and necessary but is NO LONGER understood to be sufficient on its own -- see round 9's and_gate for the current, complete picture."

round_9_reasoning_checkpoint:
  context: "A fresh rig debug.log excerpt (received after round 8's investigation above had already started -- NOT seen by that round) was supplied for continued investigation, without redoing or reverting round 8's already-applied and already-recorded CacheLiveModes fix. Two specific new hypotheses were assigned for direct source verification: (1) an uncaught TargetNotAvailableException from ActivateMonitors' own PollUntilStableActiveDevicePaths/QueryActiveDevicePaths, at a DIFFERENT call site than the already-hardened ObservePostApplyStability; (2) whether OnTileAction's 'action already in progress' guard has a try/finally gap that leaves reactive correction permanently blocked."
  hypothesis: "Hypothesis 1 CONFIRMED as stated, with mechanism refined: PollUntilStableActiveDevicePaths (introduced in commit dc93b66, well BEFORE ObservePostApplyStability's round-6 per-tick try/catch hardening was added to the codebase) was NEVER given that same hardening, despite sharing the identical fragile shape -- QueryActiveDevicePaths() calls PathDisplayTarget.DevicePath with no IsAvailable filter, which throws TargetNotAvailableException when a target transiently reports unavailable mid-CCD-renegotiation. The supplied log's stack trace (QueryActiveDevicePaths -> PollUntilStableActiveDevicePaths -> ActivateMonitors -> MainForm.OnTileAction) matches this exactly: the scoped ApplyPathInfos mutation had ALREADY succeeded (logged 'completed without throwing' one line before the throw), so the CCD topology itself was not left in an incomplete SetDisplayConfig state -- but the exception fired during the POST-apply settle-poll (attempt 2/5, before its own log line could print), aborting ActivateMonitors before its own correction loop (fix H), its own final verify-and-throw, and its own ObservePostApplyStability call ever ran for that invocation. The log's own subsequent lines (a DIFFERENT survivor, DELA0BC, reactivating while the intended target, SAM7489, stayed inactive, tracked by the PRIOR action's still-running ObservePostApplyStability background thread) confirm this is a genuine, real-world-observed drift that none of ActivateMonitors' own defense-in-depth layers got a chance to catch, specifically because the exception escaped before reaching any of them. Hypothesis 2 CONFIRMED, but the literal mechanism proposed (a stuck boolean/lease with no try/finally) is REFUTED and replaced by a DIFFERENT, real mechanism found on direct read: the 'already in progress' guard is ToggleOrchestrator._busy, acquired via a lease held in a proper `using` block spanning OnTileAction's entire try/catch/finally, and RunGuarded/the lease's Dispose() both correctly release it in a finally/Dispose regardless of exceptions (confirmed directly in ToggleOrchestrator.cs's own doc comments and code) -- so _busy does NOT get stuck. The REAL defect: OnTileAction's finally block called ArmIntentGuard() UNCONDITIONALLY, regardless of whether the try block's ActivateMonitors/DeactivateMonitors call actually succeeded or threw. ArmIntentGuard() snapshots whatever RefreshMonitorTiles() just observed as 'the deliberately-intended final state' with no awareness of whether the action that just ran actually achieved what it intended. Direct log evidence: ArmIntentGuard fired (eventually, once OnTileAction's finally completed) with intent=[ACI24A4:active, DELA0BC:active, SAM7489:inactive] -- the WRONG, accidental post-failure state -- baking it in as if it were correct. From that point on, TryReactivelyCorrectAgainstLastIntent could never again flag that exact drift, since the guard now believed the drifted state WAS the deliberate one -- a second, independent way (a different file, a different mechanism, from fix H's own gap already fixed by WindowsMonitorController.cs's own intent-guard-adjacent logic) a wrong final state gets silently and permanently accepted, producing the identical user-visible symptom (position/numbering wrong, no further correction attempted, matching the resolved session's own historical 'silent' framing even though in THIS trace a MessageBox with the raw exception text was in fact shown -- the user's own report may not have registered a technical dialog as 'an error', or this specific throwing failure mode may not be the only way this symptom has manifested for them historically)."
  confirming_evidence:
    - "Direct code read + git history, WindowsMonitorController.cs: PollUntilStableActiveDevicePaths' two QueryActiveDevicePaths() calls (original code) had NO try/catch at all, while ObservePostApplyStability's per-tick read (added later, round 6) does -- confirmed via `git show dc93b66:...WindowsMonitorController.cs | grep` that PollUntilStableActiveDevicePaths already existed, unguarded, in commit dc93b66, chronologically BEFORE 4777c40 (the resolved session's closure commit, which is where ObservePostApplyStability's per-tick catch was added) -- the hardening was added for a newer method solving a newer problem and never back-applied to this older, equally-fragile one."
    - "Supplied debug.log excerpt, 22:00:15.098-22:00:15.349: 'Post-Extend settle poll, attempt 1/5: [ACI24A4, SAM7489]' followed immediately by 'ActivateMonitors(...) threw TargetNotAvailableException' with a stack trace terminating at QueryActiveDevicePaths -> PollUntilStableActiveDevicePaths -> ActivateMonitors -> MainForm.OnTileAction -- exactly matches attempt 2/5 (after the 150ms sleep) failing before its own 'attempt 2/5' log line could execute, which is precisely where the unguarded second QueryActiveDevicePaths() call sits in the original code."
    - "Supplied debug.log excerpt, 22:00:15.081: 'round 5 -- scoped activation ApplyPathInfos completed without throwing' -- confirms the actual CCD mutation succeeded BEFORE the exception; the exception is confined to the post-apply verification/correction phase, not an incomplete SetDisplayConfig call."
    - "Direct code read, ToggleOrchestrator.cs (RunGuarded, BeginExclusiveMonitorAccess, ExclusiveMonitorAccessLease.Dispose): _busy is released via Volatile.Write inside Dispose()/finally unconditionally, and OnTileAction holds the lease inside a `using` block spanning its entire try/catch/finally -- refutes 'the guard has a missing try/finally' as literally stated; the guard is correctly released once OnTileAction's own finally block finishes running."
    - "Direct code read, MainForm.cs OnTileAction (both branches, original code before this round's fix): `finally { RefreshMonitorTiles(); ArmIntentGuard(); }` with ArmIntentGuard() called UNCONDITIONALLY -- no check for whether the preceding try block's ActivateMonitors/DeactivateMonitors call actually succeeded."
    - "Direct code read, MainForm.cs ArmIntentGuard(): unconditionally logs and snapshots `_lastKnownMonitors` as the new intent with no success/failure awareness passed in -- confirms it cannot distinguish a deliberate, achieved end-state from an accidental, failed one."
    - "Supplied debug.log excerpt, 22:00:25.492: 'MainForm.ArmIntentGuard: armed -- intent=[ACI24A4:active, DELA0BC:active, SAM7489:inactive]' -- this is the ACCIDENTAL post-failure state (SAM7489 still off, DELA0BC unexpectedly back on) being armed as the new 'intended' baseline, roughly 10 seconds after the exception was thrown and caught (consistent with OnTileAction's finally block eventually completing, its RefreshMonitorTiles()/GetAllMonitors() call presumably slowed by the same underlying transient-CCD-negotiation condition) -- directly confirms the poisoning mechanism, not merely inferred from code shape."
    - "Supplied debug.log excerpt, 22:00:17.412 and 22:00:17.925: two TryReactivelyCorrectAgainstLastIntent-driven correction attempts correctly detected 'reactivated=[DELA0BC] (should be inactive)' against the STILL-VALID prior (round-12.008) intent snapshot, but were rejected with 'a toggle/monitor action is already in progress' -- both occurred WHILE OnTileAction's own busy lease was still legitimately held (OnTileAction had not yet returned), confirming the _busy guard itself behaved correctly here; the actual defect only manifests once OnTileAction's finally later overwrites the still-valid, still-armed prior intent with the wrong new one."
  falsification_test: "If a rig trial reproducing this exact PollUntilStableActiveDevicePaths TargetNotAvailableException throw STILL shows ActivateMonitors aborting uncaught after the per-tick try/catch fix below, the fix is incomplete (e.g. a DIFFERENT unguarded QueryActiveDevicePaths call site, such as the one at ActivateMonitors' own entry line 424, is the actual culprit next time). If a rig trial where ActivateMonitors/DeactivateMonitors genuinely throws STILL shows ArmIntentGuard's intent snapshot silently overwritten with the failed action's accidental state (rather than the pre-failure intent being left untouched), the OnTileAction fix below is incomplete or was bypassed by a different code path (e.g. the Rig/Normal toggle switch handler's own separate ArmIntentGuard call site, not touched this round)."
  fix_rationale: "Fix I (WindowsMonitorController.cs, PollUntilStableActiveDevicePaths): mirrors ObservePostApplyStability's own already-proven-necessary per-tick try/catch pattern exactly -- not a new mechanism, just applying the SAME hardening this codebase already adopted for the identical hazard in a sibling method to the one method that never received it. A failed tick now costs only that tick (skip, keep polling) instead of aborting the entire correction loop; if every attempt in the budget fails, returns an empty set (matching this method's pre-existing 'always return something, never throw' contract) rather than propagating. Fix J (MainForm.cs, OnTileAction): ArmIntentGuard() is now called ONLY when the preceding ActivateMonitors/DeactivateMonitors call actually completed without throwing (tracked via a local success flag set right before the 'returned without throwing' trace line) -- RefreshMonitorTiles() still always runs in `finally` regardless, so the tile dashboard itself stays accurate either way; only the intent-guard re-arming is now conditioned on genuine success, so a failed action leaves any previously-armed (still potentially valid) intent snapshot untouched instead of overwriting it with the failure's accidental result. Both fixes are strictly additive/no-op-safe for the success path: neither changes any behavior when the underlying CCD call succeeds cleanly (the overwhelmingly common case), only when it fails."
  blind_spots:
    - "Cannot execute or verify on real Windows CCD hardware in this sandbox -- same structural limitation as every round of both debug sessions. Self-verified via build + full adjacent test suite + direct code read/hand-trace against this round's specific supplied log."
    - "Fix I only hardens PollUntilStableActiveDevicePaths, not the OTHER unguarded QueryActiveDevicePaths() call site at the top of ActivateMonitors (line ~424, the pre-mutation baseline read). Deliberately not touched this round: that call happens BEFORE any topology mutation, when the topology should be quiescent (no CCD change in flight) -- the transient-unavailability hazard this fix addresses is specifically tied to 'a mutation still in flight elsewhere' per this file's own existing comments, making that call site meaningfully less exposed, and no rig evidence (this round or any prior) implicates it. Flagged as an open, lower-priority hardening candidate, not fixed speculatively."
    - "Fix J only changes the two ActivateMonitors/DeactivateMonitors call sites inside OnTileAction (the tile-dashboard action handler). MainForm.cs has at least one other ArmIntentGuard() call site (around line 2225, in the Rig/Normal toggle switch handler) that was NOT reviewed or changed this round -- if that handler has the same unconditional-arm-on-failure shape, the same poisoning mechanism could still apply to a Rig/Normal toggle failure. Not investigated this round because no evidence (this round's log or prior) implicates the toggle-switch path specifically; flagged as an open item for a future round if a toggle-triggered (rather than tile-triggered) recurrence is reported."
    - "In THIS specific captured trace, Fix J's practical benefit may be partially masked by timing: the prior intent guard's 8-second window (armed at 22:00:12.008, deadline 22:00:20.008) had likely already expired by the time OnTileAction's finally actually completed (~22:00:25.492, per the log) -- so even with the fix leaving the stale-but-correct intent untouched, no further reactive correction may have fired in THIS exact trace before the window lapsed. The fix remains objectively correct and independently valuable (it removes a confirmed active-poisoning mechanism), and would matter in the more common case where OnTileAction's finally completes well within the guard window."
  candidate_causes:
    - "code (Fix I target, WindowsMonitorController.cs): PollUntilStableActiveDevicePaths' QueryActiveDevicePaths() calls have no per-tick exception handling, unlike its sibling ObservePostApplyStability -- a transient TargetNotAvailableException here aborts ActivateMonitors before its own correction/verify/observe machinery ever runs."
    - "code (Fix J target, MainForm.cs, a DIFFERENT file/component than Fix I and than round 8's fix): OnTileAction's finally block re-arms the intent guard unconditionally, with no awareness of whether the action it just ran actually succeeded -- poisoning the guard with a failed action's accidental state."
  and_gate: "YES -- this round confirms an AND-gate scenario, but framed as TWO INDEPENDENT DEFECTS THAT BOTH NEED FIXING, not as two conditions that must jointly hold to produce ONE failure. Fix I (PollUntilStableActiveDevicePaths hardening) and Fix J (OnTileAction's conditional ArmIntentGuard) are each independently real, each confirmed via direct code read + log evidence, each in a different file/method, and NEITHER requires the other to manifest its own failure mode in isolation (Fix I's gap is exercisable any time a transient TargetNotAvailableException occurs during any post-apply settle-poll, regardless of MainForm's intent-guard logic; Fix J's gap is exercisable any time ActivateMonitors/DeactivateMonitors throws for ANY reason, not only via Fix I's specific mechanism). They compounded in THIS one rig trial (both fired from the same triggering exception) but are not codependent -- both must be fixed to close the full regression, and both have now been fixed this round, alongside round 8's already-applied, still-necessary CacheLiveModes widening (three total, independent contributing defects across two files, none superseding another)."

round_10_reasoning_checkpoint:
  context: "User's checkpoint response to the round-9 human-verify ask: NOT 'confirmed fixed' -- a fresh debug.log excerpt from a new rig trial (22:29:59.437-22:30:08.619, PID 12668, first tile action of this app session) capturing a NEW failure. Task this round: verify fixes 1+2 fired correctly, investigate the new failure, and classify it as the same still-open driver-level item vs. a genuinely new fourth defect."
  hypothesis: "Fixes 1 (CacheLiveModes widening) and 2 (PollUntilStableActiveDevicePaths per-tick tolerance) both CONFIRMED firing and behaving exactly as designed in this trial -- direct log-line text match against current source for both. The NEW failure (ActivateMonitors(SAM7489) throwing InvalidOperationException/D-05 after all 5+2 settle-poll attempts across 2 correction rounds never observed SAM7489 active, despite the scoped ApplyPathInfos call itself 'completing without throwing') is NOT a new, fourth, code-level defect -- it is a THIRD observed failure SHAPE of the SAME still-open, never-root-caused driver/OS-level instability already documented in the resolved session's Resolution.root_cause (8) (there: scoped-plan THROWS PathChangeException -> Extend fallback -> an unrequested survivor accidentally drops, while the REQUESTED target itself still comes active; here: scoped-plan reports success with NO exception at all, yet the REQUESTED target itself never actually comes active on the hardware across 7 total settle-poll reads and 2 correction rounds). Both shapes share the same genuinely-unknown trigger already flagged as open in root_cause (8): 'WHY the scoped ApplyPathInfos plan [behaves anomalously] for this specific SAM748A/SAM7489 + ACI24A4 pairing... remains genuinely unknown... not guessed at.' Root_cause (8)'s own text explicitly lists 'a hardware source/port-group constraint specific to this pairing' and 'a cached-mode Position/Resolution mismatch' among unconfirmed candidate mechanisms for this exact monitor identity -- this round's failure is fully consistent with that same unresolved class, not a departure from it. The correction loop (fix H, ComputeUnexpectedlyDeactivated) correctly found nothing to correct both rounds (unexpectedlyActivated=[] unexpectedlyDeactivated=[] both times) because SAM7489 was never a pre-existing survivor that got dropped -- it was the NEWLY-requested target that never gained activation in the first place, a scenario fix H's own mirror-image correction was never designed to address (fix H only re-drives paths that WERE active before the call and lost that activation; it does not retry the call's own primary request). The exception that ultimately fired (InvalidOperationException, 'Monitor enable did not take effect... D-05') is the ORIGINAL verify-and-throw check (monitorDevicePaths.Except(postCorrectionActiveDevicePaths)) that has existed since commit dc93b66 (2026-08-22) with message text from 90fe29d (2026-07-28) -- both predate every fix applied this session (round 8/9, 2026-08-29) by a full week or more. This is the D-05 fail-safe working exactly as designed: surfacing a genuine failure loudly instead of reporting a silently-wrong success -- a BETTER outcome in kind than either Symptom 1's original defect (silent wrong position) or root_cause (8)'s own rig-confirmed shape (silent wrong final state, EXIT success reported)."
  confirming_evidence:
    - "Fix 1 direct confirmation: log line '[22:30:05.824] ... cached live mode for all currently-active paths=[ACI24A4, DELA0BC] before any topology mutation -- covers both a deliberate swap-exclusion and an accidental drop (fix H's correction target) equally.' is a byte-for-byte match of the exact Log() message added this session at WindowsMonitorController.cs line 550, which only executes via the widened, unconditional CacheLiveModes(activePathsForScopedPlan) call (line 549) -- confirms the widened cache call fired, unconditionally, before any topology mutation, exactly as fix 1 intended."
    - "Fix 2 direct confirmation: log lines '[22:30:06.513] ... attempt 2/5 failed (TargetNotAvailableException...) -- skipping this tick, using last known-good reading.' and the identical pattern at attempt 3/5 are byte-for-byte matches of the exact Log() message added this session at WindowsMonitorController.cs line 1096 (PollUntilStableActiveDevicePaths' new per-tick try/catch) -- and critically, the poll LOOP CONTINUED to attempts 4/5 and 5/5 rather than aborting/propagating, and ActivateMonitors itself continued on to its correction-round logic and eventually its own verify-and-throw (not an uncaught crash) -- direct proof the per-tick tolerance held under a REAL, repeated (twice) TargetNotAvailableException in this exact trial, not merely a hypothetical."
    - "Direct code read + git blame, WindowsMonitorController.cs lines 690-701 (verify-and-throw) and 697/699: the log line format ('EXIT throwing -- still inactive after correction: [...]') is attributed to commit dc93b66 (2026-08-22, git blame), and the exact exception message text ('Monitor enable did not take effect: {0}. No further automatic recovery is attempted (D-05).') is attributed to commit 90fe29d (2026-07-28, git blame) -- both predate this session's 3 fixes (all dated 2026-08-29 per this debug file's own frontmatter) by 7+ days. Rules out 'this session's fixes introduced the D-05 throw as a side effect' -- the throw/verify logic is untouched, pre-existing code doing exactly its documented job."
    - "Direct code read, WindowsMonitorController.cs lines 621-638 (ComputeUnexpectedlyActivated/ComputeUnexpectedlyDeactivated call sites) and their own doc comments (lines 163-193): both are defined relative to currentlyActiveDevicePaths (the PRE-CALL baseline). SAM7489 was never a member of that pre-call baseline (preExtendActive=[ACI24A4, DELA0BC] per the log's own line at 22:30:05.823) -- it is structurally impossible for SAM7489 to appear in unexpectedlyDeactivated (which only tracks paths that WERE in the pre-call baseline), confirming the log's own 'unexpectedlyDeactivated=[]' both rounds is CORRECT, not a bug, and confirming fix H's correction mechanism was never designed to retry the call's own primary requested-but-never-activated target -- only pre-existing verify-and-throw (dc93b66-era) covers that case, via monitorDevicePaths.Except(postCorrectionActiveDevicePaths)."
    - "Direct code read, MainForm.cs OnTileAction enable branch, lines 1305-1309: `catch (InvalidOperationException ex) { ...; MessageBox.Show(this, ex.Message, \"Rig Toggle\", MessageBoxButtons.OK, MessageBoxIcon.Warning); }` -- confirms a MODAL dialog carrying the raw D-05 exception text ('Monitor enable did not take effect: ... No further automatic recovery is attempted (D-05).') is shown synchronously on this exact exception type. This is NOT a silent failure at the code level, contradicting this whole debug session's inherited 'silent' framing (carried over from the ORIGINAL resolved bug, where the equivalent failure genuinely never threw or surfaced anything)."
    - "Direct code read, MainForm.cs OnTileAction enable branch, lines 1317-1336 (finally block): ArmIntentGuard() is called only `if (activateSucceeded)`; the else branch logs a distinct 'ArmIntentGuard SKIPPED -- ActivateMonitors(...) threw...' trace line. This trace line does NOT appear anywhere in the supplied excerpt, and by code structure MUST run only after `catch` completes -- i.e., only after MessageBox.Show() (a blocking modal call) returns, which requires the user to have clicked OK. The excerpt's last line is at 22:30:08.619, only ~1.4s after the throw at 22:30:07.181 -- OnDisplaySettingsChanged firing twice in that window is consistent with Windows still pumping WM_DISPLAYCHANGE messages through the modal dialog's own nested message loop while the MessageBox remains open/undismissed, which would explain why the finally block's own trace lines are simply not yet in the captured window. This means the excerpt cannot confirm or refute fix 3's actual conditional-arm behavior for THIS failure -- the 'SKIP' lines present in the excerpt belong to a different, unrelated guard (TryReactivelyCorrectAgainstLastIntent's own 'guard has never been armed this session' check) that is trivially true regardless of fix 3, since this was the first tile action since app startup and no ArmIntentGuard call of ANY kind -- successful or skipped -- had happened yet this session to test against."
  falsification_test: "If a future rig trial reproduces this exact shape (scoped ApplyPathInfos 'completes without throwing' yet the requested target never appears active across the full settle-poll+correction budget, ending in the D-05 throw) AND a full debug.log capture extending past the user's dismissal of the resulting MessageBox shows 'ArmIntentGuard: armed' (not 'ArmIntentGuard SKIPPED') immediately following, that would mean fix 3 (round 9) has a real gap (the activateSucceeded flag not correctly gating the arm) -- worth investigating as a genuinely new, fourth defect at that point, distinct from today's classification. If instead 'ArmIntentGuard SKIPPED' appears as expected, fix 3 is independently confirmed by that future trial. Separately: if a future trial shows this SAME failure shape reproducing for a DIFFERENT monitor pairing (not involving the Odyssey G5/SAM74xx identity), that would refute 'this is confined to the already-flagged SAM74xx+ACI24A4/DELA0Bx pairing instability' and suggest a more general driver/CCD problem worth a fresh investigation rather than treating it as the same narrow carried-forward item."
  fix_rationale: "No new fix applied this round. Per this whole session's (and the original resolved session's) own repeatedly-reaffirmed research-vs-reasoning discipline: root_cause (8) already explicitly declined to guess between multiple plausible mechanisms (stale source candidate, hardware port-group constraint, cached-mode mismatch) for WHY this specific monitor pairing's scoped CCD calls misbehave, across 8 prior rounds plus this session's 2 additional rounds -- 10 total investigation rounds have not root-caused the underlying trigger. This round's new evidence (a THIRD failure shape from the SAME suspect pairing) does not supply the missing discriminating data needed to finally identify that trigger; guessing at a fix now would repeat the exact failure pattern this file has explicitly flagged and avoided since round 5 of the original session. The three already-applied fixes each independently and correctly did their own narrow job in this trial (cached data was available had it been needed; transient ticks were tolerated instead of aborting the whole correction attempt; the pre-existing D-05 safety net correctly refused to report false success) -- none of them needed to be, or should be, touched or reverted based on this trial."
  blind_spots:
    - "Cannot execute or verify on real Windows CCD hardware in this sandbox -- same structural limitation as every round of both debug sessions."
    - "Cannot confirm or refute fix 3's specific conditional-arm behavior from this excerpt (see confirming_evidence's last item) -- genuinely open until a log capture extends past the user's dismissal of the MessageBox this trial almost certainly raised."
    - "Cannot independently confirm the user actually SAW/dismissed the MessageBox described above -- inferred from direct code read of the exact catch branch this exception type hits, not from a log line proving the dialog was displayed or dismissed (WinForms does not log MessageBox.Show calls). If, contrary to this code read, some other code path swallows the exception before reaching this catch (not found in this round's read), the 'not silent' conclusion would need revisiting."
    - "The trailing ', ]' in the very first settle-poll log line (attempt 1/5) is a minor unexplained artifact -- possibly a copy/paste line-wrap glitch in the user-supplied excerpt, possibly a transient third path present only in that first read. Does not change the conclusion (the settled, repeatedly-confirmed state across attempts 1/4/5 excludes SAM7489 either way) and is not pursued further given its ambiguity and irrelevance to the outcome."
  candidate_causes:
    - "environment/CCD-driver (pre-existing, carried forward UNCHANGED from root_cause (8), NOT a new cause introduced this round): the genuine trigger for why the scoped ApplyPathInfos call sometimes fails to durably/actually bring this specific monitor pairing's target online -- despite reporting API-level success -- remains completely unknown, exactly as unresolved as when the resolved session closed."
    - "code (checked and ELIMINATED as a candidate this round): this session's own 3 fixes (CacheLiveModes widening, PollUntilStableActiveDevicePaths per-tick tolerance, OnTileAction conditional ArmIntentGuard) -- each directly confirmed via log-line-to-source-line matching to have fired correctly and done no harm; none is implicated in or responsible for the new failure."
  and_gate: "no new AND-gate condition this round -- this is a single, already-accounted-for candidate cause (the still-open environment/CCD-driver item from root_cause (8)) recurring in a new shape, not a new code-level defect requiring its own branch. The 3-cause AND-gate set from round 9 (CacheLiveModes gap, PollUntilStableActiveDevicePaths gap, OnTileAction unconditional-arm gap) is UNCHANGED and remains fully fixed; this round adds zero new fixed causes and zero new code-level open items -- only a third observed manifestation of the one item that was already carried forward as open and accepted-as-not-being-guessed-at."

round_11_investigation_note:
  context: "User's checkpoint response to round 10's decision ask: Option B -- reopen deep investigation into WHY root_cause (8)'s driver/CCD-level instability happens, rather than accepting it as a known limitation (Option A). User's own instruction: identify what new instrumentation would be needed to distinguish between root_cause (8)'s still-unconfirmed candidate mechanisms, implement it if reasonably scoped/low-risk/additive, self-verify, and check in with a fresh rig-trial ask -- explicitly NOT to guess at or fabricate a fix/root-cause for (8) itself this round."
  what_was_already_tried_and_ruled_out_for_root_cause_8: >
    Full re-read of the resolved session's Resolution section (root_cause (8), its own fix's remarks,
    and the Final Closure section) plus this session's own round 8/9/10 candidate_causes/blind_spots
    confirms the following are ALREADY eliminated or explicitly declined as guesses, not to be
    re-attempted: (a) the CONSEQUENCE of root_cause (8) firing (a lost survivor with no correction, or
    a requested target never coming active) is fully fixed/defended by fix H (round 7 of the resolved
    session) + this session's fixes 1-3 -- none of those are implicated in or responsible for (8)'s
    trigger itself, confirmed via log-line-to-source-line matching across rounds 8-10; (b) three
    candidate mechanisms for the scoped-plan misbehavior were named but explicitly NOT chosen between,
    for lack of discriminating evidence: a stale/incompatible GetAllPaths() source candidate being
    picked as the first unclaimed-source match, a hardware source/port-group constraint specific to
    this pairing, and a cached-mode Position/Resolution mismatch against whichever source got picked;
    (c) for the DIFFERENT, but related, Symptom-2-part-B delayed-revert item (same resolved session,
    root_cause (3)), SDC_SAVE_TO_DATABASE persistence semantics, GPU-vendor auto-profile-restore
    services, and generic driver-forum reports were all researched and found NOT to conclusively match
    the observed ~1.5s-timing shape -- that research is not repeated this round since (8) and (3) are
    documented as independent, differently-shaped items and this round is scoped to (8); (d) no round
    across either session has ever logged which PathDisplaySource TryBuildScopedActivationPlan actually
    assigns to a requested target, how many candidate sources existed for it, or whether the pick
    matches the target's own previous source -- confirmed via direct re-read of WindowsMonitorController.cs
    as it stood before this round's changes (TryBuildScopedActivationPlan's candidate-selection block,
    lines ~872-895 pre-edit, and the scoped-plan log line at ActivateMonitors line ~559 pre-edit, both
    logged only a flat device-path list, never source identity) -- this is the genuinely NEW
    instrumentation gap this round targets, not a re-tread of anything above.
  new_instrumentation_identified_and_implemented: >
    Three purely additive, no-control-flow-change Log() additions to
    src/RigToggle.Windows/WindowsMonitorController.cs: (1) TryBuildScopedActivationPlan now enumerates
    and logs EVERY GetAllPaths() candidate PathDisplaySource for each requested device path (tagged
    [claimed]/[unclaimed]) before the existing greedy "first unclaimed" selection runs -- directly
    tests whether SAM7489/SAM748A ever has more than one candidate source, and whether contention
    (another active survivor already claiming a candidate) correlates with failure -- evidence for/
    against the "hardware source/port-group constraint" candidate. (2) Immediately after selection, logs
    which PathDisplaySource (GPU adapter LUID + numeric source id -- a cheap, no-extra-native-call
    identifier, confirmed via decompile of PathDisplaySource/PathDisplayAdapter/LUID) was actually
    picked, AND whether it matches the SAME target's own previously-cached source (round 8's own
    already-documented, never-fixed source-claim-greediness blind spot) -- evidence for/against the
    "stale/incompatible source candidate" mechanism. (3) ActivateMonitors now logs a full per-entry
    structural dump of the ENTIRE scoped plan array (source identity, mode-info-available, position,
    per-target device path + active flag) immediately before it is submitted to ApplyPathInfos --
    fires regardless of whether the call subsequently throws PathChangeException (root_cause (8)'s
    first observed shape) or reports success while the target never actually activates (root_cause
    (8)'s third observed shape, round 10) -- so a FAILING attempt's plan shape can be hand-compared,
    source-by-source, against a SUCCEEDING one for the identical pairing. A new internal (unit-tested)
    DescribeSource helper and a private (not unit-testable -- see its own remarks: PathDisplayTarget's
    DevicePath getter is confirmed by decompile to require a live CCD query) DescribeScopedPathEntry
    helper implement these. No selection order, claiming behavior, correction logic, or fallback path
    is changed by any of the three additions -- every new Log() call observes state the method already
    computes or already holds in scope.
  deliberately_not_implemented_this_round: >
    (i) Windows Event Log correlation -- technically reachable from .NET (System.Diagnostics.Eventing.Reader
    is BCL, no new dependency) but building automated correlation now would require GUESSING which
    event-log channel/provider (if any) logs anything relevant to an AMD/DisplayPort CCD apply failure
    -- no evidence yet that any specific channel does, and guessing here would repeat exactly the
    failure pattern this whole two-session investigation has already flagged and avoided (see Symptom-2-
    part-B research, above). Scoped instead as a MANUAL, out-of-band step for the user -- see next_action
    and the checkpoint below for exact instructions, now made concrete by this round's own millisecond-
    precision debug.log timestamps to search against. (ii) EDID/monitor capability capture for the
    Odyssey G5 -- technically reachable (raw EDID bytes are readable from the registry under
    HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY\...\Device Parameters\EDID, no elevation typically
    needed) but assessed as speculative diagnostic value for a RUNTIME CCD-apply-timing failure (EDID
    mostly describes static, unchanging monitor capabilities -- resolution/timing tables, vendor id --
    not a runtime driver-negotiation state) and a meaningfully larger, unproven-value implementation
    surface (parsing a raw EDID blob, a capability this codebase has never needed before) for a single
    round whose own instruction was to stay low-risk and additive. Flagged as a possible FUTURE addition
    only if this round's new PathDisplaySource evidence first points toward a genuine capability/
    negotiation mismatch worth confirming via EDID -- not pursued speculatively now.
  guardrail_verdict: accepted
  guardrail_note: "Purely additive, no-op-safe for every existing case (confirmed via build + the full 238-test RigToggle.Tests suite, unchanged, plus RigToggle.Windows.Tests building cleanly with 2 new DescribeSource unit tests, hand-traced against the implementation since this sandbox cannot execute RigToggle.Windows.Tests -- same pre-existing limitation as every prior round of both debug sessions). No selection/claiming/correction/fallback control flow was changed -- the one relocated dictionary lookup (_lastKnownActiveModeByDevicePath.TryGetValue for devicePath, moved earlier in the loop body to share one lookup between the new log line and the existing mode-selection branch) is confirmed behaviorally identical: TryGetValue's bool return and its out parameter being null-when-absent are equivalent for this reference type, so `if (cachedMode != null)` post-move is byte-for-byte equivalent to the pre-move `if (_lastKnownActiveModeByDevicePath.TryGetValue(...))` gate. This is NOT a fix for root_cause (8) -- no root cause is claimed or fabricated this round; this is instrumentation only, awaiting a fresh rig trial that reproduces any of root_cause (8)'s three known shapes to actually discriminate between its remaining candidate mechanisms."

round_13_investigation_note:
  context: >
    First genuinely post-round-11-instrumentation debug.log excerpt received (build banner
    2026-08-29 23:06:08 -- confirmed by direct grep to include round 11's new "candidate
    PathDisplaySource(s)", "selected ...", and "round 11 -- scoped plan entry detail" log lines,
    unlike round 12's excerpt, which predated them). This finally fulfills round 11's still-open
    checkpoint ask, and additionally contains something more valuable than what was asked for: a
    FAILING and a SUCCEEDING full plan-shape dump for the identical requested monitor (SAM7489)
    using the identical selected source, captured 7 seconds apart with no app restart or code
    change in between -- the exact byte-for-byte fail-vs-succeed comparison round 11's own
    falsification_test field named as still missing.
  hypothesis: >
    (1) For the "scoped ApplyPathInfos reports success but the requested target never actually
    settles active" D-05 failure shape (root_cause (8)'s third observed shape, first seen round
    10): CONFIRMED, by direct controlled comparison, to be genuine external CCD/driver-level
    timing nondeterminism, not a function of this app's own source-selection or cached-mode
    logic. The 23:06:46-48 failing attempt and the 23:06:53-54 succeeding attempt for the SAME
    target both selected the IDENTICAL PathDisplaySource (sourceId=2, from an identical
    5-candidate list, both logging "no prior cache entry") and submitted a byte-for-byte
    IDENTICAL 3-entry scoped plan (same sources, same mode-availability flags, same positions for
    ACI24A4/DELA0BC, same blank mode for SAM7489) -- yet one failed and the other succeeded, 7
    seconds apart, with nothing else in this app's own state having changed. This directly
    ELIMINATES "stale/incompatible source candidate" and "cached-mode mismatch" as explanations
    for this shape specifically (both require the input to differ between a failing and
    succeeding attempt; it did not) and leaves only some form of external, time-varying
    driver/CCD-level negotiation behavior (loosely, root_cause (8)'s "hardware source/port-group
    constraint" candidate, though evidently not a FIXED per-source constraint, since the exact
    same source worked cleanly on retry -- more precisely a transient negotiation race, not
    further discriminable from app-level instrumentation alone).
    (2) A SEPARATE, independently real, code-level defect was newly exposed at 23:07:09 (the
    second SAM7489 attempt, only ACI24A4 active going in after DELA0BC was deliberately
    disabled): TryBuildScopedActivationPlan's greedy "first unclaimed PathDisplaySource"
    selection (round 8's own already-documented, never-fixed blind spot) picked sourceId=1 for
    SAM7489 even though a cache entry existed showing SAM7489 had previously used sourceId=2
    (proven by the log's own "matches this target's own previously-cached source: False" line --
    only printable when cachedMode is non-null and its DisplaySource differs from the fresh
    pick). Because cachedMode is non-null, the code still applied cachedMode.Position --
    (1920,0), a value matching neither ACI24A4's logged (0,0) nor DELA0BC's logged (-1920,-58)
    anywhere in this excerpt, best explained as SAM7489's OWN previously-cached position
    (captured by DeactivateMonitors' CacheLiveModes at 23:06:59.327, moments before SAM7489 was
    disabled, reflecting wherever the driver placed it during its own successful 23:06:53-54
    activation on sourceId=2) -- to the newly-selected, DIFFERENT sourceId=1. This submits a
    position/source pairing to ApplyPathInfos that was never actually validated together on this
    hardware. This attempt's ApplyPathInfos did not throw, but its settle-poll saw SAM7489
    transiently active (attempt 1/5) then gone, while DELA0BC (untouched, not requested)
    repeatedly reactivated on its own across two correction rounds before settling off -- fix H's
    correction loop (round 7, resolved session) correctly detected and suppressed DELA0BC's
    reactivation both times, but had nothing to say about SAM7489 itself (structurally cannot
    appear in unexpectedlyDeactivated, never a pre-call survivor -- matching round 10's
    established classification exactly). NOT confirmed to be root_cause (8)'s trigger (cannot
    explain finding (1)'s failure, which had no source/position mismatch at all) -- a genuinely
    separate, real, but so-far only correlated-not-proven-causal contributing factor for this
    occurrence's DELA0BC-flapping instability, worth fixing on its own merits regardless.
    (3) The "vanishing event" (SAM7489 completely absent, not merely inactive, from one
    RefreshMonitorTiles read at 23:07:18.643) is fully explained by GetAllMonitors()'s OWN
    existing, correct IsAvailable filter on GetAllPaths()'s "available inactive targets" half
    (line 318): SAM7489's IsAvailable was transiently false at that exact query instant, so it is
    excluded from both halves of MergeAllMonitors -- the SAME code path every other
    RefreshMonitorTiles call in this excerpt used, with no exception thrown or swallowed
    anywhere (confirmed: no "GetAllMonitors failed" trace line anywhere in this excerpt).
    Combined with the repeated TargetNotAvailableException hits already logged for SAM7489 across
    both activation attempts in this same excerpt, this is a third independent observation,
    within ~30 seconds, of SAM7489's own target availability flapping -- evidence (not proof)
    that finding (1)'s external nondeterminism is tied to this monitor's specific physical
    link/negotiation behavior. This is NOT the same event as the original resolved session's
    round-6 "ACI24A4 vanishing" observation, which was later confirmed by the user to be an
    unrelated hardware confound (a manual DisplayPort/power cable unplug during that trial) and
    explicitly annotated as not evidence for any periodic-reassertion mechanism -- this round's
    event involves a different monitor with no reported cable manipulation, so it cannot be
    dismissed the same way, but likewise cannot be elevated beyond "this monitor's connection
    intermittently reports unavailable."
    (4) Separately noticed during code review (not implicated in anything observed this round, no
    fix proposed): GetActiveMonitors() (lines 276-291, called by GetAllMonitors() for its "active"
    half) reads DevicePath on every target of every active path with NO IsAvailable filter --
    unlike GetAllMonitors()'s own second half and TryBuildScopedActivationPlan, both of which
    filter first. An active path whose target transiently reports unavailable (the exact class of
    event just confirmed for SAM7489) could in principle throw TargetNotAvailableException
    uncaught out of GetActiveMonitors() -> GetAllMonitors() -> RefreshMonitorTiles' existing
    one-retry-then-degrade-to-empty path -- a more severe failure shape (whole dashboard blank)
    than what was actually observed (a correctly-explained partial list, no exception anywhere in
    the trace). Flagged as a plausible future hardening candidate, not claimed to have fired here.
    (5) The task framing's claim of a process restart at pid=36872 (~23:07:26) is NOT verifiable
    against the raw log excerpt actually supplied -- it ends at 23:07:22.147 with no PID/restart
    reference anywhere in the text. Recorded as an unconfirmed claim from the framing, not
    independently observed, per this round's own instruction to verify readings directly.
  confirming_evidence:
    - "Byte-for-byte identical 5-candidate PathDisplaySource list and selected-source/cache-match line for the 23:06:46.868-869 (failing) and 23:06:53.447 (succeeding) TryBuildScopedActivationPlan calls for SAM7489."
    - "Byte-for-byte identical 'round 11 -- scoped plan entry detail' dump (all 3 entries) for the 23:06:46.870 (failing) and 23:06:53.448 (succeeding) ActivateMonitors calls -- confirms this excerpt DOES contain the byte-for-byte plan-shape comparison round 11's falsification_test asked for."
    - "23:06:48.083/236: both correction rounds for the failing attempt log unexpectedlyActivated=[] unexpectedlyDeactivated=[] -- SAM7489 simply never came active, nothing to correct."
    - "23:07:09.261 'selected ... sourceId=1 ... matches this target's own previously-cached source: False' -- per the logging code's own ternary, 'False' only prints when cachedMode is non-null and its DisplaySource differs from the fresh pick -- a stale-source cache hit against a freshly-different selection, proven not inferred."
    - "23:07:09.262 plan entry detail shows position=(1920,0) for SAM7489 under sourceId=1 -- checked against every other position value logged anywhere in this excerpt (ACI24A4 always (0,0), DELA0BC always (-1920,-58)) -- a genuinely distinct value, not a copy of either co-resident monitor's position."
    - "23:07:10.271/11.749: both correction rounds for this second attempt log unexpectedlyActivated=[DELA0BC] with a successful DeactivateMonitors(DELA0BC) correction each time; round 3 (23:07:12.434) logs unexpectedlyActivated=[] unexpectedlyDeactivated=[] with postExtendSettledActive=[ACI24A4] only -- fix H fired correctly and eventually suppressed DELA0BC's repeated self-reactivation."
    - "Direct code read, GetAllMonitors()/MergeAllMonitors(): SAM7489's absence from the 23:07:18.643 read requires only that IsAvailable be false in that instant's GetAllPaths() read -- no exception path, no different code. Grepped the full excerpt for 'GetAllMonitors failed' -- not present."
    - "Direct code read, GetActiveMonitors() (lines 276-291): confirmed no IsAvailable filter before reading DevicePath -- a real, adjacent gap, not evidenced as having fired in this excerpt (no empty-list RefreshMonitorTiles read appears anywhere in it)."
    - "Grepped the full supplied DATA block for 'pid=36872' and '23:07:26' -- neither appears anywhere in the actual excerpt text provided."
  falsification_test: >
    If a future post-instrumentation excerpt shows a failing and succeeding SAM7489 attempt with
    DIFFERENT selected sources or plan shapes (not byte-identical, unlike this round's pair), that
    would refute finding (1)'s external-nondeterminism conclusion and re-open "stale/incompatible
    source candidate" as live. If, after fix B (below) is implemented, the same position/source-
    mismatch DELA0BC-flapping pattern still reproduces for an identical pairing, finding (2) would
    be refuted as a meaningful contributing factor. If a future excerpt shows a fully-empty
    RefreshMonitorTiles read coinciding with a "GetAllMonitors failed" trace line after SAM7489
    TargetNotAvailableException activity, that would confirm finding (4) as real and actionable.
  fix_rationale: >
    NOT APPLIED THIS ROUND, per this whole investigation's own established discipline against
    rushing a fix for a bug this stubborn the same round a plausible mechanism is first
    identified. Two candidate fixes proposed as hypotheses needing self-verification (fix A
    additionally needing one more confirming rig trial) before implementation -- see Resolution
    round 13 addendum for the full write-up of fix A (bounded retry for the "target never
    settles despite apply success" shape) and fix B (prefer-reclaim-own-previous-source in
    TryBuildScopedActivationPlan).
  blind_spots:
    - "Cannot execute or verify anything on real Windows CCD hardware in this sandbox -- same structural limitation as every round of both debug sessions."
    - "Finding (1) is drawn from a SINGLE fail/succeed pair -- a strong, controlled, direct comparison, but one data point's worth of it."
    - "Finding (2)'s causal link to the observed DELA0BC-flapping is plausible and evidence-consistent, not proven -- cannot rule out the flapping would have happened regardless of the mismatch."
    - "Finding (3)'s 'this monitor's link/negotiation is flaky' characterization remains a hypothesis -- Windows Event Viewer correlation or a physical cable/port swap test (both previously offered as optional) would be needed to move this further; neither has been done."
    - "Finding (4) is raised from code review alone, with zero evidence in this excerpt that it has ever fired."
  candidate_causes:
    - "environment/CCD-driver (root_cause (8), REFINED not newly discovered): confirmed, for the D-05/never-activates shape specifically, to be genuine external timing nondeterminism independent of this app's own source/mode selection -- narrows away two of three previously-named candidates, leaves a transient hardware/driver negotiation race as the best-supported remaining explanation, mechanism still unconfirmed."
    - "code (newly identified, NOT yet fixed, NOT proven to be root_cause (8)'s trigger): TryBuildScopedActivationPlan's greedy first-unclaimed source selection can pair a cached position with a freshly different, unvalidated source -- a genuinely separate category from the environment/CCD-driver candidate above."
  and_gate: >
    Partial yes, in the same spirit as round 9's multi-cause finding: this round's evidence
    supports (1) a narrowed, still-open environment/CCD-driver cause for root_cause (8)'s D-05
    shape, and (2) a separate, independently-real code-level defect observed in the SAME trial
    but not proven to require the other to manifest -- (1) fired with NO source/position mismatch
    present (first attempt), and (2)'s mismatch fired in a call whose eventual D-05 throw is, by
    round 10's own classification, indistinguishable in kind from the first attempt's. Whether (2)
    genuinely contributes to (1)'s shape or is purely coincidental in this one busy trial is the
    single most important open question for a future round -- not resolved here.

round_14_reasoning_checkpoint:
  context: >
    User's checkpoint response to round 13's decision ask: Option A -- implement both
    candidate fixes now (bounded retry for the "reports success but never activates" shape,
    fix A; prefer-reclaim-own-previous-source in TryBuildScopedActivationPlan, fix B),
    self-verify per this investigation's normal discipline, update this file, and check in
    with a fresh rig-test ask. Relayed via the orchestrator as a prior-checkpoint decision,
    not a new symptom report.
  reasoning_checkpoint:
    hypothesis: >
      Fix A: root_cause (8)'s third observed shape (round 10, re-confirmed round 12/13) --
      scoped ApplyPathInfos reports success with no exception, yet the CALL'S OWN requested
      target never settles active across the full settle-poll+correction budget -- is, per
      round 13's own direct controlled comparison (a byte-for-byte identical scoped plan
      failed once then succeeded 7 seconds later for the same target), a transient external
      CCD/driver-level condition that an immediate, automatic retry of the whole scoped
      sequence can recover without any user action. Fix B: TryBuildScopedActivationPlan's
      greedy "first unclaimed PathDisplaySource" selection (round 8's own long-documented,
      never-fixed blind spot) can pair a target's cached position with a freshly different,
      never-validated-together source -- round 13's evidence entry 2 directly observed this
      exact pairing occur and correlate with (though not proven to cause) a DELA0BC-flapping
      instability in the same trial; preferring to reclaim the target's own previously-used
      source when it is present and unclaimed closes this gap without weakening the existing
      greedy fallback for every other case.
    confirming_evidence:
      - "Round 13 evidence entry 1: byte-for-byte identical selected PathDisplaySource (sourceId=2), identical 5-candidate list, identical blank-mode 3-entry scoped plan for the SAME target (SAM7489), 23:06:46-48 (FAILED, D-05) vs. 23:06:53-54 (SUCCEEDED) -- direct, controlled proof that an identical-input retry can recover this shape."
      - "Round 13 evidence entry 2: 'selected sourceId=1 ... matches this target's own previously-cached source: False' (only printable when a non-null cached entry exists whose source differs from the fresh pick) paired with position=(1920,0) matching neither co-resident monitor's logged position anywhere in the excerpt -- direct proof of the source/position pairing defect fix B targets."
      - "Direct code read (this round) of TryBuildScopedActivationPlan's pre-round-14 candidate-selection block: confirmed the greedy pick had zero preference logic for a target's own cached source, exactly as round 8/11/13 documented."
      - "Direct code read (this round) of ActivateMonitors' pre-round-14 verify-and-throw: confirmed no retry of any kind existed before surfacing D-05 -- a single scoped-attempt failure always terminated the call."
    falsification_test: >
      Fix A: if a future debug.log excerpt shows the bounded retry firing (the round-14
      "INTERNAL automatic retry" log line present) but STILL exhausting its budget and
      throwing D-05 on a trial where a subsequent MANUAL tile re-click succeeds immediately
      afterward with no other change, that would mean the retry's own re-query/re-build is
      doing something subtly different from what a manual re-click does (a gap in fix A's
      implementation, not evidence against the underlying retry-recoverability finding).
      Fix B: if a future excerpt shows the "matched preferred (previously-cached) source"
      log line firing (confirming the preference was honored) yet the same source/position
      mismatch class of instability (an unrelated survivor flapping, or a D-05 throw for the
      same pairing) still reproduces, that would refute fix B's source-claim-greediness
      mechanism as a meaningful contributing factor (consistent with round 13's own
      acknowledgment that this was correlated, not proven causal, even before this round).
    fix_rationale: >
      Fix A is layered strictly OUTSIDE fix H's existing lost-survivor correction loop (a
      different mechanism for a different symptom) via ShouldRetryScopedActivation's narrow,
      three-part trigger (scoped attempt itself succeeded with no exception AND the call's
      OWN requested target is still missing AND the bounded budget is not exhausted) -- never
      retries an Extend-fallback failure or a persistent lost-survivor-only failure, both
      explicitly out of scope per the task's own design constraints. Fix B is a pure
      selection-preference change (SelectSourceForActivation) with the existing greedy
      fallback preserved byte-for-byte whenever the preference does not apply -- addresses
      the root SELECTION defect (pairing a cached position with an unvalidated source)
      directly, not merely a downstream symptom of it.
      Neither fix claims root_cause (8)'s own underlying OS/driver mechanism is understood or
      eliminated -- both are targeted mitigations for the SPECIFIC, evidenced failure shapes,
      exactly as scoped by this round's task.
    blind_spots:
      - "Cannot execute or verify on real Windows CCD hardware in this sandbox -- same structural limitation as every round of both debug sessions. Self-verified via full build + full test suite (RigToggle.Tests unchanged, RigToggle.Windows.Tests builds cleanly with 11 new hand-traced unit tests for the two newly pure/testable helpers) + direct before/after code comparison."
      - "Fix A's retry recoverability is drawn from a SINGLE fail/succeed data point (round 13) -- a strong, controlled comparison, but one instance. It is possible the underlying external condition sometimes persists across 3 total attempts (this round's bound); if so, D-05 still correctly surfaces rather than looping unboundedly or silently swallowing a genuine failure."
      - "Fix B's causal contribution to root_cause (8)'s actual D-05/flapping failures (as opposed to being merely correlated with round 13's one occurrence) remains unconfirmed -- carried forward unchanged from round 13's own acknowledgment of this limitation."
      - "Neither fix can be exercised in this sandbox's settle-poll/correction-loop control flow end-to-end (that requires live PathInfo.GetActivePaths()/ApplyPathInfos() calls) -- ShouldRetryScopedActivation and SelectSourceForActivation, the two decision points that actually changed behavior, ARE unit-tested in isolation; the live CCD plumbing around them (TryBuildScopedActivationPlan's candidate resolution, ActivateMonitors' retry loop's live calls) is self-verified via build + hand-trace only, matching this file's own established, already-documented seam boundary."
    candidate_causes:
      - "code (fix A target, WindowsMonitorController.cs ActivateMonitors): no bounded retry existed for the round-10/13-confirmed 'scoped apply succeeds but requested target never settles' shape -- a single failed settle+correction budget always terminated in D-05, even though round 13 proved an immediate retry with identical inputs can succeed."
      - "code (fix B target, WindowsMonitorController.cs TryBuildScopedActivationPlan, a DIFFERENT method/mechanism than fix A): greedy first-unclaimed source selection had no preference for reclaiming a target's own previously-cached source, allowing a cached position to be paired with an unvalidated, different source."
    and_gate: >
      Fixes A and B are two INDEPENDENT changes to two DIFFERENT methods, addressing two
      DIFFERENT (evidenced, round 13) failure shapes -- neither requires the other to fire;
      each is unit-tested and reasoned about in isolation. This mirrors round 9's own
      "independent defects, not a joint AND-gate" framing, not a single-cause branch.
  fix_a_applied: >
    src/RigToggle.Windows/WindowsMonitorController.cs, ActivateMonitors only: wrapped the
    existing scoped-build+apply+settle+correct+verify sequence in a bounded `for` loop
    (MaxScopedActivationRetryAttempts = 2, i.e. up to 3 total attempts). On each attempt,
    activePathsForScopedPlan/devicePathsToActivate/usedScopedActivation are freshly
    recomputed against live CCD state (never a stale snapshot from an earlier failed
    attempt) and CacheLiveModes re-runs (harmless, idempotent). After the settle-poll+
    correction budget, `requestedStillInactive` (the call's OWN requested targets still
    missing) is now computed separately from `stillInactive` (the combined set, unchanged
    in final content) so the new pure helper ShouldRetryScopedActivation can decide whether
    to `continue` (log an explicit "round 14 (fix A) -- INTERNAL automatic retry N/M" line,
    distinguishable from a user-initiated retry, which always produces its own fresh
    "ActivateMonitors: ENTER ..." line instead) or fall through to the existing, unchanged
    D-05 throw. Fix H's own lost-survivor correction loop (ComputeUnexpectedlyDeactivated +
    its nested ActivateMonitors call) is completely unchanged and still runs, unaffected,
    inside every attempt. Byte-for-byte behavior-identical for the common case (first
    attempt succeeds): `break` fires before any retry-specific logic runs.
  fix_b_applied: >
    src/RigToggle.Windows/WindowsMonitorController.cs, TryBuildScopedActivationPlan only:
    added a new pure helper, SelectSourceForActivation(unclaimedCandidateSources,
    previouslyCachedSource), that prefers reclaiming a target's own previously-cached
    PathDisplaySource when it is present among this call's own unclaimed candidates,
    falling back to the first unclaimed candidate (byte-for-byte identical to the pre-
    round-14 greedy behavior) whenever the preference does not apply. The existing
    candidate-enumeration log line (round 11) is untouched; a new "round 14 (fix B) --
    source-preference check" log line records the outcome (matched preferred source / no
    prior cache entry / cached source unavailable this call), and the existing round-11
    "selected ... (matches this target's own previously-cached source: ...)" log line is
    preserved byte-for-byte so debug.log excerpts spanning both rounds remain directly
    comparable.
  guardrail_verdict: accepted
  guardrail_note: >
    All self-executable signals pass. No-op/deletion review: fix A's restructuring moves
    (never deletes) the pre-existing scoped-build+apply+settle+correct+verify sequence
    inside a loop whose first iteration is confirmed behaviorally identical to the
    pre-round-14 single-pass code (same operations, same log lines except one appended,
    empty-string suffix on attempt 1); fix B's removed greedy `FirstOrDefault`+null-check is
    fully subsumed by SelectSourceForActivation's fallback branch, confirmed via direct
    before/after comparison to preserve the exact same enumeration order (LINQ Where/Select
    preserve source order, so the fallback's first-unclaimed pick is unchanged). Full-
    solution build: 0 errors, same 6 pre-existing unrelated xUnit1031 warnings, all 6
    projects. RigToggle.Tests: 238/238 passed, unchanged. RigToggle.Windows.Tests: builds
    cleanly, 0 warnings, 0 errors, including 10 new unit tests (5 for
    SelectSourceForActivation, 5 for ShouldRetryScopedActivation -- total test count now 39,
    up from 29 before this round) covering every branch of both new pure decision
    helpers -- hand-traced against the implementation (this sandbox cannot execute
    RigToggle.Windows.Tests, the same pre-existing, unrelated limitation as every prior
    round of both debug sessions) and additionally spot-checked directly against the real
    WindowsDisplayAPI 1.3.0.13 assembly (via a standalone reflection probe confirming
    PathDisplaySource's value-based Equals/GetHashCode/op_Equality, which SelectSourceForActivation's
    Contains-based preference check and its unit tests both depend on). Mutation check:
    skipped, no Stryker configured in this repo (matches every prior round). Revert-and-
    reconfirm: deferred to the human-verify checkpoint below (requires live CCD hardware).

round_15_investigation_note:
  context: >
    First genuinely post-round-14 rig debug.log excerpt (12:56:50.683-12:56:59.920, pid=33716,
    builtAt=2026-08-30 12:55:49 -- confirmed by direct grep to include round 14's own new "round
    14 (fix A)"/"round 14 (fix B)" log lines, so this build genuinely has both round-14 fixes
    active). Fix A's automatic retry fired for the first time in the real world. It did not
    recover -- and the retry hit a state (0 candidate PathDisplaySources) that is, at face value,
    more degraded than the original attempt's (5 candidates). Task this round: verify both
    readings directly against the log (not the framing), determine whether Fix A's retry timing
    is implicated, and reassess external-vs-own-code confidence honestly.
  verified_1a: >
    CONFIRMED, exactly as framed, via direct log read: 5-candidate list at 12:56:52.689, fix-B
    "no prior cache entry -- using greedy first-unclaimed selection" at .689, sourceId=2 selected,
    ApplyPathInfos "completed without throwing" at 52.978, both correction rounds (53.886, 54.041)
    logging unexpectedlyActivated=[]/unexpectedlyDeactivated=[] -- structurally correct per round
    10's classification (SAM7489 was never part of the pre-call baseline, so it cannot appear in
    either set). ONE ADDITION the framing did not mention: the settle poll's VERY FIRST read,
    immediately after apply (12:56:52.979, attempt 1/5), DID show SAM7489 active
    ([ACI24A4, DELA0BC, SAM7489]) -- it only dropped out after two TargetNotAvailableException
    ticks (53.348, 53.512) and two subsequent stable reads without it (53.721, 53.885). This
    attempt was not simply "target never came active" -- it came active, then appears to have
    dropped back out during a ~740ms window bounded by two transient-unavailable events, directly
    inside this SAME first attempt's own settle-poll, well before Fix A's retry ever ran.
  verified_1b: >
    CONFIRMED, exactly as framed: retry fires at 54.042 (~1.358s after the 52.684 original
    request), logged "round 14 (fix A) -- INTERNAL automatic retry 1/2"; TryBuildScopedActivationPlan
    logs "has 0 candidate PathDisplaySource(s) from GetAllPaths(): []" for SAM7489; this forces
    the whole-topology Extend fallback (known unreliable); Extend also fails to bring SAM7489
    active; D-05 throws again. ONE PREVIOUSLY-UNEXAMINED NUANCE found by tracing
    ShouldRetryScopedActivation's own three-part gate (usedScopedActivation && requestedStillInactiveCount>0
    && attemptNumber<=maxRetryAttempts) against this attempt's actual values: usedScopedActivation
    was FALSE for this retry (it fell back to Extend), which is what actually stopped further
    retrying -- attemptNumber (2) had NOT yet exceeded maxRetryAttempts (2), so the numeric budget
    was not literally exhausted; the retry loop's own explicit design (fix A's doc comment)
    deliberately never retries an Extend-fallback failure, treating it as a different failure
    shape. In effect only 1 of the 2 allotted retries was ever usable this trial -- the second
    slot was forfeited the instant the retry itself got redirected to Extend. Is 0 candidates
    genuinely worse, or expected/structural? GENUINELY WORSE, and NOT a code artifact: direct
    read of TryBuildScopedActivationPlan's candidate-enumeration predicate (`t.DisplayTarget.IsAvailable
    && t.DisplayTarget.DevicePath == devicePath`, lines ~1000-1004/1021-1023) is identical code,
    identical parameters, on both the original attempt and the retry -- the only thing that
    changed between "5 candidates" and "0 candidates" is the live driver-reported IsAvailable
    flag itself. Given SAM7489 was ALREADY throwing TargetNotAvailableException twice within the
    ORIGINAL attempt's own settle-poll (53.348, 53.512 -- 530-694ms before the retry's own
    GetAllPaths() query at 54.043), the most parsimonious reading is that this is a CONTINUATION
    of that same transient-unavailability episode, not an unrelated, independent flake.
  retry_timing_assessment: >
    Does Fix A retry with zero delay? CONFIRMED, directly from code: the outer retry `for` loop's
    `continue` (line 788) re-enters the loop body immediately at the top (a fresh CacheLiveModes
    call, line 628) -- no Thread.Sleep or backoff of any kind between attempts. The log confirms
    this: the verify-and-throw's postCorrection query and the "INTERNAL automatic retry" log line
    both land at 54.041/54.042, and the very next line (the retry's own CacheLiveModes call) is
    also 54.042. The ~1.358s total gap between the original request and the retry firing is
    entirely consumed by the settle-poll-then-correct loop's OWN pre-existing delays (2 correction
    rounds' worth of 150ms-spaced polls), not any deliberate Fix A backoff. Is there a plausible
    mechanism by which retrying too soon makes things worse? YES, evidence-grounded, not
    speculative: the SAME target was observably still inside a transient-unavailability episode
    (two TargetNotAvailableException hits, 53.348/53.512) only ~530-700ms before the retry's own
    GetAllPaths() query sampled it and found it fully unavailable (0 candidates). Retrying
    immediately samples the SAME external negotiation window the original attempt was already
    inside, rather than waiting for it to clear -- and, per the nuance above, a retry that lands
    inside that window doesn't just fail to recover, it also forfeits the rest of the retry
    budget (Extend-fallback attempts are excluded from ShouldRetryScopedActivation's
    usedScopedActivation gate). Is this better explained as unrelated flakiness a delay would not
    have helped? Cannot rule this out with a single trial, but it is the less parsimonious
    reading given the SAME-attempt TargetNotAvailableException evidence above.
    CONCLUSION: timing is plausibly relevant, but NOT solidly enough to implement a specific
    change this round -- see candidate_fix_k below. Presented as a decision for the user, per
    this investigation's own long-standing discipline (rounds 10/11/13) against guessing a fix
    parameter without adequate justification.
  candidate_fix_k_not_implemented: >
    Proposed, NOT applied: before each Fix-A retry attempt's own CacheLiveModes/
    TryBuildScopedActivationPlan call, either (i) a short FIXED delay (simplest, but the correct
    duration is unjustified from one data point -- the observed flap window was at least
    ~700-900ms in this trial, but its true distribution is unknown), or (ii) a short BOUNDED poll
    for the target's own IsAvailable flag returning true again before resampling (more principled
    -- reuses PollUntilStableActiveDevicePaths' existing bounded-poll idiom instead of inventing a
    blind sleep -- but is new code with its own new failure modes, a bigger change than one trial
    justifies). NOT implemented this round because: (a) single-trial evidence, the exact caution
    this file has repeatedly required before touching timing-sensitive retry code; (b) the
    underlying episode's characteristic duration is unknown beyond "at least ~700-900ms this one
    time" -- picking a specific delay value now would be exactly the "plausible but not solid"
    guess this investigation's discipline (rounds 10, 11, 13) has repeatedly declined to make.
  external_vs_own_code_reassessment: >
    Symptom 1 (round 8 fix H gap) -- UNCHANGED, reaffirmed, not touched by anything in this
    round's evidence. D-05 throw/verify age classification -- RE-VERIFIED directly this round
    (not assumed): git blame on the CURRENT, post-round-14 code confirms the throw statement
    itself (now at lines 792-794) is still attributed to commit 90fe29d (2026-07-28), unchanged;
    round 14 only added a retry-eligibility check immediately above it. Classification stands,
    now re-confirmed against the live code rather than carried forward on faith. Could Fix A's OWN
    retry loop be responsible for hitting the 0-candidate state (as opposed to the underlying
    driver)? Investigated directly, not asserted: Fix A's retry-ELIGIBILITY logic
    (ShouldRetryScopedActivation) behaved exactly as designed and queries GetAllPaths() with
    byte-identical code/parameters to any fresh call -- it is not buggy and does not itself cause
    the target to report unavailable (the flapping is independently confirmed to have STARTED
    during the ORIGINAL attempt's own settle-poll, well before Fix A's retry ever ran, so Fix A
    did not create the flakiness). BUT Fix A's retry TIMING (unconditional, zero-delay) is a
    genuine code-level design choice -- not the driver's own behavior -- that this round's
    evidence shows CAN determine which moment of an already-externally-caused unavailability
    window gets sampled, and a badly-timed sample was directly observed to produce a worse outcome
    than the original attempt's. This is a real, evidence-grounded reason to PARTIALLY temper (not
    reverse) the "our code choices don't matter" framing: HIGH confidence the underlying TRIGGER
    (why SAM7489 flaps) remains external/driver-level, unchanged from round 13. MODERATE-TO-LOW
    confidence, newly and narrowly for Fix A's retry-timing parameter specifically, that our code
    choices don't matter -- WHEN we resample the target plausibly affects whether the retry helps
    or (as observed here) makes the immediate failure mode worse. This is a refinement of, not a
    reversal of, this investigation's prior conclusions.
round_16_decision_note:
  context: >
    User's checkpoint response to round 15's decision ask (Option A: fixed short pause before each
    Fix-A retry attempt / Option B: active poll-until-reachable before each Fix-A retry attempt /
    Option C: hold off on any retry-timing change, gather one more data point first / Option D:
    accept this as an edge case, move on) -- Option C. User's explicit instruction: no code change
    this round; wait for the next debug.log excerpt whenever SAM7489 (or any monitor) fails to
    activate, so round 15's "0 candidate PathDisplaySource(s) on immediate retry" pattern can be
    compared against a future occurrence before committing to a specific timing fix (Option A vs.
    Option B, both still open, neither chosen by this decision). Relayed via the orchestrator as a
    prior-checkpoint decision, not a new symptom report or a fresh debug.log excerpt -- no new
    evidence accompanies this round.
  decision_recorded: >
    Option C selected over Option A, Option B, and Option D. No code change applied this round.
    Candidate Fix K (proposed at round 15, in either its fixed-delay or bounded-availability-poll
    form) remains exactly as round 15 left it -- neither variant implemented. Round 15's own
    retry_timing_assessment conclusion (timing is PLAUSIBLY relevant and evidence-grounded, but
    single-trial and not solid enough to justify picking a specific parameter) is UNCHANGED -- this
    round adds no new evidence for or against it, per its own explicit no-code-change/no-fabricated-
    evidence scope. Root_cause (8)'s underlying OS/driver mechanism remains exactly as open as round
    15 left it.
  what_this_round_does_not_do: >
    Does not implement candidate Fix K in either form. Does not fabricate a new hypothesis,
    root-cause claim, or Evidence entry -- no new debug.log excerpt was supplied this round. Does
    not alter round 15's reasoning, Evidence, Resolution addendum, or verification records -- those
    stand exactly as written, preserved for historical record. This mirrors round 10's Option-B
    recording (round_11_investigation_note's context field, above) and round 13's Option-A recording
    (round_14_reasoning_checkpoint's context field, above) -- same bookkeeping discipline, applied
    here to a "gather more data" choice instead of an "implement" or "reopen" choice.
round_17_investigation_note:
  context: >
    Fresh rig debug.log excerpt (17:11:00.724-17:13:04.086, pid=30908, builtAt=2026-08-30
    12:55:49 -- same build as round 15/16, no new code since then) surfaced TWO significant
    items: (1) a device-path identity mismatch (SAM748A/UID521, DELA0B8/UID516) that has
    NEVER appeared anywhere in rounds 1-16, causing an immediate, fully-deterministic
    ActivateMonitors failure via the Rig-mode TOGGLE SWITCH (ToggleSwitch_ActionRequested ->
    ToggleService.ToggleToRigMode) -- a call path no prior round of this session ever
    exercised or examined; (2) a second real-world occurrence of round 15's Fix-A
    automatic-retry pattern, providing a second data point for the retry-timing question.
  item_1_finding: >
    CONFIRMED, by direct source read, as a genuine, new, fully-deterministic (Bohrbug, not
    Heisenbug) code-level defect in a DIFFERENT subsystem than anything examined in rounds
    8-16: ToggleService.ToggleToRigMode() (src/RigToggle.Core/ToggleService.cs, lines 90-91,
    111) reads settings.MonitorsToDisable/MonitorsToEnable directly from
    ISettingsStore.Load() and passes them AS-IS into
    WindowsMonitorController.ActivateMonitors(), with zero staleness filtering.
    ActivateMonitors' own early-availability guard (WindowsMonitorController.cs lines
    499-511) queries live PathInfo.GetAllPaths() and throws IMMEDIATELY (confirmed: EXIT
    throwing logged ~2ms after ENTER, before any TryBuildScopedActivationPlan/CacheLiveModes
    logging -- exactly matching the guard's position in the method, before any of that
    machinery runs) the instant ANY requested path isn't found live -- here, SAM748A
    (requested, enable-set) and DELA0B8 (disable-set). Because ToggleToRigMode's Monitor
    step is stop-on-first-failure (D-04), this ALSO blocks the legitimate SAM7489
    enable/ACI24A4+DELA0BC disable in the same call -- the whole toggle does nothing.
    Traced the ROOT of the stale paths to src/RigToggle.App/SettingsForm.cs's
    BtnSaveSettings_Click (lines 1218-1234): a DELIBERATE, documented design
    ("stale/disconnected entries pass through untouched... a temporarily-unplugged rig
    monitor must not lose its configuration") unions any previously-saved device path
    GetAllMonitors() no longer enumerates with the freshly-checked grid selection, on EVERY
    Settings save, forever -- there is no expiry, no user-facing removal control (the grid
    only ever shows ROWS for currently-live monitors, per PopulateMonitorGrid; a stale path
    has no row and thus no checkbox to ever uncheck), and no "forget this stale monitor"
    button anywhere in SettingsForm.cs (grepped for Remove/Clear/Purge/forget -- none
    exists for monitor paths). SettingsForm.cs DOES detect and non-blockingly warn about
    staleness (GetStaleSavedDevicePaths/ShowStaleMonitorWarning, "settings preserved;
    reconnect the display to manage it here") -- but ONLY inside the Settings dialog itself;
    ToggleService/ToggleSwitch_ActionRequested never consult this detection at all.
    Verified SAM748A/DELA0B8 are genuine, historically-real device paths for this exact
    rig -- NOT a transcription artifact (checked character-by-character against the raw
    DATA_START/DATA_END block; both appear with the SAME shared adapter-instance segment
    "7&16485deb&0" every other device path in this whole session uses, just a different
    UID/manufacturer-code, consistent with Windows re-assigning CCD target identities after
    a replug/EDID renegotiation) -- confirmed via `grep`: SAM748A appears 23 times (with
    DELA0B8) in the OLDER resolved session .planning/debug/resolved/monitor-enable-
    reactivates-others-again.md, and DELA0B8 is a live, extensively-used device path
    throughout .planning/debug/resolved/monitor-position-resets-to-de.md (the session THIS
    one is a regression of) -- both are real monitors from earlier points in this rig's own
    history, superseded by SAM7489/DELA0BC at some point before this session began, with the
    old identities never pruned from settings.json.
    Answering item 1's question 1 directly and honestly: the ORIGINAL Symptom 1 complaint
    ("Windows enumerates display 3, app shows display 2" / numbering swap) was NEVER
    independently confirmed resolved across rounds 8-16 -- Symptom 1's own "Reproduction"
    field (top of this file) explicitly says "Trigger control (dashboard tile vs. Rig/Normal
    toggle switch) not yet isolated -- needs follow-up," and every single evidence entry in
    rounds 8-16 traces an OnTileAction-driven call (single device path, monitorSwapDisableSet
    empty) -- NONE exercise ToggleSwitch_ActionRequested/ToggleToRigMode's multi-path,
    settings.json-sourced call. The investigation fully pivoted to the SAM7489 D-05/retry
    mechanism (a tile-click-only phenomenon) and never returned to isolate or test the
    toggle-switch path. This is the honest gap, not a re-discovery of anything already fixed.
    Whether THIS specific stale-path defect is what originally caused the numbering-swap
    complaint cannot be proven (no way to know what settings.json contained or which control
    the user used at the time of the original report) -- but the MECHANISM is directly
    plausible: a toggle that silently no-ops (stops before ever reaching the CCD layer, exact
    zero topology mutation, confirmed by ReconcileModeAfterMonitorFailure's own "no
    observable topology change" trace) while MainForm.ArmIntentGuard still bakes in whatever
    the (unchanged) live state happens to be as "the deliberately intended new state" is
    exactly the shape of an app/Windows disagreement, without needing any position-cache or
    source-selection defect at all.
    Ancillary, directly-evidenced compounding finding: MainForm.ToggleSwitch_ActionRequested's
    own ArmIntentGuard() call (MainForm.cs line 647) is UNCONDITIONAL -- never gated by
    result.Success, unlike OnTileAction's round-9 Fix J. This is EXACTLY the gap round 9's
    own blind_spots flagged and left open ("MainForm.cs has at least one other
    ArmIntentGuard() call site... in the Rig/Normal toggle switch handler that was NOT
    reviewed or changed this round... flagged as an open item for a future round if a
    toggle-triggered... recurrence is reported") -- confirmed FIRING in this exact log
    (ArmIntentGuard armed at 17:11:01.853 and again at 17:11:04.750, immediately after each
    failed toggle attempt, baking in the accidental-but-unchanged state as "intended").
    NOT implemented as a fix this round -- flagged for the same decision checkpoint as the
    primary defect, since fixing it in isolation would not address the underlying stale-path
    failure itself.
  item_3_finding: >
    Source-level comparison (not assumption): MainForm.OnTileAction's manual call and
    ActivateMonitors' own internal Fix-A retry (WindowsMonitorController.cs, the `for`
    loop at lines 597-795, `continue` at line 788) are CONFIRMED to be the exact same
    method, same call stack, same thread, with NO discoverable code-level difference beyond
    elapsed time. Program.cs carries `[STAThread]` -- the whole app runs on one UI/STA
    thread; OnTileAction is a synchronous WinForms event handler that calls
    _monitorController.ActivateMonitors(...) directly (confirmed by every round's log:
    "OnTileAction: calling ActivateMonitors" and "ActivateMonitors: ENTER" land in the same
    millisecond, no BeginInvoke/Task.Run in between). The internal retry is a `continue`
    inside that SAME ActivateMonitors call's own `for` loop -- it runs on the identical
    thread, in the identical stack frame, re-querying PathInfo.GetActivePaths()/GetAllPaths()
    via the identical code path a fresh manual call would use. Checked directly for a
    COM/STA-related difference (not dismissed without looking): WindowsMonitorController.cs
    imports only WindowsDisplayAPI/WindowsDisplayAPI.DisplayConfig/.Native.DisplayConfig --
    no DllImport, ComImport, or Marshal.* calls anywhere in this file; PathInfo/
    PathDisplaySource wrap plain user32.dll P/Invoke exports (QueryDisplayConfig/
    SetDisplayConfig), which are NOT COM interfaces -- STA/MTA apartment state is
    structurally irrelevant to this call path. (The app's one genuine COM interop,
    IPolicyConfig, is the audio subsystem -- a completely separate component, never on this
    call path.) Since both the manual click and the internal retry run synchronously on the
    same blocking UI-thread call, the WinForms message pump is equally NOT pumping during
    either one -- no re-entrancy/message-pump distinction exists either.
    CONCLUSION: no discoverable code-level difference exists between a manual re-click and
    an appropriately-delayed automatic retry -- confirmed, not merely restated from round 15.
    The ONLY difference is elapsed wall-clock time before the live CCD re-query.
    Timing math (both baselines computed, as asked): this round's automatic retry fired at
    17:12:59.638, 1.362s after the original request (17:12:58.276) -- essentially identical
    to round 15's 1.358s gap, and landed on the SAME "0 candidate PathDisplaySource(s)"
    degraded state round 15 first observed (now 2-for-2 on this exact pattern). The manual
    re-click fired at 17:13:02.834: 2.363s after the D-05 throw (17:13:00.471), or 4.558s
    after the ORIGINAL first-attempt request (17:12:58.276) -- and succeeded cleanly
    (EXIT success at 17:13:03.612, 3.141s after the D-05 throw). Compared against round 13's
    manual-recovery data point (~6.58s between the failing and succeeding attempt's own
    timestamps, 23:06:46.868 -> 23:06:53.447): two independent real-world manual recoveries
    now exist, at ~4.56s and ~6.58s elapsed (measuring the same way, failing-attempt-start to
    next-attempt-start), both clearing whatever transient condition is present; two
    independent automatic-retry failures now exist, both at a consistent ~1.36s gap, both
    landing on the identical "0 candidates" shape. This is a real strengthening of round 15's
    single-data-point finding (now n=2 on each side, both self-consistent, no counterexample
    yet on either side) -- but still not enough to confidently pick a SPECIFIC delay
    parameter (the true minimum sufficient delay remains unknown, only bounded loosely
    between "~1.36s insufficient" and "~4.56s sufficient" from these two pairs) and still
    only two data points, not a statistically solid sample. Per this investigation's own
    standing discipline, this is reported as meaningfully strengthened evidence for Fix K's
    underlying premise, presented to the user as a decision input, NOT implemented this
    round.
next_action: "SUPERSEDED by round 20 below -- round 19's item A (Option A2) and item B
  (Option B1) were BOTH approved by the user and BOTH implemented and self-verified this
  round (full build + RigToggle.Tests + RigToggle.Windows.Tests build, both green; new unit
  tests added for both the new pure-logic seam and the new message-builder). Round 18's items
  1 (stale-device-path) and 3 (Fix-A retry-timing / fix K) STILL have NO real-world
  confirmation from the round-18 patched build -- this remains separately, and additionally,
  outstanding; NOT superseded or resolved by this round's work. See round_20_reasoning_checkpoint
  below for the fix rationale, and the round-20 Resolution addendum for the full
  self-verification detail and guardrail verdicts (recorded separately per item)."

round_19_reasoning_checkpoint:
  context: "A fresh rig debug.log excerpt (17:33:34-18:48:23) was supplied, explicitly flagged
    as captured on the PRE-round-18 build (before this session's round-18 fixes for item 1
    [stale-device-path live-filtering] and item 3 [fix K poll-until-reachable] were built) --
    the user is testing the round-18 patched build separately, so this excerpt neither
    confirms nor refutes round 18's fixes. It surfaces a genuinely new, more severe failure
    shape: enabling ACI24A4 via a tile click (18:48:06.068) causes an UNRELATED monitor
    (SAM7489) to end up OFF, with the error message naming SAM7489 -- a monitor the user
    never touched -- instead of anything about ACI24A4 (which actually DID turn on
    successfully). Two questions were assigned: (1) verify the full sequence directly against
    source with line numbers; (2) determine whether fix A/fix K's retry-then-poll mechanism
    structurally covers ActivateMonitors' own nested (fix-H-correction) call, or is
    structurally excluded from it; (3) assess whether the resulting error message/propagation
    path is genuinely indistinguishable, to the user, from a direct failure of their own
    request; (4) classify and decide whether/what to fix now."
  hypothesis: "(1) SEQUENCE -- CONFIRMED, byte-for-byte, against WindowsMonitorController.cs
    and the raw log: 17:33:xx shows two ordinary DISABLE tile clicks (DELA0BC, then ACI24A4),
    both clean. 18:48:01.165 is the Rig-mode toggle-switch attempt, requested=[SAM748A,
    SAM7489] disableSet=[DELA0B8, ACI24A4, DELA0BC] -- this hits the early
    missing-target guard (lines 502-511, PathInfo.GetAllPaths()+IsAvailable check) and throws
    'not detected: SAM748A' in the SAME millisecond as ENTER (0ms elapsed) -- CONFIRMED this
    IS the already-diagnosed item-1 stale-device-path defect (SAM748A/UID521 and DELA0B8/
    UID516 are superseded-but-never-purged CCD identities for the SAME two monitors now
    identified as SAM7489/DELA0BC, per round 17's own finding), fixed by round 18's
    LiveFilterMonitorSets -- but THIS excerpt predates that fix, so its recurrence here is
    expected, not a new finding. At 18:48:06.068, MainForm.OnTileAction fires for ACI24A4
    ENABLE; only SAM7489 is active at this point (preExtendActive=[SAM7489], line 513). The
    OUTER ActivateMonitors(ACI24A4) call (isPartOfMonitorSwap=False): TryBuildScopedActivationPlan
    reclaims sourceId=0 for ACI24A4 (fix B, round-14 source-preference match) and builds a
    scoped plan (line 636-639), but PathInfo.ApplyPathInfos throws PathChangeException at
    line 643 (caught at line 647-649) in the SAME logged millisecond as the plan-built line --
    0ms elapsed, confirmed immediate -- falling back to whole-topology ApplyTopology(Extend)
    (lines 654-660). Extend's post-apply settle poll (line 691, PollUntilStableActiveDevicePaths)
    stabilizes on [ACI24A4, DELA0BC] -- Extend correctly activated the requested ACI24A4 but
    ALSO collaterally reactivated DELA0BC (independently disabled at 17:33:34, unrelated to
    this call) AND dropped SAM7489 (active going in, not part of any swap-disable-set --
    monitorSwapDisableSet is empty here). Correction round 1/3 (line 689 onward):
    ComputeUnexpectedlyActivated (lines 1227-1235: active now, inactive before [{SAM7489} was
    the pre-call set], not requested [{ACI24A4}]) = {DELA0BC}; ComputeUnexpectedlyDeactivated
    (lines 1258-1266: active before [{SAM7489}], not in monitorSwapDisableSet [empty], not
    active now) = {SAM7489} -- exactly matching the log's own
    'unexpectedlyActivated=[DELA0BC] unexpectedlyDeactivated=[SAM7489]' line. Fix H fires as
    designed: DeactivateMonitors(DELA0BC) at line 731 succeeds (verified activeAfter=[ACI24A4]);
    then, because unexpectedlyDeactivated is non-empty, line 744 makes a NESTED
    ActivateMonitors(SAM7489, monitorSwapDisableSet: empty) call to restore the
    unexpectedly-dropped survivor -- CONFIRMED lines 735-746 wrap this nested call with NO
    try/catch of any kind. That NESTED call re-enters the SAME method as a fresh invocation:
    its own currentlyActiveDevicePaths=[ACI24A4] (its own QueryActiveDevicePaths() read, line
    486, independent of the outer call's), its own TryBuildScopedActivationPlan reclaims
    sourceId=2 for SAM7489 (fix B match again), its own scoped ApplyPathInfos ALSO throws
    PathChangeException (line 643/649, again 0ms) -> falls back to its OWN Extend call. The
    nested call's OWN post-Extend settle poll ALSO stabilizes on [ACI24A4, DELA0BC] -- Extend
    collaterally reactivated DELA0BC a SECOND time (independent recurrence of the exact same
    unrelated side effect) and did NOT bring SAM7489 online at all. The nested call's OWN
    correction round 1/3: ComputeUnexpectedlyActivated (relative to ITS OWN pre-call baseline
    of {ACI24A4}) = {DELA0BC} again -> DeactivateMonitors(DELA0BC) succeeds again;
    ComputeUnexpectedlyDeactivated (relative to the SAME baseline {ACI24A4}, which never
    included SAM7489) = {} -- SAM7489 was never part of the nested call's own 'previously
    active' set, so it is invisible to fix H's mirror-image correction from THIS call's
    perspective; it is purely 'the requested target that never came active,' a completely
    different bucket (requestedStillInactive / ShouldRetryScopedActivation), never fix H's
    concern. Rounds 2/3 of the nested call's own correction loop both come back clean
    ([ACI24A4] stable) -> consecutiveCleanRounds reaches 2 -> breaks. The nested call's own
    verify-and-throw (lines 774-778): postCorrectionActiveDevicePaths={ACI24A4};
    requestedStillInactive={SAM7489}; survivorStillInactive={} (ACI24A4, the nested call's
    only pre-existing survivor, IS present) -> stillInactive={SAM7489}.
    ShouldRetryScopedActivation(usedScopedActivation=False, requestedStillInactiveCount=1,
    attemptNumber=1, maxRetryAttempts=2) evaluates to False at line 1291
    (`usedScopedActivation && ...` short-circuits on the first operand) -- fix A/fix K's
    retry+poll block (lines 785-802) is never entered; execution falls straight to lines
    804-807: 'EXIT throwing -- still inactive after correction: [SAM7489]', throwing
    InvalidOperationException('Monitor enable did not take effect: SAM7489. No further
    automatic recovery is attempted (D-05).'). This exception is UNCAUGHT at the nested call
    site (line 744, no try/catch) and propagates directly out of the OUTER correction loop's
    round-1 body, out of the outer `for(round...)` loop, out of the outer
    `for(attemptNumber...)` loop, and out of the OUTER ActivateMonitors(ACI24A4) call entirely
    -- confirmed by the log's own absence of a 'correction round 1/3 nested
    ActivateMonitors(SAM7489) completed without throwing' line (which only prints AFTER line
    744 returns normally, per line 745) anywhere in the excerpt; the very next line is
    MainForm.OnTileAction's own catch block reporting 'ActivateMonitors(ACI24A4) threw
    InvalidOperationException: Monitor enable did not take effect: SAM7489' -- i.e. the OUTER
    call, invoked for the user's ACI24A4 tile click, never reaches its own round-2/3
    correction, its own verify-and-throw, or its own EXIT success line; it simply propagates
    the NESTED call's exception verbatim. Final observed state (18:48:08.188 RefreshMonitorTiles):
    ACI24A4 active (genuinely restored by the outer call's own Extend, before the nested
    call's exception ever fired), DELA0BC inactive (correctly re-suppressed, twice), SAM7489
    OFF (never restored) -- confirming every one of the six numbered claims in the assigned
    task, with line numbers, not just the log's own narrative. The passive
    TryReactivelyCorrectAgainstLastIntent watchdog (MainForm.cs) does detect this drift
    correctly at 18:48:08.189/.681/.838/.077 but is skipped all four times with 'a
    toggle/monitor action is already in progress' and never recovers within this excerpt.
    (2) ITEM A MECHANISM -- CONFIRMED (a), REFUTED (b): the retry loop
    (`for(attemptNumber=1; attemptNumber<=MaxScopedActivationRetryAttempts+1; ...)`, line 597)
    and the fix-A/fix-K eligibility+poll block (lines 785-802) live INSIDE ActivateMonitors'
    own method body, wrapping the ENTIRE scoped-build+apply+settle+correct sequence, with NO
    isPartOfMonitorSwap gate, NO nested-call flag, and NO special-casing anywhere for
    recursive vs. top-level invocations. A nested call (line 744) re-enters this SAME method
    and gets its OWN fresh `attemptNumber` local starting at 1 and its OWN independent
    MaxScopedActivationRetryAttempts=2 budget -- structurally, fix A/fix K's mechanism DOES
    already cover nested calls, with zero code change needed to 'reach' them. In THIS trial it
    simply did not fire because ShouldRetryScopedActivation's own, already-existing,
    already-approved gate (line 1291) evaluated false: usedScopedActivation was False for the
    nested call's one attempt, because ITS OWN scoped ApplyPathInfos also threw
    PathChangeException and fell back to Extend -- and per round 13/14's own explicit,
    already-documented design (comment lines 1274-1277: an Extend-fallback attempt 'is a
    DIFFERENT failure shape than the one round 13 proved recoverable, and is deliberately
    never retried by this mechanism'), this exclusion applies regardless of call depth. This
    is the IDENTICAL exclusion round 15/17 already found and documented for the TOP-LEVEL
    case -- newly OBSERVED here at a DIFFERENT call site (the nested fix-H correction), but the
    SAME mechanism, not a new one and not a structural nested-call gap.
    (3) ITEM B UX -- CONFIRMED, a genuine, structural defect, independent of the underlying
    flakiness: D-05's message (lines 805-807) is built purely from `stillInactive` (a bare
    device-path list) with NO indication anywhere in the string of (a) which of the two
    ActivateMonitors call frames (outer/user-requested vs. nested/fix-H-correction) produced
    it, or (b) that the user's own actually-requested target already succeeded. The exception
    TYPE itself (a plain InvalidOperationException) carries no marker distinguishing 'primary
    request failure' from 'collateral-restore failure' either. MainForm.cs's ENABLE-branch
    catch (lines 1350-1354) surfaces this via `MessageBox.Show(this, ex.Message, ...)` --
    `ex.Message` passed VERBATIM. The `devicePath` local (ACI24A4, the user's actual click
    target) IS in scope at that catch site and IS used in the adjacent
    Trace.WriteLine-only debug line (line 1352-1353) but is NEVER included in the user-facing
    MessageBox text (line 1354). Confirmed: a user clicking ACI24A4 -- whose click actually
    succeeded -- sees a dialog reading 'Monitor enable did not take effect: SAM7489...', a
    monitor they never interacted with, framed identically to a direct failure of their own
    request. This gap is structural (no origin marker anywhere in the exception's type or
    message construction path), not a one-off wording nitpick -- it recurs identically any
    time the nested correction's own bounded retry budget is exhausted for ANY reason, even if
    fix A/fix K's retry mechanism were made perfectly reliable.
    (4) CLASSIFICATION -- SAME root_cause (8) class (the still-genuinely-unconfirmed
    ApplyPathInfos/PathChangeException fragility for this SAM748A/SAM7489+ACI24A4/DELA0Bx
    pairing), newly OBSERVED at a DIFFERENT call site (nested, inside fix H's own correction,
    rather than the top-level tile-click case rounds 13-18 focused on) -- not a
    mechanistically distinct defect, and no new root cause is being claimed."
  confirming_evidence:
    - "Direct source read, WindowsMonitorController.cs lines 464-820 (ActivateMonitors in
      full): confirms the exact control-flow trace above -- the outer/nested call boundary at
      line 744, the absence of any try/catch around it, ComputeUnexpectedlyActivated/
      ComputeUnexpectedlyDeactivated's exact definitions (lines 1227-1266), and
      ShouldRetryScopedActivation's exact gate expression (line 1291)."
    - "Timestamp-level log trace: outer call's scoped ApplyPathInfos throw at 18:48:06.069 and
      nested call's scoped ApplyPathInfos throw at 18:48:06.907 both land in the SAME
      millisecond as their own respective 'plan built' log line -- confirms 0ms elapsed,
      immediate PathChangeException, for both the outer AND nested attempts."
    - "Log line-by-line comparison confirms NO 'round 14 (fix A)' or 'round 18 (fix K)' log
      line appears anywhere between the nested call's ENTER (18:48:06.903) and its own EXIT
      throwing (18:48:07.866) -- consistent with ShouldRetryScopedActivation short-circuiting
      to false before either logged branch (lines 787/799) is ever reached."
    - "Confirmed absence of a 'correction round 1/3 nested ActivateMonitors(SAM7489) completed
      without throwing' log line (the line at 745, printed only on normal return from line
      744) anywhere in the excerpt -- direct evidence the nested call's exception propagated
      rather than returning."
    - "MainForm.cs lines 1345-1361 (the ENABLE branch's try/catch): direct read confirms
      `ex.Message` is passed unmodified to MessageBox.Show at line 1354, and that the in-scope
      `devicePath` variable is used only in the Trace.WriteLine at lines 1352-1353, never in
      the user-facing dialog text."
  falsification_test: "If a future rig trial shows the nested ActivateMonitors call's own
    scoped ApplyPathInfos succeeding (usedScopedActivation=True) while its requested target
    still ends up inactive, AND fix A/fix K's retry+poll log lines (round 14/round 18) DO NOT
    appear for that nested call, THEN item A's mechanism finding is refuted -- it would mean a
    genuine structural gap exists beyond the Extend-fallback exclusion already documented.
    Absent that, this trial's specific non-firing is fully explained by the pre-existing,
    already-approved gate."
  fix_rationale: "NOT APPLIED THIS ROUND -- see next_action and the round-19 checkpoint below.
    Both items are presented as decisions, not auto-implemented, per this investigation's own
    standing discipline (see and_gate)."
  blind_spots:
    - "Cannot execute or verify on real Windows CCD hardware in this sandbox -- same
      structural limitation as every round of both debug sessions. This round is a pure
      source-verified log-reconstruction; no code was changed or built this round."
    - "This excerpt is explicitly PRE-round-18 -- it does not tell us whether round 18's item-1
      (stale-device-path live-filtering) or item-3 (fix K poll-until-reachable) changed
      anything about this specific nested-call trace, since neither fix was in the tested
      build. Whether fix K's poll-until-reachable would have helped even if it HAD fired for
      the nested call (i.e. whether SAM7489 would have reported itself 'reachable' sooner) is
      untested and unknown."
    - "Does not re-litigate or re-examine root_cause (8)'s own still-open, genuinely-unexplained
      trigger (WHY the scoped ApplyPathInfos plan throws PathChangeException for this specific
      monitor pairing in the first place) -- carried forward unchanged, exactly as every prior
      round has done."
  candidate_causes:
    - "code (already-approved, deliberately-narrow guardrail): ShouldRetryScopedActivation's
      `usedScopedActivation` condition (line 1291) excludes Extend-fallback attempts from
      fix A/fix K's retry+poll, a decision made and approved in rounds 13-15/17 for the
      TOP-LEVEL call and which applies equally, unmodified, to the nested call -- this is why
      the nested call's own D-05 fired with no retry attempt."
    - "code (control-flow, pre-existing since round 7's fix H, commit 4777c40): the nested
      ActivateMonitors call at line 744 has no try/catch, so any exception it throws
      propagates through the outer call as-is, with no context added identifying it as a
      collateral-restore failure rather than a primary-request failure -- this is a SEPARATE
      code-level gap from the retry-eligibility gate above, in a different part of the same
      method (exception propagation vs. retry eligibility)."
    - "environment/CCD-driver (pre-existing, root_cause (8), NOT re-investigated this round):
      the actual trigger for why the scoped ApplyPathInfos plan throws PathChangeException for
      this specific monitor pairing -- both the outer AND nested attempts hit this same
      unconfirmed trigger in this trial, back-to-back."
  and_gate: "YES -- this failure required BOTH the pre-existing environment/CCD-driver
    PathChangeException trigger (root_cause (8), firing on BOTH the outer AND nested attempts,
    independently) AND the already-approved, deliberately-narrow retry-eligibility gate
    (usedScopedActivation) to combine before the nested call's D-05 could surface uncaught --
    if either had been absent (a scoped plan that did not throw, OR a retry mechanism that
    also covered Extend-fallback attempts) this specific propagation would not have occurred
    in this shape. The UX/message-clarity gap (item B) is a SEPARATE, additive condition on
    top of that combination, not part of the AND-gate itself -- it determines how BADLY the
    resulting failure surfaces to the user, not whether the failure occurs."
  next_action: "Present items A and B to the user as a CHECKPOINT for a decision on next
    steps, per this investigation's own standing discipline of not implementing a fix in the
    same round it is newly characterized unless it is a trivially safe, narrowly-scoped
    extension of already-decided logic. Item A (does fix A/fix K's retry+poll extend to cover
    a nested call whose own attempt hit Extend-fallback) requires CHANGING (loosening) the
    already-approved, deliberately-narrow ShouldRetryScopedActivation gate itself -- a
    control-flow change to a shipped guardrail, not a mechanical extension to a location it
    already structurally reaches -- so this needs its OWN decision, not an auto-implement.
    Item B (error-message/propagation clarity) is comparatively low-risk (message-text/
    exception-enrichment, not control-flow) but still has more than one reasonable shape (an
    exception-origin marker vs. a try/catch-and-rewrap at the nested-call site) -- per the
    task's own instruction to err toward a checkpoint when uncertain, this is ALSO presented as
    a decision, not auto-implemented. Do not implement either fix until the user responds."

## Evidence

- timestamp: 2026-08-29T00:10:00Z
  checked: .planning/debug/resolved/monitor-position-resets-to-de.md Resolution section (root_cause/fix/files_changed) and Final Closure section, full read
  found: The resolved session closed with 8 confirmed-and-fixed root causes (fixes A-H) plus two explicitly-still-open items (Symptom 2 part B's OS-level ~1.5s delayed revert trigger, root-cause (3); and the still-unexplained SAM748A+ACI24A4-specific PathChangeException trigger noted in root_cause (8)). Fix H (round 7, the LAST fix in that session) added ComputeUnexpectedlyDeactivated + a nested nonswap-aware ActivateMonitors correction call for a previously-active survivor accidentally dropped by the Extend fallback.
  implication: Fix H is the newest, least rig-battle-tested code in the file (rig-confirmed clean only once, round 8, on a trial where "nothing to correct" -- ComputeUnexpectedlyDeactivated fired zero times that round per Resolution.verification.revert_and_reconfirm) -- a strong candidate for where a still-latent gap could live.

- timestamp: 2026-08-29T00:15:00Z
  checked: git log 4777c40..HEAD --name-only (full commit range from the resolved session's closure to HEAD)
  found: 21 commits since closure; only 3 touch any monitor-related source file (MainForm.cs, all three are About-dialog/update-check UI additions unrelated to monitor logic); zero commits touch WindowsMonitorController.cs, ToggleService.cs, or IMonitorController.cs.
  implication: Rules out "a recent code change broke this" as a literal new-regression-via-edit explanation (contradicts the user's own "app update" attribution as a CAUSE, though the timing correlation -- noticing it after an update -- is still plausible if the update simply increased exposure/usage of the already-shipped fix H path). Points at a latent defect in already-shipped code (fix H) rather than a fresh code change.

- timestamp: 2026-08-29T00:25:00Z
  checked: src/RigToggle.Windows/WindowsMonitorController.cs, full read (1367 lines) -- ActivateMonitors (lines 377-654), TryBuildScopedActivationPlan (708-830), CacheLiveModes (665-674), ComputeUnexpectedlyDeactivated (959-967), DeactivateMonitors (1183 onward)
  found: CacheLiveModes is called in exactly two places: DeactivateMonitors (deliberate removal, always) and ActivateMonitors' `if (isPartOfMonitorSwap)` block (only for monitorSwapDisableSet-matching paths). Fix H's nested correction call (line 610) reactivates unexpectedlyDeactivated paths, which by ComputeUnexpectedlyDeactivated's own definition (line 965) explicitly EXCLUDES monitorSwapDisableSet paths -- so the two "gets cached" code paths and the "gets fix-H-nested-reactivated" code path are mutually exclusive by construction. No cache entry can ever exist for a path fix H's correction targets.
  implication: Confirmed root cause -- direct code read, not inference. TryBuildScopedActivationPlan's blank-mode fallback (lines 819-822) is exercised every time fix H's correction fires, resurrecting the exact Symptom-1 defect class the resolved session's round-5 fix was built to prevent, via a code path that fix never covered.

- timestamp: 2026-08-29T00:30:00Z
  checked: src/RigToggle.App/MainForm.cs lines 727-740 (RefreshMonitorTiles' canonical ordering comment + implementation)
  found: "the ONE canonical DevicePath-ordered monitor list. Tile position, tile number, AND (Plan 19-03) the Identify overlay numbers all [derive from] this sort" -- `.OrderBy(m => m.DevicePath, StringComparer.Ordinal)`. Purely a function of the device's own stable identity string, recomputed identically every refresh regardless of CCD source/position/active-path enumeration order.
  implication: Rules out "the app's own tile-numbering logic reordered" as an explanation for the numbering-swap symptom -- the app side is provably stable. The swap must originate from Windows' own Display Settings numbering shifting (external to this app), which is consistent with (though not yet independently rig-confirmed as caused by) a PathDisplaySource reassignment during fix H's nested reactivation of a monitor with no cached mode.

## Eliminated

- hypothesis: "A recent code change to the monitor toggle/CCD logic itself introduced this regression (as the user's own 'attributed to a recent app update' framing suggested)."
  evidence: "git log 4777c40..HEAD --name-only shows zero commits touching WindowsMonitorController.cs, ToggleService.cs, or IMonitorController.cs since the resolved session's own closure commit. The only monitor-adjacent file touched (MainForm.cs) was touched exclusively by unrelated About-dialog/update-check-menu commits, confirmed by reading each commit's diff scope."
  timestamp: 2026-08-29T00:20:00Z

- hypothesis: "(Round 9, hypothesis 1 as literally proposed) An uncaught TargetNotAvailableException from PollUntilStableActiveDevicePaths is the SAME kind of gap fix H's correction had (a code-level cache/coverage gap), not a genuinely different mechanism."
  evidence: "Direct code read confirms it IS a genuinely different mechanism in a different method: PollUntilStableActiveDevicePaths has zero exception handling around QueryActiveDevicePaths() (unlike ObservePostApplyStability's per-tick try/catch), and an uncaught exception here aborts ActivateMonitors' correction loop, verify-and-throw, AND ObservePostApplyStability call entirely -- a strictly worse failure mode than fix H's gap (a wrong-but-non-throwing EXIT success). Not eliminated as a hypothesis -- CONFIRMED as an additional, independent defect (see round_9_reasoning_checkpoint) -- listed here only to record that it was checked against 'is this just fix H's gap again' and found to be genuinely separate."
  timestamp: 2026-08-29T01:15:00Z

## Evidence (round 9 -- fresh debug.log excerpt)

- timestamp: 2026-08-29T01:00:00Z
  checked: "Freshly-supplied rig debug.log excerpt (21:48:51.133-22:00:50.853, PID 10600, same session as round 8's investigation but received after round 8 had already started -- not seen by round 8). Full excerpt reviewed line-by-line, cross-referenced against WindowsMonitorController.cs and MainForm.cs source."
  found: >
    Key sequence at 22:00:14.809-22:00:17.925 (ActivateMonitors(SAM7489) called from a tile-enable click):
    "[22:00:15.081] ... round 5 -- scoped activation ApplyPathInfos completed without throwing." then
    "[22:00:15.098] ... Post-Extend settle poll, attempt 1/5: [ACI24A4, SAM7489]" then
    "[22:00:15.349] ... poll tick ... failed (TargetNotAvailableException...) -- skipping this tick, observation continues." (this line is the UNRELATED, already-hardened ObservePostApplyStability background thread from the PRIOR DeactivateMonitors call) immediately followed by
    "[22:00:15.349] MainForm.OnTileAction: ActivateMonitors(...SAM7489...) threw TargetNotAvailableException: WindowsDisplayAPI.Exceptions.TargetNotAvailableException: Extra information about the target is not available.
       at WindowsDisplayAPI.DisplayConfig.PathDisplayTarget.get_DevicePath()
       at System.Linq.Enumerable.IteratorSelectIterator`2.MoveNext()
       at System.Linq.Enumerable.ToHashSet[TSource](IEnumerable`1 source)
       at RigToggle.Windows.WindowsMonitorController.QueryActiveDevicePaths()
       at RigToggle.Windows.WindowsMonitorController.PollUntilStableActiveDevicePaths()
       at RigToggle.Windows.WindowsMonitorController.ActivateMonitors(IReadOnlySet`1 monitorDevicePaths, IReadOnlySet`1 monitorSwapDisableSet)
       at RigToggle.App.MainForm.OnTileAction(MonitorTile tile)"
    Then, over the next ~10 seconds, the PRIOR action's still-running ObservePostApplyStability background thread independently observes "now=[ACI24A4, DELA0BC]" (DELA0BC unexpectedly reactivating while SAM7489, the intended target, never came active). At 22:00:17.412 and 22:00:17.925, OnDisplaySettingsChanged correctly detects this as drift against the STILL-VALID prior intent snapshot ("reactivated=[DELA0BC] (should be inactive)") but both attempts log "reactive correction skipped -- a toggle/monitor action is already in progress." At 22:00:25.492, ArmIntentGuard finally (re-)arms with intent=[ACI24A4:active, DELA0BC:active, SAM7489:inactive] -- the WRONG, accidental post-failure state, baked in as the new baseline. A SECOND, manual-looking tile click on SAM7489 at 22:00:27.212 subsequently succeeds cleanly.
  implication: >
    Confirms two additional, independent defects beyond round 8's already-applied CacheLiveModes fix: (1) PollUntilStableActiveDevicePaths' unguarded QueryActiveDevicePaths() call lets a transient TargetNotAvailableException abort ActivateMonitors before ANY of its own correction/verify/observe machinery runs, even though the CCD mutation itself had already succeeded; (2) OnTileAction's finally block re-arms ArmIntentGuard() unconditionally, so once it eventually ran, it captured and permanently baked in the wrong, accidental post-failure state as "intended" -- explaining why no further correction was ever attempted for this specific drift even though the reactive watchdog had already correctly detected it twice while the guard was still (correctly) blocked by the in-progress busy lease.

- timestamp: 2026-08-29T01:10:00Z
  checked: "git show dc93b66:src/RigToggle.Windows/WindowsMonitorController.cs and git show 4777c40:... (same file, two points in history) -- grep for PollUntilStableActiveDevicePaths, QueryActiveDevicePaths, ObservePostApplyStability, and the per-tick 'catch (Exception tickEx)' line."
  found: "PollUntilStableActiveDevicePaths already exists, fully unguarded (no catch of any kind around its two QueryActiveDevicePaths() calls), in commit dc93b66 -- chronologically BEFORE 4777c40 (the resolved session's own closure commit). ObservePostApplyStability, WITH its per-tick try/catch already in place, exists only starting at 4777c40 (added later, round 6 per that method's own doc-comment narrative)."
  implication: "Confirms hypothesis 1(b): PollUntilStableActiveDevicePaths predates the per-tick hardening pattern entirely -- it is not that the pattern was applied inconsistently at the SAME time, but that a later fix (round 6, for a newer method) was never back-applied to this older method exhibiting the identical fragility. This is a genuinely pre-existing gap, not something introduced by round 8's own CacheLiveModes change this session."

- timestamp: 2026-08-29T01:18:00Z
  checked: "src/RigToggle.Core/ToggleOrchestrator.cs, full read (RunGuarded, BeginExclusiveMonitorAccess, ExclusiveMonitorAccessLease) -- and src/RigToggle.App/MainForm.cs, OnTileAction full body (both branches) plus TryAcquireMonitorAccess, ArmIntentGuard, TryReactivelyCorrectAgainstLastIntent."
  found: "_busy is released via Volatile.Write inside ExclusiveMonitorAccessLease.Dispose() (guarded by an Interlocked.Exchange double-dispose check) -- and OnTileAction acquires the lease via TryAcquireMonitorAccess() and immediately wraps its entire try/catch/finally body in `using (lease)`. RunGuarded (the Rig/Normal toggle's own guard path) also releases _busy in its own `finally`. Neither has a missing try/finally. BUT: OnTileAction's `finally { RefreshMonitorTiles(); ArmIntentGuard(); }` (both the enable and disable branches, before this round's fix) calls ArmIntentGuard() with no conditional on whether the preceding try block's ActivateMonitors/DeactivateMonitors call actually completed without throwing."
  implication: "Refutes hypothesis 2 AS LITERALLY STATED (a stuck boolean/lease) -- the busy-flag guard is correctly released every time. CONFIRMS a different, real defect: the intent-guard re-arm itself is unconditional, so a thrown exception's accidental resulting state gets armed as if it were the deliberately-intended one -- matching the log's own direct evidence (22:00:25.492 ArmIntentGuard entry) exactly."

## Evidence (round 10 -- fresh debug.log excerpt, checkpoint response to round 9's ask)

- timestamp: 2026-08-29T02:00:00Z
  checked: "Freshly-supplied rig debug.log excerpt (22:29:59.437-22:30:08.619, PID 12668, this session's first-ever tile action -- STARTUP through the second OnDisplaySettingsChanged firing). Full excerpt reviewed line-by-line against current WindowsMonitorController.cs and MainForm.cs source. User explicitly did NOT say 'confirmed fixed' -- supplied this log for assessment after a fresh rig test."
  found: >
    ActivateMonitors(SAM7489 / 'Odyssey G5', branch=ENABLE, isPartOfMonitorSwap=False) ran:
    (1) '[22:30:05.824] ... cached live mode for all currently-active paths=[ACI24A4, DELA0BC] ...' --
    fix 1's widened, unconditional CacheLiveModes call, byte-for-byte matching the Log() message this
    session added at line 550, fired BEFORE any topology mutation, exactly as designed.
    (2) round 5 scoped PathInfo.ApplyPathInfos 'completed without throwing' (no PathChangeException,
    no Extend fallback this trial -- a DIFFERENT trigger shape than root_cause (8)'s own rig-confirmed
    repro, which DID throw and fall back to Extend).
    (3) The post-apply settle poll then ran attempts 1/5 through 5/5: attempt 1 read [ACI24A4, DELA0BC]
    (SAM7489 absent), attempts 2/5 and 3/5 both threw TargetNotAvailableException and were tolerated --
    'skipping this tick, using last known-good reading' -- byte-for-byte matching fix 2's new per-tick
    catch (line 1096), with the poll loop CONTINUING to attempts 4/5 and 5/5 (both also [ACI24A4, DELA0BC],
    SAM7489 still absent) rather than aborting. Fix 2 held under two real, repeated transient exceptions
    in this exact trial.
    (4) Two correction rounds (1/3 and 2/3) both computed unexpectedlyActivated=[] and
    unexpectedlyDeactivated=[] -- CORRECTLY empty, since SAM7489 was never a member of the pre-call
    active baseline (preExtendActive=[ACI24A4, DELA0BC]) and so cannot appear in either check by
    construction; ACI24A4/DELA0BC themselves never flickered. Neither correction round had anything
    to act on.
    (5) ActivateMonitors then threw InvalidOperationException: 'Monitor enable did not take effect:
    [SAM7489]. No further automatic recovery is attempted (D-05).' -- the pre-existing (dc93b66/90fe29d,
    both 2026-08-22/2026-07-28, predating every fix applied this session) verify-and-throw correctly
    detected that the CALL'S OWN REQUESTED TARGET never actually became active across the full settle-
    poll+correction budget, despite the underlying CCD apply call reporting API-level success.
    (6) Two subsequent OnDisplaySettingsChanged firings both logged 'TryReactivelyCorrectAgainstLastIntent:
    SKIP -- guard has never been armed this session (no deliberate tile action yet)' -- this is a
    DIFFERENT, unrelated guard (trivially true: first tile action of the session, so no ArmIntentGuard
    call of any outcome had happened yet) and provides no signal on fix 3's own conditional-arm logic.
    The excerpt does NOT contain the 'ArmIntentGuard SKIPPED' trace line that OnTileAction's own finally
    block would log for THIS failure (activateSucceeded=false) -- that trace only runs after the
    modal MessageBox.Show() dialog (see next finding) is dismissed, which the excerpt's end (22:30:08.619,
    ~1.4s after the throw) may simply predate.
    (7) Direct code read, MainForm.cs OnTileAction enable branch: `catch (InvalidOperationException ex)
    { ...; MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning); }`
    -- confirms a modal dialog carrying the raw D-05 text was almost certainly shown to the user on this
    exact exception, i.e. this failure was NOT silent at the code/UI level (unlike the ORIGINAL resolved
    bug, which genuinely never surfaced anything).
  implication: >
    Fixes 1 and 2 are both independently confirmed firing correctly and causing no harm in this trial.
    The new failure is a THIRD observed shape (a hard, correctly-thrown D-05 exception) of the SAME
    still-open, never-root-caused driver/OS-level instability already carried forward from the resolved
    session's Resolution.root_cause (8) -- not a new, fourth, code-level defect (see round_10_reasoning_
    checkpoint for the full classification argument and git-blame confirmation that the throw/verify
    logic predates this session's own fixes). Fix 3 (OnTileAction's conditional ArmIntentGuard) is
    NEITHER confirmed nor refuted by this excerpt -- the relevant trace line is structurally absent,
    most plausibly because it only fires after the user dismisses a dialog this excerpt likely ends
    before. The user's own inherited "silent" framing does not hold for this specific failure mode --
    a technical MessageBox with the raw exception text was almost certainly displayed.

- timestamp: 2026-08-29T02:45:00Z
  checked: "User's direct follow-up answers to round 10's two open questions (relayed via checkpoint, not a log excerpt): (a) did a dialog appear, and (b) what happened next."
  found: >
    User confirms an error dialog DID appear, with text "Monitor enable did not take effect" --
    a verbatim match to the InvalidOperationException/D-05 message logged and to the MessageBox.Show
    call site identified in finding (7) above. No separate/unknown error path is involved. User also
    reports the SAM7489 enable failed twice in a row, then succeeded on a subsequent (third) attempt,
    with no code change or app restart in between.
  implication: >
    Confirms finding (7) exactly -- this failure was genuinely NOT silent, closing that open question
    from round 10's checkpoint. The "failed twice, then self-resolved on retry with no intervention"
    pattern is consistent with (not new evidence against) the same_open_item classification: an
    intermittent, transient driver/CCD-timing condition rather than a deterministic code defect, matching
    root_cause (8)'s own historical character across both sessions (10+ rounds, never reproducible on
    demand, no known deterministic trigger). Does not change the fix-3 "inconclusive" status (no new
    log data on ArmIntentGuard's post-dialog behavior was supplied). Round 10's Option A / Option B
    decision and the dialog-visibility question are now both answerable -- only the dialog question is
    answered (yes); Option A vs. B is still the user's call, not yet made.

## Evidence (round 11 -- reopened investigation, instrumentation-only)

- timestamp: 2026-08-29T03:00:00Z
  checked: "Full re-read of .planning/debug/resolved/monitor-position-resets-to-de.md's Resolution section (root_cause (8) in full, its own fix's remarks/verification, and the Final Closure section) plus this session's own round 8/9/10 candidate_causes/blind_spots entries in this file."
  found: "Root_cause (8)'s own text explicitly names three unconfirmed candidate mechanisms for why the scoped ApplyPathInfos plan misbehaves for the SAM748A/SAM7489+ACI24A4/DELA0Bx pairing specifically: a stale/incompatible GetAllPaths() source candidate picked as the first unclaimed-source match, a hardware source/port-group constraint specific to this pairing, and a cached-mode Position/Resolution mismatch against whichever source got picked -- 'no discriminating evidence, not guessed at.' The resolved session's own Final Closure section additionally names the DIFFERENT, already-independently-tracked Symptom-2-part-B item (root_cause (3), the ~1.5s delayed revert) as having already been researched (SDC_SAVE_TO_DATABASE persistence semantics, GPU-vendor auto-profile-restore, generic driver-forum reports) with nothing conclusively matching -- confirmed this is a SEPARATE, already-closed research effort, not to be repeated for root_cause (8), which this round is scoped to.
  implication: All three of root_cause (8)'s named candidate mechanisms remain genuinely open. None can be discriminated between using the app's CURRENT logging, which (confirmed by the direct code read below) never records which PathDisplaySource actually gets chosen for a requested target, nor the full candidate set available for it.

- timestamp: 2026-08-29T03:05:00Z
  checked: "Direct code read of src/RigToggle.Windows/WindowsMonitorController.cs as it stood before this round's edits -- TryBuildScopedActivationPlan's candidate-selection loop and ActivateMonitors' scoped-plan log line immediately before ApplyPathInfos."
  found: "TryBuildScopedActivationPlan's candidate selection (`allPaths.FirstOrDefault(p => !claimedSources.Contains(p.DisplaySource) && ...)`) picks the first unclaimed PathDisplaySource with NO logging of which one was picked, how many candidates existed, or whether any were excluded as already-claimed. ActivateMonitors' own pre-ApplyPathInfos log line (`scoped activation plan built for targets=[...], planPaths=[...]`) logs only a flat list of TARGET DEVICE PATHS, never the PathDisplaySource (adapter/source identity) each plan entry actually carries, nor its mode-info-available flag or position. A PathChangeException's own log line (`ex.Message`) carries only the exception's generic text ('Invalid paths information.') with no structural detail about what was submitted."
  implication: "Confirmed, by direct source review (not speculation), that the app currently captures WHETHER a scoped plan throws/succeeds and WHICH device paths end up active, but never WHICH PathDisplaySource gets assigned -- exactly the missing data needed to discriminate root_cause (8)'s 'stale/incompatible source candidate' and 'hardware source/port-group constraint' candidates from each other or from the third (cached-mode mismatch, which IS already partially observable via existing CacheLiveModes/scoped-plan logs)."

- timestamp: 2026-08-29T03:15:00Z
  checked: "Decompiled (ilspycmd) WindowsDisplayAPI 1.3.0.13's PathDisplaySource, PathDisplayAdapter, LUID, and PathTargetInfo/PathDisplayTarget classes to determine what identifying data is cheaply available (no extra native/Win32 calls) versus what requires a live query."
  found: "PathDisplaySource.Adapter.AdapterId (a LUID with its own ToString()) and PathDisplaySource.SourceId are both plain, already-populated property reads -- no native call. PathDisplayAdapter.DevicePath and PathDisplaySource.DisplayName each cost their own DisplayConfigGetDeviceInfo native call. PathDisplayTarget.DevicePath's getter is confirmed to perform a live DisplayConfigGetDeviceInfo call and to throw TargetNotAvailableException when the target is unavailable -- there is no public, hardware-independent way to construct a PathDisplayTarget whose DevicePath returns a fixed test string."
  implication: "AdapterId+SourceId is a safe, cheap, zero-extra-native-call identifier for per-candidate logging (used for the new DescribeSource helper, made internal and unit-tested). A per-target DevicePath breakdown (DescribeScopedPathEntry) cannot be unit-tested within this file's own established 'no live CCD hardware needed' seam boundary -- confirmed by decompile, not assumed; left private, self-verified via build + hand-trace only, matching this file's existing constraint for ActivateMonitors/DeactivateMonitors themselves."

- timestamp: 2026-08-29T03:20:00Z
  checked: "Implemented the three additive Log() additions described in Current Focus's round_11_investigation_note (candidate-source enumeration + selected-source/cache-match logging in TryBuildScopedActivationPlan; full per-entry scoped-plan structural dump in ActivateMonitors immediately before ApplyPathInfos) plus a new internal DescribeSource helper (2 new unit tests) and a private DescribeScopedPathEntry helper. Built and tested."
  found: "`dotnet build RigToggle.sln --no-incremental` succeeds (0 errors, same 6 pre-existing unrelated xUnit1031 warnings, all 6 projects). `dotnet test src/RigToggle.Tests` -- 238/238 passed, unchanged. `dotnet build src/RigToggle.Windows.Tests` succeeds (0 warnings, 0 errors, includes the 2 new DescribeSource tests); `dotnet test src/RigToggle.Windows.Tests` still cannot EXECUTE in this sandbox (missing Microsoft.WindowsDesktop.App) -- the same pre-existing, unrelated sandbox limitation as every prior round of both debug sessions. The 2 new tests were hand-traced against the implementation instead: DescribeSource(Source(3)) produces 'adapter={ 0 - 0 } sourceId=3' (LUID.ToString() format confirmed via decompile) -- both assertions (Contains 'adapter=', Contains 'sourceId=3') and the differently-sourceId'd distinguishability test both pass by hand-trace."
  implication: "All self-executable verification signals pass. This round's changes are confirmed additive/no-op-safe for every existing case: the one relocated `_lastKnownActiveModeByDevicePath.TryGetValue` call (moved earlier in TryBuildScopedActivationPlan's loop body so the new log line and the existing mode-selection branch share one lookup instead of two) is confirmed behaviorally identical by direct comparison of the pre- and post-move control flow."

## Evidence (round 12 -- pre-instrumentation debug.log excerpt: confirms Fix 3, second D-05 occurrence)

- timestamp: 2026-08-29T04:00:00Z
  checked: "Freshly-supplied rig debug.log excerpt (22:46:50.008-22:46:57.901, PID 40432, STARTUP through a full ENABLE-fail-then-ENABLE-succeed sequence on the Odyssey G5/SAM7489 tile). Build banner: 'builtAt=2026-08-29 22:29:49' -- BYTE-IDENTICAL to round 10's excerpt build timestamp (PID 12668), and therefore PREDATES round 11's three new instrumentation Log() additions (TryBuildScopedActivationPlan candidate/selected PathDisplaySource logging, ActivateMonitors' per-entry scoped-plan structural dump), all of which were added to WindowsMonitorController.cs AFTER round 10's excerpt was received, in this same session's round 11. Confirmed by direct search: none of the new round-11 log line text ('candidate PathDisplaySource(s)', 'selected ...', 'scoped plan entry detail') appears anywhere in this excerpt. This excerpt does NOT exercise or validate round 11's new instrumentation and does NOT fulfill round 11's still-open checkpoint ask (a repro captured on a build that includes it)."
  implication: "Round 11's checkpoint ask (a fresh rig trial reproducing root_cause (8) with the new instrumentation active) remains completely unanswered by this excerpt -- it must stay open pending a genuinely post-instrumentation repro. This excerpt is processed below ONLY for what it separately, validly confirms: Fix 3's real-world behavior, and a second real-world timing data point for root_cause (8)'s third failure shape."

- timestamp: 2026-08-29T04:05:00Z
  checked: "Direct line-by-line comparison of this excerpt's '[22:46:54.706] MainForm.OnTileAction: ArmIntentGuard SKIPPED -- ActivateMonitors(...SAM7489...) threw, so the observed state cannot be treated as the deliberately-intended one; leaving any previously-armed guard in place.' against src/RigToggle.App/MainForm.cs lines 1332-1336 (the enable branch's else-of-activateSucceeded trace call, round 9's Fix J)."
  found: "Byte-for-byte match (modulo the interpolated devicePath value): the code emits exactly `\"[{DateTime.Now:HH:mm:ss.fff}] MainForm.OnTileAction: ArmIntentGuard SKIPPED -- \" + $\"ActivateMonitors({devicePath}) threw, so the observed state cannot be treated \" + \"as the deliberately-intended one; leaving any previously-armed guard in place.\"` -- identical wording, identical structure, identical trigger condition (activateSucceeded == false, i.e. only reachable when the try block's ActivateMonitors call threw). This line fired at 22:46:54.706, ~2.66s after the InvalidOperationException/D-05 throw at 22:46:52.048 -- consistent with OnTileAction's catch block's MessageBox.Show(...) blocking synchronously until the user dismissed it, exactly as round 10's own hypothesis (item 7) predicted would eventually happen once a log capture extended far enough past the dialog. This is the FIRST excerpt, across rounds 9/10/11, to actually capture this specific trace line firing for a real ActivateMonitors failure."
  implication: "Fix 3 (round 9's conditional ArmIntentGuard re-arm, MainForm.cs) is CONFIRMED, not inconclusive -- this is direct, unambiguous, real-hardware evidence that the fix behaves exactly as designed: a failed ActivateMonitors call left the previously-armed (or, in this case, never-yet-armed-this-session) intent guard untouched rather than baking in the accidental post-failure state. Supersedes round 10's Resolution addendum verdict of 'inconclusive' for fix_3_status."

- timestamp: 2026-08-29T04:10:00Z
  checked: "Full sequence of the failed first ActivateMonitors(SAM7489) attempt in this excerpt (22:46:50.844-22:46:52.048), compared against round 10's excerpt's equivalent sequence (22:30:05.824-22:30:07.181, per round 10's own Evidence entry) for settle-poll exception timing."
  found: >
    This excerpt: settle-poll attempt 1/5 failed with TargetNotAvailableException (22:46:51.335),
    attempt 2/5 ALSO failed with TargetNotAvailableException (22:46:51.489), then attempts 3/5
    (22:46:51.731) and 4/5 (22:46:51.893) both read a stable, agreeing [ACI24A4, DELA0BC] (SAM7489
    absent) -- the poll's own "two consecutive reads agree" early-return then fires (no 5/5 line
    logged), matching fix 2's documented behavior. Both correction rounds (1/3 and 2/3) correctly
    found unexpectedlyActivated=[] unexpectedlyDeactivated=[] (SAM7489 was never a pre-call survivor,
    so it cannot appear in either set by construction -- same as round 10). ActivateMonitors then threw
    the same InvalidOperationException/D-05 ("Monitor enable did not take effect: [SAM7489]. No further
    automatic recovery is attempted (D-05).").
    Round 10's excerpt, by contrast, had attempt 1/5 SUCCEED first (reading [ACI24A4, DELA0BC]), with
    the two TargetNotAvailableException hits occurring on attempts 2/5 and 3/5 instead -- i.e. the
    FIRST tick succeeded there, whereas in THIS excerpt the first TWO ticks both failed before any
    successful read. Both trials otherwise share the identical overall shape: scoped ApplyPathInfos
    reports success with no exception, the settle-poll+correction budget never observes SAM7489 active,
    and the pre-existing D-05 verify-and-throw correctly fires. Also confirmed: the user again had to
    click the tile a SECOND time to succeed -- the retry at 22:46:56.637 ran the identical scoped-plan
    path and this time settled with SAM7489 present on its very first settle-poll attempt (22:46:57.020),
    completing cleanly (EXIT success, 22:46:57.369) -- same "fails once (or, this trial, fails on the
    first attempt only), then works on retry with no code change or restart" pattern as round 10, this
    time described by the user as more alarming: "Now I can't turn it on no matter what. I had to click
    twice to enable it."
  implication: >
    A second real-world occurrence of root_cause (8)'s third failure shape (the D-05
    InvalidOperationException), with a genuinely different exception-timing signature than round 10's
    occurrence (ticks 1+2 failing here vs. ticks 2+3 failing there). This is additional evidence that
    the underlying trigger is intermittent even in WHICH settle-poll ticks it strikes, not just in
    whether it strikes at all -- worth recording as a data point for whenever new (round-11)
    instrumentation data eventually arrives, but the instrumentation needed to actually discriminate
    between root_cause (8)'s candidate mechanisms (stale/incompatible source candidate; hardware
    source/port-group constraint; cached-mode mismatch) does not exist in this pre-round-11 build --
    no new root-cause conclusion is drawn from this timing difference alone. A further real-world data
    point across this session's own evidence (rounds 9 and 10 each contributed one occurrence to the
    "requested target never comes active" family; round 9 separately evidenced the PathChangeException+
    Extend-drop shape) plus the original resolved session's cataloged shapes.

## Evidence (round 13 -- first genuinely post-instrumentation debug.log excerpt: byte-identical fail/succeed comparison, a new source/position-mismatch defect, and the vanishing event explained)

- timestamp: 2026-08-30T00:00:00Z
  checked: "Freshly-supplied rig debug.log excerpt (23:06:45.616-23:07:22.147, PID 8860, builtAt=2026-08-29 23:06:08 -- confirmed, by direct grep, to POST-DATE round 11's instrumentation additions: 'candidate PathDisplaySource(s)', 'selected ...', and 'round 11 -- scoped plan entry detail' all appear, repeatedly, throughout this excerpt. This is the first excerpt across rounds 8-13 to actually fulfill round 11's still-open checkpoint ask."
  found: "Two full SAM7489 ENABLE attempts captured 7 seconds apart (23:06:46.863-48.237, FAILED with D-05; 23:06:53.446-54.195, SUCCEEDED), both selecting the identical PathDisplaySource (sourceId=2) from an identical 5-candidate list, both logging 'no prior cache entry', and both submitting a byte-for-byte identical 3-entry scoped plan (verified via direct side-by-side text comparison of the two 'round 11 -- scoped plan entry detail' log lines: ACI24A4 sourceId=0 (0,0); DELA0BC sourceId=1 (-1920,-58); SAM7489 sourceId=2, modeInfoAvailable=False, position=none -- identical in both). The failing attempt's two correction rounds both logged unexpectedlyActivated=[] unexpectedlyDeactivated=[] (nothing to correct -- SAM7489 simply never came active across 5 settle-poll attempts, 3 of which threw tolerated TargetNotAvailableException). The succeeding attempt's settle-poll saw SAM7489 present on its very first read, and both correction rounds again found nothing to correct (all 3 monitors already stable)."
  implication: "This is the first byte-for-byte, controlled comparison of a FAILING vs. SUCCEEDING scoped-activation attempt for the identical target using the identical source and identical (blank) mode -- exactly what round 11's own falsification_test named as missing. Because the input was identical and the outcome differed, this DIRECTLY ELIMINATES both 'stale/incompatible source candidate' and 'cached-mode mismatch' as explanations for this failure shape (root_cause (8)'s third observed shape, first seen round 10) -- the difference must lie outside anything this app's own code chooses, i.e. genuine external CCD/driver-level timing nondeterminism. This does not identify the underlying driver mechanism, but it does rule OUT two of root_cause (8)'s three long-standing candidate causes for this specific shape, a genuine narrowing after 13 total rounds across two sessions."

- timestamp: 2026-08-30T00:05:00Z
  checked: "The second SAM7489 ENABLE attempt in this excerpt (23:07:09.260-12.435, this time with only ACI24A4 active going in, DELA0BC having just been deliberately disabled) -- TryBuildScopedActivationPlan's candidate/selection log lines and the 'round 11' plan-entry-detail dump, cross-referenced against every other position value (ACI24A4, DELA0BC) logged anywhere in this excerpt."
  found: "TryBuildScopedActivationPlan selected sourceId=1 for SAM7489 despite sourceId=2 ALSO being listed unclaimed in the same 5-candidate enumeration -- and explicitly logged 'matches this target's own previously-cached source: False' (only printable when a non-null cached entry exists whose source differs from the fresh pick, per the logging code's own ternary). The resulting plan entry paired sourceId=1 with position=(1920,0) for SAM7489 -- a value matching neither ACI24A4's (0,0) nor DELA0BC's (-1920,-58) anywhere in this excerpt, and best explained as SAM7489's OWN previously-cached position (captured by DeactivateMonitors' CacheLiveModes at 23:06:59.327, moments before this same excerpt's earlier successful SAM7489 activation on sourceId=2 was disabled) now being applied under a DIFFERENT source than it was originally captured on. This ApplyPathInfos call completed without throwing, but the subsequent settle-poll saw SAM7489 active on attempt 1/5 then absent by attempt 5/5, while DELA0BC (untouched by this call, not requested) unexpectedly reactivated on its own across BOTH of the next two correction rounds (unexpectedlyActivated=[DELA0BC] both times) before finally settling off in round 3/3 (postExtendSettledActive=[ACI24A4] only, unexpectedlyActivated=[] unexpectedlyDeactivated=[]). ActivateMonitors then threw the same D-05 InvalidOperationException for SAM7489."
  implication: "Confirms a genuinely separate, real, code-level defect in TryBuildScopedActivationPlan's greedy source selection (round 8's own long-documented, never-fixed blind spot): it can pair a cached position with a freshly different, never-validated-together source. This is correlated with, but not proven to cause, this specific occurrence's DELA0BC-flapping instability -- it cannot explain the FIRST attempt's failure (evidence entry above), which involved no source/position mismatch at all. Separately confirms fix H's correction loop (round 7, resolved session) worked exactly as designed here: it correctly detected and suppressed DELA0BC's repeated unrequested reactivation both times it occurred, even though it structurally has no mechanism to correct SAM7489 itself (never a pre-call survivor, so it cannot appear in unexpectedlyDeactivated by ComputeUnexpectedlyDeactivated's own definition) -- exactly matching round 10's already-established classification of this failure family."

- timestamp: 2026-08-30T00:10:00Z
  checked: "The 23:07:18.643 RefreshMonitorTiles log line (SAM7489 completely absent, not merely inactive, from a 2-device list) against src/RigToggle.Windows/WindowsMonitorController.cs's GetAllMonitors()/GetActiveMonitors()/MergeAllMonitors() and src/RigToggle.App/MainForm.cs's RefreshMonitorTiles() -- and a full-text grep of this excerpt for 'GetAllMonitors failed' (the only trace line RefreshMonitorTiles' own catch block would emit on a genuine query exception)."
  found: "GetAllMonitors() sources its 'available-but-inactive' half from PathInfo.GetAllPaths() filtered by `t.DisplayTarget.IsAvailable` (line 318) BEFORE reading DevicePath/FriendlyName -- SAM7489's total absence from this one read requires only that its IsAvailable flag was transiently false in that instant's GetAllPaths() read; it is then excluded from BOTH the active half (correctly, it was not active) and this available-inactive half (newly, this instant), with no exception thrown anywhere in the process. Grepped the full excerpt for 'GetAllMonitors failed' -- does not appear anywhere; RefreshMonitorTiles' own retry-then-degrade-to-empty catch path (MainForm.cs lines 743-780) was never exercised in this excerpt. Separately: SAM7489 is confirmed to have thrown real, repeated TargetNotAvailableException errors during BOTH activation attempts in this same excerpt (23:06:47.345-674, 23:07:09.803-10.112) -- three independent observations, within roughly 30 seconds, of SAM7489's own target availability flickering."
  implication: "The vanishing event is fully explained by GetAllMonitors()'s OWN existing, correct IsAvailable filter -- the SAME code path every other RefreshMonitorTiles call in this excerpt used, not a degraded/filtered read from a swallowed exception. This is NOT a repeat of the ORIGINAL resolved session's round-6 'ACI24A4 vanishing from enumeration' observation, which the user later confirmed (2026-08-28T17:00:00Z correction, monitor-position-resets-to-de.md) was an unrelated hardware confound (a manual DisplayPort/power cable unplug during that trial) -- this round's event involves a different monitor (SAM7489) with no reported cable manipulation, so it cannot be dismissed the same way, but likewise cannot be elevated beyond 'this monitor's connection/negotiation is intermittently flaky,' which is consistent with, not additional independent proof of, the external-nondeterminism conclusion from the first evidence entry above."

- timestamp: 2026-08-30T00:15:00Z
  checked: "src/RigToggle.Windows/WindowsMonitorController.cs, GetActiveMonitors() (lines 276-291), re-read specifically for IsAvailable filtering, prompted by this round's IsAvailable-flapping findings above."
  found: "GetActiveMonitors() reads `targetInfo.DisplayTarget.DevicePath` on every TargetsInfo entry of every currently-active PathInfo with NO IsAvailable filter -- unlike GetAllMonitors()'s own second half (line 318) and TryBuildScopedActivationPlan (lines 892, 905), both of which explicitly filter IsAvailable first. This is a real, unguarded call site of the same class Fix I (round 9) hardened for PollUntilStableActiveDevicePaths -- an ACTIVE path whose target transiently reports unavailable could throw TargetNotAvailableException uncaught out of GetActiveMonitors() -> GetAllMonitors() -> RefreshMonitorTiles' existing retry-then-degrade-to-Array.Empty path, a more severe (whole-dashboard-blank) failure shape than what was actually observed this round."
  implication: "A plausible, adjacent hardening gap -- but NOT evidenced as having fired anywhere in this excerpt (no empty/zero-monitor RefreshMonitorTiles read appears anywhere in it; the observed vanishing event is fully explained without it, per the entry above). Recorded for a future round if evidence ever surfaces implicating it -- not fixed or claimed causal this round."

- timestamp: 2026-08-30T00:20:00Z
  checked: "The task framing's claim of a process restart at pid=36872 (~23:07:26) against the actual DATA_START...DATA_END block supplied in this round's checkpoint response, via direct full-text search for 'pid=36872' and '23:07:26'."
  found: "Neither string, nor any reference to a new process/PID/restart, appears anywhere in the raw log excerpt actually supplied. The excerpt as received ends at [23:07:22.147]."
  implication: "This specific detail from the task framing is UNVERIFIABLE against the evidence actually provided -- recorded as an unconfirmed claim, not independently observed, per this round's own explicit instruction to check readings directly rather than trust the framing. Does not affect any conclusion above, none of which depended on it."

## Evidence (round 15 -- first real-world Fix-A retry firing, verified against the raw log; retry-timing and external-vs-own-code reassessment)

- timestamp: 2026-08-30T02:00:00Z
  checked: "Freshly-supplied rig debug.log excerpt (12:56:50.683-12:56:59.920, pid=33716, builtAt=2026-08-30 12:55:49 -- confirmed by direct grep to contain round 14's own 'round 14 (fix A)' and 'round 14 (fix B)' log lines, so this build genuinely has both round-14 fixes active). The first SAM7489 activation attempt (attemptNumber=1 of the round-14 retry loop), ENTER through the fix-A retry decision, cross-referenced against WindowsMonitorController.cs's ActivateMonitors/TryBuildScopedActivationPlan/PollUntilStableActiveDevicePaths."
  found: "Confirms the checkpoint framing's item 1a exactly: 5-candidate list at 12:56:52.689 ([claimed, claimed, unclaimed, unclaimed, unclaimed]), fix-B 'no prior cache entry -- using greedy first-unclaimed selection', sourceId=2 selected, ApplyPathInfos 'completed without throwing' at 52.978, both correction rounds (53.886, 54.041) logging unexpectedlyActivated=[]/unexpectedlyDeactivated=[] (structurally correct per round 10's classification -- SAM7489 was never part of the pre-call baseline). ONE ADDITIONAL DETAIL the framing did not mention: the settle poll's very FIRST read, immediately after apply (52.979, attempt 1/5), DID show SAM7489 active ([ACI24A4, DELA0BC, SAM7489]) -- it dropped out only after two TargetNotAvailableException ticks (53.348, 53.512) and two subsequent stable reads without it (53.721, 53.885)."
  implication: "The first attempt was not simply 'target never came active' (unlike round 10/13's clean third-shape framing) -- SAM7489 transiently registered active, then appears to have dropped back out of the active-path set during a ~740ms window bounded by two transient-unavailable events, inside this SAME first attempt's own settle-poll, well before Fix A's retry ever ran. Directly relevant to the retry-timing question below."

- timestamp: 2026-08-30T02:05:00Z
  checked: "The automatic retry (attemptNumber=2, 54.042-839) -- CacheLiveModes/TryBuildScopedActivationPlan log lines, cross-referenced against TryBuildScopedActivationPlan's exact GetAllPaths()+IsAvailable candidate predicate (lines ~1000-1004, ~1021-1023) and ShouldRetryScopedActivation's exact three-part gate (line 1278)."
  found: "Confirms the checkpoint framing's item 1b exactly: retry fires at 54.042 (~1.358s after the 52.684 original request), logged 'round 14 (fix A) -- INTERNAL automatic retry 1/2'; TryBuildScopedActivationPlan logs 'has 0 candidate PathDisplaySource(s) from GetAllPaths(): []' for SAM7489; this forces the 'no unclaimed PathDisplaySource' failure and the whole-topology Extend fallback; Extend also fails to activate SAM7489; D-05 throws again, with the message wording 'budget exhausted after 2 total attempts' matching byte-for-byte. ONE PREVIOUSLY-UNEXAMINED NUANCE: tracing ShouldRetryScopedActivation's own gate (usedScopedActivation && requestedStillInactiveCount>0 && attemptNumber<=maxRetryAttempts) against this attempt's actual values shows usedScopedActivation was FALSE (this retry fell back to Extend) -- THIS, not the numeric attempt-count (attemptNumber=2 had not yet exceeded maxRetryAttempts=2), is what actually stopped further retrying; fix A's own doc comment confirms this is deliberate (an Extend-fallback failure is a different shape, never retried). Only 1 of the 2 allotted retries was ever usable this trial."
  implication: "The 0-candidate reading is genuine live-driver state, not a code artifact: the candidate-enumeration predicate (`t.DisplayTarget.IsAvailable && t.DisplayTarget.DevicePath == devicePath`) is byte-identical code with identical parameters on both the original attempt and the retry -- only the live IsAvailable flag differs. Given SAM7489 was ALREADY throwing TargetNotAvailableException twice within the ORIGINAL attempt's own settle-poll (53.348, 53.512 -- 530-694ms before the retry's own GetAllPaths() query at 54.043), the most parsimonious reading is that this is a CONTINUATION of that same transient-unavailability episode, not an independent, unrelated flake. This IS structurally worse than the original attempt's 5-candidate state: 5 candidates permitted a scoped plan to be built and applied at all (even though it ultimately failed to settle); 0 candidates forces the strictly-worse, already-known-unreliable Extend fallback, and additionally forfeits the remaining retry budget per the nuance above."

- timestamp: 2026-08-30T02:10:00Z
  checked: "ActivateMonitors' outer retry `for` loop (lines 597-795) for any Thread.Sleep/backoff between one attempt's verify-and-throw and the next attempt's CacheLiveModes call, cross-referenced against the log's own timestamps for the attempt-1-to-retry transition."
  found: "NO delay of any kind exists between attempts -- the `continue` statement (line 788) re-enters the loop body immediately, which starts with a fresh CacheLiveModes call (line 628). The log confirms this: the verify-and-throw's postCorrection query and the 'INTERNAL automatic retry 1/2' log line both land within the same millisecond (54.041/54.042), and the very next log line (54.042, the retry's own CacheLiveModes call) follows immediately. The ~1.358s total gap between the original request (52.684) and the retry firing (54.042) is entirely consumed by the settle-poll-then-correct loop's OWN pre-existing delays (2 correction rounds' worth of 150ms-spaced polls), not any deliberate Fix A backoff."
  implication: "Confirms, directly from code, that Fix A retries back-to-back with zero added delay. Combined with the prior two entries (SAM7489 was still inside a transient-unavailability episode ~530-700ms before the retry sampled it, and had not cleared by the time it did), this establishes a plausible, evidence-grounded mechanism by which a zero-delay retry can sample the SAME external negotiation window the original attempt was already inside, rather than one where it has cleared -- and, per the budget-forfeiture nuance above, a badly-timed retry costs more than one wasted attempt."

- timestamp: 2026-08-30T02:15:00Z
  checked: "git blame on the CURRENT (post-round-14) verify-and-throw block's throw statement and exact exception message text (now at lines 792-794, shifted from the pre-round-14 lines ~699-701 round 10 documented, since fix A wrapped the surrounding code in a retry loop)."
  found: "`throw new InvalidOperationException($\"Monitor enable did not take effect: ...\")` is still attributed to commit 90fe29d (2026-07-28), unchanged. Round 14's own diff (confirmed via `git blame` showing 'Not Committed Yet' on its own new/modified lines, immediately above) only added the ShouldRetryScopedActivation check and its `continue` directly above this throw -- the throw itself was not touched."
  implication: "Re-verifies round 10's classification directly against the live code, not by assumption: the D-05 throw/verify logic remains completely untouched by round 14 or any round since, a full month older (90fe29d, 2026-07-28) than every fix applied in this debug session. The classification that root_cause (8)'s trigger predates this whole investigation stands, now re-confirmed rather than carried forward on faith."

- timestamp: 2026-08-30T02:20:00Z
  checked: "The 12:56:59.920 'MainForm.OnTileAction: ArmIntentGuard SKIPPED' log line against src/RigToggle.App/MainForm.cs's OnTileAction enable-branch conditional-arm code (lines 1289-1329, Fix J from round 9)."
  found: "Byte-for-byte match of the exact log line MainForm.cs emits only when `activateSucceeded` is false. Fires 5.081s after the D-05 throw's own trace line (54.839 -> 59.920), consistent with the user needing to see and dismiss the resulting modal MessageBox (round 10's previously-inferred, never-directly-confirmed mechanism) before OnTileAction's `finally` block could complete and log this line."
  implication: "This is the FIRST log excerpt across all 15 rounds to actually CAPTURE this exact confirming line (round 10's excerpt ended too early; round 12 only inferred it from indirect timing). Directly confirms Fix J is firing correctly on this exact failure shape: the failed action's accidental post-failure state was NOT baked into the intent guard."

## Evidence (round 17 -- fresh debug.log excerpt: a new toggle-switch-path stale-device-path defect, and a second Fix-A retry-timing data point)

- timestamp: 2026-08-30T03:00:00Z
  checked: "Freshly-supplied rig debug.log excerpt (17:11:00.724-17:13:04.086, pid=30908, builtAt=2026-08-30 12:55:49 -- same build as rounds 15/16). The very first monitor action of this app session, at 17:11:01.838: ActivateMonitors ENTER requested=[SAM748A#...UID521, SAM7489#...UID516] monitorSwapDisableSet=[DELA0B8#...UID516, ACI24A4#...UID512, DELA0BC#...UID520], EXIT throwing 'not detected: [SAM748A...]' 2ms later. No MainForm.OnTileAction trace line precedes it -- checked against MainForm.cs for every call site that constructs a multi-path, isPartOfMonitorSwap=True ActivateMonitors call (matching this shape): only ToggleSwitch_ActionRequested (line 634, via ToggleService.ToggleToRigMode) does so; OnTileAction always requests a single device path with an empty monitorSwapDisableSet."
  found: "SAM748A (UID521) and DELA0B8 (UID516) do not appear anywhere in rounds 1-16 of this debug file. src/RigToggle.Core/ToggleService.cs's ToggleToRigMode (lines 90-91, 111) reads `settings.MonitorsToDisable`/`settings.MonitorsToEnable` directly from `_settingsStore.Load()` and passes them unfiltered into `_monitorController.ActivateMonitors(enableSet, monitorSwapDisableSet: disableSet)` -- no live-enumeration check of any kind before this call. WindowsMonitorController.cs's ActivateMonitors (lines 499-511) performs its OWN early-availability guard via a live `PathInfo.GetAllPaths()` query and throws the instant any requested path isn't found among available targets -- this guard runs BEFORE TryBuildScopedActivationPlan/CacheLiveModes (confirmed: no such logging appears between ENTER and the throw, and the throw fires 2ms after ENTER), exactly matching the observed shape."
  implication: "Confirmed, by direct code read (not inference): this is a genuinely new, fully-deterministic (not timing-dependent) code-level defect in the Rig-mode TOGGLE SWITCH path (ToggleSwitch_ActionRequested -> ToggleService.ToggleToRigMode), a call path no round of this debug session (8-16) ever exercised or examined -- every prior round's evidence traces exclusively OnTileAction (single device path, isPartOfMonitorSwap=False). This will fail identically, every single time, for as long as settings.json's MonitorsToEnable/MonitorsToDisable contain a device path not currently live -- confirmed by this log's own second, independent occurrence 3 seconds later (17:11:04.747) with byte-for-byte identical requested/disableSet content and outcome."

- timestamp: 2026-08-30T03:05:00Z
  checked: "src/RigToggle.App/SettingsForm.cs's BtnSaveSettings_Click (lines 1198-1234) and PopulateMonitorGrid (lines 566-631) -- how MonitorsToDisable/MonitorsToEnable are merged on save, and whether a stale (no-longer-enumerated) device path can ever be removed via the UI. Grepped the whole file for Remove/Clear/Purge/forget in the context of monitor device paths."
  found: "BtnSaveSettings_Click's own comment (lines 1218-1223) documents a DELIBERATE design: 'persisted sets = (previously-saved entries GetAllMonitors() no longer enumerates at all) UNION (currently-checked rows' device paths). Stale/disconnected entries pass through untouched.' `mergedDisable`/`mergedEnable` (lines 1230-1234) are computed as `staleDisable/staleEnable` (paths not in the live enumeration) unioned with the freshly-checked grid selection -- on EVERY save, forever, with no expiry. PopulateMonitorGrid (lines 598-616) only adds a grid row per entry in `_allMonitors` (the LIVE enumeration) -- a stale path has no row and therefore no checkbox to ever uncheck. GetStaleSavedDevicePaths/ShowStaleMonitorWarning (lines 832-862) DO detect and surface staleness, but only as a non-blocking warning label inside the Settings dialog ('settings preserved; reconnect the display to manage it here') -- this detection is never consulted by ToggleService or ToggleSwitch_ActionRequested. Grepped for Remove/Clear/Purge/forget: no per-monitor-path removal control exists anywhere in this file (btnClearAppPath is unrelated, for the companion-app path only)."
  implication: "This is the confirmed ROOT source of the stale SAM748A/DELA0B8 entries, and confirms there is currently NO supported, in-app way to ever recover from this state once a monitor's device path changes (replug/EDID renegotiation/driver reinstall) while the OLD path remains referenced -- the only recourse today is manually hand-editing %LocalAppData%\\RigToggle\\settings.json. This design was almost certainly intentional to avoid losing configuration for a merely temporarily-unplugged monitor, but has no distinction between 'temporarily unplugged' and 'permanently replaced by a new device-path identity,' which is exactly the failure mode observed here."

- timestamp: 2026-08-30T03:10:00Z
  checked: "Whether SAM748A/DELA0B8 are genuine historical device paths or a copy-paste/transcription artifact -- character-by-character comparison of the raw DATA_START/DATA_END block against every other device path logged in this same excerpt, plus a grep of .planning/debug/resolved/ for prior appearances."
  found: "SAM748A#7&16485deb&0&UID521#{e6f07b5f-...} and DELA0B8#7&16485deb&0&UID516#{e6f07b5f-...} share the IDENTICAL adapter-instance segment ('7&16485deb&0') and GUID suffix every other device path in this entire session uses -- only the manufacturer/model code and UID differ, consistent with genuine CCD target reassignment, not a malformed string. `grep -rn` confirms SAM748A appears (with DELA0B8) 23 times in .planning/debug/resolved/monitor-enable-reactivates-others-again.md (an older, already-resolved debug session on this same rig), and DELA0B8 is used extensively throughout .planning/debug/resolved/monitor-position-resets-to-de.md (the session this whole regression investigation is a follow-up to). Both are also referenced directly in WindowsMonitorController.cs's own long-standing code comments (e.g. lines 518-520, part of the round-5 fix H narrative from the resolved session)."
  implication: "CONFIRMED genuine, historically-real device paths for two of this rig's own monitors at an earlier point in time -- NOT a transcription error. Consistent with the physical monitors having been replugged/re-enumerated under new CCD target identities (SAM748A -> SAM7489, DELA0B8 -> DELA0BC) at some point before this debug session began, with the old identities silently retained forever in settings.json per the union-merge design confirmed above."

- timestamp: 2026-08-30T03:15:00Z
  checked: "Whether the ORIGINAL Symptom 1 complaint (top of this file: 'Windows now enumerates as display 3, but the app shows display 2' / numbering swap) was ever independently confirmed resolved by any round 8-16 fix, versus set aside. Re-read Symptom 1's own 'Reproduction' field and every Evidence entry across rounds 8-16 for the specific control used (tile vs. toggle switch)."
  found: "Symptom 1's own Reproduction field states: 'Trigger control (dashboard tile vs. Rig/Normal toggle switch) not yet isolated -- needs follow-up' -- and this was never revisited. Every single Evidence entry and reasoning_checkpoint from round 8 through round 16 traces an OnTileAction-driven ActivateMonitors call (single device path, monitorSwapDisableSet=[], isPartOfMonitorSwap=False) -- none examines or tests ToggleSwitch_ActionRequested/ToggleService.ToggleToRigMode's multi-path, isPartOfMonitorSwap=True call shape, which is the ONLY call site that reads settings.json's raw MonitorsToDisable/MonitorsToEnable lists directly."
  implication: "Honest finding: the original numbering-swap symptom was NEVER independently re-confirmed fixed via the toggle-switch path -- the investigation fully pivoted, after round 8's CacheLiveModes fix, to the tile-click-driven SAM7489 D-05/retry mechanism and never returned to isolate or test the toggle switch. This is a genuine, previously-unaddressed gap in verification coverage, not a re-discovery of anything already fixed. Whether THIS round's stale-device-path defect is what produced the ORIGINAL complaint cannot be proven retroactively (no record of what settings.json contained or which control was used at the time of the original report) -- but the mechanism is directly plausible without needing any position-cache or source-selection defect at all: a toggle that stops before ever reaching the CCD layer (confirmed zero topology mutation, per ReconcileModeAfterMonitorFailure's own 'no observable topology change' trace) while MainForm.ArmIntentGuard still snapshots the unchanged live state as 'the deliberately intended new state' produces exactly an app/Windows disagreement."

- timestamp: 2026-08-30T03:20:00Z
  checked: "src/RigToggle.App/MainForm.cs, ToggleSwitch_ActionRequested (lines 518-660+), specifically its own ArmIntentGuard() call (line 647) -- compared directly against OnTileAction's round-9 Fix J (conditional ArmIntentGuard, gated on activateSucceeded/deactivateSucceeded) -- and cross-referenced against round 9's own recorded blind_spots."
  found: "ToggleSwitch_ActionRequested calls `result = _orchestrator.ToggleToRigMode();` (line 634, does not throw -- ToggleService.TryExecuteStep catches internally and returns a Failed ToggleStepResult), then unconditionally `RefreshUi(); RefreshMonitorTiles(); ArmIntentGuard();` (lines 637-647) with NO conditional on `result.Success` -- unlike OnTileAction's gated `if (activateSucceeded)`/`if (deactivateSucceeded)` pattern. This is precisely the gap round 9's own blind_spots (this file, line 63) named and left open: '...MainForm.cs has at least one other ArmIntentGuard() call site (around line 2225 [now ~647], in the Rig/Normal toggle switch handler) that was NOT reviewed or changed this round... flagged as an open item for a future round if a toggle-triggered... recurrence is reported.' This round's log directly confirms it firing: 'MainForm.ArmIntentGuard: armed -- intent=[ACI24A4:active, DELA0BC:active, SAM7489:inactive]' at 17:11:01.853 and again at 17:11:04.750, immediately following each failed toggle attempt."
  implication: "Confirmed, real, evidence-based ancillary/compounding finding -- exactly the toggle-triggered recurrence round 9 anticipated might someday be reported. Baking in the (unchanged, since nothing actually mutated) live state as 'intended' after a failed toggle-switch attempt is not itself what caused the SAM748A 'not detected' failure, but it means the app's own intent-tracking is silently poisoned the same way OnTileAction's was before Fix J -- worth fixing alongside item 1's primary defect, not in isolation."

- timestamp: 2026-08-30T03:30:00Z
  checked: "Direct thread/call-stack/COM comparison of MainForm.OnTileAction's manual ActivateMonitors call versus ActivateMonitors' own internal Fix-A retry (`for` loop, WindowsMonitorController.cs lines 597-795, `continue` at line 788) -- src/RigToggle.App/Program.cs for STAThread/apartment declarations, and WindowsMonitorController.cs's own `using` directives for any COM/DllImport usage."
  found: "Program.cs's Main is decorated `[STAThread]` -- the entire app (including all monitor-control calls) runs on one single UI/STA thread. OnTileAction is a synchronous WinForms event handler; every round's log shows 'OnTileAction: calling ActivateMonitors' and 'ActivateMonitors: ENTER' landing in the same millisecond with no BeginInvoke/Task.Run/await between them. The internal Fix-A retry is a `continue` inside the SAME ActivateMonitors call's own `for` loop -- same stack frame, same thread, re-querying `PathInfo.GetActivePaths()`/`GetAllPaths()` via the identical code a fresh call would use. WindowsMonitorController.cs's `using` directives are exclusively `WindowsDisplayAPI`/`WindowsDisplayAPI.DisplayConfig`/`WindowsDisplayAPI.Native.DisplayConfig` -- no DllImport/ComImport/Marshal.* anywhere in the file; PathInfo/PathDisplaySource wrap plain user32.dll P/Invoke exports (QueryDisplayConfig/SetDisplayConfig), not COM interfaces. The app's one real COM interop (IPolicyConfig) belongs to the audio subsystem, never invoked on this call path."
  implication: "CONFIRMED, by direct source read, that there is NO discoverable code-level difference (thread, apartment, message-pump, marshaling, or call-path shape) between a manual re-click and the internal automatic retry -- both execute the identical method on the identical UI/STA thread, synchronously, with the message pump equally inactive during either. The ONLY difference between them is elapsed wall-clock time before the live CCD state is re-queried. STA/COM was a legitimate hypothesis worth checking directly (per this round's task) -- checked, and ruled out."

- timestamp: 2026-08-30T03:35:00Z
  checked: "Timing math for this round's Fix-A automatic retry (17:12:58.276-17:13:00.471) and subsequent manual recovery (17:13:02.834-17:13:03.612), computed against both the D-05-throw baseline and the original-first-attempt-start baseline, and directly compared against round 15's (12:56:52.684-12:56:54.839) and round 13's (23:06:46.868-23:06:53.447) data points."
  found: "This round's automatic retry fired at 17:12:59.638, 1.362s after the original request (17:12:58.276) -- essentially IDENTICAL to round 15's 1.358s gap -- and again logged 'has 0 candidate PathDisplaySource(s) from GetAllPaths(): []' for SAM7489, the SAME degraded shape round 15 first observed (now 2-for-2). The manual re-click fired at 17:13:02.834: 2.363s after the D-05 throw (17:13:00.471) and 4.558s after the original request (17:12:58.276) -- and succeeded cleanly (EXIT success at 17:13:03.612, 3.141s after the D-05 throw). Round 13's manual recovery, measured the same way (failing-attempt-start to succeeding-attempt-start), was ~6.579s (23:06:46.868 -> 23:06:53.447)."
  implication: "Two independent real-world automatic-retry failures now exist, both at a consistent ~1.36s gap, both landing on the identical '0 candidates' shape -- and two independent manual recoveries now exist, at ~4.56s and ~6.58s elapsed respectively (same measurement basis), both succeeding. This is a genuine strengthening (not merely a repeat) of round 15's single-data-point finding: the pattern is now self-consistent across two separate occurrences on both sides, with no counterexample yet (no successful automatic retry, no failed manual retry, has ever been captured). It still does NOT pin down a specific sufficient delay -- only a loose bound (somewhere above ~1.36s, at or below ~4.56s) -- and n=2 per side remains a small sample. Reported honestly as meaningfully strengthened, not conclusive, evidence for Fix K's underlying premise."

## Evidence (round 19 -- fresh debug.log excerpt, explicitly PRE-round-18 build: a nested ActivateMonitors correction call's own D-05 propagating uncaught through the outer, user-requested call)

- timestamp: 2026-08-30T06:00:00Z
  checked: "Freshly-supplied rig debug.log excerpt (17:33:34.258-18:48:23.331), explicitly
    flagged by the user as captured on the PRE-round-18 build (before this session's item-1
    [stale-device-path live-filtering] and item-3 [fix K poll-until-reachable] fixes were
    built) -- the patched build is being tested separately. Read WindowsMonitorController.cs's
    ActivateMonitors in full (lines 464-820) plus ComputeUnexpectedlyActivated/
    ComputeUnexpectedlyDeactivated (lines 1227-1266), ShouldRetryScopedActivation (lines
    1268-1292), and PollUntilTargetsReachable (lines 1294-1363) against the log line-by-line,
    and MainForm.cs's OnTileAction ENABLE-branch try/catch (lines 1330-1382) against the log's
    final MessageBox-producing line."
  found: "The 18:48:01.165 event (ActivateMonitors ENTER requested=[SAM748A, SAM7489]
    isPartOfMonitorSwap=True, EXIT throwing 'not detected: SAM748A' 0ms later) is the
    already-diagnosed item-1 stale-device-path defect from round 17 (SAM748A/DELA0B8 are
    superseded CCD identities for the same two monitors now identified as SAM7489/DELA0BC),
    fixed by round 18's LiveFilterMonitorSets -- expected to recur here since this excerpt
    predates that build, not a new finding. At 18:48:06.068 the user's ACI24A4 ENABLE tile
    click starts the OUTER ActivateMonitors(ACI24A4) call (preExtendActive=[SAM7489]). Its
    scoped ApplyPathInfos throws PathChangeException in the same logged millisecond as the
    plan-built line (0ms elapsed) -> falls back to Extend -> settles on [ACI24A4, DELA0BC].
    Correction round 1 correctly computes unexpectedlyActivated=[DELA0BC] and
    unexpectedlyDeactivated=[SAM7489] (fix H firing exactly as designed) -- deactivates
    DELA0BC successfully, then makes an UNCAUGHT (no try/catch anywhere around line 744)
    nested ActivateMonitors(SAM7489, monitorSwapDisableSet: empty) call to restore SAM7489.
    That nested call's OWN scoped ApplyPathInfos ALSO throws PathChangeException (0ms,
    identical shape) -> falls back to its OWN Extend -> ALSO collaterally reactivates DELA0BC
    (a second, independent recurrence) while SAM7489 itself never comes active. The nested
    call's own correction loop re-suppresses DELA0BC (twice-confirmed side effect) but,
    because SAM7489 was never part of THIS call's own pre-call active set ({ACI24A4} only),
    ComputeUnexpectedlyDeactivated cannot see it -- it is purely 'the nested call's own
    requested target, still inactive.' ShouldRetryScopedActivation(usedScopedActivation=False,
    requestedStillInactiveCount=1, attemptNumber=1, maxRetryAttempts=2) evaluates false at
    line 1291 because usedScopedActivation is false (this attempt fell back to Extend) --
    fix A/fix K's retry+poll block is never entered; the nested call falls straight to its own
    D-05 verify-and-throw: 'Monitor enable did not take effect: SAM7489.' This propagates,
    completely uncaught, through the outer call's correction-loop body, both of the outer
    call's own for-loops, and out of the OUTER ActivateMonitors(ACI24A4) call entirely --
    confirmed by the total absence, anywhere in the excerpt, of the 'correction round 1/3
    nested ActivateMonitors(SAM7489) completed without throwing' line that would only print on
    a normal return from line 744. The very next line is
    'MainForm.OnTileAction: ActivateMonitors(ACI24A4) threw InvalidOperationException: Monitor
    enable did not take effect: SAM7489 ... (D-05)', surfaced verbatim to the user via
    MessageBox.Show(this, ex.Message, ...) at MainForm.cs line 1354 -- `devicePath` (ACI24A4,
    the user's real click target, which DID succeed per the final RefreshMonitorTiles
    observation at 18:48:08.188) is in scope at that catch site and used in the adjacent
    Trace.WriteLine debug-only line, but never included in the user-facing dialog text."
  implication: "CONFIRMED, byte-for-byte against source with line numbers, all six numbered
    claims in this round's assigned sequence-verification task -- see
    round_19_reasoning_checkpoint for the full trace. Item A's mechanism: fix A/fix K's
    retry+poll block is NOT structurally excluded from nested calls (a nested call re-enters
    the same method and gets its own independent attemptNumber/budget) -- it simply did not
    fire here because the nested call's OWN attempt also hit the Extend-fallback path, which
    ShouldRetryScopedActivation's already-existing, already-approved gate (round 13/14/15/17)
    deliberately excludes from retry regardless of call depth. This is the SAME
    already-documented exclusion, newly OBSERVED at a new (nested) call site -- not a new,
    distinct structural gap. Item B: CONFIRMED genuine UX/clarity defect -- the D-05 exception
    carries no marker distinguishing 'primary request' from 'collateral-restore correction'
    origin, and MainForm's catch surfaces `ex.Message` unmodified, making a fully-successful
    user action (ACI24A4 enabling) indistinguishable, in the dialog the user actually sees,
    from a failure of that same action -- this recurs any time a nested fix-H correction's own
    retry budget is exhausted for ANY reason, independent of whether the underlying
    PathChangeException flakiness (root_cause (8)) is ever resolved."

## Resolution

root_cause: "ActivateMonitors' live-mode position cache (`_lastKnownActiveModeByDevicePath`, populated via CacheLiveModes) is populated in exactly two circumstances: a DELIBERATE removal via DeactivateMonitors, and a DELIBERATE swap-exclusion via ActivateMonitors' own `if (isPartOfMonitorSwap)` block -- which only caches monitorSwapDisableSet's paths, never every currently-active path. The resolved session's round-7 fix H (ComputeUnexpectedlyDeactivated + a nested, non-swap-aware `ActivateMonitors(unexpectedlyDeactivated, monitorSwapDisableSet: empty)` correction call) reactivates a previously-active survivor that the whole-topology `ApplyTopology(Extend)` fallback ACCIDENTALLY dropped -- by definition (ComputeUnexpectedlyDeactivated excludes monitorSwapDisableSet paths), such a survivor was NEVER a deliberate exclusion and Extend itself never routes through DeactivateMonitors, so no cache entry exists for it in either of the two populating code paths. TryBuildScopedActivationPlan's nested reactivation of this survivor therefore always falls to its blank-mode constructor branch, letting the driver's best-mode-logic pick a default position -- silently resurrecting the resolved session's original Symptom 1 defect (position resets to a driver default) via fix H's own correction path, which that fix's author did not anticipate needing position-cache coverage for. This fires 'immediately' (synchronously, inside the same ActivateMonitors call, before it returns) rather than after a delay, and 'only sometimes' because fix H's correction path only engages when the scoped activation plan happens to throw PathChangeException for a specific monitor pairing and fall back to Extend -- a trigger the resolved session's own Resolution.root_cause (8) already documented as genuinely unexplained and intermittent, carried forward unchanged, not re-investigated this round. The compounding numbering-swap symptom is plausibly explained by the same gap: TryBuildScopedActivationPlan's source-claim logic (`allPaths.FirstOrDefault(p => !claimedSources.Contains(p.DisplaySource) && ...)`) has no preference for reclaiming a target's own previous PathDisplaySource, so a survivor reactivated with a driver-default mode can also land on a different physical GPU output/adapter path than before -- and Windows' own Display Settings numbering (confirmed, via direct code read of MainForm.cs, to be assigned independently of this app's own stable DevicePath-based tile numbering) can shift as a result. The source-claim greediness itself is NOT fixed this round (see Resolution.verification blind_spots) -- only the directly-evidenced, more severe position-reset defect is."
fix: "src/RigToggle.Windows/WindowsMonitorController.cs, ActivateMonitors only: widened the existing CacheLiveModes call from `CacheLiveModes(swapExcludedSurvivors)` (only monitorSwapDisableSet-matching currently-active paths, inside the `if (isPartOfMonitorSwap)` block) to an unconditional `CacheLiveModes(activePathsForScopedPlan)` (every currently-active path) run once, immediately after `activePathsForScopedPlan` is computed and before any topology mutation (scoped ApplyPathInfos or Extend) is attempted. This is strictly additive over the prior behavior: monitorSwapDisableSet's paths are always a subset of every currently-active path, so the swap case is byte-for-byte unchanged (same paths get cached, just via the broader call); a plain non-swap call now also caches data for survivors that were never previously cached, closing the exact gap fix H's nested reactivation needs. No other method, branch, or log line touched -- the isPartOfMonitorSwap block's own explanatory Log() line is preserved for the swap-specific exclusion narrative, only its now-redundant CacheLiveModes sub-call is removed (caching already happened unconditionally above)."
verification:
  target_test: { result: pending, reason: "Awaiting rig verification -- cannot execute real Windows CCD calls in this sandbox, matching every prior round of the resolved session's own history. Self-verified via direct code read/hand-trace of the modified control flow against this round's specific repro shape (fix H's nested-reactivation path with no cache entry) plus a full-solution build and the existing RigToggle.Tests suite (unchanged, 238/238 pass)." }
  mutation_check: { result: skipped, reason: "No Stryker configured in this repo (matches every prior debug session in this codebase)." }
  no_op_deletion: { result: pass, deletion_justified_by_rca: true, note: "The only removed code is the swapExcludedSurvivors computation + its own narrower CacheLiveModes(swapExcludedSurvivors) call -- fully subsumed by the new unconditional CacheLiveModes(activePathsForScopedPlan) call immediately above it (monitorSwapDisableSet's paths are always a subset of activePathsForScopedPlan), so the swap case caches the exact same paths as before, just via the broader call. The isPartOfMonitorSwap block's own explanatory Log() line is preserved (reworded to note caching already happened above), not deleted. No control flow, correction loop, or verify-and-throw logic touched." }
  adjacent_tests: { result: pass, suites_run: ["dotnet build RigToggle.sln --no-incremental (0 errors, 6 pre-existing unrelated xUnit1031 warnings, all 6 projects including RigToggle.Windows and RigToggle.Windows.Tests)", "dotnet test src/RigToggle.Tests (238/238 passed, unchanged)", "dotnet test src/RigToggle.Windows.Tests (builds cleanly; execution aborts with the same pre-existing sandbox limitation as every prior round -- missing Microsoft.WindowsDesktop.App runtime on this Linux sandbox -- unrelated to this change, which touches no unit-tested seam in that project)"] }
  revert_and_reconfirm: { result: pending, reason: "Requires a rig trial reproducing fix H's correction path (a scoped-plan PathChangeException falling back to Extend, dropping a previously-active, non-swap-excluded survivor) -- deferred to the human-verify checkpoint below." }
  guardrail_verdict: accepted
  guardrail_note: "All self-executable signals pass: no-op/deletion review confirms the removed code's effect is fully subsumed by the new broader call (RCA-justified, not a silent behavior loss); full-solution build is clean; the existing 238-test RigToggle.Tests suite is unchanged. RigToggle.Windows.Tests cannot execute in this sandbox (pre-existing, unrelated limitation, matching every prior round of the resolved session) -- no new unit-tested seam was needed since this fix widens an existing instance-field cache population call operating on live CCD PathInfo objects, the same untestable-in-sandbox category as ActivateMonitors/DeactivateMonitors themselves throughout this file's history. Hardware-dependent behavior (does the position actually restore correctly after fix H's correction path fires) is deferred to the mandatory human-verify checkpoint." }
files_changed:
  - src/RigToggle.Windows/WindowsMonitorController.cs

## Resolution (round 9 addendum -- two additional, independent fixes)

root_cause_addendum: >
  Two ADDITIONAL, independent contributing causes were confirmed from a fresh debug.log excerpt
  (received after the CacheLiveModes fix above was already applied), neither superseding it:
  (I) WindowsMonitorController.PollUntilStableActiveDevicePaths' QueryActiveDevicePaths() calls had
  no per-tick exception handling -- unlike its sibling ObservePostApplyStability (hardened in round 6
  of the resolved session for the identical hazard: PathDisplayTarget.DevicePath throwing
  TargetNotAvailableException when a target transiently reports unavailable mid-CCD-renegotiation).
  PollUntilStableActiveDevicePaths predates that hardening (confirmed via git history: it already
  existed, unguarded, in commit dc93b66, before 4777c40 where ObservePostApplyStability's per-tick
  catch was added) and was never retrofitted. A transient tick failure here aborted ActivateMonitors
  entirely -- BEFORE its own correction loop (fix H), its own final verify-and-throw, and its own
  ObservePostApplyStability call ever ran for that invocation -- even though the CCD mutation itself
  had already succeeded moments earlier. (II) MainForm.OnTileAction's `finally` block called
  ArmIntentGuard() unconditionally, with no awareness of whether the try block's preceding
  ActivateMonitors/DeactivateMonitors call actually completed or threw. ArmIntentGuard() snapshots
  whatever RefreshMonitorTiles() just observed as "the deliberately-intended final state" -- so when
  the underlying call threw (via mechanism I, or any other exception), the ACCIDENTAL post-failure
  topology got baked in as if it were correct, permanently blinding TryReactivelyCorrectAgainstLastIntent
  to that exact drift for the rest of the (now-wrong) guard window. Direct log evidence confirms both:
  the exact stack trace for (I), and a wrongly-armed intent snapshot (SAM7489:inactive, DELA0BC:active
  -- the accidental post-failure state) for (II), roughly 10 seconds after the throw. The
  ToggleOrchestrator._busy "action already in progress" guard itself was checked and found correctly
  released via try/finally/Dispose() in all cases -- it is NOT the stuck-boolean mechanism originally
  hypothesized; the two rejected reactive-correction attempts in the log were a CORRECT rejection
  (the busy lease was still legitimately held while OnTileAction had not yet returned), not evidence of
  a guard defect.
fix_addendum: >
  Fix I: src/RigToggle.Windows/WindowsMonitorController.cs, PollUntilStableActiveDevicePaths only --
  rewrote its polling loop to wrap each QueryActiveDevicePaths() call in try/catch, mirroring
  ObservePostApplyStability's own per-tick pattern exactly: a failed attempt is logged and skipped
  (the loop continues, keeping the last known-good reading), never propagated. If every attempt in the
  budget fails, returns an empty set (matching this method's pre-existing "always return something,
  never throw" contract) instead of throwing. Byte-for-byte behavior-identical for the all-successful
  case (the overwhelmingly common path) -- attempt 1 still reads with no sleep, subsequent attempts
  still sleep SettlePollDelay first, and the "two consecutive reads agree" early-return still applies
  identically once two GOOD reads are obtained.
  Fix J: src/RigToggle.App/MainForm.cs, OnTileAction only (both the enable and disable branches) --
  introduced a local success flag (`deactivateSucceeded` / `activateSucceeded`), set immediately before
  each branch's existing "returned without throwing" trace line, and gated the `finally` block's
  ArmIntentGuard() call on that flag. RefreshMonitorTiles() still always runs unconditionally in
  `finally` (so the tile dashboard itself stays accurate on both success and failure) -- only the
  intent-guard re-arm is now conditioned on genuine success. On failure, a trace line documents the
  skip and any previously-armed (potentially still-valid) intent snapshot is left untouched rather than
  being overwritten with the failed action's accidental result.
verification_addendum:
  target_test: { result: pending, reason: "Awaiting rig verification -- same structural sandbox limitation as every prior round of both debug sessions. Self-verified via direct code read/hand-trace of both modified control flows against this round's specific supplied log (the exact stack trace for Fix I; the exact wrongly-armed intent snapshot for Fix J) plus a full-solution build and the existing RigToggle.Tests suite (unchanged, 238/238 pass)." }
  mutation_check: { result: skipped, reason: "No Stryker configured in this repo (matches every prior debug session in this codebase)." }
  no_op_deletion: { result: pass, deletion_justified_by_rca: true, note: "Fix I: no code deleted, only new try/catch wrapping and a restructured (but behavior-preserving on the success path) loop shape -- confirmed via hand-trace that attempt numbering, sleep timing, and the two-consecutive-reads-agree early-return are all unchanged for the all-successful case. Fix J: no code deleted -- RefreshMonitorTiles() unconditional call is preserved byte-for-byte; only ArmIntentGuard() gained a conditional guard plus an else-branch trace line (net addition, not a deletion)." }
  adjacent_tests: { result: pass, suites_run: ["dotnet build RigToggle.sln --no-incremental (0 errors, same 6 pre-existing unrelated xUnit1031 warnings, all 6 projects)", "dotnet test src/RigToggle.Tests (238/238 passed, unchanged)", "dotnet test src/RigToggle.Windows.Tests (builds cleanly; execution aborts with the same pre-existing sandbox limitation as every prior round -- missing Microsoft.WindowsDesktop.App runtime on this Linux sandbox -- unrelated to either change, neither of which touches a unit-tested seam: WindowsMonitorControllerTests.cs covers only the pure seams -- MergeAllMonitors, ComputeUnexpectedlyActivated/Deactivated, PromoteToOriginIfNeeded, AnyRectanglesOverlap, ComputeUndetectedDevicePaths -- none of which were touched by Fix I; MainForm.cs has no dedicated test project in this codebase, same pre-existing gap as every prior MainForm.cs change across both debug sessions)"] }
  revert_and_reconfirm: { result: pending, reason: "Requires a rig trial reproducing either mechanism (a transient TargetNotAvailableException during a post-apply settle-poll for Fix I; any ActivateMonitors/DeactivateMonitors exception for Fix J) -- deferred to the updated human-verify checkpoint below." }
  guardrail_verdict: accepted
  guardrail_note: "All self-executable signals pass for both fixes: no-op/deletion review confirms no behavior loss on the success path for either change (RCA-justified additions, not silent deletions); full-solution build is clean; the existing 238-test RigToggle.Tests suite is unchanged. Neither fix touches a unit-tested seam in either project (WindowsMonitorControllerTests.cs's pure seams are untouched by Fix I; MainForm.cs has never had a dedicated test project across either debug session, a pre-existing gap, not one introduced by this round). Hardware-dependent behavior (does the settle-poll actually tolerate a transient unavailable-target read without a real rig repro; does the intent guard actually stay usefully armed after a real failure) is deferred to the mandatory human-verify checkpoint."
files_changed_addendum:
  - src/RigToggle.Windows/WindowsMonitorController.cs
  - src/RigToggle.App/MainForm.cs

## Resolution (round 10 addendum -- no new fix; classification of a new failure as the same open item)

round_10_assessment: >
  A fresh debug.log excerpt (checkpoint response to round 9's ask -- NOT a "confirmed fixed" reply)
  captures a NEW failure: ActivateMonitors(SAM7489/"Odyssey G5") threw InvalidOperationException
  ("Monitor enable did not take effect... D-05") after the requested target never appeared active
  across 5 settle-poll attempts (2 correctly tolerated as TargetNotAvailableException per fix 2) and
  2 correction rounds (both correctly finding nothing to correct: unexpectedlyActivated=[]
  unexpectedlyDeactivated=[] both times, since SAM7489 was never a pre-existing survivor -- it was
  the newly-requested target itself failing to activate, a scenario fix H's correction was never
  designed to address). Direct git-blame confirms the throw/verify-and-throw logic that fired
  (WindowsMonitorController.cs lines 690-701/697/699) predates this session's fixes by 7+ days
  (dc93b66, 2026-08-22; 90fe29d, 2026-07-28) -- this is NOT new code this session introduced, and
  not a side effect of round 8/9's fixes. Classified as a THIRD observed failure shape of the SAME
  still-open, never-root-caused driver/OS-level instability already documented in the resolved
  session's Resolution.root_cause (8) (there: scoped-plan throws PathChangeException -> Extend
  fallback -> an unrequested survivor accidentally drops, while the requested target still activates;
  here: scoped-plan reports API-level success with no exception at all, yet the requested target itself
  never actually comes active on the hardware). No new root cause, no new fix, and no new AND-gate
  branch added this round -- per this whole two-session investigation's own repeatedly-reaffirmed
  research-vs-reasoning discipline (10 total rounds have not root-caused the underlying driver/CCD
  trigger for this specific SAM74xx+ACI24A4/DELA0Bx monitor pairing; guessing at a fix now would repeat
  an already-flagged failure pattern). Fixes 1 (CacheLiveModes widening) and 2 (PollUntilStableActive
  DevicePaths per-tick tolerance) are both independently CONFIRMED firing correctly in this exact
  trial via direct log-line-to-source-line matching -- neither is implicated in, or responsible for,
  this new failure. Fix 3 (OnTileAction's conditional ArmIntentGuard) is NEITHER confirmed nor refuted
  by this excerpt -- the decisive trace line ("ArmIntentGuard SKIPPED") only fires after the user
  dismisses the modal MessageBox this exact exception type raises (MainForm.cs lines 1305-1309), and
  the excerpt most plausibly ends before that dismissal. Separately confirmed: this failure was NOT
  silent at the UI level -- OnTileAction's catch(InvalidOperationException) branch calls
  MessageBox.Show with the raw D-05 exception text, contradicting the "silent" framing inherited from
  the ORIGINAL resolved bug (which genuinely never surfaced anything).
verification_round_10:
  target_test: { result: not_applicable, reason: "No new fix applied this round -- this is a classification/verification pass over the 3 already-applied fixes plus an honest assessment of a new failure, not a new code change requiring its own verification cycle." }
  fixes_1_and_2_reconfirmed: { result: pass, note: "Both independently reconfirmed firing correctly in a real rig trial via exact log-line-to-source-line matching (see Evidence round 10) -- a stronger signal than round 9's own hand-trace-only self-verification, since this is now backed by a live, real-hardware occurrence of both fix paths firing." }
  fix_3_status: { result: inconclusive, reason: "This excerpt cannot confirm or refute fix 3's conditional-arm behavior -- the relevant trace line is structurally absent, most plausibly because it fires only after a dialog dismissal this excerpt likely predates. Carried forward as an open verification item, not a failure. SUPERSEDED by round 12: a later-supplied excerpt captured the decisive 'ArmIntentGuard SKIPPED' trace line directly, verified byte-for-byte against MainForm.cs -- see Resolution round 12 addendum (fix_3_status_correction). Fix 3 is now CONFIRMED, not inconclusive." }
  new_failure_classification: { result: same_open_item, note: "Classified as a third shape of the resolved session's own still-open Resolution.root_cause (8), not a new fourth code-level defect -- confirmed via git blame that the exact throw/verify logic predates every fix applied this session." }
  guardrail_verdict: not_applicable
  guardrail_note: "No new fix was proposed or applied this round, so the fix-acceptance guardrail does not apply. The 3 already-applied fixes from rounds 8/9 keep their prior 'accepted' guardrail verdicts (see Resolution and Resolution round 9 addendum above), now additionally reinforced by real-rig-trial log evidence for fixes 1 and 2 specifically."
files_changed_round_10: []

## Resolution (round 11 addendum -- instrumentation only, NO new root cause and NO fix for root_cause (8) claimed)

root_cause_status_round_11: >
  UNCHANGED and explicitly NOT re-guessed at this round: root_cause (8) (the resolved session's own
  never-root-caused, intermittent CCD/driver instability specific to the SAM748A/SAM7489("Odyssey
  G5")+ACI24A4/DELA0Bx monitor pairing) remains open, with its three named candidate mechanisms
  (stale/incompatible source candidate; hardware source/port-group constraint; cached-mode mismatch)
  still undiscriminated. Per this session's own explicit instruction and this whole two-session
  investigation's repeatedly-reaffirmed research-vs-reasoning discipline, NO fix or root-cause claim
  is made this round -- 10 prior rounds of guessing at this would repeat an already-flagged failure
  pattern. This round instead identifies and closes a genuine OBSERVABILITY gap (see Evidence round 11)
  that has existed since TryBuildScopedActivationPlan was first introduced (monitor-enable-reactivates-
  others-again, round 5) and was never subsequently instrumented for source-identity visibility across
  any of the 10 prior rounds of either session.
instrumentation_added: >
  src/RigToggle.Windows/WindowsMonitorController.cs, three additive Log() additions, no control-flow
  change: (1) TryBuildScopedActivationPlan now logs every GetAllPaths() candidate PathDisplaySource for
  each requested device path (tagged [claimed]/[unclaimed]) before its existing "first unclaimed"
  selection runs. (2) Immediately after selection, logs the chosen PathDisplaySource (GPU adapter LUID +
  numeric source id) and whether it matches the SAME target's own previously-cached source (round 8's
  own already-documented, never-fixed source-claim-greediness blind spot). (3) ActivateMonitors logs a
  full per-entry structural dump (source, mode-info-available, position, per-target device path + active
  flag) of the entire scoped plan array immediately before it is submitted to ApplyPathInfos, firing
  regardless of whether that call subsequently throws (root_cause (8)'s first observed shape) or reports
  success while the target never actually activates (root_cause (8)'s third observed shape, round 10).
  Implemented via a new internal, unit-tested DescribeSource helper (2 new tests, hand-traced -- this
  sandbox cannot execute RigToggle.Windows.Tests) and a new private DescribeScopedPathEntry helper (not
  unit-testable -- see Evidence round 11's decompile finding: PathDisplayTarget.DevicePath requires a
  live CCD query). One dictionary lookup (_lastKnownActiveModeByDevicePath.TryGetValue for devicePath)
  was relocated earlier in the same loop iteration so the new log line and the existing mode-selection
  branch share it instead of querying twice -- confirmed behaviorally identical, not a logic change.
deliberately_out_of_scope_this_round: >
  Windows Event Log correlation and EDID/monitor capability capture were both considered and NOT
  implemented -- see Current Focus's round_11_investigation_note for the full reasoning (Event Log:
  would require guessing at an unconfirmed channel/provider with no evidence any logs anything relevant;
  EDID: speculative diagnostic value for a runtime CCD-apply-timing failure plus a meaningfully larger,
  unproven-value implementation surface for a single low-risk/additive round). Both are documented as
  manual, out-of-band options for the user in the checkpoint below, not fabricated into the app on
  guesswork.
verification_round_11:
  target_test: { result: not_applicable, reason: "No fix applied this round -- pure additive instrumentation, not a behavioral change requiring a fix-acceptance verification cycle in the same sense as rounds 8/9. Self-verified via build + full test suite instead (see below), matching this round's own 'observability, not correction' scope." }
  mutation_check: { result: skipped, reason: "No Stryker configured in this repo (matches every prior debug session in this codebase)." }
  no_op_deletion: { result: pass, deletion_justified_by_rca: true, note: "No existing logic deleted. The only structural change is the relocation of one _lastKnownActiveModeByDevicePath.TryGetValue call earlier in the same loop iteration (from its own inline `if` condition to a standalone statement whose out-parameter is reused by both the new log line and the existing, unchanged mode-selection `if (cachedMode != null)` branch) -- confirmed behaviorally identical: TryGetValue's bool return and its out-parameter being null-when-absent are equivalent for this reference type, so the post-move `if (cachedMode != null)` gate is byte-for-byte equivalent to the pre-move `if (_lastKnownActiveModeByDevicePath.TryGetValue(...))` gate." }
  adjacent_tests: { result: pass, suites_run: ["dotnet build RigToggle.sln --no-incremental (0 errors, same 6 pre-existing unrelated xUnit1031 warnings, all 6 projects)", "dotnet test src/RigToggle.Tests (238/238 passed, unchanged)", "dotnet build src/RigToggle.Windows.Tests (0 warnings, 0 errors, includes 2 new DescribeSource unit tests)", "dotnet test src/RigToggle.Windows.Tests (builds cleanly; execution aborts with the same pre-existing sandbox limitation as every prior round -- missing Microsoft.WindowsDesktop.App runtime on this Linux sandbox -- the 2 new tests were hand-traced against the implementation instead, both pass by hand-trace)"] }
  revert_and_reconfirm: { result: not_applicable, reason: "This round adds observability only, not a behavioral fix -- there is nothing to revert-and-reconfirm against a symptom, since no symptom is claimed fixed this round. Awaiting a fresh rig trial reproducing root_cause (8) to actually USE this new instrumentation, deferred to the checkpoint below." }
  guardrail_verdict: accepted
  guardrail_note: "All self-executable signals pass: no-op/deletion review confirms the one relocated lookup is behaviorally identical (not a silent logic change); full-solution build is clean; the existing 238-test RigToggle.Tests suite is unchanged; RigToggle.Windows.Tests builds cleanly with 2 new hand-traced unit tests for the one newly-testable pure helper (DescribeSource). DescribeScopedPathEntry is confirmed, by decompile (not assumed), to require a live CCD query for its per-target DevicePath read and so cannot be unit-tested within this file's own established test-seam boundary -- self-verified via build + hand-trace only, matching the same constraint already documented for ActivateMonitors/DeactivateMonitors themselves. This round makes NO root-cause or fix claim for root_cause (8) -- the guardrail applies to the INSTRUMENTATION change's own correctness/safety, not to a resolution of the underlying bug, which remains explicitly open."
files_changed_round_11:
  - src/RigToggle.Windows/WindowsMonitorController.cs
  - src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs

## Resolution (round 12 addendum -- Fix 3 status corrected to CONFIRMED; no new root cause; round 11's checkpoint ask still open)

round_12_assessment: >
  A freshly-supplied debug.log excerpt arrived from the user, but on direct inspection its build
  banner (builtAt=2026-08-29 22:29:49) is byte-identical to round 10's excerpt build and therefore
  predates round 11's own new instrumentation (added later in this same session) -- confirmed by the
  absence of any of round 11's new log-line text anywhere in this excerpt. This excerpt CANNOT
  exercise or validate round 11's instrumentation, and does NOT answer round 11's still-open checkpoint
  ask (a repro captured on a build that includes it) -- that ask remains open and unchanged, see the
  updated checkpoint below. This excerpt is, however, valid evidence for two things processed this
  round: (1) it directly confirms Fix 3 (round 9's conditional ArmIntentGuard re-arm in MainForm.cs) is
  genuinely working -- a byte-for-byte match of the code's own "ArmIntentGuard SKIPPED" trace line
  fired immediately following the first (failed) ActivateMonitors(SAM7489) attempt, before the user's
  second click succeeded, correcting round 10's Resolution addendum verdict of "inconclusive" (see
  Evidence round 12 for the direct code-to-log match). (2) it is a second real-world occurrence of
  root_cause (8)'s third failure shape (the D-05 InvalidOperationException), with a notably different
  settle-poll exception-timing signature than round 10's occurrence (ticks 1+2 failing here vs. ticks
  2+3 there) -- recorded as an additional data point, not a new root-cause conclusion; no
  instrumentation exists in this build to discriminate between root_cause (8)'s remaining candidate
  mechanisms, which is exactly what round 11 added and this excerpt predates.
fix_3_status_correction: >
  CORRECTED from round 10's Resolution addendum verdict of "inconclusive" to CONFIRMED. Round 10's
  excerpt structurally could not contain the decisive trace line (its capture window ended before the
  user could plausibly have dismissed the modal MessageBox). This round's excerpt captures the full
  sequence: the D-05 throw at 22:46:52.048, then -- after a gap consistent with a blocking
  MessageBox.Show() dialog being shown and dismissed -- the exact "ArmIntentGuard SKIPPED --
  ActivateMonitors(...) threw..." trace line at 22:46:54.706, verified word-for-word against
  MainForm.cs's own Log() call (lines 1332-1336). Fix 3 is now confirmed via direct, unambiguous,
  real-hardware evidence, not merely hand-traced code review.
root_cause_status_round_12: >
  UNCHANGED -- root_cause (8) remains open, exactly as carried forward from round 11. No new root
  cause is claimed or fabricated from this round's timing-difference observation. The instrumentation
  needed to discriminate between root_cause (8)'s three named candidate mechanisms was added in round
  11, AFTER this excerpt's build was compiled -- this excerpt genuinely cannot speak to which mechanism
  is responsible, and round 11's own checkpoint ask (a fresh, post-instrumentation repro) is not
  superseded or answered by it.
verification_round_12:
  target_test: { result: not_applicable, reason: "No new fix applied this round -- this is an evidence-processing and status-correction pass over an already-applied fix (Fix 3, round 9) plus an additional timing data point for an already-carried-forward open item (root_cause (8)), not a new code change." }
  fix_3_status: { result: confirmed, reason: "Direct byte-for-byte match of the supplied excerpt's 'ArmIntentGuard SKIPPED' trace line against MainForm.cs lines 1332-1336 -- fired at the exact code location and under the exact condition (activateSucceeded == false) Fix 3 was designed to guard. Corrects round 10's 'inconclusive' verdict, which was accurate given the data available at that time (round 10's excerpt ended before the trace line could have fired)." }
  round_11_checkpoint_status: { result: still_open, reason: "This excerpt predates round 11's instrumentation (same build as round 10's excerpt) -- it cannot exercise or validate the new PathDisplaySource/scoped-plan-entry logging. Round 11's ask (a fresh repro on a build that includes the new instrumentation) is unanswered and remains the active checkpoint." }
  guardrail_verdict: not_applicable
  guardrail_note: "No new fix was proposed or applied this round -- this is a status correction (Fix 3: inconclusive -> confirmed) backed by newly-supplied direct evidence, plus an additional carried-forward timing data point for the still-open root_cause (8). The fix-acceptance guardrail does not apply; Fix 3 itself keeps its original round-9 'accepted' guardrail verdict, now further reinforced by real-hardware confirmation."
files_changed_round_12: []

## Resolution (round 13 addendum -- root_cause (8) narrowed via a controlled fail/succeed comparison; a second, independent code-level defect identified; two candidate fixes proposed, NEITHER applied)

root_cause_status_round_13: >
  root_cause (8) remains OPEN in the strict "what is the underlying OS/driver mechanism" sense --
  no fix is applied and no such mechanism is confirmed this round. However, this round's evidence
  meaningfully NARROWS it for the first time across 13 rounds/two sessions: a byte-for-byte
  identical scoped-activation plan (same selected PathDisplaySource, same blank mode) failed once
  and succeeded 7 seconds later for the same requested target -- directly eliminating
  "stale/incompatible source candidate" and "cached-mode mismatch" as explanations for this
  failure shape (root_cause (8)'s third observed shape) and confirming the residual, best-
  supported explanation is genuine external CCD/driver-level timing nondeterminism, independent
  of anything this app's own code selects. Separately, and independently, a genuinely different,
  real, code-level defect was newly identified (TryBuildScopedActivationPlan's greedy
  source-claim selection pairing a cached position with a freshly different, unvalidated source)
  -- correlated with, but not proven to cause, a DELA0BC-flapping instability observed in the
  same trial. Per this whole investigation's own repeatedly-reaffirmed research-vs-reasoning
  discipline (and this round's own explicit instruction), NEITHER of the two candidate fixes this
  narrowing suggests is implemented this round -- both are proposed as hypotheses requiring
  self-verification, and the first ideally one more confirming rig trial, before being written.
candidate_fix_A_not_yet_applied: >
  Bounded automatic retry inside ActivateMonitors, specifically for the "requested target itself
  never settles active despite ApplyPathInfos reporting success" failure shape (distinct from,
  and not a replacement for, fix H's existing lost-survivor correction) -- re-attempt the whole
  scoped build+apply+settle sequence a small bounded number of times before surfacing D-05, since
  this round directly observed an immediate retry with byte-identical inputs succeeding. A
  genuine control-flow change (unlike round 11's pure logging), needing careful design (its
  interaction with the existing correction-round budget, its own logging/distinguishability from
  a user-initiated retry) before being written -- not sketched further this round.
candidate_fix_B_not_yet_applied: >
  TryBuildScopedActivationPlan should prefer reclaiming a requested target's own previously-
  cached PathDisplaySource, when present in the candidate list and unclaimed, before falling back
  to the existing greedy first-unclaimed selection -- closes round 8's own long-standing,
  never-fixed source-claim-greediness blind spot and prevents ever pairing a cached position with
  a source it was not captured under. Lower-risk than fix A (a pure selection-preference change,
  no new control flow) but its causal contribution to root_cause (8)'s actual D-05/flapping
  failures (as opposed to being merely correlated with this one occurrence) is unconfirmed.
verification_round_13:
  target_test: { result: not_applicable, reason: "No fix applied this round -- both candidate fixes are proposed hypotheses only, per this round's explicit instruction not to rush implementation for a bug this stubborn in the same round a plausible mechanism is first identified." }
  mutation_check: { result: skipped, reason: "No Stryker configured in this repo (matches every prior debug session in this codebase); also not_applicable -- no fix applied." }
  no_op_deletion: { result: not_applicable, reason: "No code changed this round." }
  adjacent_tests: { result: not_applicable, reason: "No code changed this round -- existing 238-test RigToggle.Tests suite and RigToggle.Windows.Tests remain exactly as left at the end of round 11 (round 12 also made no code change)." }
  revert_and_reconfirm: { result: not_applicable, reason: "No fix to revert -- this round is evidence analysis and hypothesis-narrowing only." }
  guardrail_verdict: not_applicable
  guardrail_note: "No fix was proposed or applied this round -- the fix-acceptance guardrail applies only when a fix is actually written. Both candidate fixes above are recorded as open, unimplemented hypotheses for a future round's own guardrail pass once (and if) they are written."
files_changed_round_13: []

## Resolution (round 14 addendum -- both candidate fixes from round 13 IMPLEMENTED and self-verified; checkpoint response to round 13's Option A)

root_cause_status_round_14: >
  root_cause (8) remains OPEN in the strict "what is the underlying OS/driver mechanism"
  sense -- neither fix applied this round claims to have identified or eliminated that
  mechanism. Both fixes are targeted mitigations for the SPECIFIC failure shapes round 13's
  evidence directly supports: fix A for the "scoped apply succeeds but the requested target
  never settles" shape (proved recoverable via retry by round 13's byte-for-byte fail/succeed
  comparison), and fix B for the source-claim-greediness defect round 13 directly observed
  (a cached position paired with a freshly different, unvalidated source). Per the user's
  own explicit Option-A instruction and this investigation's established discipline, this is
  recorded precisely: these are mitigations, not a claim that root_cause (8) is "fixed."
root_cause_addendum: >
  Fix A's root cause: ActivateMonitors' pre-round-14 verify-and-throw had no retry of any
  kind -- a single scoped-attempt failure (of the specific "reports success but never
  settles" shape) always terminated in the D-05 exception, even though round 13 directly
  proved an immediate, identical-input retry can succeed. Fix B's root cause:
  TryBuildScopedActivationPlan's candidate selection (round 8's own long-documented,
  never-fixed blind spot) picked the first unclaimed PathDisplaySource unconditionally, with
  no preference for reclaiming a target's own previously-cached source, even when doing so
  was possible -- round 13 directly observed the consequence (a cached position paired with
  a source it was never captured under). Both are genuinely independent code-level defects
  in two different methods -- see round_14_reasoning_checkpoint's own and_gate field.
fix_addendum: >
  Fix A: src/RigToggle.Windows/WindowsMonitorController.cs, ActivateMonitors only -- wrapped
  the existing scoped-build+apply+settle+correct+verify sequence in a bounded retry loop
  (MaxScopedActivationRetryAttempts=2, i.e. up to 3 total attempts), gated by the new pure
  helper ShouldRetryScopedActivation (usedScopedActivation AND at least one of the call's OWN
  requested targets still inactive AND retry budget remaining). Every live-CCD-state variable
  is freshly recomputed each attempt. Fix H's own lost-survivor correction loop is completely
  unchanged and unaffected, running inside every attempt exactly as before. Logs a distinct
  "round 14 (fix A) -- INTERNAL automatic retry N/M" line on each retry, clearly distinguishable
  from a user-initiated retry (which always produces its own fresh "ActivateMonitors: ENTER
  ..." line instead), and still surfaces the unchanged D-05 exception if all bounded attempts
  are exhausted -- no silent swallowing of a persistent failure.
  Fix B: src/RigToggle.Windows/WindowsMonitorController.cs, TryBuildScopedActivationPlan only
  -- added the new pure helper SelectSourceForActivation, which prefers reclaiming a
  requested target's own previously-cached PathDisplaySource when it is present among this
  call's own unclaimed candidates, falling back to the existing greedy first-unclaimed pick
  (byte-for-byte unchanged) whenever the preference does not apply. Logs a new "round 14
  (fix B) -- source-preference check" line recording the outcome; the existing round-11
  "selected ... (matches ...)" log line is preserved byte-for-byte.
verification_round_14:
  target_test: { result: pass, note: "SelectSourceForActivation and ShouldRetryScopedActivation are the two decision points that actually changed behavior in this round -- both are pure functions, fully unit-tested (5 tests each, all branches covered: preferred-match, no-cache-entry fallback, cached-source-unavailable fallback, no-candidates-at-all, sole-candidate-reclaimed for fix B; retryable-success-shape, extend-fallback-never-retried, survivor-only-never-retried, budget-exhausted, last-allowed-attempt-still-retries for fix A). Hand-traced against the implementation (this sandbox cannot execute RigToggle.Windows.Tests -- same pre-existing, unrelated limitation as every prior round of both debug sessions), and PathDisplaySource's value-based equality (which SelectSourceForActivation's Contains-based preference check depends on) was additionally spot-checked directly against the real WindowsDisplayAPI 1.3.0.13 assembly via a standalone reflection probe, confirming Equals/GetHashCode/op_Equality all agree by Adapter+SourceId value, not reference identity." }
  mutation_check: { result: skipped, reason: "No Stryker configured in this repo (matches every prior debug session in this codebase)." }
  no_op_deletion: { result: pass, deletion_justified_by_rca: true, note: "Fix A: the pre-round-14 single-pass scoped-build+apply+settle+correct+verify sequence was not deleted, only wrapped in a bounded for-loop -- confirmed via direct before/after comparison that the first-iteration code path (the overwhelmingly common, all-successful case) is byte-for-byte behavior-identical, with `break` firing before any new retry-specific code runs. Fix B: the removed greedy `allPaths.FirstOrDefault(...)` + null-check is fully subsumed by SelectSourceForActivation's fallback branch -- confirmed the same LINQ Where/Select predicate is applied in the same enumeration order, so the fallback pick is identical to the old greedy pick whenever the new preference does not apply. No control flow, correction loop, or verify-and-throw exception message was altered for the non-retry, non-preference-match cases." }
  adjacent_tests: { result: pass, suites_run: ["dotnet build RigToggle.sln --no-incremental (0 errors, same 6 pre-existing unrelated xUnit1031 warnings, all 6 projects)", "dotnet test src/RigToggle.Tests (238/238 passed, unchanged)", "dotnet build src/RigToggle.Windows.Tests (0 warnings, 0 errors, includes 10 new unit tests -- 39 total, up from 29)", "dotnet test src/RigToggle.Windows.Tests (builds cleanly; execution aborts with the same pre-existing sandbox limitation as every prior round -- missing Microsoft.WindowsDesktop.App runtime on this Linux sandbox -- the 10 new tests were hand-traced against the implementation instead, all pass by hand-trace)"] }
  revert_and_reconfirm: { result: pending, reason: "Requires a rig trial reproducing either failure shape (fix A: a scoped apply that succeeds but the requested target never settles; fix B: a requested target with a previously-cached source that differs from the fresh greedy pick) -- deferred to the human-verify checkpoint below." }
  guardrail_verdict: accepted
  guardrail_note: "All self-executable signals pass for both fixes: no-op/deletion review confirms no behavior loss on the common/success path for either change (both are RCA-justified additions with byte-for-byte-preserved fallback behavior, not silent deletions); full-solution build is clean; the existing 238-test RigToggle.Tests suite is unchanged; RigToggle.Windows.Tests builds cleanly with 10 new unit tests covering every branch of both newly-extracted pure decision helpers (SelectSourceForActivation, ShouldRetryScopedActivation), matching this file's own established pattern of distinguishing testable pure helpers (like DescribeSource, PromoteToOriginIfNeeded) from untestable live-CCD-query code (DescribeScopedPathEntry, the retry loop's own live PathInfo calls) -- verified directly against the actual code shape, not assumed. Hardware-dependent behavior (does the retry actually recover a real D-05-shaped failure; does the source preference actually prevent a real flapping/mismatch instance) is deferred to the mandatory human-verify checkpoint. Per this round's own explicit instruction, root_cause (8) is NOT claimed fixed -- only these two specific, evidenced failure shapes are mitigated."
files_changed_round_14:
  - src/RigToggle.Windows/WindowsMonitorController.cs
  - src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs

## Resolution (round 15 addendum -- first real-world Fix-A retry firing; did not recover; retry-timing assessed, NOT changed this round; external-vs-own-code reassessed)

fix_a_effectiveness_reassessment: >
  Fix A fired for the first time in the real world (confirmed, byte-for-byte, against source) and
  did NOT recover this occurrence -- the retry landed in a state (0 candidate PathDisplaySources,
  forcing the Extend fallback) that is structurally worse than the original attempt's (5
  candidates). This is NOT evidence that Fix A's TRIGGER condition (ShouldRetryScopedActivation) is
  broken or wrongly designed -- traced directly against this trial's actual values, it fired
  exactly when designed to (usedScopedActivation=true, one requested target still inactive, budget
  available) and correctly refused a second retry once THIS retry itself fell into the
  Extend-fallback shape it deliberately excludes. What this round adds that round 13 could not:
  round 13's one confirming data point (an identical plan succeeding on retry) came from a MANUAL
  retry roughly 7 seconds later; this round's retry, automatic and back-to-back with zero delay,
  landed inside the SAME external unavailability window the original attempt was already
  exhibiting symptoms of (two TargetNotAvailableException events during that SAME first attempt's
  own settle-poll, only ~530-700ms before the retry's own GetAllPaths() query). Fix A's underlying
  premise ("an immediate retry can recover this shape") is not refuted outright -- it was proven
  true once, under conditions (~7s delay, apparently outside the flap window) different from this
  round's (~1.36s delay, apparently inside it) -- but this round narrows what "immediate" safely
  means and suggests sub-2-second automatic retries may be systematically less likely to succeed
  than a human's naturally slower manual retry, for this specific transient-unavailability
  mechanism.
retry_timing_assessment: >
  Confirmed directly from code: Fix A adds no backoff of any kind -- the retry loop's `continue`
  re-enters immediately, and the ~1.36s elapsed before the retry fired was entirely consumed by
  the settle-poll-then-correct loop's own pre-existing delays, not a deliberate Fix A pause.
  Confirmed a plausible, evidence-grounded (not merely "seems safer") mechanism by which this can
  make things worse: the retry sampled GetAllPaths() ~530-700ms after the SAME target had already
  thrown TargetNotAvailableException twice in the ORIGINAL attempt's own settle-poll -- i.e. inside
  a transient-unavailability episode already observed to still be running, not after it cleared.
  Additionally confirmed a related, previously-unexamined nuance: because an Extend-fallback
  attempt is excluded from ShouldRetryScopedActivation's usedScopedActivation gate, a retry that
  lands mid-flap doesn't just fail to recover -- it forfeits the REST of the retry budget (only 1
  of 2 allotted retries was usable this trial). NOT implementing a timing change this round: (a)
  single-trial evidence, the exact caution this investigation has repeatedly required (rounds 10,
  11, 13) before touching timing-sensitive code; (b) the underlying episode's characteristic
  duration is unknown beyond "at least ~700-900ms this one time" -- picking a specific delay value
  now would itself be an unjustified guess. PROPOSED, NOT APPLIED: candidate Fix K -- a short
  bounded delay, OR a more principled short bounded poll for the target's own IsAvailable flag
  returning true (reusing PollUntilStableActiveDevicePaths' existing bounded-poll idiom) -- before
  each Fix-A retry attempt's own CacheLiveModes/TryBuildScopedActivationPlan call. This decision is
  returned to the user as a checkpoint, matching round 13's own precedent of proposing candidate
  fixes without implementing them until the user decides.
external_vs_own_code_reassessment: >
  Symptom 1 (round 8 CacheLiveModes/fix-H gap) -- UNCHANGED, reaffirmed; nothing in this round's
  evidence touches that classification. D-05 throw/verify age classification -- RE-VERIFIED
  directly this round (not assumed): git blame on the CURRENT, post-round-14 code confirms the
  throw statement itself (now at lines 792-794) is still attributed to commit 90fe29d
  (2026-07-28), untouched by round 14 (which only added a retry-eligibility check immediately
  above it, confirmed via `git blame` showing "Not Committed Yet" on round 14's own lines but the
  original commit hash on the throw's three lines). Classification stands, now re-confirmed
  against the live code rather than carried forward on faith. Honest assessment of whether round
  14's OWN code could be responsible for THIS failure: Fix B was not exercised as a contributing
  factor in this trace (both TryBuildScopedActivationPlan calls logged "no prior cache entry").
  Fix A's retry-ELIGIBILITY logic is confirmed, by direct trace, to behave exactly as designed,
  and queries GetAllPaths() with byte-identical code/parameters to any fresh call -- it did not
  create the target's flakiness (the flapping independently began during the ORIGINAL attempt's
  own settle-poll, well before Fix A's retry ever ran). BUT Fix A's retry TIMING (unconditional,
  zero-delay) is a genuine code-level design choice -- not the driver's own behavior -- that this
  round's evidence shows CAN determine which moment of an already-externally-caused
  unavailability window gets sampled, and a badly-timed sample was directly observed to produce a
  worse outcome than the original attempt's. Net, stated honestly: HIGH confidence the underlying
  TRIGGER (why SAM7489 flaps) remains external/driver-level, unchanged from round 13's assessment
  -- this round adds no evidence against that. MODERATE-TO-LOW confidence, newly and narrowly for
  Fix A's retry-timing parameter specifically, that "our code choices don't matter" -- WHEN we
  resample the target plausibly affects whether the retry helps or, as observed here, makes the
  immediate failure mode worse. This is a refinement of, not a reversal of, this investigation's
  prior conclusions -- the user's skepticism is partially, narrowly validated (for the retry's
  timing parameter, a genuine design choice this session made) without extending to "the
  underlying flakiness is somehow our own doing" (not supported by this round's evidence).
blind_spots:
  - "Single-trial evidence again -- same structural limitation as every round of both debug sessions, compounded here since round 13's one successful-retry data point and this round's one failed-retry data point are from DIFFERENT occurrences with unknown, possibly-different flap durations."
  - "Cannot confirm the TargetNotAvailableException flap observed at 53.348/53.512 is literally the SAME physical episode still present at 54.043 (parsimonious, not proven) -- two back-to-back distinct flap events cannot be ruled out from app-level instrumentation alone."
  - "Fix B's non-involvement this round is inferred from its own 'no prior cache entry' log lines, not independently proven irrelevant to the broader flakiness pattern."
  - "Cannot execute or verify on real Windows CCD hardware in this sandbox -- same structural limitation as every round of both debug sessions."
candidate_causes:
  - "environment/CCD-driver (root_cause (8), UNCHANGED): the underlying trigger for why SAM7489's target transiently reports unavailable remains genuinely unknown and external -- this round adds confirming, not new, evidence for this classification."
  - "code (newly evidenced this round, a DIFFERENT category -- a design PARAMETER of this session's own mitigation, not the underlying bug): Fix A's zero-delay retry timing can determine which moment of an already-external unavailability window gets sampled, and this round directly observed a badly-timed sample produce a worse outcome (0 candidates, Extend fallback, forfeited remaining retry budget) than not retrying immediately might have."
and_gate: >
  No new joint AND-gate defect identified this round. This round refines existing understanding
  (Fix A's trigger/eligibility logic is correct; its timing parameter is unexamined/unvalidated)
  and surfaces one new, code-confirmed nuance (an Extend-fallback retry forfeits the rest of the
  retry budget) directly relevant to any future timing change's design -- not a new independent
  root cause requiring its own branch.
files_changed_round_15: []
guardrail_verdict: not_applicable
guardrail_note: "No code change made this round -- investigation and reassessment only, per this round's own explicit instruction not to implement a timing change without solid justification. Candidate Fix K is proposed, not applied; the fix-acceptance guardrail does not apply to a round with no diff."

## Resolution (round 17 addendum -- ONE new, NOT-YET-FIXED candidate root cause identified (toggle-switch stale-device-path defect); Fix-A retry-timing evidence strengthened but NOT acted on; NO code change this round, pending user decision)

root_cause_addendum_item_1: >
  A genuinely new, distinct, fully-deterministic (not driver-timing-dependent) defect,
  confirmed but NOT YET FIXED, in a subsystem no prior round of this session examined:
  ToggleService.ToggleToRigMode() (src/RigToggle.Core/ToggleService.cs) reads
  settings.MonitorsToDisable/MonitorsToEnable directly from ISettingsStore.Load() and passes
  them unfiltered into WindowsMonitorController.ActivateMonitors()/DeactivateMonitors().
  SettingsForm.cs's BtnSaveSettings_Click deliberately UNIONS any previously-saved device
  path no longer live-enumerated ("stale") with the freshly-checked grid selection on every
  save, forever, with no expiry and no UI control to ever remove a stale entry (a stale path
  has no grid row and thus no checkbox). When a monitor's CCD device-path identity changes
  (replug/EDID renegotiation/driver reinstall -- confirmed here: SAM748A/UID521 and
  DELA0B8/UID516, both genuine historical device paths for this rig documented in two OLDER
  resolved debug sessions, were superseded by SAM7489/DELA0BC at some point but never
  pruned), the stale path persists in settings.json indefinitely. ActivateMonitors' own
  early-availability guard (a live PathInfo.GetAllPaths() query) throws IMMEDIATELY --
  before any of the scoped-plan-building machinery this whole session's other 8 fixes
  live inside ever runs -- the instant any requested path isn't found live, and because
  ToggleToRigMode's Monitor step is stop-on-first-failure (D-04), this blocks the entire
  toggle (including the legitimate monitors) every single time, deterministically, via the
  Rig-mode TOGGLE SWITCH specifically (ToggleSwitch_ActionRequested), never via a tile
  click. Confirmed this call path was never exercised by rounds 8-16 (all of which trace
  OnTileAction exclusively) -- and confirmed the ORIGINAL Symptom 1 complaint's own
  "Reproduction" field explicitly left "tile vs. toggle switch" unisolated, and this was
  never revisited after the investigation pivoted to the SAM7489 tile-click mechanism at
  round 8. Cannot retroactively prove this exact defect caused the original complaint (no
  record of the user's settings.json contents or control used at the time), but the
  mechanism -- a silently no-op'd toggle (zero topology mutation) with ArmIntentGuard still
  snapshotting the unchanged state as "intended" -- is directly plausible as a distinct,
  simpler explanation requiring no position-cache or source-selection defect at all.
  Ancillary, directly-evidenced compounding finding (same root defect class, different call
  site): ToggleSwitch_ActionRequested's own ArmIntentGuard() call (MainForm.cs line 647) is
  unconditional, never gated on result.Success like OnTileAction's round-9 Fix J -- exactly
  the gap round 9's own blind_spots explicitly flagged as open ("...the Rig/Normal toggle
  switch handler's own separate ArmIntentGuard call site, not touched this round... flagged
  as an open item for a future round if a toggle-triggered... recurrence is reported"),
  now directly confirmed firing in this log.
fix_item_1: >
  NOT IMPLEMENTED this round -- deliberately, per this investigation's own established
  discipline (rounds 10, 11, 13, 15) against rushing a fix for a newly-found defect the same
  round it's discovered, and per this round's explicit task instruction, given this touches
  a different subsystem (Rig-mode toggle-switch / persisted-settings loading) than anything
  examined or fixed in rounds 8-16. Candidate approaches identified, none chosen:
  (a) filter ToggleService's disable/enable sets against a live enumeration immediately
  before calling ActivateMonitors/DeactivateMonitors, surfacing a clear, specific error
  naming only the actually-stale path(s) instead of stop-on-first-failure silently blocking
  the whole toggle over one dead entry; (b) add an explicit staleness-reconciliation step
  (at settings-load time or toggle time) that prunes or migrates a stale path once a
  reasonable signal exists that it has been permanently superseded (vs. merely
  temporarily unplugged); (c) add a UI control in SettingsForm to explicitly "forget" a
  stale device path on user request, closing the "no in-app recovery path" gap directly.
  Also NOT implemented: a Fix-J-style conditional ArmIntentGuard gate for
  ToggleSwitch_ActionRequested (the ancillary finding above) -- flagged for the same
  decision checkpoint, since it would not address the underlying stale-path failure by
  itself.
root_cause_addendum_item_3: >
  NO new root cause -- root_cause (8)'s underlying OS/driver mechanism remains exactly as
  unconfirmed as round 16 left it. What changed: Fix A's retry-timing evidence (round 15)
  is now a two-data-point-per-side pattern instead of one. Direct source comparison (this
  round's task) CONFIRMS there is no discoverable code-level difference between a manual
  re-click and the internal automatic retry -- same STA UI thread, same method, same call
  stack, no COM/apartment/message-pump distinction (WindowsMonitorController.cs's CCD calls
  are plain user32.dll P/Invoke via WindowsDisplayAPI, not COM; the app's one real COM
  interop, IPolicyConfig, is the unrelated audio subsystem). The only difference remains
  elapsed wall-clock time. This round's automatic retry landed on the SAME "0 candidate
  PathDisplaySource(s)" degraded shape round 15 observed, at an almost identical ~1.36s
  gap; this round's manual recovery succeeded at a ~2.36s-4.56s gap (depending on baseline),
  consistent with (not proof beyond doubt of) round 13's ~6.58s manual-recovery data point.
fix_item_3: >
  NOT IMPLEMENTED this round -- candidate Fix K (round 15's fixed-delay Option A or
  bounded-availability-poll Option B) remains exactly as round 16 left it: proposed, not
  chosen, not applied. Per the user's own Option-C instruction (round 16) and this round's
  explicit task instruction, the strengthened-but-still-only-n=2-per-side evidence is
  reported honestly as a decision input, not acted on unilaterally.
verification_addendum:
  target_test: { result: not_applicable, reason: "No code change this round -- both items are characterized for a user decision, not fixed. Item 1 is flagged as substantial/cross-subsystem and item 3's evidence, while strengthened, remains below this investigation's own bar for picking a specific fix parameter without the user weighing in." }
  mutation_check: { result: not_applicable, reason: "No code change this round." }
  no_op_deletion: { result: not_applicable, reason: "No code change this round." }
  adjacent_tests: { result: not_applicable, reason: "No code change this round -- existing 238-test RigToggle.Tests suite and RigToggle.Windows.Tests remain exactly as left at the end of round 14." }
  revert_and_reconfirm: { result: not_applicable, reason: "No fix to revert -- no fix was applied this round." }
  guardrail_verdict: not_applicable
  guardrail_note: "No fix was proposed or applied this round -- the fix-acceptance guardrail applies only when a fix is actually written. Both items are presented to the user as a decision checkpoint below."
files_changed_addendum: []

## Resolution (round 16 addendum -- Option C recorded; no code change, no new evidence)

decision_status_round_16: >
  User's checkpoint response to round 15's decision ask: Option C -- hold off on any retry-timing
  change (candidate Fix K, in either the fixed-delay or bounded-availability-poll form proposed at
  round 15), gather one more data point first. No code change applied this round. Round 15's own
  fix_a_effectiveness_reassessment, retry_timing_assessment, and external_vs_own_code_reassessment
  all stand exactly as written -- this round neither confirms nor revises any of them, since no new
  debug.log excerpt was supplied. This is a bookkeeping round only: recording the decision, updating
  Current Focus and frontmatter status, and re-presenting the standing evidence ask (see the
  checkpoint at the end of this file), matching the precedent set by round 10's Option-B recording
  (round_11_investigation_note's context field, in Current Focus above) and round 13's Option-A
  recording (round_14_reasoning_checkpoint's context field, above) -- the same pattern applied here
  for a "gather more data" choice instead of an "implement" or "reopen" choice.
verification_round_16:
  target_test: { result: not_applicable, reason: "No code change this round -- a decision-recording round only." }
  mutation_check: { result: not_applicable, reason: "No code change this round." }
  no_op_deletion: { result: not_applicable, reason: "No code change this round." }
  adjacent_tests: { result: not_applicable, reason: "No code change this round -- existing 238-test RigToggle.Tests suite and RigToggle.Windows.Tests remain exactly as left at the end of round 14 (rounds 12, 13, and 15 also made no code change)." }
  revert_and_reconfirm: { result: not_applicable, reason: "No fix to revert -- no fix was applied this round." }
  guardrail_verdict: not_applicable
  guardrail_note: "No fix was proposed or applied this round -- the fix-acceptance guardrail applies only when a fix is actually written. Candidate Fix K remains open and unimplemented, exactly as round 15 left it, pending the evidence asked for in the checkpoint below."
files_changed_round_16: []

## CHECKPOINT REACHED (round 9 -- ANSWERED, superseded by round 10's checkpoint at the end of this file)

**Type:** human-verify
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 9 evidence entries, 1 eliminated hypothesis, across two investigation rounds (round 8: CacheLiveModes widening; round 9: two additional independent fixes from a fresh debug.log excerpt).

### Investigation State

**Three confirmed, independent contributing causes, all now fixed:**

1. **(Round 8, already applied) CacheLiveModes coverage gap** -- the resolved session's round-7 "fix H" reactivates a monitor that the CCD Extend fallback accidentally drops, but that reactivation path never had a cached position to restore -- the position-cache was only ever populated for deliberately-removed/deliberately-excluded monitors, never for one lost as an accidental side effect. Fixed by widening the existing cache call to cover every currently-active path unconditionally.

2. **(Round 9, new) PollUntilStableActiveDevicePaths missing per-tick exception handling** -- a fresh rig debug.log captured this method's own QueryActiveDevicePaths() call throwing TargetNotAvailableException UNCAUGHT (a target transiently reporting unavailable mid-CCD-renegotiation -- the same hazard ObservePostApplyStability was already hardened against in round 6, but that hardening was never applied to this older, sibling method). This aborted ActivateMonitors before its own correction loop, verify-and-throw, and ObservePostApplyStability call ever ran for that action -- even though the CCD mutation itself had already succeeded. Fixed by giving this method the same per-tick try/catch.

3. **(Round 9, new) MainForm.OnTileAction's intent guard re-armed unconditionally on failure** -- when ActivateMonitors/DeactivateMonitors threw (via cause 2 above, or any other exception), OnTileAction's `finally` block still called ArmIntentGuard(), baking in the ACCIDENTAL post-failure topology as if it were the deliberately-intended one -- permanently blinding the reactive-correction watchdog to that exact drift. Direct log evidence confirms this happened: the guard armed with the wrong, accidental state roughly 10 seconds after the exception was thrown and caught. The "action already in progress" busy-flag guard itself was checked and confirmed correctly released via try/finally/Dispose() in every case -- it is not stuck, and its two rejections in the log were correct (the lease was still legitimately held). Fixed by only re-arming the intent guard when the preceding call actually succeeded.

**Evidence:**
- git history confirms no code changes to the monitor/toggle logic since the resolved session closed -- causes 1 and 2 are both latent gaps in already-shipped code, not fresh regressions from a recent edit; cause 3's guard-re-arm logic is likewise unchanged since the resolved session's own closure.
- Direct trace of the exact code path confirms a cache-entry is structurally impossible for the specific case fix H's correction targets (cause 1).
- The supplied debug.log's own stack trace pinpoints cause 2 exactly (QueryActiveDevicePaths -> PollUntilStableActiveDevicePaths -> ActivateMonitors -> OnTileAction), and its own subsequent lines show the accidental post-failure state (DELA0BC reactivated, SAM7489 still inactive) being armed as the new "intended" baseline at 22:00:25.492 (cause 3).
- Confirmed the app's own tile numbering is stable and unaffected (rules out an app-side numbering bug as a fourth cause).
- Confirmed ToggleOrchestrator._busy is correctly released in every case via try/finally/Dispose() -- ruled out as a contributing cause.

### Checkpoint Details

**Need verification:** confirm all three fixes hold together on real rig hardware. Because the underlying trigger for both fix H's correction path AND the transient TargetNotAvailableException (cause 2) are themselves still unexplained, intermittent, driver-level conditions (carried forward, unresolved, from the original debug session's own still-open items), this may take a few normal days of use to naturally reproduce again -- this is not something that can be forced on demand.

**Self-verified checks:**
- Full solution build: 0 errors (6 pre-existing, unrelated warnings) -- unchanged across both rounds.
- Full RigToggle.Tests suite: 238/238 passed, unchanged.
- RigToggle.Windows.Tests builds cleanly (cannot execute in this sandbox -- same pre-existing limitation as the original debug session; neither round's changes touch a unit-tested seam).
- Direct code read confirms cause 1's fix is byte-for-byte behavior-identical for the swap case; cause 2's fix is byte-for-byte behavior-identical for the all-successful settle-poll case; cause 3's fix is byte-for-byte behavior-identical for the all-successful tile-action case -- all three changes only alter behavior on a failure path that previously produced a silently-wrong result.

**How to check:**
1. Use Rig Toggle normally (tile toggles and/or the Rig/Normal switch) over the next several sessions. If the app's debug.log ever shows `"ActivateMonitors: round 5 -- scoped activation ApplyPathInfos threw"` followed by `"correction round .../... unexpectedlyDeactivated=[...]"` (fix H's correction path, cause 1), or `"Post-Extend settle poll, attempt N/5 failed"` (cause 2's new per-tick tolerance), check immediately afterward whether the recovered monitor's position is correct and whether Windows' own Display Settings numbering still matches what Rig Toggle shows.
2. If ActivateMonitors/DeactivateMonitors ever throws from a tile click again (watch for `"MainForm.OnTileAction: ... threw ..."` in the log), check the very next lines for either `"ArmIntentGuard: armed"` (should NOT appear immediately after a thrown exception now) or the new `"ArmIntentGuard SKIPPED"` trace line (should appear instead) -- confirms cause 3's fix took effect.
3. If the position bug or the numbering swap reproduces again despite all three fixes, please capture the debug.log excerpt covering that moment -- that would mean a fourth, still-undiscovered mechanism is responsible.
4. Quick regression check: disable a monitor via its tile, re-enable it via the tile, confirm its position is preserved as before (unrelated to this round's changes, should be unaffected).

**Tell me:** "confirmed fixed" once you've used it normally for a while with no recurrence, or describe what's still failing (ideally with a debug.log excerpt) if it happens again.

## CHECKPOINT REACHED (round 10 -- ANSWERED: Option B, superseded by round 11's checkpoint at the end of this file)

**Status:** User chose Option B (reopen deep investigation into the driver-level flakiness itself). This triggered round 11 -- see Evidence (round 11), Resolution (round 11 addendum), and the new checkpoint at the end of this file. Original ask preserved below for historical record only -- do not act on it.

**Type:** decision (with a human-verify component)
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 10 evidence entries, 1 eliminated hypothesis, across three investigation rounds (round 8: CacheLiveModes widening; round 9: two additional independent fixes; round 10: classification of a new failure from a fresh rig trial -- no new fix applied).

### Investigation State

**This is NOT a "confirmed fixed" response.** The fresh debug.log you supplied shows a new failure: enabling the Odyssey G5 (SAM7489) tile threw an error instead of succeeding. Here is what I found:

**Good news -- both of round 9's mechanisms are now proven on real hardware, not just self-verified by code read:**
1. **Fix 1 (CacheLiveModes widening)** fired exactly as designed -- the log shows the position cache being populated for every currently-active monitor before any topology change, matching the fix's own log line exactly.
2. **Fix 2 (PollUntilStableActiveDevicePaths per-tick tolerance)** fired exactly as designed -- two real `TargetNotAvailableException` errors during the settle-poll were correctly tolerated (tick skipped, polling continued) instead of aborting the whole operation, which is precisely what this fix was built to do.

**The new failure:** After the scoped CCD apply call reported success (no exception), the Odyssey G5 never actually showed up as active across 5 settle-poll checks and 2 correction passes -- both correction passes correctly found nothing to fix (the two other monitors never flickered), because this isn't a "monitor got accidentally dropped" scenario (that's what fix H/round 7 defends against) -- it's the requested monitor itself simply never coming online, despite Windows reporting the apply call succeeded. The app's own existing safety check (which predates every fix from this session by at least a week, confirmed via git history) correctly detected this and threw an error rather than silently pretending it worked.

**My assessment: this is the SAME already-known, still-unsolved issue** documented in the original resolved debug session (`.planning/debug/resolved/monitor-position-resets-to-de.md`, Resolution root_cause item 8) -- specifically, the never-explained reason why Windows sometimes mishandles this exact pairing of monitors (the Odyssey G5 together with your other two displays). That prior investigation went 8 rounds and could not pin down why Windows' display driver behaves this way for this specific monitor combination; this session added 2 more rounds and also could not determine it. This is the THIRD different way that same underlying problem has shown up (previously: a delayed silent revert; then: a silent wrong end-state; now: an outright error). It is not a new bug this session's fixes introduced -- I confirmed the exact error-throwing code predates all three of this session's fixes.

**Two things I could not verify from the log alone, now resolved by your follow-up answers:**
- **Did you see a popup/error dialog?** CONFIRMED YES -- you reported the dialog text as "Monitor enable did not take effect," a verbatim match to the D-05 exception and the MessageBox.Show call site this session identified. This closes the question: this specific failure was genuinely NOT silent (unlike the original resolved bug).
- You additionally reported the enable failed twice in a row, then succeeded on a subsequent attempt with no code change or restart in between -- consistent with (not new evidence against) an intermittent, self-resolving driver/CCD-timing condition, matching root_cause (8)'s own historical character across both sessions.
- Round 9's third fix (conditional ArmIntentGuard re-arm) remains genuinely inconclusive -- no data was supplied on its post-dialog behavior. Carried forward as an open verification item, not a failure, per the Resolution round 10 addendum.

### Checkpoint Details

**Decision needed (still open):** How do you want to proceed, given this genuinely new (if different-shaped, and now confirmed non-silent) failure surfaced during verification of the other three fixes?

**Option A -- Accept this as a known, already-flagged limitation and close this debug session.** The underlying driver-level trigger for why this specific monitor pairing sometimes misbehaves has never been root-caused across 10 total investigation rounds spanning two sessions -- continuing to guess at fixes without new diagnostic data would repeat an already-documented failure pattern. Rig Toggle's own defenses (this session's 3 fixes, all individually working) already do the right thing when it happens: they either recover silently, or -- as in this trial -- fail loudly with a clear (if technical) error message rather than silently corrupting your monitor layout, and your own report of a successful retry immediately afterward is consistent with this being a transient condition rather than a hard failure. Closing this way means: the fixes from rounds 8/9 are accepted and kept; this specific driver-level flakiness remains a documented, open, "sometimes this monitor just won't come on and you'll see an error -- click OK and try the tile again" limitation.

**Option B -- Reopen deep investigation into the driver-level flakiness itself.** This would mean trying to determine, for the first time across both sessions, WHY Windows' CCD subsystem intermittently fails for this specific monitor combination. This is flagged as a long, low-confidence undertaking: it likely requires new kinds of diagnostic data this app doesn't currently capture (e.g. logging which display source/adapter path gets chosen internally, or correlating against Windows Event Log entries), may implicate the monitor's driver/EDID or a GPU-side auto-profile mechanism outside this app's control entirely, and has already resisted 10 rounds of investigation using the techniques available so far.

**Tell me:** "close it, accept the open item" (Option A) or "let's dig into the driver issue" (Option B). If fix 3's behavior ever needs confirming, a debug.log capture that continues a few more seconds past dismissing any dialog box would help.

## CHECKPOINT REACHED (round 11 -- ANSWERED by round 13's post-instrumentation repro; superseded by round 13's checkpoint at the end of this file)

**Type:** human-verify (with a human-action component)
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 17 evidence entries, 1 eliminated hypothesis, across five investigation rounds (round 8: CacheLiveModes widening; round 9: two additional independent fixes; round 10: classification of a new failure from a fresh rig trial; round 11: reopened deep investigation into root_cause (8) itself, per your Option-B decision -- instrumentation only, no fix or root-cause claim; round 12 (this update): processed a debug.log excerpt that arrived from you, but it turned out to be from BEFORE this round's new instrumentation was added -- it does NOT answer the ask below, which still stands unchanged. It did, however, confirm one thing and add one data point -- see the update note right below, then the original round-11 ask continues after it).

### Update (round 12): what the excerpt you sent confirmed, and why it doesn't close this checkpoint

The debug.log excerpt you sent was captured on the SAME build as the one from round 10 (build timestamp 2026-08-29 22:29:49) -- i.e. it predates the new logging this round (round 11) added to WindowsMonitorController.cs. None of the new lines this checkpoint is asking for (`candidate PathDisplaySource(s)`, `selected ...`, `scoped plan entry detail`) appear in it, so **this checkpoint's ask is still outstanding** -- I still need a fresh log capture from the rebuilt app with this round's instrumentation in it.

That said, this excerpt was still useful for two things:

1. **Fix 3 (the conditional ArmIntentGuard re-arm) is now CONFIRMED**, not "inconclusive" as I'd recorded after round 10. Your excerpt captured the exact moment: the enable attempt failed (D-05 error), and ~2.7 seconds later -- right after you'd have dismissed that error dialog -- the log shows the app logging "ArmIntentGuard SKIPPED" instead of quietly re-arming on the wrong (failed) state. I checked this word-for-word against the actual code and it's an exact match. This fix is doing its job.
2. **A second real-world instance of the same still-open driver flakiness** (root_cause (8)): the Odyssey G5 failed to enable on the first click with the same "Monitor enable did not take effect" error, then worked cleanly on your second click -- same pattern as before, though this time (as you put it) more alarming since it initially seemed like nothing would turn it on. The technical detail (which poll attempts saw the error) was slightly different from last time, but there's still no new instrumentation active yet to explain WHY -- that's exactly what I'm waiting on a fresh rebuild+repro to help answer.

No fix or root-cause conclusion is drawn from this new occurrence -- it's recorded as evidence only. The rest of this checkpoint (the original round-11 ask) is unchanged and still needs your help:

### Investigation State

**You chose Option B: reopen deep investigation into WHY this driver-level flakiness happens**, rather than accepting it as a known limitation. Per your own instructions, this round did NOT try to guess at a fix or a root cause for root_cause (8) -- 10 prior rounds across two sessions already established that guessing here repeats a documented failure pattern. Instead, this round:

1. Re-read the resolved session's full Resolution section and this session's own rounds 8-10 to confirm exactly what has already been tried/ruled out for root_cause (8) specifically (see Evidence round 11) -- three named candidate mechanisms remain genuinely undiscriminated: a stale/incompatible GPU source candidate being picked, a hardware source/port-group constraint specific to this monitor pairing, or a cached-mode mismatch.
2. Confirmed, by direct source review (not guesswork), exactly what the app's CURRENT logging does NOT capture: which specific GPU adapter/source pairing gets assigned to a monitor each time it's activated, how many candidate pairings even exist for it, and whether a failing attempt's internal plan shape differs from a succeeding one's.
3. Added new instrumentation to close that specific gap -- three additive log lines (no behavior change) that will show, the next time root_cause (8) fires in any of its three known shapes, exactly which GPU source was chosen, whether it matches what that monitor used last time, and the full internal shape of what was submitted to Windows.

**What I deliberately did NOT add, and why:**
- **Windows Event Log correlation** -- technically possible from inside the app, but building it now would mean guessing which Windows event-log channel (if any) actually records anything about this failure, with zero evidence pointing at a specific one. That would be exactly the kind of guessing this investigation has already flagged as unproductive.
- **EDID/monitor capability capture** -- technically possible (it's stored in the Windows registry), but EDID mostly describes static monitor capabilities (resolution tables, vendor ID), not the runtime timing/negotiation behavior this bug seems to involve, and is a meaningfully bigger addition for uncertain payoff.

**Self-verified checks:**
- Full solution build: 0 errors, same 6 pre-existing unrelated warnings.
- Full RigToggle.Tests suite: 238/238 passed, unchanged.
- RigToggle.Windows.Tests builds cleanly with 2 new unit tests for the one newly-testable piece (cannot execute in this sandbox -- same pre-existing limitation as every prior round of both sessions; hand-traced instead, both pass).
- No existing selection, correction, or fallback logic was changed -- confirmed via direct before/after comparison.

### Checkpoint Details

**Need verification:** none of this round's changes need YOUR verification yet (they're pure logging, self-verified above) -- what's needed now is NEW DATA, since 10 rounds of the old instrumentation were not enough to distinguish between root_cause (8)'s remaining candidate explanations.

**How to help move this forward:**
1. Use Rig Toggle normally. This cannot be forced on demand -- root_cause (8) has always been intermittent across both sessions.
2. The next time you see ANY of its three known shapes -- a silent revert, a monitor that just won't come on despite no error, or the "Monitor enable did not take effect" error dialog -- please send me the debug.log excerpt covering that moment. Specifically useful new lines to look for: `TryBuildScopedActivationPlan: ... candidate PathDisplaySource(s) ...`, `TryBuildScopedActivationPlan: selected ...`, and `ActivateMonitors: round 11 -- scoped plan entry detail ...`.
3. If you can, also send me a debug.log excerpt from a SUCCESSFUL activation of the same Odyssey G5 monitor around the same time, so I can compare a working attempt's internal shape against a failing one's.
4. Optional, and only if you're willing: the moment you see a failure, note the exact time shown in the debug.log line, open Windows Event Viewer, check "Windows Logs > System" and any "Applications and Services Logs > Microsoft > Windows" channel that looks display/GPU-related, and let me know if anything appears within a couple seconds of that timestamp. This is genuinely outside what the app itself can check, and is optional -- I can keep working from the log data above without it.

**Tell me:** send the debug.log excerpt(s) whenever root_cause (8) next reproduces (in any of its three shapes), and I'll compare the new source/plan-shape data directly against a working attempt to see whether it actually discriminates between the remaining candidate explanations. If you'd rather stop here and accept the open item after all, that's still fine to say at any point -- just let me know.

## CHECKPOINT REACHED (round 13 -- ANSWERED: Option A, superseded by round 14's checkpoint at the end of this file)

**Status:** User chose Option A (implement both candidate fixes now, self-verify, and check in for the next rig test). Both fixes are now applied -- see Resolution (round 14 addendum) and the new checkpoint at the end of this file. Original ask preserved below for historical record only -- do not act on it.

**Type:** decision
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 22 evidence entries, 1 eliminated hypothesis, across six investigation rounds since the regression reopened (round 8 through round 13).

### Investigation State

**This round finally answered round 11's ask.** You sent a debug.log excerpt captured on the rebuilt app with round 11's new instrumentation active, and it contained something even more useful than what was asked for: a failing SAM7489 (Odyssey G5) activation attempt immediately followed, 7 seconds later, by a successful one for the identical monitor, using the identical internal settings.

**What this proved:** Comparing the two attempts' full internal detail side by side, they were byte-for-byte identical -- same GPU source chosen, same (blank) position data submitted, same everything. One failed, the other succeeded. Since nothing this app controls was different between them, this rules out two of the three long-standing theories for why this monitor sometimes fails to turn on (a wrong internal source choice; a mismatched cached position) and confirms the remaining explanation: this is a genuine, moment-to-moment inconsistency in how Windows/your GPU driver handles this monitor -- not something Rig Toggle's own logic is choosing wrong.

**A separate, new finding:** at a different moment in the same log (re-enabling the Odyssey G5 with only one other monitor active), I found a real bug in how Rig Toggle picks which internal GPU "source" slot to use -- it didn't try to reuse the same slot this monitor had used moments earlier, even though that slot was available, and ended up pairing a leftover position value with a different slot than it was captured under. I can't yet prove this specific bug is THE cause of your monitor's on/off flakiness (the very first failure in this same log had no such mismatch at all), but it's real and worth fixing regardless. It also lines up with a related monitor (the Dell) repeatedly, harmlessly flickering on and off on its own during that same sequence before the app's existing safety net correctly shut it back off both times.

**A third finding:** the "monitor briefly vanished entirely" moment is now fully explained -- your Odyssey G5's connection reported itself as transiently unreachable at that exact instant, which the code handles correctly (it just leaves that monitor out of the tile list for that one read; no crash, no bug). This lines up with several other moments in the same log where that same monitor's connection briefly reported the same thing, consistent with (though not conclusive proof of) an intermittent physical-link or negotiation quirk specific to this monitor's connection to your GPU. Separately, I double-checked a detail I'd been told was in this log (a second app process starting up partway through) and could not actually find it anywhere in what you sent -- flagging that in case it matters, but it didn't affect anything above.

**Per this investigation's own long-standing rule against rushing a fix for a bug this stubborn the moment a plausible cause appears, I have NOT written or applied any code changes this round.** Two candidate fixes are identified but need your input before implementation.

### Checkpoint Details

**Decision needed:** how do you want to proceed?

**Option A -- Implement both candidate fixes now, self-verify them (build + tests), and wait for the next occurrence to confirm.**
1. A bounded automatic retry when Rig Toggle's internal apply call reports success but the monitor still doesn't actually come on -- this round proved an immediate retry can succeed with nothing else changed.
2. A fix so Rig Toggle prefers reusing a monitor's own last-used GPU source slot instead of grabbing whichever is first available -- closes a real, if not yet proven-causal, bug.

**Option B -- Hold off on both fixes and gather one more specific data point first.** The next time this happens, send the log again -- specifically useful is whether a future failing/succeeding pair shows the SAME internal settings (confirming this round's finding) or DIFFERENT ones (which would mean there's more for the code to explain after all).

**Option C -- Accept where things stand and close this investigation for now,** treating root_cause (8) as confirmed-external, code-unfixable flakiness specific to this monitor's connection, with Rig Toggle's existing safety nets (all working correctly, reconfirmed again this round) as the accepted mitigation -- same tradeoff offered at round 10, now with meaningfully stronger evidence behind the "this isn't Rig Toggle's own logic choosing wrong" conclusion.

**Self-verified checks:** not applicable this round -- no code changed. All prior fixes (rounds 8, 9, and round 11's instrumentation) remain unchanged and unaffected.

**Tell me:** "A", "B", or "C" -- or point out anything that looks wrong. I verified every claim above directly against the raw log you sent rather than trusting my own framing of it, including catching that one detail I'd been told to expect (a second app process starting up) wasn't actually present in what you sent.

## CHECKPOINT REACHED (round 14 -- ANSWERED: rig repro sent, Fix A fired but did not recover; superseded by round 15's checkpoint at the end of this file)

**Status:** User sent a fresh rig debug.log excerpt capturing Fix A's automatic retry firing for the first time in the real world. It did not recover the specific occurrence, and the retry hit a state (0 candidate GPU sources) that looked worse than the original attempt's. This triggered round 15 -- see Evidence (round 15), Resolution (round 15 addendum), and the new checkpoint at the end of this file. Original ask preserved below for historical record only -- do not act on it.

**Type:** human-verify
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 22 evidence entries, 1 eliminated hypothesis, across seven investigation rounds since the regression reopened (round 8 through round 14).

### Investigation State

**You chose Option A from round 13: implement both candidate fixes now.** Both are done and self-verified:

1. **Fix A -- bounded automatic retry.** When Rig Toggle's internal apply call reports success but your requested monitor still doesn't actually come on (the exact shape round 13 proved recoverable -- an identical attempt failed once, then succeeded 7 seconds later with nothing else changed), Rig Toggle now automatically re-tries the whole enable sequence up to 2 extra times before giving up and showing you the "Monitor enable did not take effect" error. This does NOT touch or interfere with the existing fix that recovers an unrelated monitor accidentally getting dropped -- that is a completely separate, unchanged mechanism handling a different situation. Every automatic retry is clearly logged and distinguishable from you manually clicking the tile again.

2. **Fix B -- prefer reusing a monitor's own last GPU slot.** When re-enabling a monitor, Rig Toggle now checks first whether that exact monitor's own previously-used internal GPU "source" slot is still free, and reuses it if so, instead of just grabbing whichever slot happens to be first available. This closes the real (if not yet proven to be the actual cause of your flakiness) bug round 13 found: a leftover remembered position getting paired with a different GPU slot than it was originally captured under. When no such reuse is possible (no memory of it yet, or that slot is already taken), Rig Toggle falls back to the exact same behavior as before -- nothing changes for that case.

**Self-verified checks:**
- Full solution build: 0 errors (6 pre-existing, unrelated warnings) -- unchanged.
- Full RigToggle.Tests suite: 238/238 passed, unchanged.
- RigToggle.Windows.Tests: builds cleanly, 0 warnings/errors, with 10 new automated tests covering every decision branch of both fixes (cannot execute these in this sandbox -- same pre-existing limitation as every prior round of both debug sessions -- so I hand-traced each one against the code, and additionally double-checked a key equality assumption fix B relies on directly against the real Windows display library file to make sure it behaves the way I assumed).
- Direct before/after code comparison confirms both fixes are "no-op" for the ordinary, already-working case: if nothing goes wrong, Rig Toggle behaves exactly as it did before this round, with no extra delay or behavior change.

**Important -- what this round does NOT claim:** These are targeted fixes for the two SPECIFIC failure patterns your last log excerpt showed, not a claim that the underlying, never-fully-explained Windows/driver flakiness itself (root_cause 8) has been eliminated. It's entirely possible this exact monitor pairing still occasionally misbehaves in a way neither fix catches -- if so, that would be genuinely new information, not a sign these fixes are broken.

### Checkpoint Details

**Need verification:** use Rig Toggle normally, and the next time you see ANY version of the old "monitor position is wrong" or "monitor won't come on" problem -- whether it resolves itself, needs a second click, or shows the error dialog -- please send me the debug.log excerpt covering that moment.

**Specifically useful new lines to look for in that excerpt (from this round's two fixes):**
1. `ActivateMonitors: round 14 (fix A) -- INTERNAL automatic retry N/M: ...` -- tells us fix A's automatic retry fired. If you then see a clean "EXIT success" shortly after, fix A recovered it for you automatically (no error dialog, no need to click again). If it appears 2 times and is followed by the D-05 error anyway, the retry budget was exhausted -- still useful to know.
2. `TryBuildScopedActivationPlan: round 14 (fix B) -- source-preference check for ...: matched preferred (previously-cached) source ...` -- tells us fix B successfully reused the monitor's own prior GPU slot. If instead you see "...falling back to greedy first-unclaimed selection" or "no prior cache entry", that's the normal fallback case, not a problem.
3. If you no longer see a "Monitor enable did not take effect" dialog at all for a while, or you see one but it recovers with NO extra click needed, that's a good sign fix A is helping.
4. If the position/numbering issue happens again despite both fixes clearly firing (per the log lines above), that would mean the underlying driver-level nondeterminism is simply too severe for a bounded retry to reliably catch this time -- genuinely new information, not evidence the fixes are wrong.

**How to check:** just use the rig normally over the next several sessions -- this cannot be forced on demand, since the underlying trigger has always been intermittent across both debug sessions (14 rounds total now).

**Tell me:** send the debug.log excerpt whenever you see any version of the old symptom (fixed automatically, fixed after a second click, or still showing the error) so I can confirm whether these two fixes are actually helping in the real world, not just behaving correctly on their own. If you'd rather stop here and accept the current state as-is, that's also fine to say.

## CHECKPOINT REACHED (round 15 -- ANSWERED: Option C, superseded by round 16's checkpoint at the end of this file)

**Status:** User chose Option C (hold off on any retry-timing change, gather one more data point first). No code change applied this round -- candidate Fix K (from either Option A or Option B) remains proposed, not implemented. See Resolution (round 16 addendum) and the new checkpoint at the end of this file. Original ask preserved below for historical record only -- do not act on it.

**Type:** decision (with a human-verify component)
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 27 evidence entries, 1 eliminated hypothesis, across eight investigation rounds since the regression reopened (round 8 through round 15).

### Investigation State

**This is the first real-world test of round 14's automatic retry, and it did not recover.** You sent a fresh debug.log excerpt where the Odyssey G5 (SAM7489) failed to come on, Fix A's automatic retry fired (confirmed byte-for-byte against the code), and the retry itself hit a state that was arguably WORSE than the original failed attempt, before the error dialog appeared again.

**What I verified directly against your log (not just the framing you gave me):**

1. **The original attempt (12:56:52.684-54.041) matches what you described, plus one detail you didn't mention:** the monitor briefly DID register as active in the very first status check right after Windows accepted the change (52.979), then dropped back out over the next ~740ms, with two "target temporarily unreachable" errors in between. So this wasn't simply "never turned on" -- it turned on, then seems to have dropped back off almost immediately.

2. **The automatic retry (54.042-54.839) also matches what you described:** it found ZERO usable internal GPU slots for this monitor (versus 5 the first time, ~1.4 seconds earlier), which forced it into the older, less reliable fallback method, which also failed. I confirmed this "zero slots" reading is a genuine, live reading of your hardware's actual state at that instant -- not a bug in how Rig Toggle checks for it. And I found something you didn't ask about: because that fallback method is deliberately excluded from further automatic retries, this one failed retry actually ended the WHOLE automatic-retry attempt early -- it never got to use its second, still-available retry slot at all.

**The retry-timing question -- assessed directly, not guessed at:**

I confirmed Rig Toggle's automatic retry currently fires back-to-back with ZERO built-in delay -- the ~1.4 seconds it took was entirely the normal settling/checking process, not a deliberate pause. I also found real evidence (not just a hunch) that this timing might matter: your monitor was already showing "temporarily unreachable" errors barely half a second before the retry checked it again, and it still hadn't cleared. That's consistent with the retry checking too soon, while whatever's briefly confusing Windows about this monitor was still ongoing -- rather than the retry itself being a bad idea.

**However, I do NOT have enough evidence to confidently pick a specific fix here.** I only have one example of this happening, and one (different) earlier example of a manual retry succeeding after 7 seconds -- not enough to know how long the confusion typically lasts, or whether a short pause would reliably help versus just adding a delay that doesn't fix anything. Per this whole investigation's own rule (which you set and I've followed at every prior decision point) against guessing at fix timing without solid justification, I'm bringing this back to you rather than picking a number myself.

**The "is this really external, or is it something we're doing" question -- reassessed honestly:**

- The original position-reset bug (fixed round 8) and the age of the "monitor didn't turn on" safety check (predates this whole investigation by a month, re-confirmed directly against today's code) are both unaffected by anything in this log -- I'm not walking those back.
- The monitor's own flakiness (temporarily reporting unreachable) is confirmed, again, to have started on its own during the FIRST attempt, before the automatic retry ever ran -- so Rig Toggle's retry mechanism did not cause that flakiness.
- BUT: the retry mechanism's TIMING (checking back-to-back with no pause) is a choice we made, and this round's evidence suggests that specific choice may be making the retry less likely to succeed than it could be -- checking again too soon can catch the monitor mid-confusion instead of after it's cleared. So: your skepticism is partly right, narrowly -- not that the underlying monitor flakiness is our doing, but that HOW we retry (immediately, no pause) is a design choice of ours that this evidence suggests could be improved. I want to be clear I'm not overstating this either way: one data point, and it's about the retry's effectiveness, not about the root cause.

### Checkpoint Details

**Decision needed:** how do you want to proceed on the retry-timing question?

**Option A -- Add a short pause before each automatic retry attempt** (e.g., a fixed delay of somewhere around half a second to a couple seconds, exact number still to be decided) to give the monitor's brief confusion more time to clear before checking again. Simplest change, lowest risk, but the right delay length is a guess without more data.

**Option B -- Instead of a blind pause, have the retry actively wait (with its own short timeout) for the monitor to report itself reachable again before trying.** More targeted at the actual problem, reuses a polling approach already used elsewhere in the app, but is a slightly bigger code change for something only observed once so far.

**Option C -- Hold off on any timing change and gather one more data point first** (matches this investigation's own repeated pattern at earlier decision points) -- wait for the next occurrence and see whether it's the same shape (supporting a timing fix) or different (meaning something else is going on).

**Option D -- Accept this specific failure as an acceptable edge case of an already-flagged, still-unexplained monitor quirk**, since Rig Toggle's other safety nets (the clear error message, not silently corrupting anything) all worked correctly here, and move on without further changes to the retry.

**Self-verified checks:** not applicable this round -- no code changed. All prior fixes (rounds 8, 9, 11, and round 14's Fix A/Fix B) remain unchanged and unaffected; Fix A and Fix B are both independently confirmed, again, to be firing exactly as designed in this trial (Fix A's trigger logic; Fix B's "no prior cache entry" fallback; and, newly, Fix J from round 9's conditional intent-guard arming, captured for the first time in this excerpt).

**Tell me:** "A", "B", "C", or "D" -- or point out anything that looks wrong. As always, I verified every claim above directly against the raw log you sent, including catching a detail (the monitor briefly turning on before dropping back out) that wasn't part of what I was told to expect.

## CHECKPOINT REACHED (round 16 -- ANSWERED: standing evidence ask fulfilled by round 17's fresh log; superseded by round 17's checkpoint at the end of this file)

**Status:** A fresh rig debug.log excerpt arrived, fulfilling round 16's standing ask (a second Fix-A retry occurrence) AND surfacing a separate, new, unrelated defect via the Rig-mode toggle switch. See Evidence (round 17), Resolution (round 17 addendum), and the new checkpoint at the end of this file. Original ask preserved below for historical record only -- do not act on it.

## CHECKPOINT REACHED (round 16 -- Option C recorded; no code change; standing evidence ask re-presented)

**Type:** human-verify (standing ask, not a new decision)
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 27 evidence entries, 1 eliminated hypothesis, across eight investigation rounds since the regression reopened (round 8 through round 15); round 16 is a bookkeeping round only -- no new evidence gathered, no code change made.

### Investigation State

**You chose Option C from round 15's checkpoint: hold off on any retry-timing change, gather one more data point first.** No code change was made this round. Candidate Fix K -- either a short fixed pause, or a more targeted bounded poll for the monitor to report itself reachable again, before each Fix-A automatic retry attempt -- remains exactly as round 15 left it: proposed, not implemented. Round 15's own findings all stand unchanged:
- Fix A's retry-eligibility logic fired exactly as designed in that trial.
- The automatic retry landed on a "0 usable GPU slots" state, structurally worse than the original attempt's "5 usable slots" state, forcing the older, less-reliable fallback method and forfeiting the rest of the retry budget.
- Fix A's zero-delay retry timing is a plausible, evidence-grounded -- but single-trial -- factor in why that specific retry failed.
- The underlying reason SAM7489 (or any monitor) transiently reports unreachable remains genuinely unconfirmed and external/driver-level; nothing about this decision changes that classification either way.

### Checkpoint Details

**Need verification:** none of this round's changes need your verification -- there are no changes. What's needed is the SAME standing evidence ask from round 15, still open, now restated precisely.

**What to look for in the next debug.log excerpt, whenever SAM7489 (or any other monitor) next fails to activate:**

1. **If Fix A's automatic retry fires** (look for `ActivateMonitors: round 14 (fix A) -- INTERNAL automatic retry N/M`), check what the log shows for candidate GPU sources on that retry attempt:
   - **SAME pattern as round 15** -- `0 candidate PathDisplaySource(s)`, forcing the older whole-topology fallback -- would be a second occurrence of "retrying too soon catches the monitor mid-unavailability," meaningfully strengthening the case for a timing fix (Option A or Option B from round 15, still both open).
   - **DIFFERENT pattern** -- candidates ARE available on the retry, but the target still doesn't end up active -- would mean timing is NOT the discriminating factor here, and a different explanation is needed before any timing fix would be justified.
2. **If you ever see a successful automatic retry** (Fix A fires, and the monitor comes on cleanly, no error dialog, no need to click again) -- please send that excerpt too, whenever it happens. This is equally informative: no such case has been captured yet across all 16 rounds, and seeing what a SUCCESSFUL retry's candidate-source state looks like would confirm or refute the timing theory just as directly as another failure would.

**How to check:** just use the rig normally -- this cannot be forced on demand, since the underlying trigger has always been intermittent across every round so far.

**Tell me:** send the debug.log excerpt whenever SAM7489 (or any monitor) next fails to activate, or if you ever see Fix A's automatic retry succeed cleanly. Once one of those arrives, I'll compare it directly against round 15's "0 candidates on immediate retry" pattern and come back with a recommendation on Option A vs. Option B (or confirm neither is warranted yet) before writing any code. If you'd rather stop here and accept the current state as-is (round 15's Option D), that's still fine to say at any point.

## CHECKPOINT REACHED (round 17 -- TWO items: a new toggle-switch stale-device-path defect, NOT fixed; and a strengthened, but still not acted on, Fix-A retry-timing data point)

**Type:** decision (two independent items, each with its own options)
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 35 evidence entries, 1 eliminated hypothesis, across nine investigation rounds since the regression reopened (round 8 through round 17).

### Investigation State

**Your skepticism about "click twice to fix it" was worth taking seriously, and this round's log actually surfaced something more interesting: a genuinely new, SEPARATE, fully deterministic bug -- not evidence against the external-driver explanation for the SAM7489 pattern (that one held up), but a real, different problem hiding in a part of the app this whole investigation had never actually looked at: the Rig-mode toggle switch.**

---

### Item 1 -- NEW: the Rig-mode toggle switch fails every time, right now, because of two "ghost" monitors left over in your settings

**What happened:** The very first action in your log (clicking the Rig-mode toggle switch, not a tile) failed instantly -- 2 milliseconds after it started -- with "Cannot enable monitor(s) -- not detected: SAM748A...". It failed again, identically, 3 seconds later on a retry.

**Why:** `SAM748A` and `DELA0B8` are real device-path identities for two of your monitors from an EARLIER point in this rig's history (I found them referenced in two older, already-resolved debug investigations on this exact machine) -- they got superseded at some point by the current `SAM7489`/`DELA0BC` identities (this happens when Windows re-assigns a monitor's internal identity, e.g. after a cable replug or driver update), but the OLD identities never got removed from your saved settings. I traced this to a deliberate design in the Settings screen: it preserves any previously-configured monitor forever, specifically so a temporarily-unplugged monitor doesn't lose its configuration -- but it has no way to tell "temporarily unplugged" apart from "permanently replaced," and there's no button anywhere to manually forget a stale entry (it isn't even shown as a row in the grid to uncheck).

**The impact:** The Rig-mode toggle switch reads your saved monitor list directly and hands it straight to Windows with no live check first -- so the instant it hits one of these two ghost entries, the WHOLE toggle fails, including the legitimate part (turning on your real Odyssey G5, turning off your real other two monitors). This will keep happening every single time you use the toggle SWITCH until it's fixed -- it is not intermittent or driver-related, it is 100% deterministic. (Your dashboard TILE clicks are unaffected -- they never read this saved list, which is exactly why nothing in the last 9 rounds of tile-click investigation ever caught this.)

**Bonus, smaller finding in the same area:** the toggle switch also has its own version of a bug I fixed for the tiles back in round 9 (baking in a failed attempt's accidental state as if it were "intended") -- confirmed firing in your log, worth fixing at the same time.

**Does this explain your ORIGINAL "Windows says display 3, app says display 2" complaint from the start of this whole investigation?** Honestly: I can't prove it retroactively (I have no record of what your settings looked like or which control you used back then), and the last 9 rounds never circled back to test the toggle switch specifically to rule it out either way. But the mechanism fits well: a toggle that silently does nothing (because it hit a ghost monitor) while the app still marks the unchanged state as "correct" would produce exactly that kind of disagreement, without needing any of the other 8 fixes already applied.

**I have NOT fixed this yet** -- it's a different part of the app than everything else this investigation has touched, and per how we've always handled a freshly-found bug here, I want your go-ahead before changing it.

**Options:**
- **1A -- Fix it now:** make the toggle switch check monitors against what's actually live before sending them to Windows, so a ghost entry produces one clear, specific error (naming just the stale monitor) instead of silently blocking the whole toggle -- plus fix the same "bakes in a failed attempt" bug the tiles already got fixed for.
- **1B -- Just tell me how to clean up my settings.json by hand for now,** and hold off on a code fix until you've seen whether that alone resolves it.
- **1C -- Hold off entirely** and gather more information first (e.g., you confirm whether you've been using the toggle switch or the tiles day-to-day, which would help pin down whether this really explains the original complaint).

---

### Item 3 -- REFINED: the SAM7489 "click twice" pattern -- checked directly, still looks external, but the timing evidence for a fix got stronger

**Checked directly (not assumed):** is there anything about HOW the automatic retry runs, versus a manual re-click, that could explain the difference? No -- I compared the actual code: both run on the exact same thread, call the exact same method, with no Windows-COM-related quirk anywhere in this code path (that's a legitimate thing to check, and I did -- it's not involved here). The ONLY difference is how much time passes before checking again.

**New math from this round:** the automatic retry fired at almost exactly the same ~1.36-second mark as last time, and landed on the SAME "zero available slots" bad state as last time (now 2-for-2). Your manual re-click this time succeeded after roughly 2.4-4.6 seconds of extra delay; combined with the earlier ~6.6-second success, that's now two-for-two successes at longer delays too. This doesn't prove a specific fix number yet, but it's a real strengthening of the pattern versus last time's single data point.

**Options (unchanged from round 15/16, now with somewhat better-supported footing):**
- **3A -- Add a short fixed pause before each automatic retry.**
- **3B -- Have the retry actively wait for the monitor to report itself reachable again, instead of a blind pause.**
- **3C -- Gather one more data point before deciding** (same as your round-16 choice).
- **3D -- Accept this as an edge case and stop tracking it separately.**

---

**Self-verified checks:** not applicable this round -- no code changed for either item; both are characterized and presented for your decision, not fixed.

**Tell me:** your choice for item 1 (1A/1B/1C) and, separately, your choice for item 3 (3A/3B/3C/3D) -- they're independent decisions and don't need to match.

## Resolution (round 18 addendum -- BOTH items IMPLEMENTED and self-verified; checkpoint response to round 17's Option 1A (item 1) + Option 3B (item 3))

round_18_reasoning_checkpoint_item_1:
  hypothesis: >
    ToggleService.ToggleToRigMode()/ToggleToNormalMode() pass settings.json's raw
    MonitorsToDisable/MonitorsToEnable (or the NormalMonitorsToDisable/NormalMonitorsToEnable
    equivalents) straight into ActivateMonitors/DeactivateMonitors with no live-detection
    check of any kind, so a single stale device path (a superseded CCD identity never
    pruned by SettingsForm's own deliberate union-merge preserve-forever design) causes
    WindowsMonitorController's own early-availability guard to throw immediately and
    deterministically -- and because the Monitor step is a single atomic unit
    (stop-on-first-failure in Rig mode; a single try/catch in Normal mode), this blocks the
    ENTIRE toggle, including every still-live monitor in the same request, every single
    time.
  confirming_evidence:
    - "Round 17 evidence (2026-08-30T03:00:00Z): ActivateMonitors ENTER/EXIT log shows the throw firing 2ms after entry with 'not detected: [SAM748A...]', and direct code read confirms ToggleSwitch_ActionRequested -> ToggleService.ToggleToRigMode is the ONLY call site producing this multi-path, isPartOfMonitorSwap=True shape -- OnTileAction never does."
    - "Round 17 evidence (2026-08-30T03:05:00Z): SettingsForm.BtnSaveSettings_Click's union-merge design confirmed via direct code read (lines 1218-1234) -- stale device paths are preserved forever on every save, with no in-app removal control (GetStaleSavedDevicePaths/ShowStaleMonitorWarning only ever produce a non-blocking advisory label, never consulted by ToggleService)."
    - "Direct code read this round (round 18), per this round's explicit task instruction to check rather than assume: ToggleToNormalMode's Monitor step (pre-fix lines 373-387) has the IDENTICAL unfiltered-pass-through shape against NormalMonitorsToDisable/NormalMonitorsToEnable -- confirmed both toggle directions were equally exposed, not just Rig mode."
  falsification_test: >
    If, after the live-filter fix, a stale device path STILL produces the opaque "not
    detected" exception (instead of being silently filtered and reported via
    ToggleResult.StaleMonitorsSkipped), the hypothesis about WHERE the unfiltered
    pass-through happens was wrong -- e.g. some OTHER code path also reads the raw settings
    and calls ActivateMonitors/DeactivateMonitors directly, bypassing LiveFilterMonitorSets
    entirely.
  fix_rationale: >
    The fix live-filters BEFORE the existing calls, reusing the SAME IMonitorController.
    GetAllMonitors() live-enumeration oracle already established elsewhere in this codebase
    (SettingsForm's own staleness check; WindowsMonitorController's own IsAvailable-based
    guard) rather than inventing a new detection mechanism. This addresses the root cause
    (an unfiltered pass-through of persisted, potentially-stale data to a live-CCD-validating
    API) directly -- it prevents the stale path from ever reaching
    ActivateMonitors/DeactivateMonitors at all, rather than just catching/rewording the
    resulting exception after the fact -- while leaving the still-live monitors' own request
    unchanged.
  blind_spots:
    - "Cannot execute or verify on real Windows CCD hardware in this sandbox -- same structural limitation as every round of both debug sessions. Self-verified via build + full adjacent test suite + direct code read/hand-trace, plus 5 new unit tests exercising LiveFilterMonitorSets' filtering/all-stale-no-op/GetAllMonitors-throws-degrade behavior through ToggleService's public API."
    - "Does not address WHY SettingsForm never prunes/removes a stale entry (candidates (b)/(c) from round 17's fix_item_1 -- a staleness-reconciliation step, or a manual 'forget' UI control) -- the user's Option 1A choice was specifically the live-filter-and-proceed approach (a), not a Settings-screen redesign; (b)/(c) remain unaddressed, unchosen alternatives, not silently dropped."
    - "The chosen semantics (proceed with the live monitors, surface a non-blocking informational note) is a reasoned design decision documented here (see fix_item_1 below), not something the user was asked to pick between at this granular level -- if future feedback indicates the alternative (fail the whole toggle with a clearer message) is actually preferred, this is a small, isolated change to revert to."
    - "MainForm.PerformBackgroundToggle (the tray/hotkey toggle handler) automatically benefits from the SAME ToggleService-level live-filter fix, since it also calls ToggleToRigMode/ToggleToNormalMode through the shared ToggleOrchestrator -- but does NOT get the new StaleMonitorsSkipped surfacing message this round; it remains silent about WHICH paths were skipped (though no longer blocked by them). Deliberately out of this round's explicitly-authorized scope (the checkpoint's item-1a surfacing change was scoped to ToggleSwitch_ActionRequested only) -- flagged, not fixed, for symmetry in a future round if desired."
    - "Discovered, but explicitly NOT fixed this round, matching this investigation's own established discipline of not silently expanding an authorized decision's scope beyond what was asked (rounds 10/11/13/15 precedent): MainForm.PerformBackgroundToggle has the IDENTICAL unconditional ArmIntentGuard() call (line ~2271, `RefreshMonitorTiles(); ArmIntentGuard();` with no result.Success gate) that round 9's Fix J and this round's item 1b both address for OnTileAction/ToggleSwitch_ActionRequested respectively. This is a new, real, evidence-based finding -- flagged here as an open item for a future round/decision, not yet approved for a fix."
  candidate_causes:
    - "code: ToggleService.ToggleToRigMode/ToggleToNormalMode's unfiltered pass-through of persisted settings to a live-validating API (the fixed defect)."
    - "data/config: settings.json accumulating stale device-path entries over the rig's hardware history via SettingsForm's own deliberate union-merge preserve-forever design -- a genuinely different category (persisted DATA becoming stale over time, not a code defect in the toggle path itself). This category's own root fix (pruning/reconciling stale settings entries, or a manual removal UI) was explicitly NOT chosen this round, per the user's Option 1A scope."
  and_gate: >
    No -- a single-category (code) fix is sufficient to resolve the reported symptom (the
    toggle blocking the whole action on a stale path). The data-category condition (why the
    stale entry exists at all) is a separate, longer-lived circumstance the code fix
    correctly tolerates and reports rather than needing to also eliminate simultaneously.
    Both categories are documented per the branching requirement; only the code-category fix
    was in scope for this round's decision.

round_18_reasoning_checkpoint_item_3:
  hypothesis: >
    Fix A's automatic retry (round 14) fires with ZERO built-in delay the instant the scoped
    build+apply+settle+correct sequence reports the requested target still inactive despite
    scoped ApplyPathInfos reporting success. Round 15/17 rig evidence shows this zero-delay
    retry landing on the SAME "0 candidate PathDisplaySource(s)" degraded shape both times it
    fired for real, at a consistent ~1.36s mark, while every observed successful MANUAL
    re-click recovery waited substantially longer (~2.4s-6.6s) -- consistent with the target
    genuinely still being mid-unavailability at the exact moment the zero-delay retry
    re-queries it, not with any code-path difference (already directly ruled out this session
    via a thread/COM/apartment comparison, round 17).
  confirming_evidence:
    - "Round 15 evidence: automatic retry fired ~1.358s after the original request, landed on '0 candidates'."
    - "Round 17 evidence (2026-08-30T03:35:00Z): automatic retry fired at 1.362s (near-identical to round 15), again '0 candidates' (now 2-for-2); manual recoveries succeeded at ~2.363s-4.558s this round and (round 13) ~6.579s previously -- 2-for-2 successes at substantially longer delays."
    - "Round 17 evidence (2026-08-30T03:30:00Z): direct thread/COM/apartment comparison ruled out any code-path distinction between a manual re-click and the internal automatic retry -- both run on the same single STA UI thread, calling the identical method; the only difference is elapsed wall-clock time."
  falsification_test: >
    If, after this fix, the poll consistently reports the target(s) reachable well within the
    bounded window but the SUBSEQUENT retry attempt still lands on "0 candidate
    PathDisplaySource(s)" (i.e. GetAllPaths()-level availability recovers before
    TryBuildScopedActivationPlan's own narrower unclaimed-candidate-source pool does), this
    hypothesis is refuted at the mechanism level -- the defect would then be in candidate-source
    availability specifically, not target-path availability, and a future round would need to
    poll for candidate-source count instead of mere reachability.
  fix_rationale: >
    Inserts an ACTIVE, bounded wait for the SPECIFIC condition (the requested target(s)
    reporting live-detected again) between the retry-eligibility decision and the retry
    attempt itself -- addressing the directly-evidenced timing gap (zero delay between two
    attempts that need the OS/driver to finish an in-flight renegotiation) rather than a
    blind fixed-delay guess (Option 3A, not chosen) or accepting the status quo (3D, not
    chosen). Confirmed via direct before/after comparison that ShouldRetryScopedActivation's
    own eligibility logic, fix H's correction-loop logic, fix B's source-preference logic, and
    the fixed retry-count budget (MaxScopedActivationRetryAttempts, still 2) are all
    byte-for-byte unchanged -- only WHEN a retry attempt fires has changed.
  blind_spots:
    - "Cannot execute or verify on real Windows CCD hardware in this sandbox. PollUntilTargetsReachable itself is NOT unit-testable in isolation (calls live PathInfo.GetAllPaths()) -- checked directly against the actual code shape, not assumed: it is in the same class of untestable-without-live-CCD-hardware code as ActivateMonitors/DeactivateMonitors themselves and the pre-existing PollUntilStableActiveDevicePaths, neither of which has ever had a direct unit test either. Its one dependency that IS a pure, already-tested seam (ComputeUndetectedDevicePaths) is deliberately reused rather than reimplemented, and remains covered by its own existing 4 tests, unchanged."
    - "The chosen bound (MaxReachabilityPollAttempts=20 x SettlePollDelay(150ms) = up to ~2.85s added wait) is an evidence-INFORMED but not evidence-PROVEN choice -- it sits within, but does not necessarily cover, the low end of the observed successful-manual-recovery window (~2.4s-6.6s). Whether 2.85s is enough to reliably help, or whether a future round needs to widen it further (at the cost of a longer synchronous block, since this whole call executes on the single STA UI thread per round 17's own confirmed finding), is an open, rig-verifiable question -- explicitly NOT claimed solved this round."
    - "Does NOT claim root_cause (8) (the underlying OS/driver-level nondeterminism making SAM7489 transiently unavailable in the first place) is fixed or even explained -- this is a targeted improvement to WHEN the existing retry fires, not a fix for WHY the target becomes transiently unavailable at all. Explicitly called out per this round's own task instruction to be precise about this distinction."
  candidate_causes:
    - "code: Fix A's retry loop had no delay/gating mechanism at all between attempts (the fixed defect)."
    - "environment: the underlying OS/driver-level CCD renegotiation latency for the SAM7489 target itself (root_cause (8), still unconfirmed/external) -- a genuinely different category from the code-level retry-timing gap; this round does not fix or claim to explain this category, it only makes the code-level retry tolerant of it."
  and_gate: >
    No -- the two categories are independent and do not need to combine to explain the
    retry-timing improvement's expected effect: fixing the code-level timing gap (adding a
    bounded wait) is sufficient to test whether it helps, regardless of what the
    environment-level root cause of the transient unavailability turns out to be. Both
    categories are documented per the branching requirement; only the code-category change
    was made this round, by design (matching the user's Option 3B choice, which is explicitly
    a timing/gating change, not a claimed environment-level fix).

root_cause_status_round_18: >
  root_cause (8) (the underlying SAM7489 OS/driver-level nondeterminism) remains OPEN in the
  strict "what is the mechanism" sense -- item 3's fix does not claim to have identified or
  eliminated it, only to make the existing automatic retry more tolerant of it (Option 3B).
  Item 1's defect (the toggle-switch's stale-device-path opaque-failure) IS a genuine, distinct,
  fully-deterministic code-level defect in a subsystem no prior round of this session had
  examined, first identified round 17 and fixed this round -- it is NOT part of root_cause (8)
  and was never claimed to be; per round 17's own honest framing, it MAY be part of the
  explanation for the ORIGINAL Symptom 1 numbering-swap complaint, but this cannot be proven
  retroactively and is not re-litigated here.
root_cause_addendum_item_1: >
  Unchanged from round 17's characterization (see that round's root_cause_addendum_item_1) --
  confirmed again this round via direct code read of ToggleToNormalMode alongside
  ToggleToRigMode (both equally affected, per the objective's own explicit instruction to
  check rather than assume). No new root-cause information beyond round 17's; this round is
  the fix for the already-confirmed defect.
fix_item_1: >
  (a) Live-filter, src/RigToggle.Core/ToggleService.cs: new private LiveFilterMonitorSets(
  disableSet, enableSet) helper, called at the top of both ToggleToRigMode and
  ToggleToNormalMode immediately after the raw settings-derived sets are built and BEFORE
  either is ever passed to ActivateMonitors/DeactivateMonitors. Queries
  _monitorController.GetAllMonitors() for the live-detected device-path set (the SAME oracle
  SettingsForm's own staleness check and WindowsMonitorController's own IsAvailable-based
  guard already use); any requested path NOT in that live set is removed from the set handed
  to Activate/DeactivateMonitors and instead surfaced via the new
  ToggleResult.StaleMonitorsSkipped property (an additive, init-only property with an empty
  default -- every existing `new ToggleResult(steps)` call site remains byte-for-byte valid).
  Design decision (documented per the round-17 checkpoint's explicit instruction to reason
  through and record this choice): PROCEED with whatever remains live rather than fail the
  whole action, matching SettingsForm's own already-established, non-blocking "settings
  preserved; reconnect the display to manage it here" philosophy for a stale entry (it never
  blocks Save over a stale monitor either) -- this keeps the legitimate, still-live monitors
  working, which is what matters day-to-day, while still surfacing ONE clear, specific message
  (ToggleResultFormatter.FormatStaleMonitorNote, deliberately echoing SettingsForm's own
  staleness wording) naming exactly which path(s) were skipped, shown via a dedicated
  informational MessageBox in MainForm.ToggleSwitch_ActionRequested (separate from, and
  regardless of, the existing CORE-04 partial-failure checklist dialog, since a stale-skip is
  not itself a step failure). If GetAllMonitors() itself throws (an enumeration hiccup),
  LiveFilterMonitorSets degrades to "treat every requested path as live" -- i.e. skips
  filtering entirely and falls through to the exact pre-round-18 unfiltered behavior for that
  call -- rather than let a transient enumeration failure block or partially break the toggle
  on its own. Degenerate edge case (every configured monitor is stale): Activate/
  DeactivateMonitors both already no-op safely on an empty request set in the real
  WindowsMonitorController implementation, so no special-case handling was added -- the
  Monitor step simply reports Succeeded as a no-op, with every requested path surfaced as
  stale.
  (b) Toggle-switch intent-guard fix, src/RigToggle.App/MainForm.cs,
  ToggleSwitch_ActionRequested only: mirrors round 9's Fix J EXACTLY -- the previously
  unconditional `RefreshMonitorTiles(); ArmIntentGuard();` is now `RefreshMonitorTiles(); if
  (result is not null && result.Success) { ArmIntentGuard(); } else { <trace log skip> }`.
  RefreshMonitorTiles() still always runs unconditionally (so the tile dashboard itself stays
  accurate on both success and failure) -- only the intent-guard re-arm is now gated on
  result.Success, the same predicate OnTileAction's activateSucceeded/deactivateSucceeded
  flags encode for its own two branches. On failure, any previously-armed (potentially still
  valid) intent snapshot is left untouched rather than overwritten with the failed attempt's
  accidental result, exactly matching Fix J's own documented rationale.
verification_item_1:
  target_test: { result: pass, note: "5 new unit tests added to src/RigToggle.Tests/ToggleServiceTests.cs, all passing, covering: a stale entry mixed with a live one in MonitorsToDisable (Rig mode) is filtered out while the live entry still reaches DeactivateMonitors and is reported in StaleMonitorsSkipped; the no-stale-entries case returns an empty StaleMonitorsSkipped; the identical stale-filtering behavior for ToggleToNormalMode's NormalMonitorsToDisable/NormalMonitorsToEnable sets; the degenerate all-stale case degrading to a Succeeded no-op Monitor step; and GetAllMonitors() throwing degrading to the exact pre-round-18 unfiltered behavior. FakeMonitorController (RigToggle.Tests/Doubles/FakeControllers.cs) was extended with a configurable liveDevicePaths set (defaulting to a superset covering every device-path literal ToggleServiceTests.cs already used, so every pre-existing test keeps passing unmodified) and a throwOnGetAllMonitors flag for the defensive-fallback test." }
  mutation_check: { result: skipped, reason: "No Stryker configured in this repo (matches every prior debug session in this codebase)." }
  no_op_deletion: { result: pass, deletion_justified_by_rca: true, note: "(a) No code deleted -- LiveFilterMonitorSets is a new, additive helper; the existing ActivateMonitors/DeactivateMonitors calls are unchanged, only fed filtered local variables computed immediately beforehand. ToggleResult gained an additive init-only property (default empty), not a change to its primary-constructor shape -- every pre-existing `new ToggleResult(steps)` call site (4 in ToggleService.cs, 4 in ToggleResultFormatterTests.cs) compiles and behaves unchanged. (b) No code deleted -- RefreshMonitorTiles()'s unconditional call is preserved byte-for-byte; ArmIntentGuard() gained a conditional guard plus an else-branch trace line (net addition, not a deletion), identical in shape to round 9's Fix J for OnTileAction." }
  adjacent_tests: { result: pass, suites_run: ["dotnet build RigToggle.sln --no-incremental (0 errors, same 6 pre-existing unrelated xUnit1031 warnings, all 6 projects)", "dotnet test src/RigToggle.Tests (243/243 passed -- 238 pre-existing + 5 new, all pass)"] }
  revert_and_reconfirm: { result: pending, reason: "Requires a rig trial reproducing the toggle-switch stale-device-path shape (a stale entry in settings.json's monitor sets) -- deferred to the human-verify checkpoint below." }
  guardrail_verdict: accepted
  guardrail_note: "All self-executable signals pass: no-op/deletion review confirms no behavior loss on the success/no-stale-entry path (both changes are RCA-justified additions with byte-for-byte-preserved default behavior); full-solution build is clean; the existing RigToggle.Tests suite passes in full (243/243) including 5 new tests directly exercising the new filtering/edge-case/defensive-fallback behavior. Hardware-dependent behavior (does the toggle switch actually recover and report cleanly against a real stale device path on the rig) is deferred to the mandatory human-verify checkpoint."
files_changed_item_1:
  - src/RigToggle.Core/Models/ToggleResult.cs
  - src/RigToggle.Core/ToggleService.cs
  - src/RigToggle.Core/ToggleResultFormatter.cs
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.Tests/Doubles/FakeControllers.cs
  - src/RigToggle.Tests/ToggleServiceTests.cs
root_cause_addendum_item_3: >
  No new root cause -- root_cause (8)'s underlying OS/driver mechanism remains exactly as
  unconfirmed as round 17 left it. This round implements the round-17-proposed, round-18-user-
  chosen Option 3B (active poll-until-reachable) as a targeted timing/gating improvement to
  Fix A's existing retry, not a claim about the external mechanism itself.
fix_item_3: >
  src/RigToggle.Windows/WindowsMonitorController.cs, ActivateMonitors only: a new private
  static PollUntilTargetsReachable(targetDevicePaths) helper is called between
  ShouldRetryScopedActivation's (unchanged) eligibility decision and the loop's `continue`
  into the retry attempt. Mirrors PollUntilStableActiveDevicePaths' own per-tick try/catch +
  sleep-before-each-subsequent-attempt shape exactly, reusing the SAME SettlePollDelay
  (150ms) tick interval rather than inventing a new cadence, and reuses
  ComputeUndetectedDevicePaths (the same already-unit-tested pure "is this device path
  live-detected at all" predicate DeactivateMonitors' own missing-target guard already uses)
  as the per-tick reachability check, rather than a bespoke one. Bounded by the new
  MaxReachabilityPollAttempts=20 constant (up to ~2.85s added wait: 19 sleeps of 150ms after
  an immediate first check) -- never unbounded -- and unconditionally proceeds to the retry
  attempt regardless of whether the bound was reached with or without the target(s)
  reporting reachable, letting the existing D-05 verify-and-throw machinery handle a
  genuinely persistent failure exactly as it does today. Logged distinctly (a new "round 18
  (fix K / poll-until-reachable)" line series) recording: whether the poll fired, how long it
  actually waited, whether the target(s) became reachable within the bound or the bound
  expired first, and an explicit pointer to the immediately-following
  TryBuildScopedActivationPlan log line(s) (unchanged, pre-existing) for the retry attempt's
  own candidate-source count -- so a future debug.log excerpt can directly compare against
  round 15/17's "0 candidates on immediate retry" data points. Confirmed via direct
  before/after comparison: ShouldRetryScopedActivation's own logic/signature, fix H's
  correction-loop logic, fix B's source-preference logic (SelectSourceForActivation), and
  MaxScopedActivationRetryAttempts (still 2) are all byte-for-byte unchanged -- only WHEN a
  retry attempt fires has changed.
verification_item_3:
  target_test: { result: not_applicable, reason: "PollUntilTargetsReachable calls live PathInfo.GetAllPaths() directly -- checked against the actual code shape (not assumed): it is in the same untestable-without-live-CCD-hardware class as ActivateMonitors/DeactivateMonitors themselves and the pre-existing PollUntilStableActiveDevicePaths, neither of which has a direct unit test either. Its one pure dependency (ComputeUndetectedDevicePaths) is reused, not reimplemented, and remains covered by its own existing 4 tests in WindowsMonitorControllerTests.cs, unchanged. No new test added for PollUntilTargetsReachable itself, matching this file's own established discipline of verifying testability directly against the actual code shape rather than assuming a pure-seam extraction is always possible." }
  mutation_check: { result: skipped, reason: "No Stryker configured in this repo (matches every prior debug session in this codebase)." }
  no_op_deletion: { result: pass, deletion_justified_by_rca: true, note: "No code deleted -- the new poll call and its own constant/method are inserted between the existing (unchanged) 'INTERNAL automatic retry' Log() line and the existing (unchanged) `continue` statement. Direct before/after diff confirms ShouldRetryScopedActivation's call site, MaxScopedActivationRetryAttempts, and the correction-loop/source-preference code elsewhere in the same method are byte-for-byte untouched." }
  adjacent_tests: { result: pass, suites_run: ["dotnet build RigToggle.sln --no-incremental (0 errors, same 6 pre-existing unrelated xUnit1031 warnings, all 6 projects)", "dotnet build src/RigToggle.Windows.Tests (0 warnings, 0 errors, 39 tests total -- unchanged from round 14, since no new pure-seam test was added or needed)", "dotnet test src/RigToggle.Windows.Tests (builds cleanly; execution aborts with the same pre-existing sandbox limitation as every prior round of both debug sessions -- missing Microsoft.WindowsDesktop.App runtime on this Linux sandbox -- unrelated to this change; the existing 39 tests were not touched by this round's edit and remain hand-traceable as passing against the unchanged ShouldRetryScopedActivation/ComputeUndetectedDevicePaths implementations)"] }
  revert_and_reconfirm: { result: pending, reason: "Requires a rig trial reproducing the Fix-A automatic-retry-fires shape (a scoped apply that reports success but the requested target never settles) to observe whether the poll actually fires, how long it waits, and whether the subsequent retry's candidate-source count improves versus round 15/17's '0 candidates' data points -- deferred to the human-verify checkpoint below." }
  guardrail_verdict: accepted
  guardrail_note: "All self-executable signals pass: no-op/deletion review confirms no behavior loss (a pure insertion between two existing, unchanged log/continue statements); full-solution build is clean; RigToggle.Windows.Tests builds cleanly with its existing 39 tests unchanged (no pure-seam extraction was possible or needed for this specific change, verified directly against the code shape rather than assumed -- matching this file's own established pattern of distinguishing testable pure helpers from untestable live-CCD-query code). Hardware-dependent behavior (does the poll actually fire and wait as designed on a real rig; does it measurably change the subsequent retry's candidate-source count) is deferred to the mandatory human-verify checkpoint. Per this round's own explicit instruction, root_cause (8) is NOT claimed fixed -- only the retry's own timing/gating is changed."
files_changed_item_3:
  - src/RigToggle.Windows/WindowsMonitorController.cs

## Resolution (round 19 addendum -- TWO new items characterized from a PRE-round-18 log excerpt (item A: nested-call retry-eligibility exclusion; item B: collateral-failure error-message clarity); NEITHER fixed this round, pending user decision)

root_cause_item_A: >
  SAME root_cause (8) class (the still-unconfirmed, genuinely-open ApplyPathInfos/
  PathChangeException fragility for the SAM748A/SAM7489+ACI24A4/DELA0Bx pairing), newly
  observed at a DIFFERENT call site: when fix H's correction loop (round 7,
  ComputeUnexpectedlyDeactivated) makes its nested ActivateMonitors call to restore an
  accidentally-dropped survivor, that nested call re-enters ActivateMonitors as a genuinely
  independent invocation with its own attemptNumber/retry budget -- fix A/fix K's
  retry-then-poll mechanism is NOT structurally excluded from it. In this trial it simply did
  not fire because the nested call's own single scoped-activation attempt ALSO threw
  PathChangeException and fell back to Extend, and ShouldRetryScopedActivation's own,
  already-existing, already-approved eligibility gate (line 1291,
  `usedScopedActivation && ...`) deliberately excludes any Extend-fallback attempt from retry
  -- an exclusion rounds 13/14/15/17 already established for the TOP-LEVEL call and which
  applies, unmodified, with identical effect, to the nested call. Confirmed via and_gate
  analysis (round_19_reasoning_checkpoint): this specific propagation required BOTH the
  environment/CCD-driver PathChangeException trigger (firing independently on the outer AND
  nested attempts) AND the already-approved retry-eligibility gate to combine -- not a single
  new code-level cause.
root_cause_item_B: >
  A separate, additive code-level gap: the nested ActivateMonitors call at line 744 has no
  try/catch, so its own D-05 InvalidOperationException propagates through the outer call
  completely unenriched -- the exception's type and message carry no marker distinguishing
  "the user's own primary request failed" from "a collateral side-effect restoration,
  triggered by fix H's correction of an unrelated accidental drop, failed." MainForm.cs's
  ENABLE-branch catch (lines 1350-1354) surfaces `ex.Message` verbatim via MessageBox.Show,
  with the in-scope, correctly-succeeded `devicePath` (the user's real target) never
  referenced in the user-facing text.
fix_item_A: >
  NOT APPLIED THIS ROUND. Per this investigation's own standing discipline, a fix is not
  implemented in the same round a new failure shape is characterized unless it is a
  trivially safe, narrowly-scoped extension of already-decided-and-approved logic. Extending
  fix A/fix K's retry+poll to also cover a nested call whose own attempt hit Extend-fallback
  would require CHANGING (loosening) ShouldRetryScopedActivation's own already-approved,
  deliberately-narrow gate (the `usedScopedActivation` condition itself, added and approved in
  rounds 13-15/17 specifically to EXCLUDE this shape) -- a control-flow change to an
  already-shipped guardrail, not a mechanical extension to a location the existing mechanism
  already structurally reaches. Per this round's explicit conservative default ("needs its own
  decision checkpoint" whenever there is any doubt), this is presented as a checkpoint
  decision below, not auto-implemented. Candidate directions (none yet chosen): (i) leave the
  gate unchanged (accept that a nested correction's own Extend-fallback failure is never
  retried, relying solely on the passive TryReactivelyCorrectAgainstLastIntent watchdog to
  eventually notice and correct the drift out-of-band); (ii) extend ShouldRetryScopedActivation
  (or a nested-call-specific variant) to also retry when usedScopedActivation is false BUT the
  attempt is itself a fix-H nested correction (not the top-level request) -- reasoning that a
  collateral-restore failure has lower risk-of-masking-a-real-problem than a top-level retry
  would, since the top-level request already succeeded; (iii) apply fix K's
  poll-until-reachable specifically before the NESTED call's own first attempt (not just
  before its retries), on the theory that the same transient condition affecting the outer
  call's scoped-plan throw might also affect the nested call's, and waiting first could avoid
  the Extend-fallback path entirely for the nested case.
fix_item_B: >
  NOT APPLIED THIS ROUND. Assessed as comparatively low-risk (a message-text/
  exception-enrichment change, not a control-flow change) but still a genuine design choice
  with more than one reasonable shape, not a single obvious one-liner -- per this round's own
  instruction to err toward presenting it as a checkpoint option whenever uncertain, this is
  ALSO presented as a decision below. Candidate directions (none yet chosen): (i) wrap the
  nested ActivateMonitors call (line 744) in its own try/catch that catches any exception and
  re-throws a new, enriched exception whose message explicitly names the ORIGINAL requested
  target(s) as already-succeeded and frames the failure as "could not restore an unrelated
  monitor (SAM7489) that was collaterally affected while enabling ACI24A4" rather than "enabling
  ACI24A4 failed"; (ii) add a lightweight marker (e.g. a custom exception type or a property)
  distinguishing "collateral correction failure" from "primary request failure" at the point
  fix H's nested call is made, letting MainForm's catch block construct a clearer message using
  the already-in-scope `devicePath` (the user's real target); (iii) leave ActivateMonitors'
  own exception construction unchanged and instead have MainForm's OnTileAction catch block
  inspect whether `devicePath` itself (ACI24A4) appears in the actually-active set (it does,
  per RefreshMonitorTiles) before choosing dialog wording, framing it as "your monitor was
  enabled, but restoring {other monitor} failed" whenever the requested target is confirmed
  active despite the thrown exception.
verification_item_A: { result: not_applicable, reason: "No code changed this round -- pure source-verified log reconstruction and classification. No build, test, or guardrail run performed." }
verification_item_B: { result: not_applicable, reason: "No code changed this round -- pure source-verified log reconstruction and classification. No build, test, or guardrail run performed." }
files_changed_round_19: []

## Resolution (round 20 addendum -- BOTH items IMPLEMENTED and self-verified; checkpoint response to round 19's Option A2 (item A) + Option B1 (item B))

round_20_reasoning_checkpoint_item_A:
  hypothesis: >
    ShouldRetryScopedActivation's own, already-approved usedScopedActivation-gated exclusion
    of Extend-fallback attempts is correct and must stay unchanged for the TOP-LEVEL,
    directly-user-requested call (rounds 13-17's own evidence: Extend-fallback is a
    structurally different, less-trustworthy failure shape than scoped-succeeds-but-target-
    never-settles). But that SAME exclusion, applied unmodified to fix H's OWN nested
    correction call (restoring a survivor collaterally dropped as a side effect of the
    caller's own request), is over-broad: by the time that nested call runs, the user's own
    request has ALREADY succeeded (round 19's own trace confirmed this directly), so a
    nested-cleanup-specific retry carries materially lower risk of masking a real top-level
    problem than loosening the gate for the user's own action would. A genuinely SEPARATE
    eligibility rule, gated on a newly-threaded isNestedCorrectionCall parameter and OR'd
    alongside the untouched ShouldRetryScopedActivation, extends retry-then-poll to this ONE
    additional call site without touching the top-level gate at all.
  confirming_evidence:
    - "Direct source read, WindowsMonitorController.cs (ActivateMonitorsCore in full, post-
      edit): the public ActivateMonitors(...) wrapper now delegates to
      ActivateMonitorsCore(..., isNestedCorrectionCall: false) -- every existing caller
      (ToggleService.cs lines 116/461, MainForm.cs line 1345/1288-equivalent,
      RigToggle.Windows.Tests) is unaffected, since IMonitorController's public signature was
      never touched. The ONLY call site passing isNestedCorrectionCall: true is fix H's own
      nested correction call (formerly line 744); ShouldRetryScopedActivation itself is called
      with the exact same four arguments as before, same body, confirmed unchanged."
    - "Direct source read confirms retryEligibleNestedOnly is computed as
      `!retryEligibleTopLevel && ShouldRetryNestedCorrectionActivation(...)` -- structurally
      IMPOSSIBLE for this new rule to change the top-level call's own retry decision
      (isNestedCorrectionCall is always false there, so ShouldRetryNestedCorrectionActivation
      always returns false for it; the OR only ever adds eligibility, never removes it)."
    - "New unit tests (WindowsMonitorControllerTests.cs, RigToggle.Windows.Tests) directly
      confirm ShouldRetryNestedCorrectionActivation's four boundary cases: nested+Extend-
      fallback+budget-remains returns true (the exact round-19 rig shape); top-level
      (isNestedCorrectionCall=false) NEVER retried via this gate even with an identical
      Extend-fallback shape; nothing-still-inactive returns false; budget-exhausted returns
      false (never unbounded); last-allowed-attemptNumber still returns true -- mirroring
      ShouldRetryScopedActivation's own existing 5-test shape exactly."
  falsification_test: >
    If a future rig trial shows a NESTED correction call's own scoped ApplyPathInfos falling
    back to Extend, and the log does NOT show the new "round 20 item A/fix A2: retry-
    eligibility EXTENDED..." line firing for it (i.e. the nested call's own D-05 throws
    immediately with no retry attempted, exactly as round 19 observed), this hypothesis is
    refuted -- it would mean the isNestedCorrectionCall flag is not actually reaching
    ShouldRetryNestedCorrectionActivation as designed, or ActivateMonitorsCore's own call-site
    OR condition is not evaluating as traced.
  fix_rationale: >
    Addresses the ROOT CAUSE round 19 confirmed (an already-approved, deliberately-narrow gate
    applying with equal, unmodified effect to a call site -- fix H's nested cleanup -- it was
    never specifically evaluated against) by adding a narrowly-scoped, separately-gated
    exception to that gate for exactly the one call site where the risk calculus differs (the
    user's own request has already succeeded by the time this runs) -- not a workaround, and
    not a broadening of the top-level gate itself. Confirmed via direct before/after
    comparison that ShouldRetryScopedActivation's own logic/signature, fix H's correction-loop
    logic (ComputeUnexpectedlyActivated/ComputeUnexpectedlyDeactivated), fix B's source-
    preference logic, and MaxScopedActivationRetryAttempts (still 2) are all byte-for-byte
    unchanged for the top-level path -- only a nested call's own retry-eligibility has
    changed.
  blind_spots:
    - "Cannot execute or verify on real Windows CCD hardware in this sandbox -- same
      structural limitation as every round of both debug sessions. Whether the extended
      nested retry actually RECOVERS more often than not (as opposed to merely firing) is
      unverified beyond source-level correctness and unit tests -- deferred to the mandatory
      human-verify checkpoint below."
    - "Reuses the SAME MaxScopedActivationRetryAttempts budget (2) and the SAME
      PollUntilTargetsReachable bound (~2.85s) already in place for the top-level case, per
      the task's own explicit instruction not to invent a separate budget without a stated
      reason. Whether this specific budget/bound is SUFFICIENT for the nested case (which may
      face a subtly different timing condition, since it's triggered by fix H's own
      correction loop moments after the outer call's own Extend, not by the user's original
      click) is unverified and carried forward as an open question, exactly as round 18 left
      the top-level bound's sufficiency open."
    - "Does not re-litigate root_cause (8)'s own still-open, genuinely-unexplained trigger
      (WHY the scoped ApplyPathInfos plan throws PathChangeException for this specific
      monitor pairing in the first place) -- carried forward unchanged."
  candidate_causes:
    - "code (already-approved, deliberately-narrow guardrail, now narrowly extended): the SAME
      candidate_causes entry round 19 identified -- ShouldRetryScopedActivation's
      usedScopedActivation condition -- remains byte-for-byte unchanged; a new, additive,
      separately-gated rule (ShouldRetryNestedCorrectionActivation) now runs alongside it for
      the one call site round 19 identified (fix H's nested correction call)."
    - "environment/CCD-driver (pre-existing, root_cause (8), NOT re-investigated this round):
      unchanged, still the genuinely open, unconfirmed trigger for the underlying
      PathChangeException fragility itself."
  and_gate: >
    Unchanged from round 19's own finding: this failure shape still requires BOTH the
    pre-existing environment/CCD-driver PathChangeException trigger AND the (pre-round-20)
    retry-eligibility gate excluding the nested call to combine before the nested call's D-05
    could surface uncaught. Round 20 does not touch the AND-gate's first operand
    (environment/CCD-driver trigger, still unconfirmed) -- it narrows when the second operand
    (retry-eligibility) excludes a nested cleanup call specifically, reducing (not
    eliminating) how often the combination still produces an uncaught D-05.

round_20_reasoning_checkpoint_item_B:
  hypothesis: >
    The D-05 exception's TYPE, not just its message text, is the cleanest place to mark "this
    failure originated in fix H's own nested cleanup, not the top-level request" -- reusing
    item A's SAME isNestedCorrectionCall signal (rather than a second, independent detection
    mechanism, per this round's own design constraint) to decide, at the exact point of the
    D-05 throw, whether to throw a plain InvalidOperationException (top-level, byte-for-byte
    unchanged) or a new CollateralMonitorRestoreFailedException (nested) carrying the
    collaterally-affected device path(s) as a structured property. MainForm's ENABLE-branch
    catch block (which already has the user's own succeeded devicePath in scope) uses the
    exception's concrete type -- via a new, pure, unit-tested
    MonitorEnableFailureMessageBuilder.Build helper -- to build a clarified dialog only in the
    nested-collateral case, leaving every other failure shape's dialog text unchanged.
  confirming_evidence:
    - "Direct source read confirms the terminal D-05 throw site: for isNestedCorrectionCall ==
      false, the thrown object and its Message are constructed identically to the pre-round-20
      code (exitThrowMessage built from the exact same interpolated string, passed to a plain
      `new InvalidOperationException(exitThrowMessage)`) -- confirmed byte-for-byte via direct
      before/after comparison of the string template."
    - "Direct source read, MainForm.cs lines 1293 and 1350 (both existing
      `catch (InvalidOperationException ex)` clauses) and ToggleService.cs's TryExecuteStep
      (`catch (Exception ex)`): none inspect the concrete exception type, all three continue
      to catch CollateralMonitorRestoreFailedException with zero code change -- confirmed by
      direct read, not assumed, per this round's own explicit instruction."
    - "New unit tests (RigToggle.Tests/MonitorEnableFailureMessageBuilderTests.cs) directly
      confirm: a CollateralMonitorRestoreFailedException produces a message naming BOTH the
      originally-requested device path AND every collaterally-affected device path (single and
      multiple); a plain InvalidOperationException (both the D-05 top-level shape and the
      separate 'not detected' guard shape) passes through byte-for-byte unchanged; the new
      exception type IS-A InvalidOperationException; AffectedDevicePaths is exposed separately
      from Message."
  falsification_test: >
    If a future rig trial shows the extended nested retry (item A) firing and eventually being
    exhausted, but the resulting dialog STILL fails to name the user's own succeeded target
    (i.e. still reads like a generic device-path list with no "was enabled successfully"
    framing), item B's implementation is refuted -- it would mean
    CollateralMonitorRestoreFailedException was not actually thrown/caught as designed, or
    MonitorEnableFailureMessageBuilder was not actually reached by MainForm's catch block.
  fix_rationale: >
    Addresses the ROOT CAUSE round 19 confirmed (no marker anywhere in the exception's type or
    message construction path distinguishing a collateral-restore failure from a primary-
    request failure) by adding exactly that marker at the one place it can be added accurately
    (the nested call's own D-05 throw, which already knows it is nested) and consuming it at
    the one place the user's own succeeded target is already in scope (MainForm's catch block)
    -- not a superficial reword of a message that would still describe the wrong thing, and
    not a new exception hierarchy that would risk breaking an existing catch clause (confirmed
    by direct read that none depends on the concrete type). Message construction is a pure,
    unit-tested function (MonitorEnableFailureMessageBuilder), matching this session's own
    established discipline of extracting pure logic wherever the underlying operation allows
    it.
  blind_spots:
    - "Cannot execute or verify on real Windows CCD hardware in this sandbox. The actual
      dialog text's real-world clarity (does it read well to the user in practice, not just
      pass a unit-test assertion) is unverified -- deferred to the mandatory human-verify
      checkpoint below."
    - "Deliberately scoped ONLY to MainForm.OnTileAction's ENABLE branch, matching round 19's
      own citation of exactly that catch site -- ToggleService.cs's own ActivateMonitors call
      sites (the toggle-switch path) were confirmed NOT to conflict with this change
      (TryExecuteStep's catch is type-agnostic and uses ex.Message raw), but a toggle-switch-
      triggered occurrence of this same collateral-failure shape would still surface via
      ToggleService's existing, unclarified ToggleStepResult.Message today. Not fixed this
      round; not asked for by the approved checkpoint decision text either -- flagged here, not
      silently expanded into, matching this investigation's own established discipline (rounds
      10/11/13/15/18 precedent) of not expanding an authorized decision's scope."
  candidate_causes:
    - "code (control-flow, pre-existing since round 7's fix H): the SAME candidate_causes
      entry round 19 identified -- the nested call's own exception propagating with no
      distinguishing context -- is now closed by CollateralMonitorRestoreFailedException plus
      MonitorEnableFailureMessageBuilder."
    - "environment/CCD-driver (pre-existing, root_cause (8), NOT re-investigated this round):
      unchanged."
  and_gate: >
    No -- item B's fix is a single-category (code) change: enriching the exception type and
    consuming it in exactly one catch site is sufficient to resolve the reported UX gap
    (message not distinguishing collateral from primary failure), independent of whatever the
    environment-level root cause of the underlying PathChangeException turns out to be. Both
    categories are documented per the branching requirement; only the code-category change was
    made this round, matching the user's Option B1 choice.

root_cause_status_round_20: >
  root_cause (8) (the underlying SAM7489/ACI24A4 OS/driver-level PathChangeException
  nondeterminism) remains OPEN in the strict "what is the mechanism" sense -- neither item A
  nor item B claims to have identified or eliminated it. Item A makes the EXISTING retry
  mechanism reach one additional call site (fix H's nested cleanup) it was structurally
  excluded from for a reason that no longer fully applies there; item B makes the resulting
  failure message, for the remaining case where even the extended retry is exhausted,
  accurately distinguish a collateral side-effect failure from a primary-request failure. This
  is the SAME root_cause (8) class round 19 already classified this failure shape under -- not
  a new root cause.
root_cause_addendum_item_A: >
  Unchanged from round 19's characterization (see that round's root_cause_item_A) -- confirmed
  again this round via direct code read of the post-edit ActivateMonitorsCore. No new root-
  cause information beyond round 19's; this round is the approved fix for the already-
  confirmed gap.
fix_item_A: >
  src/RigToggle.Windows/WindowsMonitorController.cs: the pre-round-20 public
  `ActivateMonitors(monitorDevicePaths, monitorSwapDisableSet)` method is now a thin wrapper
  that delegates to a new private `ActivateMonitorsCore(monitorDevicePaths,
  monitorSwapDisableSet, isNestedCorrectionCall)` -- byte-for-byte the pre-round-20 method
  body, plus the new parameter. IMonitorController's public interface is completely
  unchanged; every existing caller (ToggleService, MainForm, RigToggle.Windows.Tests) is
  unaffected. Fix H's own nested correction call (previously a recursive
  `ActivateMonitors(unexpectedlyDeactivated, monitorSwapDisableSet: new HashSet<string>())`)
  now calls `ActivateMonitorsCore(..., isNestedCorrectionCall: true)` directly -- the ONLY call
  site in the file that ever passes true. A new pure function,
  ShouldRetryNestedCorrectionActivation(isNestedCorrectionCall, requestedStillInactiveCount,
  attemptNumber, maxRetryAttempts), mirrors ShouldRetryScopedActivation's own (b)/(c)
  conditions exactly (only (a)'s condition differs: isNestedCorrectionCall instead of
  usedScopedActivation) and is OR'd alongside the unchanged ShouldRetryScopedActivation at the
  retry-eligibility decision point -- short-circuited so it can never fire for the top-level
  call. Reuses the SAME MaxScopedActivationRetryAttempts budget (2) and the SAME
  PollUntilTargetsReachable poll/bound already in place; no separate budget or bound was
  introduced. New, distinct log lines mark whenever the extension is what made a retry
  eligible ("round 20 item A/fix A2: retry-eligibility EXTENDED...").
verification_item_A:
  target_test: { result: pass, note: "5 new unit tests added to src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs, mirroring ShouldRetryScopedActivation's own 5-test shape exactly: nested+Extend-fallback+budget-remains returns true (the round-19 rig shape); top-level (isNestedCorrectionCall=false) never retried via this gate; nothing-still-inactive returns false; budget-exhausted returns false (never unbounded); last-allowed-attemptNumber still returns true." }
  mutation_check: { result: skipped, reason: "No Stryker configured in this repo (matches every prior debug session in this codebase)." }
  no_op_deletion: { result: pass, deletion_justified_by_rca: true, note: "No code deleted -- the pre-round-20 ActivateMonitors method body is preserved byte-for-byte inside the new ActivateMonitorsCore, with the public method becoming a 1-line delegating wrapper (net addition, not a deletion or behavior change for any existing caller). ShouldRetryScopedActivation itself is untouched; ShouldRetryNestedCorrectionActivation is a new, additive, separately-gated function. The nested call site's `ActivateMonitors(...)` recursive call was changed to `ActivateMonitorsCore(..., isNestedCorrectionCall: true)` -- a call-target rename with an added argument, not a deletion of any logic; the arguments passed for monitorDevicePaths/monitorSwapDisableSet are identical to before." }
  adjacent_tests: { result: pass, suites_run: ["dotnet build RigToggle.sln --no-incremental (0 errors, same 6 pre-existing unrelated xUnit1031 warnings, all 6 projects)", "dotnet build src/RigToggle.Windows.Tests (0 errors, 0 new warnings)", "dotnet test src/RigToggle.Windows.Tests (builds cleanly; execution aborts with the same pre-existing sandbox limitation as every prior round of both debug sessions -- missing Microsoft.WindowsDesktop.App runtime on this Linux sandbox, confirmed again this round, unrelated to this change)"] }
  revert_and_reconfirm: { result: pending, reason: "Requires a rig trial reproducing the nested-fix-H-correction-hits-Extend-fallback shape (round 19's exact scenario) to observe whether the extended retry actually fires and recovers -- deferred to the human-verify checkpoint below." }
  guardrail_verdict: accepted
  guardrail_note: "All self-executable signals pass: no-op/deletion review confirms no behavior loss for the top-level path (the wrapper/core split and the OR'd eligibility check are both purely additive, confirmed byte-for-byte via direct before/after comparison of ShouldRetryScopedActivation, the top-level throw's message/type, and every existing caller's call site); full-solution build is clean; 5 new unit tests directly exercise the new pure eligibility function's boundary cases, mirroring the existing ShouldRetryScopedActivation test suite's own shape and rigor. Hardware-dependent behavior (does the extended nested retry actually fire and recover on a real rig) is deferred to the mandatory human-verify checkpoint."
files_changed_item_A:
  - src/RigToggle.Windows/WindowsMonitorController.cs
root_cause_addendum_item_B: >
  Unchanged from round 19's characterization (see that round's root_cause_item_B) -- confirmed
  again this round via direct code read of MainForm.cs lines 1293/1350 and
  ToggleService.TryExecuteStep. No new root-cause information beyond round 19's; this round is
  the approved fix for the already-confirmed gap.
fix_item_B: >
  (a) src/RigToggle.Core/CollateralMonitorRestoreFailedException.cs (new file): a sealed
  exception subclassing InvalidOperationException (mirroring ToggleInProgressException's own
  precedent in the same namespace), carrying AffectedDevicePaths (IReadOnlyList<string>)
  separately from Message. (b) src/RigToggle.Windows/WindowsMonitorController.cs: the D-05
  verify-and-throw site now branches on isNestedCorrectionCall (the SAME flag item A
  introduced) -- throws CollateralMonitorRestoreFailedException(exitThrowMessage,
  stillInactive) when true, or the byte-for-byte-unchanged plain
  InvalidOperationException(exitThrowMessage) when false. (c) src/RigToggle.Core/
  MonitorEnableFailureMessageBuilder.cs (new file): a pure static Build(requestedDevicePath,
  InvalidOperationException ex) helper -- pattern-matches on CollateralMonitorRestoreFailedException
  to build "{requestedDevicePath} was enabled successfully, but restoring {affected path(s)}
  (affected as a side effect) failed. {ex.Message}"; returns ex.Message unchanged for every
  other exception shape. (d) src/RigToggle.App/MainForm.cs: OnTileAction's ENABLE-branch catch
  (InvalidOperationException) now calls MonitorEnableFailureMessageBuilder.Build(devicePath,
  ex) instead of passing ex.Message directly to MessageBox.Show -- devicePath (the user's own,
  already-in-scope, actually-succeeded target) is the only new input; the DISABLE branch's
  catch block and every other exception-handling path in the file are untouched.
verification_item_B:
  target_test: { result: pass, note: "6 new unit tests added to src/RigToggle.Tests/MonitorEnableFailureMessageBuilderTests.cs: collateral failure names both the succeeded request and the affected monitor (single and multiple affected paths); top-level plain InvalidOperationException (both the D-05 shape and the separate 'not detected' guard shape) passes through byte-for-byte unchanged; CollateralMonitorRestoreFailedException is caught by an InvalidOperationException-typed catch (confirmed via Assert.IsAssignableFrom, proving MainForm's existing catch clauses require zero change); AffectedDevicePaths/Message are exposed as separate, independently-settable properties." }
  mutation_check: { result: skipped, reason: "No Stryker configured in this repo (matches every prior debug session in this codebase)." }
  no_op_deletion: { result: pass, deletion_justified_by_rca: true, note: "No code deleted -- CollateralMonitorRestoreFailedException and MonitorEnableFailureMessageBuilder are both new, additive files; MainForm.cs's catch block changed from `MessageBox.Show(this, ex.Message, ...)` to `MessageBox.Show(this, MonitorEnableFailureMessageBuilder.Build(devicePath, ex), ...)` -- a call-target change (net addition of one argument and one helper invocation), not a deletion, and the builder itself returns ex.Message UNCHANGED for every non-collateral exception shape (confirmed by direct unit test), so no existing user-visible message text changes for any OTHER failure." }
  adjacent_tests: { result: pass, suites_run: ["dotnet build RigToggle.sln --no-incremental (0 errors, same 6 pre-existing unrelated xUnit1031 warnings, all 6 projects)", "dotnet test src/RigToggle.Tests (249/249 passed -- 243 pre-existing + 6 new, all pass)"] }
  revert_and_reconfirm: { result: pending, reason: "Requires a rig trial where the nested retry (item A) is eventually exhausted despite the extension, to observe the actual clarified dialog text in the real app -- deferred to the human-verify checkpoint below." }
  guardrail_verdict: accepted
  guardrail_note: "All self-executable signals pass: no-op/deletion review confirms no behavior loss for every non-collateral failure shape (byte-for-byte-unchanged message, confirmed by direct unit test); full-solution build is clean; the existing RigToggle.Tests suite passes in full (249/249) including 6 new tests directly exercising the message-builder's two branches and the new exception type's inheritance/property contract. Hardware-dependent behavior (does the clarified dialog actually read well and appear correctly in the real app) is deferred to the mandatory human-verify checkpoint."
files_changed_item_B:
  - src/RigToggle.Core/CollateralMonitorRestoreFailedException.cs
  - src/RigToggle.Core/MonitorEnableFailureMessageBuilder.cs
  - src/RigToggle.Windows/WindowsMonitorController.cs
  - src/RigToggle.App/MainForm.cs

## CHECKPOINT REACHED (round 18 -- ANSWERED implicitly by round 19's fresh log (which predates the round-18 build and neither confirms nor refutes it -- real-world confirmation for round 18's items 1/3 remains outstanding); superseded by round 19's checkpoint at the end of this file)

**Type:** human-verify
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 35 evidence entries, 1 eliminated hypothesis, across ten investigation/implementation rounds since the regression reopened (round 8 through round 18). This round implemented both of round 17's checkpoint decisions (item 1: Option 1A; item 3: Option 3B) -- no new investigation, no new evidence gathered.

### Investigation State

**What changed this round:**
- **Item 1 (toggle-switch stale-device-path defect):** FIXED. `ToggleService` now checks your saved monitor list against what's actually plugged in RIGHT NOW before sending anything to Windows. A stale/ghost entry is skipped (and named in a clear message) instead of blocking the whole toggle. The toggle switch's own version of the "bakes in a failed attempt" bug (the same one already fixed for the dashboard tiles) is also fixed.
- **Item 3 (SAM7489 automatic-retry timing):** IMPROVED (not "fixed" -- see below). The automatic retry that used to fire instantly now actively waits (up to ~2.85 seconds, never longer) for the monitor to report itself reachable again before trying, instead of firing with zero delay.

**Self-verified checks:**
- Full solution build: clean, 0 errors (all 6 projects).
- RigToggle.Tests: 243/243 passed (238 pre-existing + 5 new tests for item 1).
- RigToggle.Windows.Tests: builds cleanly (39 tests, unchanged); cannot execute in this sandbox (same pre-existing missing-Windows-runtime limitation as every prior round).
- No-op/deletion review: pass for both items -- nothing was deleted, only additive/gated logic inserted.
- Guardrail verdict: **accepted** for both items, recorded separately.

### Checkpoint Details

**Need verification:** please use the app normally for a while -- BOTH the Rig/Normal toggle switch AND the dashboard tile clicks -- and send a fresh debug.log excerpt the next time either symptom recurs (a toggle that fails or behaves oddly, or a monitor's position/numbering looking wrong).

**What to specifically look for in the next debug.log, for each item:**

- **Item 1:** if you still have a stale/ghost monitor entry in settings.json, does the toggle switch now show ONE clear message naming just that stale monitor, instead of the old opaque "not detected" failure blocking everything? Do your real, still-connected monitors still get switched correctly? Look for a new log line starting with `LiveFilterMonitorSets: filtering stale device path(s)...` — its presence (or absence) tells us whether this path is actually being exercised on your rig.
- **Item 3:** if the SAM7489 "click twice to fix it" pattern happens again, look for a new log line starting with `ActivateMonitors: round 18 (fix K / poll-until-reachable)` — it will show whether the wait fired, how long it actually waited, and whether the target became reachable within the bound. Immediately below it, the existing `TryBuildScopedActivationPlan: ... has N candidate PathDisplaySource(s)` line for the retry attempt will show whether the candidate count improved versus the "0 candidates" seen in rounds 15/17.

**How to check:**
1. Keep using Rig Toggle as normal for the next several days, using both the toggle switch and the dashboard tiles at different times.
2. If either symptom recurs (a toggle failure/error message, or a monitor position/numbering problem), copy the relevant excerpt of `%LocalAppData%\RigToggle\debug.log` (with `EnableDebugLogging` on) covering that event.
3. Send it over, along with which control you used (toggle switch vs. tile) and what you observed.

**Tell me:** "confirmed fixed" (for either or both items) once you've seen it work correctly in your real workflow, or send the fresh debug.log excerpt if either symptom recurs. Root cause (8) (the underlying SAM7489 external unavailability) is NOT claimed fixed by item 3 -- only the retry's own timing has changed; genuine confirmation there means seeing evidence the wait actually helps (a non-zero candidate count on the automatic retry), not just "it eventually worked" (which was already possible via manual re-click before this round).

## CHECKPOINT REACHED (round 19 -- ANSWERED: Option A2 (item A) + Option B1 (item B); superseded by round 20's checkpoint at the end of this file)

**Type:** decision
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 34 evidence entries, 1 eliminated hypothesis, across eleven investigation/implementation rounds since the regression reopened (round 8 through round 19). This round is pure source-verified log reconstruction -- no code changed, no build/test run.

### Investigation State

**What the log showed:** you clicked the ACI24A4 ("VG248") tile to turn it ON. It DID turn on successfully. But the app's own internal cleanup logic, while restoring an unrelated monitor (SAM7489, "Odyssey G5") that got collaterally knocked offline as a side effect of that same action, itself failed -- and the error dialog you saw named ONLY "SAM7489", not ACI24A4, with nothing indicating your actual click had already succeeded or that SAM7489 was a side effect, not your request. This IS the same underlying flakiness already tracked as root_cause (8) (never fully explained, only mitigated) -- appearing at a new spot in the code (a corrective retry the app makes internally, not your original click), not a new bug class. This log was explicitly sent as a PRE-round-18 capture -- it does not confirm or refute round 18's item 1 or item 3 fixes; that confirmation is still pending separately.

**Verified directly against the code (not just the log's own narrative):**
- The app's automatic-retry safety net (built in round 18) is NOT structurally blind to this internal cleanup call -- it would apply to it in principle. It simply didn't fire here because this internal cleanup call hit the SAME "fell back to a broader, less precise recovery" condition that an earlier round already deliberately decided should skip the retry -- for BOTH your original click and the internal cleanup attempt, independently, in this one trial.
- The confusing error message is a separate, genuine gap: nothing in how the app builds or passes along that error distinguishes "your click failed" from "cleaning up after your click's side effect failed" -- so it will keep looking like your click failed, for as long as that internal cleanup's own retry can be exhausted for any reason, even after the app's own known SAM7489 flakiness is eventually resolved.

### Checkpoint Details

**Decision needed (item A):** should the app's retry-then-wait safety net be extended to also cover this internal cleanup call, even when it falls back to the broader recovery path -- something an earlier round deliberately excluded for your ORIGINAL click (because that fallback path was judged a different, less-trustworthy failure shape)? Extending it here means loosening that earlier, deliberate exclusion specifically for internal cleanup, not your own direct actions.

**Options for item A:**
- **A1 (leave as-is):** Do not extend the retry. Accept that if the app's internal cleanup itself hits this fallback path, it won't get an extra automatic retry -- rely on the existing background watchdog to notice and quietly fix the drift later (it was seen trying, but blocked, four times in this log, without succeeding within the captured window).
- **A2 (extend retry to internal cleanup specifically):** Allow the retry-then-wait safety net to also apply when the FAILING attempt is the app's own internal cleanup (not your direct click) even if it falls back to the broader recovery path -- reasoning that your own request already succeeded by this point, so a cleanup-specific retry carries less risk than loosening the rule for your own actions would.
- **A3 (apply the wait before the cleanup's first attempt, not just its retries):** Have the app actively wait for the affected monitor to look available again BEFORE the internal cleanup even makes its first attempt -- on the theory the same transient condition affecting your click might still be affecting the cleanup moments later, and waiting first could avoid the fallback path entirely for this case.

**Decision needed (item B):** should the error message/dialog be made clearer when this specific situation happens (your action succeeded, but the app's own side-effect cleanup failed)?

**Options for item B:**
- **B1 (fix now):** Change the error message so it clearly says something like "ACI24A4 was enabled successfully, but restoring SAM7489 (affected as a side effect) failed" instead of the current opaque "monitor enable did not take effect: SAM7489." Low risk -- text/message construction only, not a change to what the app actually does.
- **B2 (defer):** Leave the message as-is for now and revisit after item A's decision, in case the chosen item-A option changes how often this message would even be seen.

**Tell me:** your choice for item A (A1/A2/A3) and, separately, your choice for item B (B1/B2) -- they're independent decisions and don't need to match. Also let me know once you have a fresh debug.log from the round-18 patched build, so round 18's item 1 and item 3 can finally get real-world confirmation (still outstanding, unrelated to this round's findings).

## CHECKPOINT REACHED (round 20 -- item A (Option A2) + item B (Option B1) BOTH implemented and self-verified; awaiting real-world rig confirmation; round 18's items 1/3 STILL unconfirmed, restated below)

**Type:** human-verify
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 35 evidence entries, 1 eliminated hypothesis, across twelve investigation/implementation rounds since the regression reopened (round 8 through round 20). This round implemented round 19's two checkpoint decisions (item A: Option A2; item B: Option B1) -- no new investigation, no new evidence gathered; both fixes build directly on round 19's already-confirmed root-cause characterization.

### Investigation State

**What changed this round:**
- **Item A (nested-cleanup retry-eligibility, Option A2):** IMPLEMENTED. The app's internal "wait and retry" safety net (rounds 14/18's fix A/fix K) now ALSO applies when the app's own internal cleanup call (restoring a monitor collaterally knocked offline as a side effect of your click, not your click itself) hits the same fallback path an earlier round deliberately excluded for your own direct actions. Your own direct clicks/toggles are completely unaffected -- confirmed via direct before/after code comparison that the existing retry rule for your own actions is byte-for-byte unchanged. Same retry-count budget (2 extra attempts) and same wait bound (~2.85s) as everywhere else -- no new budget was invented.
- **Item B (collateral-failure message clarity, Option B1):** IMPLEMENTED. If the internal cleanup's own retry (even with item A's extension) is eventually exhausted, the dialog you see will now say something like "ACI24A4 was enabled successfully, but restoring SAM7489 (affected as a side effect) failed..." instead of the old, opaque "Monitor enable did not take effect: SAM7489" with no indication your own click had already worked. Every OTHER failure dialog's text is completely unchanged.
- **Expected net effect going forward:** thanks to item A, you should see this error dialog LESS OFTEN than before (more cases now recover automatically, silently, with no dialog at all). In the remaining cases where it still appears, item B means it will read clearly instead of looking like your own click failed.

**Self-verified checks:**
- Full solution build: clean, 0 errors (all 6 projects, same 6 pre-existing unrelated warnings).
- RigToggle.Tests: 249/249 passed (243 pre-existing + 6 new tests for item B's message builder and new exception type).
- RigToggle.Windows.Tests: builds cleanly (5 new tests for item A's new retry-eligibility rule, mirroring the existing rule's own test shape); cannot execute in this sandbox (same pre-existing missing-Windows-runtime limitation as every prior round).
- No-op/deletion review: pass for both items -- nothing was deleted; both changes are additive (a wrapper/core method split plus an OR'd eligibility check for item A; two new files plus a one-line catch-block change for item B), confirmed byte-for-byte unchanged for every pre-existing path (your own direct clicks/toggles, and every non-collateral failure message).
- Guardrail verdict: **accepted** for both items, recorded separately in the debug file's Resolution (round 20 addendum).

### Checkpoint Details

**Need verification:** please use the app normally for a while (dashboard tiles especially, since that's where round 19's evidence came from) and send a fresh debug.log excerpt the next time you see the "enabling one monitor collaterally drops another" shape -- a monitor tile turning ON while a DIFFERENT, unrelated monitor unexpectedly goes off.

**What to specifically look for in the next debug.log, for each item:**

- **Item A:** look for a new log line starting with `ActivateMonitors: round 20 item A/fix A2: retry-eligibility EXTENDED...`. Its presence means the extended nested-retry fired. If it's followed by a normal "nested ActivateMonitors(...) completed without throwing" line and NO error dialog appeared, item A recovered the situation automatically -- this is the expected, common outcome going forward.
- **Item B:** if you DO still see an error dialog for this specific shape, check whether it now reads "{your clicked monitor} was enabled successfully, but restoring {other monitor} (affected as a side effect) failed..." instead of the old "Monitor enable did not take effect: {other monitor}" wording with no mention of your own click. Also look for a new log line starting with `ActivateMonitors: EXIT throwing ... (round 20 item B/fix B1: isNestedCorrectionCall=true -- throwing CollateralMonitorRestoreFailedException...)`.

**How to check:**
1. Keep using Rig Toggle as normal for the next several days, using both the toggle switch and the dashboard tiles at different times.
2. If you see a monitor unexpectedly go off as a side effect of turning another one on, copy the relevant excerpt of `%LocalAppData%\RigToggle\debug.log` (with `EnableDebugLogging` on) covering that event, along with a note on whether an error dialog appeared and what it said.
3. If nothing like this happens for a while, that itself is useful information -- let me know either way.

**Tell me:** "confirmed fixed" once you've seen item A recover silently in your real workflow (or seen item B's clarified message in the remaining case where it doesn't), or send the fresh debug.log excerpt if the old, unclarified behavior recurs.

**STANDING, SEPARATE, STILL-OUTSTANDING ASK (round 18's items -- do not lose track of this):** round 18's two items (the toggle-switch stale-device-path fix, and the SAM7489 retry-timing/poll-until-reachable improvement) STILL have NO real-world confirmation from the round-18 patched build. Every debug.log excerpt received since round 18 was implemented has been either pre-round-18 data or unrelated to those two specific fixes. This remains a separate, outstanding, still-unconfirmed ask that must not be dropped or forgotten alongside this round's new item A/B fixes -- if you have a fresh debug.log from the round-18-and-later patched build (regardless of whether it shows this round's collateral-drop shape or the round-18 toggle-switch/SAM7489 shapes), please send it.

## Round 21 -- HOLISTIC ARCHITECTURE ASSESSMENT (no fix applied; user explicitly asked to step back from incremental patching)

**Type of round:** assessment/architecture-review, NOT a fix round. Triggered by explicit user pushback ("Recheck the whole monitor logic... you made something way more complicated than it should be") relayed via the orchestrator, plus a partial new debug.log excerpt (23:18:34-38, a fourth failure shape). No code was changed this round. This round supersedes round 20's "awaiting_human_verify" status with a decision checkpoint about the investigation's whole direction.

### 21.1 Fresh, holistic inventory (WindowsMonitorController.cs + MainForm.cs + ToggleService.cs, read in full, ignoring round-by-round rationale)

Distinct, independently-triggered mechanisms currently coexisting in this subsystem, for a single-user, 3-monitor, single-GPU-adapter rig:

1. `_lastKnownActiveModeByDevicePath` position/mode cache (`CacheLiveModes`) -- populated unconditionally for every active path before every mutation (widened round 8 from swap-only).
2. Scoped activation (`TryBuildScopedActivationPlan` + `PathInfo.ApplyPathInfos(allowChanges:true, forceModeEnumeration:true)`) as PRIMARY mechanism, with whole-topology `ApplyTopology(Extend)` as a documented-unreliable FALLBACK only when a scoped plan cannot even be built.
3. Present-but-inactive array representation + `PromoteToOriginIfNeeded` (origin/primary normalization) inside (2), required because SetDisplayConfig's own documented contract rejects an omitted or origin-less array.
4. `SelectSourceForActivation` (fix B) -- prefers a target's own previously-cached `PathDisplaySource` over the greedy first-unclaimed pick.
5. An in-call settle-poll-then-correct loop (`MaxCorrectionRounds=3`, `RequiredConsecutiveCleanRounds=2`) with two symmetric pure predicates: `ComputeUnexpectedlyActivated` (-> `DeactivateMonitors`) and `ComputeUnexpectedlyDeactivated` (fix H, round 7 of the ORIGINAL session) -> a **recursive, nested** `ActivateMonitorsCore(..., isNestedCorrectionCall: true)` call that re-enters this entire method (its own retry loop, its own correction loop, its own verify-and-throw).
6. `ShouldRetryScopedActivation` (fix A) -- a bounded (2 extra attempts), zero-then-poll-gated automatic retry of the ENTIRE build+apply+settle+correct sequence for the TOP-LEVEL call only, deliberately excluding any attempt that fell back to Extend.
7. `PollUntilTargetsReachable` (fix K, round 18) -- an active, bounded (~2.85s) wait for target reachability inserted between (6)'s eligibility check and its retry.
8. `ShouldRetryNestedCorrectionActivation` (round 20 item A) -- a SEPARATE eligibility rule, OR'd alongside (6), that extends (6)+(7) to also cover (5)'s nested call specifically.
9. `isNestedCorrectionCall` threaded as a parameter from a new public/private (`ActivateMonitors`/`ActivateMonitorsCore`) method split, consumed by BOTH (8) and by (10).
10. `CollateralMonitorRestoreFailedException` + `MonitorEnableFailureMessageBuilder` (round 20 item B) -- exception-type-based UX branching so a nested-correction failure reads differently from a top-level failure.
11. `ObservePostApplyStability` -- a separate, non-corrective, 6s/250ms background-thread poller (diagnostic only).
12. `PollUntilStableActiveDevicePaths` -- yet another, separate settle-poll used INSIDE the correction loop (distinct from both (7) and (11), each independently hardened against `TargetNotAvailableException` in different rounds).
13. MainForm's passive watchdog: `ArmIntentGuard` / `TryReactivelyCorrectAgainstLastIntent`, driven by OS `SystemEvents.DisplaySettingsChanged`, its OWN separate 8s window / 2-correction budget, bidirectional (fix B/D), now conditionally armed only on a tracked success flag (fix J, and its round-18 toggle-switch equivalent) so a failed action doesn't poison the intent baseline.
14. `ToggleService.LiveFilterMonitorSets` (round 18 item 1) -- filters stale/superseded device paths out of settings-derived sets before they ever reach (2)-(13).
15. `ToggleOrchestrator`'s busy-lease exclusivity (`BeginExclusiveMonitorAccess`/`RunGuarded`) -- a separate cross-call mutual-exclusion mechanism, deliberately NOT unified with (13)'s own guard window.

That is **fifteen** independently-added, independently-triggered mechanisms (not counting the original position-cache/scoped-vs-Extend split itself, which is (1)-(3)) layered around ONE still-completely-unexplained external nondeterminism (`root_cause (8)`), accumulated over 20 rounds across two debug sessions. Several are recursive or cross-wired: (5)'s nested call is itself eligible for (6)/(7)/(8)'s retry-then-poll and can itself throw (10)'s new exception type; (9) is a parameter now load-bearing for two independent decisions (retry eligibility AND exception type) in two different methods. Reconstructing what actually happened in a single rig trial (see round 19's evidence entry) required a multi-paragraph, line-number-anchored trace through 4-5 of these layers at once. That is a genuine, honest measure of how hard this is to reason about -- this investigation's OWN rounds have needed that level of forensic reconstruction to explain their own code's behavior after the fact.

### 21.2 Direct verdict: proportionate, or overbuilt?

**Overbuilt, relative to the stated core value ("a single reliable action" for a single-user, single-rig, 3-monitor utility).** This is not a claim that any INDIVIDUAL fix was wrong -- every one of the 15 mechanisms above was added in response to a specific, rig-confirmed, directly-evidenced defect, following good discipline (falsifiable hypothesis, narrow fix, self-verification, human-verify checkpoint) round after round. The problem is the AGGREGATE shape that emerges from applying that discipline 20 times in a row to the SAME still-unexplained root cause without ever revisiting the higher-level design choice that keeps generating new instances of it:

- **The scoped-activation path was adopted for a cosmetic reason** (avoiding a brief visible flash when reactivating a monitor, `monitor-enable-reactivates-others-again` round 5) and has, in the 10 rounds since (`root_cause (8)` was first named), never become reliable for the SAM7489/Odyssey-G5 pairing specifically. Every fix since round 13 (fix B, fix A, fix K, round 19/20 items A/B) exists ONLY to make that one cosmetic-motivated mechanism tolerable for one specific flaky monitor -- none of them touch, explain, or reduce the actual external nondeterminism. That is a strong signal the mechanism, not the monitor, may be the wrong thing to keep hardening.
- **Three independent correction/retry layers now coexist** ((5)'s nested correction, (6)-(8)'s retry-then-poll, (13)'s passive OS-event watchdog) with overlapping purpose (all ultimately trying to make the final CCD state match the last deliberate intent) but different triggers, different timeouts, and different call-graph positions -- added in three different rounds (7, 14/18, 3/5/9) for three different observed shapes, never designed together, and only partially reconciled (round 20 had to add a fourth, narrower gate specifically because two of the three didn't originally talk to each other).
- **Every fix has addressed a symptom one layer further downstream of the actual trigger.** Round 7 fixed "a lost survivor isn't corrected." Round 9 fixed "a poll exception aborts correction entirely." Rounds 13-15/17/18 fixed "the retry that recovers a stuck target fires with bad timing/no timing at all." Round 19/20 fixed "the nested correction isn't covered by the retry, and its failure message is confusing." None of these fixed, or even claim to explain, WHY SAM7489 needs correcting in the first place -- `root_cause (8)` is exactly as unexplained today as it was at round 7.
- **This round's new evidence (see 21.4) shows the downstream-patching approach has not converged** -- it has produced a FOURTH distinct failure shape (an unlisted third monitor coming active after a scoped call that reported success) that none of rounds 13-20's machinery specifically anticipated, and that most of it (the Extend-fallback-gated retry rules) may not even apply to, since this trial shows no Extend-fallback line at all.

Given CLAUDE.md's own stated core value is a SINGLE reliable action, not a fault-tolerant distributed-systems-style layered defense, 20 rounds producing a call graph this deep for 3 monitors on 1 GPU adapter is disproportionate to the actual problem. This is exactly the accretion pattern the "propose narrowly, get approval, implement narrowly" discipline can produce when never revisited holistically -- each individual round's discipline was sound; the aggregate was never re-examined until now, which is precisely what this round was asked to do.

### 21.3 Why SAM7489/Odyssey G5 specifically, and not ACI24A4/DELA0BC

Directly re-examined every `TryBuildScopedActivationPlan`/`GetAllPaths()` candidate-source log line captured across rounds 11-20 plus this round's DATA block:

- **Same GPU adapter, not a structurally different one.** Every candidate `PathDisplaySource` logged for ALL THREE monitors across every round (this round's DATA block included) shares the identical adapter LUID `{ 1417C - 0 }` -- SAM7489 is not on a different physical GPU/output controller. The only thing that varies is WHICH of the adapter's 5 generic source slots (sourceId 0-4) gets claimed, and that varies round-to-round for SAM7489 (sourceId=2 in round 13, sourceId=1 in round 19 and in this round's DATA), consistent with dynamic, greedy-then-preference-based claiming rather than any fixed hardware source/port-group constraint. **This directly rules out "SAM7489 is wired through a structurally different adapter/port range" as the explanation** -- root_cause (8)'s own "hardware source/port-group constraint" candidate is not supported by the source-identity evidence gathered.
- **Device-path identity churn is NOT unique to SAM7489.** Round 17 found BOTH SAM748A->SAM7489 AND DELA0B8->DELA0BC changed CCD identity at some point in this rig's history (both are stale-but-preserved entries in settings.json). If a physical replug/cable-swap event were the differentiator, it affected two of the three monitors, not one -- this argues AGAINST "SAM7489 alone had its cable touched" as a sufficient explanation, since DELA0BC shares the same identity-change history but has never once, in 20 rounds, been the monitor that fails to activate, throws, or reports transiently unavailable.
- **What genuinely IS unique to SAM7489, repeatedly, across the entire 20-round history:** it is the ONLY one of the three monitors ever observed reporting `TargetNotAvailableException` / "0 candidate PathDisplaySource(s)" / a settle-poll flicker (rounds 9, 10, 13, 15, 17, 19, and now this round's DATA at 23:18:36.672/37.022-829). ACI24A4 and DELA0BC never exhibit this in this file's entire evidence trail (the one ACI24A4 "vanished from enumeration" event, round 6 of the ORIGINAL session, was independently confirmed by the user to be an unrelated manual cable-unplug during that specific trial, not a recurring pattern). Across ~20 rounds of trials this is a striking, consistent, monitor-specific asymmetry -- strong evidence the underlying negotiation/link behavior specific to THIS physical display (not this app's code, and not the shared GPU adapter) is the differentiator.
- **This has never been captured as a port-type/cable-type fact in this investigation** -- no round has recorded whether SAM7489/Odyssey G5 is on DisplayPort vs. HDMI, direct vs. daisy-chained/MST, or anything from EDID. Round 13 explicitly considered and declined EDID capture as speculative; this remains a genuine, flagged evidence gap, not something this round can resolve from existing logs.
- **General knowledge, not verified for this specific unit, but directly on-point and corroborated by multiple independent user reports (web search this round):** Samsung Odyssey G5/G6/G7/G9-series curved gaming monitors have a widely-reported, common DisplayPort "No Signal"/disconnect-reconnect quirk, most often triggered by sleep/wake or display-config-change events, frequently tied to the monitor's own power-saving ("Deep Sleep") firmware behavior on DisplayPort specifically -- commonly worked around by switching that monitor to HDMI, disabling "Deep Sleep"/power-saving in the monitor's own OSD, updating monitor firmware, or disabling Windows "Fast Startup." This is a known CLASS of issue for this monitor family, independent of Rig Toggle's own code, and matches this investigation's own repeatedly-observed symptom shape (transient DP-link unavailability around a display-config change) closely enough to be worth checking directly on the actual hardware -- flagged as an out-of-band, hardware/OS-level experiment the user can run in a few minutes, independent of and prior to any further code change. (Sources below.)

### 21.4 The new evidence: a scoped, array-based call that reportedly succeeded, followed by an unlisted monitor activating

Verified directly against the DATA block: at 23:18:36.097-098, `ActivateMonitors(SAM7489)` builds a SCOPED plan whose array contains only `[ACI24A4, SAM7489]` (2 entries -- DELA0BC is not present anywhere in the plan, not even as an inactive placeholder, confirming it was not active going in and was not part of any exclusion set). At 23:18:36.376 the log states `scoped activation ApplyPathInfos completed without throwing` -- no `PathChangeException`, no Extend-fallback log line anywhere in this excerpt. The settle-poll then shows SAM7489 active on attempt 1/5 (23:18:36.376), two tolerated `TargetNotAvailableException` ticks, then at attempt 4/5 (23:18:37.022) **DELA0BC -- a device path that appeared nowhere in the submitted array -- is active, and SAM7489 is gone.** This is confirmed, not a misreading: it is a fourth, distinct failure shape from the three `root_cause (8)` already catalogued (throw+Extend-drops-a-survivor; success+target-never-settles; and now success+an-entirely-unlisted-third-path-activates).

This DOES undermine the design assumption nearly the entire scoped-activation architecture (mechanisms 2-10 in 21.1) rests on: that a scoped, successfully-applying `ApplyPathInfos` call only ever touches the paths explicitly listed in its array. Investigated directly rather than dismissed as unexplainable driver flakiness, per this round's instruction:

- **A plausible, code-traceable mechanism exists and was not previously considered by this investigation:** the app's own call, `PathInfo.ApplyPathInfos(scopedPlan, allowChanges: true, saveToDatabase: false, forceModeEnumeration: true)`, passes `allowChanges: true` on every single scoped attempt in this file's entire history. `allowChanges` maps to `SetDisplayConfig`'s `SDC_ALLOW_CHANGES` flag, whose documented purpose (Microsoft SDK docs, confirmed via web search this round) is: *"the function can modify the specified source and target mode information in order to create a functional display path set if required... used when the exact configuration you specified isn't possible."* This flag exists specifically so the OS can deviate from the literal request rather than fail outright with `ERROR_BAD_CONFIGURATION`. Available documentation does not spell out the FULL extent of what "create a functional display path set" is licensed to touch when the literal request cannot be satisfied as-is (whether it is strictly confined to mode information for paths already in the array, or can extend to which paths end up active at all) -- this is a genuine, unresolved gap in publicly available documentation, not something this round can close with certainty. But it is a directly plausible, previously-unexamined, code-traceable candidate: this app is the one telling Windows "you're allowed to deviate from what I specified," on every single scoped call, and has never tested what happens with `allowChanges: false` (which would make an unsatisfiable request fail loudly via `SDC_VALIDATE`-style rejection instead of silently substituting something else).
- **An equally plausible alternative, NOT ruled out either:** this could be the SAME still-completely-unexplained external reassertion mechanism as the ORIGINAL session's `root_cause (3)` (the ~1.5s delayed revert toward "whatever the persisted non-rig 2-monitor pair was" -- which on this 3-monitor rig is structurally always `[ACI24A4, DELA0BC]`, exactly what this round's DATA settled on), recurring in a new call shape rather than a mechanistically distinct defect. That mechanism was never explained in the original 8-round session either, and this rig's fixed hardware means "reverts to the [ACI24A4, DELA0BC] pair" is consistent with several different external mechanisms without discriminating between them, exactly as the original session's own round-6 evidence already noted.
- **Honestly unresolved between these two candidates from this excerpt alone** -- both predict the observed outcome. The `allowChanges: false` experiment is the more actionable of the two (a single, narrowly-scoped, reversible flag flip, testable in one rig trial) and would also, as a side effect, help discriminate between them: if `allowChanges: false` either (a) makes this specific anomaly stop recurring, or (b) turns it into an outright, loud `PathChangeException` instead of a silent success-with-side-effect, that is evidence FOR the flag being causally implicated; if the anomaly recurs identically with `allowChanges: false`, that points back toward the external-reassertion explanation instead. **Not implemented this round** -- flagged as a candidate experiment for the decision below, not a fix.
- **This round's elided log tail is a genuine, flagged limitation:** the orchestrator's relay explicitly cut off before this specific attempt's own correction-round 2/3, 3/3, and final outcome (recovered vs. thrown D-05/`CollateralMonitorRestoreFailedException`). Whether `ComputeUnexpectedlyDeactivated`'s nested-correction machinery (and its round-20 extended retry) successfully recovered SAM7489 after this point, or whether this ended in a thrown exception, is unknown from what was provided and is not guessed at here.

### 21.5 Is a genuinely simpler design available?

**Yes, one credible, concrete candidate exists, with an honest tradeoff -- not a free simplification.**

**Re-examined and already true, not a further simplification:** "always re-enumerate live state fresh right before mutation" is already exactly how this code works -- every attempt (including every fix-A/K retry) already calls `GetActivePaths()`/`GetAllPaths()` fresh, never reuses a stale snapshot across a mutation boundary. This does NOT eliminate fix B's source-preference cache (which exists for a different reason: reclaiming the SAME GPU output slot across activations so a monitor doesn't visually jump between source assignments) or fix H's nested-correction complexity (which exists because a single CCD call can have side effects on paths OUTSIDE its own request, which fresh enumeration lets you DETECT after the fact but does not prevent). This candidate is already implemented; there is nothing further to simplify here.

**The one candidate that would materially shrink the design: drop scoped `ApplyPathInfos` as the primary mechanism and make whole-topology `ApplyTopology(Extend)` (or a hardened variant of it) the ONLY activation path, with the existing correction loop (mechanisms 5, 13 in 21.1, unchanged) as the sole safety net.**

What this would eliminate: `TryBuildScopedActivationPlan`, `PromoteToOriginIfNeeded`, the reflection-patch-a-readonly-field technique, `SelectSourceForActivation`/fix B, `ShouldRetryScopedActivation`'s `usedScopedActivation` gate, `PollUntilTargetsReachable`/fix K, and `ShouldRetryNestedCorrectionActivation`/round 20 item A -- roughly ten of this round's fifteen catalogued mechanisms, since all of them exist specifically to make the scoped path (or its retry) work. `CollateralMonitorRestoreFailedException`/`MonitorEnableFailureMessageBuilder` could likely be simplified too, though fix H's nested correction itself (mechanism 5) would remain, since Extend's own already-proven tendency to reactivate an unrelated, independently-disabled monitor (`root_cause (2)`, confirmed on real hardware in the ORIGINAL session's round 3) still needs correcting regardless of which primary mechanism is used.

**What this would honestly cost, not hide:** Extend is not a "known-reliable" mechanism being under-used -- it is a mechanism this investigation's OWN original session directly, rig-confirmed as ALSO unreliable for expressing a specific desired active-path-set (it failed to activate the requested target while reactivating an unrelated, independently-disabled monitor, `root_cause (2)`, round 3 of the original session). Switching to it as primary trades one basket of anomalies (scoped-plan's three-now-four failure shapes, seemingly concentrated on the SAM7489 pairing) for a DIFFERENT, already-proven-real basket (Extend's own opaque, persisted-database-driven topology choice, which can omit the actual request or include monitors the user does not want). It would also restore the original, smaller cosmetic problem scoped activation was invented to avoid (a brief visible flash of an unwanted monitor turning on before the correction loop turns it back off) -- a real, if minor, UX regression, not zero-cost. Whether Extend's failure mode is, in practice, MORE tractable for this specific rig (3 monitors, 1 adapter, one specific flaky display) than scoped-plan's has never actually been tested with the SAME 10 rounds of hardening effort that went into the scoped path -- this is a genuinely open empirical question, not something this round can settle without a rig trial.

**Collapsing the three correction/retry layers (mechanisms 5, 6-8, 13) into one:** investigated honestly, not assumed to be simple. These three operate at genuinely different layers (synchronous, inside the CCD-calling method vs. asynchronous, driven by an OS event at the WinForms layer) for genuinely different reasons (mechanism 5/6-8 exist because the CALLER needs to know synchronously whether its own request ultimately succeeded, before returning; mechanism 13 exists because SOME drift -- the original session's still-unexplained `root_cause (3)` -- only ever manifests seconds after any call has already returned, with no in-call mechanism able to observe it). Pushing all correction into the synchronous path would mean blocking the UI thread far longer waiting out a drift window that has no known fixed duration; pushing all of it out to the passive watchdog would mean a caller's own `ActivateMonitors()` call could return "success" while the requested state is not actually durable yet, which the current design deliberately does not allow (D-03/D-05 discipline). **This is not a safe, obvious collapse -- flagged as a real redesign question, not resolved here.**

**Not fixed by any redesign, in any variant:** `root_cause (8)`'s actual external trigger (why SAM7489 specifically misbehaves) remains unknown regardless of which CCD API mechanism the app uses -- see 21.3's out-of-band hardware/OS-level checks, which are the most likely path to actually eliminating the problem rather than continuing to build code-side defenses against it.

### 21.6 Honest summary

The accumulated complexity is not individually unjustified, but the aggregate is disproportionate to "a single reliable action" for a personal, 3-monitor, single-adapter rig, and it has not converged -- round 20's fixes address the third known failure shape, and this round's own new evidence is a fourth shape most of that machinery does not even reach. A credible, concrete simplification exists (drop scoped-`ApplyPathInfos`-as-primary in favor of hardening `Extend` + the existing correction loop), at the honest cost of restoring Extend's own already-proven unreliability and a minor cosmetic flash. The single most promising next step, cost-wise, may not be more code at all: two concrete, reversible experiments -- (a) flipping `allowChanges` to `false` on the scoped call as a one-line diagnostic, and (b) checking the Odyssey G5's own DisplayPort/Deep-Sleep/firmware settings and trying HDMI instead, per the corroborated, monitor-family-specific issue class found this round -- could each independently narrow or eliminate `root_cause (8)` faster than a 21st round of downstream patching.

Sources for this round's web research: [SDC_ALLOW_CHANGES / SetDisplayConfig flag semantics (Microsoft SDK docs via search)](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setdisplayconfig), [SetDisplayConfig flag-combination reference](https://github.com/MicrosoftDocs/sdk-api/blob/docs/sdk-api-src/content/winuser/nf-winuser-setdisplayconfig.md), [Samsung Odyssey G70NC sleep/DisplayPort issue](https://eu.community.samsung.com/t5/computers-it/odyssey-g70nc-sleep-issue/td-p/13086974), [Samsung Odyssey G70A standby disconnect issue](https://us.community.samsung.com/t5/Monitors-and-Memory/Samsung-Odyssey-G70A-Standby-Constantly-Disconnects-Itself/td-p/2193213), [Samsung G8 DisplayPort-after-sleep issue](https://eu.community.samsung.com/t5/computers-it/samsung-g8-after-sleep-fails-to-find-displayport/td-p/6903586), [Samsung G7 DisplayPort detection issue](https://eu.community.samsung.com/t5/computers-it/g7-monitor-not-detecting-displayport/td-p/7571390).

## CHECKPOINT REACHED (round 21 -- ANSWERED: Option R1 (redesign); SESSION NOW CLOSED, superseded by redesign decision -- see the note at the top of this file and Resolution round 22 addendum at the end of this file)

**Status:** User chose **Option R1** (redesign, not another incremental patch), with an explicit
instruction: do not implement the redesign inline in this debug session -- write a concrete
proposal into this file, close this session out, and route actual implementation through
`/gsd-plan-phase`. This triggered round 22 -- see "## Resolution (round 22 -- concrete redesign
proposal; SESSION CLOSED)" at the end of this file for the full proposal. Original ask preserved
below for historical record only -- do not act on it. **No further checkpoint follows this one in
this file** -- this session is closed; a future planning session picks up from round 22's
proposal, not from a new debug round here.

**Type:** decision
**Debug Session:** .planning/debug/monitor-position-regre.md
**Progress:** 36 evidence entries, 1 eliminated hypothesis, across thirteen investigation/implementation rounds since the regression reopened, plus this round's holistic assessment. No code changed this round -- pure review and decision-framing, per explicit instruction.

### Investigation State

**What this round did:** re-read `WindowsMonitorController.cs`, `MainForm.cs`'s monitor logic, and `ToggleService.cs` fresh, as a whole, set aside round-by-round rationale, and answered your question directly: **yes, this has become overbuilt.** 15 independently-added mechanisms now coexist (position/mode cache, scoped-activation-with-Extend-fallback, source-preference caching, a settle-poll-then-correct loop with a *recursive* nested-correction call, two additional retry/poll layers on top of that, an exception-type-based UX branch, a separate background diagnostic poller, a separate passive OS-event watchdog with its own budget, and a stale-settings live-filter) -- all defending against ONE still-completely-unexplained external nondeterminism tied to one specific monitor (SAM7489/Odyssey G5). None of the last 10 rounds' fixes have explained or reduced that root cause; each addressed a new downstream symptom. This round's own new evidence is a FOURTH distinct failure shape.

**New finding this round:** the new log excerpt shows a *scoped*, 2-entry `ApplyPathInfos` call reporting success while an entirely unlisted THIRD monitor (DELA0BC) ends up active -- undermining the "scoped calls only touch what's listed" assumption most of the accumulated machinery relies on. Two honestly-unresolved candidate explanations: (a) this app's own `allowChanges: true` flag on every scoped call, whose documented purpose is letting Windows deviate from the literal request when it can't otherwise be satisfied -- untested with `allowChanges: false`; (b) the same still-unexplained external reassertion mechanism from the ORIGINAL session's never-root-caused `root_cause (3)`, recurring in a new shape.

**Why SAM7489 specifically:** same GPU adapter as the other two monitors (ruled out "different adapter/port range" directly from source-identity logs) -- but it is the ONLY monitor, across the entire 20-round history, ever observed transiently unavailable/flapping. Device-path identity churn (SAM748A->SAM7489) is NOT unique to it either (DELA0BC has the identical history and is never flaky). General knowledge (web search this round, not verified for your specific unit): Samsung Odyssey G5/G6/G7/G9 monitors have a widely-reported DisplayPort "No Signal"/reconnect quirk around sleep/display-config changes, commonly fixed by switching to HDMI, disabling "Deep Sleep" in the monitor's own OSD, updating monitor firmware, or disabling Windows Fast Startup -- worth checking directly on your hardware.

### Checkpoint Details

**Decision needed:** how do you want to proceed?

**Options:**
- **Option R1 (redesign):** Pause further incremental patching. Replace scoped-`ApplyPathInfos`-as-primary with whole-topology `ApplyTopology(Extend)` as the ONLY activation mechanism, keeping the existing correction loop (fix H, unchanged) as the sole safety net. This would remove roughly ten of the fifteen accumulated mechanisms (the scoped-plan builder, origin/source-preference logic, and both retry/poll layers built to prop it up). Honest cost: Extend has its own already-proven unreliability (it can omit the requested target or reactivate an unwanted one -- confirmed on real hardware in the original session), and the small visible "flash" scoped activation was originally built to avoid would return. This is a real redesign, not a small patch -- it would need its own plan/implementation round, not an inline fix.
- **Option R2 (out-of-band experiments first, no code change):** Before touching any more code, (a) rebuild with `allowChanges: false` on the scoped `ApplyPathInfos` call as a single, reversible diagnostic to test the SDC_ALLOW_CHANGES hypothesis from 21.4, and (b) check the Odyssey G5's own DisplayPort/power-saving/firmware settings and try it on HDMI for a while, per 21.3. Either could shrink or eliminate root_cause (8) without any further app complexity. Cheapest option to try next.
- **Option R3 (continue incremental patching):** Keep the current architecture and continue round-by-round narrow fixes for whichever new failure shape appears next (this round's 4th shape included) -- accept the accumulated complexity as the cost of chasing a genuinely stubborn external issue.
- **Option R4 (accept and stop):** Treat root_cause (8) as a known, accepted limitation of this specific monitor on this rig (same closure pattern the original session used for its own unexplained ~1.5s revert), stop adding correction layers, and rely on the existing (already-extensive) correction/retry machinery as-is, addressing only genuinely NEW, distinct defects (not new shapes of the same root cause) going forward.

**Tell me:** your choice (R1/R2/R3/R4, or a combination -- e.g. R2 first, then decide between R1/R3/R4 based on what those experiments show). No code will be changed until you respond.

## Resolution (round 22 -- concrete redesign proposal; SESSION CLOSED, superseded by the user's Option R1 decision; NO code changed this round)

**Type of round:** proposal-authoring + session closure, explicitly NOT an implementation round
(orchestrator instruction, relaying the user's own Option R1 decision: write the proposal into
this file and close the session; route actual implementation through `/gsd-plan-phase`). This
round crystallizes round 21's own assessment (21.1-21.6) into an itemized, actionable proposal by
re-reading `WindowsMonitorController.cs` (full, 1945 lines), `WindowsMonitorControllerTests.cs`
(full, 676 lines, 44 `[Fact]` tests), `IMonitorController.cs`, and `MainForm.cs`'s
`MonitorEnableFailureMessageBuilder`/`ArmIntentGuard` call sites directly this round -- it is not
new investigation and does not add, remove, or revise any hypothesis about `root_cause (8)`
itself. **No code was changed.**

### 22.1 Target architecture

`ApplyTopology(Extend)` becomes the SOLE activation mechanism -- no scoped-plan branch, no
primary/fallback split at all. `ActivateMonitorsCore` collapses from its current multi-attempt,
two-branch shape to a single-pass shape:

```
private void ActivateMonitorsCore(devicePaths, swapDisableSet, isNestedCorrectionCall)
{
    ENTER log
    if devicePaths empty -> EXIT no-op                         // unchanged
    currentlyActive = QueryActiveDevicePaths()
    if every requested path already active -> EXIT no-op        // unchanged (Pitfall 3 skip)
    missing-availability guard -> throw if any requested path undetected   // unchanged

    activePaths = GetActivePaths()
    CacheLiveModes(activePaths)                                 // mechanism 1, KEPT, unconditional

    ApplyTopology(Extend, allowPersistence: false)               // the ONLY activation call now --
                                                                  // unconditional, not gated on a
                                                                  // scoped-plan outcome (there is no
                                                                  // scoped plan to gate on)

    // settle-poll-then-correct loop (mechanism 5, KEPT byte-for-byte unchanged):
    consecutiveCleanRounds = 0
    for round in 1..MaxCorrectionRounds:
        settled = PollUntilStableActiveDevicePaths()             // mechanism 12, KEPT
        unexpectedlyActivated   = ComputeUnexpectedlyActivated(...)
        unexpectedlyDeactivated = ComputeUnexpectedlyDeactivated(...)
        if either non-empty:
            consecutiveCleanRounds = 0
            if unexpectedlyActivated non-empty:   DeactivateMonitors(unexpectedlyActivated)
            if unexpectedlyDeactivated non-empty: ActivateMonitorsCore(unexpectedlyDeactivated,
                                                       swapDisableSet: {}, isNestedCorrectionCall: true)
        else:
            consecutiveCleanRounds++
            break if consecutiveCleanRounds >= RequiredConsecutiveCleanRounds

    // verify-and-throw, SINGLE pass, no outer retry loop:
    postCorrection = GetActivePaths()
    stillInactive = (requested paths not active) + (pre-call survivors, not in swapDisableSet, not active)
    if stillInactive empty:
        EXIT success; ObservePostApplyStability(...)             // mechanism 11, KEPT
        return
    throw isNestedCorrectionCall
        ? CollateralMonitorRestoreFailedException(...)            // mechanism 10, KEPT
        : InvalidOperationException(...)
}
```

Concretely REMOVED from this method's body, not merely bypassed: the `for (attemptNumber = 1;
... MaxScopedActivationRetryAttempts + 1; ...)` outer retry loop itself (round 14's fix-A
wrapper) -- once the scoped-plan branch is gone, nothing inside this method can ever make that
loop retry (see 22.3), so the loop is dead weight, not a safety margin, and is deleted along with
the mechanisms that populated it; the entire `if (TryBuildScopedActivationPlan(...)) {...} else
{...}` branch and its `usedScopedActivation` bookkeeping; the `if (isPartOfMonitorSwap) { Log(...)
}` informational branch (vestigial once there is no scoped-array-exclusion step for it to precede
-- see 22.4's open scoping note on `monitorSwapDisableSet`); the `retryEligibleTopLevel`/
`retryEligibleNestedOnly` checks and the `PollUntilTargetsReachable` call between them and the
(now-deleted) retry `continue`. `TryBuildScopedActivationPlan`, `PromoteToOriginIfNeeded`,
`SelectSourceForActivation`, `ResolveIsPathActiveBackingField`/the reflection-patch technique,
`DescribeSource`, and `DescribeScopedPathEntry` are deleted as standalone methods (their only
caller is the deleted branch). `ShouldRetryScopedActivation`, `ShouldRetryNestedCorrectionActivation`,
and `PollUntilTargetsReachable` are deleted as standalone methods for the same reason. The public
`ActivateMonitors`/private `ActivateMonitorsCore` split (added round 20 solely to thread
`isNestedCorrectionCall`) is KEPT -- that parameter is still load-bearing for the terminal
exception-type branch (mechanism 10). `IMonitorController.ActivateMonitors`'s public signature
(`monitorDevicePaths`, `monitorSwapDisableSet`) is UNCHANGED -- `monitorSwapDisableSet` continues
to be consumed, unchanged, by `ComputeUnexpectedlyDeactivated`'s own exclusion filter inside the
retained correction loop (see 22.4 for the one open question about its *other*, no-longer-relevant
use inside the deleted scoped-plan array-construction step).

### 22.2 Explicit mechanism disposition (round 21.1's 1-15 inventory, itemized precisely)

Round 21.5 estimated "roughly ten of fifteen" removed, explicitly a rough estimate. Itemized
precisely this round: **6 REMOVED, 1 MERGED/SIMPLIFIED, 8 KEPT unchanged** (15 total).

| # | Mechanism (round 21.1's own numbering/wording) | Disposition | Reason |
|---|---|---|---|
| 1 | `_lastKnownActiveModeByDevicePath` position/mode cache (`CacheLiveModes`) | **KEPT, unchanged** | Not scoped-plan-specific. Fix H's retained correction loop (mechanism 5) needs a live position cache to restore ANY accidentally-dropped survivor regardless of whether Extend or a scoped plan dropped it -- needed exactly as much (arguably more, since Extend drops survivors more unpredictably than a scoped plan ever did) once Extend is the sole mechanism. |
| 2 | Scoped activation (`TryBuildScopedActivationPlan` + `ApplyPathInfos`) as PRIMARY, `ApplyTopology(Extend)` as FALLBACK | **REMOVED** | This is the redesign's core action. The scoped-primary/Extend-fallback split, and the primary/fallback branch itself, cease to exist -- Extend is unconditional. |
| 3 | Present-but-inactive array representation + `PromoteToOriginIfNeeded` | **REMOVED** | Exists solely to satisfy `SetDisplayConfig`'s explicit-array contract for a SCOPED call. `ApplyTopology(Extend)` passes no path/mode array at all (`SetDisplayConfig(0, null, 0, null, ...)`, confirmed by decompile, cited in this file's own class remarks) -- there is no array left for this helper to normalize origin/primary within. |
| 4 | `SelectSourceForActivation` (fix B) | **REMOVED** | A per-target `PathDisplaySource`-claiming preference INSIDE the scoped plan's candidate-selection loop. Extend has no per-target source-claiming step -- Windows picks the entire topology, including source assignments, itself -- so there is no candidate list left for this function to choose from. |
| 5 | Settle-poll-then-correct loop (`MaxCorrectionRounds`, `RequiredConsecutiveCleanRounds`, `ComputeUnexpectedlyActivated`->`DeactivateMonitors`, `ComputeUnexpectedlyDeactivated`->nested `ActivateMonitorsCore`) | **KEPT, unchanged** | The redesign's explicit sole safety net (round 21.5's own conclusion). Confirmed via direct source read this round (`WindowsMonitorController.cs` ~lines 707-821): this loop ALREADY runs unconditionally after either branch today -- it is not gated on `usedScopedActivation` -- so removing the scoped branch requires zero change to this loop's own internal logic. See 22.3 for the frequency-of-firing consequence (not a logic gap). |
| 6 | `ShouldRetryScopedActivation` (fix A) | **REMOVED** | Its own condition (a) is `usedScopedActivation == true`. With mechanism 2 gone, `usedScopedActivation` is always false -- this predicate becomes permanently, unconditionally `false`. A permanently-dead gate is not "kept unchanged," it is dead code; deleted along with the retry loop it gated. |
| 7 | `PollUntilTargetsReachable` (fix K) | **REMOVED** | Exists purely to gate the TIMING of fix A's retry (mechanism 6). With mechanism 6 gone, nothing calls this. |
| 8 | `ShouldRetryNestedCorrectionActivation` (round 20 item A) | **REMOVED** | Extends mechanisms 6/7's retry specifically to the nested correction call. With 6 and 7 gone, there is no retry loop left to extend into. |
| 9 | `isNestedCorrectionCall` parameter + public/private method split | **MERGED/SIMPLIFIED** (narrowed, not removed) | Originally fed TWO decisions: mechanism 8's retry eligibility (removed) and mechanism 10's exception-type UX branch (kept). With 8 gone, this parameter now feeds only mechanism 10's decision -- the parameter itself, the method split, and the nested call site's `isNestedCorrectionCall: true` argument all remain in the code, unchanged in mechanics, just single-purpose now instead of dual-purpose. |
| 10 | `CollateralMonitorRestoreFailedException` + `MonitorEnableFailureMessageBuilder` (round 20 item B) | **KEPT, unchanged** | Round 21.5 flagged this as "could likely be simplified too" but did not commit to removing it, and, confirmed via direct read of `MainForm.cs`'s `MonitorEnableFailureMessageBuilder.Build(devicePath, ex)` call site this round: this mechanism is orthogonal to WHICH primary activation mechanism is used -- it only distinguishes, in the terminal D-05 throw, whether the exhausted correction belonged to fix H's nested cleanup call (mechanism 5, itself kept) or the top-level user request, for message clarity. That distinction is exactly as relevant post-redesign as before it, since mechanism 5's nested call still exists and can still exhaust its correction budget. Removing this would reintroduce the confusing-error-message regression round 20 specifically fixed, for no redesign-related reason. This is a judgment call that sharpens round 21.5's tentative language into a decision -- flagged explicitly as such. |
| 11 | `ObservePostApplyStability` (background diagnostic poller) | **KEPT, unchanged** | Pure evidence-gathering for the still-unexplained Symptom-2-part-B/`root_cause (3)` delayed revert -- orthogonal to which primary activation mechanism is used. Not named by round 21.5 among mechanisms removal would touch. |
| 12 | `PollUntilStableActiveDevicePaths` (settle-poll inside the correction loop) | **KEPT, unchanged** | This IS the settle-poll half of mechanism 5, the redesign's own retained safety net. |
| 13 | MainForm's `ArmIntentGuard`/`TryReactivelyCorrectAgainstLastIntent` passive watchdog | **KEPT, unchanged** | Explicitly named by round 21.5 as the second of the two mechanisms ("mechanisms 5, 13 ... unchanged") retained as the sole safety net; defends against a DIFFERENT (OS-level, seconds-later) drift than anything the scoped-vs-Extend choice touches. |
| 14 | `ToggleService.LiveFilterMonitorSets` (round 18 item 1) | **KEPT, unchanged** | Filters stale/superseded device paths out of settings-derived sets BEFORE they reach any activation mechanism -- orthogonal to which CCD call activates them. |
| 15 | `ToggleOrchestrator`'s busy-lease exclusivity (`BeginExclusiveMonitorAccess`/`RunGuarded`) | **KEPT, unchanged** | Cross-call mutual exclusion, unrelated to which CCD call `ActivateMonitors` makes internally. |

Two clarifications carried forward explicitly, not re-decided this round:

- **Round 21.5's "don't collapse the three correction/retry layers" conclusion stands, unchanged,
  and this proposal does not violate it.** That conclusion was about whether to MERGE mechanisms
  5, 6-8, and 13 into one unified correction mechanism -- round 21.5 said no, they operate at
  genuinely different layers (synchronous in-call vs. asynchronous OS-event-driven) for genuinely
  different reasons, and should not be merged. This proposal does not merge them: mechanisms 5 and
  13 both remain fully separate, exactly as they are today. Mechanisms 6-8 are not being "folded
  into" 5 or 13 -- they are deleted outright, because the thing they existed to retry (mechanism 2)
  no longer exists. Deleting a mechanism whose sole purpose disappears is a different action from
  collapsing two surviving mechanisms together, and does not reopen or contradict 21.5's question.
- **The reflection-patch-a-readonly-backing-field technique** (`ResolveIsPathActiveBackingField`,
  used to force `IsPathActive` true/false on a reused `PathTargetInfo`) is not separately numbered
  in round 21.1's 1-15 list -- it is a technique used exclusively inside mechanisms 2-3's array
  construction. It has no other call site in this file (confirmed by direct read) and is deleted
  alongside mechanisms 2-4, not counted separately in the 6/1/8 tally above.

### 22.3 Honest risk/migration note

**Root_cause (2)'s two confirmed Extend failure modes, restated concretely** (from the ORIGINAL
resolved session, `.planning/debug/resolved/monitor-position-resets-to-de.md`, Resolution
`root_cause (2)`, its own round 3): *"Extend simultaneously failed to activate the requested
target AND reactivated an unrelated, independently-disabled one"* -- confirmed on real rig
hardware, not theoretical.

**How the retained correction loop (mechanism 5) covers each, checked directly against source, not
assumed:**

- **"Reactivated an unrelated, independently-disabled monitor"** -- actively CORRECTED, not just
  detected: `ComputeUnexpectedlyActivated` flags it, `DeactivateMonitors` turns it back off, inside
  the same `ActivateMonitors` call, before it returns. This exact code path is not new to this
  redesign -- it is the ORIGINAL correction mechanism, built in rounds 1-4 of the
  `monitor-enable-affects-other`/`monitor-enable-reactivates-others-again` sessions, specifically
  because Extend was the ONLY activation mechanism at that time (scoped activation did not exist
  yet -- it was introduced later, see below). Reverting to Extend-as-sole-primary does not ask this
  mechanism to do anything qualitatively new; it returns the mechanism to the exact duty it was
  originally built and rig-proven for.
- **"Failed to activate the requested target"** -- detected and surfaced loudly (`stillInactive`
  check -> throw, D-05 discipline), never silently reported as success. This is NOT actively
  recovered by mechanism 5 today, and was never actively recovered by it even in the original
  rounds-1-4 Extend-only era -- it throws. **Checked directly, not assumed:** does removing fix A
  (mechanism 6, the automatic retry) reduce recoverability for this specific failure mode?
  **No.** `ShouldRetryScopedActivation`'s own condition (a) is `usedScopedActivation == true` --
  meaning fix A's retry was ALREADY, by construction, never available whenever a call fell back to
  Extend (confirmed by direct read: `usedScopedActivation=false` on any Extend-fallback attempt,
  which makes `ShouldRetryScopedActivation` return `false` unconditionally for it, both before and
  after this redesign). Fix A only ever retried the DIFFERENT shape where a scoped plan itself
  reported success but its own requested target never settled -- a shape that cannot exist once
  scoped activation is removed, since there is no scoped success to fail to settle after. So this
  redesign loses zero automatic-retry coverage for "Extend fails to activate the requested target"
  specifically -- that shape already only got "throw loudly, no retry" treatment, unchanged by this
  proposal.
- **What DOES change, honestly:** frequency, not logic. Because Extend becomes the sole path for
  every activation (previously a fallback only for the fraction of calls where a scoped plan threw
  or could not be built), the correction loop's corrective work -- and therefore the cosmetic flash
  below -- now fires on every call where Extend's opaque topology choice doesn't already match what
  was wanted, not just on the subset of calls that used to fall back.

**Cosmetic-flash regression, restated as a known, accepted tradeoff, not a surprise:** the visible
flicker of an unwanted monitor turning on (then the correction loop turning it back off a beat
later) is exactly the defect scoped activation was built to eliminate. Precisely sourced this
round (not assumed): the class-level remarks in `WindowsMonitorController.cs` and round 21.2 both
attribute this to the **`monitor-enable-reactivates-others-again` session, round 5** (a different,
earlier-resolved debug session from the one root_cause (2)/(8) came from) -- *"the scoped-activation
path was adopted for a cosmetic reason (avoiding a brief visible flash when reactivating a
monitor)."* Reverting to Extend-as-primary restores that flash as the COMMON case (every activation
that needs correction), not a rare one, for as long as `root_cause (8)`'s underlying trigger goes
unaddressed. This is the direct, accepted cost of this decision, not a risk discovered later.

**Not fixed by this redesign, in any variant** (carried forward from 21.5/21.6, not re-decided):
`root_cause (8)`'s actual external trigger -- why SAM7489 specifically misbehaves -- remains
unknown regardless of which CCD API mechanism is primary. The out-of-band hardware/OS-level checks
from round 21.3 (Odyssey G5 DisplayPort/Deep-Sleep/firmware behavior, HDMI as a workaround) remain
the most likely path to actually eliminating the underlying problem, independent of this redesign.

### 22.4 Migration/implementation scope note (NOT an implementation plan)

**Primary file:** `src/RigToggle.Windows/WindowsMonitorController.cs` (1945 lines currently).
Methods/fields removed entirely: `TryBuildScopedActivationPlan`, `PromoteToOriginIfNeeded`,
`SelectSourceForActivation`, `ResolveIsPathActiveBackingField` (+ its
`_isPathActiveBackingFieldCache` field), `DescribeSource`, `DescribeScopedPathEntry`,
`ShouldRetryScopedActivation`, `ShouldRetryNestedCorrectionActivation`, `PollUntilTargetsReachable`
(+ its `MaxReachabilityPollAttempts` constant), and the `MaxScopedActivationRetryAttempts`
constant + its enclosing outer retry `for` loop inside `ActivateMonitorsCore`. `ActivateMonitorsCore`
itself shrinks from its current ~250-line body to roughly the single-pass shape sketched in 22.1.
The extensive class-level `<summary>` doc comment (currently documenting ~20 rounds of scoped-
activation history, lines 1-300) would need a fresh top-level summary reflecting the redesign,
while preserving (not deleting) the historical narrative -- this file's own established convention
is to APPEND new remarks rather than delete old ones, and a future planning session should decide
whether that convention extends to a redesign-scale rewrite or whether the pre-redesign history
belongs in an archival note instead (an open question for `/gsd-plan-phase`, not resolved here).

**Test file:** `src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs` (676 lines, 44
`[Fact]` tests currently). Counted precisely this round by mapping each test to the mechanism it
covers: **23 of 44 tests (~52%) would be removed** alongside their corresponding deleted helpers --
`PromoteToOriginIfNeeded_*` (6 tests), `DescribeSource_*` (2 tests), `SelectSourceForActivation_*`
(5 tests), `ShouldRetryScopedActivation_*` (5 tests), `ShouldRetryNestedCorrectionActivation_*` (5
tests) -- plus the private `Source(uint)`/`WithMode(...)`/`NoMode(...)` fixture helpers (lines
377-383), which have no remaining caller once all three of those test groups are gone. **21 of 44
tests are unaffected and would need zero changes:** `AnyRectanglesOverlap_*` (3),
`MergeAllMonitors_*` (5), `ComputeUnexpectedlyActivated_*` (4), `ComputeUnexpectedlyDeactivated_*`
(5), `ComputeUndetectedDevicePaths_*` (4) -- all cover mechanisms 5, 12, 15(DeactivateMonitors), or
enumeration/merge logic untouched by this redesign. No NEW pure-helper seams are obviously
introduced by removing code (the redesign is a subtraction, not an addition), so no new test
authoring is anticipated beyond whatever a future planning session decides for
integration/regression coverage of the simplified `ActivateMonitorsCore` shape itself (still not
unit-testable in isolation today, for the same "live CCD hardware, no injectable seam" reason
documented at this test file's own top-of-file remarks -- unchanged by this redesign).

**Other files touched, if at all:** `src/RigToggle.Core/Abstractions/IMonitorController.cs` --
public signature unchanged (22.1); only its doc comment's description of `monitorSwapDisableSet`'s
mechanism (currently describing the scoped-plan exclusion behavior in detail, lines 35-59) would
need updating to describe the post-redesign behavior. `src/RigToggle.Core/CollateralMonitorRestoreFailedException.cs`,
`src/RigToggle.Core/MonitorEnableFailureMessageBuilder.cs`, and their call site in
`src/RigToggle.App/MainForm.cs` (`MessageBox.Show(this, MonitorEnableFailureMessageBuilder.Build(devicePath, ex), ...)`)
are UNCHANGED (mechanism 10, kept).

**One genuine open scoping question, not resolved this round:** with the scoped-plan's own
array-exclusion step gone (mechanism 3), `monitorSwapDisableSet`'s only REMAINING consumer inside
`ActivateMonitorsCore` is `ComputeUnexpectedlyDeactivated`'s exclusion filter (unaffected, kept).
Under the original rounds-1-4 Extend-only design (which this redesign returns to), the swap's
disable-set monitors are expected to still be ACTIVE immediately after `ActivateMonitors` returns
(Extend restores the persisted layout, which typically still includes them, since `DeactivateMonitors`
for them has not run yet) -- `ToggleService`'s existing "ActivateMonitors must run BEFORE
DeactivateMonitors on rig-mode entry" ordering contract (already documented in this file's own
class remarks, Pitfall 2) is what actually removes them, in a SEPARATE call, immediately after.
Whether this ordering contract, byte-for-byte as it exists today, is sufficient on its own for the
swap case post-redesign, or needs its own explicit rig-test pass, is exactly the kind of question a
`/gsd-plan-phase` planning round should scope and verify -- flagged here, not decided.

### 22.5 Closure

This section, combined with round 21 (21.1-21.6) and the "SESSION CLOSED" note at the top of this
file, is the complete handoff artifact for this debug session. No further debug rounds are
anticipated in this file. A future `/gsd-plan-phase` session should read this section (22.1-22.4)
plus round 21 in full before scoping implementation work.
