# Pitfalls Research

**Domain:** Adding optional/nullable hardware targets, replacing snapshot-restore with explicit dual-mode monitor configuration, and a concurrent manual-toggle entry point to an existing shipped Windows CCD/COM-interop rig-control WinForms app
**Researched:** 2026-08-04
**Confidence:** HIGH for pitfalls grounded in direct reads of this project's own source (`ToggleService.cs`, `ToggleOrchestrator.cs`, `WindowsMonitorController.cs`, `JsonSettingsStore.cs`, `AppSettings.cs`) and its own documented bug history in `PROJECT.md`; MEDIUM for exe-size-lever interaction risks (no first-party report of this exact combination breaking COM/P-Invoke, reasoned from documented trimming risk plus this project's own "verify, don't assume" track record)

**Scope note:** This is a *subsequent-milestone* pitfalls pass for v2.0 (Configurable Monitors, Optional Targets & Cleanup). It supersedes the v1.2-era `PITFALLS.md` content (dark-mode theming gaps, tray/taskbar icon contrast) — that research is preserved in git history and its conclusions were already validated and shipped in v1.2. This pass is deliberately narrow and codebase-specific: every pitfall below is grounded either in a direct read of the current `ToggleService`/`ToggleOrchestrator`/`WindowsMonitorController`/`JsonSettingsStore` source, or in a bug class this project has *already hit once* (documented in `PROJECT.md`'s Key Decisions / Context section) and is at real risk of reintroducing during this specific redesign. Generic Windows-development advice is intentionally omitted.

## Critical Pitfalls

### Pitfall 1: Two independent concurrency guards instead of one shared one — the toggle/manual-panel race

**What goes wrong:**
`ManualMonitorService` is built with its own private busy-flag (or no guard at all), reasoning "the panel is a separate feature, it needs its own concurrency story." A toggle in flight (via `ToggleOrchestrator`) and a manual panel action can then both observe "the other side is idle" at the same instant and proceed concurrently against the same CCD display topology — a genuinely inconsistent topology (e.g. a monitor left partially reconfigured, or `ActivateMonitors`/`DeactivateMonitors` racing each other's `PathInfo.GetActivePaths()` read-then-`ApplyPathInfos()`-write sequence) that neither side's own verify-and-throw logic was designed to detect, because verify-and-throw only checks *its own* mutation's outcome, not whether something else mutated the topology in between.

**Why it happens:**
`ToggleOrchestrator`'s existing `Interlocked.CompareExchange`-based busy-flag (`ToggleOrchestrator.cs` lines 39, 58) is currently private, internal state — nothing about its shape signals "this needs to be shared with a second, not-yet-written caller." A developer adding `ManualMonitorService` in isolation, without first reading `ToggleOrchestrator.cs`'s own doc comments (D-01/D-02, which already state the flag is deliberately *one shared flag guarding both toggle directions*, not independent per-direction flags — the exact same reasoning extends to the panel), will naturally reach for a second, independent flag because that's the path of least resistance for "make this one new class thread-safe."

**How to avoid:**
Extract the existing `Interlocked.CompareExchange` busy-flag out of `ToggleOrchestrator` into a standalone `SingleFlightGuard` class in `RigToggle.Core`, constructed exactly once in `Program.cs`, and inject the *same instance* into both `ToggleOrchestrator` and `ManualMonitorService`. This preserves `ToggleOrchestrator`'s existing D-01 (non-blocking, immediate rejection, never queued) and D-02 (one shared flag) semantics verbatim — verify this refactor didn't change behavior by confirming the 4 existing `ToggleOrchestratorTests` reentrancy tests still pass unchanged against the refactored version before writing any new panel-specific tests.

**Warning signs:**
`ManualMonitorService` has its own `private int _busy` field, or `SingleFlightGuard` is constructed more than once anywhere in `Program.cs`/tests (two instances defeats the entire point — grep for `new SingleFlightGuard(` and confirm exactly one call site in production wiring).

**Phase to address:**
The phase that builds the manual monitor panel (Feature 4) — but the extraction itself should happen as part of whichever phase already touches `ToggleOrchestrator` for the mode-tracking redesign (Feature 3), since re-touching the same class twice for two related refactors is wasted churn.

---

### Pitfall 2: "At least one monitor stays enabled" validated per-mode-set at Settings-save time, not against the actual resulting topology at apply time

**What goes wrong:**
A user independently configures Rig mode's disable-set to cover every currently-connected monitor (reasonable — Rig mode is *supposed* to disable the desk monitor) and, separately, configures Normal mode's disable-set to also cover every monitor (a mistake, or a monitor that got unplugged/renamed between the two edits) — or empties Normal mode's enable-set while its disable-set is non-empty. `SettingsForm`'s current validation only ever reasoned about *one* grid's checkbox state against *currently-active* monitors at edit time; it has no way to reason about "what will the resulting live topology be when this specific set is actually applied," because that depends on which monitors happen to be active *at toggle time*, not at Settings-edit time. Two independently-edited, individually-plausible-looking grids can combine into a config that — when actually applied via either the toggle or the new manual panel — leaves zero monitors active.

**Why it happens:**
Rig-mode and Normal-mode monitor sets are edited as two separate, symmetric UI sections (per ARCHITECTURE.md's recommendation) with no cross-validation between them, because there's no way to statically know "will these two independently-configured sets ever combine dangerously" without re-deriving live topology math that only the actual `IMonitorController.DeactivateMonitors` call can correctly answer (it already has to read `PathInfo.GetActivePaths()` at the moment of the call to know the true survivor set — a Settings-time check would just be guessing at the same computation with stale data).

**How to avoid:**
Do not attempt to build a second, Settings-time version of the "will this leave zero monitors" check — the existing apply-time guard in `WindowsMonitorController.DeactivateMonitors` (the `survivors.Length == 0` check, line ~295-309) already does this correctly, using live topology, for every call site that routes through it. The job for v2.0 is purely to make sure *all three* mutating entry points (Rig-mode toggle, Normal-mode toggle, manual panel) route through this exact method with no bypass — never to duplicate the check. Treat any Settings-time warning as advisory UX polish only (e.g., "this Normal-mode configuration disables every currently-connected monitor" as a non-blocking hint), never as the actual safety mechanism.

**Warning signs:**
Any code path that computes "would this leave zero monitors enabled" using `AppSettings` data alone (without calling `IMonitorController`/reading live CCD paths) — that's a second, parallel implementation of the guard, which is exactly the drift risk the quality gate for this milestone explicitly warns about ("ideally from one shared validation point, not duplicated logic").

**Phase to address:**
The phase that rewrites `ToggleService.ToggleToNormalMode`'s Monitor step (Feature 3) — confirm at that point that the rewritten Normal-mode path calls the *same* `ActivateMonitors`/`DeactivateMonitors` methods, not a new bespoke apply routine. Re-verify again in the manual-panel phase (Feature 4) for the same reason.

---

### Pitfall 3: Silently skipping a step when a target is unconfigured masks a genuinely different failure state — configured-but-broken

**What goes wrong:**
Once `CompanionAppPath`/`RigAudioDeviceId`/`NormalAudioDeviceId` become optional, a naive implementation treats "field is null or empty" and "field is set but the underlying resource is gone" (moved/uninstalled `.exe`, unplugged audio device) identically — both reduce to "the toggle step didn't run." This is a real regression: today, `ToggleService.ToggleToRigMode`'s preflight (`File.Exists(settings.CompanionAppPath)`, line 70) unconditionally throws a clear, specific "could not be found" error when the configured app is missing — that failure signal must not silently degrade into an indistinguishable-from-intentional "skipped, not configured" outcome once the field's *absence* becomes a valid, non-error state.

**Why it happens:**
The most mechanically simple way to "make a required field optional" is to change every `!string.IsNullOrEmpty(x)`-guarded required-check into a `string.IsNullOrEmpty(x) ? skip : run` branch — which is correct for the null/empty case, but if the same code path is also relied on (even accidentally) to catch the "configured but broken" case, that distinction gets flattened. Concretely: `IsFullyConfigured` (ToggleService.cs:201-205) currently `&&`s all four fields together as hard requirements; naively deleting three of those four terms removes the *validation-gate* function correctly, but the *separate* `File.Exists` preflight check (line 70) that currently only fires because the field was already guaranteed non-empty by `IsFullyConfigured` must be explicitly re-derived to mean "only check existence, and only fail hard, when the field is actually set" — not accidentally deleted alongside the validation-gate relaxation because it "looks like the same kind of check."

**How to avoid:**
Treat "unconfigured" (null/empty) and "configured but currently invalid" as two structurally distinct states in every optional-target code path, not two branches of the same simplification pass:
- **Unconfigured** → skip the step entirely, record `ToggleStepOutcome.NotAttempted` with no reason (matches the existing convention already used for the App step, ToggleService.cs:109, 142, 366).
- **Configured but broken** (path doesn't exist, device ID no longer resolves) → this must still surface as `Failed` with a real message, exactly as it does today, not silently downgraded. `SettingsForm` already has device-availability warning UI (`lblAudioNormalWarning`/`lblAudioRigWarning`) for the config-time version of this same distinction — the toggle-time behavior should stay consistent with it.

Write this as an explicit test case per optional field: one test asserting `NotAttempted` for null, a separate test asserting `Failed` for "set but invalid" — a PR that only adds the first kind of test for a newly-optional field should be treated as incomplete.

**Warning signs:**
A code review or test suite that only exercises the "field is null" path for a newly-optional target and never exercises "field is set to a value that no longer resolves" — the second case is easy to forget precisely because making the feature "not required" naturally centers attention on the empty/null case.

**Phase to address:**
The phase that relaxes `IsFullyConfigured`/adds optional-skip branching for App/Audio targets (Feature 1/2) — this is a correctness requirement for that phase's own acceptance criteria, not a follow-up concern.

---

### Pitfall 4: Removing the snapshot dependency breaks `IsInRigMode()` without anyone touching `IsInRigMode()` itself

**What goes wrong:**
`ToggleService.IsInRigMode()` (line 380) is exactly `_snapshotStore.Exists()` — mode is *entirely* derived from whether a snapshot file happens to be on disk (documented inline as D-14). Every mode-dependent decision in the app — `MainForm.RefreshUi()`'s label/tray icon/tooltip (`MainForm.cs` line 257 onward), the tray/hotkey toggle handlers' branch between `ToggleToRigMode()`/`ToggleToNormalMode()`, and the toggle-back guard MainForm uses before calling `ToggleToNormalMode()` — reads through this one boolean. If `ToggleToNormalMode`'s monitor step is rewritten to apply an explicit target set (Feature 3) *without also* replacing what `_snapshotStore.Save()`/`.Clear()` are doing today, the snapshot file's presence stops being a meaningful proxy for "which mode is active" the moment nothing is left consistently writing/clearing it for that purpose — every mode-dependent UI element in the app can then silently show the wrong state (stuck showing "Rig Mode" forever, or flipping to "Normal Mode" on the first Rig-mode toggle that doesn't happen to touch the snapshot).

**Why it happens:**
This is an easy miss precisely *because* nothing about `IsInRigMode()`'s own code changes — a developer focused on "rewrite the Monitor step" can complete that work, watch the toggle itself work correctly on the rig (monitors do turn on/off correctly), and not notice that the mode indicator is now wrong, because the indicator bug only shows up on the *next* toggle attempt or app restart, not in the same test cycle as the monitor-mutation change itself.

**How to avoid:**
Land the `ISnapshotStore` → `IModeStore` repurposing (a minimal, independently-persisted "are we in Rig mode" marker, replacing file-presence-as-proxy with an explicit flag written at the same points the snapshot save/clear happens today) in the *same* phase as the Monitor-step rewrite, not as separate/later cleanup. Explicitly test mode indication across an app restart (not just a live toggle) — this project's own established crash-recovery property (mode must read correctly from disk even after the process is killed mid-toggle, not just from in-memory state) depends on the new marker being file-backed, not a plain in-memory bool.

**Warning signs:**
`MainForm`'s mode label/tray icon shows the wrong mode after a successful toggle, or shows the *last* mode indefinitely after an app restart — both symptoms only appear on the second toggle or on restart, not on the toggle that actually introduced the bug, making this a classic "looks done, isn't" gap that a single manual test pass will miss.

**Phase to address:**
The phase that rewrites `ToggleService.ToggleToNormalMode`'s Monitor step (Feature 3) — this is not optional plumbing to defer, it is a hard prerequisite for that same phase to be considered complete, and should be a named acceptance criterion, not an implementation detail.

---

### Pitfall 5: Losing the CR-01 "verify nothing actually changed before trusting the mode flag" safety net

**What goes wrong:**
Today, when `ToggleToRigMode`'s Monitor step fails at the pre-mutation validation stage (e.g. the "at least one active display must remain" guard throws *before* any real CCD mutation), a dedicated fix (documented inline as CR-01, ToggleService.cs lines 111-135) re-captures monitor state and compares it against the pre-mutation capture, clearing the snapshot only if nothing actually changed — specifically to prevent `IsInRigMode()` from reporting "true" (because the snapshot was saved *before* the mutation attempt, per the D-08/CORE-03 guarantee that a snapshot must exist before any mutation is attempted) when the display was never actually touched. If the new `IModeStore.SetRigMode()` call is simply moved to "after the Monitor step succeeds" (as recommended) without also preserving an equivalent "did the mutation genuinely happen" check on the failure path, this exact already-fixed bug class can resurface in a new form: the *inverse* risk becomes losing the CR-01 recapture-and-compare logic during the rewrite because it's easy to read as "snapshot-specific plumbing" that no longer applies once there's no snapshot, when its actual purpose (never let the mode flag misrepresent whether the display was really touched) is timeless and still needed.

**Why it happens:**
CR-01 was written specifically in terms of "the snapshot" (compare-then-clear-the-snapshot) — a developer rewriting this method to eliminate snapshot dependence entirely can correctly delete every literal `_snapshotStore` call and, in doing so, delete the *reasoning* alongside the mechanism, because the fix's code and its purpose are currently intertwined in the same few lines.

**How to avoid:**
Re-read CR-01's inline comment (ToggleService.cs lines 111-122) as a *requirement to preserve*, not code to delete: on a Monitor-step failure during `ToggleToRigMode`, before deciding whether the mode flag should reflect "still Normal" vs. "now Rig" (or whatever partial-failure semantics v2.0 settles on), the same re-capture-and-compare logic should still run, just writing its conclusion to `IModeStore` instead of `_snapshotStore.Clear()`/`Save()`. If the capture-after re-throws (can't confirm), err toward the same fail-safe direction CR-01 already established for its context.

**Warning signs:**
A monitor-step failure from the pre-mutation validation guard (e.g. "Cannot disable all configured monitors...") leaves `IsInRigMode()` reporting a mode inconsistent with what the display topology actually shows — reachable specifically via the same reproduction path CR-01 was originally written for (a disable-set that turns out to already be the only active display), now routed through the redesigned code.

**Phase to address:**
Same phase as Pitfall 4 (Feature 3's mode-tracking redesign) — write a regression test that specifically reproduces CR-01's original failure scenario against the *new* `IModeStore`-based code, not just new happy-path tests for the redesign.

---

### Pitfall 6: Reintroducing the exact null-vs-empty migration-guard bug that was already found and fixed once, for the new fields

**What goes wrong:**
The existing `JsonSettingsStore.Load()` migration guard (lines 55-58) checks `loaded.MonitorsToDisable is null` — deliberately *not* `is null || Count == 0` — because an earlier version of this exact check re-triggered on an empty-but-non-null list, silently re-injecting the legacy single monitor into a set the user had deliberately emptied via Settings (documented inline as the CR-01 fix, with an explicit warning: "MonitorDevicePath is never cleared post-migration... so that condition could never become permanently false"). If a developer writes a parallel migration/default-population guard for the new `NormalMonitorsToDisable`/`NormalMonitorsToEnable` fields (e.g., "if the Normal-mode set is empty, populate it from something") using an `?.Count > 0`-style "already configured" check instead of an `is null`-style check, this is the *identical* bug class, freshly reintroduced in new code, despite the fix for the original instance being fully documented in the same file.

**Why it happens:**
`is null`-only checks read as slightly "wrong"/incomplete to someone who hasn't read the CR-01 comment first — `?? something : existing` idioms and `Count > 0` "is this meaningfully configured" checks are the more common, more instinctively "safe-looking" pattern in most codebases, and this project's specific reason for deviating from that instinct (a deliberately emptied set must stay empty) is documented but easy to miss if the new field's migration/default logic is written as fresh code rather than as a copy of the existing pattern.

**How to avoid:**
Any new migration or default-population logic for `NormalMonitorsToDisable`/`NormalMonitorsToEnable` (or any other newly-nullable field this milestone touches) must copy the `is null`-only check pattern verbatim from the existing guard, and should cite the CR-01 comment directly rather than being written independently. Per ARCHITECTURE.md's own recommendation, the safest version of this for v2.0 is to add *no* auto-population logic at all for the new fields (leave them null after migration, requiring one explicit Settings visit) — which sidesteps the whole bug class by never writing a second migration guard in the first place.

**Warning signs:**
Any new `if (x is null || x.Count == 0)` or `if (!(x?.Count > 0))` guard anywhere near `JsonSettingsStore.Load()` that's deciding whether to auto-populate a monitor-set field — that shape is the exact signature of the bug CR-01 already fixed once.

**Phase to address:**
The phase that adds `NormalMonitorsToDisable`/`NormalMonitorsToEnable` to `AppSettings` and `JsonSettingsStore` (Feature 3) — flag this as an explicit code-review checklist item for that phase, not just a test case, since the bug is a *pattern* to avoid writing, not just a behavior to test for.

---

### Pitfall 7: Hardcoded "before switching to Rig Mode" error text becomes misleading once the same guard fires from Normal-mode toggle or the manual panel

**What goes wrong:**
The safety-guard exception the "at least one active display must remain" check throws (`WindowsMonitorController.cs` line 308: *"...at least one active display must remain. Connect and enable another display before switching to Rig Mode."*) is currently only reachable from the Rig-mode disable path, so its Rig-Mode-specific wording is accurate. Once `ToggleToNormalMode`'s rewritten Monitor step and the new manual panel both route through the exact same `DeactivateMonitors` method (which is the *correct*, recommended design — see Pitfall 2), this same literal error message will also surface when a user is toggling *to* Normal mode, or clicking a single monitor off in the live panel — in both cases, "before switching to Rig Mode" is simply wrong, and a user reading it while trying to go the *other* direction (or not toggling at all) will be actively confused about what action the message is telling them to undo.

**Why it happens:**
The message was written when this method had exactly one caller and one meaning; reusing the method (correctly, as Pitfall 2 recommends) without also generalizing its user-facing text is the kind of oversight that's invisible in code review (the logic is unchanged and correct) and only surfaces when a human actually triggers the guard from the *new* call sites during rig verification.

**How to avoid:**
Generalize the exception message to be caller-agnostic (e.g., "...at least one active display must remain enabled. Connect and enable another display first.") as part of whichever phase adds the second and third call sites (Normal-mode toggle, manual panel) — this is a one-line fix, but it must be remembered explicitly since nothing about compiling or unit-testing the redesign will catch stale, context-specific wording in an exception message.

**Warning signs:**
Trigger the guard from the Normal-mode toggle path or the manual panel during rig verification and read the actual displayed error text end-to-end (not just confirm an exception was thrown) — this is a "looks done but isn't" item that automated tests checking `Assert.Throws<InvalidOperationException>` will never catch, since they typically don't assert on message wording.

**Phase to address:**
Whichever phase makes `DeactivateMonitors` reachable from a second call site for the first time (Feature 3, the Normal-mode rewrite) — fix the message at that point, before the manual panel (Feature 4) adds a third caller and the same staleness would need fixing twice.

---

### Pitfall 8: Two independent "is this configured" gates (`ToggleService.IsFullyConfigured` and `SettingsForm.ValidateSettingsForm`) drifting out of sync

**What goes wrong:**
This codebase already has two separate implementations of "is the app fully configured" — `ToggleService.IsFullyConfigured` (ToggleService.cs:201-205, gates whether a toggle is allowed to run) and `SettingsForm`'s own `ValidateSettingsForm`/Save-button gating (gates whether Settings lets the user save at all). As App/Audio fields become individually optional, both need to relax their required-field lists *in the same way, at the same time* — if only one is updated, the two produce contradictory UX: e.g., `SettingsForm` still blocking Save until a companion app path is filled in, while `ToggleService` would otherwise happily skip an unset one — meaning the "optional" feature is unreachable through the GUI even though the underlying toggle logic supports it, or the reverse (Settings allows saving an empty-everything config that `ToggleService` still hard-rejects at toggle time with a confusing error, since the two checks no longer agree on what "fully configured" means).

**Why it happens:**
The two checks live in different projects (`RigToggle.Core` vs. `RigToggle.App`), were written independently, and nothing enforces they stay logically equivalent — this is a natural consequence of duplicating validation logic rather than sharing it, and this project already has that duplication today (functioning correctly only because both currently happen to require the same four fields).

**How to avoid:**
When relaxing `IsFullyConfigured`'s requirements for Feature 1/2 (optional App/Audio), make the identical relaxation to `SettingsForm.ValidateSettingsForm` in the same commit/plan, and add a test (or at minimum a code-review checklist item) that explicitly compares the two lists of "required fields" side by side. Consider, as a cleanup-pass (Feature 7) candidate, having `SettingsForm` call `ToggleService.IsSettingsConfigured()`-equivalent logic directly instead of maintaining a second, hand-duplicated check — but that's an optional simplification, not a blocking requirement for v2.0 to ship correctly.

**Warning signs:**
A field is nullable/skippable in `ToggleService` but `SettingsForm` still won't let the user save it as empty (or vice versa) — surfaces immediately in manual GUI testing of the optional-target feature, but easy to miss if verification only exercises `ToggleService` directly (e.g. via unit tests) without also exercising the Settings dialog end-to-end.

**Phase to address:**
The phase delivering optional App/Audio targets (Features 1/2) — both files must change together as part of that phase's own definition of done.

---

### Pitfall 9: Deleting "dead" restore/reconstruction code without preserving the hard-won rig-specific knowledge it encodes

**What goes wrong:**
Once `ToggleToNormalMode` stops calling `IMonitorController.Restore(MonitorState)`, `WindowsMonitorController.Restore()`/`RestoreViaReconstruction()` (~260 LOC, the single largest method in the codebase per ARCHITECTURE.md) become genuinely dead code with no remaining caller — a natural, correct target for the milestone's own cleanup-pass goal (Feature 7). The risk is treating "no caller" as equivalent to "safe to delete without reading it first": this method's own doc comments reference specific, empirically-discovered rig hardware behavior (the delta-shift/repositioning "survivor reconstruction" pattern from Phase 1's feasibility spike and Phase 4's live rig debugging — five iterations of real bugs "no Linux build could catch," per `MILESTONES.md`), plus two explicitly-flagged-as-currently-unused internal helpers (`CopyOutputTechnology`, `AssignSource`, both doc-commented "NOT currently called by production code... kept only as documented fallback knowledge," `WindowsMonitorController.cs` lines 663, 697) that exist *specifically because* a prior CCD/GPU-driver edge case needed them and might again.

**Why it happens:**
"Delete unused code" is a correct, standard cleanup instinct, and static analysis (no remaining callers) will confirm this code is safe to delete *from a compile-correctness standpoint*. What static analysis cannot capture is that some of this "unused" code was deliberately *kept* unused, as documented fallback knowledge, after a specific prior debugging effort — deleting it destroys institutional memory this project has already paid for once (multiple rig-debugging iterations), not just a mechanically-redundant method.

**How to avoid:**
Before deleting `Restore()`/`RestoreViaReconstruction()`, read the method fully (not just confirm zero callers) and decide explicitly, case by case, whether any of its documented rig-specific lessons (the repositioning/delta-shift pattern, the two flagged-fallback helpers) should be preserved somewhere durable — either as a comment on the *replacement* code path if the same CCD quirk could plausibly resurface there, or in a project doc, rather than silently vanishing with the deleted method. This does not mean "keep the dead code" — it means "extract the lesson before deleting the code that encodes it."

**Warning signs:**
A cleanup-pass PR that deletes `Restore()`/`RestoreViaReconstruction()`/`CopyOutputTechnology`/`AssignSource` in one commit with a generic "remove dead code" message and no reference to what rig-specific behavior they were protecting against — a red flag for review, not because the deletion is wrong, but because the reasoning for *why it's now safe* isn't visible in the change.

**Phase to address:**
The cleanup-pass phase (Feature 7), scheduled last per ARCHITECTURE.md's suggested build order (after the snapshot/restore subsystem is confirmed genuinely dead) — this specific sub-task should be called out by name in that phase's plan, not folded anonymously into a generic "delete unused code" task.

---

### Pitfall 10: Exe-size levers assumed safe for this app's COM/P-Invoke surface without rig verification — repeating the v1.2 "false assumption" pattern

**What goes wrong:**
STACK.md's v2.0 research recommends `InvariantGlobalization=true`, `EnableCompressionInSingleFile=true`, `SatelliteResourceLanguages=en`, and splitting the `NAudio` meta-package into `NAudio.Core`+`NAudio.Wasapi` — all correctly reasoned as independent of IL trimming (so they don't hit the already-known COM/P-Invoke-stripping risk) and grep-verified against this codebase's actual API usage. The remaining risk is narrower but real: (1) `EnableCompressionInSingleFile` adds a real decompression cost at every cold start, and this app is specifically expected to auto-start with Windows (Registry `Run` key, already shipped) — a slower cold boot is most noticeable on exactly the launch path (first boot of a rig session) this app's core value depends on feeling instant and reliable; (2) swapping the `NAudio` meta-package for its two constituent packages is a `PackageReference`-only change on paper, but this app's actual runtime dependency is the undocumented `IPolicyConfig` COM interop plus `NAudio.CoreAudioApi`'s `MMDeviceEnumerator` — neither of these has been proven, on real hardware, to be unaffected by the changed assembly-resolution set a self-contained publish produces after the package swap.

**Why it happens:**
This project has already hit this exact *class* of mistake once, explicitly and on the record: v1.2's theme-infrastructure phase shipped two consecutive "the framework/tool will surely handle this" assumptions (`Application.SetColorMode` recoloring buttons; `SetColorMode` owning the title bar) that were both rig-disproven and required a dedicated gap-closure round (`PROJECT.md` Key Decisions table, Phase 12 entries). Exe-size levers carry the same shape of risk — each is individually well-documented and reasoned correctly on paper (verified against official docs, per STACK.md's own sourcing), but "verified against official docs" and "verified against this specific app's actual COM/P-Invoke-heavy, self-contained, single-file, cold-auto-started runtime" are not the same claim, and this project's own history shows that gap is where its real bugs have lived.

**How to avoid:**
Treat exe-size reduction as needing the same rig-verification discipline as any other milestone feature, not as a "just flip some MSBuild properties, it's fine" mechanical task: build the reduced-size publish, run it on the actual rig hardware, and explicitly re-exercise the full toggle round trip (monitor disable/enable, audio switch, companion-app launch/minimize) plus a cold auto-started boot, not just confirm the `.exe` launches and shows a window. Measure and record both the size delta and the cold-start latency delta (STACK.md's own sourcing already flags this as Microsoft's documented caveat, not a hypothetical) rather than treating "smaller file" as the only success criterion.

**Warning signs:**
Any exe-size change getting marked "done" based solely on a sandbox/CI build succeeding and the `.exe`'s file size shrinking — with no corresponding real-rig toggle-round-trip verification recorded anywhere (this project's own established pattern, per every prior phase's `-VERIFICATION.md`/`-HUMAN-UAT.md` files, is that CCD/COM-interop-touching changes get an explicit rig checkpoint; size-reduction work touches the exact same publish pipeline that houses all of that interop and should get the same treatment, not an exemption).

**Phase to address:**
The exe-size-reduction phase (Feature 6) — its own definition of done should explicitly include "full toggle round trip verified on real rig hardware post-change," not just a build/publish success check.

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|-----------------|------------------|
| Leaving `IModeStore`'s flag-flip timing loosely mirrored from the old snapshot save/clear points, without re-deriving the reasoning from first principles | Faster to ship — "just move the calls to the same spots" | The old timing (`_snapshotStore.Save()` *before* mutation) existed specifically to guarantee crash-recovery data existed before a risky mutation; a mode flag has different failure-mode requirements (it's a UI-truth signal, not a restore payload) and blindly copying the old timing may under- or over-protect against the new failure modes (see Pitfall 4/5) | Never as a substitute for actually re-deriving the timing requirement — acceptable only as a *starting point* that gets explicitly re-justified for the new marker's actual purpose |
| Keeping `AudioState`/`AudioRoleState`-as-restore-payload and `IAudioController.Restore`/`CaptureState` alive "just in case," without committing to the audio-symmetry question FEATURES.md flags as open | Avoids having to make a real decision this milestone | Leaves the codebase in the same "asymmetric, confusing to reason about" state FEATURES.md explicitly warns against (monitor: configured both directions; audio: configured one direction, silently ignored the other) — and defers, rather than avoids, the eventual cleanup-pass cost | Acceptable only as an explicit, documented decision ("audio stays snapshot-based, on purpose, for this reason") — not acceptable as an accidental non-decision from simply not touching `WindowsAudioController.Restore` |
| Writing the new Normal-mode `SettingsForm` section as a copy-pasted second `DataGridView`/grid rather than refactoring the existing Rig-mode grid code into something both sections share | Faster, lower-risk (doesn't touch working Rig-mode grid code) | Any future bug fix to the grid's behavior (e.g. another dedup/staleness bug like the ones already fixed in Phase 6) now needs to be applied twice, and the two copies will silently drift | Acceptable for v2.0's initial ship if genuinely under time pressure, but should be flagged explicitly as a Feature 7 (cleanup pass) candidate, not left unflagged |

## Integration Gotchas

Common mistakes when connecting to this app's own existing internal boundaries (this milestone has no new *external* service integrations — the "integrations" that matter here are the Core↔Windows↔App boundaries this codebase already established).

| Integration | Common Mistake | Correct Approach |
|-------------|-----------------|--------------------|
| `MonitorPanelForm` → monitor mutation | Calling `WindowsMonitorController`/`WindowsDisplayAPI` directly from the new form's code-behind, reasoning "it's simpler than threading another interface through" | Depend only on `IMonitorController` (already the established pattern for every existing form) and the new `ManualMonitorService` wrapper — bypassing this breaks the shared `SingleFlightGuard` (Pitfall 1) and the codebase's own stated invariant that forms never `new` a concrete adapter directly (per `Program.cs`'s own doc comment) |
| `ManualMonitorService` ↔ `ToggleOrchestrator` | Building the panel's Activate/Deactivate calls as thin pass-throughs to `IMonitorController` with *no* guard at all, reasoning "it's just a single monitor, what's the harm" | Every mutating call — single-monitor or multi — must route through the shared `SingleFlightGuard`; "just one monitor" is exactly as capable of racing an in-flight toggle as a full toggle is |
| `JsonSettingsStore` migration ↔ new Normal-mode fields | Writing a second migration guard modeled on convenience (`Count > 0` "already has data" checks) instead of copying the existing `is null`-only pattern | Copy the existing guard's exact null-check shape (see Pitfall 6) — or better, add no auto-population logic at all for the new fields |
| `ToggleResult`/`ToggleStepResult` ↔ newly-optional steps | Omitting a step from the `steps` list entirely when it's unconfigured, instead of appending a `NotAttempted` entry | Every consumer (`ToggleResultFormatter`, GUI checklist, tray balloon-tip) already assumes a predictable, complete step shape — always append `NotAttempted`, never omit (see Pitfall 3 and ARCHITECTURE.md's Anti-Pattern 3) |

## Performance Traps

Patterns that work in a sandbox/CI build but degrade on the actual rig hardware or over a long tray-resident session.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| `MonitorPanelForm` re-querying `IMonitorController.GetAllMonitors()` on every render/paint instead of only on explicit refresh/open | Sluggish panel UI, or (worse) repeated `PathInfo.GetActivePaths()` CCD calls under normal idle use | Cache the monitor list per explicit refresh action (mirrors `SettingsForm`'s existing one-shot `PopulateMonitorGrid()` pattern, per ARCHITECTURE.md) — no polling loop, no per-paint re-query | Not scale-dependent (this is a 1-4 monitor personal rig, not a scale problem) — this is a correctness/responsiveness trap that shows up the first time the panel is left open during normal use, not at any particular monitor count |
| `EnableCompressionInSingleFile` decompression cost stacking with the existing autostart cold-boot path | Noticeably slower time-to-tray-icon on first launch after Windows boot, specifically | Measure cold-start latency on the real rig before/after enabling compression (see Pitfall 10) — don't assume the documented tradeoff is negligible for *this* app's specific autostart-at-boot usage pattern | Most visible exactly at boot-time autostart, since disk cache is cold and CPU/disk contention from other startup programs is highest at that moment — a warm-cache manual relaunch during testing will under-represent this cost |
| Icon-instance leak pattern (already fixed once for the rig/normal tray pair, per v1.2's own PITFALLS.md finding) recurring for the new panel's per-monitor status icons | GDI handle count climbing over a multi-hour tray-resident session with repeated manual-panel toggles | Pre-load/cache all status-icon instances once (enabled/disabled × however many glyph variants), never construct a `new Icon(...)`/`new Bitmap(...)` per row-refresh | Only surfaces on long-running sessions with many manual toggles — a quick manual test of "click a few rows, looks fine" will not catch a slow per-toggle GDI handle leak |

## Security Mistakes

Domain-specific concerns for this app's actual attack surface (a personal, single-user, non-networked utility — most conventional web/app security categories don't apply; the relevant surface is local-machine trust boundaries this app already accepts, per its documented `T-03-09` TOCTOU stance).

| Mistake | Risk | Prevention |
|---------|------|------------|
| Treating the new "optional" `CompanionAppPath` as a reason to relax the existing `File.Exists` preflight check's strictness (e.g. "it's optional now, so don't bother validating it as carefully") | A configured-but-attacker-substituted path could still be launched without the existing (already-accepted, documented) TOCTOU caveat being re-examined for the new optional-skip branch | Keep the existing `File.Exists` check's behavior identical for the "configured" branch — optionality changes *whether* the check runs, not *how strict* it is when it does run |
| Manual panel actions (`ManualMonitorService`) becoming a second, less-scrutinized code path for the same CCD mutation the toggle already goes through, with weaker input validation on the device-path string (e.g. no check that the path came from `GetAllMonitors()` rather than some other/stale source) | A stale or malformed device path reaching `DeactivateMonitors` could produce a confusing failure or, in the worst case, target the wrong physical monitor if two devices ever share a similar-looking path fragment | Route the panel's device-path values directly from the same `GetAllMonitors()` call used to populate the panel's own list, never from a cached/stale value from a prior refresh — re-validate "still present" immediately before acting (the existing `WindowsMonitorController.Restore`'s "still-present guard first" pattern, `WindowsMonitorController.cs:539`, is the precedent to reuse) |

## UX Pitfalls

Common user-experience mistakes specific to this milestone's redesign.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-------------------|
| Manual panel shows a monitor's status as of when the panel last refreshed, with no visual indication the data might be stale (e.g. after a toggle ran while the panel was open) | User clicks "Disable" on a monitor they believe is currently enabled, but it was already disabled by a toggle that ran in the background — the resulting error (or worse, silent no-op) is confusing without context | Either auto-refresh on a `WM_DISPLAYCHANGE`-style signal, or at minimum visually mark the panel's data as "as of last refresh" and provide an obvious, low-friction manual refresh action — don't let a stale read silently masquerade as live data |
| The toggle checklist (`ToggleResultFormatter`'s MessageBox/tray balloon-tip) starts showing "Skipped (not configured)" rows for App/Audio without any prior UX precedent in this app for what a "neutral, not-failed" row looks like | A user seeing a 3-row checklist with one row now reading differently than the "Succeeded"/"FAILED(reason)" pattern they're used to from every prior milestone may misread "Skipped" as a failure, or as a bug, rather than expected behavior for their deliberate configuration choice | Design the "Skipped (not configured)" visual/text treatment to be clearly distinct from both Success and Failure — not styled or worded ambiguously close to either — and mention it explicitly in whatever user-facing changelog/README update accompanies this milestone, since it's a genuinely new state existing users haven't seen before |
| Settings UI still visually implies App/Audio fields are required (e.g. leftover red-asterisk/required-field styling) after the validation-gate relaxation ships | Users assume the fields are still mandatory and never discover the new optional behavior, defeating the point of the feature | Explicitly audit and update Settings UI's visual treatment of the now-optional fields alongside the validation-logic change — a logic-only fix without a UI-affordance update ships a feature nobody can discover |

## "Looks Done But Isn't" Checklist

Things that appear complete in a sandbox/CI build or a quick manual click-through but are missing critical pieces this project's own history shows only surface under real rig conditions or a second look.

- [ ] **Optional App/Audio targets:** Often missing the "configured but now invalid" failure path (Pitfall 3) — verify by testing with a *deliberately broken* configured value (moved `.exe`, unplugged device), not just an unset one.
- [ ] **Mode indicator after the snapshot-restore removal:** Often correct on the very toggle that changed it, wrong on the *next* toggle or after an app restart (Pitfall 4) — verify by restarting the app after a toggle, not just watching the live UI update.
- [ ] **Normal-mode + manual-panel safety guard reuse:** Often throws the correct exception type but with stale, Rig-Mode-specific wording (Pitfall 7) — verify by reading the actual displayed error text from the Normal-mode and panel call sites, not just confirming an exception fires.
- [ ] **`SettingsForm` validation vs. `ToggleService.IsFullyConfigured`:** Often only one of the two gets updated (Pitfall 8) — verify by attempting to save/toggle with the same partially-empty configuration through both surfaces and confirming they agree.
- [ ] **Manual panel vs. in-flight toggle:** Often "works" in every manual test because a human can't click fast enough to actually race the two paths — verify with an automated test that deliberately starts a toggle and, before it completes, calls the panel's Activate/Deactivate directly, asserting the second call is rejected, not silently interleaved.
- [ ] **Exe-size reduction:** Often verified by file-size diff alone — verify with a full rig round-trip toggle (monitor + audio + companion app) and a cold auto-started boot, per Pitfall 10.
- [ ] **Cleanup-pass deletions:** Often verified by "still compiles, tests still pass" — verify that any deleted method's rig-specific documented rationale (Pitfall 9) was either genuinely obsolete or deliberately preserved elsewhere before deletion, not just silently discarded.

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|-----------------|------------------|
| Mode indicator desyncs from actual topology (Pitfall 4/5) | LOW | The underlying display/audio state is unaffected — only the *UI's belief* about which mode is active is wrong. Recovery is a one-line manual fix: delete the stale mode-marker file (mirrors today's existing "delete the corrupted state file" recovery instruction already surfaced in `ToggleToNormalMode`'s own exception text) and let the app re-derive/re-configure from a clean state. Should be documented in whatever end-user troubleshooting notes exist. |
| Manual panel/toggle race produces an inconsistent topology despite the shared guard (e.g. the guard itself has a bug) | MEDIUM | The existing verify-and-throw pattern in `WindowsMonitorController` should surface *some* failure rather than silent corruption — recovery is the same as any failed CCD mutation today: re-run the toggle or manual action once the conflicting operation has finished, since neither path performs partial/uncommitted mutations by design. |
| A newly-optional field's "configured but broken" case was accidentally collapsed into silent-skip (Pitfall 3 shipped anyway) | LOW | Fixing the code path is a small, isolated change (re-add the distinct `Failed` branch) — no data migration or user-facing recovery needed, since nothing was corrupted, only under-reported. |
| A cleanup-pass deletion turns out to have removed genuinely-needed rig-specific fallback logic (Pitfall 9 realized) | HIGH | This is the expensive one: the lesson is gone from the codebase and may only resurface as a real rig failure on hardware/driver combinations not covered by the current dev's testing. Recovery requires re-deriving the fix from scratch (as the original Phase 1/4 debugging did) unless the deleted code is still recoverable from git history — which is why Pitfall 9's prevention (read before delete, extract the lesson) is worth the extra care relative to how cheap the mistake looks in the moment. |

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification |
|---------|--------------------|----------------|
| 1. Two independent concurrency guards | Manual monitor panel phase (Feature 4), extraction done alongside Feature 3's `ToggleOrchestrator` touch | Existing `ToggleOrchestratorTests` reentrancy suite passes unchanged post-extraction; new test asserts a panel action is rejected while a toggle is in flight and vice versa |
| 2. Zero-monitors edge case across two independently-configured sets | Normal-mode monitor-set rewrite phase (Feature 3) | Confirm Normal-mode toggle and manual panel both route through the unchanged `DeactivateMonitors` survivor-check — no second implementation exists anywhere in the diff |
| 3. Silent skip masking real misconfiguration | Optional App/Audio targets phase (Feature 1/2) | Test matrix includes both "unset" (NotAttempted) and "set but invalid" (Failed) cases per optional field |
| 4. Mode-detection breakage from removing snapshot dependency | Normal-mode monitor-set rewrite phase (Feature 3) | Mode indicator verified correct after an app restart post-toggle, not just live in-session |
| 5. Losing the CR-01 recapture-and-compare safety net | Normal-mode monitor-set rewrite phase (Feature 3) | Regression test reproduces CR-01's original scenario against the new `IModeStore` code path |
| 6. Reintroducing the null-vs-empty migration bug | `AppSettings`/`JsonSettingsStore` field-addition phase (Feature 3) | Code review checklist item citing CR-01 explicitly; test asserts a deliberately-emptied Normal-mode set survives reload unchanged |
| 7. Stale "before switching to Rig Mode" error text | Normal-mode monitor-set rewrite phase (Feature 3, first new caller) | Manual/rig verification reads the actual displayed message text from each of the three call sites, not just exception type |
| 8. `IsFullyConfigured`/`ValidateSettingsForm` drift | Optional App/Audio targets phase (Feature 1/2) | Both files changed in the same commit/plan; a shared or cross-checked required-field list |
| 9. Deleting rig-specific fallback knowledge during cleanup | Cleanup-pass phase (Feature 7), scheduled last | PR description explicitly addresses what happened to the documented rationale in deleted methods, not just "removed dead code" |
| 10. Exe-size levers unverified against COM/P-Invoke + autostart cold-boot | Exe-size-reduction phase (Feature 6) | Full rig round-trip toggle + cold auto-started boot timing recorded as part of that phase's own verification, not deferred to a later catch-all pass |

## Sources

- Direct source-tree reads (HIGH confidence — this project's actual current code, not documentation or inference): `src/RigToggle.Core/ToggleService.cs` (full file), `src/RigToggle.Core/ToggleOrchestrator.cs` (full file), `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` (full file), `src/RigToggle.Core/Models/AppSettings.cs` (full file), `src/RigToggle.Windows/WindowsMonitorController.cs` (`DeactivateMonitors`, `CopyOutputTechnology`/`AssignSource` doc comments), `src/RigToggle.App/MainForm.cs` (`RefreshUi`/mode-dependent call sites, grepped)
- `.planning/PROJECT.md` — this project's own documented bug history (CR-01/CR-02, D-02/D-04/D-05/D-08/D-14, the v1.2 dark-mode false-assumption gap-closure round, the Phase 1/4 rig-debugging iterations) — used as the primary evidence base for which bug classes are genuinely at risk of recurring in this specific redesign, rather than generic Windows-dev speculation
- `.planning/research/ARCHITECTURE.md` (this session, already written for v2.0) — component/data-model design this pitfalls pass is grounded against (the `SingleFlightGuard`/`ManualMonitorService`/`IModeStore` recommendations)
- `.planning/research/FEATURES.md` (this session, already written for v2.0) — edge-case table and the "Normal-mode audio symmetry" open question this pitfalls pass builds on rather than duplicates
- `.planning/MILESTONES.md` — v1.0/v1.1/v1.2 "Key accomplishments" bug-history entries (repositioning/delta-shift debugging iterations, `--tray` hidden-start bug, migration-guard re-corruption bug) used to corroborate which failure classes are project-specific recurring risks, not one-off incidents

---
*Pitfalls research for: Rig Toggle v2.0 (Configurable Monitors, Optional Targets & Cleanup)*
*Researched: 2026-08-04*
