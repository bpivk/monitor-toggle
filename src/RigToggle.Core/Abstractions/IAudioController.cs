using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Audio playback endpoint enumeration and default-device switch/restore contract.
/// Implemented by RigToggle.Windows.WindowsAudioController. Read methods
/// (GetPlaybackDevices, CaptureState) are real starting Phase 2; SetDefault/Restore
/// are no-op stubs until Phase 3 (02-RESEARCH.md Pattern 1).
/// </summary>
public interface IAudioController
{
    IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();
    AudioState CaptureState();
    void SetDefault(string deviceId);
    void Restore(AudioState previousState);

    /// <summary>
    /// Phase 15/AUDIO-05: cheap existence check, promoted from
    /// WindowsAudioController's existing internal helper, so ToggleService can
    /// detect a configured-but-removed audio device without touching Windows
    /// types. Returns null for both "not found" and "enumerator threw" — same
    /// defensive contract TryResolveDevice already has.
    /// </summary>
    AudioDeviceInfo? TryResolveDevice(string? deviceId);
}
