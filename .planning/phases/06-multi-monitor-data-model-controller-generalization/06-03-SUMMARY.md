---
phase: 06-multi-monitor-data-model-controller-generalization
plan: 03
subsystem: monitor-control
tags: [ccd, windowsdisplayapi, display-config, multi-monitor]

# Dependency graph
requires:
  - phase: 06-multi-monitor-data-model-controller-generalization
    provides: "Plan 02's generalized IMonitorController interface (GetAllMonitors/ActivateMonitors/DeactivateMonitors signatures) and FakeMonitorController test double already implementing the new shape"
provides:
  - "Real CCD implementation of the generalized IMonitorController triad in WindowsMonitorController: GetAllMonitors (active + OS-disabled-but-available enumeration), ActivateMonitors (Extend-based activation with skip-optimization, availability guard, verify-and-throw), DeactivateMonitors (N-target generalization of the former single-target Disable, with survivor uniform-shift and verify-and-throw)"
  - "AnyRectanglesOverlap pure helper (internal static, axis-aligned bounding-box check) shared by DeactivateMonitors and Restore verify-and-throw sections"
  - "N-generalized Restore() verify (both fast-path and crash-recovery fallback) with overlap check"
affects: [06-04, 06-05, 06-06-rig-validation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Extend-based CCD activation (PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence:false)) reused verbatim from Restore()'s pre-existing crash-recovery fallback for ActivateMonitors — never manually reconstruct PathTargetInfo/mode info for a previously-inactive target"
    - "Set-based verify-and-throw generalization (Except/Contains against IReadOnlySet<string>) replacing single-value null-checks, for N-target activate/deactivate"
    - "Shared pure AnyRectanglesOverlap(IReadOnlyList<Rectangle>) helper reused across every mutating method's verify step"

key-files:
  created: []
  modified:
    - src/RigToggle.Windows/WindowsMonitorController.cs
    - src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs

key-decisions:
  - "ActivateMonitors must run before DeactivateMonitors on rig-mode entry (Pitfall 2 ordering contract) — documented in both methods' XML-doc comments so a reader of this adapter alone understands the constraint, enforcement itself lives in ToggleService (a later plan's scope)"
  - "No gap-closing/reflow logic added to DeactivateMonitors beyond the existing uniform-shift-on-primary-removal idiom, per D-01's explicit scope boundary — Windows' own default placement is accepted for the surviving layout otherwise"
  - "AnyRectanglesOverlap made internal static (not private) to match this file's existing InternalsVisibleTo-based pure-logic test convention (CopyOutputTechnology, AssignSource)"

patterns-established:
  - "Verify-and-throw discipline (re-query GetActivePaths after every mutating CCD call, throw InvalidOperationException on mismatch, never trust a non-throwing return, never use Screen.AllScreens as oracle) now extends uniformly across GetAllMonitors/ActivateMonitors/DeactivateMonitors/Restore, including a shared overlap check"

requirements-completed: [DISPLAY-04, DISPLAY-05]

# Metrics
duration: 35min
completed: 2026-07-28
---

# Phase 6 Plan 03: Real Windows CCD Adapter Generalization Summary

**Generalized the real Windows CCD monitor adapter (`WindowsMonitorController`) from a single-target disable/restore pair to the full N-monitor `IMonitorController` triad — `GetAllMonitors()` enumerates OS-disabled-but-available displays, `ActivateMonitors()` reuses the proven `ApplyTopology(Extend)` mechanism, `DeactivateMonitors()` generalizes the existing repositioning-aware removal 1→N, and every mutating method's verify-and-throw now includes a shared bounding-box overlap check.**

## Performance

- **Duration:** 35 min
- **Started:** 2026-07-28T11:20:00Z (approx, per session start)
- **Completed:** 2026-07-28
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- `GetAllMonitors()` fills the enumeration gap `GetActiveMonitors()` structurally cannot: it wraps `PathInfo.GetAllPaths()`, guards `IsGDIPrimary` behind `IsModeInformationAvailable` (avoids `MissingModeException` on inactive paths), and filters targets to `IsAvailable` before reading `FriendlyName`/`DevicePath` (avoids `TargetNotAvailableException`). This makes DISPLAY-05's "enable a normally-disabled monitor" premise concrete — a rig monitor kept OS-disabled to save power is now enumerable and name-resolvable before it's ever activated.
- `ActivateMonitors(set)` activates OS-disabled monitors via the exact same zero-argument `PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence:false)` call `Restore()`'s crash-recovery fallback already proved works on this rig — no new native-API surface. Includes a skip-optimization (no-op if every requested path is already active, avoiding an unnecessary whole-topology Extend recompute), an early availability guard, and verify-and-throw.
- `DeactivateMonitors(set)` replaces the former single-target `Disable(string)`, generalizing every piece of its logic to N targets: an `Except`-based "not currently active" guard, a multi-target `targets.Any(t => t.IsGDIPrimary)` gate for the uniform-shift branch, and a generalized verify-and-throw. No gap-closing/reflow logic was added beyond the existing shift-to-origin idiom, matching D-01's explicit scope boundary.
- `Restore()`'s verify-and-throw (both the in-process fast path and the crash-recovery fallback) is now N-generalized with a bounding-box overlap check via the new shared `AnyRectanglesOverlap` helper.
- Added `AnyRectanglesOverlap_NonOverlappingSideBySide_ReturnsFalse`, `AnyRectanglesOverlap_Overlapping_ReturnsTrue`, `AnyRectanglesOverlap_Empty_ReturnsFalse` pure-logic tests, following this file's existing "pure logic only, no live CCD hardware" test convention.

## Task Commits

Each task was committed atomically:

1. **Task 1: GetAllMonitors (enumeration gap) + ActivateMonitors (Extend-based activation)** - `90fe29d` (feat)
2. **Task 2: DeactivateMonitors (generalize Disable 1→N) + Restore/overlap verify hardening + overlap-helper test** - `3dae3be` (feat)

**Plan metadata:** committed alongside this SUMMARY (see final commit in this plan's execution)

## Files Created/Modified

- `src/RigToggle.Windows/WindowsMonitorController.cs` — added `GetAllMonitors()`, `ActivateMonitors(IReadOnlySet<string>)`, replaced `Disable(string)` with `DeactivateMonitors(IReadOnlySet<string>)`, added `internal static AnyRectanglesOverlap`, N-generalized `Restore()`'s two verify sections with the overlap check, and updated stale `Disable()`-referencing doc comments throughout the file to `DeactivateMonitors()`.
- `src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs` — added `System.Drawing` using and three `AnyRectanglesOverlap_*` pure-logic tests.

## Decisions Made

- **Ordering contract documented, not enforced, in this file.** `ActivateMonitors` must run before `DeactivateMonitors` on rig-mode entry (Pitfall 2 — `ApplyTopology(Extend)` restores the CCD persistence database's last-known layout, which would silently re-activate a just-disabled monitor if Extend ran after the disable). This adapter documents the contract in both methods' doc comments; actual sequencing enforcement is `ToggleService`'s responsibility, scoped to a later plan (06-04/06-05 per the wave dependency graph), not this one.
- **No gap-closing/reflow logic in `DeactivateMonitors`.** Per D-01's explicit scope boundary and 06-RESEARCH.md Pattern 1's closing note, the generalized method preserves the exact uniform-shift-on-primary-removal behavior from the original `Disable()` and does not attempt to close gaps left by removing a non-primary monitor from a multi-monitor layout — Windows' own default placement is accepted as sufficient.
- **`AnyRectanglesOverlap` is `internal static`**, matching the existing `CopyOutputTechnology`/`AssignSource` convention in this file (pure-logic helpers exposed to `RigToggle.Windows.Tests` via the pre-existing `InternalsVisibleTo` in `AssemblyInfo.cs`, no new assembly configuration needed).

## Deviations from Plan

None — plan executed exactly as written. Both tasks' `<action>` and `<acceptance_criteria>` were followed directly from 06-RESEARCH.md's Pattern 1/2/3 code examples and 06-PATTERNS.md's line-referenced analogs. Additionally updated several doc-comment references to the old `Disable()` method name (in `Restore()`'s own comments and the unused `CopyOutputTechnology`/`AssignSource` historical-context comments) to `DeactivateMonitors()` for documentation accuracy — this is a same-scope doc-only touch-up, not a functional deviation, and was not itself a distinct commit-worthy change (folded into Task 2's commit since it touches the same replaced method's surrounding context).

## Environment / Verification Notes

This is a Linux sandbox with no `dotnet` SDK — `RigToggle.Windows`/`RigToggle.Windows.Tests` target `net10.0-windows` and reference `WindowsDisplayAPI`/WinForms, so neither `dotnet build` nor `dotnet test` could be run in this session. All verification was performed via source/grep assertions matching every `<verify>` and `<acceptance_criteria>` block in the plan:

- `public IReadOnlyList<MonitorInfo> GetAllMonitors` present (1 match)
- `public void ActivateMonitors(IReadOnlySet<string>` present (1 match)
- `GetAllPaths` referenced (5 matches across `GetAllMonitors`, `ActivateMonitors`, and `Restore`'s existing fallback)
- `ApplyTopology(DisplayConfigTopologyId.Extend` present (2 matches: `ActivateMonitors`, `Restore`'s pre-existing fallback)
- `IsModeInformationAvailable` guard present (2 matches: `GetAllMonitors`, `DeactivateMonitors`/`Restore` overlap filters)
- No manual `new PathTargetInfo(` construction added anywhere (0 matches)
- `public void DeactivateMonitors(IReadOnlySet<string>` present (1 match); `public void Disable(string` absent (0 matches)
- `AnyRectanglesOverlap` referenced 4 times in the controller (1 declaration + 3 call sites: `DeactivateMonitors`, `Restore` fast path, `Restore` fallback) and 6 times in the test file (3 test method names + 3 assertion call sites)
- `targets.Any(t => t.IsGDIPrimary)` confirms the multi-target primary gate
- `_originalPathsCache = currentPaths` confirms cache-before-mutation ordering is preserved
- `grep -c "AnyRectanglesOverlap_"` on the test file returns exactly 3

**Real CCD mutation behavior — GetAllMonitors's inactive-path enumeration, ActivateMonitors's Extend-based activation of a genuinely OS-disabled monitor, DeactivateMonitors's N-target removal with overlap-checked verify, and the combined disable+enable topology sequencing — is unvalidated by this session and is NOT achievable in this sandbox.** Per the plan's own `ENVIRONMENT` note and 06-RESEARCH.md's Environment Availability section, this is deferred to the mandatory rig-validation checkpoint (Plan 06-06 / ROADMAP.md's completion gate), which must confirm both the long-idle/reboot monitor re-enable scenario and the combined disable+enable atomic topology change on real AMD/DisplayPort hardware before Phase 6 can be considered complete.

## Known Stubs

None. No hardcoded empty values, placeholder text, or unwired data sources were introduced — every new/generalized method is a real CCD call path, not a stub.

## Threat Flags

None. Both new threats identified in this plan's own `<threat_model>` (T-06-04: stale device path reaching a mutating method; T-06-05: silent overlapping/zero-primary topology after mutation) were explicitly scoped and mitigated within this plan's own task actions (availability guards, verify-and-throw with overlap check) — no new, unaccounted-for surface was introduced.

## Issues Encountered

None beyond the expected non-Windows build limitation, already documented above and anticipated by the plan's own `ENVIRONMENT` note.

## Next Phase Readiness

- `IMonitorController`'s full generalized triad is now real end-to-end: interface (Plan 02) → real Windows adapter (this plan) → test double already updated (Plan 02's `FakeMonitorController`, confirmed still in lockstep — `.Disable(` has zero remaining call sites anywhere in `src/`).
- Ready for 06-04/06-05 (per the wave dependency graph) to wire `ToggleService`'s call sites to the new `ActivateMonitors`/`DeactivateMonitors` methods with the correct Pitfall-2 ordering (`ActivateMonitors(enableSet)` before `DeactivateMonitors(disableSet)` on rig-mode entry; `DeactivateMonitors(enableSet)` after `Restore()` on toggle-back).
- The mandatory rig-validation checkpoint (06-06) remains the true acceptance gate for this plan's CCD behavior — flagged clearly above, not silently assumed passing.

---
*Phase: 06-multi-monitor-data-model-controller-generalization*
*Plan: 03*
*Completed: 2026-07-28*

## Self-Check: PASSED

- FOUND: src/RigToggle.Windows/WindowsMonitorController.cs
- FOUND: src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs
- FOUND: .planning/phases/06-multi-monitor-data-model-controller-generalization/06-03-SUMMARY.md
- FOUND commit: 90fe29d (Task 1)
- FOUND commit: 3dae3be (Task 2)
- FOUND commit: d2533f4 (SUMMARY)
