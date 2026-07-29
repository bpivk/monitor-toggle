using System.Threading;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Tests.Doubles;

/// <summary>
/// Test-only IMonitorController double whose DeactivateMonitors blocks on a signal,
/// enabling a deterministic (non-timing-based) reentrancy test for ToggleOrchestrator
/// (07-RESEARCH.md Pitfall 2). Kept as its own small file rather than folded into
/// FakeMonitorController (07-RESEARCH.md Open Question #2) since this blocking
/// behavior is needed by exactly one test and would otherwise bloat
/// FakeMonitorController's already multi-flag constructor.
///
/// DeactivateMonitors signals <see cref="_enteredGuardedRegion"/> to tell the test "the
/// first call has genuinely entered the guarded region," then blocks on
/// <see cref="_releaseFirstCall"/> until the test explicitly releases it. The other
/// five IMonitorController members return the same minimal fixture values
/// FakeMonitorController uses.
/// </summary>
public sealed class BlockingMonitorController : IMonitorController
{
    private readonly ManualResetEventSlim _enteredGuardedRegion;
    private readonly ManualResetEventSlim _releaseFirstCall;

    public BlockingMonitorController(ManualResetEventSlim enteredGuardedRegion, ManualResetEventSlim releaseFirstCall)
    {
        _enteredGuardedRegion = enteredGuardedRegion;
        _releaseFirstCall = releaseFirstCall;
    }

    public IReadOnlyList<MonitorInfo> GetActiveMonitors() =>
        new List<MonitorInfo> { new("\\\\?\\DISPLAY#FAKE", "Fake Monitor", true, IsActive: true) };

    public IReadOnlyList<MonitorInfo> GetAllMonitors() =>
        new List<MonitorInfo> { new("\\\\?\\DISPLAY#FAKE", "Fake Monitor", true, IsActive: true) };

    public MonitorState CaptureState() =>
        new(
            new List<MonitorPathSnapshot> { new("\\\\?\\DISPLAY#FAKE", "Fake Monitor", 0, 0, 1920, 1080, 0, 0, 0, 0, 60000UL, 0, true) },
            "\\\\?\\DISPLAY#FAKE");

    public void ActivateMonitors(IReadOnlySet<string> monitorDevicePaths)
    {
        // No-op — the reentrancy test only needs DeactivateMonitors to block.
    }

    public void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths)
    {
        _enteredGuardedRegion.Set();
        _releaseFirstCall.Wait();
    }

    public void Restore(MonitorState previousState)
    {
        // No-op — not exercised by the reentrancy test.
    }
}
