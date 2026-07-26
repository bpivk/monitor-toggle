# Feature Research

**Domain:** Windows tray-resident automation utility (personal sim-racing rig toggle) — v1.1 milestone
**Researched:** 2026-07-26
**Confidence:** MEDIUM-HIGH (patterns cross-checked against Win32/.NET official docs and multiple independent OSS/commercial reference implementations; single-user personal-tool judgment calls are explicitly flagged LOW where training-data/opinion rather than a verified source)

This supersedes the v1.0 `FEATURES.md` (dated 2026-07-24, which covered the original monitor/audio/companion-app toggle domain). This file covers the **v1.1 milestone only**: tray residency, global hotkey, CLI trigger, toast notification, and multi-monitor enable/disable configuration. Each is analyzed for (a) what "table stakes" behavior comparable Windows utilities implement, (b) complexity, and (c) **specific existing v1.0 code/UX this feature touches or breaks**, since this app already has a working GUI, Settings dialog, and toggle pipeline that must not regress.

## Feature Landscape

### Table Stakes (Users Expect These)

These are the behaviors that make a tray-resident automation utility feel "done" rather than half-built. For a single-user personal tool, "users" = the one user, but the bar is still "does this match how every comparable Windows tray utility (EarTrumpet, DisplayFusion, MultiMonitorTool, f.lux, Rainmeter, etc.) behaves" — deviating reads as buggy, not minimalist.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Close (X) button minimizes to tray, does not exit | Universal convention for tray-resident Windows utilities (EarTrumpet, Discord, Dropbox, f.lux) — an X that kills a "background utility" app breaks the entire premise of tray residency | LOW | Override `FormClosing`: if `e.CloseReason == CloseReason.UserClosing`, set `e.Cancel = true` and call `Hide()` instead. **Must** still allow `Application.Exit()` / real close via the tray menu's "Exit" item and via OS shutdown/logoff (`CloseReason.WindowsShutDown` must NOT be cancelled, or the app will block a Windows shutdown — a well-documented gotcha). |
| Explicit "Exit" always available and always actually exits | Users need a guaranteed way to fully quit a tray app (e.g. before uninstalling, or for troubleshooting) — every tray utility has this in its context menu | LOW | Tray menu "Exit" must set an internal "really closing" flag before calling `Close()`/`Application.Exit()` so the overridden `FormClosing` doesn't re-cancel it. |
| Single tray icon, left-click behavior distinct from right-click menu | Convention: right-click = context menu (mode switch, Settings, Exit), left-click/double-click = show/restore main window OR fire the primary action (toggle) — tools vary on which, but *some* left-click behavior is expected, not a no-op | LOW | `NotifyIcon.MouseClick`/`DoubleClick` events. Given this app's core action is "one click to toggle," left-click-to-toggle-with-tray-icon-doubling-as-status-indicator is a reasonable and differentiator-adjacent choice (see Differentiators) rather than plain window-restore. |
| Tray icon reflects current mode (visual state) | Users glance at tray to know "am I in rig mode right now" without opening the window — same pattern as EarTrumpet (icon changes per active audio device) and battery/wifi tray icons | LOW-MEDIUM | Two-icon swap (Normal/Rig) driven by the same `ToggleService.IsInRigMode()` already used by `MainForm.RefreshUi()`. Needs icon assets; trivial in code once assets exist. |
| Autostart is a Settings checkbox, off by default | Users expect explicit opt-in for anything that runs on every boot — auto-enabling it unasked is treated as malware-like behavior by security-conscious users and by Windows Defender/SmartScreen heuristics | LOW | `HKCU\...\Run` registry value add/remove tied to a Settings checkbox. Off-by-default matches the existing `EnableDebugLogging` and `SkipMonitorConfirmation` opt-in patterns already in `AppSettings`. |
| Hotkey has a visible current binding + a way to change/clear it in Settings | Every hotkey-capable utility (Everything, ShareX, Greenshot, PowerToys) shows the current combo and lets you rebind or disable it — a hardcoded, invisible hotkey is a common early-version mistake that immediately generates "how do I change this" friction | LOW-MEDIUM | Needs a small hotkey-capture control (listen for next keydown, format as string) in Settings; not just a text field. |
| Hotkey registration failure is surfaced, not silently swallowed | `RegisterHotKey` returns `false`/fails if another app already owns that combo (very common — many combos are taken by OS/OEM software, e.g. `Win+Alt+*` OEM bindings, capture software). A tool that silently no-ops here looks broken ("I pressed the hotkey and nothing happened") | LOW | Check return value; on failure, show a one-time notification/Settings warning ("Hotkey Ctrl+Alt+R is already in use by another application") rather than failing the whole app or crashing. |
| CLI trigger works whether or not an instance is already running | A macro-pad/Stream Deck button that only works "if you remembered to have the tray app open" is unreliable by design — the entire point of a CLI trigger for hardware macro buttons is "always works, launch-or-signal transparently" | MEDIUM | This is the single most technically involved of the six features — see Feature Dependencies below. Table stakes behavior: if not running, launch it (tray-resident, no visible window) and apply the action; if running, signal it via IPC and apply the action; exit code reflects success/failure for scripting. |
| Toast/notification only appears for headless-trigger paths, not for the already-visible GUI-click path | Redundant "Switched to Rig Mode" toast popping up right next to a window the user is already looking at (because they just clicked the button in it) is noise, not signal — EarTrumpet, PowerToys, and similar tools suppress notifications for actions the user directly observed | LOW-MEDIUM | Notification should be gated on trigger source (hotkey/CLI/tray-menu-while-hidden = notify; GUI-button-click while window visible = no notify, existing MessageBox-based feedback already covers that path). |
| Multi-monitor Settings UI prevents "disable everything" configurations | Windows CCD will not allow all displays to be disabled — at least one active path must remain (verified via DisplayFusion user reports of exactly this failure mode) | LOW-MEDIUM | Settings-side validation: if the "enable" set is empty and the "disable" set covers every currently-enumerated monitor, block Save (or warn) rather than let the toggle fail at runtime with a cryptic CCD exception. |
| Confirmation dialog names *all* monitors being disabled, not one | DISPLAY-03's entire safety rationale ("informed consent before killing a display") is defeated if a 3-monitor disable-set only ever shows one name — the existing dialog design intent (see Key Decisions in PROJECT.md, DISPLAY-03) generalizes directly to a list | LOW | Direct, mechanical extension of existing `MonitorConfirmDialog` — see Dependencies section, this is not new UX design, just pluralizing existing text. |

### Differentiators (Competitive Advantage — value-add beyond bare table stakes)

For a single-user tool, "differentiator" means "worth the extra complexity for this specific rig setup," not competitive market positioning.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Left-click tray icon = instant toggle (no menu, no window) | Matches the Core Value ("a single reliable action") even more tightly than v1.0's GUI-button click — for a daily habitual action (sit down at rig → toggle), a single click on an always-present tray icon is *faster* than restoring a window first | LOW (once tray infra exists) | Right-click still opens the full menu (Settings/Exit/explicit mode switch) for the less-frequent actions. This directly extends the "one click" Core Value into the tray-resident era instead of just replicating the old window. |
| Notification shows *what changed*, not just "Toggled" | A generic "Toggle complete" toast is low-value; naming the new mode ("Switched to Rig Mode — Rig Speakers, Monitor X disabled") gives the same informed-consent value the GUI dialog gives, in the one place (hotkey/CLI trigger) where the user has no other confirmation | LOW-MEDIUM | Reuses the existing `ToggleResult`/`FormatChecklist` step data already computed for the partial-failure MessageBox in `MainForm` — this is presentation reuse, not new data plumbing. |
| Partial-failure surfaced via notification (not silently lost) when triggered headlessly | v1.0's partial-failure `MessageBox` (CORE-04) only exists because a *window* is open to show it. A hotkey/CLI-triggered toggle with no window open must not let a partial failure vanish silently — that's strictly worse than v1.0's behavior for the automated-trigger case | MEDIUM | Needs a "failure" toast variant (or an interactive toast with a "Show Details" action that surfaces the window) distinct from the success toast. This is the automation-parity requirement that makes headless triggers *as safe* as the GUI path, not just as convenient. |
| Enable-set and disable-set both independently restorable to whatever was active before, not to a fixed "on/off" | Real scenario in PROJECT.md: rig monitor is normally OS-disabled to save power. Toggle-back must not just "turn it back on" unconditionally — it must restore it to *disabled* if that's genuinely what was true before rig mode (matching the existing "remember-previous-state, not fixed preset" Key Decision) | MEDIUM-HIGH | This isn't a new UI feature so much as an architectural extension of the existing snapshot/restore mechanism (`MonitorState`/`CaptureState`/`Restore`) to plural monitor identities — flagged here because it's easy to under-scope this as "just add a second ComboBox" when it's really "snapshot-and-restore must generalize from 1 path to N paths, in both directions." |
| CLI supports a query/status mode (e.g. `--status`) in addition to `--rig`/`--normal` | Useful for a Stream Deck plugin/macro pad that wants to show current mode as an icon state, not just fire-and-forget the toggle | LOW (given the IPC channel already exists for `--rig`/`--normal`) | Not explicitly requested in the milestone scope but nearly free once the named-pipe/mutex plumbing for the two required verbs exists — flag as a cheap addition, not a requirement. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|------------------|-------------|
| Elevated/admin autostart via Scheduled Task (`Task Scheduler` "Run with highest privileges") | Task Scheduler is frequently cited as the more "robust" autostart mechanism for professional tools, and some guidance recommends it over the Registry Run key for reliability | This app is explicitly non-elevated by design (PROJECT.md Constraint: "Deliberately no elevation manifest... default non-elevated execution level is preserved... so cross-process window-focus call against the non-elevated companion app is not broken by UIPI"). An elevated Scheduled Task autostart would silently reintroduce the exact UIPI (User Interface Privilege Isolation) problem the H9 debug session and the relaunch-based `LaunchOrFocus` redesign already worked around. | Plain `HKCU\...\Run` registry value (non-elevated, per-user, no admin prompt) — simplest mechanism, matches the app's existing non-elevated posture, and is what most non-elevated personal utilities (EarTrumpet, f.lux) use. |
| A hotkey-configuration mini-language / global hotkey library with arbitrary key-combo chords, sequences, or multiple simultaneous bindings | "More power" always sounds appealing, and third-party hotkey libraries (e.g. NHotkey, SharpHook) advertise rich chord/sequence support | This is a single-user tool needing exactly one binding for exactly one action (toggle). A chord/sequence engine is unused complexity and a larger attack surface for bugs (the WM_HOTKEY message pump interaction with WinForms already has known gotchas around message-only windows and thread affinity) for zero realized value. | Two raw `RegisterHotKey`/`UnregisterHotKey` P/Invoke calls (already the stack's stated pattern for keyboard tray-utility features) bound to exactly one configurable combo. |
| Native WinRT toast notifications via full `AppNotificationManager`/Windows App SDK packaging (MSIX) | The "modern"/officially-recommended path for toast notifications on current Windows is the Windows App SDK, and search results surface this as the forward-looking recommendation | Requires MSIX packaging or at minimum an AUMID + Start-menu-shortcut registration dance for an unpackaged Win32 app, which conflicts directly with this project's core distribution constraint ("Standalone .exe... not a bare interpreted script," i.e., single self-contained portable exe, no installer/MSIX). Full Windows App SDK bootstrapping adds a runtime dependency and packaging step disproportionate to "show a toast when a background toggle completes." | `Microsoft.Toolkit.Uwp.Notifications` (or its current `CommunityToolkit.WinUI.Notifications` successor) — explicitly documented to work for unpackaged Win32 apps (WinForms/WPF/console) via `ToastNotificationManagerCompat`, no MSIX or AUMID shortcut required. If even that is judged too heavy, `NotifyIcon.ShowBalloonTip(...)` remains a zero-dependency fallback with materially lower fidelity (Windows 11 renders it as a transient banner, not persisted to Action Center) — acceptable as a fallback, not as the primary design. |
| Per-monitor "enable" and "disable" sets that silently reorder or renumber if hardware changes (e.g. matching by index/position instead of stable identity) | Easy to implement quickly — "monitor 2" is simpler code than resolving a stable device path | This exact pitfall already burned the app once conceptually: v1.0 deliberately keys off `MonitorInfo.DevicePath` (a stable identifier) specifically because friendly names/positions/indices shift when cables are replugged, GPU drivers update, or Windows re-enumerates. The existing stale-device warning pattern (D-10 in `SettingsForm`) exists precisely to catch this. A multi-monitor set that isn't keyed the same way reintroduces silent misconfiguration risk multiplied by N monitors instead of 1. | Persist plural `DevicePath` lists (`DisableMonitorDevicePaths: string[]`, `EnableMonitorDevicePaths: string[]`) and reuse the exact same "saved-but-not-found → stale warning" UX already built for the single-monitor case, applied per-item in the list. |
| CLI trigger that always force-launches a brand-new process per invocation (spawn a full new instance every time a macro pad button is pressed) | Simplest possible CLI implementation — `Process.Start` a new instance and let it do the toggle and exit | Defeats tray residency entirely (memory/handle churn on every button press, a visible flash if any window creation happens even briefly, and worse, could race with an already-running tray instance's own snapshot/settings file access — two processes touching `settings.json`/`state.json` concurrently is a real correctness risk given the app already relies on file-based state (`JsonSettingsStore`, `JsonSnapshotStore`) with no documented cross-process locking) | Single-instance-detection (Mutex) + IPC signal (named pipe) to the *existing* resident instance; only launch a fresh process (which then immediately backgrounds itself, no visible window) if no instance is currently running. |

## Feature Dependencies

```
[Tray residency + tray icon + context menu]
    └──requires──> [MainForm hide/show + real-exit vs. minimize-to-tray FormClosing override]
    └──requires──> [Toggle-trigger logic extracted from MainForm.BtnToggle_Click into a
                     reusable, UI-thread-safe method callable without a visible window]
                       └──requires──> [Confirmation-dialog / partial-failure-reporting logic made
                                       conditional on "is there a window the user is looking at"]

[Global hotkey trigger]
    └──requires──> [Toggle-trigger logic extracted (same as above)]
    └──requires──> [A persistent message-only window / existing Form's window handle to receive
                     WM_HOTKEY — natural fit: reuse MainForm's HWND even while Hidden, since
                     RegisterHotKey needs a real window handle and WinForms Forms keep their
                     handle alive while hidden, not destroyed]
    └──requires──> [Settings UI: hotkey capture control + persisted combo in AppSettings]

[CLI trigger]
    └──requires──> [Toggle-trigger logic extracted (same as above)]
    └──requires──> [Single-instance detection (Mutex) in Program.cs Main()]
    └──requires──> [IPC channel (named pipe) from "new process invocation with --rig/--normal"
                     to "already-running resident instance"]
    └──requires──> [Tray residency (an instance must be able to run with no visible window at
                     all in order for "launch if not running" to make sense for a headless trigger)]

[Toast/status notification]
    └──requires──> [Toggle-trigger logic extracted (same as above), specifically needs to know
                     "was this triggered without the GUI visible" to decide whether to notify]
    └──enhances──> [Tray residency, global hotkey, CLI trigger] (all three are the "headless"
                     paths that need a notification since there's no window to show a result in)
    └──reuses──> [Existing ToggleResult / FormatChecklist step-outcome data already computed
                   in MainForm for the CORE-04 partial-failure MessageBox]

[Multi-monitor enable/disable configuration]
    └──requires──> [AppSettings: MonitorDevicePath (singular) → DisableMonitorDevicePaths +
                     EnableMonitorDevicePaths (plural lists)]
    └──requires──> [IMonitorController.Disable(string) → Disable(IEnumerable<string>) signature
                     change, plus a new Enable(IEnumerable<string>) method — WindowsMonitorController
                     currently only implements single-path disable via ApplyPathInfos]
    └──requires──> [SettingsForm.cboMonitor (single ComboBox, single selection) → a multi-select
                     control (CheckedListBox or two list controls) for two independent monitor sets]
    └──requires──> [MonitorConfirmDialog(string monitorFriendlyName) → accept a collection and
                     render a pluralized message/list, not a single interpolated name]
    └──requires──> [ToggleService.IsFullyConfigured / IsSettingsConfigured validation logic →
                     generalize from "MonitorDevicePath is non-empty" to "at least one of the two
                     sets is non-empty" (or whatever the actual validation rule becomes)]
    └──requires──> [Settings-side validation: reject/warn when disable-set ⊇ all monitors and
                     enable-set is empty, since Windows CCD refuses to disable every display]
    └──conflicts-with──> [Existing SkipMonitorConfirmation reset-on-change logic in
                           SettingsForm.BtnSaveSettings_Click, which currently compares a single
                           MonitorDevicePath string — must be redefined as "set equality" over
                           both lists, or the confirmation-skip flag will not reset correctly
                           when a set member changes]

[Toggle-trigger logic extraction] (the shared prerequisite above)
    └──conflicts-with──> [Nothing structurally, but is the highest-risk shared refactor: three
                           new triggers (tray, hotkey, CLI) all need to invoke the same
                           confirm→toggle→report pipeline that today lives inline in
                           MainForm.BtnToggle_Click as WinForms-UI-coupled code (MessageBox.Show
                           calls, `this` as dialog owner). This must become owner-optional /
                           headless-capable before any of the three trigger features can be
                           built without duplicating or diverging the toggle logic.]
```

### Dependency Notes

- **Everything funnels through one refactor.** All three new trigger surfaces (tray-menu click, hotkey, CLI) need to run the *same* confirm-then-toggle-then-report pipeline that `MainForm.BtnToggle_Click` currently owns directly, with `MessageBox.Show(this, ...)` calls that assume a visible owning window. The single highest-leverage piece of prep work is extracting that method into something like a `TogglePresenter`/`ToggleCoordinator` that takes an "interaction mode" (interactive-with-window vs. headless) and either shows the existing dialogs or fires a notification. Get this wrong and the roadmap risks three near-duplicate copies of the confirm/report logic (one per trigger), which is exactly the kind of drift that produces the "GUI click behaves differently than hotkey" bugs comparable tools get bug reports about.
- **CLI trigger is the most technically involved feature**, not multi-monitor. It requires solving process-lifetime and IPC correctness (Mutex + named pipe) that has no v1.0 precedent at all, whereas multi-monitor "only" requires pluralizing existing, already-well-tested single-item code paths (settings persistence, confirm dialog, controller call). Roadmap sequencing should reflect that CLI trigger has the highest implementation risk of the six, even though multi-monitor touches the most distinct files.
- **Multi-monitor is a breaking data-model change**, not additive. `AppSettings.MonitorDevicePath` (singular, nullable string) cannot simply gain siblings — existing persisted `settings.json` files from v1.0 installs have this single field populated and nothing else. A migration path (e.g., on load, if the old singular field is present and the new plural fields are absent, seed `DisableMonitorDevicePaths` with the single value) is needed to avoid a silent "your monitor setting disappeared" regression for the existing user on upgrade. This is a real migration concern flagged for architecture/roadmap, not just a Settings UI relabeling.
- **Tray residency is the load-bearing prerequisite for CLI trigger**, specifically the "signal an already-running instance" requirement in the milestone scope. Without minimize-to-tray (i.e., without a mode where the process is alive with no visible window), there is no meaningful distinction between "launch a fresh instance" and "signal a running one" — the CLI feature's entire value proposition (fire toggle from a macro pad without a window flashing open) depends on tray residency existing first.
- **Notification gating logic depends on trigger-source awareness**, which only exists once the toggle-trigger logic is extracted with an explicit "interactive vs. headless" parameter (see first bullet). Building the toast mechanism before that refactor risks either always-notify (redundant noise on GUI clicks) or never-notify (useless for the headless paths it exists for).

## MVP Definition

Given this is a fixed six-feature milestone (not an open-ended feature backlog), "MVP" here means "what's the safe build order within v1.1," not "which features to cut."

### Launch With (v1.1 — required per milestone scope)

- [ ] Toggle-trigger logic extracted into a reusable, owner-optional method — *not itself a user-facing feature, but the prerequisite every other item in this milestone depends on*
- [ ] Tray residency: minimize-to-tray on close, autostart Settings checkbox, tray icon + context menu (Switch mode / Settings / Exit) — *foundation for CLI and the "headless" notion the other features key off of*
- [ ] Multi-monitor enable/disable configuration (data model, controller signature, Settings UI, confirm dialog) — *has no dependency on the other five, can be built/tested in parallel with the tray-residency track, but is the largest single change to existing files*
- [ ] Global hotkey trigger — *depends on toggle-trigger extraction; benefits from tray residency existing (so the hotkey works even fully backgrounded) but the WM_HOTKEY registration itself only needs a live window handle, which `MainForm` already has even before tray-hide support exists*
- [ ] CLI trigger (`--rig`/`--normal`) with Mutex + named-pipe signaling to a running instance — *depends on tray residency (to make "already running, no window" a real state) and toggle-trigger extraction; highest technical risk, sequence last within the "trigger" cluster so the shared extraction and IPC plumbing are both already proven by the time this lands*
- [ ] Toast/status notification for headless-triggered toggles — *depends on toggle-trigger extraction providing trigger-source context; naturally follows once at least one headless trigger (hotkey or CLI) exists to actually exercise it*

### Add After Validation (not requested this milestone, but cheap given the above)

- [ ] CLI `--status` query verb — trivial once the Mutex/named-pipe channel exists for `--rig`/`--normal`; add if/when a macro-pad integration wants to reflect current mode as an icon state, not before
- [ ] Left-click-tray-icon-to-toggle (vs. plain window-restore) — small UX polish decision, not a structural dependency; can be decided at implementation time without affecting sequencing

### Future Consideration (explicitly out of scope, do not build now)

- [ ] Toggle history/log (LOG-01) — already explicitly deferred in PROJECT.md, unrelated to this milestone's six features
- [ ] Chord/sequence hotkeys, multiple simultaneous hotkey bindings — anti-feature, see table above
- [ ] MSIX packaging for "proper" WinRT toast notifications — anti-feature, conflicts with standalone-.exe distribution constraint

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|----------------------|----------|
| Toggle-trigger logic extraction (prerequisite) | HIGH (unblocks everything else) | LOW-MEDIUM | P1 |
| Tray residency + context menu + autostart | HIGH (daily-use friction removal, explicit milestone goal) | MEDIUM | P1 |
| Multi-monitor enable/disable configuration | HIGH (explicit milestone goal, real hardware scenario in PROJECT.md) | MEDIUM-HIGH (breaking data-model change + settings migration) | P1 |
| Global hotkey trigger | MEDIUM-HIGH (explicit milestone goal) | LOW-MEDIUM | P1 |
| CLI trigger | MEDIUM-HIGH (explicit milestone goal, enables macro-pad workflow) | MEDIUM-HIGH (Mutex + named pipe + process-lifetime correctness) | P1 |
| Toast/status notification | MEDIUM (safety-net/parity for headless triggers) | LOW-MEDIUM | P1 |
| CLI `--status` query verb | LOW-MEDIUM | LOW (once IPC exists) | P3 |
| Left-click-tray-to-toggle | LOW-MEDIUM (polish) | LOW | P3 |

All six milestone-scoped features are P1 by definition (fixed milestone scope, not a backlog to triage) — the matrix here is mainly useful for **build order within the milestone**, where the "Implementation Cost" column should drive phase sequencing: do the shared extraction and tray-residency foundation first, multi-monitor's data-model change in parallel, then layer hotkey → notification → CLI (CLI last, since it's both highest-cost and depends on tray residency being solid).

## Sources

- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey — official `RegisterHotKey` docs: ID ranges, replace-on-duplicate-id semantics, WM_HOTKEY delivery to the registering window — HIGH confidence
- https://www.autoitconsulting.com/site/development/single-instance-winform-app-csharp-mutex-named-pipes/ and https://github.com/AutoItConsulting/examples-csharp/tree/master/MutexSingleInstanceAndNamedPipe — Mutex + named-pipe single-instance-with-CLI-argument-forwarding pattern, working example — MEDIUM-HIGH confidence (independent OSS reference, cross-checked against multiple similar writeups in the same search)
- https://github.com/CommunityToolkit/WindowsCommunityToolkit/blob/main/Microsoft.Toolkit.Uwp.Notifications/Toasts/Compat/ToastNotificationManagerCompat.cs and https://www.nuget.org/packages/CommunityToolkit.WinUI.Notifications/ — confirms unpackaged-Win32-app toast support without MSIX/AUMID shortcut requirement via `ToastNotificationManagerCompat` — MEDIUM-HIGH confidence
- https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/toast-notifications and related Windows App SDK notification docs — confirms `AppNotificationManager`/Windows App SDK is the newer official direction but implies MSIX/packaging investment — MEDIUM confidence, used to justify the Anti-Features entry against it for this project's distribution constraint
- https://www.displayfusion.com/Discussions/View/how-to-disable-all-monitors/ and https://www.displayfusion.com/Discussions/View/two-monitor-profiles-1-disable-monitors-2-enable-monitors-enable-doesnt-work/ — real user reports confirming Windows will not allow disabling every monitor, and confirming "disable set / enable set" monitor-profile UX is an established pattern in a comparable commercial tool (DisplayFusion) — MEDIUM confidence (community discussion, not official docs, but directly on-point and corroborated across two independent threads)
- Existing codebase, read directly (HIGH confidence, ground truth for "what v1.0 already does"):
  - `/home/bpivk/moza/src/RigToggle.App/MainForm.cs` — confirms `BtnToggle_Click` currently owns confirm→toggle→report inline, coupled to `MessageBox.Show(this, ...)` and dialog ownership
  - `/home/bpivk/moza/src/RigToggle.App/SettingsForm.cs` — confirms single `cboMonitor` ComboBox bound to one `PickerItem`, and the `monitorChanged` single-string comparison driving `SkipMonitorConfirmation` reset
  - `/home/bpivk/moza/src/RigToggle.App/MonitorConfirmDialog.cs` — confirms constructor takes one `string monitorFriendlyName` and renders one interpolated sentence
  - `/home/bpivk/moza/src/RigToggle.Core/Models/AppSettings.cs` — confirms `MonitorDevicePath` is a single nullable string field, no existing plural/list field to reuse
  - `/home/bpivk/moza/src/RigToggle.Core/Abstractions/IMonitorController.cs` and `/home/bpivk/moza/src/RigToggle.Core/ToggleService.cs` — confirms `Disable(string monitorDevicePath)` singular signature and `IsFullyConfigured` validation keyed on a single non-empty string
  - `/home/bpivk/moza/src/RigToggle.App/Program.cs` — confirms current composition root has no CLI-argument handling and no single-instance/Mutex guard at all
  - `/home/bpivk/moza/.planning/PROJECT.md` — v1.1 milestone scope, Core Value, Constraints (non-elevated execution, standalone .exe distribution), and the real triple-monitor-plus-power-saving-rig-monitor scenario motivating the multi-monitor feature

---
*Feature research for: Windows tray-resident automation utility (Rig Toggle v1.1)*
*Researched: 2026-07-26*
