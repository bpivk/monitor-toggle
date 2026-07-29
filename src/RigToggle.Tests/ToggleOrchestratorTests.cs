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
/// exception (Pitfall 3), unguarded pass-throughs (D-04), and the exception-type
/// contract (D-05) that lets MainForm's existing catch block surface a busy-rejection
/// with zero UI changes. All deterministic — no fixed-duration waits, no timing guess
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
        IMonitorController? monitorController = null)
    {
        var callLog = new List<string>();
        var settingsStore = new InMemorySettingsStore(settings ?? ConfiguredSettings);
        var snapshotStore = new InMemorySnapshotStore(callLog);
        var monitor = monitorController ?? new FakeMonitorController(callLog);
        var audioController = new FakeAudioController(callLog);
        var appController = new FakeAppController(callLog);

        var toggleService = new ToggleService(settingsStore, snapshotStore, monitor, audioController, appController);
        var orchestrator = new ToggleOrchestrator(toggleService);
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
        var (orchestrator, callLog, _) = CreateOrchestrator();
        orchestrator.ToggleToRigMode();
        callLog.Clear();

        var result = orchestrator.ToggleToNormalMode();

        Assert.True(result.Success);
        Assert.Contains(callLog, entry => entry.StartsWith("monitor.Restore"));
        Assert.False(orchestrator.IsInRigMode());
    }

    [Fact]
    public void ToggleToRigMode_RejectsSecondCallWhileFirstInFlight_SameDirection()
    {
        var enteredGuardedRegion = new ManualResetEventSlim(false);
        var releaseFirstCall = new ManualResetEventSlim(false);
        var (orchestrator, _, _) = CreateOrchestrator(
            monitorController: new BlockingMonitorController(enteredGuardedRegion, releaseFirstCall));

        var firstCallTask = Task.Run(() => orchestrator.ToggleToRigMode());
        enteredGuardedRegion.Wait(); // deterministic — no fixed-duration wait, no timing guess

        Assert.Throws<ToggleInProgressException>(() => orchestrator.ToggleToRigMode());

        releaseFirstCall.Set();
        var firstResult = firstCallTask.GetAwaiter().GetResult();
        Assert.True(firstResult.Success);
    }

    [Fact]
    public void ToggleToRigMode_InFlight_RejectsCrossDirectionToggleToNormalMode()
    {
        // D-02: one shared flag guards BOTH directions — a rig-mode toggle in flight
        // must also reject a normal-mode request, not just a same-direction repeat.
        var enteredGuardedRegion = new ManualResetEventSlim(false);
        var releaseFirstCall = new ManualResetEventSlim(false);
        var (orchestrator, _, _) = CreateOrchestrator(
            monitorController: new BlockingMonitorController(enteredGuardedRegion, releaseFirstCall));

        var firstCallTask = Task.Run(() => orchestrator.ToggleToRigMode());
        enteredGuardedRegion.Wait();

        Assert.Throws<ToggleInProgressException>(() => orchestrator.ToggleToNormalMode());

        releaseFirstCall.Set();
        var firstResult = firstCallTask.GetAwaiter().GetResult();
        Assert.True(firstResult.Success);
    }

    [Fact]
    public void IsInRigMode_And_IsSettingsConfigured_ArePassThroughs_CallableWhileToggleInFlight()
    {
        // D-04: pure reads, unguarded — must remain callable (and correct) while a
        // toggle is in flight, mirroring how MainForm.RefreshUi() calls IsInRigMode()
        // immediately after every toggle today.
        var enteredGuardedRegion = new ManualResetEventSlim(false);
        var releaseFirstCall = new ManualResetEventSlim(false);
        var (orchestrator, _, _) = CreateOrchestrator(
            monitorController: new BlockingMonitorController(enteredGuardedRegion, releaseFirstCall));

        var firstCallTask = Task.Run(() => orchestrator.ToggleToRigMode());
        enteredGuardedRegion.Wait();

        // ToggleService saves the snapshot BEFORE the Monitor mutation step runs, so by
        // the time DeactivateMonitors is blocked, IsInRigMode() already reports true.
        Assert.True(orchestrator.IsInRigMode());
        Assert.True(orchestrator.IsSettingsConfigured());

        releaseFirstCall.Set();
        var firstResult = firstCallTask.GetAwaiter().GetResult();
        Assert.True(firstResult.Success);
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
}
