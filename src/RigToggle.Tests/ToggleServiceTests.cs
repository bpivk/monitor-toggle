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
public class ToggleServiceTests : IDisposable
{
    // ToggleToRigMode now preflight-checks File.Exists(CompanionAppPath) (D-05) — the
    // happy-path fixture must point at a path that actually exists on the test host, so
    // create a real (empty) temp file rather than the fictional Program Files path.
    //
    // IN-03 (code review): previously `static readonly`, which created one temp file
    // per test-assembly run that was never deleted, leaking a file into the OS temp
    // directory on every test run. Now an instance field (xunit constructs a fresh
    // ToggleServiceTests instance per test method) paired with IDisposable.Dispose()
    // below, so each test's temp file is cleaned up after that test runs. File.Delete
    // is a no-op if the path is already gone, so Dispose() is always safe to call.
    private readonly string ExistingCompanionAppPath = Path.GetTempFileName();

    private readonly AppSettings ConfiguredSettings;

    public ToggleServiceTests()
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
    }

    public void Dispose() => File.Delete(ExistingCompanionAppPath);

    private (ToggleService Service, List<string> CallLog, InMemorySnapshotStore SnapshotStore) CreateService(
        AppSettings? settings = null,
        bool audioThrowsOnRestore = false,
        bool monitorThrowsOnDisable = false,
        bool monitorMutatesBeforeThrowingOnDisable = false,
        bool appThrowsOnMinimize = false,
        bool audioDeviceMissing = false)
    {
        var callLog = new List<string>();
        var settingsStore = new InMemorySettingsStore(settings ?? ConfiguredSettings);
        var snapshotStore = new InMemorySnapshotStore(callLog);
        var monitorController = new FakeMonitorController(
            callLog,
            throwOnDisable: monitorThrowsOnDisable,
            mutatesBeforeThrowingOnDisable: monitorMutatesBeforeThrowingOnDisable);
        var audioController = new FakeAudioController(callLog, throwOnRestore: audioThrowsOnRestore, deviceExists: !audioDeviceMissing);
        var appController = new FakeAppController(callLog, throwOnMinimize: appThrowsOnMinimize);

        var service = new ToggleService(settingsStore, snapshotStore, monitorController, audioController, appController);
        return (service, callLog, snapshotStore);
    }

    [Fact]
    public void ToggleToRigMode_SavesSnapshotBeforeAnyMutationCall()
    {
        var (service, callLog, _) = CreateService();

        service.ToggleToRigMode();

        var saveIndex = callLog.IndexOf("snapshot.Save");
        var mutationLabels = new[] { "monitor.ActivateMonitors", "monitor.DeactivateMonitors", "audio.SetDefault", "app.LaunchOrFocus" };
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

        Assert.Contains($"monitor.DeactivateMonitors:{string.Join(",", ConfiguredSettings.MonitorsToDisable!)}", callLog);
        Assert.Contains($"audio.SetDefault:{ConfiguredSettings.RigAudioDeviceId}", callLog);
        Assert.Contains($"app.LaunchOrFocus:{ConfiguredSettings.CompanionAppPath}", callLog);
    }

    [Fact]
    public void ToggleToNormalMode_AppliesNormalAudioDeviceViaSetDefault_NeverRestore()
    {
        // AUDIO-04: Normal-mode audio now applies via SetDefault(NormalAudioDeviceId),
        // replacing the old snapshot-based Restore call. Monitor's own restore path
        // (Phase 16 territory) is unaffected and still uses monitor.Restore.
        var (service, callLog, _) = CreateService();
        service.ToggleToRigMode();
        callLog.Clear();

        service.ToggleToNormalMode();

        Assert.Contains(callLog, entry => entry == $"audio.SetDefault:{ConfiguredSettings.NormalAudioDeviceId}");
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("audio.Restore"));
        Assert.Contains(callLog, entry => entry.StartsWith("monitor.Restore"));
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

        // CR-01 (code review): Disable's pre-mutation guard threw before touching live
        // state — FakeMonitorController.CaptureState() reports the same state before and
        // after, so the just-saved snapshot must be cleared rather than left behind.
        // Leaving it would flip IsInRigMode() to true even though nothing was mutated.
        Assert.False(service.IsInRigMode());
    }

    [Fact]
    public void ToggleToRigMode_KeepsSnapshot_WhenDisableThrowsAfterPartiallyMutating()
    {
        // CR-01 (code review): unlike the pre-mutation-guard case above, a Disable
        // failure that happens AFTER some real CCD mutation occurred (e.g. ApplyPathInfos
        // succeeded but the verify-and-throw failed) must NOT clear the snapshot — the
        // display may genuinely be in a different state now, and a retry/manual restore
        // needs the pre-toggle snapshot to recover from it.
        var (service, _, _) = CreateService(monitorMutatesBeforeThrowingOnDisable: true);

        var result = service.ToggleToRigMode();

        Assert.False(result.Success);
        var monitorStep = result.Steps.Single(s => s.StepName == "Monitor");
        Assert.Equal(ToggleStepOutcome.Failed, monitorStep.Outcome);
        Assert.True(service.IsInRigMode(), "Snapshot must survive a Disable failure that may have partially mutated live state.");
    }

    [Fact]
    public void ToggleToNormalMode_ReturnsFailedAudioStep_ButStillMinimizesAndClears_WhenAudioDeviceGone()
    {
        // AUDIO-04/AUDIO-05, replaces the old "audio restore throws" coverage now that
        // Normal-mode audio no longer calls Restore: a configured-but-since-removed
        // NormalAudioDeviceId must be reported (not thrown) as a Failed Audio step, and
        // isolate-and-continue still runs MinimizeIfRunning + Clear afterward, still
        // flipping IsInRigMode() back to false.
        var (service, callLog, _) = CreateService(audioDeviceMissing: true);
        service.ToggleToRigMode();
        callLog.Clear();

        var result = service.ToggleToNormalMode();

        var audioStep = result.Steps.Single(s => s.StepName == "Audio");
        Assert.Equal(ToggleStepOutcome.Failed, audioStep.Outcome);
        Assert.Contains("audio device", audioStep.Reason);
        Assert.False(service.IsInRigMode());
        Assert.Contains(callLog, entry => entry.StartsWith("app.MinimizeIfRunning"));
        Assert.Contains("snapshot.Clear", callLog);
    }

    [Fact]
    public void ToggleToNormalMode_ReturnsFailedAppStep_ButStillClears_WhenMinimizeThrows()
    {
        // CR-02 (code review): MinimizeIfRunning was the only step in
        // ToggleToNormalMode not wrapped in try/catch, contradicting the class's own
        // "isolate-and-continue... no step throws" invariant (D-05). Monitor/Audio have
        // already been restored by this point, so a throwing minimize must be recorded
        // as a Failed App step (not propagate) and snapshot.Clear() must still run —
        // otherwise IsInRigMode() would stay stuck true forever.
        var (service, callLog, _) = CreateService(appThrowsOnMinimize: true);
        service.ToggleToRigMode();
        callLog.Clear();

        var result = service.ToggleToNormalMode();

        var appStep = result.Steps.Single(s => s.StepName == "App");
        Assert.Equal(ToggleStepOutcome.Failed, appStep.Outcome);
        Assert.False(service.IsInRigMode());
        Assert.Contains("snapshot.Clear", callLog);
    }

    [Fact]
    public void ToggleToRigMode_RecordsAppFailed_WhenCompanionAppPathMissing()
    {
        // APP-05/D-04: a configured-but-missing app path is no longer a top-level
        // preflight throw — it surfaces as a Failed App step in a full 3-step result,
        // with Monitor and Audio having already run (D-04 requires the checklist to
        // always have all 3 entries).
        // AppSettings is a class, not a record — `with { ... }` does not compile here,
        // so build a fresh object-initializer copy instead (D-05 regression, 03-PATTERNS.md).
        var settings = new AppSettings
        {
            MonitorDevicePath = ConfiguredSettings.MonitorDevicePath,
            MonitorFriendlyName = ConfiguredSettings.MonitorFriendlyName,
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            NormalAudioDeviceName = ConfiguredSettings.NormalAudioDeviceName,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            RigAudioDeviceName = ConfiguredSettings.RigAudioDeviceName,
            CompanionAppPath = @"C:\nonexistent\MozaCompanion.exe",
        };
        var (service, callLog, _) = CreateService(settings);

        var result = service.ToggleToRigMode();

        Assert.Equal(3, result.Steps.Count);
        Assert.False(result.Success);
        Assert.Equal(ToggleStepOutcome.Succeeded, result.Steps.Single(s => s.StepName == "Monitor").Outcome);
        Assert.Equal(ToggleStepOutcome.Succeeded, result.Steps.Single(s => s.StepName == "Audio").Outcome);
        var appStep = result.Steps.Single(s => s.StepName == "App");
        Assert.Equal(ToggleStepOutcome.Failed, appStep.Outcome);
        Assert.Contains(callLog, entry => entry.StartsWith("snapshot.Save"));
    }

    [Fact]
    public void ToggleToNormalMode_IsNoOp_WhenNeverInRigMode()
    {
        // WR-01 (code review): ToggleToNormalMode is public with no enforced "must be
        // in rig mode" precondition (MainForm happens to gate calls behind
        // IsInRigMode(), but other/future callers are not required to). Calling it
        // when no snapshot ever existed must be a true no-op — it must not minimize
        // the companion app, must not touch the snapshot store's Clear(), and must
        // return an empty (not misleadingly 1-entry) Steps list.
        var (service, callLog, _) = CreateService();

        var result = service.ToggleToNormalMode();

        Assert.Empty(result.Steps);
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("app.MinimizeIfRunning"));
        Assert.DoesNotContain(callLog, entry => entry == "snapshot.Clear");
        Assert.False(service.IsInRigMode());
    }

    [Fact]
    public void IsSettingsConfigured_EnableOnly_ReturnsTrue()
    {
        // D-07: an enable-only configuration (empty/absent MonitorsToDisable, non-empty
        // MonitorsToEnable, both audio devices + app path set) must be considered fully
        // configured — there is no longer a single required MonitorDevicePath.
        var settings = new AppSettings
        {
            MonitorsToDisable = null,
            MonitorsToEnable = new List<string> { "\\\\?\\DISPLAY#RIG" },
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, _, _) = CreateService(settings);

        Assert.True(service.IsSettingsConfigured());
    }

    [Fact]
    public void IsSettingsConfigured_BothSetsEmpty_ReturnsFalse()
    {
        // D-07: even with both audio devices and the app path set, a configuration with
        // no monitors in either set (both null) must NOT be considered fully configured.
        var settings = new AppSettings
        {
            MonitorsToDisable = null,
            MonitorsToEnable = null,
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, _, _) = CreateService(settings);

        Assert.False(service.IsSettingsConfigured());
    }

    [Fact]
    public void ToggleToRigMode_ActivatesEnableSet_BeforeDeactivatingDisableSet()
    {
        // 06-RESEARCH.md Pitfall 2: ActivateMonitors (enable-set) must run before
        // DeactivateMonitors (disable-set) within the single Monitor step, since
        // ApplyTopology(Extend) would otherwise silently undo the disable.
        var settings = new AppSettings
        {
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            MonitorsToEnable = new List<string> { "\\\\?\\DISPLAY#RIG" },
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, callLog, _) = CreateService(settings);

        service.ToggleToRigMode();

        var activateIndex = callLog.IndexOf(callLog.First(entry => entry.StartsWith("monitor.ActivateMonitors")));
        var deactivateIndex = callLog.IndexOf(callLog.First(entry => entry.StartsWith("monitor.DeactivateMonitors")));

        Assert.True(activateIndex >= 0, "Expected monitor.ActivateMonitors to be recorded.");
        Assert.True(deactivateIndex >= 0, "Expected monitor.DeactivateMonitors to be recorded.");
        Assert.True(activateIndex < deactivateIndex, "ActivateMonitors must run before DeactivateMonitors.");
    }

    [Fact]
    public void ToggleToNormalMode_RestoresBeforeReDisablingEnableSet()
    {
        // D-02: the enable-set is unconditionally re-disabled (DeactivateMonitors) AFTER
        // the disable-set snapshot restore (Restore), not before — same Pitfall 2
        // ordering constraint as the rig-mode Monitor step.
        var settings = new AppSettings
        {
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            MonitorsToEnable = new List<string> { "\\\\?\\DISPLAY#RIG" },
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, callLog, _) = CreateService(settings);
        service.ToggleToRigMode();
        callLog.Clear();

        service.ToggleToNormalMode();

        var restoreIndex = callLog.IndexOf(callLog.First(entry => entry.StartsWith("monitor.Restore")));
        var deactivateIndex = callLog.IndexOf(callLog.First(entry => entry.StartsWith("monitor.DeactivateMonitors")));

        Assert.True(restoreIndex >= 0, "Expected monitor.Restore to be recorded.");
        Assert.True(deactivateIndex >= 0, "Expected monitor.DeactivateMonitors to be recorded.");
        Assert.True(restoreIndex < deactivateIndex, "Restore must run before the enable-set DeactivateMonitors teardown.");
    }

    [Fact]
    public void Constructor_Throws_WhenAnyDependencyIsNull()
    {
        // WR-04 (code review): fail fast at construction time with an ArgumentNullException
        // (matching the composition-root convention already established by MainForm's own
        // constructor guards) rather than surfacing a far-less-actionable NullReferenceException
        // from deep inside ToggleToRigMode/ToggleToNormalMode later.
        var callLog = new List<string>();
        var settingsStore = new InMemorySettingsStore(ConfiguredSettings);
        var snapshotStore = new InMemorySnapshotStore(callLog);
        var monitorController = new FakeMonitorController(callLog);
        var audioController = new FakeAudioController(callLog);
        var appController = new FakeAppController(callLog);

        Assert.Throws<ArgumentNullException>(() =>
            new ToggleService(null!, snapshotStore, monitorController, audioController, appController));
        Assert.Throws<ArgumentNullException>(() =>
            new ToggleService(settingsStore, null!, monitorController, audioController, appController));
        Assert.Throws<ArgumentNullException>(() =>
            new ToggleService(settingsStore, snapshotStore, null!, audioController, appController));
        Assert.Throws<ArgumentNullException>(() =>
            new ToggleService(settingsStore, snapshotStore, monitorController, null!, appController));
        Assert.Throws<ArgumentNullException>(() =>
            new ToggleService(settingsStore, snapshotStore, monitorController, audioController, null!));
    }

    // --- Phase 15: Optional Audio/App targets (APP-04/APP-05/AUDIO-03/AUDIO-04/AUDIO-05) ---
    // Every optional field gets a paired Skipped (unset) test and a Failed (set-but-broken)
    // test, per RESEARCH.md Pitfall 1/3 — never collapse "never configured" and "configured
    // but now invalid" into the same outcome.

    [Fact]
    public void ToggleToRigMode_SkipsAudio_WhenRigAudioDeviceIdUnset()
    {
        var settings = new AppSettings
        {
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = null,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, callLog, _) = CreateService(settings);

        var result = service.ToggleToRigMode();

        Assert.True(result.Success);
        var audioStep = result.Steps.Single(s => s.StepName == "Audio");
        Assert.Equal(ToggleStepOutcome.Skipped, audioStep.Outcome);
        var appStep = result.Steps.Single(s => s.StepName == "App");
        Assert.Equal(ToggleStepOutcome.Succeeded, appStep.Outcome);
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("audio.SetDefault"));
    }

    [Fact]
    public void ToggleToRigMode_FailsAudio_WhenRigAudioDeviceConfiguredButGone()
    {
        var (service, _, _) = CreateService(audioDeviceMissing: true);

        var result = service.ToggleToRigMode();

        Assert.False(result.Success);
        var audioStep = result.Steps.Single(s => s.StepName == "Audio");
        Assert.Equal(ToggleStepOutcome.Failed, audioStep.Outcome);
        Assert.Contains("audio device", audioStep.Reason);
        var appStep = result.Steps.Single(s => s.StepName == "App");
        Assert.Equal(ToggleStepOutcome.NotAttempted, appStep.Outcome);
    }

    [Fact]
    public void ToggleToRigMode_SkipsApp_WhenCompanionAppPathUnset()
    {
        var settings = new AppSettings
        {
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = null,
        };
        var (service, callLog, _) = CreateService(settings);

        var result = service.ToggleToRigMode();

        Assert.True(result.Success);
        var appStep = result.Steps.Single(s => s.StepName == "App");
        Assert.Equal(ToggleStepOutcome.Skipped, appStep.Outcome);
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("app.LaunchOrFocus"));
    }

    [Fact]
    public void ToggleToNormalMode_SkipsAudio_WhenNormalAudioDeviceIdUnset()
    {
        var settings = new AppSettings
        {
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            NormalAudioDeviceId = null,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, callLog, _) = CreateService(settings);
        service.ToggleToRigMode();
        callLog.Clear();

        var result = service.ToggleToNormalMode();

        var audioStep = result.Steps.Single(s => s.StepName == "Audio");
        Assert.Equal(ToggleStepOutcome.Skipped, audioStep.Outcome);
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("audio.SetDefault"));
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("audio.Restore"));
        Assert.Equal("snapshot.Clear", callLog.Where(entry => entry.StartsWith("snapshot.")).Last());
    }

    [Fact]
    public void ToggleToNormalMode_SkipsApp_WhenCompanionAppPathUnset()
    {
        var settings = new AppSettings
        {
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = null,
        };
        var (service, callLog, _) = CreateService(settings);
        service.ToggleToRigMode();
        callLog.Clear();

        var result = service.ToggleToNormalMode();

        Assert.True(result.Success);
        var appStep = result.Steps.Single(s => s.StepName == "App");
        Assert.Equal(ToggleStepOutcome.Skipped, appStep.Outcome);
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("app.MinimizeIfRunning"));
        Assert.Equal("snapshot.Clear", callLog.Where(entry => entry.StartsWith("snapshot.")).Last());
    }

    [Fact]
    public void ToggleToRigMode_AllThreeStepsPresent_AudioAndAppSkipped_WhenBothUnset()
    {
        // D-04: the result always has exactly 3 steps regardless of what's configured;
        // D-05: audio/app being unset never blocks the toggle.
        var settings = new AppSettings
        {
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            NormalAudioDeviceId = null,
            RigAudioDeviceId = null,
            CompanionAppPath = null,
        };
        var (service, _, _) = CreateService(settings);

        var result = service.ToggleToRigMode();

        Assert.Equal(3, result.Steps.Count);
        Assert.True(result.Success);
        Assert.Equal(ToggleStepOutcome.Succeeded, result.Steps.Single(s => s.StepName == "Monitor").Outcome);
        Assert.Equal(ToggleStepOutcome.Skipped, result.Steps.Single(s => s.StepName == "Audio").Outcome);
        Assert.Equal(ToggleStepOutcome.Skipped, result.Steps.Single(s => s.StepName == "App").Outcome);
    }
}
