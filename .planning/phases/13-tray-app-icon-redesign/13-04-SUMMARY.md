---
phase: 13-tray-app-icon-redesign
plan: 04
subsystem: ui
tags: [gdi+, system.drawing, ico, winforms, icon-generation, wine, gap-closure]

# Dependency graph
requires:
  - phase: 13-tray-app-icon-redesign (13-01/13-02/13-03)
    provides: RigToggle.IconGen console project, IconGeometry.cs drawing code, the originally-shipped (seam-defective) normal.ico/rig.ico/app.ico, and 13-REVIEW.md's CR-01 root-cause analysis this plan implements the fix for
provides:
  - "Stroke-then-fill outline compositing in IconGeometry.cs (DrawNormalIcon/DrawRigIcon) eliminating the CR-01 interior-seam-line defect by construction"
  - "VerifyNoInteriorArtifacts pixel diagnostic in Program.cs: decodes the 16px frame, flood-fills from border to find enclosed transparent components, fails the generator run on any component <=2px -- backstops the existing byte-level round-trip gate with genuine pixel-content verification"
  - "WR-01: TraySizes expanded to 8 frames (16/20/24/28/32/36/40/48px) covering 100-300% DPI scale factors"
  - "WR-02: IconWriter.PngFrameThreshold promoted to internal, single shared source of truth (Program.cs's duplicate constant removed)"
  - "Regenerated normal.ico/rig.ico (8 frames each) and re-emitted app.ico, all passing both the round-trip and interior-artifact gates"
affects: [13-tray-app-icon-redesign (this plan's own Task 3 rig checkpoint, pending), any future icon-geometry work referencing IconGeometry.cs's rig.ico constants]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Stroke-then-fill outline compositing (draw Pen stroke UNDER the fill, not over it) as the correct way to render a merged-silhouette union outline from a multi-sub-figure GraphicsPath, since GraphicsPath.DrawPath cannot compute a true union contour on its own"
    - "Alpha-based interior-artifact pixel diagnostic (border flood-fill + 4-connected component analysis) as a genuine pixel-content verification gate, distinct from and complementary to byte-level ICO-container round-trip verification"
    - "Per-icon outline pen width tuning (doubled for DrawNormalIcon, 1x for DrawRigIcon) -- doubling is only required to preserve full-thickness trace on true contour/gap-facing edges (single-side-filled); interior seams (filled on both sides) are fully hidden by the fill regardless of pen width, so a uniform doubling policy across icons with different internal geometric density is not always correct"

key-files:
  created: []
  modified:
    - src/RigToggle.IconGen/IconGeometry.cs
    - src/RigToggle.IconGen/Program.cs
    - src/RigToggle.IconGen/IconWriter.cs
    - src/RigToggle.App/Resources/normal.ico
    - src/RigToggle.App/Resources/rig.ico

key-decisions:
  - "DrawRigIcon's outline pen reverted to 1x OutlineWidth(size), NOT the 2x doubling Task 1 originally locked for both tray icons -- empirically confirmed via the actual Wine-rendered pixel diagnostic (not guessed) that doubling every one of rig.ico's six nearby stroked boundaries (hub circle, inner-cutout circle, 3 spokes' long edges) packed into a narrow 16px band compounds via sequential alpha-compositing and swallows the intended between-spoke transparent negative space almost entirely, contrary to UI-SPEC governing rule 4. Interior seams remain fully hidden at 1x because they are always filled on both sides regardless of pen width -- doubling only matters for true contour/gap-facing edges (filled on one side only)."
  - "rig.ico geometry fractions nudged (InnerCutoutRadius 0.26->0.275, HubRadius 0.12->0.065, SpokeHalfWidth 0.07->0.05) per 13-UI-SPEC.md's own Scope Notes allowance to adjust proportions when a shape reads ambiguously/artifacts at 16px -- widens the hub-to-inner-rim band from ~2.2px to ~3.4px raw while keeping both ring thickness (~2.0px) and hub diameter (~2.08px) at/above the >=2px-at-16px minimum feature rule"
  - "DrawNormalIcon's outline pen stays at 2x doubling (unchanged from Task 1) -- its geometry (screen/neck/base) has far fewer nearby stroked boundaries and already passes the interior-artifact diagnostic with 0 enclosed components, no tuning needed"

requirements-completed: [ICON-01, ICON-02]  # Artifact-delivery half only -- final human rig sign-off (Task 3) still pending, see below

# Metrics
duration: ~100min
completed: 2026-08-03
---

# Phase 13 Plan 04: CR-01 Gap Closure (Tray Icon Seam Artifacts) Summary

**Rewrote IconGeometry.cs's outline compositing to stroke-then-fill (eliminating interior seam lines by construction), added a pixel-level interior-artifact diagnostic to Program.cs that genuinely verifies clean silhouettes (not just ICO byte-container validity), and tuned rig.ico's outline-pen-width and radial geometry after the diagnostic caught a real defect the naive doubled-outline fix introduced. Tasks 1-2 complete and committed; Task 3 (blocking human rig re-check) requires real Windows 11 hardware unavailable in this Linux worktree sandbox -- plan execution is PAUSED at this checkpoint.**

## Performance

- **Duration:** ~100 min (includes substantial empirical iteration to root-cause and fix a rig.ico-specific pixel diagnostic failure not anticipated by the plan's exact proposed values)
- **Started:** 2026-08-03T15:40:00Z (approx)
- **Completed (Tasks 1-2):** 2026-08-03T16:12:04Z
- **Tasks:** 2/3 complete (Task 3 is a blocking checkpoint, not executable from this environment)
- **Files modified:** 5 (3 source files, 2 regenerated .ico assets; app.ico re-emitted but byte-identical since DrawAppIcon is untouched)

## Accomplishments

- **Task 1 (CR-01 core fix):** Rewrote `DrawNormalIcon`/`DrawRigIcon` in `IconGeometry.cs` to draw the black outline stroke FIRST (under), then the white fill ON TOP, instead of the original fill-then-stroke order. Since `GraphicsPath.DrawPath` strokes every sub-figure's boundary independently (never computing a true merged union contour), the original fill-then-stroke order left every interior seam (screen<->neck, neck<->base, hub<->spoke, spoke<->inner-rim, outer-rim<->inner-rim) visible as an unwanted black line. With stroke-then-fill, the anti-aliased white fill exactly recomputes and overpaints the correct union region, erasing every interior seam by construction while leaving only the true outer contour and genuine inner-hole/gap edges visible. Also fixed IN-01 (undisposed temporary spoke `GraphicsPath` instances in `BuildWheelPath`, now wrapped in `using var spoke`).
- **Task 2 (diagnostic + WR-01/WR-02 + regeneration):** Added `VerifyNoInteriorArtifacts` to `Program.cs` -- decodes the 16px frame via `new Icon(path, new Size(16,16)).ToBitmap()` (BMP-in-ICO, not PNG, per the existing Pitfall 5 note), flood-fills transparent pixels from every border pixel to find exterior-reachable transparent regions, groups any remaining "enclosed" transparent pixels into 4-connected components, and fails the run if any component has area <=2px. Always prints an ASCII grid (`#`=opaque, `.`=transparent) plus the component count/areas for human inspection. Folded in WR-01 (TraySizes expanded to 8 frames covering 100-300% DPI) and WR-02 (`IconWriter.PngFrameThreshold` promoted to `internal`, Program.cs's duplicate constant removed).
- **Diagnostic caught a real defect, not a false positive:** the first regeneration run (with rig.ico's outline doubled per Task 1's committed design) failed with a single <=2px enclosed transparent pixel near the wheel's hub. Root-caused via direct empirical testing (not hand-waving) that this was caused by rig.ico's six nearby stroked boundaries (hub circle, inner-cutout circle, both long edges of each of 3 spokes) compressed into a narrow 16px radial band -- doubling every one of those strokes compounds via sequential alpha-compositing and very nearly erases the intended transparent negative space entirely (the "between-spoke gaps" UI-SPEC governing rule 4 requires stay transparent), which manifested as both the specific dropout pixel and, more importantly, a near-total loss of the wheel's visible ring-hole/spoke-gap detail. Confirmed causation experimentally (isolated pen-width vs. geometry as separate variables across multiple Wine-rendered test runs) before landing on the fix: revert `DrawRigIcon`'s pen to 1x `OutlineWidth(size)` (interior seams stay fully hidden regardless of pen width since they are always filled on both sides; only true contour/gap-facing edges need the extra doubled margin) plus a modest widening of the hub-to-inner-rim band (`InnerCutoutRadius` 0.26->0.275, `HubRadius` 0.12->0.065, `SpokeHalfWidth` 0.07->0.05), both changes explicitly sanctioned by 13-UI-SPEC.md's Scope Notes ("adjust proportions ... if a shape reads ambiguously at 16px").
- **Final verification (both gates, all three icons):** `dotnet run --project src/RigToggle.IconGen` (via the Wine self-contained-publish recipe) exits 0. `normal.ico`: 0 enclosed transparent components. `rig.ico`: exactly 3 enclosed transparent components (areas 6, 6, 7px -- the three intended between-spoke gaps, comfortably above the <=2px failure threshold, confirming the wheel's negative space is genuinely present again, not just alpha-technically-passing). `app.ico`: round-trip verified, byte-identical to before (untouched `DrawAppIcon`).
- `dotnet build RigToggle.sln -c Release` and `dotnet publish src/RigToggle.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` both succeed with the regenerated icons embedded, ready for the Task 3 rig checkpoint.

## Task Commits

Each completed task was committed atomically:

1. **Task 1: Rewrite DrawNormalIcon/DrawRigIcon outline compositing so only the union contour is stroked (CR-01 core fix)** - `5979281` (fix)
2. **Task 2: Add the interior-artifact pixel diagnostic, fold WR-01/WR-02, and regenerate + republish** - `f3c4162` (feat)

**Task 3: Rig re-check (checkpoint:human-verify, gate="blocking")** - NOT executed. Requires launching the freshly republished single-file exe on real Windows 11 rig hardware to visually confirm both tray icons render as clean single silhouettes on both light and dark taskbars. This Linux worktree sandbox has no Windows GUI to perform this verification -- see "Next Phase Readiness" below.

## Files Created/Modified

- `src/RigToggle.IconGen/IconGeometry.cs` - Stroke-then-fill compositing for `DrawNormalIcon`/`DrawRigIcon`; rig.ico geometry constants nudged; `DrawRigIcon`'s outline pen reverted to 1x; IN-01 spoke-path disposal fix
- `src/RigToggle.IconGen/Program.cs` - New `VerifyNoInteriorArtifacts` + `Neighbors4` helper; `TraySizes` expanded (WR-01); `LargeFrameVerificationThreshold` removed in favor of `IconWriter.PngFrameThreshold` (WR-02)
- `src/RigToggle.IconGen/IconWriter.cs` - `PngFrameThreshold` promoted from `private` to `internal` (WR-02)
- `src/RigToggle.App/Resources/normal.ico` - Regenerated, 8 frames (16-48px), stroke-then-fill outline, 0 interior artifacts
- `src/RigToggle.App/Resources/rig.ico` - Regenerated, 8 frames (16-48px), stroke-then-fill outline with tuned pen width + geometry, 0 interior artifacts <=2px (3 legitimate gap components remain, 6-7px each)

## Decisions Made

See `key-decisions` in frontmatter for full rationale. Summary:
- `DrawRigIcon`'s outline pen uses 1x `OutlineWidth(size)`, not the 2x doubling Task 1 originally specified uniformly for both tray icons -- empirically justified deviation from Task 1's literal acceptance criteria, informed by evidence Task 1 itself could not have anticipated (the pixel diagnostic that surfaces this class of defect didn't exist until Task 2).
- `DrawNormalIcon` keeps 2x doubling unchanged -- already correct, no evidence of any problem there.
- rig.ico's `InnerCutoutRadius`/`HubRadius`/`SpokeHalfWidth` fractions nudged slightly (still well within 13-UI-SPEC.md's Scope Notes allowance and the >=2px-at-16px minimum feature rule for both ring thickness and hub diameter).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `DrawRigIcon`'s doubled outline pen (as Task 1 specified) swallows rig.ico's intended negative space at 16px**
- **Found during:** Task 2, first real execution of the regenerated interior-artifact diagnostic against rig.ico
- **Issue:** Task 1's acceptance criteria required doubling the outline pen width (`2f * OutlineWidth(size)`) uniformly for both `DrawNormalIcon` and `DrawRigIcon`, reasoned as necessary to preserve a full ~1px visible trace on the true outer contour after the fill overpaints the inner half. For rig.ico specifically, this same doubling applied to ALL of the wheel's stroked boundaries (hub circle, inner-cutout circle, and both long edges of each of 3 spokes) compressed into a narrow 16px radial band, and the cumulative alpha-compositing effect of six overlapping doubled-width strokes very nearly erased the entire hub-to-inner-rim transparent band, producing both a specific <=2px enclosed-pixel diagnostic failure and, more seriously, a near-total loss of the wheel's visible ring-hole/spoke-gap negative space (contrary to 13-UI-SPEC.md's governing rule 4).
- **Root-caused independently, not guessed:** ran a sequence of controlled empirical tests via the real Wine-rendered pixel diagnostic, isolating pen width and radial geometry as separate variables (confirmed pen width was the dominant factor by testing 1x/1.5x/2x at fixed geometry; confirmed geometry alone was insufficient by testing widened bands at the original 2x pen and finding zero improvement) before landing on the combined fix.
- **Fix:** `DrawRigIcon`'s outline pen reverted to 1x `OutlineWidth(size)` (not doubled) -- justified because interior seams (the actual subject of CR-01) are always filled on both sides regardless of pen width, so the fill still fully overpaints them at 1x; doubling is only structurally necessary for true contour/gap-facing edges (filled on one side only), and rig.ico's outer rim / inner-ring-hole edges still show a visible (if slightly thinner than normal.ico's) trace at 1x. Additionally widened the hub-to-inner-rim band (`InnerCutoutRadius` 0.26->0.275, `HubRadius` 0.12->0.065, `SpokeHalfWidth` 0.07->0.05) per 13-UI-SPEC.md's Scope Notes allowance, while keeping ring thickness and hub diameter at/above the 2px-at-16px minimum feature rule.
- **Files modified:** `src/RigToggle.IconGen/IconGeometry.cs`
- **Verification:** Re-published + re-ran via Wine; `rig.ico`'s 16px frame now shows exactly 3 enclosed transparent components (areas 6, 6, 7px -- the intended between-spoke gaps), 0 components <=2px. `normal.ico` unaffected (still 0 components, unchanged 2x pen). Both `dotnet build RigToggle.sln -c Release` and the self-contained publish succeed with the regenerated assets.
- **Committed in:** `f3c4162` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 -- a real bug in Task 1's uniform-doubling assumption, discovered and fixed via the very diagnostic Task 2 built to catch this class of defect, not planning-stage guesswork). This deviation from Task 1's literal "both icons doubled" acceptance criterion is explicitly pre-authorized by this plan's own `gap_closure_context` ("if the diagnostic fails there ... this is a real signal to widen SpokeHalfWidth or tune OutlineWidth, not a false positive to override").
**Impact on plan:** Necessary for the interior-artifact gate to genuinely pass without erasing rig.ico's intended design detail. No architectural changes; localized to `IconGeometry.cs`'s rig.ico-specific constants and pen width.

## Issues Encountered

**Significant empirical iteration required to root-cause the rig.ico diagnostic failure.** The plan's own `gap_closure_context` anticipated a possible <=2px diagnostic failure near the wheel's hub and suggested "widen SpokeHalfWidth or tune OutlineWidth" as the remedy -- but the first hypothesis tried (widening `SpokeHalfWidth`/`HubRadius` alone, keeping the 2x pen) produced *zero* observable change across two different geometry attempts, which was itself a useful negative signal ruling out spoke/hub proximity as the cause. Escalated to a systematic sanity-test sequence (drastic geometry changes to confirm the build/publish/Wine pipeline was genuinely picking up source edits; near-zero spoke width to isolate the outline-stroke-only contribution independent of spoke geometry; pen-width-only tests at 1x/1.5x/2x with fixed geometry) before correctly identifying pen width as the dominant factor and geometry widening as a secondary, complementary fix. All findings were verified against the actual Wine-rendered pixel diagnostic output at each step, not assumed.

## User Setup Required

None for Tasks 1-2 (fully automated, verified in this session).

**Task 3 (blocking checkpoint) requires the user's real Windows 11 rig hardware**, unavailable in this Linux worktree sandbox. See "Next Phase Readiness" below for exact verification steps.

## Next Phase Readiness

- **Tasks 1-2 are complete, committed, and verified** (`dotnet build RigToggle.sln -c Release` and the self-contained win-x64 publish both succeed with the regenerated icons embedded).
- **Task 3 (rig re-check) is BLOCKED, pending human verification on real Windows 11 hardware.** The freshly republished single-file exe (built in this session at `src/RigToggle.App/bin/Release/net10.0-windows/win-x64/publish/`) needs to be run on the rig to confirm:
  1. Normal-mode tray icon reads as a single clean monitor silhouette (no interior white gap in the neck/base, no stray specks)
  2. Rig-mode tray icon reads as a single clean tri-spoke wheel silhouette (no interior transparent holes, no nested-circle clutter)
  3. Both icons remain legible on both light and dark taskbars
- This plan execution is PAUSED at the Task 3 checkpoint per the standard checkpoint protocol -- a continuation agent (or the user directly) must perform the rig verification and report back "approved" (or describe remaining artifacts) before this plan can be marked complete and the icon regression (CR-01) formally closed.

---
*Phase: 13-tray-app-icon-redesign*
*Paused at: Task 3 checkpoint (2026-08-03)*

## Self-Check: PASSED

- FOUND: src/RigToggle.IconGen/IconGeometry.cs
- FOUND: src/RigToggle.IconGen/Program.cs
- FOUND: src/RigToggle.IconGen/IconWriter.cs
- FOUND: src/RigToggle.App/Resources/normal.ico
- FOUND: src/RigToggle.App/Resources/rig.ico
- FOUND: commit 5979281 (Task 1)
- FOUND: commit f3c4162 (Task 2)
- `dotnet build RigToggle.sln -c Release` verified passing (0 errors) after both commits
- `wine RigToggle.IconGen.exe` (self-contained win-x64 publish) verified exit 0: round-trip verified for all 3 files, interior-artifact gate passing (normal.ico 0 components, rig.ico 3 legitimate components of 6/6/7px, 0 components <=2px)
- Task 3 (checkpoint:human-verify, gate="blocking") NOT executed -- requires real Windows 11 rig hardware unavailable in this Linux worktree sandbox. Plan execution is PAUSED, not complete.
