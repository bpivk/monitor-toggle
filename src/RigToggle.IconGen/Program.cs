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
/// embedded frame is round-trip-loaded via new Icon(path, new Size(s, s))
/// and its reported size asserted (13-RESEARCH.md Assumption A4) -- this is
/// the automated gate that catches malformed ICO byte layout (Pitfall 3/4)
/// before any human visual-verification step.
/// </summary>
internal static class Program
{
    private static readonly int[] TraySizes = { 16, 20, 24, 32 };
    private static readonly int[] AppSizes = { 16, 20, 24, 32, 48, 256 };

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
    /// Loads the generated .ico at every required size via the sized Icon
    /// constructor and asserts the reported dimensions match what was
    /// requested. This is the automated gate (Assumption A4) that catches
    /// malformed byte layout before relying on visual inspection.
    /// </summary>
    private static bool VerifyRoundTrip(string path, int[] sizes)
    {
        foreach (var size in sizes)
        {
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
}
