---
phase: 05-orchestration-full-toggle-packaging
fixed_at: 2026-07-25T19:48:44Z
review_path: .planning/phases/05-orchestration-full-toggle-packaging/05-REVIEW.md
iteration: 1
findings_in_scope: 8
fixed: 8
skipped: 0
status: all_fixed
---

# Phase 05: Code Review Fix Report

**Fixed at:** 2026-07-25T19:48:44Z
**Source review:** .planning/phases/05-orchestration-full-toggle-packaging/05-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 8 (1 Critical, 4 Warning, 3 Info — `fix_scope=all`)
- Fixed: 8
- Skipped: 0

## Fixed Issues

### CR-01: Snapshot is persisted (flipping IsInRigMode to true) even when Monitor.Disable() fails before mutating anything

**Files modified:** `src/RigToggle.Core/ToggleService.cs`, `src/RigToggle.Tests/Doubles/FakeControllers.cs`, `src/RigToggle.Tests/ToggleServiceTests.cs`
**Commit:** `264781a` (already committed prior to this fix run — verified present on the branch history, not re-applied)
**Applied fix:** Already resolved before this run started. `ToggleToRigMode` now re-captures monitor state after a failed `Disable()` and compares it against the state captured before `Disable()` ran (via a new `MonitorStateUnchanged` helper using `SequenceEqual`, since `MonitorState.Paths` is an `IReadOnlyList<T>` and falls back to reference equality under record-generated `==`). The snapshot is cleared only if nothing changed; if re-capture throws or the states differ, the snapshot is kept for retry/manual recovery. Verified by reading `src/RigToggle.Core/ToggleService.cs:90-114` directly against the finding — the described bug is no longer present in the code.

## Warnings — Fixed

### WR-01: ToggleToNormalMode() called while not actually in rig mode skips Monitor/Audio steps but still runs App-minimize and Clear()

**Files modified:** `src/RigToggle.Core/ToggleService.cs`, `src/RigToggle.Tests/ToggleServiceTests.cs`
**Commit:** `f49e5ba`
**Applied fix:** Added an explicit early return (`return new ToggleResult(new List<ToggleStepResult>());`) inside the `snapshot is null` branch when `wasInRigMode` is `false`, before falling through to the companion-app minimize block and `_snapshotStore.Clear()`. Added a regression test (`ToggleToNormalMode_IsNoOp_WhenNeverInRigMode`) asserting an empty `Steps` list and that neither `app.MinimizeIfRunning` nor `snapshot.Clear` are called.

### WR-02: Dead code — CopyOutputTechnology/AssignSource are no longer called by production logic

**Files modified:** `src/RigToggle.Windows/WindowsMonitorController.cs`
**Commit:** `686ad5c`
**Applied fix:** Chose the doc-comment-correction option from the review's two alternatives (over deleting the methods and their tests) — these are a documented known-good fallback from the earlier manual-reconstruction `Restore()` approach, and deleting them would discard already-solved, rig-hard-won knowledge (three separate CCD validation failures were worked through to write them) for no functional gain. Rewrote both methods' doc comments to explicitly state they are NOT currently called by `Disable()` or `Restore()` in this file, and are kept intentionally as a fallback rather than claiming (as before) that they are exercised by current production logic. No behavior change — comment-only.

### WR-03: ApplyTopology(Extend) call has no diagnostic wrapper, unlike the sibling ApplyPathInfos call a few lines later

**Files modified:** `src/RigToggle.Windows/WindowsMonitorController.cs`
**Commit:** `8dfa5da`
**Applied fix:** Wrapped the `PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false)` call in `Restore()`'s crash-recovery fallback path in a `try`/`catch` that rethrows as `InvalidOperationException` with added diagnostic context, matching the existing pattern used for the sibling `ApplyPathInfos` call ~40 lines later in the same method.

### WR-04: ToggleService constructor does not null-check its injected dependencies

**Files modified:** `src/RigToggle.Core/ToggleService.cs`, `src/RigToggle.Tests/ToggleServiceTests.cs`
**Commit:** `0a407c1`
**Applied fix:** Added `?? throw new ArgumentNullException(nameof(...))` guards for all five constructor parameters (`settingsStore`, `snapshotStore`, `monitorController`, `audioController`, `appController`), matching the existing composition-root convention already established in `MainForm`'s constructor. Added a regression test (`Constructor_Throws_WhenAnyDependencyIsNull`) covering all five parameters.

## Info — Fixed

### IN-01: Redundant condition in Disable()'s primary-repositioning branch

**Files modified:** `src/RigToggle.Windows/WindowsMonitorController.cs`
**Commit:** `23295cc`
**Applied fix:** Dropped the always-true `survivors.Length > 0` clause from `if (targetPath.IsGDIPrimary && survivors.Length > 0)`, leaving `if (targetPath.IsGDIPrimary)`, with a comment noting why the clause was redundant (the `survivors.Length == 0` case already throws earlier in the method).

### IN-02: Inconsistent failure tracing — only audio-restore failures are written to Trace

**Files modified:** `src/RigToggle.Core/ToggleService.cs`
**Commit:** `9d27516`
**Applied fix:** Chose the "trace all step failures consistently" option from the review's two alternatives (over removing the one existing trace call), since a Trace breadcrumb genuinely adds forensic value for every step failure, not just audio's. Added `System.Diagnostics.Trace.WriteLine(...)` calls to `TryExecuteStep`'s catch block (covers all `ToggleToRigMode` steps) and to the monitor-restore catch block in `ToggleToNormalMode`, matching the existing audio-restore trace call's format and rationale.

### IN-03: Unmanaged temp file in test fixture

**Files modified:** `src/RigToggle.Tests/ToggleServiceTests.cs`
**Commit:** `1e20f6b`
**Applied fix:** Converted `ExistingCompanionAppPath` and `ConfiguredSettings` from `static readonly` fields (one temp file per test-assembly run, never deleted) to instance fields initialized in the constructor, implemented `IDisposable` on the test class, and delete the temp file in `Dispose()`. Note: a naive per-instance `Dispose()` deleting a *shared static* field would have broken other tests running after the first one completed (the file would already be gone) — converting the fields to per-instance avoids that cross-test interference entirely, since xunit constructs and disposes a fresh test-class instance per test method.

---

_Fixed: 2026-07-25T19:48:44Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
