# Milestones

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
