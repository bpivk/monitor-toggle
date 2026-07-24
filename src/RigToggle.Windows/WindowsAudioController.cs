using NAudio.CoreAudioApi;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Windows;

/// <summary>
/// Real audio render-endpoint enumeration and default-device read via NAudio's
/// MMDeviceEnumerator. SetDefault/Restore are documented no-op stubs until Phase 3
/// fills in the real IPolicyConfig COM interop mutation (02-RESEARCH.md Pattern 1).
/// A fresh MMDeviceEnumerator is created and released per call — never cached across
/// the session (02-RESEARCH.md Anti-Patterns, Pitfall/T-02-COMLEAK).
/// </summary>
public sealed class WindowsAudioController : IAudioController
{
    public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var result = new List<AudioDeviceInfo>();

        foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            result.Add(new AudioDeviceInfo(device.ID, device.FriendlyName));
        }

        return result;
    }

    // Real read today: captures the current default render endpoint so ToggleService
    // can restore it later. Defensive try/catch covers the case where no default
    // device can be determined at capture time.
    public AudioState CaptureState()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return new AudioState(defaultDevice.ID);
        }
        catch (Exception)
        {
            return new AudioState(null);
        }
    }

    public void SetDefault(string deviceId)
    {
        // FAKE in Phase 2 — no-op. Real IPolicyConfig COM interop
        // (IPolicyConfig::SetDefaultEndpoint) lands in Phase 3.
    }

    public void Restore(AudioState previousState)
    {
        // FAKE in Phase 2 — no-op. Real IPolicyConfig COM interop restore lands in Phase 3.
    }

    /// <summary>
    /// Defensive resolution helper for a saved device ID that may no longer exist
    /// (device unplugged/renamed since last save). NAudio's MMDeviceEnumerator.GetDevice
    /// throw-vs-null behavior on a missing ID was not independently confirmed in
    /// 02-RESEARCH.md (Pitfall 2 / Assumptions Log A2) — guarded with both a null-check
    /// and a broad try/catch so either behavior yields a graceful miss (feeds D-10 in
    /// Settings), never an unhandled exception.
    /// </summary>
    public AudioDeviceInfo? TryResolveDevice(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return null;
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            MMDevice? device = enumerator.GetDevice(deviceId);
            return device is null ? null : new AudioDeviceInfo(device.ID, device.FriendlyName);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
