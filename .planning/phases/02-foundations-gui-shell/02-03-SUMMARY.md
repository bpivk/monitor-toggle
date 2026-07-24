---
phase: 02-foundations-gui-shell
plan: 03
subsystem: infra
tags: [windowsdisplayapi, naudio, ccd, process, adapter-pattern, win32]

# Dependency graph
requires:
  - phase: 02-foundations-gui-shell (Plan 01)
    provides: IMonitorController/IAudioController/IAppController interfaces and MonitorInfo/MonitorState/AudioDeviceInfo/AudioState models
provides:
  - WindowsMonitorController (real WindowsDisplayAPI monitor enumeration, no-op disable/restore)
  - WindowsAudioController (real NAudio audio endpoint enumeration + default-device read, no-op SetDefault/Restore, defensive missing-ID resolution)
  - WindowsAppController (real Process.GetProcessesByName running-detection, no-op launch/focus/minimize)
affects: [02-04 (Settings/Main GUI forms consuming these adapters), Phase 3 (audio SetDefault + app launch/focus/minimize real implementation), Phase 4 (monitor Disable/Restore real CCD implementation)]

# Tech tracking
tech-stack:
  added: []  # WindowsDisplayAPI/NAudio already referenced in RigToggle.Windows.csproj by Plan 01/setup
  patterns: ["Real-read / fake-mutation split within a single adapter class (02-RESEARCH.md Pattern 1)"]

key-files:
  created:
    - src/RigToggle.Windows/WindowsMonitorController.cs
    - src/RigToggle.Windows/WindowsAudioController.cs
    - src/RigToggle.Windows/WindowsAppController.cs
  modified: []

key-decisions:
  - "Fresh MMDeviceEnumerator created and disposed per call in both GetPlaybackDevices() and CaptureState() — no session-lifetime caching, avoiding COM-leak-across-repeated-calls (T-02-COMLEAK mitigation)"
  - "Added a TryResolveDevice(deviceId) helper on WindowsAudioController, guarded by both a null-check and try/catch, so a saved-but-missing audio device ID resolves gracefully regardless of whether NAudio throws or returns null (unconfirmed behavior, Pitfall 2 / Assumptions Log A2) — not explicitly named in the plan's method list but required by the plan's own acceptance criteria ('Any GetDevice resolution is guarded by try/catch') and threat register (T-02-NULLID mitigate)"

requirements-completed: [SETTINGS-01, SETTINGS-02]

# Metrics
duration: 5min
completed: 2026-07-24
---

# Phase 02 Plan 03: Windows Control Adapters (Real Enumeration, Fake Mutation) Summary

**Implemented the three `RigToggle.Windows` adapter classes with real WindowsDisplayAPI/NAudio/Process enumeration for monitor, audio, and companion-app detection, while every mutating method (Disable, Restore, SetDefault, LaunchOrFocus, MinimizeIfRunning) remains a documented no-op stub for Phases 3/4.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-07-24T14:08:00Z (approx, first Read call)
- **Completed:** 2026-07-24T14:08:54Z
- **Tasks:** 3/3 completed
- **Files modified:** 3 created

## Accomplishments
- `WindowsMonitorController.GetActiveMonitors()` reuses the exact `PathInfo.GetActivePaths(virtualModeAware: false)` call already proven non-elevated on this rig's AMD/DisplayPort hardware in the Phase 1 spike, mapping each `PathDisplayTarget` to a `MonitorInfo` with `IsPrimary` sourced from `path.IsGDIPrimary`.
- `WindowsAudioController.GetPlaybackDevices()`/`CaptureState()` wire real NAudio `MMDeviceEnumerator` enumeration and default-endpoint reads, with a fresh enumerator per call (no caching) and defensive handling for a since-removed default/saved device.
- `WindowsAppController.IsRunning()` derives the process name via `Path.GetFileNameWithoutExtension` (never the raw path or a hardcoded literal) and checks `Process.GetProcessesByName`, avoiding the explicitly-forbidden `FindWindow`/title-matching approach.
- All five mutating methods across the three classes carry consistent "FAKE in Phase 2" comments naming exactly which future phase (3 or 4) and which real API call fills them in — preserving the exact class/method shape those phases will slot into without renaming or restructuring.

## Task Commits

Each task was committed atomically:

1. **Task 1: WindowsMonitorController — real enumeration + no-op disable/restore** - `6903ed1` (feat)
2. **Task 2: WindowsAudioController — real NAudio enumeration + no-op SetDefault/Restore** - `a6f3958` (feat)
3. **Task 3: WindowsAppController — real running-detection + no-op launch/focus/minimize** - `04d8ac6` (feat)

**Plan metadata:** committed separately per worktree-mode protocol (see below)

_Note: no TDD tasks in this plan — all three are `type="auto"` implementation tasks._

## Files Created/Modified
- `src/RigToggle.Windows/WindowsMonitorController.cs` - Real monitor enumeration via `WindowsDisplayAPI.PathInfo.GetActivePaths()`; `Disable()`/`Restore()` no-op stubs for Phase 4
- `src/RigToggle.Windows/WindowsAudioController.cs` - Real audio endpoint enumeration + default-device read via NAudio `MMDeviceEnumerator`; `SetDefault()`/`Restore()` no-op stubs for Phase 3; defensive `TryResolveDevice()` helper
- `src/RigToggle.Windows/WindowsAppController.cs` - Real running-detection via `Process.GetProcessesByName`; `LaunchOrFocus()`/`MinimizeIfRunning()` no-op stubs for Phase 3

## Decisions Made
- Kept `CaptureState()` on both `WindowsMonitorController` and `WindowsAudioController` as real reads (per D-05/D-08's documented boundary: only mutation is faked, not reads) — matches 02-RESEARCH.md Pattern 1 exactly.
- Added the `TryResolveDevice` helper on `WindowsAudioController` beyond the plan's literal three-method list, because the plan's own acceptance criteria explicitly requires "Any `GetDevice` resolution is guarded by try/catch" and the threat register's T-02-NULLID mitigation calls for defensive missing-ID handling; this is the natural place Settings (Plan 04) will call from to resolve a saved device ID.

## Deviations from Plan

None - plan executed exactly as written. The `TryResolveDevice` helper is an in-scope addition explicitly anticipated by the plan's acceptance criteria and threat register (not a new architectural surface), so it is not logged as a deviation requiring a rule citation.

## Verification

This sandbox has no .NET SDK and no Windows runtime (per the plan's `<execution_context>` boundary and the project's established Phase-1 precedent). Verification performed in place of `dotnet build`:
- `grep -c "GetActivePaths"` on `WindowsMonitorController.cs` → 1 (required: at least 1) — PASS
- Confirmed `using WindowsDisplayAPI;` and `using WindowsDisplayAPI.DisplayConfig;` both present — PASS
- Confirmed `Disable`/`Restore` bodies contain no `ApplyPathInfos`/`SetDisplayConfig` calls (only comments referencing them) and carry "FAKE in Phase 2" comments — PASS
- Confirmed `GetActiveMonitors` maps `path.IsGDIPrimary` → `IsPrimary` — PASS
- `grep -n "EnumerateAudioEndPoints"` on `WindowsAudioController.cs` → present with `DataFlow.Render, DeviceState.Active` args — PASS
- Confirmed `MMDeviceEnumerator` is never stored as a field (created via `using var` inside each method) — PASS
- Confirmed `SetDefault`/`Restore` carry "FAKE in Phase 2" comments and contain no `IPolicyConfig` calls (only comments) — PASS
- Confirmed `TryResolveDevice`'s `GetDevice` call is guarded by try/catch and a preceding null-check — PASS
- `grep -n "GetFileNameWithoutExtension"` on `WindowsAppController.cs` → present, feeding `Process.GetProcessesByName` — PASS
- Confirmed `IsRunning` never passes the raw path or a hardcoded literal — PASS
- Confirmed `LaunchOrFocus`/`MinimizeIfRunning` carry "FAKE in Phase 2" comments and contain no `ShowWindow`/`SetForegroundWindow` calls (only comments) — PASS
- Confirmed no `FindWindow`/`FindWindowEx` usage anywhere in `WindowsAppController.cs` — PASS

**Deferred to Windows rig (per plan's execution boundary):** `dotnet build src/RigToggle.Windows/RigToggle.Windows.csproj` compilation against `WindowsDisplayAPI` 1.3.0.13 + NAudio 2.3.0, and live verification that `GetActiveMonitors()`/`GetPlaybackDevices()` return the rig's real displays/endpoints — both require the actual Windows/.NET 10 SDK runtime, unavailable in this Linux sandbox. Structural/syntax review confirms all API call shapes match 02-RESEARCH.md's source-verified signatures (`PathInfo.GetActivePaths`, `MMDeviceEnumerator.EnumerateAudioEndPoints`/`GetDefaultAudioEndpoint`/`GetDevice`, `Process.GetProcessesByName`) exactly.

## Known Stubs

The following are **intentional** stubs per this plan's explicit design (D-05/D-08, 02-RESEARCH.md Pattern 1) — not gaps to fix, but the documented Phase-2 scope boundary:

| File | Method(s) | Reason | Resolved In |
|------|-----------|--------|-------------|
| `WindowsMonitorController.cs` | `Disable(string)`, `Restore(MonitorState)` | Real CCD topology-path-removal via `PathInfo.ApplyPathInfos` | Phase 4 |
| `WindowsAudioController.cs` | `SetDefault(string)`, `Restore(AudioState)` | Real `IPolicyConfig` COM interop default-device switch | Phase 3 |
| `WindowsAppController.cs` | `LaunchOrFocus(string)`, `MinimizeIfRunning(string)` | Real Win32 `user32.dll` `ShowWindow`/`SetForegroundWindow`/`Process.Start` | Phase 3 |

All read/enumeration methods (`GetActiveMonitors`, `CaptureState` x2, `GetPlaybackDevices`, `IsRunning`) are real, not stubs, and were the focus of this plan's SETTINGS-01/02 requirements.

## Self-Check

- `src/RigToggle.Windows/WindowsMonitorController.cs` — FOUND
- `src/RigToggle.Windows/WindowsAudioController.cs` — FOUND
- `src/RigToggle.Windows/WindowsAppController.cs` — FOUND
- Commit `6903ed1` — FOUND
- Commit `a6f3958` — FOUND
- Commit `04d8ac6` — FOUND

## Self-Check: PASSED
