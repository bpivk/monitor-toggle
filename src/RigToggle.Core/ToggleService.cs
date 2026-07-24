using RigToggle.Core.Abstractions;

namespace RigToggle.Core;

/// <summary>
/// Orchestrates the snapshot-before-mutate toggle sequence (D-08/ARCHITECTURE.md Pattern 2)
/// entirely through the ISettingsStore/ISnapshotStore/IMonitorController/IAudioController/
/// IAppController interfaces — zero Windows API references live here. Current mode is
/// derived from snapshot-file presence (D-14), not a separate flag. Partial-failure
/// handling (CORE-04) is explicitly out of scope for this phase; the sequence below is
/// linear and unconditional.
/// </summary>
public sealed class ToggleService
{
    private readonly ISettingsStore _settingsStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IMonitorController _monitorController;
    private readonly IAudioController _audioController;
    private readonly IAppController _appController;

    public ToggleService(
        ISettingsStore settingsStore,
        ISnapshotStore snapshotStore,
        IMonitorController monitorController,
        IAudioController audioController,
        IAppController appController)
    {
        _settingsStore = settingsStore;
        _snapshotStore = snapshotStore;
        _monitorController = monitorController;
        _audioController = audioController;
        _appController = appController;
    }

    /// <summary>
    /// Loads settings, captures the current monitor + audio state, saves that snapshot
    /// (BEFORE any mutation — CORE-03), then disables the configured monitor, switches
    /// the default audio device, and launches/focuses the companion app.
    /// </summary>
    public void ToggleToRigMode()
    {
        var settings = _settingsStore.Load();

        if (!IsFullyConfigured(settings))
        {
            // Guard against WR-01: without this check, an unconfigured (null-field)
            // AppSettings would still make it through to _snapshotStore.Save() below,
            // durably persisting a garbage snapshot and flipping IsInRigMode() to true
            // (D-14) even though nothing was actually captured or changed.
            throw new InvalidOperationException(
                "Rig Toggle settings are not fully configured. Open Settings and choose a monitor, both audio devices, and the companion app path before switching to Rig Mode.");
        }

        var monitorState = _monitorController.CaptureState(settings.MonitorDevicePath!);
        var audioState = _audioController.CaptureState();

        // Snapshot MUST be persisted before any mutation call (D-08/CORE-03 guarantee).
        _snapshotStore.Save(new Models.StateSnapshot(monitorState, audioState));

        _monitorController.Disable(settings.MonitorDevicePath!);
        _audioController.SetDefault(settings.RigAudioDeviceId!);
        _appController.LaunchOrFocus(settings.CompanionAppPath!);
    }

    /// <summary>
    /// True when every field ToggleToRigMode/ToggleToNormalMode depend on has been saved
    /// at least once via Settings (mirrors the four fields SettingsForm.ValidateSettingsForm
    /// already requires before enabling Save). Exposed publicly so MainForm can pre-check
    /// before offering "Switch to Rig Mode" at all, rather than relying solely on the
    /// exception thrown by ToggleToRigMode above.
    /// </summary>
    public bool IsSettingsConfigured() => IsFullyConfigured(_settingsStore.Load());

    private static bool IsFullyConfigured(Models.AppSettings settings) =>
        !string.IsNullOrEmpty(settings.MonitorDevicePath)
        && !string.IsNullOrEmpty(settings.NormalAudioDeviceId)
        && !string.IsNullOrEmpty(settings.RigAudioDeviceId)
        && !string.IsNullOrEmpty(settings.CompanionAppPath);

    /// <summary>
    /// Loads the snapshot and restores the monitor and audio state it captured — the
    /// audio restore path always uses IAudioController.Restore, never the forward-mode
    /// device-switch call, which is reserved for the rig-mode path only. Minimizes the
    /// companion app if running, then clears the snapshot last so IsInRigMode() flips
    /// back to false.
    /// </summary>
    public void ToggleToNormalMode()
    {
        var settings = _settingsStore.Load();
        var snapshot = _snapshotStore.Load();

        if (snapshot is not null)
        {
            _monitorController.Restore(snapshot.Monitor);
            _audioController.Restore(snapshot.Audio);
        }

        _appController.MinimizeIfRunning(settings.CompanionAppPath!);

        _snapshotStore.Clear();
    }

    /// <summary>
    /// Current mode is derived from snapshot-file presence (D-14) — no separate
    /// in-memory/persisted flag exists.
    /// </summary>
    public bool IsInRigMode() => _snapshotStore.Exists();
}
