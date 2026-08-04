# Rig Toggle

## What This Is

A Windows GUI utility that switches between "normal desktop mode" and "Moza rig mode" with one click — from the GUI, a tray menu, or a global keyboard shortcut. Toggling to rig mode disables the primary monitor at the OS level (so games default to the rig monitor instead), switches the default audio output to the rig speakers, and launches the Moza Companion app. Toggling back restores the exact previous monitor/audio state and minimizes the Moza Companion app. The app can run tray-resident with autostart, and supports arbitrary multi-monitor configurations (not just a single primary monitor). The GUI follows the Windows system light/dark theme live, and the tray/exe icons use a shape-distinct monitor-vs-steering-wheel motif. Distributed via a public GitHub repo (`bpivk/monitor-toggle`) with a GitHub-ready README, CI, and tagged releases. Built for a single user's personal sim-racing rig setup.

## Core Value

A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches (e.g. BeamNG.drive minimizing itself) reliably open on the rig monitor — and just as reliably restores everything to exactly how it was before.

## Current State

**Shipped: v1.2 Visual Polish & Documentation (2026-08-04)**

v1.2 replaced the default-WinForms look with a theme-aware, modern UI (live light/dark theme-follow across every control, Windows-11 Mica/rounded corners), gave the tray icons genuine shape-based visual distinction (monitor vs. steering-wheel silhouettes, colorblind-safe), and shipped a GitHub-ready README backed by real infrastructure that didn't exist before this milestone: an MIT LICENSE, two GitHub Actions workflows (CI build/test + tag-triggered release with exe attachment), the repo flipped from private to public, and v1.0/v1.1 backfilled as GitHub Releases. Two real bugs were caught only by rig/live verification, not static checks: a `dotnet/winforms` dark-mode theming false assumption (Phase 12 gap closure) and a `main`-vs-`master` CI trigger mismatch (Phase 14).

**Not yet done:** the four README screenshot placeholders (`docs/screenshots/*.png`) are intentionally still broken-image links — real screenshots must be supplied by the user from the rig (no Windows GUI in this build environment).

## Current Milestone: v2.0 Configurable Monitors, Optional Targets & Cleanup

**Goal:** Replace snapshot-restore with explicit per-mode monitor configuration, make app/audio targets optional, add a live manual monitor toggle panel, and reduce exe size + clean up code.

**Target features:**
- App path becomes optional — when unset, toggle skips launch/focus/minimize entirely
- Audio devices become optional per role — when unset, toggle skips audio switching entirely
- Normal mode gets an explicit configured monitor set (which monitors on/off), replacing snapshot-restore
- Rig mode's existing monitor set config stays, now symmetric with Normal mode's
- New GUI panel: live, on-demand enable/disable of any monitor, independent of the Rig/Normal toggle, with per-monitor status shown via monitor icons (not just text)
- Safety constraint carried over: at least one monitor must always remain enabled, enforced across both the toggle action and the manual panel
- Reduce self-contained exe size (without enabling IL trimming — COM/P-Invoke code gets falsely stripped, per existing project guidance)
- Code quality/cleanup pass across the 3-milestone-old codebase

**Key context:** This milestone revises the project's stated Core Value ("restores everything to exactly how it was before") — Normal mode moves from snapshot-restore to an explicitly configured target state, matching how Rig mode already works. The "at least one monitor enabled" safety guard is explicitly preserved across this redesign. Exe size reduction must find savings other than trimming, since `PublishTrimmed=true` is documented as unsafe for this codebase's COM/P-Invoke-heavy surface.

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
- [x] Toggling back restores the exact previous default audio device across all relevant audio roles — Validated in Phase 3
- [x] Toggling to rig mode launches the Moza Companion app if it isn't already running; if it's already running, brings it to focus instead of launching a duplicate instance — Validated in Phase 3, mechanism superseded post-ship (H9 focus-manipulation bug, fixed via relaunch-based `ShellExecute` activation, rig-confirmed both directions). Settings generalized to accept any `.lnk`/`.exe` target.
- [x] Toggling back minimizes the Moza Companion app window (best-effort) — Validated in Phase 3; refined post-ship to skip the minimize call when the window is already hidden/tray-only
- [x] Toggling to rig mode disables the primary monitor at the OS level (true disable) — Validated in Phase 4: Monitor Control (Production)
- [x] Toggling back restores the exact monitor configuration that was active immediately before toggling to rig mode — Validated in Phase 4, hardened in Phase 5's crash-recovery fallback
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

### Active

Defining/executing remaining v2.0 requirements (DISPLAY-09 through DISPLAY-13 and beyond) — see `.planning/REQUIREMENTS.md`.

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
- **State restore**: Must snapshot the active monitor + audio configuration at toggle-time so toggle-back can restore that exact prior state, not a fixed default

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| True OS-level monitor disable, not power-off | Games must see only one display; power-off doesn't remove it from Windows' display list | Validated Phase 4 |
| Remember-previous-state restore, not a fixed "normal" preset | Toggle-back should always match whatever was actually active before | Validated Phase 3 (audio), Phase 4 (monitor), hardened Phase 5 |
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
*Last updated: 2026-08-04 — Phase 15 (Optional App & Audio Targets) complete, rig-verified.*
