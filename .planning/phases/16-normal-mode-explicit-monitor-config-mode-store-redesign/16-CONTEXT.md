# Phase 16: Normal-Mode Explicit Monitor Config & Mode-Store Redesign - Context

**Gathered:** 2026-08-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Normal mode gains its own explicit monitor target set (`NormalMonitorsToDisable`/`NormalMonitorsToEnable` or equivalent), symmetric to Rig mode's existing `MonitorsToDisable`/`MonitorsToEnable`, and `ToggleToNormalMode`'s Monitor step is rewritten to apply that explicit set directly instead of restoring from the pre-toggle snapshot. Because this removes the last consumer of `ISnapshotStore` for monitor restore, the app's current mode (Rig vs. Normal) can no longer be derived from snapshot-file presence (today's `IsInRigMode() == _snapshotStore.Exists()`) — this phase also introduces an explicit, independently-persisted mode flag/store (`IModeStore` or equivalent) that every mode-dependent consumer (MainForm label/tray/tooltip, toggle routing, crash-recovery messaging) reads instead. A lightweight "toggle in progress" marker is added so a crash mid-toggle can be detected and communicated to the user on next launch (DISPLAY-13). Does not touch the manual monitor panel (Phase 17), the shared safety-guard unification across all three mutation paths (Phase 17/DISPLAY-12), or dead-code removal / exe-size reduction (Phase 18 — the old snapshot/restore code becomes unused here but its deletion is explicitly Phase 18 scope). Requirements: DISPLAY-09, DISPLAY-10, DISPLAY-11, DISPLAY-13.

</domain>

<decisions>
## Implementation Decisions

### Normal-Mode Monitor Set Semantics
- **D-01:** A monitor not listed in either the Normal-mode disable-set or enable-set is left untouched on toggle-to-Normal — this mirrors Rig mode's existing `MonitorsToDisable`/`MonitorsToEnable` convention exactly (true symmetry, no new mental model). Per FEATURES.md's explicit warning, this must be a documented default, not silent snapshot-fallback behavior: if a monitor isn't mentioned in either Normal set, nothing about it changes during that toggle.

### Crash-Recovery UX (DISPLAY-13)
- **D-02:** When the app detects, on next launch, that the "toggle in progress" marker was left behind by a crash mid-toggle, it shows a **blocking dialog at startup** (not a passive banner, not silent-log-only) stating the last toggle didn't finish cleanly, which mode it was heading to, and that the marker has been cleared. Matches this app's established pattern of always surfacing toggle-relevant failures via MessageBox rather than silent logging, and catches the problem before the user might start a second toggle on top of an unknown state.
- **D-03:** The dialog is **inform-only** — no inline "retry the toggle" action. The user manually verifies their monitor/audio state and re-toggles if needed. Avoids blindly repeating whatever action may have caused the crash, and matches how this app's other failure paths already surface (MessageBox + manual user action, no auto-retry anywhere in the codebase today).

### Settings UI Layout (Normal-Mode Monitor Grid)
- **D-04:** The new Normal-mode monitor picker is a **second grid stacked directly below** the existing Rig-mode grid in `SettingsForm`, same width, clearly labeled "Normal Mode." Both configs stay visible simultaneously (no tab, no side-by-side columns) so the user can keep the two symmetric sets mentally in sync while editing either one.
- **D-05:** The new grid mirrors the Rig grid's established column-header + explanation-label convention **exactly** — column headers read "Off (Normal)/On (Normal)" (matching the existing "Off (Rig)/On (Rig)" pattern from the prior quick-task clarity fix), with its own permanent explanation label underneath, not a single shared explanation spanning both grids.

### Mode-Marker Corruption Fallback
- **D-06:** If the new persisted mode flag is missing or corrupted on launch, the app **fails loudly** — it does not silently default to Normal mode. This matches the existing corrupted-snapshot precedent (`ToggleToNormalMode` already throws a descriptive exception rather than guessing when `state.json` is corrupted) and this project's established "never silently guess state" discipline (directly addresses Pitfall 4/5's warning about mode indication silently going wrong).
- **D-07:** This corruption check fires **at app startup** (not deferred until a toggle is attempted), and produces a **blocking dialog** explaining the mode is unknown and asking the user to verify their monitors/audio manually before using Toggle. Catches the problem at the earliest possible moment, before any toggle action risks compounding an already-unknown state.

### Claude's Discretion
- Exact shape of the mode-store abstraction (`IModeStore` interface, its file format, whether it's a new JSON file or repurposes the existing snapshot file's location) — implementation detail; research (PITFALLS.md Pitfall 4) already establishes it must be file-backed (not in-memory-only) so mode reads correctly after a process restart, not just during a live session.
- Whether the "toggle in progress" marker (DISPLAY-13) is a separate file from the mode flag or folded into the same store — left to research/planning; both must independently survive a crash, but their exact on-disk relationship is an implementation choice.
- Preserving the CR-01 "verify nothing actually changed before trusting the mode flag" safety net (PITFALLS.md Pitfall 5) when Monitor-step failure handling is rewritten against the new `IModeStore` — the *requirement* to preserve this check is locked (do not drop it), but the exact code shape is Claude's call at implementation time.
- Exact wording of the crash-recovery dialog (D-02/D-03) and the mode-corruption dialog (D-06/D-07) — must match the tone/actionability established by existing error messages (one clear statement of what's wrong, one instruction on what to do), but precise phrasing is not locked.
- Whether `AppSettings` gains flat `NormalMonitorsToDisable`/`NormalMonitorsToEnable` fields or a nested `MonitorTarget`-shaped structure reused for both Rig and Normal — FEATURES.md flags this as an open data-model choice; either satisfies D-01/D-04/D-05, left to planning.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & scope decisions
- `.planning/REQUIREMENTS.md` (DISPLAY-09, DISPLAY-10, DISPLAY-11, DISPLAY-13 — mapped to this phase; also see the Out of Scope table's "no smart migration" entry — new Normal-mode fields stay null until the user visits Settings, no synthesizing from the retired snapshot)
- `.planning/ROADMAP.md` (Phase 16 section — goal, success criteria, depends on Phase 15 per risk-ordering, not a hard architectural dependency)
- `.planning/PROJECT.md` (Current Milestone: v2.0 section — this phase is explicitly called out as revising the Core Value's "restores exactly how it was before" framing for Normal mode)

### Research (this milestone — Phase 16/"Phase 2" sections specifically)
- `.planning/research/SUMMARY.md` §"Phase 2: Normal-Mode Explicit Monitor Config + Mode-Store Redesign" — what this phase delivers, uses, implements, avoids
- `.planning/research/FEATURES.md` §"3. Normal Mode Explicit Monitor Target Set" — table-stakes behaviors, anti-features (no snapshot-fallback, no smart migration), the critical mode-detection architectural dependency, and open data-model questions
- `.planning/research/ARCHITECTURE.md` — v2.0 component/integration-point analysis
- `.planning/research/PITFALLS.md` — Pitfall 4 (mode detection breaking silently once snapshot dependency is removed — this phase's central risk), Pitfall 5 (losing the CR-01 recapture-and-compare safety net during the rewrite), Pitfall 6 (null-vs-empty migration-guard bug reintroduced for the new fields), Pitfall 7 (stale "before switching to Rig Mode" error text once `DeactivateMonitors` gets a second caller)

### Existing code (this phase's actual surface area)
- `src/RigToggle.Core/ToggleService.cs` — `IsInRigMode()` (line 456, currently `_snapshotStore.Exists()`), `ToggleToNormalMode()` (lines 299-450, entire control-flow spine is snapshot-null-branching today — genuine rewrite not a patch), `ToggleToRigMode()`'s CR-01 recapture-and-compare logic (lines 110-134) that must be preserved in spirit against the new mode store
- `src/RigToggle.Core/Abstractions/ISnapshotStore.cs` — current mode-detection contract (`Exists()`/`Save()`/`Load()`/`Clear()`), the interface this phase's `IModeStore` (or repurposed equivalent) replaces
- `src/RigToggle.Core/Models/AppSettings.cs` — `MonitorsToDisable`/`MonitorsToEnable` (the existing Rig-mode fields the new Normal-mode fields mirror), all-nullable-by-design convention, migration-guard discipline (null-only checks, never null-or-empty per the Pitfall 6 precedent)
- `src/RigToggle.App/SettingsForm.cs` / `SettingsForm.Designer.cs` — the existing Rig-mode monitor grid (column headers, permanent explanation label from the prior quick-task fix `260728-rmp`) that the new Normal-mode grid mirrors per D-04/D-05
- `src/RigToggle.App/MainForm.cs` — `RefreshUi()` (mode label/tray icon/tooltip, ~line 257 onward) and the toggle-back guard before calling `ToggleToNormalMode()`, both currently reading `IsInRigMode()` and needing to read the new mode store instead; the crash-recovery dialog (D-02/D-03) and mode-corruption dialog (D-06/D-07) are new startup-path additions here

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WindowsDisplayAPI`'s `ActivateMonitors`/`DeactivateMonitors` (already generalized to arbitrary sets since Phase 6) — the Normal-mode Monitor step reuses these directly, zero interface changes needed; same ordering constraint applies (Activate before Deactivate, per 06-RESEARCH.md Pitfall 2).
- The existing Rig-mode monitor grid UI (`SettingsForm.Designer.cs`) — D-04/D-05 lock this as the direct template for the new Normal-mode grid, including its column-header wording pattern and explanation-label convention.
- `System.Text.Json`'s existing nullable-field round-tripping (`AppSettings` is already 100% nullable-by-design) — the new Normal-mode fields follow the identical pattern, no new serialization work needed.

### Established Patterns
- "Never collapse two different states into one" — this codebase's explicit convention (already applied in Phase 15 to Skipped/Failed and NotAttempted); this phase applies the same discipline to the mode flag itself — a corrupted/missing mode marker (D-06) must fail loudly, never silently collapse into "assume Normal."
- Fail-fast, surface-via-MessageBox for anything toggle-relevant — established throughout ToggleService/MainForm; D-02/D-03/D-06/D-07 all extend this same precedent to the two new startup-time dialogs.
- CR-01's "verify nothing actually changed before trusting the state" pattern (ToggleService.cs lines 110-134) — timeless reasoning that must survive the snapshot-to-mode-store rewrite even though its current code is snapshot-specific (PITFALLS.md Pitfall 5).

### Integration Points
- `MainForm.RefreshUi()` and the toggle-back guard are the two places currently reading `IsInRigMode()` that must switch to the new mode store.
- The existing "Cannot disable all configured monitors — at least one active display must remain" safety-guard error text (`WindowsMonitorController.cs:307`) is currently only reachable from the Rig-mode disable path; it must also guard the new Normal-mode apply path once that's added (full three-way unification across Rig/Normal/manual-panel is Phase 17/DISPLAY-12 scope, but Normal mode's own path must not bypass this guard in the meantime).
- `StateSnapshot`/`MonitorState`-as-restore-payload and `WindowsMonitorController.Restore()`/`RestoreViaReconstruction()` become dead code once this phase ships — left in place per REQUIREMENTS.md's CLEANUP-01 (Phase 18 scope), not deleted here.

</code_context>

<specifics>
## Specific Ideas

- The crash-recovery dialog and the mode-corruption dialog should both read in the same one-sentence-problem, one-sentence-next-step shape as this app's existing failure messages (e.g. the app-path-not-found and audio-device-not-found messages from Phase 15) — no exact wording locked, but the tone must match.
- The Normal-mode grid's explanation label should read as a natural sibling to the Rig-mode grid's existing label, not introduce new terminology for the same "off/on set" concept.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 16-Normal-Mode-Explicit-Monitor-Config-Mode-Store-Redesign*
*Context gathered: 2026-08-04*
