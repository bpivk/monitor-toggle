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
    // ToggleToRigMode now preflight-checks File.Exists(CompanionAppPath) (D-05) — the
    // happy-path fixture must point at a path that actually exists on the test host, so
    // create a real (empty) temp file rather than the fictional Program Files path.
    private static readonly string ExistingCompanionAppPath = Path.GetTempFileName();

    private static readonly AppSettings ConfiguredSettings = new()
    {
        MonitorDevicePath = "\\\\?\\DISPLAY#PRIMARY",
        MonitorFriendlyName = "Primary Monitor",
        NormalAudioDeviceId = "normal-device-id",
        NormalAudioDeviceName = "Headset",
        RigAudioDeviceId = "rig-device-id",
        RigAudioDeviceName = "Rig Speakers",
        CompanionAppPath = ExistingCompanionAppPath,
    };

    private static (ToggleService Service, List<string> CallLog, InMemorySnapshotStore SnapshotStore) CreateService(
        AppSettings? settings = null,
        bool audioThrowsOnRestore = false,
        bool monitorThrowsOnDisable = false)
    {
        var callLog = new List<string>();
        var settingsStore = new InMemorySettingsStore(settings ?? ConfiguredSettings);
        var snapshotStore = new InMemorySnapshotStore(callLog);
        var monitorController = new FakeMonitorController(callLog, throwOnDisable: monitorThrowsOnDisable);
        var audioController = new FakeAudioController(callLog, throwOnRestore: audioThrowsOnRestore);
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

        var result = service.ToggleToRigMode();

        Assert.True(service.IsInRigMode());
        Assert.True(result.Success);
        Assert.Equal(3, result.Steps.Count);
        Assert.All(result.Steps, step => Assert.Equal(ToggleStepOutcome.Succeeded, step.Outcome));
    }

    [Fact]
    public void ToggleToNormalMode_SetsIsInRigModeFalse_AndClearIsLastSnapshotInteraction()
    {
        var (service, callLog, _) = CreateService();
        service.ToggleToRigMode();

        var result = service.ToggleToNormalMode();

        Assert.False(service.IsInRigMode());
        Assert.True(result.Success);
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

    [Fact]
    public void ToggleToNormalMode_StillMinimizesAndClears_WhenAudioRestoreThrows()
    {
        // Gap-closure 03-04 (T-03-04-02): a throwing audio Restore must not abort the
        // rest of ToggleToNormalMode — MinimizeIfRunning (APP-03) and snapshot Clear must
        // still run, and IsInRigMode() must still flip back to false, so the app never
        // gets permanently stuck reporting Rig mode.
        var (service, callLog, _) = CreateService(audioThrowsOnRestore: true);
        service.ToggleToRigMode();
        callLog.Clear();

        service.ToggleToNormalMode();

        Assert.Contains(callLog, entry => entry.StartsWith("app.MinimizeIfRunning:"));
        Assert.Contains("snapshot.Clear", callLog);
        Assert.False(service.IsInRigMode());
    }

    [Fact]
    public void ToggleToRigMode_ReturnsFailedMonitorStep_AndNotAttemptedRest_WhenDisableThrows()
    {
        // D-04 stop-on-first-failure: a failed Disable must short-circuit before Audio
        // or App ever run, and be reported (not thrown) as a Failed Monitor step.
        var (service, callLog, _) = CreateService(monitorThrowsOnDisable: true);

        var result = service.ToggleToRigMode();

        Assert.False(result.Success);
        Assert.Equal(3, result.Steps.Count);

        var monitorStep = result.Steps.Single(s => s.StepName == "Monitor");
        Assert.Equal(ToggleStepOutcome.Failed, monitorStep.Outcome);
        Assert.Contains("Fake monitor disable failure", monitorStep.Reason);

        var audioStep = result.Steps.Single(s => s.StepName == "Audio");
        var appStep = result.Steps.Single(s => s.StepName == "App");
        Assert.Equal(ToggleStepOutcome.NotAttempted, audioStep.Outcome);
        Assert.Equal(ToggleStepOutcome.NotAttempted, appStep.Outcome);

        Assert.DoesNotContain(callLog, entry => entry.StartsWith("audio.SetDefault"));
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("app.LaunchOrFocus"));
    }

    [Fact]
    public void ToggleToNormalMode_ReturnsFailedAudioStep_ButStillClears_WhenAudioRestoreThrows()
    {
        // Gap-closure 03-04 (T-03-04-02), now asserted via the ToggleResult contract:
        // a throwing audio Restore must still be reported (not thrown) as a Failed
        // Audio step, and MinimizeIfRunning + Clear must still run afterward.
        var (service, callLog, _) = CreateService(audioThrowsOnRestore: true);
        service.ToggleToRigMode();
        callLog.Clear();

        var result = service.ToggleToNormalMode();

        var audioStep = result.Steps.Single(s => s.StepName == "Audio");
        Assert.Equal(ToggleStepOutcome.Failed, audioStep.Outcome);
        Assert.False(service.IsInRigMode());
        Assert.Contains(callLog, entry => entry.StartsWith("app.MinimizeIfRunning"));
        Assert.Contains("snapshot.Clear", callLog);
    }

    [Fact]
    public void ToggleToRigMode_Throws_WhenCompanionAppPathDoesNotExist()
    {
        // AppSettings is a class, not a record — `with { ... }` does not compile here,
        // so build a fresh object-initializer copy instead (D-05 regression, 03-PATTERNS.md).
        var settings = new AppSettings
        {
            MonitorDevicePath = ConfiguredSettings.MonitorDevicePath,
            MonitorFriendlyName = ConfiguredSettings.MonitorFriendlyName,
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            NormalAudioDeviceName = ConfiguredSettings.NormalAudioDeviceName,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            RigAudioDeviceName = ConfiguredSettings.RigAudioDeviceName,
            CompanionAppPath = @"C:\nonexistent\MozaCompanion.exe",
        };
        var (service, callLog, _) = CreateService(settings);

        Assert.Throws<InvalidOperationException>(() => service.ToggleToRigMode());
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("snapshot.Save"));
    }
}
