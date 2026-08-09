# Phase 15: Optional App & Audio Targets - Context

**Gathered:** 2026-08-04
**Status:** Ready for planning

<domain>
## Phase Boundary

The companion-app launch target and both audio-device settings (Rig-mode, Normal-mode) become genuinely optional: leaving any of them unset causes toggle to skip that step cleanly with no error, in either direction. A target that IS configured but has since gone bad (missing file, removed audio device) must still surface as a real failure — never silently collapsed into the same "skipped" outcome as "never configured." Does not touch monitor configuration behavior (Phase 16), the manual monitor panel (Phase 17), or exe size/cleanup (Phase 18) — this phase is the App/Audio optionality boundary only. Requirements: APP-04, APP-05, AUDIO-03, AUDIO-04, AUDIO-05.

</domain>

<decisions>
## Implementation Decisions

### Unset Affordance (Settings UI)
- **D-01:** The companion-app path field (`txtAppPath`, a read-only textbox today, set only via Browse or drag-drop with no way to clear it) gets an explicit **Clear button** next to it — enabled only when a path is currently set. Matches the existing Browse button's affordance style; explicit and discoverable, not a hidden context-menu action.
- **D-02:** Each audio-device dropdown (`cboAudioNormal`, `cboAudioRig`) gets an explicit **"(None — don't switch audio)"** list item prepended to the real detected-device entries, rather than allowing a blank/`SelectedIndex = -1` state to mean unset. A deliberate list entry reads as an intentional choice; a blank dropdown looks like an unfinished form.

### Skipped-Step Outcome (Toggle Result)
- **D-03:** `ToggleStepResult`/`ToggleStepOutcome` gains a new, distinct **`Skipped`** outcome — not a reuse of `NotAttempted` with different text. `NotAttempted` continues to mean "blocked because an earlier step in the same toggle failed" (stop-on-first-failure, D-04 in ToggleService.cs); `Skipped` means "the user deliberately left this target unconfigured." These must never look the same to a future reader of the result, matching this codebase's existing "don't collapse two different states into one" discipline (already applied to the App/Audio configured-vs-missing distinction).
- **D-04:** The toggle-result step list always contains all 3 entries (Monitor/Audio/App) on every toggle, regardless of what's configured — an unset step reads "Skipped (not configured)" rather than being omitted from the list. Keeps the checklist shape consistent toggle-to-toggle; whatever downstream formatter/MessageBox/tray-balloon logic currently renders the 3-row checklist should handle `Skipped` as a distinct, non-alarming visual state (not styled like `Failed`).

### Toggle-Readiness & Save Gating
- **D-05:** `ToggleService.IsFullyConfigured`/`IsSettingsConfigured` (ToggleService.cs:201-205) drops the `NormalAudioDeviceId`/`RigAudioDeviceId`/`CompanionAppPath` required-field terms entirely — only the existing monitor-set check (`MonitorsToDisable?.Count > 0 || MonitorsToEnable?.Count > 0`, D-07) gates whether "Switch to Rig Mode" is enabled at all. Audio/App being unset never blocks toggling in either direction.
- **D-06:** `SettingsForm.ValidateSettingsForm`/`btnSaveSettings.Enabled` (SettingsForm.cs:634-688) is relaxed the same way: Save is enabled once the monitor grid validates, regardless of whether audio/app are set. A **configured-but-broken** audio device or app path (stale-path/stale-device warning already shown via `lblAppWarning`/`lblAudioNormalWarning`/`lblAudioRigWarning`) still blocks Save, consistent with "broken ≠ unset" — only a field that is cleanly unset (via the D-01/D-02 affordances) bypasses validation.

### Broken-Target Error Messages
- **D-07:** Audio gets the same friendly, actionable toggle-time failure message pattern the app path already has (ToggleService.cs:76-77's `"The companion app could not be found at '{path}'. Open Settings and reselect..."`). New wording: something like `"The configured Rig/Normal-mode audio device could not be found. Open Settings and reselect it."` — applied per-direction, replacing whatever raw NAudio/IPolicyConfig exception message would otherwise surface. Exact wording is Claude's discretion at implementation time (see below), but the tone/actionability/one-sentence-with-a-fix-instruction shape must match the app-path precedent.

### Claude's Discretion
- Exact enum/property shape for the new `Skipped` outcome (new `ToggleStepOutcome` case vs. some other representation) — implementation detail, not a vision decision.
- Exact wording of the new audio-device-not-found message (D-07) — must match the app-path message's tone and one-sentence-plus-fix-instruction shape, but precise phrasing is not locked.
- Exact placement/styling of the app-path Clear button and the audio dropdowns' "(None...)" list item — visual layout is Claude's call, following the app's existing Settings-form conventions (button style matching `btnBrowse`, list-item styling matching real device entries minus a device icon/detail).
- Whether audio-device-not-found detection happens via a pre-flight existence check (mirroring `File.Exists` for the app path) or by catching `SetDefault`'s own exception and re-wrapping the message — left to research/planning; NAudio's `MMDeviceEnumerator` likely offers a cheap existence check worth investigating.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & scope decisions
- `.planning/REQUIREMENTS.md` (APP-04, APP-05, AUDIO-03, AUDIO-04, AUDIO-05 — mapped to this phase; also see the "Out of Scope" table entry rejecting independent per-direction app-launch opt-in)
- `.planning/ROADMAP.md` (Phase 15 section — goal, success criteria, depends on nothing)
- `.planning/PROJECT.md` (Current Milestone: v2.0 section — full target-feature list and key context; Requirements > Validated section for existing App/Audio/Monitor behavior this phase must not regress)

### Research (this milestone — v2.0 sections specifically)
- `.planning/research/SUMMARY.md` — synthesized findings; Phase 15 rationale ("lowest risk, no prerequisites")
- `.planning/research/FEATURES.md` — Features #1 (Optional App Launch Target) and #2 (Optional Audio Devices Per Direction) sections, including the cross-cutting edge-case table (configured-but-broken vs. never-configured distinction) and the critical architectural finding that `NormalAudioDeviceId` is collected today but never read by `ToggleToNormalMode` (this phase's AUDIO-04 must give it real effect)
- `.planning/research/ARCHITECTURE.md` — v2.0 component/integration-point analysis
- `.planning/research/PITFALLS.md` — Pitfall 3 (silent-skip masking a real failure) and Pitfall 8 (two validation gates — `SettingsForm` and `ToggleService.IsFullyConfigured` — drifting out of sync) apply directly to this phase's D-05/D-06

### Existing code (this phase's actual surface area)
- `src/RigToggle.Core/ToggleService.cs` — `IsFullyConfigured` (lines 201-205), `ToggleToRigMode`'s app-path preflight (lines 70-78) and Audio step (line 140), `ToggleToNormalMode`'s Audio restore (line 306, currently 100% snapshot-based — must change per AUDIO-04), `IsSettingsConfigured` (line 195)
- `src/RigToggle.Core/Models/AppSettings.cs` — already 100% nullable-by-design; no new fields needed for this phase, no migration guard needed
- `src/RigToggle.App/SettingsForm.cs` — `ValidateSettingsForm` (lines 634-688), `PopulateAppPathField`/`ShowStaleWarning` (lines 582-612), `PopulateAudioCombo` (line 534), `BtnSaveSettings_Click`'s defensive re-validation (lines 764-778)
- `src/RigToggle.App/SettingsForm.Designer.cs` — `txtAppPath` confirmed `ReadOnly = true` (line 295) — the reason D-01's Clear button is needed rather than allowing direct text deletion

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ShowStaleWarning`/`lblAppWarning`/`lblAudioNormalWarning`/`lblAudioRigWarning` (SettingsForm.cs) — the existing "configured but now broken" warning pattern; the new audio toggle-time error message (D-07) should read consistently with these Settings-time warnings, not introduce new terminology.
- `PickerItem` record (SettingsForm.cs:51) — the existing audio-device dropdown item shape; the new "(None...)" entry (D-02) is a natural extension of this same type (e.g. a sentinel `PickerItem(null, "(None — don't switch audio)")` or equivalent), not a new UI control.

### Established Patterns
- "Never collapse two different states into one" — already the codebase's explicit convention for the app-path missing-vs-unset distinction (ToggleService.cs comments); this phase extends the same discipline to audio and to the new Skipped/NotAttempted split.
- Fail-fast preflight before any mutation (ToggleToRigMode's app-path check happens before `CaptureState()`/`Save()`) — the audio "configured but broken" check (D-07) should likely follow the same preflight shape for consistency, though the exact mechanism (preflight vs. catch-and-rewrap) is left to research/planning per Claude's Discretion above.

### Integration Points
- `ToggleService.IsFullyConfigured` and `SettingsForm.ValidateSettingsForm` are two independently-maintained validation gates that must be changed in lockstep (Pitfall 8) — both need the same D-05/D-06 relaxation applied together, not just one of them.
- `ToggleService.ToggleToNormalMode`'s Audio step currently calls `_audioController.Restore(snapshot.Audio)` unconditionally — AUDIO-04 requires this become `_audioController.SetDefault(settings.NormalAudioDeviceId)` when set (mirroring the Rig-mode path), skipped when unset. This phase delivers that runtime-effect change; note it's a real behavior change to existing code, not just a validation relaxation.

</code_context>

<specifics>
## Specific Ideas

- The Clear button for the app path should look and feel like a natural sibling to the existing Browse button (same row, consistent styling) — not a separate/hidden mechanism.
- The audio dropdown "None" entry should read clearly as a deliberate choice, e.g. "(None — don't switch audio)" rather than just "None" or a blank string, so it's unambiguous in a device list that otherwise shows real hardware names.
- Audio error messages should mirror the app-path message's exact tone: one sentence stating what's wrong, one sentence telling the user what to do about it ("Open Settings and reselect...").

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 15-Optional-App-Audio-Targets*
*Context gathered: 2026-08-04*
