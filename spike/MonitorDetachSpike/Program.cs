// MonitorDetachSpike — throwaway feasibility-spike console tool.
//
// Answers ONE question: does true OS-level monitor disable (CCD topology-path
// removal via WindowsDisplayAPI's PathInfo.ApplyPathInfos) actually work on this
// rig's AMD Radeon + DisplayPort hardware? Verification is cross-checked through
// two independent enumeration sources (WindowsDisplayAPI re-query AND
// System.Windows.Forms.Screen.AllScreens) and re-verified after a delay to catch
// DisplayPort hotplug re-detection (Pitfall C).
//
// This tool is non-elevated by construction (asInvoker, no manifest) and never
// invokes any elevated device-node disable/enable operation or command.
// The admin fallback path is documented separately (plan 01-02 FALLBACK.md) and
// is always run manually from a SEPARATE elevated terminal, never from here.

using System.Drawing;
using System.Text.Json;
using System.Threading;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;
using WindowsDisplayAPI.Exceptions;
using System.Windows.Forms;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

switch (args[0])
{
    case "--list":
        RunList();
        return 0;

    case "--disable":
        return RunDisable(args);

    case "--disable-primary":
        return RunDisablePrimary(args);

    case "--verify":
        RunVerify();
        return 0;

    default:
        PrintUsage();
        return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  MonitorDetachSpike --list");
    Console.WriteLine("  MonitorDetachSpike --disable <index>");
    Console.WriteLine("  MonitorDetachSpike --disable-primary <index>");
    Console.WriteLine("  MonitorDetachSpike --verify");
}

static void RunList()
{
    PathInfo[] activePaths = PathInfo.GetActivePaths(virtualModeAware: false);
    for (int i = 0; i < activePaths.Length; i++)
    {
        PathInfo path = activePaths[i];
        foreach (PathTargetInfo targetInfo in path.TargetsInfo)
        {
            PathDisplayTarget target = targetInfo.DisplayTarget;
            string friendlyName = target.FriendlyName ?? "(unavailable)";
            Console.WriteLine(
                $"[{i}] Target={friendlyName} " +
                $"DevicePath={target.DevicePath} " +
                $"IsGDIPrimary={path.IsGDIPrimary} " +
                $"OutputTechnology={targetInfo.OutputTechnology}");
        }
    }
}

static int RunDisable(string[] args)
{
    PathInfo[] originalActivePaths = PathInfo.GetActivePaths(virtualModeAware: false);

    if (args.Length < 2 || !int.TryParse(args[1], out int targetIndex) ||
        targetIndex < 0 || targetIndex >= originalActivePaths.Length)
    {
        int upperBound = originalActivePaths.Length - 1;
        Console.WriteLine(
            $"ERROR: invalid --disable index; valid range is 0..{upperBound} " +
            $"(there are {originalActivePaths.Length} active display paths). " +
            "Run --list first to see valid indices.");
        return 1;
    }

    PathInfo targetPath = originalActivePaths[targetIndex];
    string? targetDevicePath = targetPath.TargetsInfo.FirstOrDefault()?.DisplayTarget.DevicePath;

    // Capture state BEFORE mutating anything.
    int screenCountBefore = Screen.AllScreens.Length;

    // snapshot.json is a human-readable AUDIT TRAIL only. The real restore below
    // re-applies the in-memory originalActivePaths array directly — never a JSON
    // round-trip, since PathInfo does not deserialize cleanly from its ToString().
    File.WriteAllText(
        "snapshot.json",
        JsonSerializer.Serialize(originalActivePaths.Select(p => p.ToString()).ToArray()));

    PathInfo[] reducedPaths = originalActivePaths
        .Where((_, idx) => idx != targetIndex)
        .ToArray();

    // allowChanges: true is mandatory (not optional) here — it lets Windows
    // auto-promote a remaining display to primary (position 0,0) when the
    // removed path held the GDI-primary designation (Pitfall B).
    PathInfo.ApplyPathInfos(reducedPaths, allowChanges: true, saveToDatabase: false, forceModeEnumeration: false);

    Console.WriteLine("Applied reduced topology. Verifying (immediate check)...");
    VerifyOnce(targetDevicePath, screenCountBefore);

    Console.WriteLine("Waiting ~20 seconds to catch DisplayPort hotplug re-detection (Pitfall C)...");
    Thread.Sleep(20000);

    Console.WriteLine("Re-verifying after delay...");
    VerifyOnce(targetDevicePath, screenCountBefore);

    Console.WriteLine("Press Enter to RESTORE the original display topology...");
    Console.ReadLine();

    PathInfo.ApplyPathInfos(originalActivePaths, allowChanges: true);
    Console.WriteLine($"Restore applied. Screen.AllScreens.Length is now {Screen.AllScreens.Length}.");

    return 0;
}

static int RunDisablePrimary(string[] args)
{
    // RESEARCH Pattern 1 (Assumption A1): repositioning-aware primary removal.
    // The naive --disable approach (drop the target path, apply the rest
    // unchanged) throws PathChangeException when the target is the GDI primary,
    // because Windows requires a surviving display at (0,0) and PathInfo.Position
    // has no public setter to reposition one there first. This mode reconstructs
    // every survivor via the public PathInfo constructor, shifted by a uniform
    // delta so exactly one lands at (0,0).
    PathInfo[] originalActivePaths = PathInfo.GetActivePaths(virtualModeAware: false);

    if (args.Length < 2 || !int.TryParse(args[1], out int targetIndex) ||
        targetIndex < 0 || targetIndex >= originalActivePaths.Length)
    {
        int upperBound = originalActivePaths.Length - 1;
        Console.WriteLine(
            $"ERROR: invalid --disable-primary index; valid range is 0..{upperBound} " +
            $"(there are {originalActivePaths.Length} active display paths). " +
            "Run --list first to see valid indices.");
        return 1;
    }

    PathInfo targetPath = originalActivePaths[targetIndex];
    string? targetDevicePath = targetPath.TargetsInfo.FirstOrDefault()?.DisplayTarget.DevicePath;

    PathInfo[] survivors = originalActivePaths.Where(p => p != targetPath).ToArray();

    if (survivors.Length == 0)
    {
        Console.WriteLine("ERROR: cannot remove the only active display path (survivors.Length == 0).");
        return 1;
    }

    PathInfo[] rebuilt;
    if (targetPath.IsGDIPrimary)
    {
        // Shift ALL survivors by the same delta (not just survivors[0]) so their
        // relative layout (left/right/above/below) is preserved (Pitfall 1).
        Point promoted = survivors[0].Position;
        var delta = new Point(-promoted.X, -promoted.Y);

        rebuilt = survivors
            .Select(p => new PathInfo(
                p.DisplaySource,
                new Point(p.Position.X + delta.X, p.Position.Y + delta.Y),
                p.Resolution,
                p.PixelFormat,
                p.TargetsInfo))
            .ToArray();

        Console.WriteLine(
            $"Target is GDI primary. Shifting {survivors.Length} survivor(s) by delta " +
            $"({delta.X},{delta.Y}) so one lands at (0,0).");
    }
    else
    {
        rebuilt = survivors;
        Console.WriteLine("Target is not GDI primary; applying survivors unchanged.");
    }

    Console.WriteLine("Applying reduced+repositioned topology...");
    try
    {
        PathInfo.ApplyPathInfos(rebuilt, allowChanges: true, saveToDatabase: false, forceModeEnumeration: false);
    }
    catch (PathChangeException ex)
    {
        Console.WriteLine($"FAIL: ApplyPathInfos threw {ex.GetType().FullName}: {ex.Message}");
        Console.WriteLine("Assumption A1 did NOT hold for this hardware/index. Attempting restore of original topology...");
        try
        {
            PathInfo.ApplyPathInfos(originalActivePaths, allowChanges: true);
            Console.WriteLine("Restore after failure succeeded.");
        }
        catch (Exception restoreEx)
        {
            Console.WriteLine($"WARNING: restore-after-failure also threw {restoreEx.GetType().FullName}: {restoreEx.Message}");
        }
        return 1;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL: ApplyPathInfos threw unexpected {ex.GetType().FullName}: {ex.Message}");
        return 1;
    }

    Console.WriteLine("Applied. Verifying via PathInfo.GetActivePaths() (authoritative oracle, not Screen.AllScreens per D-04)...");

    PathInfo[] activeAfter = PathInfo.GetActivePaths(virtualModeAware: false);
    bool targetStillActive = activeAfter
        .SelectMany(p => p.TargetsInfo)
        .Any(t => t.DisplayTarget.DevicePath == targetDevicePath);
    int primaryCountAfter = activeAfter.Count(p => p.IsGDIPrimary);

    Console.WriteLine($"Target device path still active? {targetStillActive} (expect False)");
    Console.WriteLine($"Exactly one GDI primary after apply? {primaryCountAfter == 1} (count={primaryCountAfter}, expect 1)");
    Console.WriteLine($"[informational only, not the oracle] Screen.AllScreens.Length = {Screen.AllScreens.Length}");

    // A2 probe: confirm the disabled monitor remains discoverable for restore-time
    // re-attachment via GetAllPaths (includes inactive-but-connected paths).
    PathInfo[] allPaths = PathInfo.GetAllPaths(virtualModeAware: false);
    bool targetDiscoverableViaGetAllPaths = allPaths
        .SelectMany(p => p.TargetsInfo)
        .Any(t => t.DisplayTarget.DevicePath == targetDevicePath);
    Console.WriteLine($"Target still discoverable via GetAllPaths (A2)? {targetDiscoverableViaGetAllPaths} (expect True)");

    Console.WriteLine("Press Enter to RESTORE the original display topology...");
    Console.ReadLine();

    PathInfo.ApplyPathInfos(originalActivePaths, allowChanges: true);
    int activeCountAfterRestore = PathInfo.GetActivePaths(virtualModeAware: false).Length;
    Console.WriteLine($"Restore applied. Active path count is now {activeCountAfterRestore} (expected {originalActivePaths.Length}).");

    return 0;
}

static void VerifyOnce(string? targetDevicePath, int screenCountBefore)
{
    bool stillActive = PathInfo.GetActivePaths()
        .SelectMany(p => p.TargetsInfo)
        .Any(t => t.DisplayTarget.DevicePath == targetDevicePath);

    int screenCountAfter = Screen.AllScreens.Length;

    bool verified = !stillActive && screenCountAfter < screenCountBefore;

    if (verified)
    {
        Console.WriteLine("PASS: monitor removed from BOTH WindowsDisplayAPI and Screen.AllScreens enumeration.");
    }
    else
    {
        Console.WriteLine("FAIL: monitor still detected by at least one enumeration source.");
        if (stillActive)
        {
            Console.WriteLine("  - WindowsDisplayAPI re-query still reports the target device path active.");
        }
        if (screenCountAfter >= screenCountBefore)
        {
            Console.WriteLine($"  - Screen.AllScreens.Length did not drop (before={screenCountBefore}, after={screenCountAfter}).");
        }
    }
}

static void RunVerify()
{
    int activePathCount = PathInfo.GetActivePaths().Length;
    int screenCount = Screen.AllScreens.Length;
    Console.WriteLine($"Active display paths (WindowsDisplayAPI): {activePathCount}");
    Console.WriteLine($"Screen.AllScreens.Length: {screenCount}");
}
