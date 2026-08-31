using RigToggle.Core;
using RigToggle.Core.Models;
using RigToggle.Tests.Doubles;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves ToggleService's IModeStore-backed mode tracking (DISPLAY-11), the explicit
/// symmetric monitor-set application on both toggle directions (DISPLAY-10), the
/// mode-write-only-after-success timing, and the shared CR-01 reconcile safety net —
/// all against hand-written recording doubles, no Windows dependency, no mocking
/// framework.
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

    private (ToggleService Service, List<string> CallLog, InMemoryModeStore ModeStore) CreateService(
        AppSettings? settings = null,
        bool monitorThrowsOnDisable = false,
        bool monitorMutatesBeforeThrowingOnDisable = false,
        bool appThrowsOnMinimize = false,
        bool audioDeviceMissing = false,
        IReadOnlyList<string>? liveDevicePaths = null,
        bool monitorThrowsOnGetAllMonitors = false)
    {
        var callLog = new List<string>();
        var settingsStore = new InMemorySettingsStore(settings ?? ConfiguredSettings);
        var modeStore = new InMemoryModeStore(callLog);
        var monitorController = new FakeMonitorController(
            callLog,
            throwOnDisable: monitorThrowsOnDisable,
            mutatesBeforeThrowingOnDisable: monitorMutatesBeforeThrowingOnDisable,
            liveDevicePaths: liveDevicePaths,
            throwOnGetAllMonitors: monitorThrowsOnGetAllMonitors);
        var audioController = new FakeAudioController(callLog, deviceExists: !audioDeviceMissing);
        var appController = new FakeAppController(callLog, throwOnMinimize: appThrowsOnMinimize);

        var service = new ToggleService(settingsStore, modeStore, monitorController, audioController, appController);
        return (service, callLog, modeStore);
    }

    [Fact]
    public void ToggleToRigMode_WritesModeOnlyAfterMonitorStepSucceeds()
    {
        // Pattern 2 (16-RESEARCH.md): unlike the retired pre-mutation snapshot save,
        // the mode flag is now written AFTER the Monitor step's mutation calls
        // succeed, and before the Audio/App steps run.
        var (service, callLog, modeStore) = CreateService();

        service.ToggleToRigMode();

        var deactivateIndex = callLog.IndexOf(callLog.First(entry => entry.StartsWith("monitor.DeactivateMonitors")));
        var saveIndex = callLog.IndexOf("mode.Save");
        var audioIndex = callLog.IndexOf(callLog.First(entry => entry.StartsWith("audio.SetDefault")));

        Assert.True(saveIndex >= 0, "Expected mode.Save to be recorded.");
        Assert.True(deactivateIndex >= 0, "Expected monitor.DeactivateMonitors to be recorded.");
        Assert.True(saveIndex > deactivateIndex, "mode.Save must occur only after the Monitor step's mutation calls succeed, not before.");
        Assert.True(saveIndex < audioIndex, "mode.Save must occur before the Audio step runs.");
        Assert.Equal(ToggleMode.Rig, modeStore.TryLoad());
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
    public void ToggleToNormalMode_SetsIsInRigModeFalse_AfterSuccess()
    {
        var (service, _, modeStore) = CreateService();
        service.ToggleToRigMode();

        var result = service.ToggleToNormalMode();

        Assert.False(service.IsInRigMode());
        Assert.True(result.Success);
        Assert.Equal(ToggleMode.Normal, modeStore.TryLoad());
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
    public void ToggleToNormalMode_AppliesNormalAudioDeviceViaSetDefault_AndAppliesExplicitMonitorSet()
    {
        // AUDIO-04/DISPLAY-10: Normal-mode audio applies via SetDefault(NormalAudioDeviceId),
        // and Normal-mode Monitor applies its own explicit set via Activate-then-Deactivate.
        var (service, callLog, _) = CreateService();
        service.ToggleToRigMode();
        callLog.Clear();

        service.ToggleToNormalMode();

        Assert.Contains(callLog, entry => entry == $"audio.SetDefault:{ConfiguredSettings.NormalAudioDeviceId}");
        Assert.Contains(callLog, entry => entry.StartsWith("monitor.ActivateMonitors"));
        Assert.Contains(callLog, entry => entry.StartsWith("monitor.DeactivateMonitors"));
    }

    [Fact]
    public void ToggleToRigMode_ReturnsFailedMonitorStep_AndNotAttemptedRest_WhenDisableThrows()
    {
        // D-04 stop-on-first-failure: a failed Disable must short-circuit before Audio
        // or App ever run, and be reported (not thrown) as a Failed Monitor step.
        var (service, callLog, modeStore) = CreateService(monitorThrowsOnDisable: true);

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

        // CR-01/Pattern 2 (code review + 16-RESEARCH.md): Disable's pre-mutation guard
        // threw before touching live state — FakeMonitorController.CaptureState()
        // reports the same state before and after, so the shared reconcile helper
        // must leave the mode flag unwritten rather than claim a mode that was never
        // actually reached.
        Assert.False(service.IsInRigMode());
        Assert.Null(modeStore.TryLoad());
    }

    [Fact]
    public void ToggleToRigMode_LeavesModeUnchanged_WhenDisableThrowsAfterPartiallyMutating()
    {
        // CR-01/Pattern 2: unlike the pre-mutation-guard case above, a Disable failure
        // that happens AFTER some real CCD mutation occurred (e.g. ApplyPathInfos
        // succeeded but the verify-and-throw failed) must NOT write a mode the display
        // may not have actually reached. This is the first-ever toggle in this test,
        // so the mode flag must stay unwritten (still unknown), not flip to Rig.
        var (service, callLog, modeStore) = CreateService(monitorMutatesBeforeThrowingOnDisable: true);

        var result = service.ToggleToRigMode();

        Assert.False(result.Success);
        var monitorStep = result.Steps.Single(s => s.StepName == "Monitor");
        Assert.Equal(ToggleStepOutcome.Failed, monitorStep.Outcome);
        Assert.DoesNotContain(callLog, entry => entry == "mode.Save");
        Assert.Null(modeStore.TryLoad());
        Assert.False(service.IsModeKnown(), "Mode must remain unwritten after a Disable failure that may have partially mutated live state.");
    }

    [Fact]
    public void ToggleToNormalMode_LeavesModeUnchanged_WhenDisableThrowsAfterPartiallyMutating()
    {
        // Pattern 2 (16-RESEARCH.md): the shared ReconcileModeAfterMonitorFailure
        // helper now also protects ToggleToNormalMode — previously only Rig mode could
        // reach a guarded DeactivateMonitors failure at all, since Restore() could
        // never leave zero monitors active by construction. Seed a known prior mode
        // (Rig) directly on the double, bypassing a real toggle, since this fake
        // throws on every DeactivateMonitors call once configured to mutate-then-throw
        // — including a same-fixture ToggleToRigMode call.
        var settings = new AppSettings
        {
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            NormalMonitorsToDisable = new List<string> { "\\\\?\\DISPLAY#NORMAL" },
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, callLog, modeStore) = CreateService(settings, monitorMutatesBeforeThrowingOnDisable: true);
        modeStore.Save(ToggleMode.Rig);
        callLog.Clear();

        var result = service.ToggleToNormalMode();

        Assert.False(result.Success);
        var monitorStep = result.Steps.Single(s => s.StepName == "Monitor");
        Assert.Equal(ToggleStepOutcome.Failed, monitorStep.Outcome);
        Assert.DoesNotContain(callLog, entry => entry == "mode.Save");
        Assert.Equal(ToggleMode.Rig, modeStore.TryLoad());
        Assert.True(service.IsInRigMode(), "Mode flag must survive a Normal-mode Disable failure that may have partially mutated live state.");
    }

    [Fact]
    public void ToggleToNormalMode_ReturnsFailedAudioStep_ButStillMinimizes_WhenAudioDeviceGone()
    {
        // AUDIO-04/AUDIO-05: a configured-but-since-removed NormalAudioDeviceId must
        // be reported (not thrown) as a Failed Audio step, and isolate-and-continue
        // still runs MinimizeIfRunning afterward, with the mode flag already flipped
        // to Normal (written right after the Monitor step's earlier success).
        var (service, callLog, modeStore) = CreateService(audioDeviceMissing: true);
        service.ToggleToRigMode();
        callLog.Clear();

        var result = service.ToggleToNormalMode();

        var audioStep = result.Steps.Single(s => s.StepName == "Audio");
        Assert.Equal(ToggleStepOutcome.Failed, audioStep.Outcome);
        Assert.Contains("audio device", audioStep.Reason);
        Assert.False(service.IsInRigMode());
        Assert.Equal(ToggleMode.Normal, modeStore.TryLoad());
        Assert.Contains(callLog, entry => entry.StartsWith("app.MinimizeIfRunning"));
    }

    [Fact]
    public void ToggleToNormalMode_ReturnsFailedAppStep_ButStillFlipsModeToNormal_WhenMinimizeThrows()
    {
        // CR-02 (code review): MinimizeIfRunning is wrapped in try/catch, matching the
        // class's "isolate-and-continue... no step throws" invariant (D-05). Monitor
        // has already succeeded (and the mode flag already flipped to Normal) by this
        // point, so a throwing minimize must be recorded as a Failed App step, not
        // propagate and leave the mode flag stuck.
        var (service, callLog, modeStore) = CreateService(appThrowsOnMinimize: true);
        service.ToggleToRigMode();
        callLog.Clear();

        var result = service.ToggleToNormalMode();

        var appStep = result.Steps.Single(s => s.StepName == "App");
        Assert.Equal(ToggleStepOutcome.Failed, appStep.Outcome);
        Assert.False(service.IsInRigMode());
        Assert.Equal(ToggleMode.Normal, modeStore.TryLoad());
    }

    [Fact]
    public void ToggleToRigMode_RecordsAppFailed_WhenCompanionAppPathMissing()
    {
        // APP-05/D-04: a configured-but-missing app path is no longer a top-level
        // preflight throw — it surfaces as a Failed App step in a full 3-step result,
        // with Monitor and Audio having already run (D-04 requires the checklist to
        // always have all 3 entries), and the mode flag already written since it is
        // set right after the Monitor step succeeds, before Audio/App run.
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
        var (service, _, modeStore) = CreateService(settings);

        var result = service.ToggleToRigMode();

        Assert.Equal(3, result.Steps.Count);
        Assert.False(result.Success);
        Assert.Equal(ToggleStepOutcome.Succeeded, result.Steps.Single(s => s.StepName == "Monitor").Outcome);
        Assert.Equal(ToggleStepOutcome.Succeeded, result.Steps.Single(s => s.StepName == "Audio").Outcome);
        var appStep = result.Steps.Single(s => s.StepName == "App");
        Assert.Equal(ToggleStepOutcome.Failed, appStep.Outcome);
        Assert.Equal(ToggleMode.Rig, modeStore.TryLoad());
    }

    [Fact]
    public void ToggleToNormalMode_RunsAllSteps_EvenWhenNeverInRigMode()
    {
        // DISPLAY-10/11 (16-RESEARCH.md Pattern 4): the old "never was in rig mode,
        // no-op" branch is retired along with the snapshot-based control flow —
        // ToggleToNormalMode now always attempts its explicit Normal-mode set,
        // regardless of prior mode state (this service is public with no enforced
        // precondition; Plan 04's startup check handles "mode is unknown," not a
        // per-call guard inside ToggleService).
        var (service, callLog, modeStore) = CreateService();

        var result = service.ToggleToNormalMode();

        Assert.Equal(3, result.Steps.Count);
        Assert.True(result.Success);
        Assert.Equal(ToggleMode.Normal, modeStore.TryLoad());
        Assert.Contains(callLog, entry => entry.StartsWith("monitor.ActivateMonitors"));
        Assert.Contains(callLog, entry => entry.StartsWith("monitor.DeactivateMonitors"));
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
    public void ToggleToNormalMode_ActivatesEnableSet_BeforeDeactivatingDisableSet()
    {
        // 06-RESEARCH.md Pitfall 2 / DISPLAY-10: Normal mode now mirrors Rig mode's
        // Monitor-step ordering exactly against its own NormalMonitorsToDisable/
        // NormalMonitorsToEnable sets — the old "Restore before re-disabling the
        // enable-set" D-02 asymmetry is retired; both directions now apply symmetric
        // explicit sets via the same ActivateMonitors-then-DeactivateMonitors shape.
        var settings = new AppSettings
        {
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            NormalMonitorsToDisable = new List<string> { "\\\\?\\DISPLAY#NORMAL" },
            NormalMonitorsToEnable = new List<string> { "\\\\?\\DISPLAY#RIG" },
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, callLog, _) = CreateService(settings);
        service.ToggleToRigMode();
        callLog.Clear();

        service.ToggleToNormalMode();

        var activateIndex = callLog.IndexOf(callLog.First(entry => entry.StartsWith("monitor.ActivateMonitors")));
        var deactivateIndex = callLog.IndexOf(callLog.First(entry => entry.StartsWith("monitor.DeactivateMonitors")));

        Assert.True(activateIndex >= 0, "Expected monitor.ActivateMonitors to be recorded.");
        Assert.True(deactivateIndex >= 0, "Expected monitor.DeactivateMonitors to be recorded.");
        Assert.True(activateIndex < deactivateIndex, "ActivateMonitors must run before DeactivateMonitors.");
    }

    // --- Debug session monitor-position-resets-to-de, Symptom 2: WindowsMonitorController's
    // round-5 scoped-activation path is rig-confirmed unreliable for a full Rig/Normal
    // monitor swap (silently reverted by the OS/driver seconds after an apparently-
    // successful apply, or an outright PathChangeException on the reverse direction).
    // ToggleService signals "skip scoped activation, go straight to Extend" via
    // isPartOfMonitorSwap, computed from whether a DeactivateMonitors call follows in the
    // same Monitor step (disableSet.Count > 0). This routing decision is the one part of
    // the fix that is genuinely testable without live CCD hardware -- WindowsMonitorController
    // itself remains rig-verify-only (see its own class remarks). ---

    [Fact]
    public void ToggleToRigMode_MarksActivateAsPartOfSwap_WhenDisableSetNonEmpty()
    {
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

        var activateEntry = callLog.First(entry => entry.StartsWith("monitor.ActivateMonitors"));
        Assert.EndsWith("isPartOfMonitorSwap=True", activateEntry);
    }

    [Fact]
    public void ToggleToRigMode_MarksActivateAsNotPartOfSwap_WhenDisableSetEmpty()
    {
        // D-07 enable-only configuration: no DeactivateMonitors call follows in the same
        // Monitor step, so round-5 scoped activation's flash-avoidance benefit should
        // still apply -- this is the narrower single-target case it was built for.
        var settings = new AppSettings
        {
            MonitorsToDisable = null,
            MonitorsToEnable = new List<string> { "\\\\?\\DISPLAY#RIG" },
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, callLog, _) = CreateService(settings);

        service.ToggleToRigMode();

        var activateEntry = callLog.First(entry => entry.StartsWith("monitor.ActivateMonitors"));
        Assert.EndsWith("isPartOfMonitorSwap=False", activateEntry);
    }

    [Fact]
    public void ToggleToNormalMode_MarksActivateAsPartOfSwap_WhenNormalDisableSetNonEmpty()
    {
        // Reverse-direction call -- this is the exact rig log's PathChangeException
        // repro shape (Normal-mode ActivateMonitors immediately followed by
        // DeactivateMonitors of a different set within the same Monitor step).
        var settings = new AppSettings
        {
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            NormalMonitorsToDisable = new List<string> { "\\\\?\\DISPLAY#NORMAL" },
            NormalMonitorsToEnable = new List<string> { "\\\\?\\DISPLAY#RIG" },
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, callLog, _) = CreateService(settings);
        service.ToggleToRigMode();
        callLog.Clear();

        service.ToggleToNormalMode();

        var activateEntry = callLog.First(entry => entry.StartsWith("monitor.ActivateMonitors"));
        Assert.EndsWith("isPartOfMonitorSwap=True", activateEntry);
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
        var modeStore = new InMemoryModeStore(callLog);
        var monitorController = new FakeMonitorController(callLog);
        var audioController = new FakeAudioController(callLog);
        var appController = new FakeAppController(callLog);

        Assert.Throws<ArgumentNullException>(() =>
            new ToggleService(null!, modeStore, monitorController, audioController, appController));
        Assert.Throws<ArgumentNullException>(() =>
            new ToggleService(settingsStore, null!, monitorController, audioController, appController));
        Assert.Throws<ArgumentNullException>(() =>
            new ToggleService(settingsStore, modeStore, null!, audioController, appController));
        Assert.Throws<ArgumentNullException>(() =>
            new ToggleService(settingsStore, modeStore, monitorController, null!, appController));
        Assert.Throws<ArgumentNullException>(() =>
            new ToggleService(settingsStore, modeStore, monitorController, audioController, null!));
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
        var (service, callLog, modeStore) = CreateService(settings);
        service.ToggleToRigMode();
        callLog.Clear();

        var result = service.ToggleToNormalMode();

        var audioStep = result.Steps.Single(s => s.StepName == "Audio");
        Assert.Equal(ToggleStepOutcome.Skipped, audioStep.Outcome);
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("audio.SetDefault"));
        Assert.Equal(ToggleMode.Normal, modeStore.TryLoad());
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
        var (service, callLog, modeStore) = CreateService(settings);
        service.ToggleToRigMode();
        callLog.Clear();

        var result = service.ToggleToNormalMode();

        Assert.True(result.Success);
        var appStep = result.Steps.Single(s => s.StepName == "App");
        Assert.Equal(ToggleStepOutcome.Skipped, appStep.Outcome);
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("app.MinimizeIfRunning"));
        Assert.Equal(ToggleMode.Normal, modeStore.TryLoad());
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

    // --- Debug session monitor-position-regre, round 18 (item 1a): ToggleService now
    // live-filters MonitorsToDisable/MonitorsToEnable (and the Normal-mode equivalents)
    // against IMonitorController.GetAllMonitors() before ever calling
    // ActivateMonitors/DeactivateMonitors, instead of passing a stale saved device path
    // through unfiltered and letting the whole Monitor step fail on WindowsMonitorController's
    // own opaque "not detected" guard (round 17 rig evidence: SAM748A/DELA0B8). ---

    [Fact]
    public void ToggleToRigMode_FiltersStaleDisableSetEntry_AndStillActivatesLiveEnableSet()
    {
        // A stale entry in MonitorsToDisable ("\\?\DISPLAY#GHOST", never live-enumerated
        // by GetAllMonitors) must not block the still-live MonitorsToDisable/MonitorsToEnable
        // entries from being applied -- per the round-18 design decision, the toggle
        // proceeds with whatever remains live rather than throwing over the ghost entry.
        var settings = new AppSettings
        {
            MonitorsToDisable = new List<string> { "\\\\?\\DISPLAY#PRIMARY", "\\\\?\\DISPLAY#GHOST" },
            MonitorsToEnable = new List<string> { "\\\\?\\DISPLAY#RIG" },
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, callLog, _) = CreateService(settings);

        var result = service.ToggleToRigMode();

        Assert.True(result.Success);
        Assert.Equal(ToggleStepOutcome.Succeeded, result.Steps.Single(s => s.StepName == "Monitor").Outcome);
        Assert.Contains("monitor.DeactivateMonitors:\\\\?\\DISPLAY#PRIMARY", callLog);
        Assert.DoesNotContain(callLog, entry => entry.Contains("GHOST"));
        Assert.Single(result.StaleMonitorsSkipped);
        Assert.Equal("\\\\?\\DISPLAY#GHOST", result.StaleMonitorsSkipped[0]);
    }

    [Fact]
    public void ToggleToRigMode_NoStaleEntries_ReturnsEmptyStaleMonitorsSkipped()
    {
        var (service, _, _) = CreateService();

        var result = service.ToggleToRigMode();

        Assert.Empty(result.StaleMonitorsSkipped);
    }

    [Fact]
    public void ToggleToNormalMode_FiltersStaleNormalDisableSetEntry_AndStillAppliesLiveSet()
    {
        var settings = new AppSettings
        {
            MonitorsToDisable = ConfiguredSettings.MonitorsToDisable,
            NormalMonitorsToDisable = new List<string> { "\\\\?\\DISPLAY#NORMAL", "\\\\?\\DISPLAY#GHOST" },
            NormalMonitorsToEnable = new List<string> { "\\\\?\\DISPLAY#RIG" },
            NormalAudioDeviceId = ConfiguredSettings.NormalAudioDeviceId,
            RigAudioDeviceId = ConfiguredSettings.RigAudioDeviceId,
            CompanionAppPath = ExistingCompanionAppPath,
        };
        var (service, callLog, _) = CreateService(settings);
        service.ToggleToRigMode();
        callLog.Clear();

        var result = service.ToggleToNormalMode();

        Assert.True(result.Success);
        Assert.Contains("monitor.DeactivateMonitors:\\\\?\\DISPLAY#NORMAL", callLog);
        Assert.DoesNotContain(callLog, entry => entry.Contains("GHOST"));
        Assert.Single(result.StaleMonitorsSkipped);
        Assert.Equal("\\\\?\\DISPLAY#GHOST", result.StaleMonitorsSkipped[0]);
    }

    [Fact]
    public void ToggleToRigMode_AllRequestedPathsStale_MonitorStepStillSucceeds_AsANoOp()
    {
        // Degenerate edge case: every configured monitor is stale. ActivateMonitors/
        // DeactivateMonitors both already no-op safely on an empty request set (see
        // WindowsMonitorController's own "EXIT no-op (empty request set)" behavior) --
        // LiveFilterMonitorSets does not need any special-case handling for this, and the
        // Monitor step reports Succeeded (nothing to fail), with every requested path
        // surfaced as stale.
        var (service, callLog, _) = CreateService(liveDevicePaths: Array.Empty<string>());

        var result = service.ToggleToRigMode();

        Assert.Equal(ToggleStepOutcome.Succeeded, result.Steps.Single(s => s.StepName == "Monitor").Outcome);
        Assert.DoesNotContain(callLog, entry => entry.StartsWith("monitor.DeactivateMonitors:\\"));
        Assert.Contains("\\\\?\\DISPLAY#PRIMARY", result.StaleMonitorsSkipped);
    }

    [Fact]
    public void ToggleToRigMode_GetAllMonitorsThrows_DegradesToUnfilteredBehavior_AndStillDeactivatesConfiguredSet()
    {
        // LiveFilterMonitorSets' own defensive fallback: an enumeration hiccup must never
        // block or partially break the toggle -- it degrades to "treat every requested
        // path as live" (the exact pre-round-18 behavior), unfiltered, rather than either
        // throwing itself or silently treating everything as stale.
        var (service, callLog, _) = CreateService(monitorThrowsOnGetAllMonitors: true);

        var result = service.ToggleToRigMode();

        Assert.True(result.Success);
        Assert.Empty(result.StaleMonitorsSkipped);
        Assert.Contains($"monitor.DeactivateMonitors:{string.Join(",", ConfiguredSettings.MonitorsToDisable!)}", callLog);
    }
}
