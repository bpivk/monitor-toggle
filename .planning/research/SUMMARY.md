# Project Research Summary

**Project:** Rig Toggle — v2.0 Milestone (Configurable Monitors, Optional Targets & Cleanup)
**Domain:** Windows desktop GUI utility (display/audio/process control automation) — subsequent-milestone redesign of an existing four-project .NET 10 WinForms solution
**Researched:** 2026-08-04
**Confidence:** HIGH

## Executive Summary

v2.0 is not a new product — it's a scoped redesign threaded through an already-shipped, rig-validated four-project solution (`RigToggle.Core`/`.Windows`/`.App`/`.Tests`). All four research passes converge on the same conclusion: **no new NuGet dependencies are required anywhere in this milestone.** Every target capability — optional/nullable App and Audio targets, an explicit per-mode monitor target set replacing snapshot-restore, a live manual monitor panel, exe-size reduction, and a cleanup pass — is achievable with existing packages (`WindowsDisplayAPI`, `NAudio`), existing BCL/WinForms primitives, and the project's own already-proven architectural patterns (Core-interface / Windows-adapter / App-composition-root, nullable-by-design settings, hand-rolled WinForms controls over third-party suites). The `IMonitorController`/`IAudioController` interfaces need **zero signature changes** — the entire redesign happens above the Windows-adapter layer, which meaningfully de-risks the milestone since the highest-uncertainty code (real CCD/COM interop) is untouched.

The recommended approach: (1) ship the low-risk optional-target validation relaxations (App path, Audio device IDs) first, since they require no data-model or mode-tracking changes and deliver real user value immediately; (2) tackle the core, highest-risk piece next — replacing Normal-mode's snapshot-restore with an explicit, symmetric-with-Rig-mode monitor target set, which forces a load-bearing but under-specified architectural dependency: mode detection (`IsInRigMode()`) currently *is* "does a snapshot file exist on disk" (D-14), and that proxy breaks silently the moment Normal mode stops depending on the snapshot for restore — this must become an explicit, independently-persisted mode flag (`IModeStore`), landed in the same phase as the monitor-set rewrite, not deferred; (3) build the manual monitor panel and its shared concurrency guard after the mode-store work, so it reuses the unified apply/safety-guard codepath rather than introducing a third bespoke one; (4) finish with a cleanup pass (deleting the now-dead ~260-LOC `Restore()`/`RestoreViaReconstruction()` subsystem) and exe-size reduction (MSBuild-property-only, no trimming), both schedulable independently of the rest.

The dominant risk theme across all four research files is **silent regression through flattening distinct states into one** — treating "unconfigured" and "configured but broken" as the same skip-path (masks real failures), treating "mode flag" timing as interchangeable with old "snapshot" timing (loses a hard-won CR-01 safety net), and treating two independently-edited monitor-set grids as safe without re-verifying the shared apply-time "at least one monitor enabled" guard actually covers all three mutating entry points. None of these require new technology to avoid — they require discipline in *how* existing, well-understood patterns are extended, which is exactly what the Pitfalls research maps to specific phases below.

## Key Findings

### Recommended Stack

No new packages. Exe-size reduction is pure MSBuild configuration (`EnableCompressionInSingleFile`, `SatelliteResourceLanguages=en`, `InvariantGlobalization=true`, splitting the `NAudio` meta-package into `NAudio.Core`+`NAudio.Wasapi`) — explicitly **not** IL trimming, which remains a hard, unchanged project constraint because trimming's reachability analysis misidentifies the app's COM-interop (`IPolicyConfig`) and P/Invoke (`WindowsDisplayAPI`) code paths as dead and strips them. The live manual monitor panel is a hand-rolled tile `UserControl` in a `FlowLayoutPanel`, reusing the project's own already-validated `RigToggle.IconGen` GDI+ icon pipeline — explicitly not `ListView.Tile` (native-control theming risk under WinForms dark mode) or a third-party component suite (Krypton/Infragistics — same "extra dependency, second theming API" rejection already applied to prior milestones). Optional/nullable settings need zero stack change: `AppSettings` is already 100% nullable-by-design, `System.Text.Json` already round-trips missing/null fields, and this exact migration pattern (detect legacy/absent field, migrate once, never re-trigger — the CR-01-hardened `is null`-only check) has already shipped once for v1.0→v1.1.

**Core technologies (unchanged from prior milestones, reused as-is):**
- `WindowsDisplayAPI` 1.3.0.13 (CCD display topology) — no interface changes needed; `ActivateMonitors`/`DeactivateMonitors` already take arbitrary sets.
- `NAudio.CoreAudioApi` (via split `NAudio.Core`+`NAudio.Wasapi`) — same types/version, dependency-hygiene-only swap.
- Hand-rolled `IPolicyConfig` COM interop — unchanged, still the only way to set the default audio device.
- `System.Text.Json` + nullable-by-design `AppSettings` — extend with more `string?`/`List<string>?` fields, no serializer change.

### Expected Features

Five target features (per PROJECT.md scope), all closing scope gaps or matching patterns already standard in comparable tools (DisplayFusion Monitor Profiles, NirSoft MultiMonitorTool, Windows' own Settings > Display, EarTrumpet/SoundVolumeView) — none are competitive differentiators requiring extra polish beyond matching the established convention.

**Must have (table stakes):**
- Optional App launch target — unset path silently skips launch/minimize, no dialog; "configured but now missing" must still surface as a real failure (not silently downgraded).
- Optional Audio devices per direction — unset device skips the audio switch, no error; **architectural finding:** `NormalAudioDeviceId` is already collected in Settings but never read by `ToggleToNormalMode` today — giving it real runtime effect (mirroring Rig mode's `SetDefault` call) is a genuine open decision, not just a validation change, and should be resolved explicitly during roadmap/requirements, not left to an implementer.
- Normal-mode explicit monitor target set, symmetric with Rig mode's existing `MonitorsToDisable`/`MonitorsToEnable` — replaces `Restore(snapshot.Monitor)` entirely; no snapshot-fallback for monitors not mentioned in either set.
- Live manual monitor panel with per-monitor status icons, immediate-effect (not deferred like Settings), independent of Rig/Normal mode.
- Safety constraint ("at least one monitor always enabled") enforced identically across all three now-independent mutation paths (Rig toggle, Normal toggle, manual panel) from one shared codepath.

**Should have (worth doing well, cheap relative to value):**
- Identify action (briefly overlay a number on the physical screen) for the manual panel — standard in Windows' own Display settings, not explicitly scoped but flagged as a candidate for the roadmap to accept or defer.
- Extending the existing `SkipMonitorConfirmation` confirmation-gate pattern to the new manual panel, since impulsive single-click toggles there carry more risk than pre-vetted Rig/Normal config.

**Defer / explicitly out of scope:**
- Independent per-direction app-launch opt-in (splitting the single `CompanionAppPath` field) — unrequested scope creep.
- Drag-and-drop monitor arrangement/resolution/orientation editing in the manual panel — Windows' own Display settings already owns that; stay narrowly on/off.
- "Smart" migration that synthesizes a Normal-mode config from the retired snapshot at upgrade time — leave new fields null, require one explicit Settings visit post-upgrade.

### Architecture Approach

The v2.0 redesign fits entirely inside the existing four-project shape with **zero `IMonitorController`/`IAudioController`/`IAppController` signature changes**. The single most load-bearing architectural change is repurposing `ISnapshotStore`/`JsonSnapshotStore` into a minimal `IModeStore` — keeping the same atomic-write, crash-safe file-presence idiom, but flipping an explicit Rig/Normal marker instead of keying mode off "does a snapshot happen to exist." A new `SingleFlightGuard` is extracted from `ToggleOrchestrator`'s existing `Interlocked.CompareExchange` busy-flag into its own class, constructed once in `Program.cs`, and injected into **both** `ToggleOrchestrator` and the new `ManualMonitorService` — this is the only genuinely new cross-component coordination boundary the milestone introduces, and it's what prevents a toggle and a manual panel action from racing the same CCD topology concurrently.

**Major components:**
1. `AppSettings` — gains `NormalMonitorsToDisable`/`NormalMonitorsToEnable` (new sibling fields, not a rename of the existing Rig-mode pair, to avoid touching the CR-01 migration guard and every existing test for zero behavioral benefit).
2. `IModeStore` (repurposed `ISnapshotStore`) — independently-persisted mode marker, replacing file-presence-as-mode-proxy.
3. `SingleFlightGuard` (new, extracted) — shared concurrency guard for both `ToggleOrchestrator` and `ManualMonitorService`.
4. `ManualMonitorService` (new) — thin guarded wrapper over `IMonitorController.ActivateMonitors`/`DeactivateMonitors`, single-element sets.
5. `MonitorPanelForm` (new, `RigToggle.App`) — non-modal `Form`, depends only on `IMonitorController`+`ManualMonitorService`, never a concrete adapter directly (established codebase invariant).
6. `ToggleService.ToggleToNormalMode` — rewritten Monitor step (and, if the audio-symmetry recommendation is adopted, Audio step) to use the same `ActivateMonitors(enableSet); DeactivateMonitors(disableSet);` shape Rig mode already uses, instead of `Restore(snapshot)`.

**Suggested build order (from ARCHITECTURE.md, matches FEATURES.md's independent dependency analysis):** optional targets first (lowest risk, no prerequisites) → monitor-set + mode-store redesign (highest risk, forces the mode-tracking decision) → shared concurrency guard + manual panel (reuses the prior phase's unification) → cleanup pass + exe-size reduction last (only safe once the snapshot/restore subsystem is confirmed genuinely dead; exe-size work has no ordering dependency and can be batched here for scheduling convenience only).

### Critical Pitfalls

1. **Two independent concurrency guards instead of one shared one** — building `ManualMonitorService` with its own busy-flag "because it's a separate feature" cannot prevent a genuine cross-path race against the same CCD topology. Extract `ToggleOrchestrator`'s existing guard into a standalone `SingleFlightGuard`, construct it once, inject the same instance into both callers.
2. **Mode detection breaks silently when the snapshot dependency is removed, without anyone touching `IsInRigMode()` itself** — the bug only surfaces on the *next* toggle or app restart, not the toggle that introduced it, making it a classic "looks done, isn't" gap a single manual test pass will miss. Land the `IModeStore` repurposing in the *same* phase as the monitor-step rewrite, and explicitly test mode indication across an app restart.
3. **Silently skipping a step when a target is unconfigured masks a genuinely different failure state ("configured but broken")** — a moved companion-app exe or unplugged audio device must still surface as a real `Failed` result, not be flattened into the same `NotAttempted` outcome as "never configured." Write one test per state per newly-optional field.
4. **Losing the CR-01 recapture-and-compare safety net** during the mode-tracking rewrite — the reasoning ("never let the mode flag misrepresent whether the display was really touched") is timeless even though its current code is snapshot-specific; deleting the literal `_snapshotStore` calls risks deleting the reasoning alongside the mechanism.
5. **Reintroducing the already-fixed null-vs-empty migration-guard bug** for the new `NormalMonitorsToDisable`/`NormalMonitorsToEnable` fields — any new migration logic must copy the existing `is null`-only check verbatim (not `Count > 0`-style checks), or better, add no auto-population logic at all.

## Implications for Roadmap

Based on combined research (FEATURES.md's dependency graph + ARCHITECTURE.md's build order + PITFALLS.md's phase mapping, all three converge on the same sequencing), suggested phase structure:

### Phase 1: Optional App & Audio Targets
**Rationale:** Lowest risk, no data-model or mode-tracking prerequisites — pure validation-gate relaxation against the existing `AppSettings`/`ToggleService` shape. Delivers real user value independently and builds confidence before the higher-risk mode-tracking redesign.
**Delivers:** `CompanionAppPath`, `RigAudioDeviceId` become skippable in `ToggleToRigMode` without breaking the "configured but broken" failure path; `SettingsForm.ValidateSettingsForm` relaxed in lockstep with `ToggleService.IsFullyConfigured` (must not drift — Pitfall 8).
**Addresses:** Features 1 & 2 (Rig-mode half) from FEATURES.md.
**Avoids:** Pitfall 3 (silent-skip masking real failures), Pitfall 8 (two validation gates drifting out of sync).
**Open question to resolve before/during this phase:** does `NormalAudioDeviceId` gain real runtime effect (recommended, for symmetry) or stay an inert, collected-but-unused field? Flagged in FEATURES.md as a requirements-clarification item, not an implementer's call.

### Phase 2: Normal-Mode Explicit Monitor Config + Mode-Store Redesign
**Rationale:** The core, highest-risk, most novel piece of the milestone — forces the mode-detection redesign that Phase 3 then benefits from for free. Sequencing this before the manual panel avoids two divergent "toggle a monitor" implementations.
**Delivers:** `AppSettings` gains `NormalMonitorsToDisable`/`NormalMonitorsToEnable`; `ToggleToNormalMode`'s Monitor step (and Audio step, if the symmetry question above resolves "yes") rewritten to the same configured-target shape Rig mode already uses; `ISnapshotStore` repurposed into `IModeStore`; `StateSnapshot`/`MonitorState`-as-restore-payload marked for deletion in Phase 4.
**Uses:** `WindowsDisplayAPI`'s already-proven `ActivateMonitors`/`DeactivateMonitors` (zero interface changes needed); `System.Text.Json`'s existing nullable-field round-tripping.
**Implements:** `IModeStore`, the `NormalMonitorsToDisable`/`ToEnable` data model.
**Avoids:** Pitfall 2 (zero-monitors edge case across two independently-configured sets — verify no second implementation of the survivor-check exists), Pitfall 4 (mode detection breaking silently), Pitfall 5 (losing CR-01's safety net), Pitfall 6 (null-vs-empty migration bug reintroduced), Pitfall 7 (stale "before switching to Rig Mode" error text once `DeactivateMonitors` gets a second caller — fix the message here, before Phase 3 adds a third).

### Phase 3: Shared Concurrency Guard + Manual Monitor Panel
**Rationale:** Depends on Phase 2's controller-call unification (not its data model) — building this after Phase 2 means the panel's status icons reflect the final, redesigned notion of monitor state without a second UI pass, and the `SingleFlightGuard` extraction is more natural as a companion refactor to `ToggleOrchestrator` while it's already being touched.
**Delivers:** `SingleFlightGuard` (extracted, single shared instance), `ManualMonitorService`, non-modal `MonitorPanelForm` with per-monitor status-icon tiles (reusing `RigToggle.IconGen`).
**Addresses:** Features 4 & 5 from FEATURES.md.
**Avoids:** Pitfall 1 (two independent concurrency guards — verify with an automated test that deliberately races a toggle against a panel action), Pitfall 7 (verify the generalized error text reaches the panel's third call site too).

### Phase 4: Cleanup Pass + Exe-Size Reduction
**Rationale:** Only safe to run once Phase 2 has retired the snapshot/restore subsystem — removing dead code in one pass rather than incrementally. Exe-size reduction has no ordering dependency on anything else and is batched here for scheduling convenience, not because it's architecturally coupled.
**Delivers:** `WindowsMonitorController.Restore()`/`RestoreViaReconstruction()` (~260 LOC) deleted along with `StateSnapshot` and (if the audio-symmetry decision from Phase 1 was "yes") `AudioState`/`AudioRoleState`-as-restore-payload and `WindowsAudioController.Restore()`/`CaptureState()`; MSBuild-only exe-size levers (`EnableCompressionInSingleFile`, `SatelliteResourceLanguages=en`, `InvariantGlobalization=true`, `NAudio` package split) applied and verified.
**Avoids:** Pitfall 9 (deleting dead code without preserving the rig-specific knowledge it encodes — read `Restore()` fully before deleting, extract any lessons worth preserving elsewhere), Pitfall 10 (exe-size levers assumed safe for this app's COM/P-Invoke surface without rig verification — this phase's definition of done must include a full rig round-trip toggle plus a cold auto-started-boot timing check, not just a file-size diff, echoing the v1.2 "false assumption" pattern this project has already been burned by once).

### Phase Ordering Rationale

- **Dependency-driven, not feature-list-order:** FEATURES.md's own dependency graph confirms Features 1 & 2 have no hard dependency on 3/4/5 and can ship first; Feature 4 explicitly requires Feature 3's controller-unification work; Feature 5 (safety guard) is a cross-cutting verification concern threaded through 3 and 4, not a standalone phase — this shapes the 4-phase structure above rather than a 1-phase-per-numbered-feature structure.
- **The mode-tracking redesign is the load-bearing risk, so it's isolated in its own phase** with dedicated pitfall coverage (4 of PITFALLS.md's 10 critical pitfalls map to this one phase) rather than being folded silently into "the monitor config feature" as an implementation detail.
- **Cleanup and exe-size work are sequenced last** specifically because their prerequisite (dead code confirmed genuinely dead) doesn't exist until Phase 2 ships — attempting them earlier would require re-doing the dead-code analysis.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2 (Normal-mode + mode-store redesign):** The audio-symmetry question (does `NormalAudioDeviceId` gain real runtime effect?) is explicitly flagged as unresolved by both FEATURES.md and ARCHITECTURE.md — resolve this as a requirements decision before or at the start of planning this phase, not mid-implementation. The exact `IModeStore` flag-flip timing (mirroring old snapshot save/clear points vs. re-deriving from first principles) also needs explicit design attention per Pitfall 5/Technical Debt table.
- **Phase 4 (cleanup):** Needs a deliberate read-before-delete pass on `Restore()`/`RestoreViaReconstruction()` and the two flagged-fallback helpers (`CopyOutputTechnology`, `AssignSource`) — not pure mechanical dead-code removal.

Phases with standard, well-documented patterns (skip deep research-phase):
- **Phase 1 (optional targets):** Pure validation-gate relaxation against an already-nullable-by-design model; the project has already shipped this exact class of change once.
- **Phase 3 (manual panel + guard):** Hand-rolled `UserControl`/`FlowLayoutPanel` and `Interlocked`-based guard extraction are both established, low-ambiguity WinForms/threading patterns already used elsewhere in this codebase.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | MSBuild-property and dependency-hygiene claims verified directly against official Microsoft docs, package contents, and this repo's own source (grep-confirmed API usage). MEDIUM only on exact compression/satellite-language MB-savings figures, which are workload-dependent and not yet empirically measured against this specific exe. |
| Features | MEDIUM-HIGH | Patterns verified against DisplayFusion, NirSoft MultiMonitorTool, Windows' own Settings > Display UI, and EarTrumpet/SoundVolumeView via web search; codebase dependency claims (e.g., the `NormalAudioDeviceId` dead-field finding) verified by direct inspection of current source, not training data. |
| Architecture | HIGH on integration points and component placement (verified directly against the real source tree). MEDIUM on one specific recommendation: whether audio also drops snapshot-restore in Normal mode — this is the research's own inference from evidence (an already-scaffolded-but-unwired field), not a directly-stated requirement, and should be confirmed during roadmap/planning. |
| Pitfalls | HIGH for pitfalls grounded in direct reads of this project's own source and documented bug history (`PROJECT.md`); MEDIUM for exe-size-lever interaction risks (no first-party report of this exact combination breaking COM/P-Invoke, reasoned from documented trimming risk plus this project's own track record). |

**Overall confidence:** HIGH

### Gaps to Address

- **Audio-symmetry decision (Normal-mode `SetDefault` vs. stay `Restore`-based):** Flagged independently by FEATURES.md, ARCHITECTURE.md, and PITFALLS.md as the one genuinely open architectural question in this milestone. Resolve explicitly as a requirements/roadmap decision before Phase 2 planning, not left implicit.
- **Mode-flag timing re-derivation:** The old snapshot save/clear timing existed for restore-safety reasons that no longer apply to a pure mode marker — don't copy the old timing without re-justifying it for the new marker's actual purpose (a UI-truth signal, not restore payload). Address explicitly during Phase 2 planning.
- **Long-disabled-monitor re-enable behavior across a full reboot (carried over from v1.1, still relevant to the manual panel's "enable a monitor" action):** best-mode-logic reconstruction of resolution/position for a monitor inactive since before the current session is API-contract-verified but not empirically proven on this specific rig hardware across a real reboot — if not already validated in a prior milestone, budget a small hardware-verification step rather than assuming.
- **Exe-size lever interaction with COM/P-Invoke + autostart cold-boot:** No first-party report of this exact combination breaking anything, but this project has already been burned once (v1.2) by "the framework will surely handle this" assumptions that were rig-disproven. Treat Phase 4's exe-size work with the same rig-verification discipline as any CCD/COM-interop-touching change, not as a low-risk mechanical task.

## Sources

### Primary (HIGH confidence)
- Direct source-tree reads this session: `src/RigToggle.Core/ToggleService.cs`, `ToggleOrchestrator.cs`, `Models/AppSettings.cs`, `Models/StateSnapshot.cs`, `Persistence/JsonSettingsStore.cs`, `Persistence/JsonSnapshotStore.cs`; `src/RigToggle.Windows/WindowsMonitorController.cs`, `WindowsAudioController.cs`; `src/RigToggle.App/MainForm.cs`, `Program.cs`, `SettingsForm.cs`; `src/RigToggle.Tests/Doubles/*` — used across all four research passes as primary evidence, not inference.
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview — `EnableCompressionInSingleFile` semantics and documented startup-cost tradeoff.
- https://learn.microsoft.com/en-us/windows/win32/shell/notification-area — official tray icon size guidance (carried over context).
- `.planning/PROJECT.md` and `.planning/MILESTONES.md` — v2.0 milestone framing, target features list, documented bug history (CR-01/CR-02, D-02/D-04/D-05/D-08/D-14) used as ground truth for scope and as the evidence base for which bug classes are genuine recurrence risks.

### Secondary (MEDIUM confidence)
- https://www.displayfusion.com/HelpGuide/WorkingWithDisplayFusionMonitorProfiles/ and companion forum thread — Monitor Profiles as explicit named target sets, confirmation-prompt-suppression pattern.
- https://www.nirsoft.net/articles/turn-off-monitor.html / MultiMonitorTool product page — per-monitor checkbox + hotkey + CLI pattern.
- https://github.com/File-New-Project/EarTrumpet (issues/discussions) — unset-target-is-a-no-op convention for audio switching.
- https://andrewlock.net/disabling-localized-satellite-assemblies-during-dotnet-publish/ — `SatelliteResourceLanguages` mechanism and a cited MB-savings figure (single blog source for the exact figure).

### Tertiary (LOW confidence)
- Windows' classic "Keep these display settings?" 15-second auto-revert behavior — well-established from training data, not independently re-verified via a fresh fetch this session; used only as UX-pattern precedent for the manual panel's optional revert-safety-window idea, not as a hard requirement.

---
*Research completed: 2026-08-04*
*Ready for roadmap: yes*
