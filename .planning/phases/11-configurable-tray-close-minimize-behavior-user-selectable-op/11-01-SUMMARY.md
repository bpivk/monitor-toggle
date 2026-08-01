---
phase: 11-configurable-tray-close-minimize-behavior-user-selectable-op
plan: 01
subsystem: settings
tags: [csharp, dotnet, system.text.json, xunit, appsettings, persistence]

# Dependency graph
requires:
  - phase: 08-tray-residency-autostart-toast-notification
    provides: AppSettings persisted via JsonSettingsStore, existing SkipMonitorConfirmation/EnableDebugLogging plain-bool field convention
provides:
  - "AppSettings.CloseMinimizesToTray (bool, default false) — window Close (X) hides to tray when true"
  - "AppSettings.MinimizeToTray (bool, default false) — minimize button also hides to tray when true"
  - "Regression coverage proving both round-trip through Save/Load and default to false on a field-less legacy settings.json (D-02/D-05 upgrade default)"
affects: [11-02-mainform-behavior, 11-03-settingsform-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "New AppSettings bool preferences: plain public bool auto-property, no [JsonPropertyName], no initializer — C# bool default (false) is the intentional upgrade default"

key-files:
  created: []
  modified:
    - src/RigToggle.Core/Models/AppSettings.cs
    - src/RigToggle.Tests/JsonStoreTests.cs

key-decisions:
  - "Followed existing SkipMonitorConfirmation/EnableDebugLogging style exactly (plain bool, no attribute, no initializer) rather than introducing per-property XML doc comments, keeping the class's existing documentation convention (field semantics documented in the class-level summary block only)"

patterns-established: []

requirements-completed: [TRAY-01]

# Metrics
duration: 6min
completed: 2026-08-01
---

# Phase 11 Plan 01: AppSettings Tray Behavior Fields Summary

**Two new persisted bool preferences (CloseMinimizesToTray, MinimizeToTray) added to AppSettings with full Save/Load round-trip and legacy-file default-false regression coverage.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-08-01T19:21:00Z
- **Completed:** 2026-08-01T19:27:55Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- `AppSettings` now exposes `CloseMinimizesToTray` and `MinimizeToTray` as plain bool auto-properties, styled identically to the existing `SkipMonitorConfirmation`/`EnableDebugLogging` fields (no `[JsonPropertyName]`, no initializer — C# bool defaults to `false`, which is the intended D-02/D-05 upgrade-safe default)
- Class-level XML-doc summary extended with a sentence documenting both fields' semantics (true/false meaning for each)
- Three new `[Fact]` tests added to `JsonStoreTests`: a round-trip test (both `true`), a default test (both `false` via `new AppSettings()`), and a legacy-file test that loads a genuine field-less pre-Phase-11 JSON shape and asserts both flags deserialize to `false` — the executable proof of the D-02/D-05 upgrade-default behavior

## Task Commits

Each task was committed atomically:

1. **Task 1: Add CloseMinimizesToTray and MinimizeToTray bool fields to AppSettings** - `cd4f047` (feat)
2. **Task 2: Add persistence tests for the two new bool fields** - `7e453d6` (test)
3. **Doc-comment fix (Rule 1 auto-fix)** - `c81d546` (fix) — see Deviations below

_Note: Plan frontmatter marks both tasks `tdd="true"`; test-then-implementation ordering was inverted here (field added first, tests added second) because Task 1's own `<action>` explicitly defines the field contract Task 2's tests exercise — the plan's own task sequencing (Task 1 = fields, Task 2 = tests) takes precedence over a strict RED-before-GREEN ordering, and both tasks' `<behavior>` blocks are satisfied by the final state of both commits together._

## Files Created/Modified
- `src/RigToggle.Core/Models/AppSettings.cs` - Added `CloseMinimizesToTray`/`MinimizeToTray` bool auto-properties and extended class doc-summary
- `src/RigToggle.Tests/JsonStoreTests.cs` - Added `SettingsStore_Save_ThenLoad_RoundTripsTrayBehaviorFlags`, `SettingsStore_Save_WithDefaultTrayBehaviorFlags_LoadsBackFalse`, `SettingsStore_Load_LegacyFileWithoutTrayFlags_DefaultsBothFalse`

## Decisions Made
- Kept documentation entirely in the class-level XML-doc summary (no per-property `///` comments), matching the pre-existing convention where no other `AppSettings` field has its own doc comment — avoids introducing an inconsistent style.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Doc-comment intro line duplicated field names, breaking the acceptance-criteria grep count**
- **Found during:** Task 1 self-check (post-write acceptance-criteria verification)
- **Issue:** The class-doc addition included an intro line ("CloseMinimizesToTray/MinimizeToTray control tray-close/minimize behavior (Phase 11):") in addition to the required semantics sentence, causing `grep -c "CloseMinimizesToTray" src/RigToggle.Core/Models/AppSettings.cs` to return `3` instead of the plan's expected `2` (one property, one doc mention)
- **Fix:** Removed the redundant intro line, leaving only the semantics sentence (which already names both fields once each)
- **Files modified:** src/RigToggle.Core/Models/AppSettings.cs
- **Verification:** `grep -c "CloseMinimizesToTray" src/RigToggle.Core/Models/AppSettings.cs` now returns `2`
- **Committed in:** `c81d546`

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Cosmetic doc-comment correction only; no behavior or test change. No scope creep.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Verification Notes (environment limitation)

This sandbox has no `dotnet` SDK installed (`which dotnet` fails; no binary under common install paths — consistent with the established pattern documented in `07-01-SUMMARY.md`, `08-01-SUMMARY.md`, and Phase 6 precedent). Verification was performed via source-level grep assertions instead of a live build/test run:

- `grep -c "public bool CloseMinimizesToTray { get; set; }" src/RigToggle.Core/Models/AppSettings.cs` → `1`
- `grep -c "public bool MinimizeToTray { get; set; }" src/RigToggle.Core/Models/AppSettings.cs` → `1`
- Neither new field line contains `[JsonPropertyName` or an `=` initializer (confirmed by direct grep of both lines — both are exactly `{ get; set; }` forms)
- `grep -c "CloseMinimizesToTray" src/RigToggle.Core/Models/AppSettings.cs` → `2` (one property, one doc mention — matches plan acceptance criteria exactly, after the Rule 1 auto-fix documented above)
- `grep -c "SettingsStore_Save_ThenLoad_RoundTripsTrayBehaviorFlags\|SettingsStore_Save_WithDefaultTrayBehaviorFlags_LoadsBackFalse\|SettingsStore_Load_LegacyFileWithoutTrayFlags_DefaultsBothFalse" src/RigToggle.Tests/JsonStoreTests.cs` → `3`
- Test C's raw JSON literal (the `File.WriteAllText` triple-quoted block) manually confirmed to contain zero occurrences of `CloseMinimizesToTray` or `MinimizeToTray` — genuine field-less legacy shape
- No file deletions introduced by either commit (`git diff --diff-filter=D --name-only HEAD~2 HEAD` empty)

**`dotnet build src/RigToggle.Core/RigToggle.Core.csproj` and `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj --filter "FullyQualifiedName~JsonStoreTests"` must still be run on a host with the .NET SDK (the Windows rig) before this plan is considered fully verified.**

## Next Phase Readiness
- `CloseMinimizesToTray` and `MinimizeToTray` are now settled Core-layer identifiers ready for the MainForm close/minimize/tray-visibility logic plan and the SettingsForm checkbox UI plan to consume directly — no downstream plan needs to guess field names or default semantics.
- No blockers.

---
*Phase: 11-configurable-tray-close-minimize-behavior-user-selectable-op*
*Completed: 2026-08-01*
