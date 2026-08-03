---
phase: 13-tray-app-icon-redesign
plan: 02
subsystem: ui
tags: [winforms, notifyicon, dpi, msbuild, applicationicon, system.drawing]

# Dependency graph
requires:
  - phase: 13-tray-app-icon-redesign (plan 01)
    provides: "Regenerated normal.ico/rig.ico (4 frames, 16/20/24/32px) and new app.ico (6 frames, 16/20/24/32/48/256px), all round-trip verified"
provides:
  - "MainForm.LoadTrayIconsIfNeeded() now requests SystemInformation.SmallIconSize, so the tray NotifyIcon gets the DPI-correct frame instead of always the smallest 16px frame (fixes 13-RESEARCH.md Pitfall 1)"
  - "RigToggle.App.csproj wires app.ico as the compiled exe's native Win32 icon via <ApplicationIcon>, satisfying ICON-04's exe/taskbar/Explorer/Alt-Tab identity requirement"
affects: ["13-03-PLAN.md (rig-checkpoint human visual verification of tray-icon DPI sharpness and exe/taskbar icon, now that both the assets (13-01) and the runtime/build wiring (this plan) are complete)"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "SystemInformation.SmallIconSize as the sized Icon(Stream, Size) constructor argument for DPI-correct NotifyIcon frame selection (BCL-native, no new dependency)"
    - "<ApplicationIcon> MSBuild property as the separate native-Win32-resource icon-embedding mechanism, distinct from EmbeddedResource+LogicalName runtime-loaded icons"

key-files:
  created: []
  modified:
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/RigToggle.App.csproj

key-decisions:
  - "Followed 13-RESEARCH.md Pitfall 1 over 13-UI-SPEC.md's Scope Notes claim that no MainForm.cs code changes were needed -- RESEARCH.md's finding (documented System.Drawing.Icon size-less-constructor-returns-smallest-frame behavior, corroborated by dotnet/winforms#6955) is empirically grounded and supersedes the earlier UI-SPEC assumption per this plan's own objective"

patterns-established: []

requirements-completed: [ICON-03, ICON-04]

# Metrics
duration: ~15min
completed: 2026-08-03
---

# Phase 13 Plan 02: Tray DPI Fix & ApplicationIcon Wiring Summary

**Fixed the tray NotifyIcon's DPI-blur defect by requesting `SystemInformation.SmallIconSize` in both `Icon` constructors, and wired `app.ico` as the compiled exe's native Win32 icon via `<ApplicationIcon>`.**

## Performance

- **Duration:** ~15 min
- **Completed:** 2026-08-03T12:09:16Z
- **Tasks:** 2/2 complete
- **Files modified:** 2

## Accomplishments
- `MainForm.LoadTrayIconsIfNeeded()`'s two `new System.Drawing.Icon(stream ?? throw ...)` calls (for `normal.ico` and `rig.ico`) now pass `SystemInformation.SmallIconSize` as a second argument, so `NotifyIcon` receives the frame matching the OS's current DPI scaling (16/20/24/32px at 100/125/150/200%) instead of always the smallest 16px frame upscaled by Windows — the exact defect documented in 13-RESEARCH.md Pitfall 1, verified against Microsoft Learn's `Icon(Stream)` docs and `dotnet/winforms#6955`
- `RigToggle.App.csproj` gained a single `<ApplicationIcon>Resources\app.ico</ApplicationIcon>` line in the top `PropertyGroup`, embedding `app.ico` as a native `RT_GROUP_ICON`/`RT_ICON` Win32 resource — Explorer, Alt-Tab, and the taskbar now resolve the correct per-DPI frame directly from the compiled binary through their own DPI-aware shell path, independent of the `System.Drawing.Icon` defect above
- Confirmed no runtime code anywhere in `MainForm.cs` references `app.ico` (it is consumed exclusively via the native-resource mechanism, never `GetManifestResourceStream`)
- Confirmed the existing `normal.ico`/`rig.ico` `EmbeddedResource`+`LogicalName` block is byte-for-byte unchanged, and `app.ico` was NOT added as an `EmbeddedResource`
- Both the IN-01 explicit-null-check-with-descriptive-exception convention and the `_normalIcon is not null && _rigIcon is not null` GDI-handle-leak-avoidance caching guard (08-RESEARCH.md Pitfall 3) preserved verbatim
- `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` and `-c Release` both exit 0; full `dotnet build RigToggle.sln -c Debug` also exits 0 (0 errors; 3 pre-existing unrelated xUnit1031 test warnings, out of this plan's scope)

## Task Commits

Each task was committed atomically:

1. **Task 1: Fix tray-icon DPI frame selection in MainForm.cs (Pitfall 1)** - `25b7727` (fix)
2. **Task 2: Wire app.ico as the exe/taskbar icon via `<ApplicationIcon>`** - `a4103ad` (feat)

## Files Created/Modified
- `src/RigToggle.App/MainForm.cs` - Both `new System.Drawing.Icon(...)` calls in `LoadTrayIconsIfNeeded()` now pass `SystemInformation.SmallIconSize`
- `src/RigToggle.App/RigToggle.App.csproj` - Added `<ApplicationIcon>Resources\app.ico</ApplicationIcon>` to the top `PropertyGroup`; existing `EmbeddedResource` block for `normal.ico`/`rig.ico` unchanged

## Decisions Made
- Trusted 13-RESEARCH.md's Pitfall 1 finding over 13-UI-SPEC.md's earlier (pre-research) Scope Notes claim that no `MainForm.cs` changes were needed — this plan's own objective explicitly calls this out as an intentional supersession, not a deviation, so it is not logged under "Deviations from Plan" below.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched their `<action>` blocks precisely; all `<acceptance_criteria>` and the plan-level `<verification>` checks passed on the first attempt with no auto-fixes needed.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 13-03 (rig-checkpoint human visual verification) is unblocked: both the regenerated icon assets (13-01) and the runtime/build wiring that makes them actually visible correctly (this plan) are complete. The rig check can now meaningfully evaluate tray-icon DPI sharpness (ICON-03) and the exe/taskbar icon (ICON-04) together.
- No outstanding gaps from this plan's scope.

---
*Phase: 13-tray-app-icon-redesign*
*Completed: 2026-08-03*
