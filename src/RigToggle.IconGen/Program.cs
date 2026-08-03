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
    private static readonly int[] TraySizes = { 16, 20, 24, 32 };
    private static readonly int[] AppSizes = { 16, 20, 24, 32, 48, 256 };

    /// <summary>
    /// Sizes at or above this threshold cannot be reliably round-trip-verified via
    /// System.Drawing.Icon's sized constructor (Pitfall 6) -- verified instead by
    /// direct raw-frame decode. Matches IconWriter's PNG-encoding threshold.
    /// </summary>
    private const int LargeFrameVerificationThreshold = 256;

    private const string ResourcesRelativePath = "../RigToggle.App/Resources";

    [STAThread]
    static void Main(string[] args)
    {
        bool ok = true;

        ok &= GenerateIcon("normal.ico", TraySizes, IconGeometry.DrawNormalIcon);
        ok &= GenerateIcon("rig.ico", TraySizes, IconGeometry.DrawRigIcon);
        ok &= GenerateIcon("app.ico", AppSizes, IconGeometry.DrawAppIcon);

        if (!ok)
        {
            Console.Error.WriteLine("One or more icons failed round-trip self-verification.");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Draws every required frame for one icon, packs it via IconWriter, writes
    /// it atomically, then round-trip-verifies every embedded size. Returns
    /// false (and prints a diagnostic) if any step fails.
    /// </summary>
    private static bool GenerateIcon(string fileName, int[] sizes, Func<int, Bitmap> draw)
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
    /// below LargeFrameVerificationThreshold use the sized Icon constructor
    /// (matches the exact API MainForm.LoadTrayIconsIfNeeded() uses at runtime);
    /// larger sizes are verified by decoding that frame's raw bytes directly,
    /// bypassing Icon's confirmed frame-selection defect at 256px (Pitfall 6).
    /// </summary>
    private static bool VerifyRoundTrip(string path, int[] sizes)
    {
        foreach (var size in sizes)
        {
            if (size >= LargeFrameVerificationThreshold)
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
}
