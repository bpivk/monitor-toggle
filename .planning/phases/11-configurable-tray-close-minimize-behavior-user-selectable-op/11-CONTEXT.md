# Phase 11: Configurable Tray Close/Minimize Behavior - Context

**Gathered:** 2026-08-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace Phase 8's fixed "closing the window (X) always hides to tray, minimize always does standard OS minimize" behavior with two independent, user-configurable Settings preferences: (1) whether X hides to tray or exits the app, and (2) whether the minimize button also hides to tray. The tray icon's own existence becomes derived from whichever of these two settings is currently active, and toast notifications (NOTIF-01, Phase 9's hotkey toast) must keep working regardless of tray-icon visibility. Does not touch the tray context menu's contents, the hotkey trigger, or the CLI trigger (Phase 10) — this phase only revises TRAY-01's fixed close behavior into a preference and adds the new minimize option.

</domain>

<decisions>
## Implementation Decisions

### Close (X) Button Behavior
- **D-01:** New `AppSettings` boolean field (e.g. `CloseMinimizesToTray`) replaces Phase 8's unconditional `MainForm_FormClosing` redirect-to-tray for `CloseReason.UserClosing`. When true, X hides to tray exactly as it does today (`e.Cancel = true; Hide();`); when false, X lets the close proceed (app exits).
- **D-02:** Default value is **false (unchecked = exit the app)** — explicitly confirmed by the user despite this being a real behavior change from what's currently shipped: the existing `settings.json` has no such field, so it will deserialize as `false` on upgrade, meaning X will start exiting the app immediately after this phase ships until the user opens Settings and checks the box. This was confirmed twice (once on the initial answer, once on an explicit "are you sure this is intended given it changes today's behavior" follow-up) — do not "fix" this to default-true during planning or execution.
- **D-03:** Settings control: a checkbox labeled **"Closing the window (X) minimizes to tray"** (unchecked = exit), following the exact same control pattern as the existing `chkStartWithWindows` checkbox from Phase 8 (plain `CheckBox`, read/written directly to/from `AppSettings` on Load/Save, no separate confirmation step).

### Minimize Button Behavior
- **D-04:** New `AppSettings` boolean field (e.g. `MinimizeToTray`). When true, pressing minimize uses the **same mechanism as the Close-to-tray path** — `Hide()`, window vanishes from the taskbar entirely, tray-only — not standard OS minimize-to-taskbar. This is a deliberate reuse of the existing Close-path hide mechanism, not a second/different "minimized to tray" visual state.
- **D-05:** Default value is **false (off) — standard OS minimize**, matching Phase 8's original D-03 distinction (minimize was deliberately left as standard OS behavior, separate from Close). No behavior change on upgrade for this one.
- **D-06:** Settings control: a second checkbox in the same section, e.g. **"Minimizing the window also sends it to tray"**, same `chkStartWithWindows`-style pattern as D-03.

### Settings UI Placement
- **D-07:** Both new checkboxes live in **the same Settings section as the existing `chkStartWithWindows` ("Start with Windows")** checkbox from Phase 8 — a shared tray-behavior grouping, not a new separate section. Exact layout/spacing is a UI-SPEC concern (this phase should carry `UI hint: yes` in ROADMAP.md).

### Tray Icon Existence & Toast Notifications
- **D-08:** The tray icon (`NotifyIcon.Visible`) is shown whenever **either** `CloseMinimizesToTray` **or** `MinimizeToTray` is true, and hidden only when **both** are false. This is a derived/reactive value, not tied to the X-button setting alone — otherwise turning on minimize-to-tray while X is set to exit would silently do nothing (nowhere to hide into). Recompute and apply this derived visibility both at startup and immediately when either setting changes via Settings-Save (the tray icon must appear/disappear live, not only take effect after an app restart).
- **D-09:** When both settings are false, the tray icon is fully absent — TRAY-03 (right-click context menu), TRAY-04 (mode-reflecting icon), and TRAY-05 (left-click restore) are only reachable when the tray icon is actually shown. This is the accepted behavior for that configuration, not a gap: if the user hasn't opted into any tray-hiding behavior, the app behaves like a fully normal desktop app with no tray presence.
- **D-10:** Autostart (`TRAY-02`, "Start with Windows") and X-set-to-exit are allowed to combine freely with **no special handling or warning**. If the user enables autostart (app launches hidden via `--tray`) while X is set to exit, that means the app starts hidden but can only be permanently quit (no relaunch back into tray-resident mode without manually starting it again) — this is an accepted consequence of two independent settings, not a case requiring a Settings-time warning.
- **D-11:** The `NotifyIcon` **component itself is always instantiated** at startup regardless of these settings (avoids null-reference handling elsewhere in code that already assumes it exists, e.g. `ShowBalloonTip` calls from the toggle/hotkey paths). Only its `.Visible` property is driven by D-08's derived rule. **Accepted consequence:** `NotifyIcon.ShowBalloonTip` requires `Visible = true` to actually render a toast — so when both settings are off (tray icon hidden), toast notifications (NOTIF-01, and Phase 9's hotkey toast) will **not visually appear**, even though the underlying toggle still executes correctly. This was explicitly surfaced to the user (including the alternative of briefly flashing `Visible = true` just to show a toast) and the "always-instantiated, Visible-gated" approach was chosen anyway — silent toasts in "no tray" mode is an accepted tradeoff, not a bug to fix later.

### Claude's Discretion
- Exact `AppSettings` field names (`CloseMinimizesToTray`/`MinimizeToTray` used above are suggestions, not locked identifiers) — left to planner, though should follow the existing `bool` field naming convention (`SkipMonitorConfirmation`, `EnableDebugLogging`).
- Exact mechanism for making the tray-icon visibility change take effect live when Settings is saved (e.g. `notifyIcon.Visible = ...` called directly from the Save handler vs. a small helper method on `MainForm`) — left to planner; a small `MainForm` helper mirroring the existing `TryRegisterConfiguredHotkey`-style pattern is a reasonable default.
- Whether the minimize-to-tray interception is wired via a `Resize`/`SizeChanged` event handler checking `WindowState == FormWindowState.Minimized`, or some other WinForms mechanism — left to planner, this is a standard, well-documented pattern.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level
- `.planning/PROJECT.md` — core value, v1.1 milestone context (this phase extends v1.1's tray residency work, added mid-milestone per user request during Phase 9's rig checkpoint)
- `.planning/ROADMAP.md` — Phase 11 section: goal (revise TRAY-01's fixed behavior into a preference, add minimize-to-tray)
- `.planning/REQUIREMENTS.md` — no dedicated REQ-ID exists yet for this phase (TBD); this phase revises the already-Complete TRAY-01 rather than adding a net-new requirement — planner/roadmap sync should account for this

### Prior phases (tray behavior precedent — this phase directly revises Phase 8's design)
- `.planning/phases/08-tray-residency-autostart-toast-notification/08-CONTEXT.md` — D-03 is the exact fixed behavior this phase makes configurable ("only the window's Close... is intercepted... The native taskbar minimize button keeps standard OS minimize behavior... TRAY-01's literal wording is scoped to closing, not minimizing"); D-07/D-08/D-09 define the toast mechanism (`NotifyIcon.ShowBalloonTip`) that D-11 above depends on
- `.planning/phases/09-global-hotkey-trigger/09-CONTEXT.md` — D-06: the hotkey's startup-failure toast also uses `NotifyIcon.ShowBalloonTip` — same `Visible`-gating consequence from D-11 applies to it

### Existing code (this phase's actual surface area)
- `src/RigToggle.App/MainForm.cs` — `MainForm_FormClosing` (lines ~301-327, the exact handler D-01 makes conditional), `NotifyIcon_MouseClick` (D-09's left-click-restore, only reachable when tray icon visible)
- `src/RigToggle.App/SettingsForm.cs` — `chkStartWithWindows` (~line 106, direct pattern template for the two new checkboxes per D-03/D-06/D-07)
- `src/RigToggle.Core/Models/AppSettings.cs` — `SkipMonitorConfirmation`/`EnableDebugLogging` bool fields (lines 27-28) — naming/pattern precedent for the new fields

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MainForm_FormClosing`'s existing `Hide()` call for `CloseReason.UserClosing` — the exact mechanism D-04 reuses for minimize-to-tray; both paths should call through one shared "send to tray" helper rather than duplicating the `Hide()` call, to avoid the two paths drifting apart later.
- `SettingsForm`'s `chkStartWithWindows` read/write pattern (plain checkbox, `AppSettings` round-trip on Load/Save) — exact template for both new checkboxes.

### Established Patterns
- XML-doc rationale comments explaining *why* — this codebase's established convention (see Phase 8's D-03 comment in `MainForm.cs` explaining the close-vs-minimize distinction). Continue this for D-01's now-conditional close behavior and D-08's derived tray-icon-visibility rule, since both replace previously-fixed, previously-commented behavior — a future reader needs to understand these are now settings-driven, not hardcoded.
- `Trace.WriteLine` for best-effort/swallowed-failure paths — not directly relevant here (no new failure-prone I/O in this phase), but keep consistent with the rest of the codebase if any edge case needs it.

### Integration Points
- `MainForm_FormClosing` — becomes conditional on `AppSettings.CloseMinimizesToTray` instead of unconditional.
- A new `Resize`/`SizeChanged`-driven handler on `MainForm` for the minimize-to-tray path (D-04).
- `SettingsForm`'s Save flow — writes both new fields to `AppSettings`, then must trigger `MainForm` to recompute/apply the derived tray-icon visibility (D-08) live, not just on next launch.
- `Program.cs` / `MainForm`'s constructor or `InitializeTrayState()` — startup-time application of the same derived visibility rule (D-08), mirroring how `InitializeTrayState()` already primes tray state before either `Application.Run` branch (Phase 8 Pitfall 6 precedent).

</code_context>

<specifics>
## Specific Ideas

- Checkbox wording: "Closing the window (X) minimizes to tray" and "Minimizing the window also sends it to tray" — both grouped under/near the existing "Start with Windows" checkbox.
- User's original framing (verbatim intent): "options if x closes to tray or if it closes the app. And also if we want the minimize button to close the app to tray" — confirmed as two independent boolean preferences, not a combined mode picker.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. No new capabilities beyond the two configurable behaviors and their tray-icon-visibility/toast consequences were raised.

</deferred>

---

*Phase: 11-Configurable-Tray-Close-Minimize-Behavior*
*Context gathered: 2026-08-01*
