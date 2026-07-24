# Phase 4: Monitor Control (Production) - Context

**Gathered:** 2026-07-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace Phase 2's fake `IMonitorController.Disable`/`Restore` mutation stubs with real CCD-level topology-path-removal, using the mechanism validated by Phase 1's spike on this rig's actual AMD Radeon/DisplayPort hardware. Add the new DISPLAY-03 confirmation dialog before disabling. Enumeration (`GetActiveMonitors`) is already real from Phase 2 — this phase only touches the mutation path plus a new confirmation UX in the toggle flow.

**Critical de-risking fact from Phase 1's spike** (recorded at `spike/RESULTS-TEMPLATE.md`, not yet reflected in `.planning/STATE.md`'s Blockers section as of this discussion — flag for the orchestrator to reconcile): the spike's actual result is **GO**, but with a specific caveat. Disabling a *non-primary* monitor via `PathInfo.ApplyPathInfos()` (non-elevated, `allowChanges: true`) worked cleanly. Disabling the *primary* monitor specifically threw `WindowsDisplayAPI.Exceptions.PathChangeException: Invalid paths information` on two separate, reproducible attempts — root cause: Windows requires exactly one active path positioned at (0,0), and `SDC_ALLOW_CHANGES` does not auto-reposition remaining paths for this specific case. `WindowsDisplayAPI.PathInfo.Position` has no public setter, so the spike's naive path-array approach cannot self-correct. This is the primary technical risk for this phase and was explicitly anticipated in `01-RESEARCH.md`'s Pitfall B / Assumption A2 — those are now confirmed against real hardware, not hypothetical.

</domain>

<decisions>
## Implementation Decisions

### Confirmation Dialog (DISPLAY-03)
- **D-01:** The confirmation dialog is shown once, not on every toggle — with a "don't ask again" checkbox that persists to settings (`AppSettings`/`settings.json`), not a per-toggle MessageBox.
- **D-02:** The remembered "don't ask again" preference resets automatically if the configured monitor (device path) changes in Settings — the user gets exactly one fresh confirmation naming the newly-configured monitor, never a stale consent silently carried over to a different display.

### Verification Strictness (mirrors Phase 3's audio verify-and-throw pattern)
- **D-03:** After `Disable`/`Restore` applies a topology change, re-query the actual resulting display state via `WindowsDisplayAPI` (`PathInfo.GetActivePaths()`) and confirm it matches the expected topology (monitor genuinely gone / genuinely restored). Throw a clear error if it doesn't match — do not trust `ApplyPathInfos()`'s non-throwing return alone as proof of success. Directly motivated by the spike's own Finding 2 (Screen.AllScreens staleness — an oracle that can look "unchanged" immediately after a real, successful change) and Finding 3 (primary-removal validation failures) — this project's core value is a display that is *genuinely* absent/restored, not one that merely didn't error.
- **D-04:** Per spike Finding 2, `Screen.AllScreens` is NOT the verification oracle for this check — `WindowsDisplayAPI`'s own re-query (`PathInfo.GetActivePaths()`) is authoritative. (Carried into `<specifics>` as a concrete pitfall for research/planner, not re-litigated as a user decision.)

### Failure Path
- **D-05:** When Disable/Restore's verification throws, let the exception bubble up through the existing `MainForm` exception handling — same pattern as Phase 3's audio verify-and-throw (03-CONTEXT.md D-03/D-04). No automatic rollback/re-apply attempt on failure: attempting another `ApplyPathInfos()` call immediately after one already failed validation risks compounding the problem with a second risky mutation. Comprehensive step-by-step failure reporting and recovery is explicitly Phase 5 (CORE-04) scope, consistent with the precedent already set in Phase 3.

### Claude's Discretion
- The exact mechanism for repositioning the remaining display to (0,0) before removing the primary monitor's path (lower-level `DISPLAYCONFIG_SOURCE_MODE` reconstruction, raw P/Invoke of `SetDisplayConfig`/`DisplayConfigGetDeviceInfo`, or another technique) — this is implementation risk for research/planner to resolve, not a user preference. STACK.md's "Alternatives Considered" table already documents raw P/Invoke of `QueryDisplayConfig`/`SetDisplayConfig`/`DisplayConfigGetDeviceInfo` as a fallback if `WindowsDisplayAPI`'s abstraction can't express the needed reconstruction.
- `MonitorState`'s exact snapshot shape (currently Phase-2-minimal: just a device path) — needs enriching to support exact restore (position, primary designation, orientation per DISPLAY-02), following the spike's proven pattern of keeping the full original `PathInfo`/mode-info array in memory and re-applying it wholesale on restore, rather than reconstructing a delta. Left to planner per `02-RESEARCH.md` Pitfall 7's guidance, already flagged in the existing `WindowsMonitorController.cs`/`MonitorState.cs` doc comments.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level
- `.planning/PROJECT.md` — core value, requirements, evolution rules
- `.planning/REQUIREMENTS.md` — DISPLAY-01/02/03 (mapped to this phase)

### Phase 1 (spike) — MOST important refs for this phase
- `spike/RESULTS-TEMPLATE.md` — **the actual recorded go/no-go decision and empirical findings** (GO, with the primary-repositioning caveat). This is the load-bearing artifact for this phase; read it in full, not just the checkbox.
- `.planning/phases/01-monitor-disable-feasibility-spike/01-RESEARCH.md` — Pitfall B (removing primary requires (0,0) repositioning) and Assumption A2 (now empirically confirmed wrong — explicit repositioning IS needed) directly predicted the spike's actual Finding 3
- `.planning/phases/01-monitor-disable-feasibility-spike/01-VERIFICATION.md` — confirms the spike tool itself correctly implements true CCD topology-removal (not power-off), non-elevated by construction; flags that STATE.md's Blockers section is stale relative to the now-filled-in RESULTS-TEMPLATE.md
- `spike/MonitorDetachSpike/Program.cs` — the spike's actual detach/verify/restore code; reusable for the *non-primary* mechanism pattern, but explicitly NOT sufficient as-is for the primary-monitor case (known gap, this phase's job to close)
- `spike/FALLBACK.md` — admin `pnputil` fallback; per the spike record, NOT needed and NOT tested (primary approach's remaining gap is a repositioning bug, not a fundamental mechanism failure) — do not build this into production unless research determines otherwise

### Research (from /gsd:new-project)
- `.planning/research/STACK.md` — `WindowsDisplayAPI` as primary mechanism; documents raw P/Invoke of `QueryDisplayConfig`/`SetDisplayConfig`/`DisplayConfigGetDeviceInfo` as the fallback alternative if the wrapper's abstraction can't express primary-repositioning
- `.planning/research/ARCHITECTURE.md` — `IMonitorController` interface, snapshot-before-mutate pattern

### Prior phases
- `.planning/phases/02-foundations-gui-shell/02-CONTEXT.md` — D-05 (real enumeration in Settings, already done); already flags the "known primary-monitor repositioning gap" as Phase 4 scope in its own code comments
- `.planning/phases/03-app-audio-control/03-CONTEXT.md` — D-03/D-04 (verify-and-throw pattern for audio) is the direct precedent D-03/D-05 in this document extend to monitor control

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/RigToggle.Windows/WindowsMonitorController.cs` — `GetActiveMonitors` is already real (`PathInfo.GetActivePaths()`, `IsGDIPrimary`, `FriendlyName`, `DevicePath`). `Disable`/`Restore` are the documented no-op stubs this phase fills in. Existing doc comments already reference the "known primary-monitor repositioning gap."
- `spike/MonitorDetachSpike/Program.cs` — `RunDisable`/`VerifyOnce`/restore-on-Enter logic demonstrates the working non-primary mechanism and the dual-oracle verification pattern (though the spike itself found `Screen.AllScreens` too strict for an instant check — don't copy that part literally).

### Established Patterns
- Interface-per-concern (`IMonitorController`) with real `RigToggle.Windows` implementation — same pattern as `IAudioController`/`IAppController` from Phase 3.
- Verify-and-throw after a mutating COM/CCD call — direct precedent from Phase 3's `WindowsAudioController.SetDefault`/`ApplyAndVerify` (D-03 in 03-CONTEXT.md), now extended to monitor control per D-03 above.

### Integration Points
- `MonitorState` (`src/RigToggle.Core/Models/MonitorState.cs`) currently holds only `MonitorDevicePath` — needs enriching for exact restore (position/primary/orientation), touching `StateSnapshot`/`JsonSnapshotStore` serialization, similar to Phase 3's `AudioState` reshape (03-01-PLAN.md precedent).
- `IMonitorController`/`WindowsMonitorController` likely need no interface signature changes — same method set, real implementations — unless the primary-repositioning fix requires a different data shape than a single device-path string for `CaptureState`/`Restore`. Planner's call.
- New confirmation dialog integrates into `MainForm`'s toggle-to-rig-mode click handler, before calling `ToggleService.ToggleToRigMode()` — needs a new persisted settings field for the "don't ask again" flag, reset when `AppSettings.MonitorDevicePath` changes in `SettingsForm`.

</code_context>

<specifics>
## Specific Ideas

- Confirmation dialog text should name the specific monitor by friendly name (e.g. "This will disable VG248 (primary). Continue?"), matching the pattern already used for informational/warning MessageBoxes in `MainForm.cs`.
- Verification after Disable/Restore must use `WindowsDisplayAPI`'s own re-query as the authoritative oracle, not `Screen.AllScreens` (per spike Finding 2's documented staleness/caching gotcha).
- The primary-monitor repositioning mechanism itself is NOT decided here — it's explicitly left to research/planner, informed by `spike/RESULTS-TEMPLATE.md` Finding 3's root-cause analysis.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. Automatic rollback-on-failure and comprehensive step-by-step failure reporting are correctly deferred to Phase 5 (CORE-04) per D-05, matching the precedent already set in Phase 3.

</deferred>

---

*Phase: 4-Monitor-Control-Production*
*Context gathered: 2026-07-24*
