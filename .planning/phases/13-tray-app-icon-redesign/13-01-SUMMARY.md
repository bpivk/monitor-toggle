---
phase: 13-tray-app-icon-redesign
plan: 01
subsystem: ui
tags: [gdi+, system.drawing, ico, winforms, icon-generation, wine]

# Dependency graph
requires:
  - phase: 12-theme-infrastructure-live-theme-following
    provides: Established WinForms/theming conventions this phase's icon work sits alongside (independent subsystem, no direct code dependency)
provides:
  - "RigToggle.IconGen console project (complete, executed, verified): IconWriter.cs hand-rolled multi-frame ICO writer (BMP-in-ICO + PNG-in-ICO for 256px frames), IconGeometry.cs GDI+ silhouette drawing for normal/rig/app icons, Program.cs generation + atomic-write + round-trip-verification wiring"
  - "Regenerated src/RigToggle.App/Resources/normal.ico and rig.ico (4 frames each, 16/20/24/32px) and new app.ico (6 frames, 16/20/24/32/48/256px), all round-trip verified"
affects: [13-02-PLAN.md (ApplicationIcon wiring + MainForm DPI fix can now proceed, app.ico exists), 13-03-PLAN.md (rig-checkpoint human visual verification of the actual regenerated icons)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dev-time-only console generator project (OutputType=Exe, UseWindowsForms=true for zero-new-package GDI+ access, no ProjectReference/PackageReference, never referenced by RigToggle.App) isolated from the self-contained publish"
    - "Hand-rolled binary file format writer (ICONDIR/ICONDIRENTRY + BMP-in-ICO for small frames, PNG-in-ICO for >=256px frames) for a format the BCL can read but not write"
    - "Redraw-per-target-size GDI+ geometry (fractional coordinates × target size) instead of bitmap-scaling a single master image"
    - "Executing a Windows-only (net10.0-windows/UseWindowsForms) console tool in a Linux CI/sandbox via self-contained win-x64 publish + Wine, when no native WindowsDesktop.App runtime is available"

key-files:
  created:
    - src/RigToggle.IconGen/RigToggle.IconGen.csproj
    - src/RigToggle.IconGen/IconWriter.cs
    - src/RigToggle.IconGen/IconGeometry.cs
    - src/RigToggle.IconGen/Program.cs
    - src/RigToggle.App/Resources/app.ico
  modified:
    - RigToggle.sln
    - src/RigToggle.App/Resources/normal.ico
    - src/RigToggle.App/Resources/rig.ico

key-decisions:
  - "RigToggle.IconGen registered in RigToggle.sln via `dotnet sln add` (not hand-edited) to get correct GUID/config rows"
  - "Program.cs resolves output paths via Environment.CurrentDirectory (not AppContext.BaseDirectory), since `dotnet run --project <dir>` sets the working directory to the project's own directory, not the build output directory -- required for the '../RigToggle.App/Resources/*.ico' relative path to resolve correctly"
  - "256px ICO frames are PNG-encoded (not raw BMP-in-ICO) per the Vista+ convention -- discovered necessary via round-trip testing, not assumed upfront"
  - "Round-trip verification for >=256px frames bypasses System.Drawing.Icon's sized constructor (confirmed broken for large-frame selection) and instead parses the raw ICO container + decodes the frame's own bytes directly"

patterns-established:
  - "Pattern: dev-time-only generator project isolation (no ProjectReference from the shipped app, no RuntimeIdentifier) -- same isolation principle RigToggle.Windows.Tests already uses for a different reason"
  - "Pattern: verify hand-rolled binary format output against the actual consumer's real read path where possible, and against a from-scratch manual parse where the primary BCL reader (System.Drawing.Icon) has known selection defects at specific sizes"

requirements-completed: []  # Frontmatter requirements marking is the orchestrator's responsibility per this plan's execution contract; ICON-01 through ICON-04 artifacts are now genuinely delivered (see below), orchestrator should mark complete after reviewing this SUMMARY.

duration: ~80min
completed: 2026-08-03
---

# Phase 13 Plan 01: RigToggle.IconGen Scaffold & Icon Geometry Summary

**GDI+ icon generator (hand-rolled ICO writer, UI-SPEC-locked monitor/wheel/app geometry) executed end-to-end via a self-contained win-x64 publish + Wine, producing and round-trip-verifying regenerated `normal.ico`/`rig.ico` and a new `app.ico`.**

## Performance

- **Duration:** ~80 min (includes an initial ~50 min blocked on Linux-sandbox execution, resolved when Wine became available mid-session, plus a subsequent round-trip bug fix and re-verification)
- **Completed:** 2026-08-03T12:02:00Z
- **Tasks:** 3/3 complete and executed
- **Files modified:** 8 (5 created, 3 modified)

## Accomplishments
- `src/RigToggle.IconGen/` scaffolded as a dev-time-only console project (`OutputType=Exe`, `UseWindowsForms=true`, zero `ProjectReference`/`PackageReference`), registered in `RigToggle.sln`, confirmed never referenced by `RigToggle.App.csproj`
- `IconWriter.cs`: complete hand-rolled multi-frame `.ico` writer (`WriteIco`/`EncodeBmpInIco`/`EncodePngInIco`) per 13-RESEARCH.md Pattern 3 -- ICONDIR/ICONDIRENTRY header, the 256px width/height byte=0 guard (Pitfall 3), bottom-up BGRA rows + 32-bit-padded 1bpp AND mask + doubled `biHeight` (Pitfall 4), and PNG encoding for >=256px frames (Pitfall 5, discovered during execution)
- `IconGeometry.cs`: `DrawNormalIcon`/`DrawRigIcon`/`DrawAppIcon`, each redrawing fresh per target size from 13-UI-SPEC.md's locked fractional geometry -- monitor silhouette (screen ∪ neck ∪ base as one `GraphicsPath`), tri-spoke wheel silhouette (rim ∪ hub ∪ 3 spokes at 180/60/300° via `FillMode.Alternate` ring cutout), and the color `app.ico` treatment (`#2D2D30` body + `#005A9E` screen-glass inset, no outline) reusing the exact same monitor fractions as `normal.ico`
- `Program.cs`: full generation wiring -- draws every required frame per icon (tray: 16/20/24/32px, app: +48/256px), packs via `IconWriter.WriteIco`, writes atomically (`.tmp` + `File.Move(overwrite: true)`, mirroring `JsonSettingsStore.Save`'s shape), and round-trip-verifies every embedded size (Assumption A4) -- via the sized `Icon` constructor for <256px frames, and via direct raw-frame decode for >=256px frames (Pitfall 6, discovered during execution)
- **Actually executed the generator** via `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` + `wine RigToggle.IconGen.exe` (run from `src/RigToggle.IconGen`) -- confirmed exit 0, all three files print "round-trip verified" with no FAILED lines
- Regenerated `src/RigToggle.App/Resources/normal.ico` (4 frames, 9622 bytes) and `rig.ico` (4 frames, 9622 bytes); created new `src/RigToggle.App/Resources/app.ico` (6 frames, 20589 bytes) -- verified byte-level via manual ICONDIRENTRY parsing that every frame (including the 256px PNG-encoded one) reports correct effective dimensions
- `dotnet build RigToggle.sln -c Debug` succeeds cleanly with the new project included (0 errors, pre-existing warnings only, unrelated to this plan)

## Task Commits

Each task was committed atomically:

1. **Task 1: Scaffold the RigToggle.IconGen console project and the ICO writer** - `e966633` (feat)
2. **Task 2: Draw the three icon silhouettes per the UI-SPEC geometry contract** - `d3e4a50` (feat)
3. **Task 3: Wire Program.cs to generate all three .ico files** - `8339a92` (feat, code-complete) + `1273cbe` (fix: PNG-encode 256px frames, fix round-trip verification, regenerate all three checked-in `.ico` files after actually executing the generator)

## Files Created/Modified
- `src/RigToggle.IconGen/RigToggle.IconGen.csproj` - Dev-time-only console project, `OutputType=Exe`, `UseWindowsForms=true`, no `ProjectReference`/`PackageReference`
- `src/RigToggle.IconGen/IconWriter.cs` - Hand-rolled `WriteIco`/`EncodeBmpInIco`/`EncodePngInIco` multi-frame `.ico` binary writer
- `src/RigToggle.IconGen/IconGeometry.cs` - `DrawNormalIcon`/`DrawRigIcon`/`DrawAppIcon` GDI+ drawing per UI-SPEC fractions
- `src/RigToggle.IconGen/Program.cs` - Generation entry point: draw → pack → atomic write → round-trip verify (dual strategy per frame size), per icon
- `RigToggle.sln` - `RigToggle.IconGen` project registered via `dotnet sln add`
- `src/RigToggle.App/Resources/normal.ico` - Regenerated (4 frames: 16/20/24/32px, new monitor silhouette geometry)
- `src/RigToggle.App/Resources/rig.ico` - Regenerated (4 frames: 16/20/24/32px, new tri-spoke wheel silhouette geometry)
- `src/RigToggle.App/Resources/app.ico` - New (6 frames: 16/20/24/32/48/256px, color monitor motif)

## Decisions Made
- Output-path resolution uses `Environment.CurrentDirectory` (the project directory, as set by `dotnet run --project <dir>` and by running an exe with that cwd), not `AppContext.BaseDirectory` (the build output directory three levels deeper) -- the latter would resolve `../RigToggle.App/Resources/` incorrectly.
- Spoke geometry implemented via a small per-spoke rotated-rectangle helper (`BuildSpokePath`) rather than a single combined polygon, since GDI+ has no native "3 spokes at fixed angles" primitive -- each spoke computed from hub-edge to inner-rim-edge along its angle, with perpendicular half-width offset, then unioned into the wheel's single `GraphicsPath` per Pattern 2's contiguous-silhouette requirement.
- 256px frames PNG-encoded, not raw BMP-in-ICO (Pitfall 5) -- both a spec convention (Vista+) and empirically required: a raw-BMP 256px frame was tested and confirmed to not reliably decode at its intended size.
- Round-trip verification of >=256px frames uses a from-scratch raw ICO parse + direct frame decode, not `System.Drawing.Icon`'s sized constructor, because that constructor's frame-selection logic was empirically confirmed broken for any large-size request against a 256px entry (Pitfall 6) -- this is a verification-code fix only; it does not affect the real runtime consumer of `app.ico`, which is the Win32 resource compiler/shell (never `System.Drawing.Icon`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] 256px ICO frames require PNG encoding, not raw BMP-in-ICO (newly discovered Pitfall 5)**
- **Found during:** Task 3, first real execution of the generator (via the Wine recipe below)
- **Issue:** `IconWriter.EncodeBmpInIco` originally encoded every frame -- including the 256px one -- as raw uncompressed BMP-in-ICO. The file was byte-correct per the format spec (`ICONDIRENTRY` width/height byte = 0, per Pitfall 3), but the 256px frame did not reliably decode as 256x256.
- **Fix:** `IconWriter.WriteIco` now special-cases frames at or above 256px (`PngFrameThreshold`) to encode via `bitmap.Save(stream, ImageFormat.Png)` (new `EncodePngInIco` method) instead of `EncodeBmpInIco`. All smaller frames are unaffected.
- **Files modified:** `src/RigToggle.IconGen/IconWriter.cs`
- **Verification:** Re-published + re-ran via Wine; `app.ico`'s 256px frame now decodes with the correct embedded PNG `IHDR` reporting 256x256 (independently confirmed via manual byte-level Python parsing of the checked-in file, not just the tool's own claim).
- **Committed in:** `1273cbe`

**2. [Rule 1 - Bug] `System.Drawing.Icon`'s sized constructor cannot select a >=256px frame by request, regardless of encoding (newly discovered Pitfall 6)**
- **Found during:** Task 3, immediately after applying fix #1 -- round-trip verification for `app.ico`'s 256px frame still failed (`loaded icon reports 48x48`) even with correct PNG encoding.
- **Root-caused independently** (did not stop at the initial PNG-encoding hypothesis, which was necessary but not sufficient): wrote a small throwaway diagnostic program (published self-contained win-x64, run under Wine) that requested `new Icon(app.ico, new Size(s,s))` for `s` in `{16,20,24,32,48,64,100,200,256,300,1000}`. Every request >=48 returned the 48px frame -- proving `Icon`'s best-fit frame *selection* logic treats the ICO format's "0 byte means 256" convention (Pitfall 3) as literal `0` during comparison, so the 256px entry can never win against 48px for any target size, however large. Independently confirmed via manual byte parsing that the file itself is correct (ICONDIRENTRY reports `0x0`/256 per spec; the embedded PNG's own `IHDR` genuinely reports `256x256`) -- this is a `System.Drawing.Icon` reader defect, not a writer defect.
- **Fix:** `Program.cs`'s `VerifyRoundTrip` now branches: sizes below 256px keep using the sized `Icon` constructor (empirically confirmed correct for 16-48px); sizes at or above 256px are verified by parsing the raw ICO container directly (`ICONDIR`/`ICONDIRENTRY` table, applying the 0=256 rule ourselves) and decoding that frame's own bytes via `Image.FromStream`, which bypasses `Icon`'s broken selection heuristic entirely.
- **Files modified:** `src/RigToggle.IconGen/Program.cs`
- **Verification:** Re-published + re-ran via Wine; all three files now print "round-trip verified" with exit code 0, no FAILED lines. Independently re-confirmed via a second manual byte-level Python parse of the final checked-in `app.ico`.
- **Impact assessment (why this is safe to ship):** This defect is confined to `System.Drawing.Icon`'s API surface and does not affect any real runtime path in this project -- `app.ico` is consumed exclusively via the `<ApplicationIcon>` MSBuild property (native Win32 resource, read by Explorer/shell/taskbar through DPI-aware shell APIs per 13-RESEARCH.md Pattern 4), never via `System.Drawing.Icon`; `normal.ico`/`rig.ico` (which *are* loaded via `System.Drawing.Icon` in `MainForm.LoadTrayIconsIfNeeded()`) never embed a frame >=256px, so their real runtime usage is unaffected by this defect.
- **Committed in:** `1273cbe`

---

**Total deviations:** 2 auto-fixed (both Rule 1 -- bugs found and fixed during real execution, not planning-stage guesses)
**Impact on plan:** Both fixes were necessary for the generator's own round-trip gate to genuinely pass, not scope creep. No architectural changes; both fixes are localized to `IconWriter.cs`/`Program.cs`.

## Issues Encountered

**Linux sandbox initially could not execute the generator.** Early in this plan's execution, `dotnet run --project src/RigToggle.IconGen` failed because this Linux worktree sandbox had no `Microsoft.WindowsDesktop.App` runtime (confirmed as a pre-existing, whole-repository limitation via `dotnet test src/RigToggle.Windows.Tests`, which failed identically) and no Wine/Mono. Investigated exhaustively (checked `dotnet --list-runtimes`, tested `libgdiplus`, attempted a manual runtimeconfig/assembly workaround that failed with `BadImageFormatException` on ReadyToRun-compiled Windows-only images) before concluding it was unfixable in-session. Mid-session, Wine (`wine64`) became available in the sandbox; the working execution recipe (`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` then `wine ...RigToggle.IconGen.exe` run with `cwd=src/RigToggle.IconGen`) was verified end-to-end, unblocking full execution and the two bug-fixes documented above. All claims in this issue report were independently re-verified (not taken on faith) before proceeding: confirmed the `wine` binary's presence, confirmed the pre-fix `app.ico`'s round-trip failure via a from-scratch throwaway diagnostic, and confirmed the post-fix file's correctness via manual Python-level byte parsing of both the `ICONDIRENTRY` table and the embedded PNG's own `IHDR` chunk.

## Requirements NOT completed

None -- ICON-01 through ICON-04's artifact-delivery half (this plan's scope; the runtime DPI-selection fix and `<ApplicationIcon>` wiring are Plan 13-02's job per this plan's own objective) is now genuinely complete: `normal.ico`/`rig.ico` regenerated with the new UI-SPEC geometry, `app.ico` created with the color monitor motif, all frames round-trip verified. Final human visual sign-off (16px legibility on light/dark taskbars) is still 13-03's job, as originally planned -- not a gap introduced by this plan.

## Known Stubs

None. `normal.ico`, `rig.ico`, and `app.ico` all contain real, UI-SPEC-geometry-derived, round-trip-verified pixel content -- no placeholder/stub data.

## User Setup Required

None. The generator was executed in this session; no further manual step is required to produce the icon artifacts. (Human visual verification on real Windows hardware, per 13-UI-SPEC.md's Scope Notes, remains 13-03's planned job -- unchanged from the original plan, not a new requirement introduced here.)

## Next Phase Readiness

- **13-02 (ApplicationIcon wiring + MainForm DPI fix) is unblocked** -- `app.ico` now exists with the correct 6-frame content.
- **13-03 (rig-checkpoint human visual verification)** can proceed against the actual regenerated icons produced in this plan.
- No outstanding gaps from this plan's scope.

---
*Phase: 13-tray-app-icon-redesign*
*Completed: 2026-08-03*

## Self-Check: PASSED

- FOUND: src/RigToggle.IconGen/RigToggle.IconGen.csproj
- FOUND: src/RigToggle.IconGen/IconWriter.cs
- FOUND: src/RigToggle.IconGen/IconGeometry.cs
- FOUND: src/RigToggle.IconGen/Program.cs
- FOUND: src/RigToggle.App/Resources/normal.ico (4 frames, verified via manual ICONDIRENTRY parse)
- FOUND: src/RigToggle.App/Resources/rig.ico (4 frames, verified via manual ICONDIRENTRY parse)
- FOUND: src/RigToggle.App/Resources/app.ico (6 frames including PNG-encoded 256px, verified via manual ICONDIRENTRY + PNG IHDR parse)
- FOUND: commit e966633 (Task 1)
- FOUND: commit d3e4a50 (Task 2)
- FOUND: commit 8339a92 (Task 3, code)
- FOUND: commit 1273cbe (Task 3, fix + regenerated assets)
- `dotnet build RigToggle.sln -c Debug` verified passing (0 errors) after all commits
- `wine RigToggle.IconGen.exe` (self-contained win-x64 publish) verified exit 0 with "round-trip verified" printed for all 3 files, no FAILED lines
