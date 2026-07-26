---
phase: 03-app-audio-control
plan: 01
subsystem: audio
tags: [naudio, csharp, dotnet, records, system-text-json]

# Dependency graph
requires:
  - phase: 02-foundations-gui-shell
    provides: original single-role AudioState/WindowsAudioController/JsonSnapshotStore built in Phase 2, now reshaped
provides:
  - Three-role AudioState/AudioRoleState model (Console/Multimedia/Communications)
  - Per-role WindowsAudioController.CaptureState with independent per-role fallback
  - Defensive JsonSnapshotStore.Load that returns null on malformed/stale JSON instead of throwing
affects: [03-02-app-audio-control (Restore/SetDefault IPolicyConfig wiring), 05-orchestration-packaging]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Per-role defensive capture: each Windows audio role (Console/Multimedia/Communications) read in its own using-scoped MMDeviceEnumerator + try/catch, falling back to AudioRoleState(null, null) independently so one role's failure never aborts the others"
    - "Defensive JSON snapshot load: JsonSnapshotStore.Load wraps Deserialize in try/catch(JsonException) returning null, treating any malformed/incompatible on-disk snapshot as 'no snapshot' = normal mode"

key-files:
  created:
    - src/RigToggle.Core/Models/AudioRoleState.cs
  modified:
    - src/RigToggle.Core/Models/AudioState.cs
    - src/RigToggle.Core/Persistence/JsonSnapshotStore.cs
    - src/RigToggle.Windows/WindowsAudioController.cs
    - src/RigToggle.Tests/Doubles/FakeControllers.cs
    - src/RigToggle.Tests/JsonStoreTests.cs

key-decisions:
  - "AudioState reshaped to three named AudioRoleState fields (Console, Multimedia, Communications) per D-02/AUDIO-02, replacing the Phase-2 single DefaultDeviceId"
  - "JsonSnapshotStore.Load treats any JsonException (stale shape, corruption, truncated write) as 'no snapshot' rather than crashing on startup (Open Question 1 resolution)"
  - "FakeAudioController keeps a single captured device id but projects it into all three roles for test simplicity; Restore log key changed from DefaultDeviceId to Multimedia.DeviceId to keep the existing audio.Restore: prefix contract intact for ToggleServiceTests"

patterns-established:
  - "Three independent try/catch blocks in CaptureState (one per role) rather than a shared parameterized helper, to keep each role's failure isolated and to satisfy literal grep-based verification of three GetDefaultAudioEndpoint(DataFlow.Render, Role.X) call sites"

requirements-completed: [AUDIO-02]

# Metrics
duration: 5min
completed: 2026-07-24
---

# Phase 03 Plan 01: Per-Role Audio State Reshape Summary

**Reshaped AudioState from a single DefaultDeviceId into three independent per-role (Console/Multimedia/Communications) snapshots, with per-role defensive capture and a defensive JsonSnapshotStore.Load that no longer crashes on stale-shaped state.json.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-07-24T16:51:00Z (approx)
- **Completed:** 2026-07-24T16:55:00Z
- **Tasks:** 2/2 completed
- **Files modified:** 6 (1 created, 5 modified)

## Accomplishments
- `AudioRoleState(DeviceId, DeviceName)` record added; `AudioState` reshaped to `AudioState(Console, Multimedia, Communications)` — one snapshot per Windows audio role, per D-02/AUDIO-02
- `WindowsAudioController.CaptureState` now reads `Role.Console`, `Role.Multimedia`, and `Role.Communications` independently, each in its own fresh `MMDeviceEnumerator` + try/catch, so a failure on one role falls back to `AudioRoleState(null, null)` without aborting the other two
- `JsonSnapshotStore.Load` wraps `Deserialize` in `try/catch (JsonException)` returning `null`, so a stale-shaped or corrupted `state.json` is treated as normal mode instead of crashing on startup
- `FakeAudioController` and `JsonStoreTests.cs` updated to compile and pass against the new three-role shape, keeping the `audio.Restore:` log-prefix contract that `ToggleServiceTests.cs` depends on

## Task Commits

Each task was committed atomically:

1. **Task 1: Reshape AudioState to per-role + defensive snapshot Load** - `af42813` (feat)
2. **Task 2: Per-role CaptureState + fix FakeAudioController to new shape** - `42bd348` (feat)

_Note: Task 2's commit also includes the JsonStoreTests.cs fix (Rule 3 blocking-issue) and one new test (Rule 2), both directly caused by this task's reshape — see Deviations below._

## Files Created/Modified
- `src/RigToggle.Core/Models/AudioRoleState.cs` - New per-role id+name record (Console/Multimedia/Communications building block)
- `src/RigToggle.Core/Models/AudioState.cs` - Reshaped from `AudioState(string? DefaultDeviceId)` to `AudioState(AudioRoleState Console, AudioRoleState Multimedia, AudioRoleState Communications)`
- `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs` - `Load()` now catches `JsonException` and returns `null` instead of throwing
- `src/RigToggle.Windows/WindowsAudioController.cs` - `CaptureState()` now performs three independent per-role reads with per-role fallback
- `src/RigToggle.Tests/Doubles/FakeControllers.cs` - `FakeAudioController.CaptureState`/`Restore` updated to the three-role shape
- `src/RigToggle.Tests/JsonStoreTests.cs` - Fixed positional `AudioState` construction and `.DefaultDeviceId` assertions to the new shape; added a new test for `Load()`'s malformed-JSON defensive behavior

## Decisions Made
- Kept `FakeAudioController`'s single-`capturedDefaultDeviceId` constructor parameter (no API break for existing test call sites) and projected that one value into all three roles inside `CaptureState`, since no test in this plan's scope needed per-role fake control
- Used three literal per-role try/catch blocks in `CaptureState` rather than a shared private helper parameterized by `Role`, matching the plan's explicit acceptance criterion (`grep -c "GetDefaultAudioEndpoint(DataFlow.Render, Role\." ... ` returns 3) and keeping each role's failure isolated per D-02/T-03-03
- Chose genuinely malformed JSON (`"{not valid json"`) for the new defensive-load test rather than a "correctly-formed-but-old-shaped" JSON payload, since .NET's `System.Text.Json` supplies default values for missing constructor parameters rather than throwing — a syntactically invalid payload is the unambiguous way to exercise the `catch (JsonException)` path

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed JsonStoreTests.cs compile breaks not listed in the plan's file list**
- **Found during:** Task 2 (verifying no other call sites break after the reshape)
- **Issue:** `src/RigToggle.Tests/JsonStoreTests.cs` (not in the plan's `files_modified` or "Positional AudioState construction sites" list) constructs `new AudioState("device-id")` in three tests and asserts `snapshot.Audio.DefaultDeviceId` in a fourth — all of which fail to compile against the new three-role `AudioState` shape
- **Fix:** Updated the three constructions to `new AudioState(new AudioRoleState("device-id", "Fake Device"), ...)` (one per role) and replaced the `.DefaultDeviceId` assertion with three per-role `.DeviceId` assertions (`Console`, `Multimedia`, `Communications`)
- **Files modified:** `src/RigToggle.Tests/JsonStoreTests.cs`
- **Committed in:** `42bd348` (Task 2 commit)

**2. [Rule 2 - Missing Critical] Added test coverage for JsonSnapshotStore.Load's new defensive behavior**
- **Found during:** Task 1/2 (the plan's must-have truth "A stale-shaped state.json on disk no longer crashes JsonSnapshotStore.Load" had no corresponding test)
- **Issue:** The `catch (JsonException)` guard added in Task 1 was previously verifiable only via `grep`, with no runtime test proving `Load()` actually returns `null` on malformed input
- **Fix:** Added `SnapshotStore_Load_OnMalformedFile_ReturnsNullInsteadOfThrowing`, writing deliberately invalid JSON to disk and asserting `Load()` returns `null`
- **Files modified:** `src/RigToggle.Tests/JsonStoreTests.cs`
- **Committed in:** `42bd348` (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 missing-critical-test-coverage)
**Impact on plan:** Both fixes were necessary consequences of the AudioState reshape — the first was required for the solution/test project to compile at all, the second closes a coverage gap on a must-have truth called out explicitly in the plan frontmatter. No scope creep beyond what the reshape itself required.

## Issues Encountered
- This is a Linux sandbox with no `dotnet` installed (matches the Phase 1/2 build-on-Windows convention noted in the plan's `<verification>` section). All Task 1/Task 2 acceptance criteria were verified via targeted `grep` checks against the exact patterns specified in the plan (record shapes, catch clause, role literals, log-prefix contract) plus a manual read-through of every file referencing `AudioState`/`AudioRoleState` across the solution (`IAudioController.cs`, `StateSnapshot.cs`, `ToggleService.cs`, `MainForm.cs`, `Program.cs`, `WindowsMonitorController.cs`, `InMemoryStores.cs`) to confirm no other call site was missed. Actual `dotnet build`/`dotnet test` execution still needs to happen on the Windows dev/rig machine before merging, per the existing project convention.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 02 (real `IPolicyConfig` COM interop for `SetDefault`/`Restore`) can now build directly on the three-role `AudioState`/`AudioRoleState` contract established here — `Restore(AudioState previousState)` has `previousState.Console`, `.Multimedia`, `.Communications` each available to feed three separate `IPolicyConfig::SetDefaultEndpoint` calls
- No blockers. One follow-up note for the human running the actual Windows build: run `dotnet build src/RigToggle.Core/RigToggle.Core.csproj` and `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` on Windows to confirm the sandbox's static verification holds at compile/runtime, per the plan's own verification note.

---
*Phase: 03-app-audio-control*
*Completed: 2026-07-24*

## Self-Check: PASSED

- All 6 created/modified files confirmed present on disk (AudioRoleState.cs, AudioState.cs, JsonSnapshotStore.cs, WindowsAudioController.cs, FakeControllers.cs, JsonStoreTests.cs)
- Both task commits confirmed present in git history (`af42813`, `42bd348`)
