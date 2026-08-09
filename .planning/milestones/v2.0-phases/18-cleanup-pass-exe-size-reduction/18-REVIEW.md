---
phase: 18-cleanup-pass-exe-size-reduction
reviewed: 2026-08-09T00:00:00Z
depth: standard
files_reviewed: 22
files_reviewed_list:
  - src/RigToggle.App/Program.cs
  - src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml
  - src/RigToggle.App/RigToggle.App.csproj
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.Core/Abstractions/IAudioController.cs
  - src/RigToggle.Core/Abstractions/IModeStore.cs
  - src/RigToggle.Core/Abstractions/IMonitorController.cs
  - src/RigToggle.Core/Models/MonitorState.cs
  - src/RigToggle.Core/Persistence/JsonModeStore.cs
  - src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs
  - src/RigToggle.Core/ToggleResultFormatter.cs
  - src/RigToggle.Core/ToggleService.cs
  - src/RigToggle.Tests/Doubles/BlockingMonitorController.cs
  - src/RigToggle.Tests/Doubles/FakeControllers.cs
  - src/RigToggle.Tests/Doubles/InMemoryStores.cs
  - src/RigToggle.Tests/JsonStoreTests.cs
  - src/RigToggle.Tests/ToggleResultFormatterTests.cs
  - src/RigToggle.Tests/ToggleServiceTests.cs
  - src/RigToggle.Windows/AssemblyInfo.cs
  - src/RigToggle.Windows/RigToggle.Windows.csproj
  - src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs
  - src/RigToggle.Windows/WindowsAudioController.cs
  - src/RigToggle.Windows/WindowsMonitorController.cs
findings:
  critical: 0
  warning: 1
  info: 2
  total: 3
status: issues_found
---

# Phase 18: Code Review Report

**Reviewed:** 2026-08-09T00:00:00Z
**Depth:** standard
**Files Reviewed:** 22
**Status:** issues_found

## Summary

This phase is deletion-heavy (dead snapshot-restore subsystem, dead CCD reconstruction
helpers, dead audio-restore path, dead test knobs) plus four MSBuild-only exe-size
levers and four closed code-review findings. I re-verified the specific claims in the
phase description against the actual diffs (not just the current tree) rather than
trusting the merged-tree regression gate at face value:

- **Dead-code deletion (snapshot subsystem, `IMonitorController.Restore`,
  `IAudioController.Restore`, dead test knobs):** confirmed complete. `grep -rn
  "SnapshotStore\|StateSnapshot\|ISnapshotStore"` across `src/` returns zero hits, and
  the `IMonitorController.cs`/`IAudioController.cs`/`AssemblyInfo.cs` doc comments were
  correctly reworded in their respective deletion commits (`3a93bf8`, `6393180`) to no
  longer describe the removed mechanisms.
- **Program.cs bootstrap rewire** (`snapshotStore.Exists()` → `File.Exists(...)`):
  confirmed behaviorally identical — `JsonSnapshotStore.Exists()` was itself just
  `File.Exists(_path)` against the same `state.json` path (verified against the
  pre-deletion commit), so this is a pure refactor, not a behavior change.
  `git diff` against pre-deletion commit `61b88d5^` confirms no other logic changed.
  A local `dotnet build RigToggle.sln` (net10.0-windows, WinForms) succeeds with 0
  errors, corroborating the regression gate's build claim.
- **The four closed Phase 15/16 findings** (unreachable audio-combo branch,
  sentinel-sentence leaking into persisted `NormalAudioDeviceName`/`RigAudioDeviceName`,
  lowercase "skipped" wording, `ReconcileModeAfterMonitorFailure` tracing): all verified
  correct against their respective commits (`64b1c17`, `2ee7c4e`).
- **`ReconcileModeAfterMonitorFailure` (WR-03) specifically investigated per the review
  brief:** confirmed the fix is exactly what its own commit message and doc comment
  claim — Trace diagnostics only, mode-flag write behavior is provably unchanged (all
  three branches still leave the mode flag untouched). It does not silently pretend to
  fix the underlying "should the mode flag be reconciled differently" question; the
  class-level doc comment explicitly states the asymmetric-recovery design is
  intentional and must not be "fixed" into false symmetry. This is not a defect.

One real defect survived the cleanup: `WindowsMonitorController.cs` (the file the dead
code was actually deleted *from*) still contains several doc comments that describe the
now-deleted `Restore()`/`RestoreViaReconstruction()` methods as if they still exist in
this file, including one that promises "see Restore()'s own doc comment below" — a
promise that is no longer true. This is exactly the failure mode the review brief asked
me to watch for, and it is not caught by the grep-based structural audits (which check
for *reachability*/*unused* symbols, not for prose that references a since-deleted
symbol by name in an unreachable way). See WR-01 below.

## Warnings

### WR-01: Dangling doc-comment references to deleted `Restore()`/`RestoreViaReconstruction()` methods

**File:** `src/RigToggle.Windows/WindowsMonitorController.cs:66, 158-160, 170-171, 191`

**Issue:** Commit `6393180` deleted `WindowsMonitorController.Restore()`,
`RestoreViaReconstruction()`, `CopyOutputTechnology()`, `AssignSource()`, and the
`_originalPathsCache` field, and correctly updated the doc comments in
`IMonitorController.cs` and `AssemblyInfo.cs` to stop naming the deleted members. It did
**not**, however, update the doc comments inside `WindowsMonitorController.cs` itself,
which still reference `Restore()` as a currently-existing method in this file:

- Line 66 (on `GetAllMonitors`): "...the same 'inactive-path fields are unreliable'
  landmine already worked around elsewhere in this file (`Restore()`/`DeactivateMonitors()`)."
  — `Restore()` no longer exists anywhere in this file.
- Lines 158-160 (on `ActivateMonitors`): "...already tried and abandoned in this exact
  codebase's `Restore()` history, three separate rig-tested validation failures —
  **see `Restore()`'s own doc comment below**". This is a direct, false promise: there
  is no `Restore()` doc comment below (or anywhere) in this file anymore. A reader
  following this pointer will search the file, not find it, and have no idea the
  content moved to `.planning/debug/knowledge-base.md`.
- Line 160: "...zero-argument `PathInfo.ApplyTopology(Extend)` call `Restore()`'s
  crash-recovery fallback already proves works" — same dangling reference.
- Lines 170-171 (on `ActivateMonitors`' ordering contract): "...it must run **AFTER
  `Restore()`**, not before, for the same reason (`Restore()`'s crash-recovery fallback
  also uses Extend internally)." — describes an ordering constraint relative to a method
  that no longer exists in the codebase at all (Normal mode now applies its own explicit
  target via `ActivateMonitors`/`DeactivateMonitors`, not `Restore()`).
- Line 191 (on `ActivateMonitors`' availability guard): "Early availability guard
  (mirrors `Restore()` Step 1)" — same dangling reference.

This directly contradicts the phase's own stated goal (CLEANUP-01: preserve
rig-specific knowledge *and* leave the tree clean) and is precisely the failure mode
called out in the review brief ("doc comments that still describe deleted mechanisms").
The knowledge itself *was* correctly preserved in
`.planning/debug/knowledge-base.md` (`ccd-topology-restore-findings` section, confirmed
present with both cited fragile identifiers, `<OutputTechnology>k__BackingField` and the
`SDC_ALLOW_CHANGES` note) — this is purely a matter of the surviving comments in the
live source file still pointing at a corpse.

**Fix:** Reword the five call sites to stop referring to `Restore()` as if it still
exists in this file, and point at the knowledge-base entry instead, e.g.:

```csharp
// Real Extend-based activation of previously OS-disabled monitors (06-RESEARCH.md
// Pattern 2) — the load-bearing generalization answer: NEVER manually reconstruct
// PathTargetInfo/mode info for a previously-inactive target. This was tried and
// abandoned in this codebase's history (three separate rig-tested validation
// failures) — see .planning/debug/knowledge-base.md#ccd-topology-restore-findings
// for the retained findings (the code that produced them, WindowsMonitorController.
// Restore/RestoreViaReconstruction, was deleted in Phase 18/CLEANUP-01). Instead
// reuse the same zero-argument PathInfo.ApplyTopology(Extend) call the retired
// crash-recovery fallback proved works: ...
```

Apply the same "reference the knowledge-base entry, not a deleted in-file method" edit
to lines 66, 170-171, and 191.

## Info

### IN-01: `ReconcileModeAfterMonitorFailure`'s "partial mutation" branch leaves the persisted mode flag potentially stale (pre-existing, not a Phase 18 regression)

**File:** `src/RigToggle.Core/ToggleService.cs:264-267`

**Issue:** Not a defect introduced by this phase (the WR-03 change here is
Trace-diagnostics-only, confirmed against the diff), but worth recording since the
review brief specifically asked about this method's correctness: when a Monitor step
fails *after* a real, partial CCD mutation, the mode flag is deliberately left at its
prior value rather than being marked "unknown"/reconciled to the new physical state.
The class-level doc comment states this is intentional (no third "Indeterminate" mode
value is introduced), so this is a documented design tradeoff, not a bug — flagging only
because the persisted mode flag can genuinely misrepresent reality until the next
successful toggle corrects it, and a future reader might mistake the Trace-only WR-03
change for having "fixed" this ambiguity when it has not (and was never intended to).

**Fix:** No action required for Phase 18. If a future phase wants to close this gap,
the class doc comment already identifies the option space (a third mode value) and
explicitly rejects it for this phase — that decision should be revisited deliberately,
not accidentally reopened by a future refactor of this method.

### IN-02: `MonitorState.cs` history comment is slightly ambiguous about deletion timing

**File:** `src/RigToggle.Core/Models/MonitorState.cs:14-17`

**Issue:** "That restore path was removed in Phase 18 once Phase 16 replaced it with
explicit per-mode target application" is technically correct (Phase 16 replaced the
*production* usage; Phase 18 deleted the now-dead *code*) but reads ambiguously on
first pass — "removed... once... replaced" could be misread as "Phase 16 did the
removal." Given this file was explicitly reviewed for exactly this class of issue, it's
worth a one-word tighten.

**Fix:** Minor wording tighten, not blocking:

```csharp
/// That restore path was made dead by Phase 16 (which replaced it with explicit
/// per-mode target application) and was physically deleted in Phase 18; this record's
/// shape was kept as-is because CR-01's comparison still needs the same full-topology
/// snapshot.
```

---

_Reviewed: 2026-08-09T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

## Resolution (2026-08-09)

- **WR-01** (stale `Restore()` doc comments) — fixed, commit `57c99cf`. Five comments in `WindowsMonitorController.cs` updated to describe the deleted method accurately in past tense, including one dangling "see Restore()'s own doc comment below" forward reference.
- **IN-01** (mode-flag reconciliation design tradeoff) — no action, as recommended. Documented, deliberate scope boundary for this phase.
- **IN-02** (`MonitorState.cs` ambiguous history wording) — fixed, commit `8ff7b3a`, using the reviewer's own suggested wording verbatim.

`dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` — 0 errors. `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 81/81 pass.
