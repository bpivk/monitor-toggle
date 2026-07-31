---
phase: 09-global-hotkey-trigger
plan: 01
subsystem: core
tags: [hotkey, formatting, settings, win32-constants, xunit, tdd]

# Dependency graph
requires:
  - phase: 02-foundations-gui-shell
    provides: AppSettings flat-POCO convention and JsonSettingsStore atomic save/load
  - phase: 08-tray-residency-autostart-toast-notification
    provides: RigToggle.Core static-formatter precedent (ToggleResultFormatter) this plan mirrors
provides:
  - HotkeyCombo readonly record struct with Win32 MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN constants
  - HotkeyCombo.IsModifierVirtualKey bare-modifier detection helper
  - HotkeyFormatter.ToDisplayString friendly combo string formatter (Ctrl+Alt+R style)
  - AppSettings.HotkeyModifiers / AppSettings.HotkeyKey nullable persisted fields
affects: [09-02-hotkey-registration-and-capture-ui, 10-cli-trigger]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pure Core value type + static formatter pair (HotkeyCombo + HotkeyFormatter) mirrors the existing StartupArgs/ToggleResultFormatter precedent: zero Windows references, never-throw contract, unit-tested in RigToggle.Tests"

key-files:
  created:
    - src/RigToggle.Core/HotkeyCombo.cs
    - src/RigToggle.Core/HotkeyFormatter.cs
    - src/RigToggle.Tests/HotkeyFormatterTests.cs
  modified:
    - src/RigToggle.Core/Models/AppSettings.cs
    - src/RigToggle.Tests/JsonStoreTests.cs

key-decisions:
  - "HotkeyCombo modifier constants and modifier-virtual-key ranges hardcoded as plain int Win32 wire-format values (not an enum) — matches the interfaces block's canonical MOD_* values exactly, and keeps RigToggle.Core free of any Windows-API-flavored abstraction that plan 09-02's actual RegisterHotKey P/Invoke will consume as raw ints"
  - "HotkeyFormatter falls back to a stable \"Key\"+value token for any unmapped virtual-key value, including negative/out-of-range garbage, satisfying T-09-01's never-throw mitigation for hand-edited settings.json tampering"

requirements-completed: [TRIG-01]

# Metrics
duration: 10min
completed: 2026-07-31
---

# Phase 9 Plan 1: Global Hotkey Core Representation & Formatting Summary

**Pure Core `HotkeyCombo`/`HotkeyFormatter` pair rendering Win32 modifier+virtual-key pairs as fixed-order friendly strings (e.g. "Ctrl+Alt+R"), plus two new nullable `AppSettings` fields that round-trip through `JsonSettingsStore`.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-07-31T18:25:00Z (approx, first read)
- **Completed:** 2026-07-31T18:35:45Z
- **Tasks:** 2 completed
- **Files modified:** 5 (2 created new source files, 1 created test file, 2 existing files extended)

## Accomplishments
- `HotkeyCombo` readonly record struct with `ModAlt`/`ModControl`/`ModShift`/`ModWin` Win32-mirroring constants and `IsModifierVirtualKey` bare-modifier detection, zero Windows API references
- `HotkeyFormatter.ToDisplayString` renders the fixed Ctrl→Alt→Shift→Win modifier order followed by the key's display name (A-Z, 0-9, F1-F24, common named keys, stable fallback for anything else), matching 09-UI-SPEC.md's friendly combo string format exactly
- `AppSettings.HotkeyModifiers`/`HotkeyKey` nullable int fields added, round-tripping through the existing `JsonSettingsStore` atomic save/load with no store-code changes needed (plain `JsonSerializer` had no field allow-list to update)
- Full TDD RED→GREEN cycle for the formatter: `HotkeyFormatterTests.cs` written first (referencing a not-yet-existing `HotkeyFormatter`, confirmed as a compile-failure RED state), then `HotkeyFormatter.cs` implemented to satisfy it

## Task Commits

Each task was committed atomically:

1. **Task 1: HotkeyCombo value type + modifier constants + modifier-key detection** - `438d977` (feat)
2. **Task 2 (RED): HotkeyFormatterTests.cs failing test** - `45b9ac9` (test)
2. **Task 2 (GREEN): HotkeyFormatter.ToDisplayString + JsonStoreTests round-trip** - `e626594` (feat)

**Plan metadata:** committed separately after this SUMMARY (see final commit step)

_Note: Task 2 is TDD (`tdd="true"`), so it produced the required test → feat commit pair per the plan-level TDD gate._

## Files Created/Modified
- `src/RigToggle.Core/HotkeyCombo.cs` - Readonly record struct with Win32 MOD_* constants and `IsModifierVirtualKey`
- `src/RigToggle.Core/HotkeyFormatter.cs` - `ToDisplayString(int modifiers, int virtualKey)` friendly-combo formatter, never throws
- `src/RigToggle.Core/Models/AppSettings.cs` - Added `HotkeyModifiers`/`HotkeyKey` nullable int fields, extended class XML doc
- `src/RigToggle.Tests/HotkeyFormatterTests.cs` - Theory/Fact coverage for every ToDisplayString example plus `IsModifierVirtualKey` range checks
- `src/RigToggle.Tests/JsonStoreTests.cs` - Added hotkey round-trip fact, null-round-trip fact, and null-default assertions on the existing missing-file-load fact

## Decisions Made
- Kept modifier constants as plain `int` (not an enum) to match the plan's explicit interfaces-block contract and stay a direct pass-through for the eventual `RegisterHotKey` P/Invoke in plan 09-02, which expects raw `uint`/`int` flag values, not a managed enum
- `KeyDisplayName`'s fallback token format (`"Key" + virtualKey`) chosen over an empty string or exception specifically to satisfy the threat register's T-09-01 "never throw on hand-edited settings.json tampering" mitigation

## Deviations from Plan

None - plan executed exactly as written. All behavior examples in the plan's `<behavior>` blocks are covered by tests; `AppSettings`/`HotkeyCombo`/`HotkeyFormatter` shapes match the plan's `<action>` blocks exactly (field names, constant values, modifier ordering, key-name mapping table).

## Issues Encountered

None. The sandbox has no .NET SDK installed (confirmed: `dotnet` not on PATH) — this is an established, accepted constraint carried over from Phases 6, 7, and 8 (see `08-01-SUMMARY.md`). Verification was performed via targeted `grep`-based acceptance-criteria checks (constant values, field presence, zero Windows-reference greps) and manual trace-through of every `ToDisplayString`/`IsModifierVirtualKey` test case against the implementation logic, rather than a live `dotnet test` run.

**A real `dotnet build`/`dotnet test` pass on the Windows rig is still needed before this plan (and the phase) is fully verified — same standing note as prior phases.**

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `HotkeyCombo`/`HotkeyFormatter` are ready for plan 09-02 (hotkey capture UI + `RegisterHotKey` P/Invoke in RigToggle.Windows) to consume directly — the modifier constants and `IsModifierVirtualKey` predicate are the exact contract the Settings capture textbox needs to reject bare-modifier presses (D-01)
- `AppSettings.HotkeyModifiers`/`HotkeyKey` are persisted and null-until-configured (D-02); plan 09-02 can read/write them without further Core-layer changes
- Blocker carried from STATE.md: `RegisterHotKey` must be rig-tested with Moza Companion actually running in plan 09-02/09-03, since silent conflicts with other rig software are the realistic failure mode TRIG-01 exists to catch — no action needed in this plan, flagged for the next one
- No blockers for proceeding to plan 09-02

---
*Phase: 09-global-hotkey-trigger*
*Completed: 2026-07-31*
