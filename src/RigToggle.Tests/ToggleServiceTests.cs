using RigToggle.Core;
using RigToggle.Core.Models;
using RigToggle.Tests.Doubles;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves ToggleService's snapshot-before-mutate sequencing (D-08/CORE-03), mode
/// derivation from snapshot presence (D-14), and the symmetric monitor/audio restore
/// contract on the normal-mode path — all against hand-written recording doubles, no
/// Windows dependency, no mocking framework.
/// </summary>
public class ToggleServiceTests
{
    private static readonly AppSettings ConfiguredSettings = new()
    {
        MonitorDevicePath = "\\\\?\\DISPLAY#PRIMARY",
        MonitorFriendlyName = "Primary Monitor",
        NormalAudioDeviceId = "normal-device-id",
        NormalAudioDeviceName = "Headset",
        RigAudioDeviceId = "rig-device-id",
        RigAudioDeviceName = "Rig Speakers",
        CompanionAppPath = @"C:\Program Files\Moza\MozaCompanion.exe",
    };

    private static (ToggleService Service, List<string> CallLog, InMemorySnapshotStore SnapshotStore) CreateService(
        AppSettings? settings = null)
    {
        var callLog = new List<string>();
        var settingsStore = new InMemorySettingsStore(settings ?? ConfiguredSettings);
        var snapshotStore = new InMemorySnapshotStore(callLog);
        var monitorController = new FakeMonitorController(callLog);
        var audioController = new FakeAudioController(callLog);
        var appController = new FakeAppController(callLog);

        var service = new ToggleService(settingsStore, snapshotStore, monitorController, audioController, appController);
        return (service, callLog, snapshotStore);
    }

    [Fact]
    public void ToggleToRigMode_SavesSnapshotBeforeAnyMutationCall()
    {
        var (service, callLog, _) = CreateService();

        service.ToggleToRigMode();

        var saveIndex = callLog.IndexOf("snapshot.Save");
        var mutationLabels = new[] { "monitor.Disable", "audio.SetDefault", "app.LaunchOrFocus" };
        var firstMutationIndex = callLog.FindIndex(entry => mutationLabels.Any(entry.StartsWith));

        Assert.True(saveIndex >= 0, "Expected snapshot.Save to be recorded.");
        Assert.True(firstMutationIndex >= 0, "Expected at least one mutation call to be recorded.");
        Assert.True(saveIndex < firstMutationIndex, "snapshot.Save must precede every mutation call.");
    }

    [Fact]
    public void ToggleToRigMode_SetsIsInRigModeTrue()
    {
        var (service, _, _) = CreateService();

        service.ToggleToRigMode();

        Assert.True(service.IsInRigMode());
    }

    [Fact]
    public void ToggleToNormalMode_SetsIsInRigModeFalse_AndClearIsLastSnapshotInteraction()
    {
        var (service, callLog, _) = CreateService();
        service.ToggleToRigMode();

        service.ToggleToNormalMode();

        Assert.False(service.IsInRigMode());
        var snapshotInteractions = callLog.Where(entry => entry.StartsWith("snapshot.")).ToList();
        Assert.Equal("snapshot.Clear", snapshotInteractions.Last());
    }

    [Fact]
    public void ToggleToRigMode_PassesSettingsDerivedValuesToMutationCalls()
    {
        var (service, callLog, _) = CreateService();

        service.ToggleToRigMode();

        Assert.Contains($"monitor.Disable:{ConfiguredSettings.MonitorDevicePath}", callLog);
        Assert.Contains($"audio.SetDefault:{ConfiguredSettings.RigAudioDeviceId}", callLog);
        Assert.Contains($"app.LaunchOrFocus:{ConfiguredSettings.CompanionAppPath}", callLog);
    }

    [Fact]
    public void ToggleToNormalMode_RestoresAudioViaRestore_NeverSetDefault()
    {
        var (service, callLog, _) = CreateService();
        service.ToggleToRigMode();
        callLog.Clear();

        service.ToggleToNormalMode();

        Assert.Contains(callLog, entry => entry.StartsWith("audio.Restore:"));
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("audio.SetDefault"));
    }
}
