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

using System.Text.Json;
using System.Threading;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;
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
