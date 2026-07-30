# Phase 8: Tray Residency, Autostart & Toast Notification - Context

**Gathered:** 2026-07-30
**Status:** Ready for planning

<domain>
## Phase Boundary

Make the app tray-resident: closing the main window minimizes to tray instead of exiting, a tray icon context menu (Switch to Rig/Normal Mode, Settings, Exit) fully controls the app, the tray icon's appearance reflects the current mode, an opt-in Settings checkbox enables launch-at-Windows-startup, and a toast/balloon notification confirms what changed whenever a toggle is triggered from the tray menu (the only non-GUI trigger that exists as of this phase — hotkey/CLI arrive in Phases 9-10 and will reuse this same notification path). Does not include the hotkey trigger itself (Phase 9) or the CLI trigger/single-instance IPC (Phase 10) — this phase only needs to make its own tray-menu trigger notification-worthy and reuse the Phase 7 `ToggleOrchestrator` entry point those later phases will also call.

</domain>

<decisions>
## Implementation Decisions

### Tray Icon Appearance & Click Behavior (TRAY-04, TRAY-05)
- **D-01:** Two distinct tray icon states — a "normal" icon and a "rig" icon — swapped via `NotifyIcon.Icon` (not a badge/overlay drawn on one icon). The tooltip text also updates to match ("Rig Toggle — Normal Mode" / "Rig Toggle — Rig Mode"). Exact icon glyph/color design is a UI-SPEC concern (this phase carries `UI hint: yes` in ROADMAP.md — a real UI-SPEC.md is expected here, unlike Phase 7's false-positive UI-gate trigger).
- **D-02:** Left-click on the tray icon restores and focuses the main window (TRAY-05, literal reading). Double-click is not specially handled — WinForms' `NotifyIcon` fires `Click` then `DoubleClick` on a double-click sequence, so the window simply gets a second harmless restore/focus call. No extra complexity needed.

### Minimize-to-Tray Scope (TRAY-01)
- **D-03:** Only the window's Close (X button / Alt+F4 / taskbar-close) is intercepted and redirected to "hide to tray" (via `FormClosing` with `CloseReason` checked, `e.Cancel = true`, then `Hide()`). The native taskbar minimize button keeps standard OS minimize behavior (window still in the taskbar, not hidden) — TRAY-01's literal wording is scoped to "closing the main window," not minimizing, and conflating the two would be scope creep past what's asked.

### Tray Context Menu (TRAY-03)
- **D-04:** The toggle menu item's label is dynamic and mirrors `MainForm`'s existing `btnToggle.Text` wording exactly ("Switch to Rig Mode" / "Switch to Normal Mode") — one shared source of truth for that string, not a second hardcoded copy. "Settings" opens the existing modal `SettingsForm` (same as the GUI's Settings button). "Exit" performs a real `Application.Exit()` — the tray icon (`NotifyIcon`) MUST be explicitly disposed/hidden (`Visible = false`) before or during exit, since an undisposed `NotifyIcon` is a well-known WinForms bug that leaves a stale, unclickable ghost icon in the tray until the user hovers over it.

### Autostart (TRAY-02)
- **D-05:** Carries forward the v1.1 roadmap decision already recorded in STATE.md: autostart uses a plain `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` registry value (not Task Scheduler — matches the app's existing non-elevated execution model). Settings gets a new checkbox ("Start with Windows"), off by default, following the exact same UI pattern as the existing `chkEnableDebugLogging` checkbox in `SettingsForm.cs` (plain checkbox, no separate Save step beyond the existing Save button). The registry value's command string points at the self-contained exe's own path plus the `--tray` flag from D-08 below (so an autostart launch starts hidden, not with a popped-up window).

### Startup-Mode Flag for Autostart (TRAY-02, cross-cutting with tray residency)
- **D-06:** Not explicitly stated in ROADMAP.md but required to make TRAY-02 non-annoying: launching via the autostart registry entry must NOT pop up the main window at boot — the entire value of "tray-resident + start with Windows" is defeated if a window flashes on screen every login. `Program.cs`'s `Main` gains a `string[] args` parameter; when `args` contains `--tray`, the app starts with `MainForm` created but never shown (constructed hidden, tray icon initialized, `Application.Run(mainForm)` still runs the message loop without an initial `Show()`/`ShowDialog()`). The Run registry value itself supplies `--tray` as part of its command string (D-05) — no runtime "was I autostarted" detection heuristic needed. This is the minimal startup-mode addition needed for this phase, not the general CLI-trigger feature (Phase 10's macro-pad/Stream-Deck toggle args are a separate, larger scope).

### Toast Notification (NOTIF-01)
- **D-07:** Carries forward the v1.1 roadmap decision already recorded in STATE.md: uses `NotifyIcon.ShowBalloonTip`, not a packaged-app toast API (AUMID/shortcut registration is a confirmed trap for unpackaged self-contained exes).
- **D-08:** The toast fires on every toggle triggered via the tray context menu, unconditionally — it does NOT check whether the main window happens to be currently visible or hidden-to-tray at that moment. A simpler, more robust rule ("tray-menu-triggered toggles always toast") avoids a fragile visibility/focus check, and is consistent with NOTIF-01's framing that the notification exists for "triggered without the GUI open" scenarios, which the tray menu inherently satisfies (you only use the tray menu when you're not interacting with the visible GUI). GUI-button-triggered toggles keep their existing `MessageBox` behavior unchanged — do not add a toast for the button-triggered path.
- **D-09:** Toast content exactly mirrors `MainForm`'s existing `FormatChecklist` per-step outcome text (Monitor/Audio/App: OK/FAILED/not attempted) plus the resulting mode ("Switched to Rig Mode" / "Switched to Normal Mode"), reusing the same formatting logic rather than inventing new wording — matches NOTIF-01's explicit "matching the GUI's existing partial-failure detail" requirement.

### Claude's Discretion
- Whether the `NotifyIcon`/`ContextMenuStrip` component lives directly on `MainForm` (as a designer component, most idiomatic WinForms pattern since `MainForm` already owns the toggle button and `ToggleOrchestrator` dependency) or a separate class — left to planner, though `MainForm`-hosted is the natural default given no other form is a better fit.
- Exact registry value name/format for the autostart entry (e.g. value name "RigToggle") and how the Settings checkbox reads current state (checking for the registry value's existence vs. a separate settings.json flag) — left to planner.
- Exact `FormatChecklist`-reuse mechanism (make it internal/public so a new tray-menu handler can call it, vs. duplicating the formatting) — left to planner; reuse is strongly preferred over duplication per this codebase's established DRY conventions.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level
- `.planning/PROJECT.md` — core value, v1.1 milestone goal
- `.planning/REQUIREMENTS.md` — TRAY-01/02/03/04/05, NOTIF-01 (mapped to this phase)
- `.planning/ROADMAP.md` — Phase 8 section: goal, success criteria, `UI hint: yes` (a real UI-SPEC.md is expected for this phase's plan-phase run, unlike Phase 7)
- `.planning/STATE.md` — v1.1 roadmap decisions: toast via `NotifyIcon.ShowBalloonTip` (not packaged toast), autostart via plain `HKCU\...\Run` key (not Task Scheduler) — both carried forward verbatim into D-05/D-07 above, not re-litigated

### Prior phases (orchestration & UI precedent)
- `.planning/phases/07-shared-toggle-orchestration-helper-extraction/07-CONTEXT.md` — D-03/D-04: `ToggleOrchestrator`'s public surface (`ToggleToRigMode()`/`ToggleToNormalMode()`/`IsInRigMode()`/`IsSettingsConfigured()`, throws `ToggleInProgressException` when busy) is exactly what the tray menu's toggle handler must call — this phase's tray menu is the second-ever caller of that entry point (after `MainForm.BtnToggle_Click`), validating the extraction's whole purpose
- `.planning/milestones/v1.0-phases/05-orchestration-full-toggle-packaging/05-CONTEXT.md` — D-01/D-02/D-03: the structured `ToggleResult` step-checklist contract and `FormatChecklist`'s exact per-step wording (Monitor/Audio/App: OK/FAILED/not attempted) that D-09's toast content must reuse verbatim, not reinvent

### Existing code (this phase's actual surface area)
- `src/RigToggle.App/MainForm.cs` — `BtnToggle_Click` (call pattern to replicate for the tray menu's toggle handler), `FormatChecklist` (private static — reuse target for D-09), `RefreshUi` (icon/tooltip state must be kept in sync the same way `lblMode`/`btnToggle.Text` already are)
- `src/RigToggle.App/SettingsForm.cs` — `chkEnableDebugLogging` (lines ~60-64, 545) is the direct analog for the new "Start with Windows" checkbox (D-05)
- `src/RigToggle.App/Program.cs` — composition root; `Main()` (line 25) needs an `args` parameter for D-06's `--tray` flag; `Application.Run(mainForm)` (line 98) is where startup show/hide branches
- `src/RigToggle.Core/ToggleOrchestrator.cs` — the entry point the tray menu's toggle handler calls (Phase 7)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MainForm.FormatChecklist` (private static, maps `ToggleResult` to a human-readable per-step string) — direct reuse target for the toast's content (D-09), just needs visibility widened if the tray handler lives in a different class than `MainForm`.
- `SettingsForm`'s `chkEnableDebugLogging` checkbox pattern (plain `CheckBox`, read/written directly to/from `AppSettings` on Load/Save) — exact template for the new autostart checkbox (D-05).
- `ToggleOrchestrator` (Phase 7) — already the correct, reentrancy-safe entry point for the tray menu's toggle call; no new guard logic needed here.

### Established Patterns
- Win32 P/Invoke kept minimal and hand-rolled (see `WindowsAppController.cs`'s `user32.dll` `DllImport`s) — if registry access needs anything beyond `Microsoft.Win32.Registry` (BCL, no P/Invoke needed for `HKCU\...\Run`), follow this same "hand-roll only what's needed" posture rather than adding a package.
- XML-doc rationale comments explaining *why* — continue for D-03's close-vs-minimize distinction and D-08's toast-always-fires-unconditionally rule, so a future reader doesn't "fix" either into unintended symmetry with the other trigger paths.

### Integration Points
- `MainForm`'s constructor/composition root wiring in `Program.cs` — the new `NotifyIcon`/`ContextMenuStrip` component and its event handlers attach here (Claude's Discretion: same class vs. new one).
- `Program.cs`'s `Main()` — startup branch for D-06's `--tray` flag.
- `SettingsForm`'s Save flow — new checkbox's read/write, plus the actual registry-key write/delete when the checkbox changes.

</code_context>

<specifics>
## Specific Ideas

- Tray context menu order and wording: "Switch to Rig Mode" / "Switch to Normal Mode" (dynamic, matches GUI button) → "Settings" → separator → "Exit".
- Toast title/body format should read naturally as a balloon notification, e.g. title "Switched to Rig Mode", body = the same Monitor/Audio/App checklist lines the GUI's MessageBox already shows.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. The general CLI-trigger feature (external tool/macro-pad toggle args) remains correctly scoped to Phase 10; D-06's `--tray` flag is the minimal startup-mode addition this phase needs, not a preview of that feature.

</deferred>

---

*Phase: 8-Tray-Residency-Autostart-Toast-Notification*
*Context gathered: 2026-07-30*
