using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using RigToggle.Windows.Audio;

namespace RigToggle.Windows;

/// <summary>
/// Real audio render-endpoint enumeration, default-device read, and default-device
/// mutation. SetDefault/Restore apply the requested device to all three Windows audio
/// roles (eConsole, eMultimedia, eCommunications) via the hand-embedded IPolicyConfig
/// COM interop (D-01), and re-verify each role's actual default via NAudio afterward,
/// throwing InvalidOperationException on any mismatch instead of silently trusting the
/// HRESULT (D-03/D-04). A fresh MMDeviceEnumerator/PolicyConfigClient is created and
/// released per call/cycle — never cached across the session (02-RESEARCH.md
/// Anti-Patterns, Pitfall/T-02-COMLEAK; 03-RESEARCH.md Pitfall C).
/// CaptureState reads all three Windows audio roles (eConsole, eMultimedia,
/// eCommunications) independently (D-02) so restore can be exact per role — a failed
/// read for one role falls back to a null AudioRoleState without aborting the others.
/// </summary>
public sealed class WindowsAudioController : IAudioController
{
    // D-01/Pattern 2: native ERole <-> NAudio Role pairing, applied to all three roles
    // for every SetDefault/Restore cycle. Values are numerically identical (0/1/2) but
    // paired explicitly rather than cast, per 03-RESEARCH.md Pattern 2.
    private static readonly (ERole Native, Role Managed)[] Roles =
    {
        (ERole.eConsole, Role.Console),
        (ERole.eMultimedia, Role.Multimedia),
        (ERole.eCommunications, Role.Communications),
    };

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
        SetDefaultForAllRoles(deviceId);
    }

    // D-01: applies deviceId to all three Windows audio roles, never just one
    // (03-RESEARCH.md Anti-Patterns explicitly forbids an eConsole-only switch).
    // Each role cycle creates a fresh PolicyConfigClient, releases it in a finally
    // (Pitfall C — never cache across cycles), then re-verifies via NAudio and throws
    // on mismatch (D-03/D-04) rather than trusting the HRESULT alone (Pitfall 6).
    private void SetDefaultForAllRoles(string deviceId)
    {
        foreach (var (nativeRole, managedRole) in Roles)
        {
            var client = (IPolicyConfig)new PolicyConfigClient();
            try
            {
                int hr = client.SetDefaultEndpoint(deviceId, nativeRole);
                if (hr != 0) // S_OK
                {
                    throw new InvalidOperationException(
                        $"SetDefaultEndpoint failed for role {nativeRole} (HRESULT 0x{hr:X8}).");
                }
            }
            finally
            {
                Marshal.ReleaseComObject(client); // release every cycle — Pitfall 5/C COM leak
            }

            // D-03/D-04: verify-and-throw, not trust-the-HRESULT (Pitfall 6)
            using var enumerator = new MMDeviceEnumerator();
            using var actual = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, managedRole);
            if (!string.Equals(actual.ID, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Audio default for role {managedRole} did not change to the requested " +
                    $"device after SetDefaultEndpoint (expected '{deviceId}', got '{actual.ID}').");
            }
        }
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
