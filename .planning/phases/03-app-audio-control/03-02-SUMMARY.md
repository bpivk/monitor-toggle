---
phase: 03-app-audio-control
plan: 02
subsystem: audio
tags: [com-interop, ipolicyconfig, naudio, windows-audio, win32]

# Dependency graph
requires:
  - phase: 03-app-audio-control (Plan 01)
    provides: per-role AudioState/AudioRoleState model and per-role CaptureState
provides:
  - Verified 12-method IPolicyConfig COM interop (RigToggle.Windows.Audio)
  - Real SetDefault switching all three Windows audio roles to a configured device
  - Real Restore replaying each captured per-role device (ID or friendly-name fallback)
  - Shared verify-and-throw mutation path (ApplyAndVerify) reused by both
affects: [phase-05-orchestration-packaging]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Hand-embedded undocumented COM interop (IPolicyConfig) isolated in RigToggle.Windows.Audio, never a NuGet dependency"
    - "Fresh COM object per mutation cycle + Marshal.ReleaseComObject in finally (never cached across calls)"
    - "Write-then-verify-and-throw (mutate via COM, re-read via NAudio, throw InvalidOperationException on mismatch) instead of trusting HRESULT alone"

key-files:
  created:
    - src/RigToggle.Windows/Audio/IPolicyConfig.cs
  modified:
    - src/RigToggle.Windows/WindowsAudioController.cs

key-decisions:
  - "Reproduced the RESEARCH-verified 12-method IPolicyConfig vtable exactly (ResetDeviceFormat present at slot 3) rather than trusting an unchecked copy, per the confirmed vtable-offset bug in a circulating community copy"
  - "SetDefault and Restore share one ApplyAndVerify helper (fresh PolicyConfigClient per role, ReleaseComObject in finally, NAudio read-back verify-and-throw) so the D-03/D-04 verification logic exists once, not duplicated"
  - "Restore resolves each role's saved DeviceId first, falling back to a live friendly-name match via GetPlaybackDevices() only when DeviceId is missing; a role with neither is skipped rather than aborting the other two"

patterns-established:
  - "Pattern: any future undocumented-COM-API integration in this codebase should follow the same isolated-namespace + verify-and-throw shape established here"

requirements-completed: [AUDIO-01, AUDIO-02]

# Metrics
duration: 4min
completed: 2026-07-24
---

# Phase 3 Plan 2: Real Windows Default-Audio-Device Switching Summary

**Real `IPolicyConfig` COM interop switches and restores the Windows default playback device across all three audio roles (console/multimedia/communications), with a NAudio read-back verify-and-throw safety net replacing the Phase 2 no-op stubs.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-07-24T16:59:00Z (approx)
- **Completed:** 2026-07-24T17:01:09Z
- **Tasks:** 3 completed
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments
- Created the verified, cross-checked 12-method `IPolicyConfig` COM interop (`ResetDeviceFormat` present, `SetDefaultEndpoint` at slot 11) with both correct GUIDs
- `SetDefault` now sets the default render device for all three Windows audio roles and throws `InvalidOperationException` if any role's NAudio read-back doesn't match what was requested
- `Restore` replays each role's captured snapshot (ID-first, friendly-name fallback) through the same verify-and-throw path as `SetDefault`

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the verified IPolicyConfig COM interop** - `c753751` (feat)
2. **Task 2: Implement SetDefault — all three roles with verify-and-throw** - `5eb9e06` (feat)
3. **Task 3: Implement Restore — per-role exact restore with friendly-name fallback** - `b25ab62` (feat)

_Note: no TDD tasks in this plan; each commit is a single feat commit._

## Files Created/Modified
- `src/RigToggle.Windows/Audio/IPolicyConfig.cs` - New: hand-embedded 12-method `IPolicyConfig` interface, `ERole` enum, `PolicyConfigClient` COM-import class, verified against 3 independent cross-checked sources
- `src/RigToggle.Windows/WindowsAudioController.cs` - `SetDefault`/`Restore` replaced with real implementations; new `Roles` pairing array and shared `ApplyAndVerify` helper

## Decisions Made
- Reproduced the RESEARCH's verified vtable layout exactly rather than sourcing a fresh copy, since the research explicitly flagged a confirmed transcription bug (`ResetDeviceFormat` omission) in a commonly-circulated alternative
- Refactored the per-role set-and-verify logic into one shared `ApplyAndVerify` static helper called by both `SetDefault` and `Restore`, per the plan's Task 3 acceptance criteria, instead of duplicating the sequence
- Restore's friendly-name fallback reuses the existing `GetPlaybackDevices()` method rather than adding a new lookup path, keeping device enumeration in one place

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. Build/test verification (`dotnet build`) could not be run in this Linux sandbox per the plan's own noted Environment Availability constraint (no `dotnet` CLI, no Windows runtime) — verification here was done via targeted `grep` checks against every acceptance criterion (12 `[PreserveSig] int` methods, `ResetDeviceFormat` before `SetDeviceFormat`, both GUIDs present, no leftover "FAKE"/"no-op" language in `SetDefault`/`Restore`, `Marshal.ReleaseComObject` inside a `finally`, shared `ApplyAndVerify` helper called by both methods, friendly-name fallback present). Actual `dotnet build` must be run on the Windows dev/rig machine before this code is considered fully verified, consistent with how prior phases (Phase 1 spike, WindowsMonitorController) were validated.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- AUDIO-01/AUDIO-02 are functionally complete pending on-hardware `dotnet build`/manual toggle verification (Phase 5/full-toggle concern per this plan's own verification scope)
- No new NuGet packages were added; `RigToggle.Windows.csproj` unchanged
- `WindowsAudioController` is now fully real (enumeration, capture, set, restore) — only `WindowsMonitorController.Disable`/`Restore` remain stubbed, scoped to Phase 4

---
*Phase: 03-app-audio-control*
*Completed: 2026-07-24*
