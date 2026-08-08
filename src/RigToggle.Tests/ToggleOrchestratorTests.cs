using System.Threading;
using System.Threading.Tasks;
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using RigToggle.Tests.Doubles;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves ToggleOrchestrator's non-blocking single-flight guard (CORE-06): same- and
/// cross-direction reentrancy rejection (D-01/D-02), flag release after a preflight
/// exception (Pitfall 3), unguarded pass-throughs (D-04), the exception-type
/// contract (D-05) that lets MainForm's existing catch block surface a busy-rejection
/// with zero UI changes, and the DISPLAY-13 crash-marker lifecycle wrapping every
/// guarded toggle. All deterministic — no fixed-duration waits, no timing guess
/// (07-RESEARCH.md Pitfall 2).
/// </summary>
public class ToggleOrchestratorTests : IDisposable
{
    private readonly string ExistingCompanionAppPath = Path.GetTempFileName();

    private readonly AppSettings ConfiguredSettings;
    private readonly AppSettings UnconfiguredSettings;

    public ToggleOrchestratorTests()
    {
        ConfiguredSettings = new AppSettings
        {
            MonitorDevicePath = "\\\\?\\DISPLAY#PRIMARY",
            MonitorFriendlyName = "Primary Monitor",
            MonitorsToDisable = new List<string> { "\\\\?\\DISPLAY#PRIMARY" },
            NormalMonitorsToDisable = new List<string> { "\\\\?\\DISPLAY#PRIMARY" },
            NormalAudioDeviceId = "normal-device-id",
            NormalAudioDeviceName = "Headset",
            RigAudioDeviceId = "rig-device-id",
            RigAudioDeviceName = "Rig Speakers",
            CompanionAppPath = ExistingCompanionAppPath,
        };

        UnconfiguredSettings = new AppSettings();
    }

    public void Dispose() => File.Delete(ExistingCompanionAppPath);

    private (ToggleOrchestrator Orchestrator, List<string> CallLog, InMemorySettingsStore SettingsStore) CreateOrchestrator(
        AppSettings? settings = null,
        IMonitorController? monitorController = null,
        IToggleInProgressStore? markerStore = null)
    {
        var callLog = new List<string>();
        var settingsStore = new InMemorySettingsStore(settings ?? ConfiguredSettings);
        var modeStore = new InMemoryModeStore(callLog);
        var marker = markerStore ?? new InMemoryToggleInProgressStore(callLog);
        var monitor = monitorController ?? new FakeMonitorController(callLog);
        var audioController = new FakeAudioController(callLog);
        var appController = new FakeAppController(callLog);

        var toggleService = new ToggleService(settingsStore, modeStore, monitor, audioController, appController);
        var orchestrator = new ToggleOrchestrator(toggleService, marker);
        return (orchestrator, callLog, settingsStore);
    }

    [Fact]
    public void ToggleToRigMode_Idle_DelegatesToToggleServiceAndReturnsItsResult()
    {
        var (orchestrator, callLog, _) = CreateOrchestrator();

        var result = orchestrator.ToggleToRigMode();

        Assert.True(result.Success);
        Assert.Contains(callLog, entry => entry.StartsWith("app.LaunchOrFocus"));
        Assert.True(orchestrator.IsInRigMode());
    }

    [Fact]
    public void ToggleToNormalMode_Idle_DelegatesToToggleServiceAndReturnsItsResult()
    {
        // DISPLAY-10: ToggleToNormalMode no longer restores from a snapshot — it
        // applies its own explicit NormalMonitorsToDisable/ToEnable set via the same
        // ActivateMonitors/DeactivateMonitors primitives Rig mode uses.
        var (orchestrator, callLog, _) = CreateOrchestrator();
        orchestrator.ToggleToRigMode();
        callLog.Clear();

        var result = orchestrator.ToggleToNormalMode();

        Assert.True(result.Success);
        Assert.Contains(callLog, entry => entry.StartsWith("monitor.ActivateMonitors"));
        Assert.Contains(callLog, entry => entry.StartsWith("monitor.DeactivateMonitors"));
        Assert.False(orchestrator.IsInRigMode());
    }

    /// <summary>
    /// WR-03 (code review): 5s bound on every guarded-region wait. If a regression ever
    /// makes ToggleToRigMode() fail before reaching the blocking Monitor step, the wait
    /// fails fast with a clear assertion message instead of hanging the test run forever.
    /// </summary>
    private static readonly TimeSpan GuardedRegionWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void ToggleToRigMode_RejectsSecondCallWhileFirstInFlight_SameDirection()
    {
        using var enteredGuardedRegion = new ManualResetEventSlim(false);
        using var releaseFirstCall = new ManualResetEventSlim(false);
        var (orchestrator, _, _) = CreateOrchestrator(
            monitorController: new BlockingMonitorController(enteredGuardedRegion, releaseFirstCall));

        var firstCallTask = Task.Run(() => orchestrator.ToggleToRigMode());
        Assert.True(enteredGuardedRegion.Wait(GuardedRegionWaitTimeout), "Blocking step was never entered.");

        try
        {
            // WR-03: releaseFirstCall.Set() must run even if this assertion fails,
            // otherwise a broken guard orphans the background Task.Run thread forever
            // instead of just failing this one assertion.
            Assert.Throws<ToggleInProgressException>(() => orchestrator.ToggleToRigMode());
        }
        finally
        {
            releaseFirstCall.Set();
        }

        var firstResult = firstCallTask.GetAwaiter().GetResult();
        Assert.True(firstResult.Success);
    }

    [Fact]
    public void ToggleToRigMode_InFlight_RejectsCrossDirectionToggleToNormalMode()
    {
        // D-02: one shared flag guards BOTH directions — a rig-mode toggle in flight
        // must also reject a normal-mode request, not just a same-direction repeat.
        using var enteredGuardedRegion = new ManualResetEventSlim(false);
        using var releaseFirstCall = new ManualResetEventSlim(false);
        var (orchestrator, _, _) = CreateOrchestrator(
            monitorController: new BlockingMonitorController(enteredGuardedRegion, releaseFirstCall));

        var firstCallTask = Task.Run(() => orchestrator.ToggleToRigMode());
        Assert.True(enteredGuardedRegion.Wait(GuardedRegionWaitTimeout), "Blocking step was never entered.");

        try
        {
            Assert.Throws<ToggleInProgressException>(() => orchestrator.ToggleToNormalMode());
        }
        finally
        {
            releaseFirstCall.Set();
        }

        var firstResult = firstCallTask.GetAwaiter().GetResult();
        Assert.True(firstResult.Success);
    }

    [Fact]
    public void IsInRigMode_And_IsSettingsConfigured_ArePassThroughs_CallableWhileToggleInFlight()
    {
        // D-04: pure reads, unguarded — must remain callable (and correct) while a
        // toggle is in flight, mirroring how MainForm.RefreshUi() calls IsInRigMode()
        // immediately after every toggle today.
        using var enteredGuardedRegion = new ManualResetEventSlim(false);
        using var releaseFirstCall = new ManualResetEventSlim(false);
        var (orchestrator, _, _) = CreateOrchestrator(
            monitorController: new BlockingMonitorController(enteredGuardedRegion, releaseFirstCall));

        var firstCallTask = Task.Run(() => orchestrator.ToggleToRigMode());
        Assert.True(enteredGuardedRegion.Wait(GuardedRegionWaitTimeout), "Blocking step was never entered.");

        try
        {
            // Pattern 2 (16-RESEARCH.md): the mode flag is now written only AFTER the
            // Monitor step succeeds — the blocking double blocks INSIDE
            // DeactivateMonitors, so the Monitor step has NOT completed yet and the
            // mode flag has NOT flipped to Rig. IsInRigMode() must still be safely
            // callable (and honestly report false) mid-mutation.
            Assert.False(orchestrator.IsInRigMode());
            Assert.True(orchestrator.IsSettingsConfigured());
        }
        finally
        {
            releaseFirstCall.Set();
        }

        var firstResult = firstCallTask.GetAwaiter().GetResult();
        Assert.True(firstResult.Success);

        // Only after the blocked call completes successfully does the mode flag
        // flip, proving the pass-through's value tracks confirmed success.
        Assert.True(orchestrator.IsInRigMode());
    }

    [Fact]
    public void RunGuarded_ReleasesFlag_AfterPreflightException()
    {
        // Pitfall 3: a preflight InvalidOperationException (unconfigured settings) must
        // NOT be confused with a busy rejection, and must not permanently wedge the
        // orchestrator — a subsequent, well-formed call must still succeed.
        var (orchestrator, _, settingsStore) = CreateOrchestrator(settings: UnconfiguredSettings);

        var ex = Assert.Throws<InvalidOperationException>(() => orchestrator.ToggleToRigMode());
        Assert.IsNotType<ToggleInProgressException>(ex);

        settingsStore.Save(ConfiguredSettings);

        var result = orchestrator.ToggleToRigMode();
        Assert.True(result.Success);
    }

    [Fact]
    public void ToggleInProgressException_IsAssignableToInvalidOperationException()
    {
        // D-05: this is what makes MainForm.BtnToggle_Click's existing
        // catch (Exception ex) block surface a busy-rejection with zero UI changes.
        var exception = new ToggleInProgressException("A toggle is already in progress.");

        Assert.IsAssignableFrom<InvalidOperationException>(exception);
    }

    [Fact]
    public void RunGuarded_SavesAndClearsMarker_AroundSuccessfulToggle()
    {
        // DISPLAY-13: the crash marker is saved at the start of every guarded toggle
        // and cleared in the finally on clean completion — surviving everything
        // except a real process kill.
        var (orchestrator, callLog, _) = CreateOrchestrator();

        orchestrator.ToggleToRigMode();

        var saveIndex = callLog.IndexOf("marker.Save");
        var clearIndex = callLog.IndexOf("marker.Clear");
        Assert.True(saveIndex >= 0, "Expected marker.Save to be recorded.");
        Assert.True(clearIndex >= 0, "Expected marker.Clear to be recorded.");
        Assert.True(saveIndex < clearIndex, "marker.Save must precede marker.Clear.");
    }

    [Fact]
    public void RunGuarded_ReleasesFlag_EvenWhenMarkerClearThrows()
    {
        // CR-01 (code review): a marker-cleanup I/O failure (AV lock, sharing
        // violation, permissions) must never wedge the busy flag — a subsequent,
        // well-formed call must still succeed rather than throwing
        // ToggleInProgressException forever.
        var (orchestrator, _, _) = CreateOrchestrator(markerStore: new ThrowingClearToggleInProgressStore());

        var firstResult = orchestrator.ToggleToRigMode();
        Assert.True(firstResult.Success);

        var secondResult = orchestrator.ToggleToNormalMode();
        Assert.True(secondResult.Success);
    }

    [Fact]
    public void IsModeKnown_IsPassThrough_TrueOnceAModeHasBeenWritten()
    {
        var (orchestrator, _, _) = CreateOrchestrator();

        Assert.False(orchestrator.IsModeKnown());

        orchestrator.ToggleToRigMode();

        Assert.True(orchestrator.IsModeKnown());
    }
}
