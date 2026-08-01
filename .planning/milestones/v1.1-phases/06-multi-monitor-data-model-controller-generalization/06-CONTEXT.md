# Phase 6: Multi-Monitor Data Model & Controller Generalization - Context

**Gathered:** 2026-07-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Generalize the monitor data model and controller from v1.0's single "primary monitor to disable" to two independently-configurable sets: monitors to disable and monitors to enable when entering rig mode (mirrored on toggle-back). Covers: `AppSettings`/`MonitorState` shape changes, `IMonitorController`/`WindowsMonitorController` generalization (new enable-set activation logic alongside the existing disable/restore mechanism), Settings UI rework for multi-select, DISPLAY-06 save-time validation, DISPLAY-07 confirmation dialog naming every affected monitor, and a one-time silent migration of genuine v1.0-era `settings.json` files. Does not include the shared reentrancy-safe orchestration helper (Phase 7), tray/hotkey/CLI triggers (Phases 8-10), or toast notifications (Phase 8).

**Completion gate, not optional groundwork** (carried from ROADMAP.md, not re-litigated here): this phase requires a rig-validation checkpoint before being considered done — (a) disable a monitor, sleep/wake or reboot, confirm it re-enables at a sane resolution; (b) apply a combined disable+enable topology in one operation, confirming exactly one GDI primary with no position overlap. Both are go/no-go gates.

</domain>

<decisions>
## Implementation Decisions

### Enable-Set Activation (DISPLAY-05)
- **D-01:** A monitor in the enable-set that's currently OS-disabled gets activated via auto-extend placement at its native/preferred resolution — the same "let CCD's Extend-topology mechanism decide placement" approach `WindowsMonitorController.Restore`'s crash-recovery fallback already uses (`PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, ...)`). No manual position/resolution configuration UI. Reasoning: this is a single-user personal tool: Windows' own default placement is good enough, and adding a position-picker UI is scope the user explicitly doesn't want.
- **D-02:** On toggle-back, every enable-set monitor is unconditionally returned to OS-disabled — not routed through the general "restore whatever the snapshot says" mechanism. This is deliberately asymmetric from how the disable-set's survivors are restored (which does replay the captured snapshot). Rationale: an enable-set monitor is disabled by definition of being in that set: entering rig mode is what activates it, so toggle-back's job is simply to undo that activation, not to consult a snapshot that (by construction) never captured it as active in the first place.

### Settings Selection UI
- **D-03:** Settings' monitor section becomes a `DataGridView`-style grid: one row per enumerated monitor (friendly name), with two checkbox columns, "Disable" and "Enable" — replacing the single `cboMonitor` dropdown. Ties both sets visually to the same monitor list rather than two disconnected pickers.
- **D-04:** A monitor cannot be checked in both columns simultaneously — enforced live in the UI (checking one column for a row automatically prevents/unchecks the other for that same row), not just caught at Save time. Prevents an unresolvable config from ever being expressible, rather than allowing it and then blocking Save with an error message.

### Validation & Confirmation Wording (DISPLAY-06, DISPLAY-07)
- **D-05:** DISPLAY-06's "don't allow disabling every monitor" check counts enable-set monitors as "staying active" — the real check is "will at least one monitor be active once the rig-mode topology is fully applied" (disable-set removed, enable-set added), not just "is every currently-active monitor in the disable-set." A config that disables every currently-active monitor but enables another (e.g. an all-rig-monitor desk swap) is valid and must not be blocked.
- **D-06:** The confirmation dialog (DISPLAY-07) always spells out every affected monitor's full friendly name in a comma-separated list — no truncation/"and N more" logic. E.g. `This will disable "Dell U2720Q", "LG UltraGear" and enable "Rig Monitor". Continue?` A personal rig never has enough monitors for length to be a real concern, so truncation logic is unneeded complexity.

### Required Fields & Migration (DISPLAY-08)
- **D-07:** `IsFullyConfigured` (`ToggleService.cs`) no longer requires a non-empty disable-set specifically — it requires disable-set **OR** enable-set to be non-empty (at least one monitor-set action configured), plus both audio devices and the companion app path as before. This generalizes past v1.0's implicit "rig mode always disables exactly one monitor" assumption — an enable-only configuration (e.g. bringing up a 3rd monitor with nothing to disable) is a legitimate use of this generalized model.
- **D-08:** The v1.0 → v1.1 settings migration (`AppSettings.MonitorDevicePath` → the new `MonitorsToDisable` set) is fully silent. A genuine v1.0-era `settings.json` loads, the legacy field maps automatically into the new disable-set, and Settings simply shows that monitor already checked in the Disable column on next open — no dialog, no toast, no one-time banner. This is the literal reading of DISPLAY-08 ("no re-configuration required"), and avoids new UI-state tracking for a one-time event.

### Claude's Discretion
- Exact migration mechanism (in `JsonSettingsStore.Load()` itself, vs. a separate migration step in the composition root) — implementation detail, left to planner.
- Exact `DataGridView` column/control configuration (checkbox column types, row height, sizing) to achieve D-03/D-04 — left to planner/executor.
- Whether `MonitorState`/`AppSettings` represent the enable-set as a `List<string>` of device paths directly or a small wrapper type — left to planner, consistent with the existing `MonitorsToDisable`-style naming already anticipated in STATE.md's decision log.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level
- `.planning/PROJECT.md` — core value, v1.1 milestone goal, constraints (stable `DevicePath` matching, no index/position-keyed sets)
- `.planning/REQUIREMENTS.md` — DISPLAY-04/05/06/07/08 (mapped to this phase), and the Out of Scope table's explicit ban on index/position-keyed monitor sets
- `.planning/ROADMAP.md` — Phase 6 section: success criteria, the mandatory rig-validation completion gate (long-idle/reboot re-enable + combined disable+enable topology), and the `IsFullyConfigured` open question this discussion resolved as D-07
- `.planning/STATE.md` — v1.1 roadmap decisions: Phase 6 sequenced first because it changes shapes every later trigger path depends on; requires its own rig-validation checkpoint as a completion gate

### Prior phases (monitor control precedent)
- `.planning/milestones/v1.0-phases/04-monitor-control-production/04-CONTEXT.md` — D-01/D-02 (confirmation dialog persistence + reset-on-monitor-change pattern this phase's D-06 extends to multiple monitors), D-03/D-04 (verify-and-throw against `WindowsDisplayAPI`'s own re-query, never `Screen.AllScreens`) — this phase's enable-set activation must follow the same verification discipline
- `.planning/milestones/v1.0-phases/02-foundations-gui-shell/02-CONTEXT.md` — D-05 (real enumeration, not placeholder data), D-10 (stale-saved-device UI pattern — unselected + inline warning), D-12 (Save blocked until validly configured) — the multi-select grid must preserve these existing UX guarantees, generalized to sets
- `.planning/milestones/v1.0-phases/05-orchestration-full-toggle-packaging/05-CONTEXT.md` — structured per-step `ToggleResult` reporting pattern; this phase's monitor step remains a single "Monitor" step in that reporting shape (disable+enable both happen within it), not split into two

### Existing code (this phase's actual surface area)
- `src/RigToggle.Core/Models/AppSettings.cs` — `MonitorDevicePath`/`MonitorFriendlyName` (legacy singular fields, migration source) need new plural fields alongside/replacing them
- `src/RigToggle.Core/Models/MonitorState.cs`, `MonitorPathSnapshot.cs` — snapshot shape; D-01/D-02 mean enable-set monitors are handled by the toggle-back-always-disables rule (D-02), not by extending `MonitorState.Paths` capture semantics
- `src/RigToggle.Core/Models/MonitorInfo.cs` — enumeration DTO, `DevicePath` is the stable identifier (unchanged)
- `src/RigToggle.Core/Abstractions/IMonitorController.cs` — `Disable(string)`/`Restore(MonitorState)` signatures likely need to become set-aware (e.g. `Disable(IReadOnlySet<string>)` + an enable-set parameter, or a combined-topology method) — planner's call on exact shape
- `src/RigToggle.Core/ToggleService.cs` — `IsFullyConfigured` (line 176-180, this phase's D-07), `ToggleToRigMode`'s single "Monitor" `TryExecuteStep` call site (line 82)
- `src/RigToggle.Windows/WindowsMonitorController.cs` — `Disable`/`Restore` real CCD implementation; `Restore`'s existing `ApplyTopology(Extend)` crash-recovery fallback (lines 263-282) is the direct precedent/reusable mechanism for D-01's enable-set activation
- `src/RigToggle.App/SettingsForm.cs` — `cboMonitor` (lines 59-117) is replaced per D-03/D-04; `ValidateSettingsForm`/`BtnSaveSettings_Click` need the new set-aware validation (D-05) and migration-aware defaulting (D-08)
- `src/RigToggle.App/MonitorConfirmDialog.cs` — `lblMessage.Text` single-monitor string (line 17) needs the multi-monitor list format per D-06
- `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` — `Load()` (lines 27-51) is the natural home for the silent migration (D-08), consistent with its existing "degrade gracefully, never throw" pattern

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WindowsMonitorController.Restore`'s `ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false)` crash-recovery fallback (lines 263-282) — already-proven mechanism for "bring an inactive/disabled path back to some active state without manually reconstructing mode info," directly reusable for D-01's enable-set activation.
- `WindowsMonitorController.Disable`'s survivor-repositioning idiom (lines 132-158: reuse real live `PathInfo`/`TargetsInfo` objects wholesale, only touch `Position`) — the established pattern for any new "combine disable-set removal + enable-set addition into one `ApplyPathInfos` call" logic this phase needs for the roadmap's "combined topology in one atomic operation" gate.
- `SettingsForm`'s existing `PopulateAudioCombo`/`ShowStaleWarning` stale-device pattern (D-10 precedent) — apply the same "saved-but-not-currently-enumerated" handling per-row in the new grid.

### Established Patterns
- Interface-per-concern + verify-and-throw after every mutating CCD call (Phase 4 D-03/D-04) — the enable-set activation path must re-query and verify exactly like `Disable`/`Restore` already do, never trusting a non-throwing return alone.
- Stable `DevicePath` as the only identifier for matching/persisting monitor identity — explicitly required by REQUIREMENTS.md's Out of Scope table (already burned once in v1.0 on index-based matching); the new sets must be `DevicePath`-keyed, same as `MonitorDevicePath` today.
- XML-doc rationale comments explaining *why*, not *what* — continue this convention for the new D-02 asymmetry (enable-set always re-disables vs. disable-set's snapshot-based restore), matching how the existing rig-mode-vs-normal-mode stop-vs-continue asymmetry (Phase 5 D-05) is documented inline so it isn't "corrected" into false symmetry later.

### Integration Points
- `SettingsForm`'s three-section layout (Monitor / Audio / App path, D-09 from Phase 2) is preserved — only the Monitor section's control changes from a single ComboBox to the D-03 grid.
- `MainForm`'s pre-disable confirmation call site (constructs `MonitorConfirmDialog` today) needs updating to pass the full disable-set and enable-set friendly names instead of a single monitor name.
- `ToggleService.ToggleToRigMode`'s single `TryExecuteStep("Monitor", ...)` call (line 82) stays a single step in the `ToggleResult` checklist — the disable+enable combination happens inside one controller call, consistent with Phase 5's per-step (not per-sub-action) reporting granularity.

</code_context>

<specifics>
## Specific Ideas

- Confirmation dialog format for multiple monitors: `This will disable "X", "Y" and enable "Z". Continue?` — always full names, comma-separated, no truncation (D-06).
- Grid-based Settings UI: one row per monitor, "Disable" and "Enable" checkbox columns, mutual exclusivity enforced live per row (D-03/D-04).
- Migration is invisible: a v1.0 user just sees their existing monitor already checked in the "Disable" column the first time they open the new Settings grid (D-08).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. No scope-creep items came up (e.g. no manual position/arrangement UI was pursued past D-01's explicit "auto-extend is good enough" decision).

</deferred>

---

*Phase: 6-Multi-Monitor-Data-Model-Controller-Generalization*
*Context gathered: 2026-07-28*
