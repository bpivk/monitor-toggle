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

        var monitorState = _monitorController.CaptureState(settings.MonitorDevicePath!);
        var audioState = _audioController.CaptureState();

        // Snapshot MUST be persisted before any mutation call (D-08/CORE-03 guarantee).
        _snapshotStore.Save(new Models.StateSnapshot(monitorState, audioState));

        _monitorController.Disable(settings.MonitorDevicePath!);
        _audioController.SetDefault(settings.RigAudioDeviceId!);
        _appController.LaunchOrFocus(settings.CompanionAppPath!);
    }

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
