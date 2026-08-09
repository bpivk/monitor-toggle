using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Audio playback endpoint enumeration and forward-only default-device switch
/// contract. Implemented by RigToggle.Windows.WindowsAudioController: enumeration
/// (GetPlaybackDevices), per-role capture (CaptureState), forward application
/// (SetDefault), and existence resolution (TryResolveDevice).
/// </summary>
public interface IAudioController
{
    IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();
    AudioState CaptureState();
    void SetDefault(string deviceId);

    /// <summary>
    /// Phase 15/AUDIO-05: cheap existence check, promoted from
    /// WindowsAudioController's existing internal helper, so ToggleService can
    /// detect a configured-but-removed audio device without touching Windows
    /// types. Returns null for both "not found" and "enumerator threw" — same
    /// defensive contract TryResolveDevice already has.
    /// </summary>
    AudioDeviceInfo? TryResolveDevice(string? deviceId);
}
