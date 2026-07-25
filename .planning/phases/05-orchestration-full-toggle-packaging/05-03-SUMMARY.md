---
phase: 05-orchestration-full-toggle-packaging
plan: 03
subsystem: packaging, monitor-control
tags: [dotnet-publish, msbuild, pubxml, ccd-api, windowsdisplayapi, self-contained]

requires:
  - phase: 05-orchestration-full-toggle-packaging (Plans 01-02)
    provides: CORE-04 structured ToggleResult + checklist UI, rig-verified
  - phase: 04-monitor-control-production
    provides: Real CCD monitor Disable/Restore via WindowsDisplayAPI
provides:
  - Self-contained, single-file, untrimmed win-x64 publish configuration (PACKAGING-01)
  - README documenting the publish command
  - Fixed monitor-restore fallback path for crash-recovery (CORE-05), replacing a
    latent Phase 4 bug that had never been exercised until this checkpoint
affects: [future packaging changes, any future WindowsMonitorController.Restore changes]

tech-stack:
  added: []
  patterns:
    - "dotnet publish -p:PublishProfile=<name> requires RuntimeIdentifier in the .csproj, not just the .pubxml (CLI does not honor RID from a pubxml alone)"
    - ".pubxml root <Project> element requires xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\" or the SDK silently treats the profile as not found (NETSDK1198), even though the file exists at the correct path"
    - "CCD restore-after-process-restart: never manually reconstruct PathTargetInfo/PathInfo mode info from primitives — reuse the exact 'ApplyTopology(Extend) then reposition using real live TargetsInfo objects' idiom already proven by Disable()'s survivor-repositioning"

key-files:
  created:
    - src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml
    - README.md
  modified:
    - src/RigToggle.App/RigToggle.App.csproj
    - src/RigToggle.Windows/WindowsMonitorController.cs

key-decisions:
  - "RuntimeIdentifier lives in RigToggle.App.csproj's PropertyGroup, not only the .pubxml, per RESEARCH Pitfall 1 (dotnet publish CLI ignores RID set only in a pubxml)"
  - "win-x64.pubxml requires the MSBuild xmlns on its root <Project> element — omitting it causes NETSDK1198 'profile not found' even when the file is present at the correct conventional path (not documented anywhere in this project's research; found via live rig failure)"
  - "Monitor Restore's crash-recovery fallback path (no in-process cache) was rewritten from manual field-by-field CCD struct reconstruction to a two-step ApplyTopology(Extend)-then-reposition approach, reusing Disable()'s already-proven 'touch only Position, reuse everything else from a real live object' pattern"

patterns-established:
  - "Any future WindowsDisplayAPI PathInfo/PathTargetInfo construction should prefer reusing real, live-queried objects wholesale over manually rebuilding them from primitives — every manual-reconstruction attempt in this codebase (3 separate CCD validation failures across this plan's Task 3) traced back to a field Windows does not reliably report for inactive paths."

requirements-completed: [PACKAGING-01, CORE-05, CORE-03, CORE-01, CORE-02]

duration: ~4h (including 5 rig round-trips for Task 3 checkpoint debugging)
completed: 2026-07-25
---

# Phase 5: Orchestration, Full Toggle & Packaging Summary

**Standalone self-contained win-x64 .exe (PACKAGING-01) ships correctly, and a latent Phase 4 crash-recovery monitor-restore bug — never before exercised — is fixed and rig-verified.**

## Performance

- **Duration:** ~4h (Tasks 1-2 ~15min; Task 3 checkpoint spanned 5 rig round-trips of live debugging)
- **Completed:** 2026-07-25
- **Tasks:** 3/3 (2 auto, 1 checkpoint — checkpoint required 4 follow-up fix commits before passing)
- **Files modified:** 4 (2 new, 2 modified — 1 modified file, `WindowsMonitorController.cs`, was not in the plan's original `files_modified` list)

## Accomplishments
- `RigToggle.App.csproj` + `win-x64.pubxml`: self-contained, single-file, untrimmed win-x64 publish configuration, CLAUDE.md-compliant
- `README.md`: documents the publish command and output location
- Fixed a real, previously-undetected bug in `WindowsMonitorController.Restore()`'s crash-recovery fallback path (built in Phase 4, never exercised by any prior verification since Phase 4's own rig checks used the in-process fast path, not a genuine process-restart scenario)
- Full rig verification passed: publish → no-runtime launch → complete monitor+audio+app round trip → crash-recovery (kill process while in Rig Mode, relaunch, restore) — all confirmed working by the user on the actual hardware

## Task Commits

1. **Task 1: Add RuntimeIdentifier to csproj + create win-x64.pubxml** - `77749fc` (feat)
2. **Task 2: Write README documenting the publish command** - `c62e5ed` (docs)
3. **Task 3: Publish + full rig end-to-end verification** - checkpoint, approved after 4 follow-up fixes:
   - `d46e11a` (fix) — pubxml missing MSBuild xmlns, causing `NETSDK1198`
   - `6f86120` (fix) — Restore's fallback reconstruction never supplied target signal info
   - `8fc6cd0` (fix) — Restore's fallback trusted an unreliable `GetActivePaths()`-vs-`GetAllPaths()` source for the active target, causing a source collision
   - `c02cfb6` (fix) — replaced manual reconstruction entirely with `ApplyTopology(Extend)` + reposition-from-live-objects, mirroring `Disable()`'s proven pattern

**Plan metadata:** this file (docs: complete plan)

## Files Created/Modified
- `src/RigToggle.App/RigToggle.App.csproj` - added `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`
- `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` - new: self-contained/single-file/untrimmed publish profile
- `README.md` - new: publish instructions, output location, untrimmed/win-x64-only notes
- `src/RigToggle.Windows/WindowsMonitorController.cs` - `Restore()`'s crash-recovery fallback path rewritten (not originally in this plan's scope — see Deviations)

## Decisions Made
- RuntimeIdentifier placed in the `.csproj` (not only the `.pubxml`) — the plan's own D-07 already anticipated this via "or equivalent PropertyGroup" wording, confirmed necessary by RESEARCH Pitfall 1 and by the first rig test (`NETSDK1198` warning + wrong output directory).
- Monitor-restore crash-recovery fallback redesigned around reusing live CCD objects wholesale (`ApplyTopology(Extend)` + reposition) instead of manual struct reconstruction, after three different manual-reconstruction bugs surfaced across three rig round-trips. This trades a small, documented limitation (rotation/scaling now come from `Extend`'s own defaults rather than the stored snapshot) for a fix built on a pattern already proven twice by `Disable()`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rig-discovered, outside plan scope] `win-x64.pubxml` missing MSBuild XML namespace**
- **Found during:** Task 3 checkpoint, first `dotnet publish` attempt on the rig
- **Issue:** `dotnet publish -p:PublishProfile=win-x64` emitted `NETSDK1198: A publish profile with the name 'win-x64' was not found in the project`, and published to the default framework-dependent output path instead of the profile's custom directory — even though the file existed at the correct conventional path (`Properties/PublishProfiles/win-x64.pubxml`). Root cause: the file's root `<Project>` element lacked `xmlns="http://schemas.microsoft.com/developer/msbuild/2003"` (and `ToolsVersion="Current"`), which the SDK's profile-validity check silently requires.
- **Fix:** added the xmlns/ToolsVersion attributes to the root element.
- **Files modified:** `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml`
- **Verification:** rig re-test — publish succeeded, output landed in the profile's custom `bin\publish\win-x64\` directory, no warning.
- **Committed in:** `d46e11a`

**2. [Rig-discovered, outside plan scope] Monitor `Restore()` crash-recovery fallback: missing target signal info**
- **Found during:** Task 3 checkpoint, crash-recovery test (kill process while in Rig Mode, relaunch, Switch to Normal Mode)
- **Issue:** `Restore()`'s fallback reconstruction path (used only when no in-process cache survives a process restart — i.e. exactly the crash-recovery scenario this checkpoint tests, and a path never actually exercised by any prior phase's rig verification) failed CCD validation with `PathChangeException: Invalid paths information.` Root cause, found by reading the vendored `WindowsDisplayAPI` source directly: the `PathTargetInfo` constructor overload used never sets `IsSignalInformationAvailable = true`, so the rebuilt topology supplied full source-mode info (position/resolution) while silently omitting target-mode info — an inconsistent combination `SetDisplayConfig` validation rejects.
- **Fix:** construct an explicit `PathTargetSignalInfo` so target mode info is genuinely supplied.
- **Files modified:** `src/RigToggle.Windows/WindowsMonitorController.cs`
- **Verification:** rig re-test — this specific validation error stopped recurring (confirmed by new diagnostic fields `sigInfo=True modeInfo=True` in the next failure's error message), though a second, distinct bug was then exposed.
- **Committed in:** `6f86120`

**3. [Rig-discovered, outside plan scope] Monitor `Restore()` crash-recovery fallback: active-target source collision**
- **Found during:** Task 3 checkpoint, same crash-recovery retest immediately after fix #2
- **Issue:** With target signal info now supplied, CCD validation still failed — diagnostics revealed both the currently-active survivor monitor and the inactive monitor being restored were assigned the same `source=0`. The existing `AssignSource` collision-avoidance logic correctly reserved active sources before assigning the inactive target a free one, but for the *active* target it trusted a source value read from `GetAllPaths()` — the same query already documented elsewhere in this method as unreliable for inactive targets, and empirically also unreliable for the active one.
- **Fix:** built a device-path → source lookup from `GetActivePaths()` (the only query proven reliable) and used it for the active target's "own source" instead of the `GetAllPaths()`-derived value.
- **Files modified:** `src/RigToggle.Windows/WindowsMonitorController.cs`
- **Verification:** rig re-test — sources were now correctly distinct (`source=0`/`source=1`, no collision), but a third, still-unexplained "Invalid paths information" failure occurred.
- **Committed in:** `8fc6cd0`

**4. [Rig-discovered, outside plan scope] Monitor `Restore()` crash-recovery fallback: replaced manual reconstruction entirely**
- **Found during:** Task 3 checkpoint, third rig retest — same generic CCD validation error persisted even with signal info and source assignment both individually correct
- **Issue:** Three separate, confirmed bugs across three rig round-trips in the same manual-reconstruction code path (source collision, missing signal info, a second source-collision variant) indicated a structural problem with hand-building `PathTargetInfo`/`PathInfo` from stored primitives, not one more missing field.
- **Fix:** replaced the entire manual reconstruction with a two-step approach reusing `Disable()`'s already-proven pattern: (1) `PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend)` — a single built-in CCD topology switch with no manually-supplied structs, bringing the disabled monitor back to some active state; (2) re-query `GetActivePaths()` and reuse each target's real `TargetsInfo` array unchanged, touching only `Position` to reproduce the exact stored layout — mirroring `Disable()`'s "reuse real active TargetsInfo, only touch Position" idiom instead of rebuilding target/signal/source info by hand. `AssignSource`/`CopyOutputTechnology` (fixes #2/#3's machinery) are no longer called by `Restore()` but remain defined and unit-tested rather than deleted mid-incident.
- **Known limitation (accepted):** rotation/scaling now come from `Extend`'s own defaults rather than the stored snapshot, since `TargetsInfo` is reused unchanged. Acceptable because `Extend` defaults to unrotated for the near-universal case, and the existing verify-and-throw logic in `Restore()` does not check rotation either (matching prior scope, not a new gap).
- **Files modified:** `src/RigToggle.Windows/WindowsMonitorController.cs`
- **Verification:** rig re-test — full crash-recovery scenario (kill process while in Rig Mode, relaunch, Switch to Normal Mode) confirmed working by the user on the actual hardware, including after a subsequent forced-close retest.
- **Committed in:** `c02cfb6`

---

**Total deviations:** 4 auto-fixed, all rig-discovered bugs found only by actually exercising this plan's own Task 3 checkpoint (crash-recovery restore had never been tested end-to-end before, in any prior phase).
**Impact on plan:** Deviation #1 was packaging-scope (in line with this plan's own objective). Deviations #2-4 are all in `src/RigToggle.Windows/WindowsMonitorController.cs`, a file built in Phase 4 and not originally in this plan's `files_modified` — necessary because Phase 5's CORE-05 checkpoint is the first thing in this project to actually exercise the crash-recovery restore path, and it was genuinely broken. No scope creep beyond what was required to make CORE-05's own acceptance criterion (crash recovery) actually true.

## Issues Encountered

Beyond the four fixes above: mid-checkpoint, the user's physical monitor was left in an inconsistent state after one failed restore attempt combined with a manual GPU driver restart (external recovery action, not app behavior) — this temporarily confounded debugging (a subsequent test showed both monitors reporting the same source, which was correctly diagnosed as driver-level state corruption from the manual intervention rather than a new code bug). Resolved by having the user fully reboot Windows and clear the app's stale `state.json` before retesting from a clean baseline.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

Phase 5 is the final phase in the v1.0 milestone roadmap (Phases 1-5, all now complete). The complete toggle (monitor + audio + app, both directions), partial-failure reporting, crash-recovery mode detection, and standalone .exe packaging are all rig-verified working. No blockers for milestone completion.

---
*Phase: 05-orchestration-full-toggle-packaging*
*Completed: 2026-07-25*
