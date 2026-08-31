using WindowsDisplayAPI.DisplayConfig;
using WindowsDisplayAPI.Native.DisplayConfig;

// Standalone diagnostic tool. Deliberately bypasses RigToggle entirely -- no correction loop,
// no retry, no RigToggle.App process or WinForms message pump. Calls the exact same
// WindowsDisplayAPI 1.3.0.13 primitives WindowsMonitorController uses, in complete isolation,
// to answer one question: does whole-topology PathInfo.ApplyTopology(Extend) succeed or fail
// for a given monitor when nothing else is involved at all?
//
// Usage:
//   DisplayProbe list
//   DisplayProbe extend [device-path-substring]

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

switch (args[0].ToLowerInvariant())
{
    case "list":
        ListPaths();
        return 0;

    case "extend":
        RunExtend(args.Length > 1 ? args[1] : null);
        return 0;

    default:
        PrintUsage();
        return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  DisplayProbe list");
    Console.WriteLine("  DisplayProbe extend [device-path-substring]");
}

static void ListPaths()
{
    PathInfo[] allPaths = PathInfo.GetAllPaths(virtualModeAware: false);
    HashSet<string> activeDevicePaths = PathInfo.GetActivePaths(virtualModeAware: false)
        .SelectMany(p => p.TargetsInfo)
        .Select(t => t.DisplayTarget.DevicePath)
        .ToHashSet();

    Console.WriteLine($"GetAllPaths() returned {allPaths.Length} path(s) (includes stale/historical CCD database entries -- most targets will be unavailable; that's normal).");
    Console.WriteLine();

    int unavailableCount = 0;

    foreach (PathInfo path in allPaths)
    {
        foreach (PathTargetInfo target in path.TargetsInfo)
        {
            // IsAvailable MUST be checked before touching DevicePath/FriendlyName -- both throw
            // TargetNotAvailableException on an unavailable target (WindowsDisplayAPI 1.3.0.13).
            // Mirrors the same discipline WindowsMonitorController.cs's own GetAllMonitors() uses.
            if (!target.DisplayTarget.IsAvailable)
            {
                unavailableCount++;
                continue;
            }

            string devicePath = target.DisplayTarget.DevicePath;
            string friendlyName = target.DisplayTarget.FriendlyName ?? "(unknown display)";
            bool isActive = activeDevicePaths.Contains(devicePath);

            Console.WriteLine($"  DevicePath : {devicePath}");
            Console.WriteLine($"  Friendly   : {friendlyName}");
            Console.WriteLine($"  Available  : True");
            Console.WriteLine($"  PathActive : {target.IsPathActive}");
            Console.WriteLine($"  LiveActive : {isActive}  (in GetActivePaths() result)");
            Console.WriteLine($"  Source     : {path.DisplaySource}");
            Console.WriteLine();
        }
    }

    Console.WriteLine($"({unavailableCount} unavailable/stale target(s) skipped)");
}

static void RunExtend(string? targetSubstring)
{
    PathInfo[] before = PathInfo.GetActivePaths(virtualModeAware: false);
    HashSet<string> beforeDevicePaths = before
        .SelectMany(p => p.TargetsInfo)
        .Select(t => t.DisplayTarget.DevicePath)
        .ToHashSet();

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Active BEFORE: [{string.Join(", ", beforeDevicePaths)}]");
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Calling PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false) -- raw, single call, no correction/retry...");

    try
    {
        PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ApplyTopology THREW: {ex.GetType().Name}: {ex.Message}");
        return;
    }

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ApplyTopology returned without throwing. Polling active paths every 250ms for up to 5 attempts (matching RigToggle's own settle-poll cadence, but with NO correction applied)...");

    HashSet<string> afterDevicePaths = new();
    for (int attempt = 1; attempt <= 5; attempt++)
    {
        Thread.Sleep(250);
        PathInfo[] settled = PathInfo.GetActivePaths(virtualModeAware: false);
        afterDevicePaths = settled
            .SelectMany(p => p.TargetsInfo)
            .Select(t => t.DisplayTarget.DevicePath)
            .ToHashSet();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Attempt {attempt}/5: [{string.Join(", ", afterDevicePaths)}]");
    }

    Console.WriteLine();
    Console.WriteLine($"Active BEFORE: [{string.Join(", ", beforeDevicePaths)}]");
    Console.WriteLine($"Active AFTER : [{string.Join(", ", afterDevicePaths)}]");

    IEnumerable<string> newlyActive = afterDevicePaths.Except(beforeDevicePaths);
    IEnumerable<string> newlyInactive = beforeDevicePaths.Except(afterDevicePaths);
    Console.WriteLine($"Newly ACTIVE : [{string.Join(", ", newlyActive)}]");
    Console.WriteLine($"Newly INACTIVE (dropped as a side effect): [{string.Join(", ", newlyInactive)}]");

    if (!string.IsNullOrEmpty(targetSubstring))
    {
        bool matchedBefore = beforeDevicePaths.Any(dp => dp.Contains(targetSubstring, StringComparison.OrdinalIgnoreCase));
        bool matchedAfter = afterDevicePaths.Any(dp => dp.Contains(targetSubstring, StringComparison.OrdinalIgnoreCase));
        Console.WriteLine();
        Console.WriteLine($"Target substring '{targetSubstring}': active before={matchedBefore}, active after={matchedAfter} -> {(matchedAfter ? "SUCCEEDED" : "FAILED to activate")}");
    }
}
