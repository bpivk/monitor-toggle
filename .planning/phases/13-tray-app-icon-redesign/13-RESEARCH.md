# Phase 13: Tray & App Icon Redesign - Research

**Researched:** 2026-08-03
**Domain:** Windows Forms icon rendering — procedural GDI+ icon drawing, hand-rolled multi-resolution `.ico` file writing, MSBuild `ApplicationIcon` embedding, `System.Drawing.Icon`/`NotifyIcon` DPI frame-selection behavior
**Confidence:** MEDIUM-HIGH (core mechanisms verified against official Microsoft docs and dotnet source/issue tracker; exact pixel-level ICO byte layout is MEDIUM — cross-referenced from multiple community sources, not executable-tested in this sandbox since no Windows/dotnet runtime is available here)

## Summary

This phase has two genuinely separate technical problems, both solvable with zero new NuGet packages: (1) **drawing** the locked geometry from `13-UI-SPEC.md` crisply at six discrete pixel sizes using GDI+ (`System.Drawing.Graphics`/`GraphicsPath`), and (2) **packaging** those per-size bitmaps into valid multi-frame `.ico` files, which is the harder problem because `System.Drawing`'s `Icon` class has essentially no public API for *writing* a multi-size `.ico` — only for *reading* one. The standard, widely-used solution (confirmed across multiple independent sources) is to hand-write the `.ico` binary format directly: a 6-byte `ICONDIR` header + one 16-byte `ICONDIRENTRY` per frame + the frames' image data (uncompressed 32bpp BMP-in-ICO or PNG-in-ICO) concatenated after. This is well-documented, stable since Windows 95/Vista, and matches the project's existing checked-in-`.ico` asset pattern — no build-time tooling needed.

The second, more important finding is a **verified, currently-unfixed .NET/WinForms defect** that directly threatens ICON-03: `System.Drawing.Icon`'s size-less constructors (`new Icon(stream)` — exactly what `MainForm.LoadTrayIconsIfNeeded()` currently calls) **always return the smallest frame in a multi-size `.ico`**, per Microsoft's own official API docs, and `NotifyIcon` does not re-select a better-fitting frame at higher DPI (confirmed by an open, unresolved `dotnet/winforms` GitHub issue). Left unpatched, embedding a correct 16/20/24/32px `.ico` accomplishes nothing for the tray icon at 150%/200% DPI — Windows will still upscale the 16px frame and it will look soft, defeating ICON-03. The fix is a one-line change to the two `new Icon(...)` calls in `MainForm.cs`, not a resource-file-only change (this corrects an assumption in `13-UI-SPEC.md`'s Scope Notes — see Pitfall 1 below). This does *not* apply to the `.exe`/taskbar icon set via `<ApplicationIcon>`, which Windows' native shell icon-loading path (not `System.Drawing.Icon`) handles correctly on its own.

**Primary recommendation:** Draw each icon frame in-process via GDI+ per the UI-SPEC's fractional geometry grid, hand-roll a small `IconWriter` utility that packs the frames into standards-compliant multi-size `.ico` files (uncompressed 32bpp BMP-in-ICO frames for all sizes, per the UI-SPEC's explicitly pre-approved fallback), run this as a one-time dev-time console tool (`src/RigToggle.IconGen`) that overwrites the checked-in `Resources/*.ico` files, and fix `MainForm.cs`'s tray-icon loading to request `SystemInformation.SmallIconSize` explicitly so the correct DPI-scaled frame is actually used at runtime.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Icon geometry drawing (monitor/wheel/bezel shapes) | Dev-time tooling (new `RigToggle.IconGen` console project) | — | Pure GDI+ rasterization logic with no runtime dependency on the shipped app; belongs in a throwaway generator, not `RigToggle.App` |
| Multi-frame `.ico` byte packing | Dev-time tooling (same project) | — | File-format concern, not a UI concern; co-located with the drawing code since both only run at design time |
| Tray icon runtime loading/frame selection | `RigToggle.App` (`MainForm.cs`) | — | Existing `LoadTrayIconsIfNeeded()`/`NotifyIcon.Icon` assignment path; needs a small correctness fix (DPI frame selection), not new capability |
| `.exe`/taskbar icon embedding | Build system (`RigToggle.App.csproj` `<ApplicationIcon>`) | — | MSBuild/Win32 resource-compiler concern; Windows' shell resolves the correct frame per DPI natively — no application code involved |
| Checked-in icon assets | Source control (`Resources/*.ico`) | — | Static build outputs from the one-time generator, versioned like the current `normal.ico`/`rig.ico` |

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Normal mode = desktop monitor silhouette. Rig mode = steering wheel silhouette (Moza-style). Chosen specifically for strong shape contrast that reads correctly at 16×16 without relying on color, and because it maps directly onto the user's actual sim-racing setup (desk monitor vs. Moza wheel/pedals rig) rather than an abstract on/off variation of the same glyph.
- **D-02:** Flat, filled Fluent-style icons — solid shapes, minimal internal detail, consistent with native Windows 11 icons. Not outline/line-art.
- **D-03:** Icons are procedurally drawn in code (GDI+/System.Drawing), the same general approach as the current `normal.ico`/`rig.ico` assets — no external design tool, no new asset pipeline, no image-generation step. The concrete drawing approach (build-time generation script vs. hand-authored `.ico` files checked into `Resources/`) is an implementation detail for research/planning, but the icons themselves are code-drawn geometry, not sourced artwork.
- **D-04:** Both tray icons (`normal.ico`, `rig.ico`) are monochrome silhouettes — no per-mode accent color; the wheel-vs-monitor silhouette (D-01) is the sole differentiator, satisfying ICON-01's "shape alone, not color" requirement directly.
- **D-05:** Each tray icon silhouette includes a thin outline/stroke in the opposite tone around the shape — this is the ICON-02 mechanism: one asset per mode that stays legible on both light and dark taskbars, with no theme-detection or asset-swapping logic needed.
- **D-06:** The static `.exe`/taskbar icon (ICON-04) reuses the normal-mode monitor motif (D-01) at a larger size. Not a combined/neutral third design, not the rig-mode wheel.
- **D-07:** Unlike the two monochrome tray icons, the `.exe`/taskbar icon gets a color treatment (dark-gray body with a subtle accent) — following the Windows convention that taskbar/app icons are typically full-color even when a notification-tray icon from the same app is monochrome.

### Claude's Discretion

- Exact stroke width, corner radius, and geometric proportions of the monitor and steering-wheel silhouettes — **now locked by `13-UI-SPEC.md`'s Icon Geometry Contract**, see below; no longer open.
- Exact color values (grays, accent color) for the `.exe`/taskbar icon's color treatment — **now locked by `13-UI-SPEC.md`'s Color table** (`#2D2D30` body, `#005A9E` accent).
- Whether icon generation happens via a one-time script producing checked-in `.ico` files, or a build-time step — an implementation detail, not a vision decision. **This research recommends one-time script** (see Architecture Patterns below).
- File/resource naming and embedding mechanism (current code uses `assembly.GetManifestResourceStream("normal.ico"/"rig.ico")` via `LogicalName` embedded resources in `RigToggle.App.csproj` — reuse or evolve this pattern as appropriate). **This research recommends reusing the resource names but evolving the loading code** — see Pitfall 1.

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| ICON-01 | Tray icon pair uses genuinely distinct silhouettes (shape, not color) | UI-SPEC's locked geometry (monitor vs. tri-spoke wheel) already satisfies this by construction; research confirms GDI+ can render both silhouettes as one contiguous `GraphicsPath` per icon (Architecture Patterns, Pattern 1/2) |
| ICON-02 | Tray icons legible at 16×16, visible against both light and dark taskbars | UI-SPEC's white-fill/black-outline self-contained-contrast design (D-05) is the mechanism; research adds the GDI+ rendering-quality settings (`SmoothingMode`, `PixelOffsetMode`) needed to avoid the outline/fill boundary blurring away at 16px (Pitfall 2) |
| ICON-03 | Multi-resolution `.ico` (min 16/20/24/32px) rendering sharply at every DPI Windows requests | Research supplies the hand-rolled `ICONDIR`/`ICONDIRENTRY` writer (Architecture Patterns, Pattern 3) needed because `System.Drawing.Icon` cannot write multi-frame `.ico` files, **and** identifies the critical runtime frame-selection defect that would silently defeat this requirement if not fixed in `MainForm.cs` (Pitfall 1) |
| ICON-04 | `.exe`/taskbar icon reuses tray artwork/motif at larger size | Research confirms `<ApplicationIcon>` MSBuild property is the correct, well-documented mechanism (Architecture Patterns, Pattern 4) and that it is unaffected by the `System.Drawing.Icon` frame-selection defect (Pitfall 1's scope note) |
</phase_requirements>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Drawing.Common` (BCL, via `UseWindowsForms=true`) | Ships with .NET 10 SDK — already referenced by `RigToggle.App`; the new generator project should also set `UseWindowsForms=true` (rather than adding a separate `System.Drawing.Common` PackageReference) to get GDI+ types for free, matching this project's zero-new-package pattern `[VERIFIED: npm registry N/A — BCL, confirmed present via existing RigToggle.App.csproj `<UseWindowsForms>true</UseWindowsForms>`]` | Purpose: `Graphics`, `GraphicsPath`, `Bitmap`, `SolidBrush`, `Pen` for procedural drawing; `BinaryWriter`/raw byte arrays for the hand-rolled ICO writer. No alternative needed — this is exactly what the current `normal.ico`/`rig.ico` era already implies (System.Drawing is the only GDI+-capable BCL surface in .NET). |

### Supporting

None — this phase deliberately adds zero new libraries (`13-UI-SPEC.md` Registry Safety section, `13-CONTEXT.md` D-03). See Package Legitimacy Audit below.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled `ICONDIR`/`ICONDIRENTRY` writer | ImageMagick `magick` CLI (recommended in the *milestone-level* pre-roadmap research, `.planning/research/STACK.md`) | Project-level research (done before Phase 13's `discuss-phase`) recommended ImageMagick + Inkscape/Figma for dev-time `.ico` packing. **This is superseded by D-03**, which explicitly locks out external design tools/asset pipelines for Phase 13 — noted here only so the planner doesn't reintroduce it by mistake. |
| Hand-rolled ICO writer | `jtippet/icotools` (`IcoCat`/`IcoCrush`, MIT, pure C#) [ASSUMED — found via WebSearch, not independently verified against the actual repo source beyond its README] | CLI-only tool (`dotnet icocat.dll -i icon.ico -s frame.png`), not a library with a programmatic API — would mean shelling out to an external process from the generator, which adds no benefit over ~40 lines of hand-rolled binary writing and reintroduces the "external tool" pattern D-03 explicitly rejects. Not recommended. |
| Hand-rolled ICO writer | `System.Drawing.Icon.Save()` after loading a pre-built multi-frame file | Only usable for *round-tripping* an existing multi-frame `.ico` (load → re-save preserves all frames) — has no public constructor path to *combine* several independently-drawn `Bitmap`s into one multi-frame `Icon` object in the first place. Confirmed: no `Icon.FromImages(...)`-style API exists in the BCL. |

**Installation:**
```bash
# No installation needed — System.Drawing types come from UseWindowsForms=true,
# already set in RigToggle.App.csproj. The new generator project just needs the
# same SDK property, not a new PackageReference.
```

**Version verification:** N/A — no versioned package is being added. `System.Drawing.Common` ships in-box with the `net10.0-windows` TFM's Windows Desktop shared framework when `UseWindowsForms=true`; already confirmed present via direct read of `RigToggle.App.csproj`.

## Package Legitimacy Audit

**Not applicable.** This phase adds zero NuGet packages. `13-UI-SPEC.md`'s Registry Safety section and `13-CONTEXT.md`'s D-03 both explicitly confirm: procedural GDI+ drawing uses only BCL `System.Drawing` types already referenced by the project's existing `UseWindowsForms=true` setting. No `slopcheck`/registry verification step applies.

## Architecture Patterns

### System Architecture Diagram

```
                     DESIGN TIME (developer machine, run manually, not part of CI/dotnet publish)
                     ============================================================================

  13-UI-SPEC.md               src/RigToggle.IconGen (new console project)
  fractional geometry    ┌──────────────────────────────────────────────┐
  + color contract  ───► │ 1. IconGeometry.cs                          │
                          │    DrawNormalIcon(Graphics g, int size)     │
                          │    DrawRigIcon(Graphics g, int size)        │
                          │    DrawAppIcon(Graphics g, int size)        │
                          │    — redraws fresh GraphicsPath per size,   │
                          │      never scales one bitmap (UI-SPEC rule 2)│
                          └───────────────────┬──────────────────────────┘
                                              │ produces one Bitmap per
                                              │ target size (16/20/24/32
                                              │ for tray; +48/256 for app)
                                              ▼
                          ┌──────────────────────────────────────────────┐
                          │ 2. IconWriter.cs                             │
                          │    WriteIco(Stream, IReadOnlyList<Bitmap>)   │
                          │    — hand-rolled ICONDIR + ICONDIRENTRY[]    │
                          │      + BMP-in-ICO frame data                 │
                          └───────────────────┬──────────────────────────┘
                                              │ overwrites
                                              ▼
                     src/RigToggle.App/Resources/normal.ico   (16/20/24/32, monochrome)
                     src/RigToggle.App/Resources/rig.ico      (16/20/24/32, monochrome)
                     src/RigToggle.App/Resources/app.ico      (16/20/24/32/48/256, color)
                                              │ checked into git, consumed unchanged
                                              │ at every future `dotnet build`/`publish`
                                              ▼
                     ============================================================================
                     RUNTIME / BUILD TIME (unchanged — no new build steps)
                     ============================================================================

  RigToggle.App.csproj                                    RigToggle.App.csproj
  <EmbeddedResource LogicalName="normal.ico">   ┐         <ApplicationIcon>Resources\app.ico
  <EmbeddedResource LogicalName="rig.ico">      │         </ApplicationIcon>  (NEW property)
                                                  │                    │
                                                  ▼                    ▼
                          MainForm.cs                         Win32 resource compiler
                          LoadTrayIconsIfNeeded()              embeds app.ico's frames
                          new Icon(stream, SmallIconSize) ◄─ FIX      directly into the .exe
                          (Pitfall 1 — must pick correct              (RT_GROUP_ICON/RT_ICON)
                           frame per current DPI, not the                    │
                           default "smallest frame")                        ▼
                                    │                          Windows Explorer / Alt-Tab /
                                    ▼                          Taskbar auto-select correct
                          notifyIcon.Icon = ...                frame per DPI (shell-native,
                          (tray, DPI-correct)                   no app code involved)
```

### Recommended Project Structure

```
src/
├── RigToggle.App/
│   └── Resources/
│       ├── normal.ico      # regenerated by IconGen, same LogicalName as today
│       ├── rig.ico         # regenerated by IconGen, same LogicalName as today
│       └── app.ico         # NEW — referenced by <ApplicationIcon>
├── RigToggle.IconGen/       # NEW — dev-time-only console project, never referenced
│   ├── RigToggle.IconGen.csproj   # UseWindowsForms=true, OutputType=Exe, NOT added
│   │                               # as a ProjectReference from RigToggle.App (keeps
│   │                               # it out of the self-contained publish entirely)
│   ├── Program.cs           # entry point: generates all 3 .ico files, writes to
│   │                         # ../RigToggle.App/Resources/ via relative path
│   ├── IconGeometry.cs      # the 3 DrawXxxIcon(Graphics, int) methods per UI-SPEC
│   └── IconWriter.cs        # hand-rolled multi-frame .ico byte writer
└── ...(existing projects unchanged)
```

### Pattern 1: Redraw-per-size GDI+ geometry (not bitmap-scaled)

**What:** For each target pixel size, create a fresh `Bitmap`/`Graphics` at exactly that size and re-run the shape-drawing logic using fractional coordinates × the target size — never draw once at a large size and downscale.
**When to use:** All three icons, all sizes, per UI-SPEC governing rule 2.
**Example:**
```csharp
// Source: pattern synthesized from UI-SPEC's fractional-grid contract + standard
// GDI+ usage (Microsoft Learn "Antialiasing with Lines and Curves",
// https://learn.microsoft.com/en-us/windows/win32/gdiplus/-gdiplus-antialiasing-with-lines-and-curves-about)
static Bitmap DrawNormalIcon(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.Clear(Color.Transparent);

    float w = size, h = size;
    using var path = new GraphicsPath();
    // Screen: top-left (0.125W, 0.125H), size 0.75W x 0.5H, corner radius 0.06W
    AddRoundedRect(path, 0.125f * w, 0.125f * h, 0.75f * w, 0.5f * h, 0.06f * w);
    // Neck: top-left (0.4375W, 0.625H), size 0.125W x 0.125H, sharp corners
    path.AddRectangle(new RectangleF(0.4375f * w, 0.625f * h, 0.125f * w, 0.125f * h));
    // Base: top-left (0.28W, 0.75H), size 0.44W x 0.125H, corner radius 0.03W
    AddRoundedRect(path, 0.28f * w, 0.75f * h, 0.44f * w, 0.125f * h, 0.03f * w);

    using var fill = new SolidBrush(Color.White);
    g.FillPath(fill, path);
    using var outline = new Pen(Color.Black, Math.Max(1f, size / 16f)); // ~1px at 16px canvas
    g.DrawPath(outline, path);
    return bmp;
}
```
*Note on outline stroke width:* UI-SPEC does not pin an exact outline stroke-width fraction — only that an outline must exist (D-05) tracing every boundary. `Math.Max(1f, size / 16f)` keeps a ~1px stroke at the 16px canvas and scales proportionally at larger sizes; treat this as a starting value for the planner/executor to tune against the mandated human visual-verification step (UI-SPEC Scope Notes), not a locked value.

### Pattern 2: Contiguous-silhouette outline via `GraphicsPath.AddPath`/union, not per-shape strokes

**What:** UI-SPEC requires outlining "the full contiguous silhouette (screen ∪ neck ∪ base as one shape, since they touch/overlap)" for `normal.ico`, and "rim ∪ hub ∪ 3 spokes" for `rig.ico` — a single traced boundary, not three independently-outlined rectangles (which would draw visible seams where shapes overlap).
**When to use:** Both tray icons.
**Example:**
```csharp
// Combine sub-shapes into one GraphicsPath (as in Pattern 1 above — screen, neck,
// base all added to the SAME path object), then a single g.DrawPath(outlinePen, path)
// call traces only the outer contour of the union, not internal seams between
// overlapping rectangles. This is standard GraphicsPath behavior when shapes are
// added to one path and the winding mode is left at its default (Alternate) —
// [ASSUMED: exact seam-elimination behavior at overlap boundaries should be
// visually confirmed during the UI-SPEC-mandated human verification step; GDI+'s
// path-stroke rendering is well-documented for outer contours but corner cases at
// shape overlaps are not something this research executed/tested].
```
For `rig.ico`'s ring-with-spokes shape, build the outer rim as a filled circle, subtract the inner-cutout circle (`FillMode.Alternate` with two concentric `AddEllipse` calls achieves the ring), then add the 3 spoke rectangles/hub disc to the same path so the whole silhouette (rim ∪ hub ∪ spokes, minus the inner cutout) draws and outlines as one unit.

### Pattern 3: Hand-rolled multi-frame `.ico` writer

**What:** Since `System.Drawing.Icon` has no public multi-frame *write* API `[VERIFIED: Microsoft Learn, Icon constructors reference — every constructor either loads a single best-fit frame from a source, or duplicates/resizes an existing Icon's already-selected frame; none combines independent Bitmaps into a new multi-frame Icon]`, the file must be assembled by hand per the ICO binary format.
**When to use:** All three `.ico` outputs.
**Example:**
```csharp
// Source: format verified against Wikipedia "ICO (file format)"
// (https://en.wikipedia.org/wiki/ICO_(file_format)) — HIGH confidence for the
// byte layout, cross-referenced against the general community pattern described
// in "Generate a True ICO Format Image in .NET Core" (Edi Wang, edi.wang) and
// "C# Helper: Make multi-image icon files in C#" (csharphelper.com). The BMP
// pixel/mask encoding below is the standard technique from those sources —
// MEDIUM confidence (not executed/tested in this sandbox; no Windows runtime
// available here to verify the produced file renders correctly).
static void WriteIco(Stream output, IReadOnlyList<Bitmap> framesSmallestFirstOrAny)
{
    using var bw = new BinaryWriter(output);
    int count = framesSmallestFirstOrAny.Count;

    // ICONDIR (6 bytes)
    bw.Write((ushort)0);   // reserved
    bw.Write((ushort)1);   // type = icon
    bw.Write((ushort)count);

    var entryDataOffsets = new int[count];
    var entryDataSizes = new int[count];
    var frameBytes = new byte[count][];

    // Pre-encode every frame's BMP-in-ICO bytes so we know sizes before writing
    // the directory (offsets must be known up front).
    for (int i = 0; i < count; i++)
        frameBytes[i] = EncodeBmpInIco(framesSmallestFirstOrAny[i]);

    int headerSize = 6 + 16 * count;
    int runningOffset = headerSize;
    for (int i = 0; i < count; i++)
    {
        entryDataOffsets[i] = runningOffset;
        entryDataSizes[i] = frameBytes[i].Length;
        runningOffset += frameBytes[i].Length;
    }

    for (int i = 0; i < count; i++)
    {
        var bmp = framesSmallestFirstOrAny[i];
        // Width/height byte = 0 means 256 (per format spec)
        bw.Write((byte)(bmp.Width >= 256 ? 0 : bmp.Width));
        bw.Write((byte)(bmp.Height >= 256 ? 0 : bmp.Height));
        bw.Write((byte)0);      // color count (0 = no palette, >=8bpp)
        bw.Write((byte)0);      // reserved
        bw.Write((ushort)1);    // planes
        bw.Write((ushort)32);   // bit count
        bw.Write((uint)entryDataSizes[i]);
        bw.Write((uint)entryDataOffsets[i]);
    }

    foreach (var data in frameBytes)
        bw.Write(data);
}

static byte[] EncodeBmpInIco(Bitmap bmp)
{
    int w = bmp.Width, h = bmp.Height;
    var rect = new Rectangle(0, 0, w, h);
    var locked = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try
    {
        int stride = locked.Stride;
        var pixels = new byte[stride * h];
        System.Runtime.InteropServices.Marshal.Copy(locked.Scan0, pixels, 0, pixels.Length);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // BITMAPINFOHEADER (40 bytes) — height is DOUBLED (XOR + AND masks stacked)
        bw.Write((uint)40);
        bw.Write(w);
        bw.Write(h * 2);
        bw.Write((ushort)1);      // planes
        bw.Write((ushort)32);     // bit count
        bw.Write((uint)0);        // BI_RGB, uncompressed
        bw.Write((uint)(stride * h));
        bw.Write(0); bw.Write(0); // x/y pels per meter
        bw.Write((uint)0); bw.Write((uint)0); // colors used/important

        // XOR mask: bottom-up, BGRA per pixel (Bitmap's Format32bppArgb is already
        // BGRA in memory on little-endian Windows, so rows are copied as-is,
        // just in reverse row order)
        for (int y = h - 1; y >= 0; y--)
            bw.Write(pixels, y * stride, stride);

        // AND mask: 1bpp, bottom-up, each row padded to a 4-byte boundary.
        // All-zero (fully opaque per legacy XOR convention) — the real
        // transparency comes from the 32bpp alpha channel above, which Vista+
        // icon rendering honors directly.
        int maskRowBytes = ((w + 31) / 32) * 4;
        var zeroRow = new byte[maskRowBytes];
        for (int y = 0; y < h; y++)
            bw.Write(zeroRow);

        return ms.ToArray();
    }
    finally
    {
        bmp.UnlockBits(locked);
    }
}
```

### Pattern 4: `<ApplicationIcon>` MSBuild wiring for the exe/taskbar icon

**What:** Add a single MSBuild property to embed `app.ico` as the compiled executable's native Win32 icon resource.
**When to use:** `RigToggle.App.csproj`, confirmed absent today (direct read).
**Example:**
```xml
<!-- Source: Microsoft Learn, "Common MSBuild Project Properties"
     https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-properties
     [VERIFIED: official docs] — "ApplicationIcon | .NET | The .ico icon file to
     pass to the compiler for embedding as a Win32 icon. The property is
     equivalent to the /win32icon compiler switch." -->
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <ApplicationIcon>Resources\app.ico</ApplicationIcon>  <!-- NEW -->
</PropertyGroup>
```
No separate `<ItemGroup>`/`Content`/`None` entry is required for `app.ico` — the property alone is sufficient; the SDK's build targets resolve the path (relative to the project file) and pass it to the Win32 resource compiler independently of the existing `EmbeddedResource` items used for the tray icons. This is a **separate embedding mechanism** from the tray icons' `EmbeddedResource`+`LogicalName` pattern — `app.ico` becomes a native Win32 `RT_GROUP_ICON`/`RT_ICON` resource in the compiled `.exe`, not something read via `Assembly.GetManifestResourceStream` at runtime. Windows Explorer, Alt-Tab, and the taskbar read this resource directly through shell APIs that correctly select the best-fitting frame per current DPI on their own — this path is unaffected by Pitfall 1 below.

### Anti-Patterns to Avoid

- **Loading `app.ico`'s bytes at runtime the same way as `normal.ico`/`rig.ico` (`GetManifestResourceStream` + `new Icon(...)`) and assigning it somewhere in code:** Unnecessary and wrong mechanism — `<ApplicationIcon>` already embeds it as a native resource at compile time; no runtime code should reference `app.ico` at all (Explorer/taskbar read it from the compiled binary directly, not through the app's own code).
- **Scaling one large drawn bitmap down to smaller sizes with `Graphics.DrawImage`/`InterpolationMode.HighQualityBicubic`:** Explicitly forbidden by UI-SPEC governing rule 2 ("redraw at each target size — do not scale a single bitmap") — produces mushy edges at 16px specifically, the size that matters most for ICON-02.
- **Reusing a single `PixelFormat.Format8bppIndexed`/paletted `Bitmap` for the tray icons:** UI-SPEC's Color table requires "fully opaque"/"transparent (alpha 0)" per-pixel alpha — always use `Format32bppArgb` so both the fill/outline and background-transparency contract are representable per-pixel.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Icon *artwork* (vector design, professional-grade shape polish) | A general-purpose vector illustration tool/pipeline | The locked GDI+ `GraphicsPath` geometry in `13-UI-SPEC.md` (this phase already decided, D-03, to hand-roll this specific piece — noted here only as the boundary: don't over-invest in a fuller design tool, the spec is already pixel-exact) | The spec exists precisely so no design judgment is needed at implementation time |
| Multi-frame `.ico` *reading* elsewhere in the codebase (if ever needed again) | A second bespoke ICO parser | `System.Drawing.Icon`'s built-in constructors (`Icon(Stream, Size)` etc.) — reading multi-size `.ico` files is fully supported by the BCL; only *writing* them needed the hand-rolled path in this phase | Don't generalize the hand-rolled writer into also being a reader; the BCL already covers reading |
| PNG/BMP pixel encoding inside the ICO frame data | A custom bitmap/PNG encoder from scratch | `Bitmap.LockBits`/`Marshal.Copy` for BMP-in-ICO (Pattern 3 above), or `Bitmap.Save(stream, ImageFormat.Png)` if the planner chooses the PNG-in-ICO path for the 48/256 `app.ico` frames instead | `System.Drawing.Bitmap` already has correct, tested pixel/PNG encoding — only the outer ICO container format needs hand-rolling, not the pixel codec itself |

**Key insight:** The only piece of this phase that is *justifiably* hand-rolled (beyond the deliberately-hand-drawn artwork, D-03) is the ~16-byte-per-entry `ICONDIR`/`ICONDIRENTRY` wrapper — a small, stable, 30-year-old binary format with no ambiguity in its public specification. Everything inside each frame (pixel data, PNG encoding) should still go through `System.Drawing.Bitmap`'s built-in, well-tested encoders — don't also hand-roll pixel-level BMP/PNG writing beyond what Pattern 3 already shows.

## Common Pitfalls

### Pitfall 1: `System.Drawing.Icon`'s size-less constructors always select the *smallest* frame — silently defeats ICON-03 at the tray

**What goes wrong:** `MainForm.LoadTrayIconsIfNeeded()` currently calls `new Icon(normalStream)` / `new Icon(rigStream)` with no size argument. Even after embedding a correct 16/20/24/32px multi-frame `.ico`, this constructor **always returns the 16px frame**, regardless of the system's actual current icon-size setting or DPI. `NotifyIcon.Icon` then gets assigned that fixed 16px-frame `Icon` object, whose `.Handle` (the real `HICON` the shell paints) never changes — Windows upscales that 16px bitmap to render the tray icon at 150%/200% DPI, producing a soft/blurry icon exactly like the thing ICON-03 exists to prevent.
**Why it happens:** `[VERIFIED: Microsoft Learn, Icon(Stream) constructor reference, https://learn.microsoft.com/en-us/dotnet/api/system.drawing.icon.-ctor]` — official docs state verbatim: "This constructor returns the smallest image that is contained in the specified stream." This is not a bug in the strict sense (it's documented behavior) but interacts with a second, genuinely unfixed defect: `[VERIFIED: dotnet/winforms GitHub issue #6955, "NotifyIcon does not use the appropriate icon size"]` confirms `NotifyIcon` itself performs no corrective re-selection — it uses whatever `Icon.Handle` it was handed, with no DPI-awareness of its own.
**How to avoid:** Use the sized constructor/overload instead — `new Icon(stream, SystemInformation.SmallIconSize)` (or reconstruct via `new Icon(loadedIcon, SystemInformation.SmallIconSize)`). `[VERIFIED: Microsoft Learn Icon(Icon,Size) constructor reference]` confirms this correctly re-parses the full retained multi-frame byte buffer (`_iconData`, verified present internally for any Icon originally loaded from a Stream/file) rather than merely duplicating whichever single frame was already selected — so this fix works correctly even applied to an already-constructed size-less `Icon`. `SystemInformation.SmallIconSize` reflects the OS's actual current small-icon metric, which scales with DPI (100%→16px, 125%→20px, 150%→24px, 200%→32px) — the exact four sizes `13-UI-SPEC.md` already mandates. **This requires a small code change in `MainForm.cs`, contradicting `13-UI-SPEC.md`'s Scope Notes claim that "no code changes needed in MainForm.cs's loading path"** — that note was written before this DPI-selection defect was investigated; the planner should treat the two `new Icon(...)` call sites in `LoadTrayIconsIfNeeded()` as in-scope for a one-line fix, not out-of-scope.
**Warning signs:** Tray icon looks visibly soft/pixelated specifically at 125%/150%/200% Windows display scaling (but crisp at 100%) even after the new `.ico` files are embedded — this is the fingerprint of this exact defect, not a drawing-geometry problem.
**Scope note:** This defect is specific to `System.Drawing.Icon` objects loaded from a stream/file and assigned to `NotifyIcon.Icon`. It does **not** apply to `app.ico`/`<ApplicationIcon>` — Windows' shell (Explorer, Alt-Tab, taskbar) resolves the correct frame from the compiled `.exe`'s native Win32 icon resource through its own DPI-aware icon-loading path, independent of `System.Drawing.Icon`.

### Pitfall 2: GDI+ anti-aliasing can blur thin features below their nominal pixel width at 16px

**What goes wrong:** `13-UI-SPEC.md`'s `rig.ico` geometry specifies a ring thickness/spoke width of `0.14W` (≈2.2px at the 16px canvas) — technically clears the spec's "≥2px minimum feature" rule as a raw fraction, but GDI+'s `SmoothingMode.AntiAlias` blends partial-coverage pixels at every edge, which can make a nominally-2px-wide feature render as a soft ~1px-equivalent smear rather than two crisp pixel rows, especially where the edge falls between pixel boundaries.
**Why it happens:** Anti-aliasing trades hard edges for partial-alpha blending at sub-pixel boundaries; at 16px, "sub-pixel" is a large fraction of the whole feature. This is inherent to the technique, not a configuration mistake — `SmoothingMode.AntiAlias` + `PixelOffsetMode.HighQuality` `[MEDIUM confidence — WebSearch-sourced general GDI+ guidance, corroborated by Microsoft's own "Antialiasing with Lines and Curves" doc which confirms AntiAlias mode is the standard mechanism but does not itself discuss small-icon-specific softness tradeoffs]` is still the right *default* setting per UI-SPEC governing rule 2, but does not eliminate this risk on its own.
**How to avoid:** UI-SPEC already anticipates this and mandates a human visual-verification step (real 16×16 render, both light and dark taskbar) before calling ICON-01/ICON-02 done. If a shape reads ambiguously, the fix is increasing the *fraction* (e.g. spoke width from `0.14W` to `0.18W`) — not switching off anti-aliasing, which would reintroduce jagged/pixelated edges that look worse at scaled-up sizes (20/24/32px).
**Warning signs:** Ring/spoke boundaries look "fuzzy gray" rather than crisp black/white specifically at the 16px frame when zoomed to 800%+ in an image viewer, or the wheel silhouette reads ambiguously against a mid-gray taskbar.

### Pitfall 3: `ICONDIRENTRY` width/height byte overflow at 256px

**What goes wrong:** The `app.ico`'s recommended 256px frame cannot be represented as a literal `byte` value (max 255) in the `bWidth`/`bHeight` fields.
**Why it happens:** `[VERIFIED: Wikipedia, "ICO (file format)"]` — the format spec reserves `0` in these single-byte fields to mean "256," a convention dating to Windows 95. A naive `(byte)bmp.Width` cast on a 256px frame silently truncates to `0` — which happens to be exactly correct by the spec's convention, but only if the code does it deliberately (`bmp.Width >= 256 ? 0 : bmp.Width`), not by accident of unsigned-byte wraparound reasoning.
**How to avoid:** Pattern 3's example code above already handles this explicitly (`(byte)(bmp.Width >= 256 ? 0 : bmp.Width)`) — flag this specifically in code review since it's easy to "simplify" into a plain cast that happens to produce the same byte value for the wrong reason (and would break for any future 512px+ frame).
**Warning signs:** N/A for this project's frame set (max frame is exactly 256px, and the `>=256 ? 0` check handles exactly that case) — worth a unit test asserting the directory entry byte is `0` for the 256px frame specifically, given how easy this is to get right by accident and wrong on the next change.

### Pitfall 4: BMP-in-ICO frames must be bottom-up with 32-bit-padded AND mask rows

**What goes wrong:** Writing pixel rows in the same top-to-bottom order `Bitmap.LockBits` returns them, or omitting row padding on the 1bpp AND mask, produces a `.ico` file that looks correct in some viewers but renders upside-down, sheared, or garbled in others (behavior is inconsistent across renderers because some are lenient about malformed DIB data and others are not).
**Why it happens:** `[MEDIUM confidence — cross-referenced from Wikipedia's format description ("DIB entries... bottom-up") plus the general community pattern in multiple independent blog write-ups (Edi Wang, csharphelper.com, softwarebydefault.com), not independently execution-verified in this sandbox]` — the DIB (device-independent bitmap) format embedded in `.ico` files follows classic Windows BMP row order (bottom row first), and each mask row must be padded to a 4-byte boundary regardless of the actual pixel width.
**How to avoid:** Pattern 3's `EncodeBmpInIco` example writes rows in reverse (`for (int y = h - 1; y >= 0; y--)`) and computes `maskRowBytes = ((w + 31) / 32) * 4` for the AND mask specifically to handle this. Treat any deviation from this exact loop structure as high-risk and worth an explicit unit test (open+re-decode the generated `.ico` via `new Icon(path, size)` and assert `IconSize`/pixel-sample correctness) before relying on visual inspection alone.
**Warning signs:** Icon renders correctly in one context (e.g. `explorer.exe` thumbnail) but wrong in another (e.g. `System.Drawing.Icon` re-loading it), or appears vertically flipped/skewed.

## Code Examples

Verified/cross-referenced patterns — see Architecture Patterns section above for full listings (Pattern 1: geometry drawing, Pattern 3: ICO writer, Pattern 4: `ApplicationIcon` wiring). No additional standalone examples needed beyond those.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| BMP-only `.ico` frames (all sizes) | PNG-compressed frames allowed for large sizes (typically ≥48px/256px) | Windows Vista (2007) `[VERIFIED: Wikipedia ICO format article]` | This project's Windows 10/11-only target means PNG compression is always safely available if the planner prefers it for the 48/256 `app.ico` frames over BMP-in-ICO's larger uncompressed size — but UI-SPEC's explicit fallback permission (BMP for all sizes, if hand-rolling) means this is optional, not required. |
| `ChangeIconResource`/direct `.ico` file replacement in a built `.exe` post-build | `<ApplicationIcon>` MSBuild property, compiled in at build time | Standard since early .NET Framework project system; unchanged mechanism through .NET 10 SDK-style projects `[VERIFIED: Microsoft Learn, current doc dated 2026-06-04]` | No "modern replacement" to be aware of — this is still the current, correct mechanism; nothing deprecated here. |

**Deprecated/outdated:** Nothing identified as deprecated within this phase's scope — both the ICO file format and the `ApplicationIcon` MSBuild property are long-stable, unchanged mechanisms.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `jtippet/icotools` provides CLI-only `IcoCat`/`IcoCrush` tools with no programmatic C# API | Alternatives Considered | Low — this is only cited as a rejected alternative; even if it does expose a library API, the recommendation (hand-roll instead, per D-03) doesn't change |
| A2 | GDI+'s exact sub-pixel blending behavior at 16px for a `0.14W`-fraction feature will visually read as intended | Pitfall 2 | Medium — if wrong, the wheel's ring/spokes could look ambiguous at native tray size; mitigated by UI-SPEC's own mandated human visual-verification step, which exists specifically to catch this |
| A3 | `GraphicsPath` union-stroke behavior (Pattern 2) eliminates internal seams between overlapping sub-shapes (screen/neck/base, rim/hub/spokes) without extra `FillMode`/winding-rule tuning | Pattern 2 | Medium — if wrong, the silhouette could show visible internal seam lines instead of one clean outline; would need a `FillMode.Winding` adjustment or explicit path simplification, catchable at the same human-verification step |
| A4 | The hand-rolled `EncodeBmpInIco` byte layout (Pattern 3, Pitfall 4) is correct end-to-end (bottom-up rows, 32-bit-padded AND mask, doubled `biHeight`) | Pattern 3, Pitfall 4 | High if wrong — a malformed ICO file could fail to load at all (`InvalidOperationException` at startup, per the existing null-check pattern in `LoadTrayIconsIfNeeded`) or render garbled; **strongly recommend the plan include a task that round-trips each generated `.ico` through `new Icon(path, size)` and asserts it loads and reports the expected `.Size` for every frame**, not just a visual check, before the human-verification step |

## Open Questions

1. **Does `SystemInformation.SmallIconSize` update live if display scaling changes while the app is tray-resident, or only at process start?**
   - What we know: `LoadTrayIconsIfNeeded()` is called once and its result cached for the app's lifetime (existing 08-RESEARCH.md Pitfall 3 constraint, cited in the code comments, explicitly to avoid a GDI handle leak over multi-hour sessions).
   - What's unclear: If a user changes Windows display scaling mid-session (rare but possible on this rig-and-desk dual-context machine), whether the cached `Icon` objects would need to be re-derived to stay DPI-correct, and whether `SystemInformation.SmallIconSize` itself reflects the *new* scaling without an app restart.
   - Recommendation: Out of scope for this phase's success criteria (ICON-01 through ICON-04 don't mention live-DPI-change handling) — note as a known limitation if raised during human verification, don't build new re-load logic speculatively.

2. **Exact outline stroke width and corner-radius-implied thin-feature interaction at 16px are not independently pinned by UI-SPEC beyond "thin"/existing fraction values.**
   - What we know: UI-SPEC pins shape fractions precisely but leaves outline stroke width to "Claude's Discretion" (still open per CONTEXT.md, not resolved by UI-SPEC's Color table which only pins the outline *color*, not its *width*).
   - What's unclear: Whether a 1px-at-16px outline (Pattern 1's `Math.Max(1f, size/16f)` starting value) reads correctly against both extremes (pure white and pure black taskbars) or needs to be thicker.
   - Recommendation: Treat as a tunable parameter validated by the UI-SPEC's own mandated human-verification step, not something to over-specify in the plan before seeing a real render.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `dotnet` CLI / .NET 10 SDK | Building/running the new `RigToggle.IconGen` generator and regenerating `.ico` files | ✗ (this research sandbox — Linux, no `dotnet` on PATH) | — | None available in this sandbox; this is expected and consistent with every prior phase of this Windows-only project — actual generation/build/test must happen on a machine with the .NET 10 SDK installed (the developer's normal build machine or the rig PC itself), not in this research environment. |
| Windows 10/11 runtime (for visually verifying rendered icons) | UI-SPEC's mandated human visual-verification step (real tray render, light/dark taskbar) | ✗ (this sandbox is Linux) | — | None — this step genuinely requires a real Windows session, consistent with this project's established rig-checkpoint pattern (Phase 12's 12-04/12-06 plans) that already assumes execution/verification happens outside this research/planning environment. |

**Missing dependencies with no fallback:**
- Actual `.ico` file generation and visual verification cannot happen in this research session — the plan must schedule this as a normal execution-phase task run on a Windows/dotnet-capable machine, exactly like every other code-writing task in this project.

**Missing dependencies with fallback:**
- None beyond the above — there is no viable non-Windows fallback for verifying real tray/taskbar icon rendering; this is inherent to the phase's domain, not a gap this research can close.

## Security Domain

`security_enforcement` is not set to `false` anywhere in `.planning/config.json`; per protocol, treated as enabled. This phase has an unusually small attack surface — it reads/writes local files and draws static geometry; there is no user input, no network I/O, no authentication, and no secrets involved.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | N/A — no auth surface in this phase |
| V3 Session Management | No | N/A |
| V4 Access Control | No | N/A |
| V5 Input Validation | Marginally | The hand-rolled ICO writer/parser boundary (Pattern 3, Pitfall 4) is the closest thing to an "input" this phase touches — but the only "input" is the app's own procedurally-generated bitmaps, not external/untrusted data. The existing `LoadTrayIconsIfNeeded()` null-check pattern (throws `InvalidOperationException` with a descriptive message on a missing/malformed embedded resource) is the correct existing control and should be preserved unchanged for the new/regenerated resources. |
| V6 Cryptography | No | N/A — no cryptographic material involved |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Malformed/corrupt `.ico` embedded resource crashing the app at startup | Denial of Service (self-inflicted, not attacker-driven) | Already mitigated by the existing explicit null-check + descriptive exception in `LoadTrayIconsIfNeeded()`; extend the same care to the new `app.ico` build-time embedding path (a malformed `app.ico` would fail at compile time via the Win32 resource compiler, which is an even earlier/safer failure point than a runtime exception) |

This phase does not introduce any new trust boundary — all inputs to the icon-writing code are values the app itself computed (fractional geometry, hardcoded color constants), not externally supplied data.

## Sources

### Primary (HIGH confidence)
- Microsoft Learn — [Icon Constructor (System.Drawing)](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.icon.-ctor?view=windowsdesktop-9.0) — confirmed size-less constructors return the smallest frame; confirmed `Icon(Icon, Size)`/`Icon(Stream, Size)` overloads exist for explicit frame selection
- Microsoft Learn — [Common MSBuild Project Properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-properties) — confirmed `ApplicationIcon` property definition and semantics (`/win32icon` compiler switch equivalent)
- Wikipedia — [ICO (file format)](https://en.wikipedia.org/wiki/ICO_(file_format)) — confirmed exact `ICONDIR`/`ICONDIRENTRY` byte layout, the 0=256 width/height convention, and BMP-vs-PNG frame differences plus Vista as the PNG-support minimum version
- dotnet/winforms GitHub — [Issue #6955, "NotifyIcon does not use the appropriate icon size"](https://github.com/dotnet/winforms/issues/6955) — confirmed this is a real, open (unfixed as of research date), community-documented defect with the exact recommended workaround pattern used in this research

### Secondary (MEDIUM confidence)
- Edi Wang — [Generate a True ICO Format Image in .NET Core](https://edi.wang/post/2019/11/12/generate-a-true-ico-format-image-in-net-core) — corroborates that `System.Drawing`'s native `.Save()` with `ImageFormat.Icon` does not produce a valid multi-frame ICO, motivating the hand-rolled writer approach
- C# Helper — [Make multi-image icon files in C#](https://www.csharphelper.com/howtos/howto_make_icon.html) — corroborating community source for the general BMP-in-ICO packing technique
- WebSearch-aggregated GDI+ guidance on `SmoothingMode`/`PixelOffsetMode` for antialiased small-size rendering — general community consensus, partially corroborated by Microsoft's own "Antialiasing with Lines and Curves" doc

### Tertiary (LOW confidence)
- `jtippet/icotools` GitHub repository — WebFetch summary only, not independently read against actual source; cited solely as a rejected alternative, not a recommendation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new packages, confirmed via direct `.csproj` read that `System.Drawing` is already available through `UseWindowsForms=true`
- Architecture (drawing + ApplicationIcon wiring): HIGH — `ApplicationIcon` mechanism confirmed via official Microsoft docs; GDI+ drawing patterns are standard, well-documented BCL usage
- Architecture (hand-rolled ICO writer): MEDIUM — byte-format confirmed via Wikipedia + cross-referenced community sources, but not executed/tested against a real Windows/.NET runtime in this sandbox (none available); planner should schedule an explicit round-trip-load verification task, not rely on visual inspection alone
- Pitfalls: HIGH for Pitfall 1 (the DPI frame-selection defect) — verified against two independent official/authoritative sources (Microsoft Learn docs + dotnet/winforms issue tracker); MEDIUM for Pitfalls 2–4 (GDI+ antialiasing and ICO byte-layout specifics — plausible, cross-referenced, but not independently execution-verified here)

**Research date:** 2026-08-03
**Valid until:** ~90 days (stable, decades-old file format and long-standing .NET BCL APIs — low churn risk; re-verify only if .NET 11 changes `System.Drawing.Icon`'s public surface, which is not indicated by anything found during this research)
