namespace RigToggle.Core.Models;

/// <summary>
/// Captured default audio playback device for a single Windows audio role (eConsole,
/// eMultimedia, or eCommunications), used to restore that role's default later.
/// </summary>
public sealed record AudioRoleState(string? DeviceId, string? DeviceName);
