# Rig Toggle

## What This Is

A Windows GUI utility that switches between "normal desktop mode" and "Moza rig mode" with one click — from the GUI, a tray menu, or a global keyboard shortcut. Toggling to rig mode disables the primary monitor at the OS level (so games default to the rig monitor instead), switches the default audio output to the rig speakers, and launches the Moza Companion app. Toggling back applies Normal mode's own explicitly configured monitor set and audio device — symmetric with how Rig mode already works — and minimizes the Moza Companion app. Both the companion app and either mode's audio device can be left unset, in which case that toggle step is skipped cleanly with no error, while a target that's configured but genuinely broken (missing file, removed device) still surfaces as a real failure. A separate live Manual Monitor Panel lets the user enable/disable any monitor on demand, independent of the Rig/Normal toggle. The app can run tray-resident with autostart, and supports arbitrary multi-monitor configurations (not just a single primary monitor). The GUI follows the Windows system light/dark theme live, and the tray/exe icons use a shape-distinct monitor-vs-steering-wheel motif. Distributed via a public GitHub repo (`bpivk/monitor-toggle`) with a GitHub-ready README, CI, tagged releases, and a self-contained exe roughly 58% smaller than pre-v2.0. Built for a single user's personal sim-racing rig setup.

## Core Value

A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches (e.g. BeamNG.drive minimizing itself) reliably open on the rig monitor — and just as reliably applies Normal mode's own explicitly configured monitor/audio state on toggle-back. (Revised at v2.0: the original "restores everything to exactly how it was before" framing described snapshot-restore, which v2.0 replaced with an explicit, symmetric Normal-mode configuration — see DISPLAY-10/AUDIO-04 and the v2.0 archive below.)

## Current State

**Shipped: v2.0 Configurable Monitors, Optional Targets & Cleanup (2026-08-09)**

v2.0 made the companion app and both audio-device targets genuinely optional (unset skips cleanly, broken still fails loudly), replaced Normal mode's snapshot-restore with an explicitly configured monitor set symmetric to Rig mode's, moved mode-tracking off snapshot-file-presence onto a persisted `IModeStore` flag with crash-mid-toggle recovery, added a live Manual Monitor Panel with a safety guard shared across all three monitor-mutation paths, and closed out the milestone by deleting the now-dead snapshot-restore subsystem entirely (751 net lines removed) and shrinking the self-contained exe from 116.9 MB to 49.4 MB (57.79% smaller) via MSBuild-only configuration — no IL trimming, no code changes. All four phases were rig-verified on real Windows 11 hardware with no FAILs and no waived scenarios at close.

**Not yet done:** the four README screenshot placeholders (`docs/screenshots/*.png`) are still broken-image links from v1.2 — real screenshots must be supplied by the user from the rig (no Windows GUI in this build environment).

## Current Milestone: v2.1 Modern UI Redesign & Theme Backlog

**Goal:** Replace both windows' bolted-on-feature layout with an intentional, modern design — MainForm becomes a monitor-tile dashboard leading into the mode toggle, SettingsForm gets a real layout pass, and the deferred theme backlog (accent color, custom toggle switch, manual override) closes out.

**Target features:**
- MainForm redesign: clickable per-monitor tiles (icon + number, live on/off status) as the primary screen, Rig/Normal toggle button placed after the tile row, Settings relocated to a secondary/bottom position
- Monitor tiles absorb the standalone Monitors panel's functionality (direct toggle, Identify action moved near the tiles, existing `SkipMonitorConfirmation` safety gate preserved) — the separate `MonitorPanelForm` and its entry points (MainForm button + tray item) are retired
- SettingsForm layout pass: fix overlapping/crowded controls, better grouping and spacing for the two monitor grids, audio dropdowns, app path, and hotkey box
- THEME-08: the Rig/Normal toggle becomes a custom-drawn toggle-switch control instead of a plain button
- THEME-07: key interactive elements pick up the Windows accent color instead of a fixed palette
- THEME-09: a manual light/dark override setting, independent of live Windows theme-follow

**Key context:** This milestone retires Phase 17's standalone Manual Monitor Panel (`MonitorPanelForm`) by folding its capability directly into the redesigned MainForm — the panel's underlying `IMonitorController.DeactivateMonitors`/`ActivateMonitors` calls and the shared DISPLAY-12 safety guard stay, only the separate window and its two entry points go away. LOG-01 (toggle history/log) was considered and explicitly dropped — not carried forward again.

<details>
<summary>Archived: v2.0 milestone framing (superseded)</summary>

**v2.0 Configurable Monitors, Optional Targets & Cleanup target features (shipped 2026-08-09, as scoped 2026-08-04):**
- App path becomes optional — when unset, toggle skips launch/focus/minimize entirely
- Audio devices become optional per role — when unset, toggle skips audio switching entirely
- Normal mode gets an explicit configured monitor set (which monitors on/off), replacing snapshot-restore
- Rig mode's existing monitor set config stays, now symmetric with Normal mode's
- New GUI panel: live, on-demand enable/disable of any monitor, independent of the Rig/Normal toggle, with per-monitor status shown via monitor icons (not just text)
- Safety constraint carried over: at least one monitor must always remain enabled, enforced across both the toggle action and the manual panel
- Reduce self-contained exe size (without enabling IL trimming — COM/P-Invoke code gets falsely stripped, per existing project guidance)
- Code quality/cleanup pass across the 3-milestone-old codebase

All eight target features shipped as scoped; no scope was dropped or added mid-milestone.

</details>

<details>
<summary>Archived: v1.0, v1.1, and v1.2 milestone framing (superseded)</summary>

**v1.0 MVP target features (shipped 2026-07-26):** GUI settings view, one-click toggle in both directions, true OS-level monitor disable, default audio device switching, companion-app launch/focus/minimize, standalone .exe distribution.

**v1.1 Automation & Multi-Monitor target features (shipped 2026-08-01, as scoped 2026-07-26):**
- Tray residency (autostart on boot, minimize-to-tray on close) with a tray icon context menu, configurable via Settings
- Global hotkey trigger (Windows-wide keyboard shortcut)
- CLI trigger (command-line args for macro pads / Stream Deck / external tools) — *scoped but not delivered; dropped to v2 at milestone close*
- Toast/status notification confirming a toggle when triggered without the GUI open
- Multi-monitor enable/disable configuration — generalizes v1.0's single "primary monitor to disable" to arbitrary multi-monitor desks

**Deferred from v1.1:** LOG-01 (toggle history/log) — still a nice-to-have, lower priority than automation/multi-monitor.

**v1.2 Visual Polish & Documentation target features (shipped 2026-08-04, as scoped 2026-08-02):**
- GUI restyle: modern flat controls following Windows system light/dark mode (title bar via DWM API + re-themed controls), applied to MainForm and SettingsForm — Phase 12
- Redesigned tray icon pair: visually distinct, well-designed icons for rig mode vs. normal mode (replaces the prior functional-but-plain pair) — Phase 13
- New README.md: feature overview + screenshots, install/build instructions, badges (build status, license, latest release) — Phase 14

**Deferred from v1.2:** LOG-01 (toggle history/log) — still a nice-to-have, lower priority than visual polish.

</details>

## Requirements

### Validated

- [x] GUI includes a settings view where the user selects: which monitor is the "primary to disable," which audio devices are the toggle pair, and which app (path) to launch/minimize — Validated in Phase 2: Foundations & GUI Shell
- [x] Toggling to rig mode switches the default audio output device to the rig speakers — Validated in Phase 3: App & Audio Control
- [x] Toggling back restores the exact previous default audio device across all relevant audio roles — Validated in Phase 3 (superseded at v2.0: Normal-mode audio now applies an explicitly configured device via `SetDefault`, not a restored snapshot — see AUDIO-04)
- [x] Toggling to rig mode launches the Moza Companion app if it isn't already running; if it's already running, brings it to focus instead of launching a duplicate instance — Validated in Phase 3, mechanism superseded post-ship (H9 focus-manipulation bug, fixed via relaunch-based `ShellExecute` activation, rig-confirmed both directions). Settings generalized to accept any `.lnk`/`.exe` target.
- [x] Toggling back minimizes the Moza Companion app window (best-effort) — Validated in Phase 3; refined post-ship to skip the minimize call when the window is already hidden/tray-only
- [x] Toggling to rig mode disables the primary monitor at the OS level (true disable) — Validated in Phase 4: Monitor Control (Production)
- [x] Toggling back restores the exact monitor configuration that was active immediately before toggling to rig mode — Validated in Phase 4, hardened in Phase 5's crash-recovery fallback (superseded at v2.0: Normal mode now applies its own explicitly configured monitor set, not a restored snapshot — see DISPLAY-10; the underlying `Restore()` mechanism was deleted in Phase 18/CLEANUP-01)
- [x] User can toggle from normal mode to rig mode in one action from a GUI window — Validated in Phase 5: Orchestration, Full Toggle & Packaging
- [x] User can toggle back from rig mode to normal mode in one action — Validated in Phase 5
- [x] Distributed as a standalone Windows .exe (no separate runtime install required to run it) — Validated in Phase 5
- [x] Multi-monitor enable/disable configuration, generalizing the single "primary monitor to disable" setting (DISPLAY-04/05/06/07/08) — Validated in Phase 6: Multi-Monitor Data Model & Controller Generalization. Rig-validated on real 2-monitor hardware after two gap-closure rounds; post-validation code review fixed two further correctness bugs (migration re-corruption of an emptied disable set; a non-exception-safe minimize step).
- [x] Reentrancy guard: a toggle already in progress safely rejects a second concurrent request (CORE-06) — Validated in Phase 7: Shared Toggle-Orchestration Helper Extraction (non-blocking `Interlocked.CompareExchange` busy-flag on a new `ToggleOrchestrator`, 4 deterministic reentrancy tests, `ToggleService.cs` unchanged)
- [x] Tray residency, autostart, minimize-to-tray-on-close, and tray icon context menu (TRAY-01/02/03/04/05) — Validated in Phase 8: Tray Residency, Autostart & Toast Notification. Rig-validated after fixing a genuine `--tray` hidden-startup bug. Revised in Phase 11: TRAY-01's close-to-tray behavior is now an independent `CloseMinimizesToTray` Settings preference (default off), plus a new independent `MinimizeToTray` preference, with tray-icon existence derived live. Phase 11's critical lockout bug (both preferences off + window hidden → no reachable UI) fixed across two commits and re-verified on the real rig at v1.1 milestone close (2026-08-01).
- [x] Toast/status notification on toggle (NOTIF-01) — Validated in Phase 8 (`NotifyIcon.ShowBalloonTip`, shared `ToggleResultFormatter`)
- [x] Global hotkey trigger, with registration-failure surfacing (TRIG-01) — Validated in Phase 9: Global Hotkey Trigger. Rig-confirmed toggle-from-anywhere including tray-hidden, conflict surfacing with Moza Companion running, and a non-corrupting Settings-dialog race.

### Validated (v1.2)

- [x] GUI restyle: theme-aware, modern flat controls following Windows system light/dark mode — title bar (DWM `DWMWA_USE_IMMERSIVE_DARK_MODE`), every control (grid, hotkey box, buttons, panels), Windows-11 Mica/rounded corners, live theme-follow while running (THEME-01 through THEME-06) — Validated in Phase 12: Theme Infrastructure & Live Theme-Following. Rig-validated on real Windows 11 hardware after one gap-closure round: the first rig pass failed the title bar and buttons (an unproven bet that .NET's `Application.SetColorMode` alone would recolor them) plus an unenumerated audio-ComboBox gap; a code review root-caused all three, a gap-closure plan fixed them with an explicit per-control theming pattern (including a deliberate `dotnet/winforms#13897` hover/pressed workaround), and a second rig pass confirmed all three fixed on real hardware.
- [x] Redesigned tray icon pair + exe/taskbar icon: silhouette-distinct (monitor vs. steering wheel, no color-only differentiation), monochrome self-contained-contrast tray icons, color-treated exe icon, multi-resolution `.ico` (8 frames, 16-48px tray; 6 frames, 16-256px exe) via a new dev-time `RigToggle.IconGen` GDI+ generator (ICON-01 through ICON-04) — Validated in Phase 13: Tray & App Icon Redesign. Rig-validated on real Windows 11 hardware after one gap-closure round: the first rig pass approved shape/legibility/DPI/exe-icon, but a code review afterward found the outline-drawing approach (`GraphicsPath.DrawPath` on a combined multi-shape path) produced real seam artifacts the human hadn't caught at a glance; fixed via stroke-then-fill compositing plus a new automated pixel-level diagnostic, re-confirmed clean on a second rig pass.
- [x] GitHub-ready README.md: three live-endpoint badges (build status, license, latest release), generic feature overview + problem statement, download+build instructions, four screenshot placeholders (DOCS-01 through DOCS-03) — Validated in Phase 14: README & Release Documentation. Delivering this required real backing infrastructure beyond docs: a new MIT LICENSE, two GitHub Actions workflows (build + tag-triggered release with exe attachment), the repo flipped from private to public, and v1.0/v1.1 backfilled as notes-only GitHub Releases. Live-verified end to end (repo public, Build workflow green, all three badges rendering correct values, releases present) after fixing a real bug caught only by that live verification: `build.yml` was scoped to `branches: [main]` but this repo's actual default branch is `master`.

### Validated (v2.0)

- [x] User can leave the companion app launch target unset; toggling skips launch/focus (Rig direction) and minimize (Normal direction) entirely, with no error (APP-04) — Validated in Phase 15: Optional App & Audio Targets
- [x] A configured-but-missing app path still surfaces as a real failure, not silently treated as unset (APP-05) — Validated in Phase 15
- [x] User can leave the Rig-mode audio device unset; toggling to Rig mode skips Rig-direction audio switching entirely (AUDIO-03) — Validated in Phase 15
- [x] User can configure a Normal-mode audio device that actually applies on toggle to Normal mode, replacing snapshot-based restore; leaving it unset skips Normal-direction audio switching (AUDIO-04) — Validated in Phase 15, rig-confirmed the Windows default device actually switches
- [x] A configured-but-invalid audio device still surfaces as a real failure, not silently skipped (AUDIO-05) — Validated in Phase 15, rig-confirmed with a removed USB device
- [x] User can configure which monitors are enabled/disabled specifically for Normal mode, independent of and symmetric to Rig mode's existing config (DISPLAY-09) — Validated in Phase 16: Normal-Mode Explicit Monitor Config & Mode-Store Redesign
- [x] Toggling to Normal mode applies the explicitly configured Normal-mode monitor set directly, not a snapshot restored from before the last toggle (DISPLAY-10) — Validated in Phase 16, rig-confirmed
- [x] App correctly reports which mode (Rig/Normal) it's in immediately after an app restart, even with no snapshot file on disk (DISPLAY-11) — Validated in Phase 16 (`IModeStore`-backed persisted flag)
- [x] A crash or kill mid-toggle is detected on next launch via a persisted marker and communicated to the user (DISPLAY-13) — Validated in Phase 16; DISPLAY-13's exact-crash-mid-toggle rig scenario was formally waived rather than live-tested (user judged it niche/low-probability), recorded as a documented override in `16-VERIFICATION.md`
- [x] New GUI panel shows one row/tile per detected monitor with live on/off status via icon, not just text (PANEL-01) — Validated in Phase 17: Manual Monitor Panel & Shared Safety Guard
- [x] User can enable/disable any individual monitor directly from the panel, independent of the Rig/Normal toggle, taking effect immediately (PANEL-02) — Validated in Phase 17
- [x] Panel's monitor list and status update live on connect/disconnect while open (PANEL-03) — Validated in Phase 17
- [x] Disabling a monitor from the panel is gated by the same `SkipMonitorConfirmation` setting as the Rig/Normal toggle (PANEL-04) — Validated in Phase 17
- [x] Panel includes an Identify action overlaying a number on each physical screen (PANEL-05) — Validated in Phase 17
- [x] The "at least one monitor enabled" safety guard is enforced identically whether attempted via Rig toggle, Normal toggle, or the manual panel (DISPLAY-12) — Validated in Phase 17; static audit confirmed exactly one implementation reached by all three mutation paths
- [x] Dead snapshot-restore code (`Restore()`/`RestoreViaReconstruction()` and related models) removed after preserving any rig-specific knowledge it encoded (CLEANUP-01) — Validated in Phase 18; CCD findings preserved in `.planning/debug/knowledge-base.md`
- [x] Codebase shows measurably less duplication/cruft with no user-facing behavior change (CLEANUP-02) — Validated in Phase 18
- [x] Self-contained exe measurably smaller via `EnableCompressionInSingleFile`, `SatelliteResourceLanguages=en`, `InvariantGlobalization=true`, and the NAudio meta-package split, without IL trimming (PERF-01) — Validated in Phase 18: 116,946,229 → 49,356,430 bytes (57.79% reduction)
- [x] A full toggle round trip and cold autostart boot verified working on real rig hardware after the exe-size changes (PERF-02) — Validated in Phase 18, rig-confirmed

### Validated (v2.1)

- [x] The Rig/Normal toggle is a custom-drawn toggle-switch control (track + thumb), distinguishable by shape/position alone, not color (THEME-08) — Validated in Phase 20: Custom Toggle-Switch Control. Rig-verified after 2 fix rounds (row sizing/focus-ring clipping, then an action-row merge with Identify + Identify's corner rounding for visual consistency); a post-rig code review then found and fixed a real concurrency race (the confirm dialog's nested message loop wasn't holding the same exclusive-access lease `OnTileAction` already uses, letting a hotkey/tray toggle mutate state while the dialog was open) plus three lower-severity findings (focus-ring margin math, keyboard-autorepeat re-firing, two unguarded settings loads).
- [x] Key interactive elements (monitor tiles, toggle switch, Identify/Settings focus rings) pick up the user's live Windows accent color instead of a fixed palette, updating live on accent change without restart, matching Settings > Colors including for a custom non-default accent (THEME-07) — Validated in Phase 21: Accent-Color Reading & Live Update. `IThemeProvider` extended with `AccentColor`/`AccentColorChanged`, read registry-primary (`HKCU\...\DWM\AccentColor`) with `DwmGetColorizationColor` fallback, diffed inside the existing `SystemEvents.UserPreferenceChanged` handler (no second subscription). Rig-verified on real Windows 11 hardware — all 3 success criteria passed, including the phase's one open technical question (registry byte-order: confirmed correct as implemented, no swap needed). Code review found 2 non-blocking warnings (byte-order math lacks unit coverage; `Color` equality vs `.ToArgb()` in change detection) — tracked, not yet fixed.

### Active

Continuing v2.1 requirements (SettingsForm layout pass, THEME-09) — see `.planning/REQUIREMENTS.md`.

### Out of Scope

- Guaranteed true "close main window, keep process running" — not reliably possible to force externally on an arbitrary app; best-effort minimize is the fallback, not a guarantee
- Elevated/Task-Scheduler autostart — would reintroduce the UIPI cross-process-focus problem the v1.0 H9 debug session worked around; plain non-elevated Registry `Run` key is sufficient
- Hotkey chord/sequence engine — unused complexity for a single binding, single action
- Full Windows App SDK / MSIX toast packaging — conflicts with the standalone self-contained-.exe distribution constraint
- Per-monitor sets keyed by index/position instead of stable `DevicePath` — already burned once in v1.0
- Toggle history/log (LOG-01) — deferred twice now (v1.0, v1.1); tracked in v2 backlog, still lower priority
- CLI trigger + single-instance IPC (TRIG-02/TRIG-03) — scoped as Phase 10 for v1.1, never built. Tray (Phase 8) and global hotkey (Phase 9) already cover toggling without the GUI open; decided permanently out of scope at v1.1 close, not a v2 candidate.

## Context

- Personal single-user tool for a sim-racing setup: a Moza wheel/pedals rig sits to the right of the desk with its own monitor and its own speakers (rig mode audio/video). The primary desk monitor and a headset are the normal-use defaults.
- Problem driving this: games launch on the primary monitor by default, and some games (e.g. BeamNG.drive) actively misbehave (self-minimize) when run on what Windows considers a secondary display. The fix is making the primary monitor genuinely absent from Windows' display list while racing.
- No existing single app does this exact combination (monitor disable + audio switch + companion-app launch + tray/hotkey automation + multi-monitor sets as one preset toggle), though individual building blocks exist elsewhere. This project composes those capabilities into one custom GUI tool.
- Shipped state as of v1.2 close (2026-08-04): ~8,870 LOC C# across the solution (now including a dev-time-only `RigToggle.IconGen` generator project alongside Core/Windows/App/Tests), self-contained win-x64 single-file publish. v1.2 added 107 commits over ~2.3 days on top of v1.1's 186-commit, ~6-day build. Repo (`bpivk/monitor-toggle`) is now public with CI (GitHub Actions build+release workflows) and real GitHub Releases (v1.0, v1.1) — none of that existed before v1.2.
- Shipped state as of v2.0 close (2026-08-09): 165 commits over 5 days on top of v1.2, net +17,474/-1,971 lines across 120 files. The snapshot-restore subsystem (monitor + audio) is fully gone from the codebase — replaced by explicit `IModeStore`-tracked Rig/Normal configs — and the self-contained publish exe shrank 57.79% (116.9 MB → 49.4 MB) via MSBuild config alone, both confirmed on real rig hardware, not just build-output diffs.
- Five rig-discovered `WindowsDisplayAPI`/CCD findings that the deleted `Restore()`/`RestoreViaReconstruction()` code encoded were extracted into `.planning/debug/knowledge-base.md` before deletion (Phase 18, CLEANUP-01), so that operational knowledge isn't lost with the code.
- Post-v1.0-ship hardening (2026-07-26): Moza Companion window-focus-manipulation bug (H9) root-caused and fixed via relaunch-based (`ShellExecute`) activation instead of raw `SetForegroundWindow`/`ShowWindow` calls — see `.planning/debug/resolved/moza-foreground-focus.md`.
- v1.1 rig-discovered/code-review-found bugs, all fixed and verified: `GetAllMonitors()` duplicate-row/dual-primary dedup; `Restore()` Source-ID staleness for enable-set monitors; migration guard re-corrupting an emptied disable set; non-exception-safe companion-app minimize step; `--tray` hidden-start not actually suppressing the window; autostart save-failure recovery itself throwing unhandled; hotkey owner-window-destroyed timing bug; Escape-closes-Settings-during-hotkey-capture; Phase 11's tray-preference lockout bug (two fix commits, rig-reverified at milestone close).
- Known limitation carried forward unpatched: `LaunchOrFocus`/`MinimizeIfRunning` derive the running-process name from the configured launch-target path via `Path.GetFileNameWithoutExtension` — if the user configures a `.lnk` (not the target `.exe` itself), toggle-back's minimize may silently no-op. Documented, out of scope.
- v1.2 CI hardening gaps flagged by code review, advisory/non-blocking, not yet addressed: `build.yml` has no explicit `permissions:` block despite running on `pull_request`; all three GitHub Actions are pinned to floating major-version tags rather than commit SHAs; `release.yml` publishes on tag push with no build/test gate beforehand; neither workflow sets `timeout-minutes`. Full detail: `.planning/phases/14-readme-release-documentation/14-REVIEW.md`.
- Phase 15 code-review debt flagged for Phase 18 cleanup, advisory/non-blocking: `IAudioController.Restore` is now dead in production (Normal-mode audio uses `SetDefault` instead) but the interface/implementations still carry it; `SettingsForm.cs` treats "unset" as `is null`/`is not null` while `ToggleService.cs` uses `string.IsNullOrEmpty` — a latent semantic mismatch if `""` were ever persisted instead of `null`. Full detail: `.planning/phases/15-optional-app-audio-targets/15-REVIEW.md`.

## Constraints

- **Platform**: Windows only — no cross-platform requirement
- **Distribution**: Standalone .exe — implies a compiled/self-contained runtime (e.g. .NET self-contained publish), not a bare interpreted script requiring a separately-installed runtime
- **Monitor control**: Must achieve true OS-level display disable/enable (Windows CCD API or equivalent), not merely a monitor power signal — power-off leaves Windows still treating the display as connected/active
- **Audio control**: Must be able to set the Windows default audio playback device programmatically
- **App control**: Must be able to detect if the Moza Companion app is already running (to avoid duplicate launches) and manipulate its window (focus / minimize) via Win32 window APIs
- **Target state**: Both Rig mode and Normal mode apply an explicit, independently configured monitor set and audio device (revised at v2.0 — superseded the original v1.0 "snapshot the state before toggle and restore it exactly" constraint; the snapshot-restore mechanism itself was removed in Phase 18/CLEANUP-01)

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| True OS-level monitor disable, not power-off | Games must see only one display; power-off doesn't remove it from Windows' display list | Validated Phase 4 |
| Remember-previous-state restore, not a fixed "normal" preset | Toggle-back should always match whatever was actually active before | Validated Phase 3 (audio), Phase 4 (monitor), hardened Phase 5 — superseded at v2.0 by explicit symmetric Normal-mode config (see below), original mechanism deleted Phase 18 |
| Standalone .exe packaging | No runtime-install friction | Validated Phase 5 |
| Manual launch only, no autostart/tray/hotkey in v1 | Keep v1 scope tight; validate the GUI-click flow before adding automation | Held for v1.0 — TRIG-01/TRAY-01/NOTIF-01 taken up and shipped in v1.1 |
| Best-effort minimize instead of guaranteed close-without-kill for Moza Companion | Can't be forced externally unless the target app supports it itself | Validated Phase 3 |
| Stop-on-first-failure for toggle-to-rig, isolate-and-continue for toggle-to-normal | Forward steps have real dependencies; restore steps recover independent hardware state | Validated Phase 5, rig-confirmed |
| Relaunch-based (`ShellExecute`) app activation instead of window-handle focus manipulation | Raw external `SetForegroundWindow`/`ShowWindow` desyncs Moza's own window procedure, permanently disabling its close button | Validated post-ship 2026-07-26, rig-confirmed both directions |
| Settings accepts any app (drag-and-drop `.lnk`/`.exe`), not a Moza-specific hardcoded path | Free side effect of the relaunch redesign no longer needing Moza-specific window-finding logic | Validated post-ship 2026-07-26 |
| Diagnostic `debug.log` gated behind an off-by-default Settings checkbox | Keeps the capability available without unconditional disk writes | Validated post-ship 2026-07-26 |
| `GetAllMonitors()` dedups by stable `DevicePath`, sourcing Active/Primary state only from `GetActiveMonitors()` | `GetAllPaths()` returns one entry per historical CCD path, causing duplicate rows and dual-primary | Validated Phase 6, rig-confirmed |
| `Restore()`'s cache-replay fast path requires an exact `SetEquals`; any enable-set monitor or stale cache routes through live reconstruction | An intervening CCD mutation can renumber a Source-ID between capture and replay | Validated Phase 6, rig-confirmed |
| Settings-migration guard keys off `MonitorsToDisable is null` only, never null-or-empty | Prior check re-injected the legacy v1.0 monitor into the disable set even after deliberate emptying | Validated Phase 6 |
| Reentrancy guard (CORE-06) is a new `ToggleOrchestrator` wrapper, not logic inside `ToggleService` | Keeps `ToggleService` a pure, unit-tested step sequencer; gives every future trigger source one obvious, already-guarded entry point | Validated Phase 7, 35/35 tests pass |
| `--tray` hidden-startup uses `Application.Run(new ApplicationContext())` with no `MainForm` reference | The Microsoft-doc-cited `ApplicationContext(mainForm)` pattern did not actually suppress `Show()` on this runtime | Validated Phase 8, rig-confirmed |
| Tray icon existence derived as `CloseMinimizesToTray \|\| MinimizeToTray`, applied live on Settings-Save | Lets close-to-tray and minimize-to-tray be configured as two independent preferences instead of one combined flag | Validated Phase 11, rig-confirmed after fixing a lockout bug found by code review |
| Phase 10 (CLI trigger + single-instance IPC, TRIG-02/TRIG-03) permanently out of scope, not delivered | Phase 8/9's tray and hotkey triggers already deliver the "toggle without opening the GUI" core value this milestone targeted; a CLI/IPC path for external tools was judged not needed | Decided at v1.1 close 2026-08-01 |
| Explicit-color `FlatStyle.Flat` button theming (with explicit `BorderSize=0` + hover/pressed color overrides), not `FlatStyle.System` | `Application.SetColorMode` doesn't recolor `FlatStyle.System`'s native visual-styles rendering pipeline at all — rig-proven false on real Windows 11; explicit-color `Flat` is deterministic but reintroduces `dotnet/winforms#13897` unless hover/pressed colors are also set explicitly, which this fix does | Validated Phase 12 gap closure (12-05/12-06), rig-confirmed including interaction states |
| Manual `DWMWA_USE_IMMERSIVE_DARK_MODE` call, not relying on `Application.SetColorMode` to own the title bar | Original bet that `SetColorMode` alone would flip the DWM title-bar attribute was rig-disproven | Validated Phase 12 gap closure |
| Steering-wheel vs. desktop-monitor motif for rig/normal tray icons, procedurally drawn via GDI+ (not sourced artwork) | Strong shape contrast readable at 16px without color, maps directly onto the user's actual sim-racing setup; zero new asset-pipeline dependency | Validated Phase 13 |
| Stroke-then-fill outline compositing for icon geometry, not `FillPath`+`DrawPath` on a combined multi-shape `GraphicsPath` | `DrawPath` strokes every touching/overlapping sub-shape's boundary independently rather than a merged contour, producing seam artifacts a human rig glance didn't catch but code review + pixel-level diagnostic did | Validated Phase 13 gap closure (13-04), rig-confirmed |
| GitHub repo flipped private → public, badges wired to live GitHub Actions/shields.io endpoints (never static/decorative) | A "GitHub-ready README" with badges only makes sense on a public repo; CONTEXT.md explicitly rejected decorative badges not backed by real CI/license/release state | Validated Phase 14, live-verified |
| README kept deliberately generic (no "Moza"/"BeamNG" naming) despite internal docs (CLAUDE.md/PROJECT.md) using those names | User's explicit choice during Phase 14 discuss — a public-facing doc reads more general-purpose than the internal project framing; downstream agents must not "correct" this back | Validated Phase 14 |
| `build.yml` triggers on `branches: [master]`, not the more common `[main]` default | This repo's actual default branch is `master` — a bug where the workflow was originally scoped to `main` (never firing) was caught only by Phase 14's live human-verify checkpoint, not by local acceptance-criteria grep checks | Validated Phase 14, fixed and re-verified live |
| Distinct `Skipped` toggle-step outcome, never conflated with `NotAttempted` or `Failed` | An unset target must never trigger a "did not fully complete" warning, but a configured-but-broken target must still fail loudly — a two-state boolean couldn't represent both | Validated Phase 15, rig-confirmed both toggle directions |
| Normal-mode audio device applies via `SetDefault`, not `Restore(snapshot.Audio)` | AUDIO-04 requires the configured Normal-mode device to genuinely become the Windows default on toggle-to-Normal, not whatever was previously snapshotted | Validated Phase 15, rig-confirmed in the Windows sound flyout |
| `IsFullyConfigured`/Save gate relaxed to the monitor-set check only; a broken (not merely unset) app/audio target still blocks Save | Now-optional targets shouldn't block Save just for being unset, but "broken" (moved .exe, removed device) must remain distinguishable from "unset" everywhere, including at Save time | Validated Phase 15, rig-confirmed |
| Mode tracked via a new `IModeStore`-backed persisted flag, not snapshot-file presence | Snapshot-file presence was only ever a proxy for "which mode am I in"; DISPLAY-11 requires correct mode reporting even with no snapshot on disk | Validated Phase 16, rig-confirmed |
| Normal mode applies its own explicit, symmetric monitor set directly, not a restored pre-toggle snapshot | Matches how Rig mode's monitor config already works; removes the "restore whatever was there before" ambiguity DISPLAY-10 was scoped to fix | Validated Phase 16, rig-confirmed |
| Disk-persisted `ToggleInProgressMarker` wraps every guarded toggle, checked by a new `StartupRecoveryChecker` at launch | DISPLAY-13 requires a crash mid-toggle to be detectable and communicated on next launch, not silently ignored | Validated Phase 16 (exact crash-mid-toggle scenario formally waived by user as niche, not tested live) |
| `ToggleOrchestrator.BeginExclusiveMonitorAccess()` lease shares the existing `_busy` flag with `RunGuarded` | Gives the new Manual Monitor Panel and the Rig/Normal toggle bidirectional mutual exclusion without a second locking mechanism | Validated Phase 17, rig-confirmed |
| Manual Monitor Panel mutates monitors through the exact same `IMonitorController.DeactivateMonitors`/`ActivateMonitors` calls the toggle already uses | Gives DISPLAY-12's "at least one monitor enabled" guard a single shared implementation across all three mutation paths, with zero new guard code to keep in sync | Validated Phase 17, static audit + rig-confirmed |
| Snapshot-restore subsystem (`Restore`/`RestoreViaReconstruction`, related models) fully deleted, not deprecated-in-place | Confirmed genuinely dead once Phase 16's explicit-config rewrite shipped; rig-specific CCD knowledge it encoded was extracted to `.planning/debug/knowledge-base.md` first | Validated Phase 18, rig-confirmed no behavior change |
| Exe-size reduction via four MSBuild-only levers (compression, satellite-language trim, invariant globalization, NAudio meta-package split), `PublishTrimmed` left explicitly false | IL trimming's static analysis was already documented as unsafe for this codebase's COM/P-Invoke-heavy surface; MSBuild config alone still delivered a 57.79% size cut | Validated Phase 18, rig-confirmed cold boot + toggle round trip |
| `ToggleSwitch_ActionRequested`'s exclusive-access lease is released before calling `ToggleToRigMode()`/`ToggleToNormalMode()`, never held across it | `BeginExclusiveMonitorAccess()` and the guarded toggle methods' `RunGuarded()` share the same `_busy` flag (Phase 17 decision, above) — holding the lease into the guarded call would make every toggle self-reject as "already in progress"; the lease only needs to span the confirm dialog's nested message loop, not the toggle itself | Validated Phase 20, code-review-found (CR-01) and fixed before close |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-08-11 — Phase 21 (Accent-Color Reading & Live Update, THEME-07) shipped, rig-verified.*
