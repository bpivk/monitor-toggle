---
phase: 18-cleanup-pass-exe-size-reduction
plan: 01
subsystem: core-persistence, audio-control, tests
tags: [cleanup, dead-code-removal, snapshot-persistence, audio-restore]
dependency-graph:
  requires: []
  provides:
    - "snapshot-free mode-seeding bootstrap (Program.cs)"
    - "forward-only IAudioController contract (no Restore member)"
  affects:
    - src/RigToggle.App/Program.cs
    - src/RigToggle.Core/Abstractions/IAudioController.cs
    - src/RigToggle.Windows/WindowsAudioController.cs
tech-stack:
  added: []
  patterns:
    - "Bare File.Exists() check replacing a deserializing store for legacy one-time bootstrap seeding (reduces attack/parsing surface, T-18-01-01)"
key-files:
  created: []
  modified:
    - src/RigToggle.App/Program.cs
    - src/RigToggle.Core/Abstractions/IModeStore.cs
    - src/RigToggle.Core/Persistence/JsonModeStore.cs
    - src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs
    - src/RigToggle.Core/Abstractions/IAudioController.cs
    - src/RigToggle.Windows/WindowsAudioController.cs
    - src/RigToggle.Tests/Doubles/InMemoryStores.cs
    - src/RigToggle.Tests/JsonStoreTests.cs
  deleted:
    - src/RigToggle.Core/Abstractions/ISnapshotStore.cs
    - src/RigToggle.Core/Models/StateSnapshot.cs
    - src/RigToggle.Core/Persistence/JsonSnapshotStore.cs
decisions: []
metrics:
  duration_min: 25
  completed: 2026-08-09
---

# Phase 18 Plan 01: Delete Dead Snapshot-Persistence Subsystem & Audio Restore Path Summary

Deleted the entire orphaned snapshot-persistence subsystem (`ISnapshotStore`, `StateSnapshot`, `JsonSnapshotStore`, `InMemorySnapshotStore`, 5 persistence tests) and the dead `IAudioController.Restore`/`WindowsAudioController.Restore` path, both orphaned when Phase 16 moved Normal-mode monitor/audio handling to explicit configured-target application instead of snapshot-restore.

## What Was Built

**Task 1 — Snapshot-persistence trio deletion + Program.cs rewire:**
- Confirmed via `grep -rn "ISnapshotStore\|JsonSnapshotStore\|StateSnapshot" src --include="*.cs"` that the only production construction site was `Program.cs:93` (`new JsonSnapshotStore(...)`); all other hits were the three files targeted for deletion or comment-only mentions.
- Deleted `src/RigToggle.Core/Abstractions/ISnapshotStore.cs`, `src/RigToggle.Core/Models/StateSnapshot.cs`, `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs`.
- Replaced the `JsonSnapshotStore` construction in `Program.cs` with `string legacyStateJsonPath = Path.Combine(basePath, "state.json");` and changed the mode-seeding ternary to `File.Exists(legacyStateJsonPath) ? ToggleMode.Rig : ToggleMode.Normal` — identical semantics, strictly less parsing surface (T-18-01-01).
- Reworded three surviving doc comments (`IModeStore.cs`, `JsonModeStore.cs`, `JsonToggleInProgressStore.cs`) that named the deleted types by name.

**Task 2 — Dead audio Restore path removal:**
- Deleted `void Restore(AudioState previousState);` from `IAudioController` and rewrote its stale XML doc summary to describe the forward-only contract (`GetPlaybackDevices`, `CaptureState`, `SetDefault`, `TryResolveDevice`).
- Deleted the entire `public void Restore(AudioState previousState)` method from `WindowsAudioController.cs` (51 lines) along with its leading multi-line comment and the stale-ID-to-friendly-name fallback logic that existed only to serve it.
- Kept `TryResolveDevice`, `GetPlaybackDevices`, `CaptureState`, `SetDefault`, `SetDefaultForAllRoles`, `ApplyAndVerify`, the `Roles` array untouched.
- Reworded three comments (class doc summary, `Roles` field comment, `ApplyAndVerify` leading comment) that named `Restore`.
- File shrank from 237 to 173 lines.

**Task 3 — Snapshot test double + 5 persistence tests removal:**
- Deleted `InMemorySnapshotStore` from `InMemoryStores.cs` and reworded the two doc comments in `InMemoryModeStore`/`InMemoryToggleInProgressStore` that cross-referenced it by name.
- Deleted the five `SnapshotStore_*` `[Fact]` methods from `JsonStoreTests.cs` (`SnapshotStore_Exists_IsFalseBeforeSave_TrueAfterSave`, `SnapshotStore_Clear_DeletesFile_SoExistsReturnsFalseAgain`, `SnapshotStore_Load_ReturnsNullWhenAbsent_AndSavedSnapshotWhenPresent`, `SnapshotStore_Load_OnMalformedFile_ReturnsNullInsteadOfThrowing`, `SnapshotStore_MonitorState_RoundTripsAllPathFields`) and updated the file header comment to name only `JsonSettingsStore`.
- `using RigToggle.Core.Models;` was retained in both files — still needed for `AppSettings`/`ToggleMode`/`ToggleInProgressMarker`.

## Verbatim Verification Output

**Confirm-dead-before-delete gate (before any deletion):**
```
$ grep -rn "ISnapshotStore\|JsonSnapshotStore\|StateSnapshot" src --include="*.cs"
```
Returned 24 lines, all within `IModeStore.cs` (comment), `JsonModeStore.cs` (comment x2), `JsonStoreTests.cs` (comment + 8 usages inside the 5 SnapshotStore_ tests), `JsonSnapshotStore.cs` (the class itself), `Program.cs:93` (the sole production construction site), `ISnapshotStore.cs` (the interface itself), `InMemoryStores.cs` (the double), `StateSnapshot.cs` (the record itself), `JsonToggleInProgressStore.cs` (comment x2). No unexpected production call site — proceeded with deletion.

**Final full-solution build:**
```
$ PATH="$HOME/.dotnet:$PATH" dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
Build succeeded.
    4 Warning(s)   <- the 4 pre-existing xUnit1031 warnings in ToggleOrchestratorTests.cs (documented baseline)
    0 Error(s)
```

**Final test run:**
```
$ PATH="$HOME/.dotnet:$PATH" dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true
Passed!  - Failed: 0, Passed: 80, Skipped: 0, Total: 80, Duration: 74 ms - RigToggle.Tests.dll (net10.0)
```
Matches the plan's exact target (85 baseline minus 5 removed snapshot persistence tests).

**RigToggle.Windows.Tests build (not executed — dev-host limitation, per plan's environment_note):**
```
$ PATH="$HOME/.dotnet:$PATH" dotnet build src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj -p:EnableWindowsTargeting=true
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**Final grep verification (all expected empty/zero):**
```
$ grep -rn "ISnapshotStore\|JsonSnapshotStore\|StateSnapshot\|InMemorySnapshotStore" src --include="*.cs"
(no output, exit 1)

$ grep -c "Restore" src/RigToggle.Core/Abstractions/IAudioController.cs src/RigToggle.Windows/WindowsAudioController.cs
src/RigToggle.Core/Abstractions/IAudioController.cs:0
src/RigToggle.Windows/WindowsAudioController.cs:0
```

## Deviations from Plan

None — plan executed exactly as written. One acceptance-criteria grep in Task 2 (`grep -n "public AudioDeviceInfo? TryResolveDevice" src/RigToggle.Core/Abstractions/IAudioController.cs ...`) does not match because C# interface members carry no `public` access modifier — this is the interface's pre-existing declaration style (line 24, `AudioDeviceInfo? TryResolveDevice(string? deviceId);`), which the plan explicitly instructed not to touch ("Do not touch the `TryResolveDevice` declaration or its own XML doc block"). Not a code defect; the plan's own grep pattern was imprecise for interface syntax. `WindowsAudioController.cs`'s concrete implementation does carry `public` and matches the pattern as written.

## Self-Check: PASSED

- CONFIRMED GONE: src/RigToggle.Core/Abstractions/ISnapshotStore.cs
- CONFIRMED GONE: src/RigToggle.Core/Models/StateSnapshot.cs
- CONFIRMED GONE: src/RigToggle.Core/Persistence/JsonSnapshotStore.cs
- FOUND: .planning/phases/18-cleanup-pass-exe-size-reduction/18-01-SUMMARY.md
- FOUND commit: 61b88d5 (Task 1)
- FOUND commit: 3a93bf8 (Task 2)
- FOUND commit: 4e76f15 (Task 3)
- FOUND commit: 0a77806 (docs: summary)
