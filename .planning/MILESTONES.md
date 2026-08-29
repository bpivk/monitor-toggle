# Milestones

## v2.2 Auto-Update, Single-Instance Guard & Smaller Footprint (Shipped: 2026-08-29)

**Phases completed:** 3 phases, 10 plans, 26 tasks

**Key accomplishments:**

- Added one MSBuild deny-list target excluding 7 unused WinForms Design/VB assemblies, cutting the self-contained exe by 2,596,463 bytes (5.26%) — all three tasks complete, including operator rig verification on real Windows hardware.
- Named cross-process Mutex single-instance guard with a level-triggered Mutex-based readiness handshake (RegisterWindowMessage/PostMessage(HWND_BROADCAST) activation signal, 3x retry) closing Pitfall 8's startup race — built cross-platform-testable after discovering named EventWaitHandle/Semaphore are Windows-only in .NET.
- `--apply-update` startup-gate bypass: `StartupArgs.TryGetApplyUpdateArgs` (opaque trailing-payload parse contract, option-a) checked as the first branch in `Main()`, transferring control to a deliberately empty `UpdateApplyEntryPoint.Run` before the single-instance guard is ever touched.
- Real child-process xUnit suite proving INSTANCE-01/INSTANCE-02/UPDATE-07 end-to-end, plus two real production bugs found and fixed during the Task 3 operator checkpoint: a SetColorMode/SystemEvents race crashing duplicate launches, and a foreground-activation grant (`AllowSetForegroundWindow`) that was silently ineffective.
- `WaitForInstanceReady` now catches `AbandonedMutexException` and treats an abandoned-but-acquired wait as success, closing the phase's one blocking crash gap; `Acquire()`'s readiness-mutex construction can no longer escape as an unhandled exception. Task 3's blocking checkpoint was closed by explicit operator authorization on PARTIAL evidence — build succeeds and the app functions normally on real Windows hardware, but the specific flakiness check and live CR-01 repro this task originally asked for were not run. See "Task 3: Operator Verification" below for the exact evidence and what remains open.
- End-to-end GitHub-release auto-update slice — version-stamped exe queries `/releases/latest`, shows a themed confirm dialog, downloads the asset, self-replaces at its original path via a temp-copy helper, and relaunches — proven end-to-end and covered by 31 new automated tests.
- Publishes a SHA256 checksum alongside every release exe and verifies it in the still-running old process before any code path can reach the swap step — a mismatch or missing checksum now surfaces as the same D-08 Warning toast as any other apply failure.
- Retained-backup + applied-but-unconfirmed marker + above-the-guard auto-rollback state machine, so a failed or crash-looping update can never leave the app stranded (UPDATE-05, D-09) — proven by 6 marker-store unit tests and 4 real child-process swap tests.
- Three-way Update Now/Later/Skip prompt result with a per-version persisted skip (compared numerically, never by string) threaded through UpdateOrchestrator, plus a hand-rolled Markdown-lite formatter/renderer pair that turns a GitHub release body into styled RichTextBox runs with a verbatim empty-notes fallback.
- UpdateOrchestrator.CheckOnDemandAsync shares CheckOnLaunchAsync's fetch/compare/confirm/apply sequencer via a new private CheckAsync body, always overriding a prior skip and always surfacing a distinct CheckFailed outcome; MainForm.PerformManualUpdateCheck is the one shared body reached from a new tray menu item and a new Settings button, reporting honest Info/Warning-icon toasts.

---

## v2.1 Modern UI Redesign & Theme Backlog (Shipped: 2026-08-17)

**Phases completed:** 5 phases, 19 plans, 36 tasks

**Key accomplishments:**

- `src/RigToggle.App/MonitorIconGeometry.cs`
- MainForm now renders a live, hotplug-reactive monitor-tile dashboard — one `MonitorTile` per detected monitor, numbered from a single canonical `DevicePath`-sorted list, laid out and auto-sized entirely arithmetically via a font-derived `Scaled()` helper, with the old full-width Monitors button replaced by a de-emphasized icon-only Settings gear.
- Tile clicks now mutate exactly one monitor through the same `IMonitorController` methods both toggle directions already use, gated by a lease-held-across-dialog and a disable-only confirmation prompt, while a ported Identify handler numbers its overlays from the same canonical list the tiles use.
- `MonitorPanelForm.cs` and `MonitorPanelForm.Designer.cs` are deleted outright (git rm, no shim), both entry points (Monitors button — already gone since Plan 19-02 — and the tray Monitors item, removed here) are gone, `MainForm` and the composition root both construct with five arguments instead of six, and the solution builds and tests clean at the same 81/81 baseline Plan 19-03 left.
- Task 1 (full regression gate + four static audits) is complete with zero source changes and zero audit failures; Task 2 (the rig-hardware checkpoint) is a blocking human-verify gate that requires real Windows display hardware and has not yet been run — this summary is intentionally partial and will be appended once the checkpoint resolves.
- Owner-drawn ToggleSwitch UserControl (track+thumb pill, three states, keyboard-activatable) plus ThemeApplier.ThemeToggleSwitch, built standalone and not yet wired into MainForm
- btnToggle/lblMode fully retired from MainForm; ToggleSwitch occupies the exact former slot, drives its state from RefreshUi(), and its ActionRequested handler is a byte-identical port of the four-gate BtnToggle_Click body
- Regression gate green (build 0 errors, 81/81 tests) and all four static audits clean (Audit 1's stale-comment finding fixed, Audit 4's pre-existing accent-literal finding documented as non-blocking). Task 2's rig-hardware checkpoint APPROVED — 11/11 checks pass after 2 fix rounds (layout sizing/focus-ring clipping, then action-row merge/Identify corner rounding). All three Phase 20 ROADMAP success criteria VERIFIED.
- Extended `IThemeProvider` with a live `AccentColor`/`AccentColorChanged` pair, and made `WindowsThemeProvider` the sole reader — registry-primary (`HKCU\Software\Microsoft\Windows\DWM\AccentColor`) with a `DwmGetColorizationColor` fallback, never throwing, raising exactly once per genuine change.
- Repointed all five D-04 accent consumers (two MonitorTile properties, two ToggleSwitch properties, MainForm's focus-ring color) from the hardcoded `Color.FromArgb(0, 90, 158)`/`SystemColors.Highlight` dark/light pair to the live `IThemeProvider.AccentColor`, with a live accent flip now repainting through the existing `OnThemeChanged` -> `ApplyDashboardTheming()` funnel.
- Full-solution regression gate confirmed green at baseline (0 Errors, 4 pre-existing Warnings, 82/82 tests) and all five static audits of Phase 21's structural/safety properties passed with recorded command evidence; no source file was touched. The user personally ran the app on real Windows 11 hardware and reported a PASS, with explicit confirmation of the decisive byte-order check (pure red rendered as red, pure blue rendered as blue) -- closing the phase's one open technical contradiction. Both tasks are complete and Phase 21's three ROADMAP success criteria are now verified.
- Migrated SettingsForm's two monitor sections from hardcoded Panel+Location/Size positioning into a 50/50 Percent-split TableLayoutPanel scaffold (tlpRoot/tlpModeColumns), with each mode's audio picker moved out of the shared pnlAudioDevices panel into its own mode column (D-01).
- Completed SettingsForm's TableLayoutPanel/FlowLayoutPanel migration: built the flat, full-width shared section (D-02) with a reserved Phase 23 theme slot (D-04), right-aligned the Save/Discard buttons in their own growth-only row, and switched the form from a fixed 828x768 FixedDialog to a content-driven, edge-resizable Sizable window (D-05/D-06).
- Task 1 (build/test regression gate plus five static audits of pixel-positioning absence, control conservation, load-bearing-property preservation, grid/drag-drop wiring, and one-file blast radius) is complete and green. Task 2 (blocking rig-hardware DPI verification) is also complete: the user tested the published binary on real Windows hardware and reported two FAILs (Check 1 — monitor grid and audio picker missing from both mode columns; Check 3 — manual window resize does not work) before stopping the remaining 15 checks as not meaningfully evaluable against a broken 100%-scale layout. Both Phase 22 success criteria (SETTINGS-01, SETTINGS-02) are FAIL. Phase 22 is NOT complete and requires a gap-closure plan.
- Source-level fix for both defects the Phase 22 rig-hardware checkpoint found: added AutoSize to the two mode-wrapper Panels plus a documented 280px MinimumSize floor on tlpModeColumns (Bug B — grid/audio picker not rendering), and replaced Form.AutoSize with an explicit content-driven OnLoad override (Bug A — manual resize broken). Neither bug is confirmed fixed by this plan; that is Plan 05's rig checkpoint to establish.
- All 17 rig-hardware checks pass on real Windows 11 25H2 hardware after the Plan 04 fix. Both Phase 22 success criteria (SETTINGS-01, SETTINGS-02) are now confirmed. Both gaps recorded in `22-VERIFICATION.md` are closed. Phase 22 is complete.
- One shared `OverridableThemeProvider` decorator (preview ?? persisted `ThemeOverride` ?? live OS signal) wired into every `IThemeProvider` consumer via a single composition-root swap, plus an application-wide color mode now derived from that effective theme instead of hardcoded OS-follow.
- A System/Light/Dark radio group fills Phase 22's reserved slot in `SettingsForm`, applies live to the running app the instant it's clicked (before Save), and reverts to the last persisted override on Discard, Esc, or the window X — the one field in this form that intentionally does not wait for Save.
- Six static audits confirm Pitfall 6's three-copy consistency risk is closed with a single resolver, and all fifteen rig checks pass on real Windows hardware — all three Phase 23 success criteria are PASS, including the known open question (check 12's in-place Settings repaint) resolving cleanly with no residual gap.

---

## v2.0 Configurable Monitors, Optional Targets & Cleanup (Shipped: 2026-08-09)

**Phases completed:** 4 phases (15, 16, 17, 18), 19 plans
**Git range:** v1.2 → HEAD (165 commits, 120 files changed, +17,474/-1,971 lines)
**Timeline:** 2026-08-04 → 2026-08-09 (5 days)

**Key accomplishments:**

- Made the companion app and both audio-device targets genuinely optional in `ToggleService` — unset targets skip cleanly with no error, while a configured-but-broken target (missing exe, removed device) still surfaces as a real failure — and gave `NormalAudioDeviceId` real runtime effect for the first time (Phase 15, APP-04/05, AUDIO-03/04/05)
- Replaced snapshot-based Normal-mode restore with an explicitly configured, symmetric Normal-mode monitor set, moved "which mode am I in" off snapshot-file-presence onto a persisted `IModeStore` flag, and added a disk-persisted crash-in-progress marker with startup recovery dialogs (Phase 16, DISPLAY-09/10/11/13)
- Added a live Manual Monitor Panel (per-monitor status icons, immediate enable/disable, hotplug refresh, Identify overlay) that mutates monitors through the exact same controller calls as the Rig/Normal toggle, so the "at least one monitor enabled" safety guard is enforced from one shared codepath across all three mutation paths (Phase 17, PANEL-01..05, DISPLAY-12)
- Removed the now-dead snapshot-restore subsystem (`Restore`/`RestoreViaReconstruction` and related models) after preserving the rig-specific CCD knowledge it encoded, netting 751 fewer lines with zero user-facing behavior change (Phase 18, CLEANUP-01/02)
- Shrunk the self-contained publish exe from 116.9 MB to 49.4 MB (57.79% reduction) using four MSBuild-only levers — compression, satellite-language trim, invariant globalization, NAudio meta-package split — with no IL trimming and no code changes (Phase 18, PERF-01)
- All four phases rig-verified on real Windows 11 hardware (cold autostart boot, full toggle round trips, panel interactions) with no FAILs and no waived scenarios at close; all 20 v2.0 requirements shipped

---

## v1.2 Visual Polish & Documentation (Shipped: 2026-08-04)

**Phases completed:** 3 phases, 13 plans, 33 tasks

**Key accomplishments:**

- Core IThemeProvider/AppTheme contract, WindowsThemeProvider registry-read + live SystemEvents theme detection, and a DwmTitleBar facade over a new DwmSetWindowAttribute P/Invoke for Mica/rounded-corner requests — all built and tested green with no consumers wired yet.
- Application.SetColorMode wired as the very first Main() statement, one composition-root WindowsThemeProvider threaded into every form, and MainForm + MonitorConfirmDialog get full launch-time + SystemEvents-driven live theme-follow (title bar, controls, flat buttons, Windows-11 Mica/rounded corners) — SettingsForm's own per-control theming is deferred to plan 12-03.
- New `ThemeApplier` helper closes the confirmed `dgvMonitors` DataGridView and `txtHotkey` hand-rolled-`SystemColors` theming gaps, SettingsForm gains its own live theme-follow (subscribe/unsubscribe + marshaled `OnThemeChanged`) and Windows-11 DWM chrome, and all three `GroupBox`es become flat bordered `Panel`s with captions and zero layout drift — full rig-visual verification deferred to plan 12-04 per this phase's plan.
- Release build + publish succeeded; rig verification on Windows 11 found the title bar, all buttons, and the Settings audio-device dropdowns still rendering in the light/white system theme in dark mode
- Closes all three rig-confirmed dark-mode gaps from 12-04 (white title bar, white/light buttons, white audio-device dropdowns) by replacing three falsified "Application.SetColorMode will handle it" assumptions with explicit DWM/ThemeApplier overrides, plus the two low-cost robustness warnings (Dispose backstop, provider thread-safety lock) from the code review.
- Rig re-verification on Windows 11 confirms the 12-05 fixes closed all three gaps: dark title bar, dark buttons (including hover/pressed states), and dark audio-device ComboBoxes
- GDI+ icon generator (hand-rolled ICO writer, UI-SPEC-locked monitor/wheel/app geometry) executed end-to-end via a self-contained win-x64 publish + Wine, producing and round-trip-verifying regenerated `normal.ico`/`rig.ico` and a new `app.ico`.
- Fixed the tray NotifyIcon's DPI-blur defect by requesting `SystemInformation.SmallIconSize` in both `Icon` constructors, and wired `app.ico` as the compiled exe's native Win32 icon via `<ApplicationIcon>`.
- Rig verification on Windows 11 confirms the redesigned monitor/wheel tray icons and color exe icon satisfy all four ICON requirements — shape distinguishability, light/dark legibility, DPI sharpness at 100-200% scaling, and exe icon identity
- Rewrote IconGeometry.cs's outline compositing to stroke-then-fill (eliminating interior seam lines by construction), added a pixel-level interior-artifact diagnostic to Program.cs that genuinely verifies clean silhouettes (not just ICO byte-container validity), and tuned rig.ico's outline-pen-width and radial geometry after the diagnostic caught a real defect the naive doubled-outline fix introduced. Rig-confirmed on real Windows 11 hardware: both tray icons render as clean single silhouettes on light and dark taskbars, no seam artifacts remain -- CR-01 is resolved.
- MIT LICENSE plus two GitHub Actions workflows (windows-latest build+test on push/PR, tag-triggered self-contained exe publish+release) backing the README's badges and download flow with real, live infrastructure.
- COMPLETE — All three tasks done. Repo bpivk/monitor-toggle is public; v1.0 and v1.1 exist as notes-only GitHub Releases with zero attached assets; no v1.2 release exists.
- Rewrote root README.md with three live shields.io/GitHub-Actions badges, a generic feature overview and problem statement, download+build instructions, and four screenshot placeholders under a new docs/screenshots/ directory. Task 3's live-repo human verification passed after the orchestrator caught and fixed a real bug: build.yml was scoped to `branches: [main]` but this repo's actual default branch is `master`.

---

## v1.1 Automation & Multi-Monitor (Shipped: 2026-08-01)

**Phases completed:** 5 phases (6, 7, 8, 9, 11), 19 plans + 2 gap-closure quick tasks
**Git range:** v1.0 → HEAD (186 commits, 190 files changed, +17,214/-1,172 lines)
**Timeline:** 2026-07-26 → 2026-08-01 (6 days)

**Key accomplishments:**

- Generalized monitor control from one hardcoded primary monitor to independently-configurable N-monitor disable/enable sets, with a silent v1.0→v1.1 settings migration and a rig-validated CCD checkpoint covering reboot/sleep re-enable and combined disable+enable topology (Phase 6, DISPLAY-04 through DISPLAY-08)
- Extracted a shared, reentrancy-safe `ToggleOrchestrator` (non-blocking busy-flag guard) that every trigger — button, tray, hotkey — now routes through, so a toggle already in progress can never be corrupted by a second concurrent request (Phase 7, CORE-06)
- Added full tray residency: close-to-tray, autostart with Windows, tray context menu, mode-reflecting tray icon, and toast notifications for toggles triggered without the GUI open (Phase 8, TRAY-01 through TRAY-05, NOTIF-01)
- Added a configurable global hotkey that toggles the mode from anywhere in Windows, including while hidden in the tray, with registration-conflict failures surfaced in Settings instead of silently swallowed (Phase 9, TRIG-01)
- Made tray close/minimize behavior a genuine user preference — independent `CloseMinimizesToTray` and `MinimizeToTray` Settings checkboxes replacing Phase 8's fixed always-minimize-to-tray default, including a critical lockout bug found by code review, fixed, and rig-reverified at milestone close (Phase 11, revises TRAY-01)
- Nine real rig-discovered or code-review-found bugs fixed and verified across the milestone, including a `--tray` hidden-startup mechanism that silently failed to suppress the window and a settings-migration guard that could re-corrupt a deliberately-emptied monitor set

**Scope decision:** Phase 10 (CLI Trigger + Single-Instance IPC, TRIG-02/TRIG-03) was in the original v1.1 roadmap scope but was never planned or executed — Phase 11 was prioritized ahead of it after surfacing during Phase 9's rig checkpoint. Reviewed at milestone close and decided permanently out of scope rather than delivered or deferred: tray + hotkey triggers already cover every trigger path this project needs. Full detail: `.planning/milestones/v1.1-REQUIREMENTS.md`.

Full milestone detail: `.planning/milestones/v1.1-ROADMAP.md`, `.planning/milestones/v1.1-REQUIREMENTS.md`

---

## v1.0 MVP (Shipped: 2026-07-26)

**Phases completed:** 5 phases, 18 plans, 49 tasks

**Key accomplishments:**

- Throwaway .NET 10 console spike using WindowsDisplayAPI's CCD topology-path-removal (PathInfo.ApplyPathInfos) with dual-oracle (WindowsDisplayAPI + Screen.AllScreens) detach verification and delayed hotplug re-check, to be built/run by the user on the AMD Radeon rig PC
- Three user-facing markdown docs (RUN-INSTRUCTIONS, RESULTS-TEMPLATE, FALLBACK) that turn the Wave-1 spike tool into a self-contained round-trip the user can execute on the rig PC and report back from, keeping the admin pnputil escalation strictly separate from the primary non-elevated tool
- Four-project .NET 10 solution (Core/Windows/App/Tests) scaffolded with WindowsDisplayAPI 1.3.0.13 + NAudio 2.3.0 isolated to RigToggle.Windows, plus all 5 interfaces and 6 models Phase 2's downstream plans implement against.
- Atomic JSON persistence (JsonSettingsStore/JsonSnapshotStore) and a fully Windows-API-free ToggleService orchestrating snapshot-before-mutate sequencing (D-08/D-14), proven by 11 hand-rolled xUnit facts against recording test doubles — no Windows machine required to exercise this logic meaningfully.
- Implemented the three `RigToggle.Windows` adapter classes with real WindowsDisplayAPI/NAudio/Process enumeration for monitor, audio, and companion-app detection, while every mutating method (Disable, Restore, SetDefault, LaunchOrFocus, MinimizeIfRunning) remains a documented no-op stub for Phases 3/4.
- WinForms Settings modal (Monitor / Audio Devices / Application Path GroupBoxes) bound to real IMonitorController/IAudioController enumeration, with D-10 stale-selection warnings, D-12 Save-gating, and .exe-filtered browse persisting via ISettingsStore.
- Wired the complete Phase 2 app end-to-end — real display/audio/process enumeration feeding a fixed-size Main window with snapshot-derived mode indicator, live companion status, and a modal Settings dialog — and had it confirmed working on the actual Windows rig, including a true app-restart persistence check.
- Reshaped AudioState from a single DefaultDeviceId into three independent per-role (Console/Multimedia/Communications) snapshots, with per-role defensive capture and a defensive JsonSnapshotStore.Load that no longer crashes on stale-shaped state.json.
- Real `IPolicyConfig` COM interop switches and restores the Windows default playback device across all three audio roles (console/multimedia/communications), with a NAudio read-back verify-and-throw safety net replacing the Phase 2 no-op stubs.
- Hand-rolled user32 P/Invoke drives real companion-app launch/focus/minimize control, and a File.Exists guard in ToggleService now fails fast before touching any state when the companion app path is missing.
- Closed the stuck-in-Rig-mode gap: `WindowsAudioController.Restore` now falls through to friendly-name matching for a present-but-stale `DeviceId`, isolates each audio role's apply/verify so one role's failure doesn't abort the others, and `ToggleService.ToggleToNormalMode` now always reaches `MinimizeIfRunning`/`snapshotStore.Clear()` even when restore throws.
- Extended the Phase 1 throwaway spike tool with a `--disable-primary` mode implementing RESEARCH Pattern 1 (delta-shift survivor reconstruction), then had the user run it on the real rig (AMD Radeon/DisplayPort) — result: GO. Assumption A1 (repositioning avoids PathChangeException) and Assumption A2 (removed monitor stays discoverable via GetAllPaths) are both empirically confirmed. Plan 03 will implement `WindowsMonitorController.Disable`/`Restore` using Pattern 1 as documented.
- Reshaped MonitorState from a single device-path string into a full-topology, JSON-durable record (`MonitorPathSnapshot[]` + `TargetDevicePath`) and made `IMonitorController.CaptureState()` parameterless, with a real WindowsMonitorController implementation capturing every active CCD path.
- Real repositioning-aware CCD monitor disable/restore, debugged live against the user's rig through 5 iterations after the sandbox-only implementation shipped with real bugs no Linux build could catch.
- Named "don't ask again" confirmation dialog gating the primary-monitor disable, wired through MainForm/Program.cs composition root, with a durable skip flag that auto-resets when the configured monitor changes in Settings (DISPLAY-03, D-01, D-02).
- ToggleService.ToggleToRigMode/ToggleToNormalMode now return a ToggleResult (Monitor/Audio/App per-step outcomes) instead of throwing a single generic exception, with stop-on-first-failure for rig mode and isolate-and-continue preserved for normal mode.
- MainForm.BtnToggle_Click now captures the ToggleResult from both toggle directions and renders a per-step OK/FAILED(reason)/not-attempted checklist MessageBox only on partial failure, confirmed against a real green build/test/toggle round trip on the Windows rig.
- Standalone self-contained win-x64 .exe (PACKAGING-01) ships correctly, and a latent Phase 4 crash-recovery monitor-restore bug — never before exercised — is fixed and rig-verified.

---
