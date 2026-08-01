# Phase 9: Global Hotkey Trigger - Context

**Gathered:** 2026-07-31
**Status:** Ready for planning

<domain>
## Phase Boundary

Let the user configure a global Windows keyboard shortcut in Settings that toggles the mode from anywhere — including while the main window is hidden in the tray — with registration failures (e.g. a conflict with Moza Companion or other rig software) surfaced to the user rather than silently swallowed, and defined, non-corrupting behavior if the hotkey fires while Settings is open. Does not include the CLI trigger or single-instance IPC (Phase 10) — this phase only adds the hotkey as a third trigger source alongside the existing GUI button (Phase 5) and tray menu (Phase 8), reusing Phase 7's `ToggleOrchestrator` entry point both already call through.

</domain>

<decisions>
## Implementation Decisions

### Hotkey Capture UI (TRIG-01)
- **D-01:** A single read-only "recording" textbox in Settings, not separate modifier-checkbox + key-dropdown controls. Clicking/focusing it enters capture mode; the next real key combination pressed (modifier(s) + a non-modifier key) is captured and displayed as a friendly string (e.g. "Ctrl+Alt+R"); a bare modifier press alone is not accepted. Escape while capturing clears the field (no hotkey configured). This is the standard, immediately-recognizable "press a shortcut" interaction pattern used throughout Windows software, and needs only one control instead of two combined ones. Exact textbox styling/placement is a UI-SPEC concern (this phase carries `UI hint: yes`).
- **D-02:** No default hotkey is pre-filled — unconfigured/disabled by default, matching the opt-in pattern already established for TRAY-02's "Start with Windows" checkbox. An empty/cleared field means the hotkey trigger is simply off.

### Registration Mechanism & Trigger Handling (TRIG-01)
- **D-03:** `RegisterHotKey`/`UnregisterHotKey` (user32.dll P/Invoke, per CLAUDE.md's explicit guidance) targeting `MainForm`'s window handle, with `WM_HOTKEY` (0x0312) intercepted via a `MainForm.WndProc` override. On receipt, the handler calls `ToggleOrchestrator.ToggleToRigMode()`/`ToggleToNormalMode()` exactly like `TrayToggleMenuItem_Click` already does (same skip-the-GUI-confirmation-dialog posture, same unconditional `ShowBalloonTip` result toast per Phase 8's NOTIF-01 pattern — NOTIF-01's own requirement text already explicitly lists "hotkey" as one of the trigger sources this toast covers).
- **D-04:** Registration is attempted in two places, both calling one shared registration helper: (1) at app startup, right after the composition root loads settings; (2) immediately when the user changes and Saves the hotkey field in Settings, so they get instant conflict feedback rather than waiting for the next app restart.

### Failure Surfacing (TRIG-01)
- **D-05:** At Settings-Save time, a failed registration shows a dedicated inline warning next to the hotkey field (same `err*`/`lbl*Warning` pattern established for TRAY-02's autostart checkbox — a new dedicated pair, not reusing an unrelated section's controls) reading something like "Could not register hotkey — it may already be in use by another application." **Save is NOT blocked** by a registration failure — the user's chosen combination is still persisted as their preference (they may be about to close the conflicting app), but the warning makes clear it isn't currently active.
- **D-06:** At app-startup time (Settings isn't open), a failed registration does not crash the app: it's traced (matching this codebase's established `Trace.WriteLine` convention for swallowed failures) and surfaced via a `NotifyIcon.ShowBalloonTip` warning toast (reusing Phase 8's toast infrastructure) — e.g. "Rig Toggle: the configured hotkey could not be registered (already in use)." — since the user may not have Settings open to see an inline warning at that moment.

### Hotkey vs. Settings-Dialog Race (TRIG-01 success criterion 3)
- **D-07:** The hotkey is explicitly unregistered (`UnregisterHotKey`) when `SettingsForm` opens (`ShowDialog`) and re-registered (via the same D-04 helper, reflecting whatever the user ends up saving or discarding) when it closes. This is simpler and more robust than trying to queue or ignore a mid-edit `WM_HOTKEY` — it guarantees zero possibility of a toggle racing an in-progress Settings edit, satisfying the roadmap's "explicitly suppressed... not left to race" requirement directly.

### Claude's Discretion
- Exact `Keys`/modifier-flag representation stored in `AppSettings` (e.g. a single packed `int`, or separate `Keys` + `KeyModifiers` fields) — left to planner.
- Exact hotkey-ID constant management for `RegisterHotKey`'s required unique ID parameter (a single fixed ID is sufficient since only one hotkey exists) — left to planner.
- Whether the registration helper lives on `MainForm` directly or as a small dedicated class — left to planner, though `MainForm`-hosted is consistent with how Phase 8 hosted the tray/`NotifyIcon` logic directly on the form that owns the window handle `RegisterHotKey` needs.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level
- `.planning/PROJECT.md` — core value, v1.1 milestone goal
- `.planning/REQUIREMENTS.md` — TRIG-01 (mapped to this phase)
- `.planning/ROADMAP.md` — Phase 9 section: goal, success criteria, `UI hint: yes`, and the explicit rig-test note ("verify hotkey registration/conflict behavior with Moza Companion actually running")
- `CLAUDE.md` — "Use `RegisterHotKey`/`UnregisterHotKey` (user32.dll P/Invoke) rather than a hooking library — it's 2 P/Invoke calls and needs a message-pump window (WinForms already gives you one), no extra dependency needed."

### Prior phases (trigger & toast precedent)
- `.planning/phases/07-shared-toggle-orchestration-helper-extraction/07-CONTEXT.md` — D-03/D-04/D-05: `ToggleOrchestrator`'s public surface and busy-rejection exception contract — this phase's hotkey handler is the third caller of that same entry point
- `.planning/phases/08-tray-residency-autostart-toast-notification/08-CONTEXT.md` and `08-REVIEW.md` — D-04/D-05 (dedicated inline-error-pair pattern for a Settings checkbox failure, not reusing an unrelated section's controls — direct precedent for D-05 above), D-07/D-08/D-09 (toast mechanism/content — direct precedent for D-06 above), and the CR-01 lesson (an error-recovery path must not itself throw) — the hotkey registration failure paths in this phase should apply that same lesson
- `.planning/milestones/v1.0-phases/05-orchestration-full-toggle-packaging/05-CONTEXT.md` — `ToggleResult` step-checklist contract the hotkey's toast content (via `ToggleResultFormatter`, Phase 8) must keep reusing verbatim

### Existing code (this phase's actual surface area)
- `src/RigToggle.App/MainForm.cs` — `TrayToggleMenuItem_Click` (the direct pattern to replicate for the hotkey handler: skip GUI confirmation, route all outcomes through `ShowBalloonTip`, never `MessageBox`), `OpenSettingsDialog()` (needs to gain the unregister/re-register bracketing per D-07)
- `src/RigToggle.App/SettingsForm.cs` / `.Designer.cs` — `chkStartWithWindows` + `lblAutostartWarning`/`errAutostart` (direct analog for the new hotkey field + its own dedicated warning pair)
- `src/RigToggle.Core/Models/AppSettings.cs` — needs new field(s) for the persisted hotkey combination
- `src/RigToggle.App/Program.cs` — composition root; needs the startup-time registration call (D-04) alongside existing `InitializeTrayState()`-style unconditional priming

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MainForm`'s existing tray-toggle handler (`TrayToggleMenuItem_Click`) — the hotkey handler is structurally almost identical (same orchestrator call, same toast-only error surfacing), just triggered by `WM_HOTKEY` instead of a menu click.
- `SettingsForm`'s dedicated-inline-error-pair pattern (`lblAutostartWarning`/`errAutostart`, Phase 8) — exact template for the new hotkey-registration-failure warning (D-05).
- `RigToggle.Core.ToggleResultFormatter` (Phase 8) — the toast content for a hotkey-triggered toggle reuses this unchanged.

### Established Patterns
- Interface-per-concern kept minimal: this phase's only genuinely new Windows-specific surface is the `RegisterHotKey`/`UnregisterHotKey`/`WM_HOTKEY` P/Invoke trio, hand-rolled per CLAUDE.md guidance (no new library).
- XML-doc rationale comments explaining *why* — continue for D-07's unregister-during-Settings choice and D-05's non-blocking-Save decision, so a future reader doesn't "fix" either into something more complex.

### Integration Points
- `MainForm`'s `WndProc` override is new surface — must call `base.WndProc(m)` for all non-`WM_HOTKEY` messages, and must not interfere with any existing WinForms message handling (focus, tray icon messages, etc.).
- `Program.cs`'s composition root gains the startup registration call.
- `SettingsForm`'s Save flow gains the immediate re-registration attempt (D-04) alongside its existing autostart-write and settings-persistence steps.

</code_context>

<specifics>
## Specific Ideas

- Hotkey field UX: read-only textbox, click to "recording..." state, next combo pressed becomes the displayed value (e.g. "Ctrl+Alt+R"), Escape clears it.
- Startup failure toast wording: "Rig Toggle: the configured hotkey could not be registered (already in use)."
- Settings inline warning wording: "Could not register hotkey — it may already be in use by another application."

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. The CLI trigger and single-instance IPC remain correctly scoped to Phase 10.

</deferred>

---

*Phase: 9-Global-Hotkey-Trigger*
*Context gathered: 2026-07-31*
