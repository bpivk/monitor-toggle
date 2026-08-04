# Milestones

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
