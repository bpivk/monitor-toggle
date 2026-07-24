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
}
