---
phase: 02-foundations-gui-shell
plan: 05
subsystem: ui
tags: [winforms, composition-root, toggle-service, settings-persistence, dotnet10]

# Dependency graph
requires:
  - phase: 02-foundations-gui-shell (02-02)
    provides: ToggleService, ISettingsStore/ISnapshotStore, JsonSettingsStore/JsonSnapshotStore
  - phase: 02-foundations-gui-shell (02-03)
    provides: WindowsMonitorController, WindowsAudioController, WindowsAppController
  - phase: 02-foundations-gui-shell (02-04)
    provides: SettingsForm(IMonitorController, IAudioController, ISettingsStore)
provides:
  - Runnable Phase 2 app end-to-end (real enumeration + fake mutation)
  - MainForm: mode indicator, Toggle button, Settings launch, live companion status line
  - Program.cs composition root wiring all real adapters + Json stores + ToggleService
  - Confirmed on real hardware: settings persistence across true app-restart (SETTINGS-04),
    snapshot-presence mode derivation across a crash-in-Rig-mode restart (D-14), stale-device
    reselect warning without crash (D-10)
affects: [03-monitor-audio-control, 04-app-control-elevation, 05-orchestration-packaging]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Composition root (Program.cs) is the ONLY place concrete adapters/stores are constructed; forms receive interfaces + factory delegates via constructor injection (Anti-Pattern 2 compliance)"
    - "Mode is derived from ToggleService.IsInRigMode() (snapshot-file presence), never from an in-memory flag, so it self-heals correctly on restart even after a crash mid-Rig-mode (D-14)"
    - "Settings dialog launched via injected Func<SettingsForm> factory rather than MainForm constructing SettingsForm itself, keeping MainForm free of adapter wiring"

key-files:
  created: []
  modified:
    - src/RigToggle.App/MainForm.Designer.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/Program.cs

key-decisions:
  - "Basic try/catch failure message box on Toggle click (T-02-FAKEFAIL) — full per-step CORE-04 partial-failure reporting explicitly deferred to Phase 5"
  - "No elevation manifest added; asInvoker preserved so Phase 3's cross-process companion-app window focus isn't broken by UIPI (Pitfall 6 / T-02-ELEV)"

patterns-established:
  - "SettingsForm factory delegate pattern for modal dialog launch from MainForm without adapter coupling"

requirements-completed: [SETTINGS-04]

# Metrics
duration: ~35min (composition + prior-session tasks) + human-verify walkthrough
completed: 2026-07-24
---

# Phase 2 Plan 5: Main Window + Composition Root Summary

**Wired the complete Phase 2 app end-to-end — real display/audio/process enumeration feeding a fixed-size Main window with snapshot-derived mode indicator, live companion status, and a modal Settings dialog — and had it confirmed working on the actual Windows rig, including a true app-restart persistence check.**

## Performance

- **Tasks:** 4/4 complete (3 auto tasks + 1 human-verify checkpoint, approved)
- **Files modified:** 3 (`MainForm.Designer.cs`, `MainForm.cs`, `Program.cs`; also deleted the WinForms template's `Form1.cs`/`Form1.Designer.cs`/`Form1.resx`)

## Accomplishments

- Built the fixed-size Main window (`FormBorderStyle.FixedDialog`, non-maximizable, 320×200, `CenterScreen`, system-default styling — no custom Font/BackColor/ForeColor) with a vertical stack of mode label, Toggle button (height 40), Settings… button (height 32), and companion-status label.
- Implemented `MainForm` code-behind: `RefreshUi()` derives mode strictly from `ToggleService.IsInRigMode()` (snapshot-file presence, D-14) and companion status strictly from `IAppController.IsRunning(...)` against the persisted settings path — zero adapter construction in the form itself.
- Implemented the `Program.cs` composition root: constructs `JsonSettingsStore`/`JsonSnapshotStore` under `%LocalAppData%\RigToggle\{settings,state}.json`, the three real `Windows*Controller` adapters, and `ToggleService`, then injects all of it (plus a `SettingsForm` factory delegate) into `MainForm`. No elevation manifest added.
- Removed the WinForms template's placeholder `Form1.cs`/`Form1.Designer.cs`/`Form1.resx`.
- User completed the full Phase 2 acceptance walkthrough on the Windows rig (Task 4 checkpoint) and confirmed every step passes — see "Checkpoint Outcome" below.

## Task Commits

Each task was committed atomically:

1. **Task 1: MainForm.Designer.cs — fixed-size Main window layout** - `2c09c72` (feat)
2. **Task 2: MainForm.cs — mode derivation, toggle wiring, status line, Settings launch** - `6060057` (feat)
3. **Task 3: Program.cs composition root + remove template Form1** - `9a9c239` (feat, Form1 removal + partial staging) + `870fe14` (feat, correction — see Deviations)
4. **Task 4: Human-verify checkpoint — full Phase 2 GUI + persistence on the rig** - no commit (gate-only task); **approved** by the user after a full walkthrough on real hardware

**Plan metadata:** this commit (docs: complete plan)

## Checkpoint Outcome (Task 4)

The user ran the built app on the Windows rig and confirmed, with no issues found:

- `dotnet build RigToggle.sln` succeeds across all 4 projects.
- Main window opens fixed-size/centered with `Text = "Rig Toggle"` (not the template's default "Form1"), showing the mode indicator, Toggle button, Settings… button, and companion status line.
- Settings dialog opens with real-enumerated monitor and audio-device pickers; Save-gating requires all selections (including browsed .exe) before enabling; Save persists to `settings.json`.
- Toggle button correctly flips "Mode: Normal" ↔ "Mode: Rig", relabels itself ("Switch to Rig Mode" ↔ "Switch to Normal Mode"), and creates/deletes `%LocalAppData%\RigToggle\state.json` on each toggle.
- Restarting the app while in Rig mode correctly shows "Mode: Rig" on startup — confirms D-14 snapshot-presence mode derivation survives a process restart, including the crash-in-Rig-mode recovery case.
- **True full-process-restart persistence confirmed (SETTINGS-04):** fully closing and relaunching the app, then reopening Settings, shows all three saved selections (monitor, both audio devices, companion .exe path) still preselected with no stale warnings — not merely a same-session dialog reopen.
- **Stale-device handling confirmed (D-10):** editing a saved device ID in `settings.json` to a garbage value and reopening Settings shows that picker unselected with a "not found — please reselect" warning, with no crash.
- No real monitor/audio/app-launch mutation occurred at any point (fakes confirmed) — consistent with Phase 2 scope (real enumeration, fake mutation; real mutation lands in Phases 3/4).

All four ROADMAP Phase 2 success criteria and SETTINGS-01/02/03/04 are confirmed working end-to-end on real hardware. This closes the Phase 2 acceptance gate.

## Files Created/Modified

- `src/RigToggle.App/MainForm.Designer.cs` - Fixed-size (320×200) vertical-stack layout: `lblMode`, `btnToggle` (h=40), `btnSettings` (h=32), `lblCompanionStatus`; `FixedDialog`/`MaximizeBox=false`/`MinimizeBox=true`/`CenterScreen`; no custom Font/BackColor/ForeColor.
- `src/RigToggle.App/MainForm.cs` - `RefreshUi()` (mode + companion status derivation), `BtnToggle_Click` (try/catch-guarded `ToggleService` invocation + basic failure message box), `BtnSettings_Click` (modal `SettingsForm` launch via injected factory, `RefreshUi()` on return).
- `src/RigToggle.App/Program.cs` - `[STAThread] Main`: composition root constructing `JsonSettingsStore`, `JsonSnapshotStore`, `WindowsMonitorController`, `WindowsAudioController`, `WindowsAppController`, `ToggleService`, and a `SettingsForm` factory delegate, all injected into `MainForm`; no elevation manifest.
- `src/RigToggle.App/Form1.cs`, `Form1.Designer.cs`, `Form1.resx` - deleted (WinForms template placeholder, superseded by `MainForm`).

## Decisions Made

- Basic try/catch failure-box handling on Toggle click is sufficient for Phase 2 (T-02-FAKEFAIL mitigation); full per-step partial-failure reporting (CORE-04) is explicitly out of scope until Phase 5, per plan.
- No application manifest / `requestedExecutionLevel` added anywhere in the project — default asInvoker execution level preserved so Phase 3's cross-process `SetForegroundWindow` against the non-elevated companion app is not broken by UIPI (T-02-ELEV mitigation, Pitfall 6).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking issue] Program.cs composition-root content missed initial staging**
- **Found during:** Task 3 (Program.cs composition root + remove template Form1)
- **Issue:** The Write-tool edit to `Program.cs` landed in the working tree, but `git add` ran before the file write had fully settled on disk, so commit `9a9c239` only captured the Form1 file deletions (staged correctly) while the actual `Main()` composition-root body was not yet included in that commit's diff.
- **Fix:** Caught immediately by reviewing the commit diff before moving on; re-staged `Program.cs` and made a follow-up commit (`870fe14`) capturing the full `Main()` wiring body.
- **Files modified:** `src/RigToggle.App/Program.cs`
- **Verification:** `git show 870fe14 --stat` confirms the composition-root content (39 lines) is present; `git show 9a9c239 --stat` confirms only the Form1 deletions were in the first commit — no content was lost or duplicated across the two commits.
- **Committed in:** `870fe14`

## Known Stubs

None — this plan wires real enumeration adapters end-to-end. Monitor-disable, audio-switch, and app-launch mutation remain intentional fakes/no-ops by Phase 2 design (real mutation is Phase 3/4 scope), and this is already documented in the plan's `<what-built>` and accepted by the human-verify checkpoint; not a stub requiring future rework beyond the already-scheduled Phase 3/4 work.

## Self-Check: PASSED

- FOUND: src/RigToggle.App/MainForm.Designer.cs
- FOUND: src/RigToggle.App/MainForm.cs
- FOUND: src/RigToggle.App/Program.cs
- MISSING (expected, intentional deletion): src/RigToggle.App/Form1.cs
- MISSING (expected, intentional deletion): src/RigToggle.App/Form1.Designer.cs
- FOUND: commit 2c09c72
- FOUND: commit 6060057
- FOUND: commit 9a9c239
- FOUND: commit 870fe14
