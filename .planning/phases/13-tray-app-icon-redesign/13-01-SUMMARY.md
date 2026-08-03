---
phase: 13-tray-app-icon-redesign
plan: 01
subsystem: ui
tags: [gdi+, system.drawing, ico, winforms, icon-generation]

# Dependency graph
requires:
  - phase: 12-theme-infrastructure-live-theme-following
    provides: Established WinForms/theming conventions this phase's icon work sits alongside (independent subsystem, no direct code dependency)
provides:
  - "RigToggle.IconGen console project (source-complete, build-verified): IconWriter.cs hand-rolled multi-frame ICO writer, IconGeometry.cs GDI+ silhouette drawing for normal/rig/app icons, Program.cs generation + atomic-write + round-trip-verification wiring"
affects: [13-02-PLAN.md (ApplicationIcon wiring + MainForm DPI fix depends on app.ico existing), 13-03-PLAN.md (rig-checkpoint human visual verification depends on final regenerated icons)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dev-time-only console generator project (OutputType=Exe, UseWindowsForms=true for zero-new-package GDI+ access, no ProjectReference/PackageReference, never referenced by RigToggle.App) isolated from the self-contained publish"
    - "Hand-rolled binary file format writer (ICONDIR/ICONDIRENTRY + BMP-in-ICO) for a format the BCL can read but not write"
    - "Redraw-per-target-size GDI+ geometry (fractional coordinates × target size) instead of bitmap-scaling a single master image"

key-files:
  created:
    - src/RigToggle.IconGen/RigToggle.IconGen.csproj
    - src/RigToggle.IconGen/IconWriter.cs
    - src/RigToggle.IconGen/IconGeometry.cs
    - src/RigToggle.IconGen/Program.cs
  modified:
    - RigToggle.sln

key-decisions:
  - "RigToggle.IconGen registered in RigToggle.sln via `dotnet sln add` (not hand-edited) to get correct GUID/config rows"
  - "Program.cs resolves output paths via Environment.CurrentDirectory (not AppContext.BaseDirectory), since `dotnet run --project <dir>` sets the working directory to the project's own directory, not the build output directory -- required for the '../RigToggle.App/Resources/*.ico' relative path to resolve correctly"

patterns-established:
  - "Pattern: dev-time-only generator project isolation (no ProjectReference from the shipped app, no RuntimeIdentifier) -- same isolation principle RigToggle.Windows.Tests already uses for a different reason"

requirements-completed: []  # See 'Requirements NOT completed' below -- ICON-01 through ICON-04 remain open pending Windows execution.

duration: ~50min
completed: 2026-08-03
---

# Phase 13 Plan 01: RigToggle.IconGen Scaffold & Icon Geometry Summary

**Complete, build-verified GDI+ icon-generation source code (hand-rolled ICO writer, UI-SPEC-locked monitor/wheel/app geometry, atomic-write + round-trip-verification wiring) — but the actual `.ico` regeneration could not run in this Linux execution sandbox, which has no way to execute any `Microsoft.WindowsDesktop.App`-dependent assembly.**

## Performance

- **Duration:** ~50 min
- **Completed:** 2026-08-03T11:50:01Z
- **Tasks:** 3/3 code-complete; Task 3's runtime execution step blocked (see Deviations)
- **Files modified:** 5 (4 created, 1 modified)

## Accomplishments
- `src/RigToggle.IconGen/` scaffolded as a dev-time-only console project (`OutputType=Exe`, `UseWindowsForms=true`, zero `ProjectReference`/`PackageReference`), registered in `RigToggle.sln`, confirmed never referenced by `RigToggle.App.csproj`
- `IconWriter.cs`: complete hand-rolled multi-frame `.ico` writer (`WriteIco`/`EncodeBmpInIco`) per 13-RESEARCH.md Pattern 3 -- ICONDIR/ICONDIRENTRY header, the 256px width/height byte=0 guard (Pitfall 3), bottom-up BGRA rows + 32-bit-padded 1bpp AND mask + doubled `biHeight` (Pitfall 4)
- `IconGeometry.cs`: `DrawNormalIcon`/`DrawRigIcon`/`DrawAppIcon`, each redrawing fresh per target size from 13-UI-SPEC.md's locked fractional geometry -- monitor silhouette (screen ∪ neck ∪ base as one `GraphicsPath`), tri-spoke wheel silhouette (rim ∪ hub ∪ 3 spokes at 180/60/300° via `FillMode.Alternate` ring cutout), and the color `app.ico` treatment (`#2D2D30` body + `#005A9E` screen-glass inset, no outline) reusing the exact same monitor fractions as `normal.ico`
- `Program.cs`: full generation wiring -- draws every required frame per icon (tray: 16/20/24/32px, app: +48/256px), packs via `IconWriter.WriteIco`, writes atomically (`.tmp` + `File.Move(overwrite: true)`, mirroring `JsonSettingsStore.Save`'s shape), and round-trip-verifies every embedded size via `new System.Drawing.Icon(path, new Size(s, s))` per 13-RESEARCH.md Assumption A4
- `dotnet build RigToggle.sln -c Debug` succeeds cleanly with the new project included (0 errors, pre-existing warnings only, unrelated to this plan)

## Task Commits

Each task was committed atomically:

1. **Task 1: Scaffold the RigToggle.IconGen console project and the ICO writer** - `e966633` (feat)
2. **Task 2: Draw the three icon silhouettes per the UI-SPEC geometry contract** - `d3e4a50` (feat)
3. **Task 3: Wire Program.cs to generate all three .ico files** - `8339a92` (feat, code-complete; execution blocked, see Deviations)

## Files Created/Modified
- `src/RigToggle.IconGen/RigToggle.IconGen.csproj` - Dev-time-only console project, `OutputType=Exe`, `UseWindowsForms=true`, no `ProjectReference`/`PackageReference`
- `src/RigToggle.IconGen/IconWriter.cs` - Hand-rolled `WriteIco`/`EncodeBmpInIco` multi-frame `.ico` binary writer
- `src/RigToggle.IconGen/IconGeometry.cs` - `DrawNormalIcon`/`DrawRigIcon`/`DrawAppIcon` GDI+ drawing per UI-SPEC fractions
- `src/RigToggle.IconGen/Program.cs` - Generation entry point: draw → pack → atomic write → round-trip verify, per icon
- `RigToggle.sln` - `RigToggle.IconGen` project registered via `dotnet sln add`

## Decisions Made
- Output-path resolution uses `Environment.CurrentDirectory` (the project directory, as set by `dotnet run --project <dir>`), not `AppContext.BaseDirectory` (the build output directory three levels deeper) -- the latter would resolve `../RigToggle.App/Resources/` incorrectly.
- Spoke geometry implemented via a small per-spoke rotated-rectangle helper (`BuildSpokePath`) rather than a single combined polygon, since GDI+ has no native "3 spokes at fixed angles" primitive -- each spoke computed from hub-edge to inner-rim-edge along its angle, with perpendicular half-width offset, then unioned into the wheel's single `GraphicsPath` per Pattern 2's contiguous-silhouette requirement.

## Deviations from Plan

### Blocking Issue Investigated, Confirmed Unfixable In This Sandbox (not auto-fixed — infrastructure limitation, no code-level fix exists)

**1. [Rule 3 boundary case] `dotnet run --project src/RigToggle.IconGen` cannot execute in this Linux worktree sandbox**

- **Found during:** Task 3 verification step (`dotnet run --project src/RigToggle.IconGen/RigToggle.IconGen.csproj`)
- **Issue:** The command fails immediately with `System.IO.FileNotFoundException`-class host error: `Framework: 'Microsoft.WindowsDesktop.App', version '10.0.0' ... No frameworks were found.` This is not specific to the new project or a code bug in this plan's deliverables.
- **Investigation performed (exhaustive, per the executor prompt's explicit instruction to investigate before assuming unfixable):**
  1. `dotnet --list-runtimes` confirms only `Microsoft.AspNetCore.App` and `Microsoft.NETCore.App` are installed -- no `Microsoft.WindowsDesktop.App` shared runtime exists on this machine, and this framework is never published for `linux-x64` by Microsoft at all (WinForms/WPF are Windows-only; `EnableWindowsTargeting=true` only bypasses the SDK's *build-time* TFM guard, it does not provide or fake a runtime).
  2. Confirmed this is a **pre-existing, whole-repository** limitation, not introduced by this plan: `dotnet test src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj` (an existing project that predates Phase 13) fails with the **identical** `Microsoft.WindowsDesktop.App` host error.
  3. Installed `libgdiplus` (system package, apt) to test whether the GDI+ native dependency specifically was the blocker -- it was not; the runtime host refuses to start before ever reaching GDI+ code, because the TFM itself (`net10.0-windows` + `UseWindowsForms=true`) declares a hard `Microsoft.WindowsDesktop.App` framework dependency in the generated `runtimeconfig.json`.
  4. Attempted a manual workaround: stripped the `Microsoft.WindowsDesktop.App` framework entry from a *copy* of `runtimeconfig.json` (never modified the checked-in project), deleted the accompanying `.deps.json` so the CLR would probe the app directory, and copied the actual `Microsoft.WindowsDesktop.App.Runtime.win-x64` NuGet-cached assemblies (`System.Drawing.Common.dll`, `System.Private.Windows.GdiPlus.dll`, etc.) alongside the app DLL. Result: `System.BadImageFormatException` -- those assemblies are ReadyToRun-compiled native Windows PE images, not portable MSIL, and cannot be loaded by the Linux CoreCLR host at all.
  5. Checked for a Wine/Mono fallback -- neither is installed in this sandbox.
- **Conclusion:** This is a categorical, unfixable-in-this-sandbox platform limitation (confirmed via 4 independent verification angles above), not a Rule 1/2/3 auto-fixable code defect. It matches 13-RESEARCH.md's own "Environment Availability" section, which flagged this exact risk during research: *"Actual .ico file generation and visual verification cannot happen in this research session ... actual generation/build/test must happen on a machine with the .NET 10 SDK installed."*
- **What was NOT done as a workaround, and why:** Did not hand-fabricate `.ico` byte content via an alternate (non-GDI+) rasterizer to force a "passing" result. Doing so would violate this phase's locked decision D-03 ("icons are procedurally drawn in code via GDI+/System.Drawing ... no external design tool, no new asset pipeline") for the artifacts that would actually ship, and risks producing visually-different output from what the real `RigToggle.IconGen` tool renders on Windows -- worse than leaving the gap explicit.
- **Files affected:** None modified as a result (existing `normal.ico`/`rig.ico` left byte-for-byte unchanged; no `app.ico` created).
- **Verification:** `dotnet build RigToggle.sln -c Debug` succeeds (0 errors) confirming the generator's source code is syntactically and semantically correct and compiles cleanly alongside the rest of the solution. Execution itself is the only step that could not be verified here.
- **Committed in:** `8339a92` (Task 3 commit message documents this in full)

---

**Total deviations:** 1 (infrastructure/environment limitation, investigated exhaustively, not auto-fixable)
**Impact on plan:** All Task 1-3 *code* deliverables are complete, spec-compliant, and build-verified. The plan's runtime deliverables (regenerated `normal.ico`/`rig.ico`, new `app.ico`, round-trip verification) are **not yet produced** -- this requires running `dotnet run --project src/RigToggle.IconGen` on an actual Windows machine or any environment with the `Microsoft.WindowsDesktop.App` runtime installed. See "Requirements NOT completed" below.

## Issues Encountered
See Deviations above -- the `Microsoft.WindowsDesktop.App` execution-environment gap is the only issue encountered, and it is fully investigated and documented there rather than repeated here.

## Requirements NOT completed

**ICON-01, ICON-02, ICON-03, ICON-04 remain open** (not marked complete in REQUIREMENTS.md by this plan). The generator code that will satisfy them is written and build-verified, but the actual artifacts (regenerated `normal.ico`, `rig.ico`, new `app.ico`, all round-trip-verified) do not yet exist because `dotnet run --project src/RigToggle.IconGen` must execute on a machine with the .NET 10 Windows Desktop runtime -- unavailable in this Linux worktree sandbox. The orchestrator/user should run this command on a Windows (or otherwise WindowsDesktop.App-capable) machine before Phase 13's icon requirements can be marked complete; `13-03-PLAN.md`'s rig-checkpoint human-verify step already anticipates this exact hand-off point per 13-RESEARCH.md.

## Known Stubs

- `src/RigToggle.App/Resources/normal.ico` and `rig.ico` are unchanged from their pre-Phase-13 content (old design) -- not yet regenerated with the new UI-SPEC geometry.
- `src/RigToggle.App/Resources/app.ico` does not exist yet -- Plan 13-02's `<ApplicationIcon>` MSBuild wiring will need this file to exist before it can build/publish successfully on Windows.

## User Setup Required

None from an external-service standpoint, but a **manual execution step is required** before this phase's icon artifacts exist:

1. On a Windows machine (or any machine with the .NET 10 SDK + Windows Desktop runtime installed), from the repo root run: `dotnet run --project src/RigToggle.IconGen`
2. Confirm it exits 0 and prints a "round-trip verified" line for each of `normal.ico`, `rig.ico`, `app.ico`.
3. Commit the regenerated `src/RigToggle.App/Resources/normal.ico`, `rig.ico`, and new `app.ico`.

## Next Phase Readiness

- **13-02 (ApplicationIcon wiring + MainForm DPI fix) is blocked** on `app.ico` existing -- it does not yet, per the gap above. 13-02's planner/executor should either wait for the manual generation step above or treat generating the icons as a prerequisite first sub-task.
- **13-03 (rig-checkpoint human visual verification)** already anticipates running on real Windows hardware, so its execution is unaffected in kind -- but it now also needs to perform the icon-generation step (or confirm it was already done) before visual verification can proceed, since the icons this plan intended to produce don't exist yet.
- The generator's source code itself is complete and does not need further changes to unblock this -- only an actual Windows-capable execution environment is needed.

---
*Phase: 13-tray-app-icon-redesign*
*Completed: 2026-08-03*

## Self-Check: PASSED

- FOUND: src/RigToggle.IconGen/RigToggle.IconGen.csproj
- FOUND: src/RigToggle.IconGen/IconWriter.cs
- FOUND: src/RigToggle.IconGen/IconGeometry.cs
- FOUND: src/RigToggle.IconGen/Program.cs
- FOUND: commit e966633 (Task 1)
- FOUND: commit d3e4a50 (Task 2)
- FOUND: commit 8339a92 (Task 3)
- `dotnet build RigToggle.sln -c Debug` verified passing (0 errors) after all task commits
