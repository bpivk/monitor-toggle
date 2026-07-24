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
/// CaptureState reads all three Windows audio roles (eConsole, eMultimedia,
/// eCommunications) independently (D-02) so restore can be exact per role — a failed
/// read for one role falls back to a null AudioRoleState without aborting the others.
/// </summary>
public sealed class WindowsAudioController : IAudioController
{
    public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var result = new List<AudioDeviceInfo>();

        foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            // WR-03: each MMDevice wraps a native/COM IMMDevice reference that must be
            // disposed individually — the enumerator's own `using` above does not cover it.
            using (device)
            {
                result.Add(new AudioDeviceInfo(device.ID, device.FriendlyName));
            }
        }

        return result;
    }

    // Real read today: captures the current default render endpoint for each of the
    // three Windows audio roles so ToggleService can restore all of them later (D-02).
    // Each role read is independently defensive — a failure on one role (e.g. no
    // default assigned yet) falls back to AudioRoleState(null, null) rather than
    // aborting capture of the other two roles.
    public AudioState CaptureState()
    {
        AudioRoleState consoleState;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            consoleState = new AudioRoleState(device.ID, device.FriendlyName);
        }
        catch (Exception)
        {
            consoleState = new AudioRoleState(null, null);
        }

        AudioRoleState multimediaState;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            multimediaState = new AudioRoleState(device.ID, device.FriendlyName);
        }
        catch (Exception)
        {
            multimediaState = new AudioRoleState(null, null);
        }

        AudioRoleState communicationsState;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);
            communicationsState = new AudioRoleState(device.ID, device.FriendlyName);
        }
        catch (Exception)
        {
            communicationsState = new AudioRoleState(null, null);
        }

        return new AudioState(consoleState, multimediaState, communicationsState);
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
            using MMDevice? device = enumerator.GetDevice(deviceId);
            return device is null ? null : new AudioDeviceInfo(device.ID, device.FriendlyName);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
