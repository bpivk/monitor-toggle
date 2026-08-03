---
phase: 13-tray-app-icon-redesign
reviewed: 2026-08-03T12:53:56Z
depth: standard
files_reviewed: 6
files_reviewed_list:
  - src/RigToggle.IconGen/RigToggle.IconGen.csproj
  - src/RigToggle.IconGen/IconWriter.cs
  - src/RigToggle.IconGen/IconGeometry.cs
  - src/RigToggle.IconGen/Program.cs
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/RigToggle.App.csproj
findings:
  critical: 1
  warning: 3
  info: 3
  total: 7
status: issues_found
---

# Phase 13: Code Review Report

**Reviewed:** 2026-08-03T12:53:56Z
**Depth:** standard
**Files Reviewed:** 6
**Status:** issues_found

## Summary

Reviewed the new dev-time-only `RigToggle.IconGen` console project (procedural GDI+ icon drawing + hand-rolled multi-frame `.ico` binary writer) and the two shipped-app files touched by this phase (`MainForm.cs`'s tray-icon DPI fix, `RigToggle.App.csproj`'s embedded-resource wiring). Project isolation is correct: `RigToggle.App.csproj` has no `ProjectReference` to `RigToggle.IconGen`, and `RigToggle.IconGen` is not part of the app's publish output. The hand-rolled ICO byte layout (ICONDIR/ICONDIRENTRY, BMP-in-ICO bottom-up rows, padded 1bpp AND mask, PNG-in-ICO for the 256px frame) was traced against the format spec and is internally consistent; the round-trip verification logic in `Program.cs` correctly compensates for `System.Drawing.Icon`'s documented sized-constructor defect at ≥256px.

However, **the actual generated tray icon assets are visibly broken**. I extracted and rendered the real checked-in `normal.ico`/`rig.ico` files (produced by this code) at their shipped sizes and confirmed by inspection that `IconGeometry.cs`'s outline-stroking approach produces cluttered/seamed icons rather than the single clean silhouette the code's own comments describe. This is a functional defect in the phase's core deliverable, not a hypothetical edge case — see CR-01 below. Three warnings and three info-level findings round out the review (DPI-size coverage gap, a duplicated magic constant at risk of drift, a runtime DPI-caching gap, and minor code-quality nits).

## Critical Issues

### CR-01: DrawPath strokes every touching/overlapping sub-figure independently, producing visibly broken tray icons

**File:** `src/RigToggle.IconGen/IconGeometry.cs:63-69` (`DrawNormalIcon`), `:88-94` (`DrawRigIcon`), root geometry in `:144-157` (`BuildMonitorPath`) and `:165-195` (`BuildWheelPath`)

**Issue:** `BuildMonitorPath`/`BuildWheelPath` add several independent closed sub-figures (screen/neck/base rounded-rects; outer/inner rim ellipses, hub ellipse, three spoke polygons) into a single `GraphicsPath`, then `DrawNormalIcon`/`DrawRigIcon` call `g.FillPath(...)` followed by `g.DrawPath(outline, path)` on that combined path. `GraphicsPath.FillPath` correctly computes the union region via the fill-rule (so the *fill* looks right), but `GraphicsPath.DrawPath` (Pen stroke) does **not** compute a merged outer-contour outline — it strokes each sub-figure's own boundary independently. Every place two sub-shapes touch or overlap (screen↔neck, neck↔base, hub↔spoke, spoke↔inner-rim, outer-rim↔inner-rim) gets an extra black seam line drawn on top of the white fill, contradicting the file's own doc comments ("a single DrawPath call traces the outer contour only, not internal seams between the touching/overlapping sub-shapes" — line 51-52, and "the union outlines as a single contiguous silhouette" — line 76-77).

This is not theoretical — I extracted the actual `normal.ico`/`rig.ico` bytes from `src/RigToggle.App/Resources/` (the real output of this code, already committed) and rendered every shipped frame:
- **16px** `normal.ico` (the primary tray-icon-display size on 100% DPI): the "base" of the monitor renders as two disconnected black squares with a white gap between them instead of a single rounded bar, and the "neck" shows a stray white square poking through solid black — the monitor is barely recognizable as a monitor.
- **32px** `normal.ico`: a clear extra horizontal black line cuts across where the neck meets the screen and where the neck meets the base (visible internal seams), and the base region is mostly solid black rather than white-filled-with-outline.
- **16px/32px** `rig.ico`: instead of one clean tri-spoke wheel silhouette, the render shows a busy nested-circle/flower pattern — the outer rim, inner cutout, hub, and all three spokes are each independently outlined, producing many extra internal lines.
- By contrast, `app.ico` (`DrawAppIcon`, which never calls `DrawPath` — fill only, per D-06) renders cleanly with no artifacts, which corroborates that the outline stroke is specifically the cause.

Since `normal.ico`/`rig.ico` are the **tray** icons — the one asset the user actually looks at on every mode switch, and the literal subject of this phase ("tray app icon redesign") — this is a functional failure of the core deliverable, not a cosmetic nit.

**Fix:** Do not stroke the combined multi-figure path directly. Options:
- Compute the true union outline explicitly before stroking — e.g. build a `Region` from the filled path and trace its boundary (`Region.GetRegionScans` after a flatten, or export/re-trace via `GraphicsPath` boolean combine helpers), rather than relying on `DrawPath` to infer a merged contour it was never designed to compute.
- Or hand-construct each icon as a single non-self-overlapping outline polygon per visual segment (as `BuildSpokePath` already attempts for one axis) instead of unioning independently-closed primitive shapes (rounded-rects/ellipses) and expecting `DrawPath` to merge them.
- Or, as a lower-effort mitigation, skip stroking sub-figures whose edges are interior to the filled union (e.g., only stroke the *outer* rim ellipse and hub for the wheel; only stroke the outer contour of the monitor body, computed by hand, for `normal.ico`) rather than stroking every added primitive.
- Whichever approach is chosen, add a manual visual-inspection step (not just the existing byte-level round-trip check in `Program.cs`, which only proves the ICO container parses — it does not catch this class of rendering defect) before considering the icon assets done.

## Warnings

### WR-01: `TraySizes`/`AppSizes` omit common Windows DPI scale-factor icon sizes

**File:** `src/RigToggle.IconGen/Program.cs:40-41`
**Issue:** `TraySizes = { 16, 20, 24, 32 }` covers 100/125/150/200% scaling only. Windows also commonly reports `SM_CXSMICON`/`SM_CYSMICON` (what `MainForm.cs`'s new `SystemInformation.SmallIconSize` fix reads) as 28px (175%), 36px (225%), 40px (250%), and 48px (300%) — none of which have a matching embedded frame. `System.Drawing.Icon`'s sized constructor will silently substitute the nearest available frame at those scale factors rather than failing, so this degrades gracefully, but it partially undermines the stated purpose of this phase's DPI fix (pixel-correct frame selection) for a meaningful slice of real-world scale factors.
**Fix:** Add the missing scale-factor sizes (28, 36, 40, 48) to `TraySizes`, or explicitly document that only 100/125/150/200% are pixel-perfect and larger factors fall back gracefully.

### WR-02: Duplicated magic constant (`256`) between `IconWriter.cs` and `Program.cs` with no shared source of truth

**File:** `src/RigToggle.IconGen/IconWriter.cs:30` (`PngFrameThreshold`), `src/RigToggle.IconGen/Program.cs:48` (`LargeFrameVerificationThreshold`)
**Issue:** Both constants are independently declared as `private const int ... = 256;`, tied together only by a comment ("Matches IconWriter's PNG-encoding threshold"). If either value is changed in the future without the other, `Program.cs`'s round-trip verification would silently exercise the wrong code path (e.g. verifying a PNG-encoded frame via the broken sized-`Icon` constructor, or vice versa), potentially masking a real encoding regression behind a false "round-trip verified" pass.
**Fix:** Expose `IconWriter.PngFrameThreshold` as `internal` (or a shared `public const`) and have `Program.cs` reference it directly instead of redeclaring the value.

### WR-03: `MainForm`'s DPI-correct tray icon is only re-evaluated once, at first load

**File:** `src/RigToggle.App/MainForm.cs:231-249`
**Issue:** `LoadTrayIconsIfNeeded()` reads `SystemInformation.SmallIconSize` once and caches `_normalIcon`/`_rigIcon` for the lifetime of the form (guarded by the `_normalIcon is not null && _rigIcon is not null` early-return). This is a deliberate and reasonable choice to avoid re-deriving an `Icon` per toggle (documented GDI-handle-leak concern), but it means the DPI fix only applies to whatever DPI was active at process startup — if the user changes display scaling at runtime, or the app's window (and its associated DPI context) moves to a monitor with a different scale factor after startup, the tray icon frame chosen initially is never re-picked. The icon will still render (Windows scales it), just potentially not pixel-native anymore — silently reintroducing a milder version of the bug this phase fixes.
**Fix:** At minimum, note this as an accepted limitation in the doc comment (it currently only frames the fix as solving "regardless of display DPI scaling," which overstates the fix's runtime scope). If in-session DPI changes matter for this app's use case, consider re-deriving the cached icons on `WM_DPICHANGED`.

## Info

### IN-01: Temporary spoke `GraphicsPath` objects are never disposed

**File:** `src/RigToggle.IconGen/IconGeometry.cs:189-192`
**Issue:** `BuildSpokePath(...)` returns a new `GraphicsPath` (an `IDisposable`) that is passed directly into `path.AddPath(...)` without ever being captured or disposed: `path.AddPath(BuildSpokePath(cx, cy, hubR, innerR, spokeHalf, angleDegrees), false);`. `AddPath` copies the path data rather than taking ownership, so the three per-icon spoke `GraphicsPath` instances leak their underlying GDI resources until finalization. Inconsequential in practice since this is a short-lived console tool that exits immediately after writing the three `.ico` files, but it's inconsistent with the careful `using` discipline used everywhere else in this same file.
**Fix:** `using var spoke = BuildSpokePath(...); path.AddPath(spoke, false);`

### IN-02: Redundant `using System.Linq;`

**File:** `src/RigToggle.App/MainForm.cs:1`
**Issue:** The project has `<ImplicitUsings>enable</ImplicitUsings>` (`RigToggle.App.csproj:11`), which already implicitly includes `System.Linq` for the default SDK project type — the explicit `using System.Linq;` at the top of `MainForm.cs` is redundant.
**Fix:** Remove the explicit using, or leave it if the project intends to disable implicit usings later (verify with `dotnet build` / an analyzer such as IDE0005 rather than removing blind).

### IN-03: `biSizeImage` in the hand-rolled BITMAPINFOHEADER only counts the XOR mask, not the AND mask

**File:** `src/RigToggle.IconGen/IconWriter.cs:122`
**Issue:** `bw.Write((uint)(stride * h));` sizes only the pixel-data (XOR) portion of the frame, excluding the AND-mask bytes written afterward. For `BI_RGB` (uncompressed) bitmaps this field is documented as safe to leave unreliable/zero and readers are not expected to depend on it, so this is very unlikely to break anything in practice — but a value that doesn't match either "0 (unspecified)" or "full data size including the mask" is a slightly confusing middle ground for future maintainers reading this code as a reference for the format.
**Fix:** Either write `0` (the spec-sanctioned "don't care" value for `BI_RGB`) or the true total (`stride * h + maskRowBytes * h`) for clarity; either is more defensible than the current partial value.

---

_Reviewed: 2026-08-03T12:53:56Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
