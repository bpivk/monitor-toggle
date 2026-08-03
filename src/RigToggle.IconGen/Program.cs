using System.Drawing;

namespace RigToggle.IconGen;

/// <summary>
/// Dev-time-only console entry point. Generates the three checked-in .ico assets
/// under src/RigToggle.App/Resources/ from the procedural geometry in
/// IconGeometry.cs, packed via IconWriter.cs. Never referenced by RigToggle.App --
/// this project is not part of the shipped self-contained publish (13-RESEARCH.md
/// "Architectural Responsibility Map").
///
/// Writes are atomic (.tmp + File.Move(overwrite: true), mirroring
/// JsonSettingsStore.Save's shape) so an interrupted run never leaves a
/// half-written .ico checked into git. After each file is written, every
/// embedded frame is round-trip-verified (13-RESEARCH.md Assumption A4) -- this
/// is the automated gate that catches malformed ICO byte layout (Pitfall 3/4)
/// before any human visual-verification step.
///
/// Pitfall 6 (discovered during Task 3 execution): System.Drawing.Icon's sized
/// constructor (new Icon(path, new Size(s, s))) applies the ICO format's "0 byte
/// means 256" convention (Pitfall 3) literally as 0 during its best-fit frame
/// SELECTION logic -- confirmed by direct empirical testing (requesting sizes
/// 48/64/100/200/256/300/1000 against a correctly-encoded 256px PNG-in-ICO frame
/// all return the 48px frame instead). This reproduces regardless of whether the
/// 256px frame is BMP- or PNG-encoded, so it is a distinct, deeper defect than
/// Pitfall 3/5's byte-encoding concerns -- the file bytes are correct (confirmed
/// via manual ICONDIRENTRY + PNG IHDR inspection), but this specific .NET API
/// cannot select a 256px frame by request, full stop. This does not affect real
/// usage: app.ico's only production consumer is the Win32 resource compiler via
/// <ApplicationIcon> plus native shell icon loading (RESEARCH.md Pattern 4), never
/// System.Drawing.Icon; normal.ico/rig.ico never embed a frame >= 256px, so their
/// MainForm.LoadTrayIconsIfNeeded() sized-constructor usage is unaffected. Sizes
/// >= the PNG-encoding threshold are therefore verified here by parsing the raw
/// ICO container directly and decoding that frame's own image bytes (bypassing
/// Icon's broken selection heuristic entirely), while smaller sizes keep using
/// the sized Icon constructor (confirmed correct for 16-48px by the same testing).
/// </summary>
internal static class Program
{
    // WR-01: covers 100/125/150/175/200/225/250/300% DPI scale factors --
    // SystemInformation.SmallIconSize can report any of these depending on the
    // active display scaling.
    private static readonly int[] TraySizes = { 16, 20, 24, 28, 32, 36, 40, 48 };
    private static readonly int[] AppSizes = { 16, 20, 24, 32, 48, 256 };

    private const string ResourcesRelativePath = "../RigToggle.App/Resources";

    // CR-01: the 16px frame is the primary tray-display size and the size the
    // interior-artifact pixel diagnostic gates on -- 20/24/28/32/36/40/48px are
    // not separately gated since the geometry driving all frames is identical
    // (same fractional coordinates redrawn at each size), only the rasterized
    // pixel grid differs, and 16px is both the smallest (least anti-aliasing
    // slack) and the most-viewed size in practice.
    private const int InteriorArtifactCheckSize = 16;

    [STAThread]
    static void Main(string[] args)
    {
        bool ok = true;

        ok &= GenerateIcon("normal.ico", TraySizes, IconGeometry.DrawNormalIcon, checkInteriorArtifacts: true);
        ok &= GenerateIcon("rig.ico", TraySizes, IconGeometry.DrawRigIcon, checkInteriorArtifacts: true);
        ok &= GenerateIcon("app.ico", AppSizes, IconGeometry.DrawAppIcon, checkInteriorArtifacts: false);

        if (!ok)
        {
            Console.Error.WriteLine("One or more icons failed round-trip self-verification.");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Draws every required frame for one icon, packs it via IconWriter, writes
    /// it atomically, then round-trip-verifies every embedded size. When
    /// <paramref name="checkInteriorArtifacts"/> is true (tray icons only --
    /// app.ico is fill-only and out of scope for CR-01), also runs the
    /// interior-artifact pixel diagnostic on the 16px frame AFTER the round-trip
    /// gate passes. Returns false (and prints a diagnostic) if any step fails.
    /// </summary>
    private static bool GenerateIcon(string fileName, int[] sizes, Func<int, Bitmap> draw, bool checkInteriorArtifacts)
    {
        var frames = new List<Bitmap>();
        try
        {
            foreach (var size in sizes)
            {
                frames.Add(draw(size));
            }

            // `dotnet run --project src/RigToggle.IconGen` (and `dotnet run` invoked
            // from inside the project directory) both set the working directory to
            // the project's own directory -- no launchSettings.json overrides that
            // here -- so a path relative to Environment.CurrentDirectory resolves to
            // the sibling RigToggle.App project regardless of where `dotnet` itself
            // was launched from.
            var outputDir = Path.Combine(Environment.CurrentDirectory, ResourcesRelativePath);
            var path = Path.GetFullPath(Path.Combine(outputDir, fileName));
            var tempPath = path + ".tmp";

            using (var ms = new MemoryStream())
            {
                IconWriter.WriteIco(ms, frames);
                File.WriteAllBytes(tempPath, ms.ToArray());
            }

            File.Move(tempPath, path, overwrite: true);

            if (!VerifyRoundTrip(path, sizes))
            {
                return false;
            }

            if (checkInteriorArtifacts && !VerifyNoInteriorArtifacts(path, InteriorArtifactCheckSize))
            {
                return false;
            }

            Console.WriteLine($"Wrote {path} ({frames.Count} frames: {string.Join(", ", sizes)}px) -- round-trip verified.");
            return true;
        }
        finally
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }
        }
    }

    /// <summary>
    /// Round-trip-verifies every requested size in the written .ico file. Sizes
    /// below IconWriter.PngFrameThreshold (WR-02: single shared source of truth,
    /// no longer a locally duplicated constant) use the sized Icon constructor
    /// (matches the exact API MainForm.LoadTrayIconsIfNeeded() uses at runtime);
    /// larger sizes are verified by decoding that frame's raw bytes directly,
    /// bypassing Icon's confirmed frame-selection defect at 256px (Pitfall 6).
    /// </summary>
    private static bool VerifyRoundTrip(string path, int[] sizes)
    {
        foreach (var size in sizes)
        {
            if (size >= IconWriter.PngFrameThreshold)
            {
                if (!VerifyLargeFrameByDirectDecode(path, size))
                {
                    return false;
                }

                continue;
            }

            try
            {
                using var icon = new System.Drawing.Icon(path, new Size(size, size));
                if (icon.Width != size || icon.Height != size)
                {
                    Console.Error.WriteLine(
                        $"Round-trip FAILED for {path} at requested size {size}px: " +
                        $"loaded icon reports {icon.Width}x{icon.Height}.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Round-trip FAILED for {path} at requested size {size}px: {ex.Message}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Verifies a >=256px frame by parsing the raw ICO container's ICONDIR/
    /// ICONDIRENTRY table directly (applying the format's "0 byte means 256"
    /// convention ourselves, correctly, unlike System.Drawing.Icon's sized
    /// constructor -- Pitfall 6), locating the matching entry's frame bytes, and
    /// decoding them via Image.FromStream to assert the actual embedded pixel
    /// dimensions. This exercises the same PNG bytes IconWriter produced without
    /// going through Icon's broken large-frame selection heuristic.
    /// </summary>
    private static bool VerifyLargeFrameByDirectDecode(string path, int size)
    {
        byte[] fileBytes;
        try
        {
            fileBytes = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Round-trip FAILED for {path} at requested size {size}px: could not read file ({ex.Message}).");
            return false;
        }

        using var reader = new BinaryReader(new MemoryStream(fileBytes));

        reader.ReadUInt16(); // reserved
        reader.ReadUInt16(); // type
        ushort count = reader.ReadUInt16();

        for (int i = 0; i < count; i++)
        {
            byte widthByte = reader.ReadByte();
            byte heightByte = reader.ReadByte();
            reader.ReadByte();   // color count
            reader.ReadByte();   // reserved
            reader.ReadUInt16(); // planes
            reader.ReadUInt16(); // bit count
            uint entrySize = reader.ReadUInt32();
            uint entryOffset = reader.ReadUInt32();

            // ICO format convention: a 0 byte value means 256 (Pitfall 3), applied
            // here explicitly and correctly, unlike Icon's sized constructor.
            int entryWidth = widthByte == 0 ? 256 : widthByte;
            int entryHeight = heightByte == 0 ? 256 : heightByte;

            if (entryWidth != size || entryHeight != size)
            {
                continue;
            }

            try
            {
                using var frameStream = new MemoryStream(fileBytes, (int)entryOffset, (int)entrySize);
                using var image = Image.FromStream(frameStream);
                if (image.Width != size || image.Height != size)
                {
                    Console.Error.WriteLine(
                        $"Round-trip FAILED for {path} at requested size {size}px: " +
                        $"decoded frame reports {image.Width}x{image.Height}.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Round-trip FAILED for {path} at requested size {size}px: could not decode frame ({ex.Message}).");
                return false;
            }
        }

        Console.Error.WriteLine($"Round-trip FAILED for {path} at requested size {size}px: no matching ICONDIRENTRY found.");
        return false;
    }

    /// <summary>
    /// CR-01 gate: decodes the requested frame of a tray icon and asserts there
    /// is no small enclosed transparent region -- the pixel-level signature of
    /// an unfixed seam artifact (an interior black outline stroke not fully
    /// overpainted by the fill) or a stray hole. This is a genuinely deeper
    /// check than VerifyRoundTrip: the round-trip gate only proves the ICO
    /// container parses and reports the right dimensions, it says nothing about
    /// whether the rendered silhouette is visually clean (13-REVIEW.md CR-01 --
    /// the byte-level check above did not catch the original seam defect).
    ///
    /// Loads the frame via `new Icon(path, new Size(size, size)).ToBitmap()` --
    /// deliberately NOT via Image.FromStream/new Bitmap on the raw ICONDIRENTRY
    /// bytes, because sub-256px tray frames are BMP-in-ICO (a raw DIB with no
    /// BITMAPFILEHEADER), not PNG, and only the >=256px app.ico frames
    /// (verified by VerifyLargeFrameByDirectDecode) are PNG-encoded.
    ///
    /// Algorithm: mark every pixel with alpha &lt; 128 as "transparent". Flood-fill
    /// (4-connected) outward from every transparent border pixel to mark
    /// exterior-reachable transparent pixels -- these are legitimate background/
    /// negative-space regions (the wheel's ring cutout, the gaps between
    /// spokes, all of which touch the frame's edge or connect to it). Any
    /// transparent pixel NOT reached by that flood-fill is "enclosed" --
    /// completely surrounded by opaque pixels, which never happens for an
    /// intended design feature at 16px (the smallest genuine transparent gap,
    /// a between-spoke sector, is far larger and always touches the outer
    /// background). Enclosed transparent pixels are grouped into 4-connected
    /// components; any component with area &lt;= 2px fails the gate. Always prints
    /// an ASCII grid ('#' = opaque/alpha&gt;=128, '.' = transparent) plus the
    /// enclosed-component count and each component's area, so the result is
    /// visually inspectable in the generator's own console output.
    /// </summary>
    private static bool VerifyNoInteriorArtifacts(string path, int size)
    {
        Bitmap frame;
        try
        {
            using var icon = new System.Drawing.Icon(path, new Size(size, size));
            frame = icon.ToBitmap();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Interior-artifact check FAILED for {path} at {size}px: could not load frame ({ex.Message}).");
            return false;
        }

        using (frame)
        {
            int w = frame.Width, h = frame.Height;
            var transparent = new bool[w, h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    transparent[x, y] = frame.GetPixel(x, y).A < 128;
                }
            }

            // Flood-fill outward from every border transparent pixel to mark
            // exterior-reachable transparent regions.
            var exteriorReachable = new bool[w, h];
            var floodQueue = new Queue<(int x, int y)>();

            void SeedBorder(int x, int y)
            {
                if (transparent[x, y] && !exteriorReachable[x, y])
                {
                    exteriorReachable[x, y] = true;
                    floodQueue.Enqueue((x, y));
                }
            }

            for (int x = 0; x < w; x++)
            {
                SeedBorder(x, 0);
                SeedBorder(x, h - 1);
            }

            for (int y = 0; y < h; y++)
            {
                SeedBorder(0, y);
                SeedBorder(w - 1, y);
            }

            while (floodQueue.Count > 0)
            {
                var (x, y) = floodQueue.Dequeue();

                foreach (var (nx, ny) in Neighbors4(x, y, w, h))
                {
                    if (transparent[nx, ny] && !exteriorReachable[nx, ny])
                    {
                        exteriorReachable[nx, ny] = true;
                        floodQueue.Enqueue((nx, ny));
                    }
                }
            }

            // Group enclosed (transparent && not exterior-reachable) pixels into
            // 4-connected components.
            var visited = new bool[w, h];
            var componentAreas = new List<int>();

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!transparent[x, y] || exteriorReachable[x, y] || visited[x, y])
                    {
                        continue;
                    }

                    int area = 0;
                    var componentQueue = new Queue<(int x, int y)>();
                    componentQueue.Enqueue((x, y));
                    visited[x, y] = true;

                    while (componentQueue.Count > 0)
                    {
                        var (cx, cy) = componentQueue.Dequeue();
                        area++;

                        foreach (var (nx, ny) in Neighbors4(cx, cy, w, h))
                        {
                            if (transparent[nx, ny] && !exteriorReachable[nx, ny] && !visited[nx, ny])
                            {
                                visited[nx, ny] = true;
                                componentQueue.Enqueue((nx, ny));
                            }
                        }
                    }

                    componentAreas.Add(area);
                }
            }

            // Always print the ASCII grid + component summary, pass or fail --
            // this is the human-inspectable reference the rig checkpoint (Task 3)
            // and any future debugging session can compare against.
            Console.WriteLine($"Interior-artifact grid for {Path.GetFileName(path)} at {size}px ('#'=opaque, '.'=transparent):");
            var grid = new System.Text.StringBuilder();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    grid.Append(transparent[x, y] ? '.' : '#');
                }

                grid.Append('\n');
            }

            Console.Write(grid.ToString());
            Console.WriteLine(componentAreas.Count == 0
                ? "Enclosed transparent components: 0."
                : $"Enclosed transparent components: {componentAreas.Count} (areas: {string.Join(", ", componentAreas)}).");

            var failingComponents = componentAreas.Where(a => a <= 2).ToList();
            if (failingComponents.Count > 0)
            {
                Console.Error.WriteLine(
                    $"Interior-artifact check FAILED for {path} at {size}px: " +
                    $"{failingComponents.Count} enclosed transparent component(s) with area <= 2px " +
                    $"(areas: {string.Join(", ", failingComponents)}) -- this is the pixel-level signature " +
                    "of a seam artifact (CR-01).");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Yields the in-bounds 4-connected neighbors of (x, y) within a w x h grid.
    /// </summary>
    private static IEnumerable<(int x, int y)> Neighbors4(int x, int y, int w, int h)
    {
        if (x > 0) yield return (x - 1, y);
        if (x < w - 1) yield return (x + 1, y);
        if (y > 0) yield return (x, y - 1);
        if (y < h - 1) yield return (x, y + 1);
    }
}
